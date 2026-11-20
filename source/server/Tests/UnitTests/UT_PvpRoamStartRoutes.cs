using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

/// <summary>
/// The start points of the 8v8 roam in Emain Macha (docs/PVP-8V8-ROADMAP.md, M9.7), read off the
/// installed navmesh without a server: OFFLINE_DAOC_NAV_ROOT is a runtime\server folder. It
/// prints what it finds and asserts nothing: it is a probe.
/// </summary>
[TestFixture, Explicit("Reads an installed navmesh"), NonParallelizable]
public class UT_PvpRoamStartRoutes
{
    private static readonly IObjectDatabase Empty = DispatchProxy.Create<IObjectDatabase, UT_UnobservedConcentration.EmptyReads>();
    private sealed class Server : GameServer { protected override IObjectDatabase DataBaseImpl => Empty; }

    [Test]
    public void BetweenThePortalKeeps()
    {
        string root = Environment.GetEnvironmentVariable("OFFLINE_DAOC_NAV_ROOT");
        string previous = Environment.CurrentDirectory;
        var loaded = new List<Zone>();
        var previousServer = GameServer.Instance;
        var previousNav = PathfindingProvider.Instance;
        GameServer.LoadTestDouble((Server)RuntimeHelpers.GetUninitializedObject(typeof(Server)));
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LocalPathfindingMgr).Assembly,
                (name, assembly, search) => name == "lib/Detour" ? NativeLibrary.Load(Path.Combine(root, "lib", "Detour.dll")) : IntPtr.Zero);
            Environment.CurrentDirectory = root;
            using var db = new SQLiteConnection($"Data Source={Path.GetFullPath("../data/opendaoc.sqlite3.db")};Read Only=True;Pooling=False;");
            db.Open();
            Region region = UT_AuditedDungeonInstalledMesh.BuildRegion(db, 200, loaded);
            PathfindingProvider.SetPathfindingMgr(PathfindingProvider.LocalPathfindingMgr);
            (string Name, int X, int Y)[] points =
            [
                ("Albion Portal Keep", 475844, 343696), ("2,500 west of it", 473344, 343696), ("4,000 west of it", 471844, 343696),
                ("3,000 north of it", 475844, 340696),
                ("Midgard Portal Keep", 474129, 295207), ("2,500 west of it", 471629, 295207), ("4,000 west of it", 470129, 295207),
                ("3,000 south of it", 474129, 298207),
                ("the basin's west start", 455292, 333720), ("the basin's east start", 476792, 333720),
                ("Dun Crauchon's gate side", 426569, 319120), ("west of the basin", 445970, 334336), ("west of the Midgard keep", 449336, 299008),
                ("the middle of the zone", 450000, 318000),
                // The owner's /loc on the hill by the tower in front of the Albion gate, 2026-11-20: 22984,51041,3966 in the zone.
                ("Hibernia's hill", 417792 + 22984, 286720 + 51041),
            ];
            foreach ((string from, string to) in new[] {
                ("Midgard Portal Keep", "Albion Portal Keep"), ("Albion Portal Keep", "Midgard Portal Keep"),
                ("2,500 west of it", "the basin's east start"), ("Midgard Portal Keep", "the basin's east start"),
                ("Midgard Portal Keep", "the basin's west start"), ("Albion Portal Keep", "the basin's east start"),
                ("Albion Portal Keep", "the basin's west start"),
                ("Midgard Portal Keep", "west of the Midgard keep"), ("west of the Midgard keep", "Dun Crauchon's gate side"),
                ("Dun Crauchon's gate side", "west of the basin"), ("west of the basin", "the basin's west start"),
                ("west of the Midgard keep", "the middle of the zone"), ("the middle of the zone", "west of the basin"),
                ("the middle of the zone", "the basin's west start"), ("Midgard Portal Keep", "the middle of the zone"),
                ("west of the Midgard keep", "west of the basin"), ("Midgard Portal Keep", "west of the basin"),
                ("west of the basin", "Albion Portal Keep"), ("west of the basin", "Midgard Portal Keep"),
                ("Hibernia's hill", "west of the basin"), ("Hibernia's hill", "west of the Midgard keep"),
                ("Hibernia's hill", "Albion Portal Keep"), ("Hibernia's hill", "the basin's west start") })
            {
                foreach (var a in Array.FindAll(points, point => point.Name == from))
                foreach (var b in Array.FindAll(points, point => point.Name == to))
                    TestContext.Progress.WriteLine($"ROUTE {a.Name} ({a.X},{a.Y}) -> {b.Name} ({b.X},{b.Y}): {JsonSerializer.Serialize(PvpScrim.RouteCheck(region, a.X, a.Y, b.X, b.Y))}");
            }
        }
        finally
        {
            foreach (Zone zone in loaded)
                LocalPathfindingMgr.UnloadNavMesh(zone);
            Environment.CurrentDirectory = previous;
            GameServer.LoadTestDouble(previousServer);
            PathfindingProvider.SetPathfindingMgr(previousNav);
        }
    }
}
