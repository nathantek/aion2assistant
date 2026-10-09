namespace Aion2Assistant.Core.Hotkeys;

/// <summary>Noms lisibles des codes de touches virtuelles Win32 pris en charge pour les raccourcis.</summary>
public static class VirtualKeyNames
{
    public const int F1 = 0x70;
    public const int F13 = 0x7C;
    public const int F24 = 0x87;

    private static readonly Dictionary<string, int> ByName = Build();
    private static readonly Dictionary<int, string> ByKey = ByName.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static bool TryGetKey(string name, out int virtualKey) => ByName.TryGetValue(name, out virtualKey);

    public static bool TryGetName(int virtualKey, out string name)
    {
        if (ByKey.TryGetValue(virtualKey, out var found))
        {
            name = found;
            return true;
        }
        name = "";
        return false;
    }

    private static Dictionary<string, int> Build()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;
        for (var d = 0; d <= 9; d++) map[d.ToString()] = 0x30 + d;
        for (var f = 1; f <= 24; f++) map[$"F{f}"] = F1 + f - 1;
        for (var n = 0; n <= 9; n++) map[$"NumPad{n}"] = 0x60 + n;
        map["Space"] = 0x20;
        map["PageUp"] = 0x21;
        map["PageDown"] = 0x22;
        map["End"] = 0x23;
        map["Home"] = 0x24;
        map["Left"] = 0x25;
        map["Up"] = 0x26;
        map["Right"] = 0x27;
        map["Down"] = 0x28;
        map["Insert"] = 0x2D;
        map["Delete"] = 0x2E;
        map["Multiply"] = 0x6A;
        map["Add"] = 0x6B;
        map["Subtract"] = 0x6D;
        map["Divide"] = 0x6F;
        return map;
    }
}
