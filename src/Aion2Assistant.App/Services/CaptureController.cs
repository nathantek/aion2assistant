using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Capture;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Relie la capture à l'interface : les événements passent par une file vidée toutes les 250 ms sur le thread UI,
/// les compteurs et le voyant sont rafraîchis chaque seconde.
/// </summary>
internal sealed class CaptureController : IDisposable
{
    private readonly CapturePipeline _pipeline = new();
    private readonly LiveCaptureService _live;
    private readonly CaptureHealthMonitor _health = new();
    private readonly UnknownOpcodeWatch _unknown = new();
    private readonly ConcurrentQueue<GameEvent> _pending = new();
    private readonly DispatcherTimer _drain = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private string? _lastFlow;
    private string? _lastSelf;
    private SelfIdentifiedEvent? _self;
    private long? _selfPower;
    private long _lastSummaryMs;

    public CaptureController()
    {
        View = new CaptureViewModel(ToggleRecording, OpenCapturesFolder);
        _pipeline.EventDecoded += e => _pending.Enqueue(e);
        _live = new LiveCaptureService(_pipeline);
        _live.StatusChanged += s => _dispatcher.BeginInvoke(() => OnStatus(s));
        _live.Diagnostic += message => Log.Information("Capture : {Message}", message);
        _drain.Tick += (_, _) => Drain();
        _refresh.Tick += (_, _) => Refresh();
    }

    public CaptureViewModel View { get; }

    /// <summary>Lots d'événements sur le thread UI (consommés par les meters à l'étape 4).</summary>
    public event Action<IReadOnlyList<GameEvent>>? EventsReceived;

    public void Start()
    {
        _live.Start();
        _drain.Start();
        _refresh.Start();
        Refresh();
    }

    private void OnStatus(CaptureStatus status)
    {
        Log.Information("Capture : {State:l} — {Message}", status.State.ToString(), status.Message);
        View.SetStatus(status);
        Refresh();
    }

    private void Drain()
    {
        var sw = Stopwatch.StartNew();
        var count = 0;
        try
        {
            if (_pending.IsEmpty) return;
            var batch = new List<GameEvent>();
            while (_pending.TryDequeue(out var e)) batch.Add(e);
            var self = batch.OfType<SelfIdentifiedEvent>().LastOrDefault();
            if (self is not null)
            {
                // Nouveau personnage : l'ancienne puissance ne vaut plus.
                if (_self is null || _self.ActorId != self.ActorId) _selfPower = null;
                _self = self;
                View.SetSelf(self);
                var description = $"{(self.Name.Length == 0 ? "(sans nom)" : self.Name)} (id {self.ActorId}, serveur {self.ServerId}, {self.Class} niv. {self.Level})";
                if (description != _lastSelf)
                {
                    _lastSelf = description;
                    Log.Information("Personnage détecté : {Self}", description);
                }
            }
            if (_self is not null)
            {
                // Puissance de soi : par id, à défaut (roster, inspection) par nom.
                var power = batch.OfType<CombatPowerSeenEvent>().LastOrDefault(p =>
                    p.ActorId == _self.ActorId || (p.ActorId == 0 && p.Name == _self.Name));
                if (power is not null) _selfPower = power.CombatPower;
            }
            View.SetSelfCombatPower(_selfPower);
            count = batch.Count;
            EventsReceived?.Invoke(batch);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le traitement des événements de capture");
        }
        if (sw.ElapsedMilliseconds > 100) Log.Warning("Lent : {Where} {Ms} ms ({Count} éléments)", "Capture.Drain", sw.ElapsedMilliseconds, count);
    }

    private void Refresh()
    {
        var sw = Stopwatch.StartNew();
        var opcodeCount = 0;
        try
        {
            var stats = _pipeline.Snapshot();
            var now = _clock.ElapsedMilliseconds;
            opcodeCount = stats.Opcodes.Count;
            var (indicator, reason) = _health.Update(_live.Status.State, stats, now);
            var message = _live.Status.State == CaptureState.Capturing ? reason : _live.Status.Message;
            View.SetIndicator(indicator, message);
            View.SetStats(stats, _live.DroppedSegments);
            View.SetRecording(_live.IsRecording);
            if (stats.LockedFlow != _lastFlow)
            {
                _lastFlow = stats.LockedFlow;
                // Nouvelle session de jeu : le personnage et sa puissance précédents ne valent plus.
                _self = null;
                _selfPower = null;
                View.SetSelfCombatPower(null);
                if (_lastFlow is not null) Log.Information("Flux du jeu verrouillé : {Flow}", _lastFlow);
            }
            if (_live.Status.State == CaptureState.Capturing && now - _lastSummaryMs >= 30_000)
            {
                _lastSummaryMs = now;
                var top = string.Join(", ", stats.Opcodes.OrderByDescending(o => o.Received).Take(5)
                    .Select(o => $"{Opcodes.Name(o.Opcode)}={o.Received}"));
                Log.Information(
                    "Capture : {Segments} segments, {Bytes} octets, {Frames} trames, {Bundles} bundles, {Resyncs} resync., {Gaps} trous, {Events} événements, {Parsed} décodés, {Rejected} rejetés ; top : {Top}",
                    stats.Segments, stats.Bytes, stats.Frames, stats.Bundles, stats.Resyncs, stats.Gaps, stats.Events, stats.Parsed, stats.Rejected, top);
            }
            foreach (var (opcode, perMinute) in _unknown.Check(stats.Opcodes, now))
                Log.Information("Opcode inconnu fréquent : {Opcode} ({Count}/min)", Opcodes.Name(opcode), perMinute);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant la mise à jour du diagnostic de capture");
        }
        if (sw.ElapsedMilliseconds > 100) Log.Warning("Lent : {Where} {Ms} ms ({Count} éléments)", "Capture.Refresh", sw.ElapsedMilliseconds, opcodeCount);
    }

    private void ToggleRecording()
    {
        if (_live.IsRecording)
        {
            _live.StopRecording();
        }
        else
        {
            var path = Path.Combine(AppPaths.CapturesDirectory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.pcap");
            if (!_live.StartRecording(path)) Log.Warning("Enregistrement impossible : la capture n'est pas active");
        }
        View.SetRecording(_live.IsRecording);
    }

    private static void OpenCapturesFolder()
    {
        Directory.CreateDirectory(AppPaths.CapturesDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.CapturesDirectory) { UseShellExecute = true });
    }

    public void Dispose()
    {
        _drain.Stop();
        _refresh.Stop();
        _live.Dispose();
    }
}
