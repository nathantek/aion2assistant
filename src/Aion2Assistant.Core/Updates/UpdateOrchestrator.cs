namespace Aion2Assistant.Core.Updates;

/// <summary>État de la mise à jour automatique.</summary>
public enum UpdateState
{
    /// <summary>Rien en cours.</summary>
    Idle,
    /// <summary>Recherche d'une nouvelle version.</summary>
    Checking,
    /// <summary>Téléchargement en arrière-plan.</summary>
    Downloading,
    /// <summary>Version téléchargée, bandeau affiché, en attente du clic.</summary>
    Ready,
    /// <summary>« Plus tard » : bandeau masqué, application à la fermeture.</summary>
    Deferred,
}

/// <summary>
/// Machine d'états de la mise à jour : vérification 10 s après le démarrage puis toutes les 6 h,
/// téléchargement, puis attente du choix de l'utilisateur. Aucun redémarrage sans demande explicite.
/// Indépendante de WPF et de Velopack : tout passe par <see cref="IUpdateBackend"/> et <see cref="IUpdateScheduler"/>.
/// </summary>
public sealed class UpdateOrchestrator : IDisposable
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Period = TimeSpan.FromHours(6);

    private readonly IUpdateBackend _backend;
    private readonly IUpdateScheduler _scheduler;
    private readonly AppVersion _current;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();

    // Tout l'état mutable est protégé par _gate.
    private UpdateState _state = UpdateState.Idle;
    private AppVersion? _version;
    private int _progress;
    private bool _enabled;
    private bool _scheduled;
    private bool _exitApplied;
    private bool _disposed;
    private bool _stopped;
    private CancellationTokenSource? _cycleCts;

    public UpdateOrchestrator(IUpdateBackend backend, IUpdateScheduler scheduler, AppVersion current)
    {
        _backend = backend;
        _scheduler = scheduler;
        _current = current;
    }

    public UpdateState State { get { lock (_gate) return _state; } }

    /// <summary>Version téléchargée ; null tant qu'aucune n'est prête.</summary>
    public AppVersion? Version { get { lock (_gate) return _version; } }

    /// <summary>Progression du téléchargement, de 0 à 100.</summary>
    public int Progress { get { lock (_gate) return _progress; } }

    /// <summary>Levé après chaque changement d'état (peut venir d'un thread d'arrière-plan).</summary>
    public event Action? StateChanged;

    /// <summary>Levé quand une vérification, un téléchargement ou une application échoue (pour journaliser).</summary>
    public event Action<Exception>? Failed;

    /// <summary>Démarre la planification si le réglage est activé et que l'application est installée.</summary>
    public void Start(bool enabled) => SetEnabled(enabled);

    /// <summary>
    /// Réglage « Mises à jour automatiques » : désactivé ⇒ aucun appel réseau, cycle en cours annulé, rien n'est
    /// installé (ni redémarrage ni fermeture). Activé ⇒ une version déjà téléchargée redevient « prête ».
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        // Lecture locale faite hors verrou : le paquet d'un lancement précédent, sans nouveau téléchargement.
        var local = enabled ? ReadDownloadedVersion() : null;
        var adopted = false;
        lock (_gate)
        {
            if (_disposed) return;
            _enabled = enabled;
            if (!enabled) CancelCycleLocked();
            else if (local is not null && _version is null && _state == UpdateState.Idle
                     && !_exitApplied && !_stopped && local.CompareTo(_current) > 0)
            {
                _version = local;
                _state = UpdateState.Ready;
                adopted = true;
            }
            RescheduleLocked();
        }
        if (adopted) Raise();
    }

    private AppVersion? ReadDownloadedVersion()
    {
        try { return _backend.IsInstalled ? _backend.DownloadedVersion : null; }
        catch (Exception ex)
        {
            RaiseFailed(ex);
            return null;
        }
    }

    /// <summary>Fermeture : plus aucun cycle, sans changer le réglage (l'installation à la fermeture reste possible).</summary>
    public void StopScheduling()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _stopped = true;
            CancelCycleLocked();
            RescheduleLocked();
        }
    }

    private void CancelCycleLocked()
    {
        try { _cycleCts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void RescheduleLocked()
    {
        var want = _enabled && _backend.IsInstalled && !_exitApplied && !_disposed && !_stopped;
        if (want && !_scheduled)
        {
            _scheduled = true;
            _scheduler.Start(InitialDelay, Period, RunCycleAsync);
        }
        else if (!want && _scheduled)
        {
            _scheduled = false;
            _scheduler.Stop();
        }
    }

    /// <summary>Un cycle : vérification puis téléchargement éventuel. Ignoré si un cycle est déjà en cours.</summary>
    public async Task RunCycleAsync()
    {
        UpdateState previous;
        AppVersion reference;
        CancellationTokenSource cycle;
        lock (_gate)
        {
            if (_disposed || _stopped || !_enabled || !_backend.IsInstalled || _exitApplied) return;
            // Ready persiste jusqu'à l'application ; Checking/Downloading : cycle déjà en cours.
            if (_state is not (UpdateState.Idle or UpdateState.Deferred)) return;
            previous = _state;
            // En Deferred, seule une version plus récente que celle déjà téléchargée est intéressante.
            reference = previous == UpdateState.Deferred && _version is not null ? _version : _current;
            cycle = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _cycleCts = cycle;
            _state = UpdateState.Checking;
        }
        Raise();

        try
        {
            var found = await _backend.CheckAsync(cycle.Token).ConfigureAwait(false);
            var proceed = false;
            lock (_gate)
            {
                // Désactivé ou fermeture en cours pendant la vérification : on ne télécharge pas.
                if (found is not null && found.CompareTo(reference) > 0 && !cycle.IsCancellationRequested && CanContinueLocked())
                {
                    _state = UpdateState.Downloading;
                    _progress = 0;
                    proceed = true;
                }
                else _state = previous;
            }
            Raise();
            if (!proceed) return;

            await _backend.DownloadAsync(found!, OnProgress, cycle.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _version = found;
                _state = UpdateState.Ready;
            }
            Raise();
        }
        catch (OperationCanceledException) when (cycle.IsCancellationRequested)
        {
            // Annulation réelle du cycle (réglage coupé, fermeture) : pas une erreur.
            // Sans annulation (ex. délai HttpClient dépassé), on tombe dans le catch général : échec.
            Restore(previous);
        }
        catch (Exception ex)
        {
            // Retour à l'état précédent (Idle) ; en Deferred on garde la version déjà téléchargée pour la fermeture.
            Restore(previous);
            RaiseFailed(ex);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cycleCts, cycle)) _cycleCts = null;
            }
            cycle.Dispose();
        }
    }

    private bool CanContinueLocked() => _enabled && !_exitApplied && !_disposed && !_stopped;

    private void Restore(UpdateState previous)
    {
        lock (_gate) _state = previous;
        Raise();
    }

    private void OnProgress(int percent)
    {
        lock (_gate) _progress = Math.Clamp(percent, 0, 100);
    }

    // Les événements sont levés hors verrou, après validation de l'état ; un abonné fautif ne le corrompt pas.
    private void Raise()
    {
        lock (_gate) { if (_disposed) return; }
        try { StateChanged?.Invoke(); }
        catch (Exception ex) { RaiseFailed(ex); }
    }

    private void RaiseFailed(Exception ex)
    {
        lock (_gate) { if (_disposed) return; }
        try { Failed?.Invoke(ex); }
        catch { /* abonné fautif : ignoré */ }
    }

    /// <summary>« Plus tard » : masque le bandeau ; la mise à jour sera appliquée à la fermeture.</summary>
    public void Defer()
    {
        lock (_gate)
        {
            if (_state != UpdateState.Ready) return;
            _state = UpdateState.Deferred;
        }
        Raise();
    }

    /// <summary>« Redémarrer » : applique la mise à jour téléchargée et relance l'application.</summary>
    public void RestartNow()
    {
        lock (_gate)
        {
            if (_disposed || !_enabled || _exitApplied || _version is null) return;
            if (_state is not (UpdateState.Ready or UpdateState.Deferred)) return;
            _exitApplied = true;
            RescheduleLocked();
        }
        try
        {
            _backend.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            // Échec : on reste utilisable (nouvel essai, application à la fermeture, prochains cycles).
            lock (_gate)
            {
                _exitApplied = false;
                RescheduleLocked();
            }
            RaiseFailed(ex);
        }
    }

    /// <summary>À la fermeture : applique en silence une mise à jour déjà téléchargée, sans relancer. Une seule fois, si activé.</summary>
    public void ApplyOnExit()
    {
        lock (_gate)
        {
            // Une version téléchargée suffit, même si un nouveau cycle est en cours depuis Deferred.
            // Réglage coupé : le choix de l'utilisateur est respecté, rien n'est installé.
            if (_disposed || !_enabled || _exitApplied || _version is null) return;
            _exitApplied = true;
            CancelCycleLocked();
            RescheduleLocked();
        }
        try { _backend.ApplyOnExit(); }
        catch (Exception ex) { RaiseFailed(ex); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { }
            _scheduled = false;
        }
        _scheduler.Dispose();
    }
}
