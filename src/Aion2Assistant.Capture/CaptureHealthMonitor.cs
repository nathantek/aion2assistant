namespace Aion2Assistant.Capture;

/// <summary>Couleur du voyant à partir de l'état et des compteurs récents (fenêtre glissante d'une minute).</summary>
public sealed class CaptureHealthMonitor
{
    private const long WindowMs = 60_000;
    private const long NoPacketsMs = 10_000;
    private const long NoEventsMs = 30_000;
    private const double MaxRejectRate = 0.2;

    private readonly Queue<(long TimeMs, PipelineStats Stats)> _history = new();

    public (CaptureIndicator Indicator, string Reason) Update(CaptureState state, PipelineStats stats, long nowMs)
    {
        switch (state)
        {
            case CaptureState.NpcapMissing: return (CaptureIndicator.Red, "Npcap n'est pas installé");
            case CaptureState.NoAdapter: return (CaptureIndicator.Red, "Aucune carte réseau ne porte la connexion du jeu");
            case CaptureState.Error: return (CaptureIndicator.Red, "Erreur de capture");
            case CaptureState.GameNotRunning: _history.Clear(); return (CaptureIndicator.Grey, "Jeu non détecté");
            case CaptureState.WaitingForConnection: _history.Clear(); return (CaptureIndicator.Grey, "En attente de la connexion du jeu");
        }

        _history.Enqueue((nowMs, stats));
        while (_history.Count > 0 && nowMs - _history.Peek().TimeMs > WindowMs) _history.Dequeue();

        if (stats.Segments == 0) return (CaptureIndicator.Amber, "Aucun paquet reçu pour l'instant");

        if (SampleAtOrBefore(nowMs - NoPacketsMs) is { } old10 && old10.Stats.Segments == stats.Segments)
            return (CaptureIndicator.Red, "Aucun paquet depuis 10 s");

        var oldest = _history.Peek();
        var parsed = stats.Parsed - oldest.Stats.Parsed;
        var rejected = stats.Rejected - oldest.Stats.Rejected;
        if (parsed + rejected > 0 && (double)rejected / (parsed + rejected) > MaxRejectRate)
            return (CaptureIndicator.Amber, "Beaucoup de paquets illisibles (protocole modifié ?)");

        if (SampleAtOrBefore(nowMs - NoEventsMs) is { } old30 && old30.Stats.Events == stats.Events)
            return (CaptureIndicator.Amber, "Paquets reçus mais aucun événement décodé");

        return (CaptureIndicator.Green, "Capture active");
    }

    /// <summary>Échantillon le plus récent pris au plus tard à <paramref name="atOrBeforeMs"/> (null s'il n'y en a pas).</summary>
    private (long TimeMs, PipelineStats Stats)? SampleAtOrBefore(long atOrBeforeMs)
    {
        (long TimeMs, PipelineStats Stats)? found = null;
        foreach (var sample in _history)
        {
            if (sample.TimeMs <= atOrBeforeMs) found = sample;
            else break;
        }
        return found;
    }
}
