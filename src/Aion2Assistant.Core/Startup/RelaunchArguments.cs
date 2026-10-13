using System.Globalization;

namespace Aion2Assistant.Core.Startup;

/// <summary>
/// Arguments de la relance de l'app par elle-même (changement de langue) : la nouvelle instance attend la fin
/// de l'ancienne (<c>--wait-for-pid &lt;pid&gt;</c>) avant de prendre le verrou d'instance unique.
/// </summary>
public static class RelaunchArguments
{
    public const string WaitForPid = "--wait-for-pid";

    public static string[] Build(int pid) => [WaitForPid, pid.ToString(CultureInfo.InvariantCulture)];

    /// <summary>Lit le pid à attendre ; faux si l'option est absente ou sa valeur invalide.</summary>
    public static bool TryParseWaitPid(IReadOnlyList<string> args, out int pid)
    {
        pid = 0;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], WaitForPid, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                pid = value;
                return true;
            }
            return false;
        }
        return false;
    }
}
