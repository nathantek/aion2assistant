using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Aion2Assistant.App.ViewModels;

namespace Aion2Assistant.App.Views;

public partial class HistoryView
{
    public HistoryView() => InitializeComponent();

    private HistoryViewModel? Vm => DataContext as HistoryViewModel;

    // Vérification différée de la prise de focus (une seule à la fois).
    private DispatcherTimer? _focusCheck;

    // L'overlay ne prend pas le focus : on l'autorise le temps de taper dans la recherche.
    private void OnSearchMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null) return;
        Vm.BeginTyping();
        // Filet de sécurité : si l'activation a été refusée, la recherche n'a pas le focus et rien ne rendrait
        // l'overlay « sans focus ». On laisse 250 ms à l'activation avant de terminer la saisie.
        _focusCheck?.Stop();
        _focusCheck = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
        _focusCheck.Tick += OnFocusCheck;
        _focusCheck.Start();
    }

    private void OnFocusCheck(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer timer)
        {
            timer.Stop();
            timer.Tick -= OnFocusCheck;
            if (ReferenceEquals(_focusCheck, timer)) _focusCheck = null;
        }
        if (!SearchBox.IsKeyboardFocused && Window.GetWindow(this)?.IsActive != true) Vm?.EndTyping();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        e.Handled = true;
        Keyboard.ClearFocus();
        Vm?.EndTyping();
    }

    private void OnSearchLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Vm?.EndTyping();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false) Vm?.EndTyping();
    }
}
