using System.Collections.ObjectModel;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Timers;

namespace Aion2Assistant.App.ViewModels;

public sealed class TimersViewModel(AppSettings settings, Action requestSave)
{
    private DateTimeOffset _lastNow;
    private IReadOnlyList<TimerState> _lastStates = [];

    public ObservableCollection<TimerRowViewModel> Rows { get; } = new();

    /// <summary>Prochain timer à afficher : le premier qui n'est pas ouvert, sinon le premier, sinon null.</summary>
    public TimerRowViewModel? NextTimer => Rows.FirstOrDefault(r => !r.IsOpen) ?? Rows.FirstOrDefault();

    /// <summary>Met à jour les lignes sans les recréer (ajouts, suppressions, réordonnancement par prochaine échéance).</summary>
    public void Refresh(IReadOnlyList<TimerState> states, DateTimeOffset now)
    {
        _lastStates = states;
        _lastNow = now;
        var visible = states.Where(s => settings.GetTimer(s.Id).Visible).ToList();

        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (visible.All(s => s.Id != Rows[i].Id)) Rows.RemoveAt(i);
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var state = visible[i];
            var row = Rows.FirstOrDefault(r => r.Id == state.Id);
            if (row is null)
            {
                row = new TimerRowViewModel(state.Id, ToggleSound);
                Rows.Insert(i, row);
            }
            else
            {
                var current = Rows.IndexOf(row);
                if (current != i) Rows.Move(current, i);
            }
            row.Update(state, settings.GetTimer(state.Id), now);
        }
    }

    private void ToggleSound(string id)
    {
        var timer = settings.GetTimer(id);
        timer.Sound = !timer.Sound;
        requestSave();
        Refresh(_lastStates, _lastNow);
    }
}
