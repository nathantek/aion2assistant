using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Hotkeys;

namespace Aion2Assistant.App.Services;

internal static class HotkeyLabels
{
    public static string For(HotkeyAction action) => action switch
    {
        HotkeyAction.TabTimers => UiStrings.HotkeyTabTimers,
        HotkeyAction.TabDps => UiStrings.HotkeyTabDps,
        HotkeyAction.TabTaken => UiStrings.HotkeyTabTaken,
        HotkeyAction.TabHeal => UiStrings.HotkeyTabHeal,
        HotkeyAction.TabHistory => UiStrings.HotkeyTabHistory,
        HotkeyAction.PreviousTab => UiStrings.HotkeyPreviousTab,
        HotkeyAction.NextTab => UiStrings.HotkeyNextTab,
        HotkeyAction.ToggleVisible => UiStrings.HotkeyToggleVisible,
        HotkeyAction.ToggleDock => UiStrings.HotkeyToggleDock,
        HotkeyAction.ToggleMinimize => UiStrings.HotkeyToggleMinimize,
        HotkeyAction.ResetEncounter => UiStrings.HotkeyResetEncounter,
        HotkeyAction.ScaleUp => UiStrings.HotkeyScaleUp,
        HotkeyAction.ScaleDown => UiStrings.HotkeyScaleDown,
        _ => action.ToString(),
    };
}
