namespace Aion2Assistant.Core.Updates;

/// <summary>Planificateur injectable : permet de tester l'orchestrateur sans attendre de vrais délais.</summary>
public interface IUpdateScheduler : IDisposable
{
    /// <summary>Lance <paramref name="tick"/> après <paramref name="initialDelay"/>, puis à chaque <paramref name="period"/>.</summary>
    void Start(TimeSpan initialDelay, TimeSpan period, Func<Task> tick);

    void Stop();
}

/// <summary>Planificateur réel basé sur <see cref="System.Threading.Timer"/>.</summary>
public sealed class TimerUpdateScheduler : IUpdateScheduler
{
    private Timer? _timer;
    private readonly object _gate = new();
    private bool _disposed;

    public void Start(TimeSpan initialDelay, TimeSpan period, Func<Task> tick)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _timer?.Dispose();
            // Le tick gère lui-même ses erreurs ; on évite seulement qu'une exception ne remonte au pool.
            _timer = new Timer(async _ =>
            {
                try { await tick().ConfigureAwait(false); }
                catch { /* ignoré : le cycle suivant réessaiera */ }
            }, null, initialDelay, period);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        Stop();
    }
}
