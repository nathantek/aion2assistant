using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Aion2Assistant.Capture;

public sealed record GameConnection(IPAddress LocalIp, ushort LocalPort, IPAddress RemoteIp, ushort RemotePort);

/// <summary>Trouve le client AION 2 et ses connexions TCP établies (table TCP de Windows, IPv4).</summary>
public static class GameConnectionLocator
{
    public static readonly string[] ProcessNames = ["AION2", "AION2-Win64-Shipping"];

    private const int AfInet = 2;
    private const int TcpTableOwnerPidAll = 5;
    private const uint StateEstablished = 5;
    private const uint ErrorInsufficientBuffer = 122;

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRow
    {
        public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort, OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

    public static int[] FindGameProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var name in ProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                ids.Add(p.Id);
                p.Dispose();
            }
        }
        return ids.ToArray();
    }

    public static List<GameConnection> FindConnections(IReadOnlyCollection<int> pids)
    {
        var result = new List<GameConnection>();
        if (pids.Count == 0) return result;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AfInet, TcpTableOwnerPidAll, 0);
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var rc = GetExtendedTcpTable(buffer, ref size, false, AfInet, TcpTableOwnerPidAll, 0);
                if (rc == ErrorInsufficientBuffer) continue; // la table a grandi entre les deux appels
                if (rc != 0) return result;
                var count = Marshal.ReadInt32(buffer);
                var rowSize = Marshal.SizeOf<TcpRow>();
                for (var i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<TcpRow>(buffer + 4 + i * rowSize);
                    if (row.State != StateEstablished || !pids.Contains((int)row.OwningPid)) continue;
                    var remotePort = Port(row.RemotePort);
                    if (remotePort is 80 or 443) continue;
                    result.Add(new GameConnection(new IPAddress(row.LocalAddr), Port(row.LocalPort), new IPAddress(row.RemoteAddr), remotePort));
                }
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        return result;
    }

    private static ushort Port(uint networkOrder) => (ushort)((networkOrder & 0xFF) << 8 | (networkOrder >> 8) & 0xFF);
}
