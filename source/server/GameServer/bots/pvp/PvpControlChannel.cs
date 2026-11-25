using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// Lets a tool outside the game drive the 8v8 mode on a running server without a game client:
    /// it writes <c>pvp-request.json</c> beside the server, the server does the work on the game
    /// loop and answers in <c>pvp-result.json</c>. The same pattern as the launcher's bot-world
    /// request. Scrims (roadmap M2) and build checks (M1.2) both use it.
    ///
    /// Request: { "Id": "any new text", "Command": "audit-builds", "Args": { "Build": "hero" } }
    /// Result:  { "Id": ..., "Command": ..., "Ok": true, "Error": null, "Data": ... }
    /// </summary>
    public static class PvpControlChannel
    {
        public const int PollIntervalMilliseconds = 500;

        public sealed record Request(string Id, string Command, Dictionary<string, string> Args);
        public sealed record Result(string Id, string Command, bool Ok, string Error, object Data, DateTime FinishedUtc);

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpControlChannel));
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
        private static Timer _timer;
        private static string _lastRequestId = string.Empty;
        private static int _busy;

        public static string RequestPath => Path.Combine(AppContext.BaseDirectory, "pvp-request.json");
        public static string ResultPath => Path.Combine(AppContext.BaseDirectory, "pvp-result.json");

        [GameServerStartedEvent]
        public static void Start(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            _lastRequestId = string.Empty;
            // A request written before this start belongs to the previous run.
            try { File.Delete(RequestPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            _timer = new Timer(_ => Poll(), null, 2_000, PollIntervalMilliseconds);
        }

        [GameServerStoppedEvent]
        public static void Stop(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            _timer = null;
        }

        private static void Poll()
        {
            // This runs on a timer thread: an exception that escapes here ends the server process.
            try
            {
                if (Volatile.Read(ref _busy) != 0 || !File.Exists(RequestPath))
                    return;
                Request request;
                try
                {
                    using var stream = new FileStream(RequestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    request = JsonSerializer.Deserialize<Request>(stream, Json);
                }
                catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
                {
                    return;   // half-written or briefly locked file: the next poll reads it whole
                }
                if (request == null || string.IsNullOrWhiteSpace(request.Id) || request.Id == _lastRequestId ||
                    Interlocked.Exchange(ref _busy, 1) != 0)
                    return;
                _lastRequestId = request.Id;
                // A request runs once. Left on disk it would run again after the next start
                // (a leftover stop-server would end every start).
                try { File.Delete(RequestPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                // Bots are created and inspected on the game loop, like every other world change.
                NpcService.Instance.Post(Execute, request);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _busy, 0);
                Log.Error("8v8 control request could not be read", exception);
            }
        }

        private static void Execute(Request request)
        {
            Result result;
            try
            {
                result = new(request.Id, request.Command, true, null, Run(request), DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Log.Error($"8v8 control request '{request.Command}' failed", exception);
                result = new(request.Id, request.Command, false, exception.ToString(), null, DateTime.UtcNow);
            }
            try
            {
                string temporary = ResultPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(result, Json));
                // The tool may have the previous answer open for a moment; the replace then
                // fails on Windows. The work is already done, so keep trying to deliver it.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.Move(temporary, ResultPath, true);
                        break;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < 40)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("8v8 control result could not be written", exception);
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }

        private static object Run(Request request)
        {
            string Arg(string name) => request.Args != null && request.Args.TryGetValue(name, out string value) ? value : null;
            switch (request.Command?.ToLowerInvariant())
            {
                case "ping":
                    return new { Builds = PvpBuildCatalog.Builds.Count, Groups = PvpBuildCatalog.Groups.Concat(PvpBuildCatalog.Variants).Select(group => group.Id) };

                case "audit-builds":
                {
                    // Each bot is built, described and dropped. It never enters the world.
                    string only = Arg("Build");
                    IEnumerable<PvpBuild> builds = only == null
                        ? PvpBuildCatalog.Builds
                        : [PvpBuildCatalog.Find(only) ?? throw new ArgumentException($"No build named '{only}'.")];
                    return builds.Select(build => PvpBuildAudit.Describe(new GameBot(build))).ToList();
                }

                case "spawn-group":
                {
                    // Args: Group (a PvpBuildCatalog group id), Region, X, Y. The height is taken from the navmesh.
                    PvpGroupTemplate template = PvpBuildCatalog.FindGroup(Arg("Group"))
                        ?? throw new ArgumentException($"No group named '{Arg("Group")}'.");
                    IReadOnlyList<GameBot> bots = PvpArena.SpawnGroup(template,
                        ushort.Parse(Arg("Region") ?? "200"), int.Parse(Arg("X")), int.Parse(Arg("Y")),
                        // RealmRank=5 is rank 5 level 0; the default is rank 1 level 0, where everyone starts.
                        (int.Parse(Arg("RealmRank") ?? "1") - 1) * 10);
                    return bots.Select(bot => new { bot.Name, Build = bot.PvpBuild.Id, Position = $"{bot.X},{bot.Y},{bot.Z}" }).ToList();
                }

                case "arena-move":
                    return new { Moved = PvpArena.MoveGroups(int.Parse(Arg("DX") ?? "0"), int.Parse(Arg("DY") ?? "0")) };

                case "arena-strip":
                    return new { Stripped = PvpArena.StripBuffs(Arg("Name")) };

                case "arena-status":
                    return PvpArena.Status();

                case "arena-clear":
                    return new { Removed = PvpArena.Clear() };

                case "scrim-start":
                {
                    // Args: A and B (group ids, different realms), or Pairs for several series at once
                    // ("alb-main:mid-main,mid-main:hib-main"); Matches, RealmRank, Limit.
                    // Copies: how many lanes each pairing gets. Lanes=1 puts even a single series in a lane
                    // (groups start 6,000 apart) instead of the whole arena. Versus=1: a comparison of two sets
                    // of weights, pvp-weights.json (new) against pvp-weights-old.json; use Copies=2 with it.
                    // Vary=50: each team of each match draws its own weights, half of them moved one step
                    // down or up by Spread percent (40), and the recording keeps the draw.
                    PvpGroupTemplate Group(string id) => PvpBuildCatalog.FindGroup(id) ?? throw new ArgumentException($"No group named '{id}'.");
                    List<(PvpGroupTemplate, PvpGroupTemplate)> pairs = (Arg("Pairs") ?? $"{Arg("A")}:{Arg("B")}")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(pair => pair.Split(':'))
                        .Select(ids => ids.Length == 2 ? (Group(ids[0]), Group(ids[1])) : throw new ArgumentException("A pairing is written A:B."))
                        .ToList();
                    PvpScrim.BotsRoam = Arg("Roam") == "1";
                    // SkillA / SkillB (casual, seasoned, sharp): a level for each side of a scrim of bots; without them, the level of "the others".
                    (PvpSkills.BotsA, PvpSkills.BotsB) = (PvpSkills.Parse(Arg("SkillA")), PvpSkills.Parse(Arg("SkillB")));
                    PvpScrim.Start(pairs, int.Parse(Arg("Matches") ?? "1"), (int.Parse(Arg("RealmRank") ?? "1") - 1) * 10,
                        int.Parse(Arg("Limit") ?? "300"),
                        // BrainA / BrainB: "squad" turns the squad brain on for that side; anything else is the old brain.
                        Arg("BrainA") == "squad", Arg("BrainB") == "squad",
                        int.Parse(Arg("Copies") ?? "1"), Arg("Lanes") == "1", Arg("Versus") == "1",
                        int.Parse(Arg("Vary") ?? "0"), int.Parse(Arg("Spread") ?? "40"));
                    return PvpScrim.Status();
                }

                case "scrim-call":
                    // A member of a group of bots makes a player's call: Lane, Team (A or B), Call (regroup, push, hold,
                    // engage, extend, assist), Who (the caller's build or class), Target (for assist: the enemy's).
                    return PvpScrim.TestCall(int.Parse(Arg("Lane") ?? "0"), Arg("Team") ?? "A", Arg("Call") ?? "regroup", Arg("Who"), Arg("Target"));

                case "sight-scan":
                    // How often a rise of the ground hides one point of the basin from another: Pairs, Min and Max distance.
                    return PvpScrim.ScanSight(int.Parse(Arg("Pairs") ?? "2000"), int.Parse(Arg("Min") ?? "2000"), int.Parse(Arg("Max") ?? "6500"));

                case "scrim-lanes":
                    return PvpScrim.CheckLanes();

                case "scrim-scan":
                    return PvpScrim.ScanLanes(int.Parse(Arg("Distance") ?? "0"), int.Parse(Arg("Step") ?? "500"));

                case "roam-start":
                    // Lineups=hib-2bard: that group for its realm's teams, in place of the main one. StaleCopies=off: an
                    // effect fault as it was before 2026-11-21, to show it (EffectListComponent.RetireStaleCopies).
                    EffectListComponent.RetireStaleCopies = !string.Equals(Arg("StaleCopies"), "off", StringComparison.OrdinalIgnoreCase);
                    // Prime=8 Pool=test: a prime-time evening (M11), eight guilds of each realm from that pool (made if it is not there).
                    List<PvpGuildTeam> guilds = null;
                    string pool = Arg("Pool") ?? "scrim";
                    // Listener=hibernia: that realm's chat is written to the recording, as a player of it would read it.
                    PvpRoam.ListenTo(Enum.TryParse(Arg("Listener"), true, out eRealm listener) ? listener : eRealm.None);
                    if (int.TryParse(Arg("Prime"), out int prime) && prime > 0)
                        guilds = PvpTeamPool.Tonight(PvpTeamPool.LoadOrCreate(AppContext.BaseDirectory, pool, int.TryParse(Arg("PoolSize"), out int poolSize) && poolSize > 0 ? poolSize : PvpTeamPool.PoolFor(prime), Random.Shared), prime, Random.Shared);
                    // Hunters=2: for a test, that many teams of each realm hunt packs this evening (PvpTeamPool.WithHunters).
                    if (guilds != null && int.TryParse(Arg("Hunters"), out int hunters) && hunters > 0)
                        guilds = PvpTeamPool.WithHunters(guilds, hunters);
                    // Elites=2: the same for the elite teams.
                    if (guilds != null && int.TryParse(Arg("Elites"), out int elites) && elites > 0)
                        guilds = PvpTeamPool.With(guilds, PvpTeamKind.Elite, elites);
                    // Zones=all: the evening is in every frontier zone; Zones=tonight: in as many as its groups are for, drawn
                    // (PvpRoam.ZonesTonight, M11.8); Zones=odin,hadrian: in those and the map it begins in.
                    List<PvpRoamMap> zones = string.Equals(Arg("Zones"), "all", StringComparison.OrdinalIgnoreCase) ? PvpRoam.Frontiers.ToList()
                        : string.Equals(Arg("Zones"), "tonight", StringComparison.OrdinalIgnoreCase)
                            ? PvpRoam.EveningZones(PvpRoam.MapFor(Arg("Map")) ?? PvpRoam.Emain, prime, Random.Shared, PvpRoam.EightZone)
                        : (Arg("Zones") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(PvpRoam.MapFor).Where(zone => zone != null).ToList();
                    return PvpRoam.Start(int.Parse(Arg("Teams") ?? "2"), Arg("Owner") ?? "roam", null, null, PvpRoam.MapFor(Arg("Map")),
                        (Arg("Lineups") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(PvpBuildCatalog.FindGroup).Where(group => group != null).ToList(), guilds, pool, zones.Count > 0 ? zones : null);

                case "roam-status":
                    return PvpRoam.Status();

                case "roam-maps":
                    return PvpRoam.Maps();

                case "nav-route":
                    return PvpScrim.RouteCheck(ushort.Parse(Arg("Region") ?? "200"), int.Parse(Arg("X1")), int.Parse(Arg("Y1")),
                        int.Parse(Arg("X2")), int.Parse(Arg("Y2")));

                // sight Region= X1= Y1= X2= Y2=: whether one standing at the first point sees one at the second (PvpSight):
                // the ray along the navmesh, the ground between them for spotting and for a cast, and both heights. Changes nothing.
                case "sight":
                {
                    ushort regionId = ushort.Parse(Arg("Region") ?? "200");
                    int x1 = int.Parse(Arg("X1")), y1 = int.Parse(Arg("Y1")), x2 = int.Parse(Arg("X2")), y2 = int.Parse(Arg("Y2"));
                    var nav = PathfindingProvider.Instance;
                    Zone zone = WorldMgr.GetRegion(regionId)?.GetZone(x1, y1);
                    float z1 = zone == null ? float.NaN : PvpSight.HeightAt(zone, x1, y1), z2 = zone == null ? float.NaN : PvpSight.HeightAt(zone, x2, y2);
                    if (float.IsNaN(z1) || float.IsNaN(z2))
                        return new { Standing = false };
                    var a = new System.Numerics.Vector3(x1, y1, z1);
                    var b = new System.Numerics.Vector3(x2, y2, z2);
                    int points = PvpSight.Samples(System.Numerics.Vector2.Distance(new(x1, y1), new(x2, y2)));
                    return new
                    {
                        Standing = true, FromZ = (int)z1, ToZ = (int)z2, Mesh = nav.HasLineOfSight(zone, a, b, nav.DefaultFilters),
                        Spotting = PvpSight.Clear(zone, a, b), Cast = PvpSight.ClearForACast(zone, a, b),
                        Ground = Enumerable.Range(0, points).Select(index =>
                        {
                            float height = PvpSight.HeightAt(zone, x1 + (x2 - x1) * (index + 1) / (float)(points + 1), y1 + (y2 - y1) * (index + 1) / (float)(points + 1));
                            return float.IsNaN(height) ? (int?)null : (int)height;
                        }).ToList(),
                        Line = Enumerable.Range(0, points).Select(index => (int)PvpSight.LineAt(z1 + PvpSight.EyeHeight, z2 + PvpSight.EyeHeight, index, points)).ToList(),
                    };
                }

                // way-out Region= X= Y= [FromX= FromY=]: whether one standing at the point can walk off it, and where it would
                // be put if not (PvpTrapped). Changes nothing.
                case "way-out":
                {
                    ushort regionId = ushort.Parse(Arg("Region") ?? "200");
                    int x = int.Parse(Arg("X")), y = int.Parse(Arg("Y"));
                    var nav = PathfindingProvider.Instance;
                    Zone zone = WorldMgr.GetRegion(regionId)?.GetZone(x, y);
                    float z = zone == null ? float.NaN : PvpSight.HeightAt(zone, x, y);
                    if (float.IsNaN(z))
                        return new { Standing = false };
                    var at = new System.Numerics.Vector3(x, y, z);
                    List<int> walks = PvpTrapped.Walks(zone, at);
                    System.Numerics.Vector3? open = Arg("FromX") == null ? null
                        : nav.GetFloorBeneath(zone, new System.Numerics.Vector3(int.Parse(Arg("FromX")), int.Parse(Arg("FromY")), 20_000), 20_000, nav.DefaultFilters);
                    System.Numerics.Vector3? beside = open == null ? null : PvpTrapped.WayOut(zone, at, open.Value);
                    return new
                    {
                        Standing = true, Z = (int)z, Walks = walks, Trapped = PvpTrapped.IsTrapped(walks),
                        Beside = beside is { } found ? $"{(int)found.X},{(int)found.Y},{(int)found.Z}" : null,
                        Away = beside is { } near ? (int?)(int)System.Numerics.Vector3.Distance(near, at) : null,
                    };
                }

                case "scrim-status":
                    return PvpScrim.Status();

                case "scrim-stop":
                    PvpScrim.Stop();
                    return PvpScrim.Status();

                case "stop-server":
                    // The same orderly stop as typing "exit" in the server console. It runs after
                    // this answer is written, off the game loop the stop is about to end.
                    new Thread(() =>
                    {
                        Thread.Sleep(1_500);
                        try { GameServer.Instance?.Stop(); }
                        catch (Exception exception) { Log.Error("8v8 stop-server failed", exception); }
                        Environment.Exit(0);
                    }) { IsBackground = false, Name = "PvpStopServer" }.Start();
                    return new { Stopping = true };

                default:
                    throw new ArgumentException($"Unknown 8v8 command '{request.Command}'.");
            }
        }
    }
}
