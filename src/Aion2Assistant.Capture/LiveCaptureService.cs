using System.Collections.Concurrent;
using System.Net;
using Aion2Assistant.Core.Localization;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Aion2Assistant.Capture;

/// <summary>
/// Capture live : toutes les 2 s, cherche la connexion du jeu, ouvre la bonne carte Npcap et ajuste le filtre.
/// Les paquets passent par une file bornée vers un thread de traitement qui alimente le pipeline.
/// Verrous : _stateLock (carte, filtre) et _writerLock (enregistrement) ; le rappel de capture ne prend que _writerLock,
/// et StopCapture n'est jamais appelé sous un verrou.
/// </summary>
public sealed class LiveCaptureService(CapturePipeline pipeline) : IDisposable
{
    private const int QueueCapacity = 100_000;
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(2);

    private sealed class OpenFailedException(string message) : Exception(message);

    private readonly object _stateLock = new();
    private readonly object _writerLock = new();
    private readonly BlockingCollection<TcpSegment> _queue = new(QueueCapacity);
    private readonly CancellationTokenSource _cts = new();
    private LibPcapLiveDevice? _device;
    private string? _filter;
    private CaptureFileWriterDevice? _writer;
    private Timer? _timer;
    private Thread? _worker;
    private long _dropped;
    private int _started;
    private int _refreshing;
    private int _disposedFlag;

    private bool Disposed => Volatile.Read(ref _disposedFlag) != 0;

    public CaptureStatus Status { get; private set; } = new(CaptureState.GameNotRunning, Strings.CaptureStatusStarting);
    public event Action<CaptureStatus>? StatusChanged;
    public event Action<string>? Diagnostic;

    public long DroppedSegments => Interlocked.Read(ref _dropped);
    public bool IsRecording => Volatile.Read(ref _writer) is not null;
    public string? RecordingPath { get; private set; }

    public void Start()
    {
        if (Disposed || Interlocked.Exchange(ref _started, 1) == 1) return;
        if (!NpcapProbe.IsInstalled())
        {
            SetStatus(new CaptureStatus(CaptureState.NpcapMissing,
                Strings.CaptureStatusNpcapMissing));
            return;
        }
        lock (_stateLock)
        {
            if (Disposed) return;
            _worker = new Thread(ProcessLoop) { IsBackground = true, Name = "Aion2Assistant capture" };
            _worker.Start();
            _timer = new Timer(_ => Refresh(), null, TimeSpan.Zero, RefreshPeriod);
        }
    }

    public bool StartRecording(string path)
    {
        CaptureFileWriterDevice? writer = null;
        var installed = false;
        try
        {
            PacketDotNet.LinkLayers? link;
            lock (_stateLock) link = _device?.LinkType;
            if (link is null) return false;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            writer = new CaptureFileWriterDevice(path);
            writer.Open(new DeviceConfiguration { LinkLayerType = link.Value });
            lock (_writerLock)
            {
                // Revérifie : la carte a pu être fermée (ou le service libéré) pendant l'ouverture du fichier.
                bool deviceOpen;
                lock (_stateLock) deviceOpen = !Disposed && _device is not null;
                if (_writer is null && deviceOpen)
                {
                    _writer = writer;
                    RecordingPath = path;
                    installed = true;
                }
            }
            if (!installed) return false;
            Raise($"Enregistrement démarré : {path}");
            return true;
        }
        catch (Exception ex)
        {
            Raise("Enregistrement impossible : " + ex.Message);
            return false;
        }
        finally
        {
            if (!installed) DisposeWriter(writer);
        }
    }

    public void StopRecording() => StopRecordingCore("Enregistrement terminé");

    private void StopRecordingCore(string message)
    {
        string? path;
        lock (_writerLock)
        {
            if (_writer is null) return;
            DisposeWriter(_writer);
            _writer = null;
            path = RecordingPath;
        }
        Raise($"{message} : {path}");
    }

    private static void DisposeWriter(CaptureFileWriterDevice? writer)
    {
        if (writer is null) return;
        try { writer.Close(); } catch (Exception) { /* fermeture au mieux */ }
        try { writer.Dispose(); } catch (Exception) { /* fermeture au mieux */ }
    }

