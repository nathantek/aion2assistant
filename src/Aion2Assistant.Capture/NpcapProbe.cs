namespace Aion2Assistant.Capture;

public static class NpcapProbe
{
    public static bool IsInstalled()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system, "Npcap", "wpcap.dll")) || File.Exists(Path.Combine(system, "wpcap.dll"));
    }
}
