using Aion2Assistant.App.Localization;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Settings;
using System.Windows.Threading;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Suivi des âmes de familiers (thread UI). Le suivi tourne toujours, même option désactivée : l'état enregistré
/// dans familiars.json reste juste le jour où l'utilisateur active l'onglet. Pop-ups et sons seulement si l'option est activée.
/// </summary>
internal sealed class FamiliarsController : IDisposable
{
    private readonly CaptureController _capture;
    private readonly AppSettings _settings;
    private readonly Func<int, string> _name;
    private readonly Func<bool> _exclusiveFullscreen;
    private readonly Action<string, string, bool> _showPopup;
    private readonly Action<Core.Audio.SoundRef> _playSound;
    private readonly FamiliarTracker _tracker;
    // Réévalue le bandeau tant qu'un avertissement peut encore apparaître sans nouvel événement ; arrêté sinon.
    private readonly DispatcherTimer _bannerTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    /// <param name="showPopup">Titre, message, vrai pour le MAX.</param>
    public FamiliarsController(CaptureController capture, AppSettings settings, Func<int, string> name,
        Func<bool> exclusiveFullscreen, Action<string, string, bool> showPopup, Action<Core.Audio.SoundRef> playSound)
    {
        _capture = capture;
        _settings = settings;
        _name = name;
        _exclusiveFullscreen = exclusiveFullscreen;
        _showPopup = showPopup;
        _playSound = playSound;
        var loaded = FamiliarStore.Load(AppPaths.Familiars);
        if (loaded.Error is not null)
            Log.Warning("{File} : {Error} (source utilisée : {Source})", FamiliarStore.FileName, loaded.Error, loaded.Source);
        _tracker = new FamiliarTracker(loaded.Value);
        _tracker.DebugMessage += message => Log.Information("{Message}", message);
        _bannerTimer.Tick += (_, _) => RefreshBanner();
        RefreshView();
        capture.EventsReceived += OnEvents;
    }

    public FamiliarsViewModel View { get; } = new();

    private void OnEvents(IReadOnlyList<GameEvent> batch)
    {
        try
        {
            var relevant = false;
            var changed = false;
            var milestones = new List<FamiliarMilestone>();
            foreach (var e in batch)
            {
                if (e is not (StreamStartedEvent or SelfIdentifiedEvent or FamiliarStateEvent or FamiliarSoulEvent or FamiliarTierEvent))
                    continue;
                relevant = true;
                var result = _tracker.Apply(e);
                changed |= result.Changed;
                milestones.AddRange(result.Milestones);
                if (e is FamiliarStateEvent state)
                    Log.Information("Familiers : état complet reçu ({Count} entrées, serveur {Server})", state.Entries.Count, _tracker.CurrentServerId);
            }
            if (!relevant) return;
            // Le bandeau dépend aussi du serveur courant (StreamStarted, SelfIdentified) : on réaffiche même sans changement.
            RefreshView();
            if (changed) Save();
            foreach (var m in milestones) Alert(m);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le traitement des familiers");
        }
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void RefreshView()
    {
        var now = NowMs();
        View.Refresh(_tracker, _name, now);
        UpdateBannerTimer(now);
    }

    private void RefreshBanner()
    {
        try
        {
            var now = NowMs();
            View.RefreshBanner(_tracker, now);
            UpdateBannerTimer(now);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le rafraîchissement du bandeau des familiers");
        }
    }

    /// <summary>Le minuteur ne tourne que pendant les périodes de grâce (flux, identification).</summary>
    private void UpdateBannerTimer(long now)
    {
        var needed = FamiliarBanner.NeedsTimedRefresh(_tracker.IsSynced, _tracker.IdentifiedAtMs, _tracker.StreamStartedAtMs, now);
        if (needed && !_bannerTimer.IsEnabled) _bannerTimer.Start();
        else if (!needed && _bannerTimer.IsEnabled) _bannerTimer.Stop();
    }

    private void Alert(FamiliarMilestone milestone)
    {
        var name = _name(milestone.Id);
        Log.Information("Familier {Id} ({Name}) : palier {Tier}", milestone.Id, name, milestone.Tier);
        var decision = FamiliarAlerts.Decide(_settings.FamiliarsEnabled, _exclusiveFullscreen(), _settings.Familiars, milestone);
        if (decision.ShowPopup)
        {
            var message = milestone.IsMax
                ? UiStrings.Format(nameof(UiStrings.FamiliarMaxReached), name)
                : UiStrings.Format(nameof(UiStrings.FamiliarTierReached), name, milestone.Tier);
            _showPopup(UiStrings.TabFamiliars, message, milestone.IsMax);
        }
        if (decision.Sound is { } sound) _playSound(sound);
    }

    /// <summary>Écriture immédiate (comme records.json) : les changements sont rares (une âme, un palier).</summary>
    private void Save()
    {
        try
        {
            FamiliarStore.Save(AppPaths.Familiars, _tracker.File);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Échec de la sauvegarde de {File}", FamiliarStore.FileName);
        }
    }

    public void Dispose()
    {
        _bannerTimer.Stop();
        _capture.EventsReceived -= OnEvents;
    }
}
