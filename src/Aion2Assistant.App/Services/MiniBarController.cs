using System.Windows.Media;
using System.Windows.Threading;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Combat;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Valeur clé de la barre réduite : ton DPS en combat, sinon le prochain timer. Lit l'instantané du suivi
/// directement (les meters ne sont pas mis à jour fenêtre réduite) ; ne tourne que barre réduite.
/// </summary>
internal sealed class MiniBarController : IDisposable
{
    private readonly MainViewModel _main;
    private readonly Func<EncounterSnapshot?> _snapshot;
    private readonly TimersViewModel _timers;
    private readonly BossFilter _bossFilter;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MiniBarController(MainViewModel main, Func<EncounterSnapshot?> snapshot, TimersViewModel timers, BossFilter bossFilter)
    {
        _main = main;
        _snapshot = snapshot;
        _timers = timers;
        _bossFilter = bossFilter;
        _timer.Tick += (_, _) => Update();
        _main.PropertyChanged += OnMainPropertyChanged;
        Sync();
    }

    private void OnMainPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsMinimized)) Sync();
    }

    private void Sync()
    {
        if (_main.IsMinimized)
        {
            Update();
            _timer.Start();
            return;
        }
        _timer.Stop();
        Set(null, null, null);
    }

    private void Update()
    {
        if (LiveSummary.SelfDps(_snapshot(), _bossFilter.BossOnly) is { } dps)
            Set($"{MeterFormat.Compact(dps)}/s", AppHost.Resource("DpsBrush"), "Ton DPS (combat en cours)");
        else if (_timers.NextTimer is { } t)
            Set($"{t.Name} {t.RemainingText}", AppHost.Resource(t.IsSoon ? "AlertBrush" : "TimerBrush"), "Prochain timer");
        else
            Set(null, null, null);
    }

    private void Set(string? text, Brush? brush, string? tip)
    {
        _main.MiniValueText = text;
        _main.MiniValueBrush = brush;
        _main.MiniValueTip = tip;
    }

    public void Dispose()
    {
        _main.PropertyChanged -= OnMainPropertyChanged;
        _timer.Stop();
    }
}
