namespace Aion2Assistant.Core.Hotkeys;

/// <summary>
/// Raccourcis par défaut, tous sur le pavé numérique. Le jeu lit le clavier en entrée brute : les modificateurs
/// ne masquent pas la touche (Ctrl+Alt+F1 déclenchait aussi F1 en jeu). Les touches du pavé numérique ne sont
/// liées à rien dans le jeu ; Verr Num doit être activé.
/// </summary>
public static class HotkeyDefaults
{
    public static Dictionary<string, string> Create() => new()
    {
        [nameof(HotkeyAction.TabTimers)] = "Ctrl+Alt+NumPad1",
        [nameof(HotkeyAction.TabDps)] = "Ctrl+Alt+NumPad2",
        [nameof(HotkeyAction.TabTaken)] = "Ctrl+Alt+NumPad3",
        [nameof(HotkeyAction.TabHeal)] = "Ctrl+Alt+NumPad4",
        [nameof(HotkeyAction.TabHistory)] = "Ctrl+Alt+NumPad5",
        [nameof(HotkeyAction.PreviousTab)] = "Ctrl+Alt+Divide",
        [nameof(HotkeyAction.NextTab)] = "Ctrl+Alt+Multiply",
        [nameof(HotkeyAction.ToggleVisible)] = "Ctrl+Alt+NumPad0",
        [nameof(HotkeyAction.ToggleDock)] = "Ctrl+Alt+NumPad7",
        [nameof(HotkeyAction.ToggleMinimize)] = "Ctrl+Alt+NumPad8",
        [nameof(HotkeyAction.ResetEncounter)] = "Ctrl+Alt+NumPad9",
        [nameof(HotkeyAction.ScaleUp)] = "Ctrl+Alt+Add",
        [nameof(HotkeyAction.ScaleDown)] = "Ctrl+Alt+Subtract",
    };
}
