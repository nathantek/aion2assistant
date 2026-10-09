using Aion2Assistant.Core.Hotkeys;

namespace Aion2Assistant.App.Services;

internal static class HotkeyLabels
{
    public static string For(HotkeyAction action) => action switch
    {
        HotkeyAction.TabTimers => "Onglet Timers",
        HotkeyAction.TabDps => "Onglet DPS",
        HotkeyAction.TabTaken => "Onglet Dégâts reçus",
        HotkeyAction.TabHeal => "Onglet Soins",
        HotkeyAction.TabHistory => "Onglet Historique",
        HotkeyAction.PreviousTab => "Onglet précédent",
        HotkeyAction.NextTab => "Onglet suivant",
        HotkeyAction.ToggleVisible => "Masquer / afficher",
        HotkeyAction.ToggleDock => "Ancrer / libérer",
        HotkeyAction.ToggleMinimize => "Réduire / agrandir",
        HotkeyAction.ResetEncounter => "Réinitialiser le combat",
        HotkeyAction.ScaleUp => "Agrandir l'interface",
        HotkeyAction.ScaleDown => "Rétrécir l'interface",
        _ => action.ToString(),
    };
}
