namespace Aion2Assistant.Core.Hotkeys;

/// <summary>Modificateurs, avec les mêmes valeurs que MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Touche connue, et au moins un modificateur (sauf F13–F24, libres dans tous les jeux).</summary>
    public bool IsValid =>
        VirtualKeyNames.TryGetName(VirtualKey, out _) &&
        (Modifiers != HotkeyModifiers.None || VirtualKey is >= VirtualKeyNames.F13 and <= VirtualKeyNames.F24);

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Ctrl,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };
            if (modifier == HotkeyModifiers.None) return false;
            modifiers |= modifier;
        }

        if (!VirtualKeyNames.TryGetKey(parts[^1], out var virtualKey)) return false;

        var candidate = new HotkeyGesture(modifiers, virtualKey);
        if (!candidate.IsValid) return false;

        gesture = candidate;
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(VirtualKeyNames.TryGetName(VirtualKey, out var name) ? name : $"VK{VirtualKey:X2}");
        return string.Join("+", parts);
    }
}