    private void Refresh()
    {
        if (Disposed || Interlocked.Exchange(ref _refreshing, 1) == 1) return;
        try
        {
            var pids = GameConnectionLocator.FindGameProcessIds();
            if (pids.Length == 0)
            {
                CloseDevice();
                SetStatus(new CaptureStatus(CaptureState.GameNotRunning, Strings.CaptureStatusGameNotRunning));
                return;
            }

            var connections = GameConnectionLocator.FindConnections(pids);
            if (connections.Count == 0)
            {
                SetStatus(new CaptureStatus(CaptureState.WaitingForConnection, Strings.CaptureStatusWaiting));
                return;
            }

            // Le jeu garde aussi des connexions locales (127.0.0.1, entre ses processus) qui ne portent pas le
            // trafic de combat : la carte loopback ne sert que si AUCUNE connexion n'est externe (accélérateur de ping).
            var external = connections.Where(c => !IPAddress.IsLoopback(c.RemoteIp) && !IPAddress.IsLoopback(c.LocalIp)).ToList();
            if (external.Count > 0) connections = external;

            var device = ChooseDevice(connections);
            if (device is null)
            {
                CloseDevice();
                SetStatus(new CaptureStatus(CaptureState.NoAdapter, Strings.CaptureStatusNoAdapter));
                return;
            }

            string? currentName;
            lock (_stateLock) currentName = _device?.Name;
            if (currentName is not null && currentName != device.Name) CloseDevice();

            var hosts = connections.Select(c => $"(host {c.RemoteIp} and port {c.RemotePort})").Distinct().Order();
            var filter = "tcp and (" + string.Join(" or ", hosts) + ")";
            if (!OpenDevice(device, filter)) return;
            var server = string.Join(", ", connections.Select(c => $"{c.RemoteIp}:{c.RemotePort}").Distinct());
            SetStatus(new CaptureStatus(CaptureState.Capturing, Strings.CaptureStatusActive, device.Description ?? device.Name, server));
        }
        catch (OpenFailedException ex)
        {
            CloseDevice();
            SetStatus(new CaptureStatus(CaptureState.Error,
                Strings.Format(nameof(Strings.CaptureStatusAccessDenied), ex.Message)));
        }
        catch (Exception ex)
        {
            CloseDevice();
            Raise("Erreur de capture : " + ex);
            SetStatus(new CaptureStatus(CaptureState.Error, Strings.Format(nameof(Strings.CaptureStatusError), ex.Message)));
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }

    private LibPcapLiveDevice? ChooseDevice(List<GameConnection> connections)
    {
        // Les cartes déjà ouvertes ne sont pas invalidées : on ne rafraîchit la liste que lorsque rien n'est ouvert.
        bool none;
        lock (_stateLock) none = _device is null;
        if (none) CaptureDeviceList.Instance.Refresh();

        var devices = CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>().ToList();
        foreach (var c in connections)
        {
            if (IPAddress.IsLoopback(c.RemoteIp) || IPAddress.IsLoopback(c.LocalIp))
            {
                var loopback = devices.FirstOrDefault(d =>
                    d.Name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
                    || (d.Description ?? "").Contains("loopback", StringComparison.OrdinalIgnoreCase));
                if (loopback is not null) return loopback;
            }
            else
            {
                var physical = devices.FirstOrDefault(d => d.Addresses.Any(a => c.LocalIp.Equals(a.Addr?.ipAddress)));
                if (physical is not null) return physical;
            }
        }
        return null;
    }

    /// <summary>Faux si le service a été libéré entre-temps.</summary>
    private bool OpenDevice(LibPcapLiveDevice device, string filter)
    {
        string? message = null;
        lock (_stateLock)
        {
            if (Disposed) return false;
            if (_device is null)
            {
                var opened = false;
                try
                {
                    device.Open(new DeviceConfiguration { Mode = DeviceModes.None, ReadTimeout = 100 });
                    opened = true;
                    device.Filter = filter;
                    // SharpPcap exige un abonné avant StartCapture (DeviceNotReadyException sinon) ;
                    // en cas d'échec, le catch le désabonne.
                    device.OnPacketArrival += OnPacketArrival;
                    device.StartCapture();
                }
                catch (Exception ex)
                {
                    try { device.OnPacketArrival -= OnPacketArrival; } catch (Exception) { /* au mieux */ }
                    try { device.Close(); } catch (Exception) { /* au mieux */ }
                    if (!opened && ex is PcapException) throw new OpenFailedException(ex.Message);
                    throw;
                }
                _device = device;
                _filter = filter;
                message = $"Carte ouverte : {device.Description ?? device.Name} — filtre {filter}";
            }
            else if (_filter != filter)
            {
                _device.Filter = filter;
                _filter = filter;
                message = $"Filtre mis à jour : {filter}";
            }
        }
        if (message is not null) Raise(message);
        return true;
    }

    private void CloseDevice()
    {
        LibPcapLiveDevice? device;
        lock (_stateLock)
        {
            device = _device;
            _device = null;
            _filter = null;
        }
        if (device is null) return;

        // Hors de tout verrou : StopCapture attend la fin du rappel de capture en cours.
        try
        {
            device.StopCapture();
        }
        catch (Exception ex)
        {
            Raise("Arrêt de la capture : " + ex.Message);
        }
        finally
        {
            try { device.OnPacketArrival -= OnPacketArrival; } catch (Exception) { /* au mieux */ }
            try
            {
                device.Close();
            }
            catch (Exception ex)
            {
                Raise("Fermeture de la carte : " + ex.Message);
            }
        }
        StopRecordingCore("Enregistrement terminé (carte fermée)");
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        RawCapture raw;
        try
        {
            raw = e.GetPacket();
        }
        catch (Exception)
        {
            return;
        }

        if (Volatile.Read(ref _writer) is not null) WriteRecording(raw);

        try
        {
            if (SegmentReader.TryRead(raw, out var segment) && !_queue.TryAdd(segment!))
                Interlocked.Increment(ref _dropped);
        }
        catch (Exception)
        {
            // Un paquet malformé (ou la file déjà fermée) ne doit jamais arrêter le thread de capture.
        }
    }

    private void WriteRecording(RawCapture raw)
    {
        string? failure = null;
        lock (_writerLock)
        {
            if (_writer is null) return;
            try
            {
                _writer.Write(raw);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                DisposeWriter(_writer);
                _writer = null;
            }
        }
        if (failure is not null) Raise("Enregistrement interrompu : " + failure);
    }

    private void ProcessLoop()
    {
        try
        {
            foreach (var segment in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    pipeline.Process(segment);
                }
                catch (Exception ex)
                {
                    Raise("Erreur de traitement : " + ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // arrêt demandé
        }
        catch (Exception ex)
        {
            Raise("Thread de traitement arrêté : " + ex.Message);
        }
    }

    private void Raise(string message)
    {
        try { Diagnostic?.Invoke(message); } catch (Exception) { /* un abonné ne doit pas casser la capture */ }
    }

    private void SetStatus(CaptureStatus status)
    {
        if (Disposed || status == Status) return;
        Status = status;
        try { StatusChanged?.Invoke(status); } catch (Exception) { /* idem */ }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) == 1) return;

        Timer? timer;
        Thread? worker;
        lock (_stateLock)
        {
            timer = _timer;
            worker = _worker;
        }
        if (timer is not null)
        {
            // L'événement n'est libéré que si le rappel en cours a fini : sinon le runtime
            // le signalerait plus tard sur un handle fermé (ObjectDisposedException non gérée).
            var done = new ManualResetEvent(false);
            if (!timer.Dispose(done) || done.WaitOne(TimeSpan.FromSeconds(5))) done.Dispose();
        }

        CloseDevice();
        StopRecordingCore("Enregistrement terminé");
        _queue.CompleteAdding();
        if (worker is null || worker.Join(TimeSpan.FromSeconds(2)))
        {
            _queue.Dispose();
            _cts.Dispose();
        }
        else
        {
            _cts.Cancel(); // le thread tarde : on ne libère pas la file sous ses pieds
        }
    }
}
