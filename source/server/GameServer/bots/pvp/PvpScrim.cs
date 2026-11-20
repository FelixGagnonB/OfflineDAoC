using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.SkillHandler;
using DOL.Logging;
using static DOL.GS.PvpScrim;

namespace DOL.GS
{
    /// <summary>Where scrims are fought: a box inside one connected part of a zone's navmesh.</summary>
    public sealed record PvpArenaBox(ushort Region, int MinX, int MinY, int MaxX, int MaxY, (int X, int Y) SpawnA, (int X, int Y) SpawnB)
    {
        /// <summary>
        /// The south-east basin of Emain Macha (zone 214, region 200). With its invisible walls the
        /// zone's mesh is two areas that do not connect; this box lies inside the larger, playable
        /// one, where it is open ground about 26,000 by 18,000 units. The spawns are 21,500 apart.
        /// </summary>
        public static readonly PvpArenaBox EmainSouthEast = new(200,
            417792 + 34816, 286720 + 38912, 417792 + 61440, 286720 + 57344,
            (417792 + 37500, 286720 + 47000), (417792 + 59000, 286720 + 47000));

        /// <summary>
        /// Six lanes inside that box for matches fought side by side (M2.5), each 6,000 units
        /// long with a group starting at either end, and no two closer than 6,000 to each other:
        /// nothing a bot does reaches 4,500. They were picked from a scan of the box
        /// (<see cref="PvpScrim.ScanLanes"/>, 2026-11-17) for open ground from end to end and
        /// little rise and fall. The first four run north to south and are nearly level (the
        /// ground varies by 48 to 123 units along them); the last two run west to east over
        /// hillier ground (425 and 451) and are used only when more than four series run at once.
        /// </summary>
        public static readonly IReadOnlyList<PvpArenaBox> EmainLanes =
        [
            Lane((475308, 328832), (475308, 334832)),
            Lane((456308, 334332), (456308, 340332)),
            Lane((462308, 333332), (462308, 339332)),
            Lane((468308, 327832), (468308, 333832)),
            Lane((467808, 341832), (473808, 341832)),
            Lane((456308, 327332), (462308, 327332)),
        ];

        private static PvpArenaBox Lane((int X, int Y) a, (int X, int Y) b) =>
            new(200, Math.Min(a.X, b.X) - 2_000, Math.Min(a.Y, b.Y) - 2_000, Math.Max(a.X, b.X) + 2_000, Math.Max(a.Y, b.Y) + 2_000, a, b);
    }

    public sealed record PvpMatchResult(int Number, string Winner, string End, int Seconds, int SecondsToContact,
        int AliveA, int AliveB, DateTime FinishedUtc);

    /// <summary>
    /// Runs scrims: two groups spawn at opposite ends of the arena, walk toward each other, fight,
    /// and the match ends when one side is dead or time runs out; then the next match starts
    /// (docs/PVP-8V8-ROADMAP.md, M2). This first version is the plain baseline the recorder will
    /// measure: each group simply marches on the enemy spawn, and a bot attacks the nearest enemy
    /// it can see. Everything runs on the game loop, driven by a one-second timer.
    ///
    /// Several series can run at once, each in its own lane of the arena (M2.5): this class holds
    /// the shared numbers and the list of running series, and <see cref="PvpScrimRun"/> is one series.
    /// </summary>
    public static class PvpScrim
    {
        public const int SightRange = 3_000;          // an enemy this close is noticed
        public const int EngageRange = 3_500;         // a member this close to its target fights; farther, it marches
        // Owner, 2026-11-17: a group that sees another spreads out at once, so one area mez does
        // not catch it whole. Two groups close 1,500 units in a second or two, too little for a
        // bot to move aside, so for now a group simply travels spread: two rows, fighters in
        // front, casters and healers behind. Enemies are spotted from farther than they are engaged.
        public const int SpotRange = 4_500;
        public const int SpreadSpacing = 450;         // between neighbours in a row; an area mez reaches about 350
        public const int BackRowDepth = 700;
        // The ready check before a match. The buffbot is instant, but casters also have buffs of
        // their own to put up. A member is ready once it has gone QuietSeconds without casting a
        // buff it had not cast yet; the match starts when both groups are ready, or at the timeout.
        public const int MinBuffSeconds = 5;
        public const int QuietSeconds = 8;
        public const int ReadyTimeoutSeconds = 120;
        /// <summary>How far apart the two groups of a lane start. Enemies are spotted at 4,500, so they first walk a little in formation.</summary>
        public const int LaneLength = 6_000;

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpScrim));
        private static volatile PvpScrimRun[] _runs = [];
        private static Timer _timer;
        private static int _ticking;
        private static bool _hooked;

        public static bool Running => _runs.Any(run => run.Running);

        /// <summary>For a GM who watches: what is fighting in each lane right now.</summary>
        public static List<string> LaneLines() => _runs.Where(run => run.Running).Select(run => run.Describe()).ToList();

        /// <summary>The middle of a lane, on the ground: where its two groups meet. Null for a lane that does not exist.</summary>
        public static (ushort Region, Vector3 Point)? LaneMiddle(int lane)
        {
            if (lane < 0 || lane >= PvpArenaBox.EmainLanes.Count)
                return null;
            PvpArenaBox box = PvpArenaBox.EmainLanes[lane];
            int x = (box.SpawnA.X + box.SpawnB.X) / 2, y = (box.SpawnA.Y + box.SpawnB.Y) / 2;
            var nav = PathfindingProvider.Instance;
            Zone zone = WorldMgr.GetRegion(box.Region)?.GetZone(x, y);
            if (zone == null)
                return null;
            Vector3 point = nav.GetFloorBeneath(zone, new Vector3(x, y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                ? floor : new Vector3(x, y, 4_000);
            return (box.Region, point);
        }

        /// <summary>One series in the whole arena, as before there were lanes.</summary>
        public static void Start(PvpGroupTemplate groupA, PvpGroupTemplate groupB, int matches, int realmLevel, int limitSeconds = 300,
            bool squadA = false, bool squadB = false) =>
            Start([(groupA, groupB)], matches, realmLevel, limitSeconds, squadA, squadB, 1, false, false);

        /// <summary>
        /// Starts one series for each pairing and copy, all at once, each in its own lane. A
        /// single series has the whole arena, as before, unless <paramref name="inLane"/> is set.
        /// </summary>
        public static void Start(IReadOnlyList<(PvpGroupTemplate A, PvpGroupTemplate B)> pairs, int matches, int realmLevel,
            int limitSeconds, bool squadA, bool squadB, int copies, bool inLane, bool versus, int vary = 0, int spread = 40)
        {
            Log.Info($"8v8 scrim weights: {PvpWeights.Reload(AppContext.BaseDirectory)}");
            if (Running)
                throw new InvalidOperationException("A scrim is already running. Stop it first.");
            if (pairs.Count == 0)
                throw new ArgumentException("No pairing given.");
            if (pairs.Any(pair => pair.A.Realm == pair.B.Realm))
                throw new ArgumentException("The two groups must be of different realms to be able to fight.");
            if (versus && vary > 0)
                throw new ArgumentException("Versus compares two fixed sets of weights and Vary draws a new set for every team: use one of them.");
            if (versus && PvpWeights.Reference == null)
                throw new ArgumentException($"A comparison needs {PvpWeights.ReferenceFileName} beside the server.");
            copies = Math.Max(1, copies);
            int count = pairs.Count * copies;
            IReadOnlyList<PvpArenaBox> lanes = count == 1 && !inLane ? [PvpArenaBox.EmainSouthEast] : PvpArenaBox.EmainLanes;
            if (count > lanes.Count)
                throw new ArgumentException($"{count} series asked for and there are {lanes.Count} lanes.");
            PvpArena.Clear();
            if (!_hooked)
            {
                GameEventMgr.AddHandler(GameLivingEvent.CastFinished, OnCastFinished);
                _hooked = true;
            }
            string stamp = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            var runs = new List<PvpScrimRun>();
            // Copies of a pairing share its batch name; their match numbers are kept apart (1.., 101..).
            // In a comparison the first copy gives the new weights to the first group of each
            // pairing and the second copy to the other, so each realm fights with both sets
            // against each opponent and what is left is new against old.
            for (int copy = 0; copy < copies; copy++)
            {
                foreach ((PvpGroupTemplate a, PvpGroupTemplate b) in pairs)
                    runs.Add(new PvpScrimRun(runs.Count, lanes[runs.Count], a, b, Math.Max(1, matches), realmLevel,
                        Math.Clamp(limitSeconds, 30, 3600), squadA, squadB, $"{stamp}-{a.Id}-vs-{b.Id}", copy * 100,
                        !versus ? 0 : copy % 2 == 0 ? 1 : 2, vary, spread));
            }
            _runs = runs.ToArray();
            try
            {
                foreach (PvpScrimRun run in runs)
                    run.Begin();
            }
            catch
            {
                Stop();
                throw;
            }
            _timer?.Dispose();
            _timer = new Timer(_ => NpcService.Instance.Post(Tick, 0), null, 1_000, 1_000);
        }

        /// <summary>
        /// How much of the basin a rise of the ground hides: of pairs of points on walkable ground,
        /// drawn at random that far apart, the share with no clear line between them
        /// (bots/pvp/PvpSight.cs). For knowing what sight over the ground changes there.
        /// </summary>
        public static object ScanSight(int pairs, int min, int max)
        {
            PvpArenaBox box = PvpArenaBox.EmainSouthEast;
            Region region = WorldMgr.GetRegion(box.Region);
            var random = new Random(8);
            int tried = 0, hidden = 0, offGround = 0;
            var byDistance = new SortedDictionary<int, (int Tried, int Hidden)>();
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < pairs * 4 && tried < pairs; i++)
            {
                int x1 = random.Next(box.MinX, box.MaxX), y1 = random.Next(box.MinY, box.MaxY);
                double angle = random.NextDouble() * Math.PI * 2;
                int length = random.Next(min, max);
                int x2 = x1 + (int)(Math.Cos(angle) * length), y2 = y1 + (int)(Math.Sin(angle) * length);
                if (x2 < box.MinX || x2 > box.MaxX || y2 < box.MinY || y2 > box.MaxY)
                    continue;
                Zone zone = region?.GetZone(x1, y1);
                if (zone == null || region.GetZone(x2, y2) != zone)
                    continue;
                float z1 = PvpSight.HeightAt(zone, x1, y1), z2 = PvpSight.HeightAt(zone, x2, y2);
                if (float.IsNaN(z1) || float.IsNaN(z2))
                {
                    offGround++;
                    continue;
                }
                (lowest, highest) = (Math.Min(lowest, Math.Min(z1, z2)), Math.Max(highest, Math.Max(z1, z2)));
                bool clear = PvpSight.Clear(zone, new Vector3(x1, y1, z1), new Vector3(x2, y2, z2));
                tried++;
                hidden += clear ? 0 : 1;
                int band = length / 1_000 * 1_000;
                (int t, int h) = byDistance.GetValueOrDefault(band);
                byDistance[band] = (t + 1, h + (clear ? 0 : 1));
            }
            return new
            {
                Pairs = tried, Hidden = hidden, HiddenShare = tried == 0 ? 0 : Math.Round(100.0 * hidden / tried, 1), OffGround = offGround,
                Lowest = lowest, Highest = highest,
                ByDistance = byDistance.Select(band => new { From = band.Key, band.Value.Tried, band.Value.Hidden }).ToList(),
            };
        }

        /// <summary>
        /// Set for a series of bots in the whole basin (scrim-start Roam=1): both groups roam from
        /// point to point as in a play session, to see that roam without a player.
        /// </summary>
        public static bool BotsRoam { get; set; }

        /// <summary>
        /// For testing the player's calls without a player (scrim-call): one member of a group of
        /// bots makes the call, as if it were the player. <paramref name="who"/> is the build or
        /// class of the caller (a healer when empty); for an assist, <paramref name="target"/> is
        /// the build or class of the enemy to call.
        /// </summary>
        public static object TestCall(int lane, string team, string word, string who, string target)
        {
            PvpScrimRun run = _runs.FirstOrDefault(each => each.Running && each.Lane == lane)
                ?? throw new InvalidOperationException($"Nothing is running in lane {lane}.");
            return run.TestCall(team, word, who, target);
        }

        /// <summary>The squad a player leads in a play session that is running; null otherwise.</summary>
        public static PvpSquad SquadOf(GamePlayer player) =>
            player == null ? null : _runs.FirstOrDefault(run => run.Running && run.Leader == player)?.LeaderSquad;

        /// <summary>True for the player of a play session that is running.</summary>
        public static bool IsPlaying(GamePlayer player) => player != null && _runs.Any(run => run.Running && run.Leader == player);

        /// <summary>The length of a play session's fight at most, in seconds: a fight between equals ran five to ten minutes.</summary>
        public const int PlayLimitSeconds = 1_800;

        /// <summary>
        /// The two starts of a play session. "lane": face to face, the groups 6,000 units apart
        /// on level ground, as in the scrims. "basin": opposite ends of the open basin, 21,500
        /// apart and out of sight; the enemy patrols and the player leads the group to find it.
        /// </summary>
        public static PvpArenaBox PlayBox(string start) =>
            string.Equals(start, "basin", StringComparison.OrdinalIgnoreCase) ? PvpArenaBox.EmainSouthEast : PvpArenaBox.EmainLanes[0];

        /// <summary>
        /// Takes the player to the start of a play session and begins it once the player stands
        /// there: a change of region takes the client a moment, and the companions are put around
        /// where the player stands. <paramref name="say"/> receives what happened.
        /// </summary>
        public static void TravelAndStartPlay(GamePlayer player, PvpGroupTemplate enemy, int realmLevel, string start, Action<string> say,
            bool botDrives = false, PvpGroupTemplate own = null)
        {
            PvpArenaBox box = PlayBox(start);
            // The player fights at the rank of the bots: the points are there to spend (before or after this fight).
            if (PvpPlayerReady.RaiseRealmRank(player, realmLevel) is { } raised)
                say(raised);
            // Every session starts with the player's realm abilities and timed abilities ready: the owner,
            // 2026-11-20, "so we can test with everything rather than waiting 30 minutes for purge to be up".
            // Again when both groups are ready (BeginRoaming), for what was used while buffing.
            player.ResetDisabledSkills();
            (ushort region, Vector3 point) = PlayStart(box);
            var spot = new Point3D((int)point.X, (int)point.Y, (int)point.Z);
            bool There() => player.ObjectState == GameObject.eObjectState.Active && player.CurrentRegionID == region && player.IsWithinRadius(spot, 2_500);
            void Begin()
            {
                try
                {
                    StartPlay(player, enemy, realmLevel, box, botDrives, own);
                    say($"Seven companions are with you at realm rank {realmLevel / 10 + 1}; {enemy.Id} is " +
                        (box == PvpArenaBox.EmainSouthEast ? "at the far end of the basin, to the east, and will roam" : "6,000 units to the south") +
                        ". Both groups buff for about a minute first. /8v8 stop ends it.");
                    if (botDrives && SquadOf(player)?.Driver is { } driver)
                        say($"{driver.Name} drives: target it and /stick. It sets off when both groups are buffed, and roams until it finds the enemy.");
                }
                catch (Exception exception)
                {
                    say("The fight could not start: " + exception.Message);
                }
            }
            if (There())
            {
                Begin();
                return;
            }
            say("Taking you to the start in Emain Macha; the fight begins when you are there.");
            player.MoveTo(region, spot.X, spot.Y, spot.Z, player.Heading);
            int waited = 0;
            new ECSGameTimer(player, _ =>
            {
                // While the client loads the zone the player is not "playing": that is the wait, not a reason to give up.
                if (player.Client == null || player.ObjectState == GameObject.eObjectState.Deleted || ++waited > 120)
                    return 0;
                if (player.Client.IsPlaying != true || !There())
                    return 1_000;
                Begin();
                return 0;
            }, 2_000);
        }

        /// <summary>Where a play session starts: the player's end of the lane or the basin, on the ground.</summary>
        public static (ushort Region, Vector3 Point) PlayStart(PvpArenaBox box = null)
        {
            box ??= PvpArenaBox.EmainSouthEast;
            var nav = PathfindingProvider.Instance;
            Zone zone = WorldMgr.GetRegion(box.Region)?.GetZone(box.SpawnA.X, box.SpawnA.Y);
            Vector3 point = zone != null && nav.GetFloorBeneath(zone, new Vector3(box.SpawnA.X, box.SpawnA.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                ? floor : new Vector3(box.SpawnA.X, box.SpawnA.Y, 4_000);
            return (box.Region, point);
        }

        /// <summary>
        /// A play session (docs/PVP-8V8-ROADMAP.md, M9.4): the player with seven companions of the
        /// realm's main group, both under the squad brain, against a group of eight that starts at
        /// the far end of the basin and roams. One fight; it is recorded like a scrim. The player
        /// must stand at <see cref="PlayStart"/> already: moving a player between regions takes
        /// the client a moment, and the companions are put around where the player stands.
        /// </summary>
        public static void StartPlay(GamePlayer player, PvpGroupTemplate enemy, int realmLevel, PvpArenaBox box = null, bool botDrives = false,
            PvpGroupTemplate own = null)
        {
            box ??= PvpArenaBox.EmainSouthEast;
            Log.Info($"8v8 play weights: {PvpWeights.Reload(AppContext.BaseDirectory)}");
            if (Running)
                throw new InvalidOperationException("A scrim or a session is already running. Stop it first.");
            if (enemy.Realm == player.Realm)
                throw new ArgumentException("The enemy group must be of another realm.");
            if (own == null || own.Realm != player.Realm)
                own = PvpBuildCatalog.Groups.First(group => group.Realm == player.Realm);
            PvpArena.Clear();
            if (!_hooked)
            {
                GameEventMgr.AddHandler(GameLivingEvent.CastFinished, OnCastFinished);
                _hooked = true;
            }
            var run = new PvpScrimRun(0, box, own, enemy, 1, realmLevel, PlayLimitSeconds, true, true,
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-play-{own.Id}-vs-{enemy.Id}", 0, 0, 0, 40, player, botDrives);
            _runs = [run];
            try
            {
                run.Begin();
            }
            catch
            {
                Stop();
                throw;
            }
            _timer?.Dispose();
            _timer = new Timer(_ => NpcService.Instance.Post(Tick, 0), null, 1_000, 1_000);
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            foreach (PvpScrimRun run in _runs)
                run.Stop();
            PvpArena.Clear();
        }

        private static void Tick(int _)
        {
            if (Interlocked.Exchange(ref _ticking, 1) != 0)
                return;
            try
            {
                foreach (PvpScrimRun run in _runs)
                    run.Tick();
                if (!Running)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
            }
            finally
            {
                Volatile.Write(ref _ticking, 0);
            }
        }

        // Every finished cast in the world comes through here, from game threads; a series only
        // looks at those of its own members while it is buffing.
        private static void OnCastFinished(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GameLiving caster || args is not CastingEventArgs { SpellHandler: { Spell: { } spell } } cast)
                return;
            foreach (PvpScrimRun run in _runs)
                run.NoteCast(caster, spell, cast.Target);
        }

        /// <summary>Every series: its lane, pairing, phase, results so far.</summary>
        public static object Status() => new { Running, Runs = _runs.Select(run => run.Status()).ToList() };

        /// <summary>
        /// For choosing lanes: every straight stretch of <paramref name="distance"/> in the arena
        /// box, north to south and west to east, <paramref name="step"/> apart, that is open ground
        /// from end to end, with how much the ground rises and falls along it.
        /// </summary>
        public static object ScanLanes(int distance, int step)
        {
            var nav = PathfindingProvider.Instance;
            PvpArenaBox box = PvpArenaBox.EmainSouthEast;
            distance = distance > 0 ? distance : LaneLength;
            step = Math.Max(250, step);
            var found = new List<object>();
            foreach (bool northSouth in new[] { true, false })
            {
                int maxX = box.MaxX - 700 - (northSouth ? 0 : distance), maxY = box.MaxY - 700 - (northSouth ? distance : 0);
                for (int x = box.MinX + 700; x <= maxX; x += step)
                {
                    for (int y = box.MinY + 700; y <= maxY; y += step)
                    {
                        (int X, int Y) end = northSouth ? (x, y + distance) : (x + distance, y);
                        Zone zone = WorldMgr.GetRegion(box.Region)?.GetZone(x, y);
                        if (zone == null)
                            continue;
                        var heights = new List<float>();
                        Vector3? first = null, last = null;
                        for (int i = 0; i <= 6; i++)
                        {
                            var point = new Vector3(x + (end.X - x) * i / 6f, y + (end.Y - y) * i / 6f, 20_000);
                            if (nav.GetFloorBeneath(zone, point, 20_000, nav.DefaultFilters) is not { } floor || floor.Z >= 19_999)
                                break;
                            heights.Add(floor.Z);
                            if (i == 0) first = floor;
                            if (i == 6) last = floor;
                        }
                        if (heights.Count < 7 || !nav.HasLineOfSight(zone, first.Value, last.Value, nav.DefaultFilters))
                            continue;
                        found.Add(new { NS = northSouth, X = x, Y = y, Rise = (int)(heights.Max() - heights.Min()), EndsDiffer = (int)Math.Abs(heights[0] - heights[6]) });
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Whether each lane is usable: ground under both starting points and under the middle,
        /// and open ground in a straight line between the two.
        /// </summary>
        public static object CheckLanes()
        {
            var nav = PathfindingProvider.Instance;
            return PvpArenaBox.EmainLanes.Select((lane, index) =>
            {
                Zone zone = WorldMgr.GetRegion(lane.Region)?.GetZone(lane.SpawnA.X, lane.SpawnA.Y);
                Vector3? Ground((int X, int Y) point) => zone == null ? null
                    : nav.GetFloorBeneath(zone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999 ? floor : null;
                Vector3? a = Ground(lane.SpawnA), b = Ground(lane.SpawnB);
                Vector3? middle = Ground(((lane.SpawnA.X + lane.SpawnB.X) / 2, (lane.SpawnA.Y + lane.SpawnB.Y) / 2));
                return new
                {
                    Lane = index, Zone = zone?.Description, A = $"{lane.SpawnA.X},{lane.SpawnA.Y}", B = $"{lane.SpawnB.X},{lane.SpawnB.Y}",
                    GroundA = a?.Z, GroundB = b?.Z, GroundMiddle = middle?.Z,
                    OpenLine = a != null && b != null && nav.HasLineOfSight(zone, a.Value, b.Value, nav.DefaultFilters),
                };
            }).ToList();
        }
    }

    /// <summary>
    /// One series of matches between two groups in one lane: spawn, ready check, patrol, fight,
    /// result, and the next match. Created and ticked by <see cref="PvpScrim"/> on the game loop.
    /// </summary>
    public sealed class PvpScrimRun
    {
        private enum Phase { Idle, Buffing, Roaming, Fighting }

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpScrim));

        private Phase _phase = Phase.Idle;
        private readonly PvpArenaBox _arena;
        private readonly int _lane, _numberOffset;
        // A comparison of two sets of weights: 0 none, 1 the first group has the new set, 2 the second has it.
        private readonly int _versus;
        // Above 0: each team of each match fights with its own draw of the weights, with this
        // share of them moved by this spread (bots/pvp/PvpWeightDraw.cs).
        private readonly int _vary, _spread;
        private PvpRecording _recording;
        // Everything this series put in the world for the current match, buffbots included.
        private readonly List<GameBot> _bots = [];
        private PvpGroupTemplate _groupA, _groupB;
        private int _realmLevel, _matchesWanted, _matchNumber;
        private long _phaseStartTick, _matchStartTick, _contactTick;
        private List<GameBot> _teamA = [], _teamB = [];
        private readonly List<PvpMatchResult> Results = [];
        private string _lastError;
        private string _batch;
        // Whether each side fights with the squad brain, and the squads of the current match (null for the old brain).
        private bool _squadBrainA, _squadBrainB;
        private PvpSquad _squadA, _squadB;
        private (int X, int Y) _spawnA, _spawnB;
        private int _waypointA, _waypointB;
        // Ready check: the buffs cast in this buffing phase (caster, spell, target), and when each
        // member last finished one that was new. Filled from game threads by OnCastFinished.
        private ConcurrentDictionary<(GameLiving Caster, int Spell, GameLiving Target), byte> _buffsCast = new();
        private ConcurrentDictionary<GameLiving, long> _lastNewBuffTick = new();
        private HashSet<GameLiving> _members = [];
        private object _lastReady;
        // Healers that stayed back on incoming in the current match, to record it once each.
        private readonly HashSet<GameBot> _hungBack = [];
        private readonly HashSet<GameBot> _stoodAtRange = [];
        // Set in a play session in the basin: the groups roam from point to point by this draw.
        private Random _roam;
        // Whether each side had an enemy in sight at its last march, to catch the moment one comes into sight.
        private bool _sawEnemyA, _sawEnemyB;
        // A play session: the player who leads side A, with seven companions in place of a group of eight.
        private readonly GamePlayer _leader;
        internal GamePlayer Leader => _leader;
        internal PvpSquad LeaderSquad => _leader == null ? null : _squadA;
        internal int Lane => _lane;

        internal object TestCall(string team, string word, string who, string target)
        {
            bool a = !string.Equals(team, "B", StringComparison.OrdinalIgnoreCase);
            PvpSquad squad = (a ? _squadA : _squadB) ?? throw new InvalidOperationException("That side has no squad brain.");
            List<GameBot> own = a ? _teamA : _teamB, enemy = a ? _teamB : _teamA;
            bool Is(GameBot bot, string asked) => string.Equals(bot.PvpBuild.Id, asked, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(PvpMember.ClassOf(bot).ToString(), asked, StringComparison.OrdinalIgnoreCase);
            GameBot caller = own.FirstOrDefault(bot => bot.IsAlive && (string.IsNullOrEmpty(who) ? PvpMember.RoleOf(bot) == PvpRole.Heal : Is(bot, who)))
                ?? throw new InvalidOperationException("No such living member to make the call.");
            if (string.Equals(word, "assist", StringComparison.OrdinalIgnoreCase))
            {
                GameBot called = enemy.FirstOrDefault(bot => bot.IsAlive && Is(bot, target))
                    ?? throw new InvalidOperationException("No such living enemy to call.");
                return new { Caller = caller.Name, Assist = called.Name, Taken = squad.CallAssist(caller, called) };
            }
            PvpOrder order = PvpCalls.OrderFor(word) ?? throw new ArgumentException("push, regroup, hold, engage, extend or assist.");
            squad.CallOrder(caller, order);
            return new { Caller = caller.Name, Order = order.ToString() };
        }
        // A play session in which a bot drives: the companions roam by themselves, as the enemy does, and the player follows.
        private readonly bool _botDrives;

        /// <summary>Side A as the other side sees it: its bots, and the player who leads them in a play session.</summary>
        private IReadOnlyList<GameLiving> SideA => _leader == null ? _teamA : _teamA.Cast<GameLiving>().Append(_leader).ToList();

        public bool Running => _phase != Phase.Idle;
        public int MatchLimitSeconds { get; }

        internal PvpScrimRun(int lane, PvpArenaBox arena, PvpGroupTemplate groupA, PvpGroupTemplate groupB, int matches, int realmLevel,
            int limitSeconds, bool squadA, bool squadB, string batch, int numberOffset, int versus, int vary = 0, int spread = 40,
            GamePlayer leader = null, bool botDrives = false)
        {
            _leader = leader;
            _botDrives = botDrives && leader != null;
            (_versus, _vary, _spread) = (versus, vary, spread);
            (_lane, _arena, _groupA, _groupB, _matchesWanted, _realmLevel) = (lane, arena, groupA, groupB, matches, realmLevel);
            (_squadBrainA, _squadBrainB, _batch, _numberOffset) = (squadA, squadB, batch, numberOffset);
            MatchLimitSeconds = limitSeconds;
        }

        internal void Begin() => BeginMatch();

        internal void Stop()
        {
            _phase = Phase.Idle;
            _recording?.End(null);
            PvpArena.Remove(_bots);
            _bots.Clear();
        }

        private void BeginMatch()
        {
            _matchNumber++;
            // Sides swap every match so neither group always has the same end of the arena.
            bool swapped = _matchNumber % 2 == 0 && _leader == null;
            (int X, int Y) spawnA = swapped ? _arena.SpawnB : _arena.SpawnA;
            (int X, int Y) spawnB = swapped ? _arena.SpawnA : _arena.SpawnB;
            (_spawnA, _spawnB, _waypointA, _waypointB) = (spawnA, spawnB, 0, 0);
            _bots.Clear();
            // In a play session side A is the player and seven companions, put around where the player stands.
            _teamA = _leader != null
                ? PvpArena.SpawnCompanions(_leader, _realmLevel, _bots, _groupA).ToList()
                : PvpArena.SpawnGroup(_groupA, _arena.Region, spawnA.X, spawnA.Y, _realmLevel, _bots).ToList();
            _teamB = PvpArena.SpawnGroup(_groupB, _arena.Region, spawnB.X, spawnB.Y, _realmLevel, _bots).ToList();
            _squadA = _squadBrainA ? new PvpSquad(_teamA, _leader != null ? [_leader] : null) : null;
            _squadB = _squadBrainB ? new PvpSquad(_teamB) : null;
            // The groups buff where they came in, standing together, and take their rows at the ready
            // (TakePlaces): a buff on the group reaches 1,000 units, and from the end of a row the far
            // members stood 1,140 away. In twelve matches a third of Hibernia's members began the
            // fight without one or other of the Druid's resistances, which it had cast two or three times.
            if (_botDrives)
            {
                // The companions do not follow the player in this session: the squad places them, and one of them leads.
                foreach (GameBot bot in _teamA)
                {
                    bot.StopFollowing();
                    if (bot.Brain is BotBrain brain)
                    {
                        brain.PvpSpreadOut = true;
                        brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                    }
                }
                if (_squadA != null)
                    _squadA.Driver = PvpFormation.DriverOf(_teamA);
                _roam = new Random();
            }
            else if ((_leader != null || PvpScrim.BotsRoam) && _arena == PvpArenaBox.EmainSouthEast)
                _roam = new Random();
            // Each group sets off for a point of its own draw, not both for the same one.
            if (_roam != null)
                (_waypointA, _waypointB) = (_roam.Next(9), _roam.Next(9));
            PvpWeights drawA = _vary > 0 ? PvpWeightDraw.Vary(PvpWeights.Current, Random.Shared, _vary, _spread) : null;
            PvpWeights drawB = _vary > 0 ? PvpWeightDraw.Vary(PvpWeights.Current, Random.Shared, _vary, _spread) : null;
            foreach (GameBot bot in _teamA)
                bot.PvpWeightsSet = drawA ?? (_versus == 2 ? PvpWeights.Reference : null);
            foreach (GameBot bot in _teamB)
                bot.PvpWeightsSet = drawB ?? (_versus == 1 ? PvpWeights.Reference : null);
            _buffsCast = new();
            _lastNewBuffTick = new();
            _hungBack.Clear();
            _stoodAtRange.Clear();
            _members = _teamA.Concat(_teamB).Cast<GameLiving>().ToHashSet();
            SetSelfBuffsOnly(true);
            _phase = Phase.Buffing;
            _phaseStartTick = _matchStartTick = GameLoop.GameLoopTime;
            _contactTick = 0;
            _recording = PvpRecorder.Begin(_numberOffset + _matchNumber, _groupA, _groupB, _teamA, _teamB, _realmLevel, _batch, _leader);
            if (_teamA.Count > 0)
                PvpObserver.Say(_teamA[0], $"Lane {_lane}: match {_numberOffset + _matchNumber}, {_groupA.Id} against {_groupB.Id}, is buffing " +
                    $"(recording {_batch}-match-{_numberOffset + _matchNumber:000})");
        }

        /// <summary>
        /// A fresh group stands in its travelling formation from the start, facing where the
        /// enemy starts: in a lane the two groups are too close to form up on the way.
        /// </summary>
        private void FormUp(List<GameBot> team, (int X, int Y) enemySpawn)
        {
            var centre = new Vector2((float)team.Average(bot => bot.X), (float)team.Average(bot => bot.Y));
            Vector2 toward = new Vector2(enemySpawn.X, enemySpawn.Y) - centre;
            if (toward.LengthSquared() < 1)
                return;
            // The front row a row's depth ahead of where the group spawned, the back row on the spot.
            Vector2 front = centre + Vector2.Normalize(toward) * BackRowDepth;
            Dictionary<GameBot, Vector3> places = SpreadPlaces(team, new Vector3(front.X, front.Y, (float)team.Average(bot => bot.Z)));
            if (places == null)
                return;
            foreach ((GameBot bot, Vector3 place) in places)
                bot.MoveTo(bot.CurrentRegionID, (int)Math.Round(place.X), (int)Math.Round(place.Y), (int)Math.Round(place.Z), bot.GetHeading(new Point2D(enemySpawn.X, enemySpawn.Y)));
        }

        internal void Tick()
        {
            if (_phase == Phase.Idle)
                return;
            try
            {
                long now = GameLoop.GameLoopTime;
                _recording?.Snapshot();
                int aliveA = _teamA.Count(bot => bot.IsAlive) + (_leader is { IsAlive: true } ? 1 : 0), aliveB = _teamB.Count(bot => bot.IsAlive);
                // The player has left the game: the session is over.
                if (_leader != null && (_leader.ObjectState != GameObject.eObjectState.Active || _leader.Client?.IsPlaying != true))
                {
                    EndMatch(_groupB.Id, "the player left", aliveA, aliveB);
                    return;
                }

                if (_phase == Phase.Buffing)
                {
                    // A member that has buffed itself dry rests and goes on: the groups start rested in any
                    // case, and a Druid short of power left its last resistance uncast.
                    foreach (GameBot bot in _teamA.Concat(_teamB))
                    {
                        if (bot.IsAlive && !bot.IsCasting && bot.MaxMana > 0 && bot.ManaPercent < 40)
                            bot.Mana = bot.MaxMana;
                    }
                    long waited = now - _phaseStartTick;
                    List<GameBot> busy = _teamA.Concat(_teamB).Where(bot => bot.IsAlive && StillBuffing(bot, now)).ToList();
                    if (waited < MinBuffSeconds * 1000 || busy.Count > 0 && waited < ReadyTimeoutSeconds * 1000)
                        return;
                    BeginRoaming(now, busy);
                }

                if (aliveA == 0 || aliveB == 0)
                {
                    EndMatch(aliveA == aliveB ? "draw" : aliveA > 0 ? _groupA.Id : _groupB.Id, "wipe", aliveA, aliveB);
                    return;
                }
                if (now - _matchStartTick > MatchLimitSeconds * 1000)
                {
                    EndMatch(aliveA == aliveB ? "draw" : aliveA > aliveB ? _groupA.Id : _groupB.Id, "time limit", aliveA, aliveB);
                    return;
                }

                _squadA?.Decide(_teamB);
                _squadB?.Decide(SideA);
                Engage(_teamA, _teamB);
                Engage(_teamB, SideA);
                if (_contactTick == 0 && (_teamA.Concat(_teamB).Any(bot => bot.InCombat) || _leader?.InCombat == true))
                {
                    _contactTick = now;
                    if (_teamA.Count > 0)
                        PvpObserver.Say(_teamA[0], $"Lane {_lane}: first blow, {(now - _matchStartTick) / 1000} s into the match");
                    if (_squadA != null) _squadA.FightStarted = true;
                    if (_squadB != null) _squadB.FightStarted = true;
                    SetSelfBuffsOnly(false);
                    _phase = Phase.Fighting;
                }
                if (_leader == null || _botDrives)
                    March(_teamA, _teamB, _spawnA, _spawnB, ref _waypointA);
                else
                    TravelWithLeader();
                March(_teamB, SideA, _spawnB, _spawnA, ref _waypointB);
                _squadA?.ClearPets();
                _squadB?.ClearPets();
                _squadA?.StandOff();
                _squadB?.StandOff();
                _squadA?.KeepApart();
                _squadB?.KeepApart();
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                Log.Error($"8v8 scrim tick failed in lane {_lane} ({_batch})", exception);
                if (_leader?.Client?.IsPlaying == true)
                    _leader.Out.SendMessage($"[8v8] The fight stopped on an error and its bots were removed: {exception.Message}",
                        PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
                Stop();
            }
        }

        /// <summary>A bot that is not fighting takes up the nearest living enemy it can see.</summary>
        /// <summary>
        /// A play session: a companion with nothing to fight travels with the player, as it did
        /// before the session, and does not march on the enemy by itself. Where the group goes
        /// is the player's choice.
        /// </summary>
        private void TravelWithLeader()
        {
            // The enemy is in sight: the companions leave the clump they travel in and take their
            // places in rows around the player, the player's own place left free. In the six
            // sessions of 2026-11-19 all eight stood inside one area mez eight seconds before the
            // first blow, and two to seven of them still did at the first blow.
            bool overGround = PvpWeights.Of(_teamA.FirstOrDefault()).SightOverGround;
            List<GameBot> seen = _teamB.Where(enemy => enemy.IsAlive &&
                (_leader.IsWithinRadius(enemy, PvpFormation.FormRange) && (!overGround || PvpSight.Clear(_leader, enemy)) ||
                 _teamA.Any(bot => bot.IsAlive && PvpSight.Sees(bot, enemy, PvpFormation.FormRange, PvpWeights.Of(bot))))).ToList();
            Dictionary<GameBot, Vector3> places = seen.Count == 0 ? null : LeaderPlaces(_teamA.Where(bot => bot.IsAlive).ToList(),
                new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z)));
            foreach (GameBot bot in _teamA)
            {
                if (bot.Brain is BotBrain own)
                    own.PvpSpreadOut = places != null;
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.InCombat || brain.HasAggro ||
                    _teamB.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, EngageRange)))
                    continue;
                if (places != null)
                {
                    if (!places.TryGetValue(bot, out Vector3 place))
                        continue;
                    if (bot.FollowTarget != null)
                        bot.StopFollowing();
                    if (brain.FSM.GetCurrentState()?.StateType == eFSMStateType.FOLLOW)
                        brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                    // The player moves, and its places with it: a new order every other second.
                    if (!bot.IsWithinRadius(new Point3D((int)place.X, (int)place.Y, (int)place.Z), 120) &&
                        !(bot.IsMoving && GameLoop.GameLoopTime % 2_000 >= 1_000))
                        bot.PathTo(place, bot.MaxSpeed);
                    continue;
                }
                if (!bot.IsWithinRadius(_leader, BotManager.MAX_FOLLOW_DISTANCE))
                    bot.PathTo(new Vector3(_leader.X, _leader.Y, _leader.Z), bot.MaxSpeed);
                else if (bot.FollowTarget != _leader)
                {
                    bot.Follow(_leader, BotManager.FOLLOW_DISTANCE, BotManager.MAX_FOLLOW_DISTANCE);
                    brain.FSM.SetCurrentState(eFSMStateType.FOLLOW);
                }
            }
        }

        private void Engage(List<GameBot> team, IReadOnlyList<GameLiving> enemies)
        {
            var nav = PathfindingProvider.Instance;
            // While a group holds, its fighters go for nobody (bots/pvp/PvpReading.cs).
            bool holds = (team == _teamA ? _squadA : _squadB)?.Order == PvpOrder.Hold;
            foreach (GameBot bot in team)
            {
                // A shield fighter staying beside its ward has chosen not to attack.
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || brain.HasAggro || _squadA?.IsHolding(bot) == true || _squadB?.IsHolding(bot) == true ||
                    holds && PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee)
                    continue;
                GameLiving target = enemies
                    .Where(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, SightRange))
                    .OrderBy(enemy => bot.GetDistanceTo(enemy))
                    .FirstOrDefault(enemy => nav.HasLineOfSight(bot.CurrentZone, new Vector3(bot.X, bot.Y, bot.Z),
                        new Vector3(enemy.X, enemy.Y, enemy.Z), nav.DefaultFilters) &&
                        PvpSight.Sees(bot, enemy, SightRange, PvpWeights.Of(bot)));
                if (target == null)
                    continue;
                brain.AddToAggroList(target, 1);
                brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
            }
        }

        /// <summary>
        /// Bots with nothing to fight move as a group. While no member can see an enemy the group
        /// does not know where the other side is: it patrols (the middle of the arena, the far
        /// end, back to its own end, and round again). Once any member spots enemies it goes for
        /// them spread out, each member to its own place in two rows. The order is repeated every
        /// few seconds because the brain's own idle logic stops a walking bot.
        /// </summary>
        private void March(List<GameBot> team, IReadOnlyList<GameLiving> enemies, (int X, int Y) own, (int X, int Y) far, ref int waypoint)
        {
            List<GameBot> members = team.Where(bot => bot.IsAlive).ToList();
            if (members.Count == 0)
                return;
            // A group that holds stands where it is and lets the enemy come.
            if ((team == _teamA ? _squadA : _squadB)?.Order == PvpOrder.Hold)
            {
                foreach (GameBot bot in members.Where(bot => bot.IsMoving && !bot.IsCasting))
                {
                    bot.StopMovingOnPath();
                    bot.StopMoving();
                }
                return;
            }
            // In the open basin a group sees the other from farther than in a lane, where the two start in sight.
            bool basin = _arena == PvpArenaBox.EmainSouthEast;
            int spot = basin ? PvpFormation.FormRange : SpotRange;
            PvpWeights sight = PvpWeights.Of(members[0]);
            List<GameLiving> seen = enemies.Where(enemy => enemy.IsAlive && members.Any(bot => PvpSight.Sees(bot, enemy, spot, sight))).ToList();
            Vector3 goal;
            Dictionary<GameBot, Vector3> places = null;
            if (seen.Count > 0)
            {
                goal = new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z));
            }
            else
            {
                (int X, int Y)[] route = [((own.X + far.X) / 2, (own.Y + far.Y) / 2), far, ((own.X + far.X) / 2, (own.Y + far.Y) / 2), own];
                // A play session in the basin: each group goes from one point of the basin to another as it
                // pleases, so the two do not meet at the same place and from the same side every time.
                if (_roam != null && basin)
                    route = PvpFormation.RoamPoints(_arena.MinX, _arena.MinY, _arena.MaxX, _arena.MaxY, 3_000);
                (int X, int Y) next = route[waypoint % route.Length];
                double x = members.Average(bot => bot.X), y = members.Average(bot => bot.Y);
                if (Math.Sqrt(Math.Pow(next.X - x, 2) + Math.Pow(next.Y - y, 2)) < 900)
                    next = route[waypoint = _roam != null && basin
                        ? PvpFormation.NextRoamPoint(waypoint % route.Length, route.Length, _roam.Next()) : (waypoint + 1) % route.Length];
                // The route has no heights: take the ground under the waypoint from the navmesh,
                // as the spawn does. A goal above or below the mesh gives no path and nobody moves.
                var nav = PathfindingProvider.Instance;
                Zone zone = members[0].CurrentZone;
                goal = nav.GetFloorBeneath(zone, new Vector3(next.X, next.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                    ? floor
                    : nav.GetClosestPoint(zone, new Vector3(next.X, next.Y, 5_000), 256, 256, 20_000, nav.DefaultFilters)
                      ?? new Vector3(next.X, next.Y, (float)members.Average(bot => bot.Z));
            }
            // On the road in the basin a group follows its driver, the member with the speed song, close
            // behind it as players on /stick do; it takes its rows when the enemy is in sight. (A clump
            // 300 apart set around a point ahead, tried for a night, was to the owner's eye still "a
            // rectangle", and with the driver stopping for its songs the driver was the one left behind.)
            bool onTheRoad = basin && seen.Count == 0;
            GameBot driver = onTheRoad ? PvpFormation.DriverOf(members) : null;
            places = onTheRoad ? null : SpreadPlaces(members, goal);
            bool sawBefore = team == _teamA ? _sawEnemyA : _sawEnemyB;
            bool sighted = seen.Count > 0 && !sawBefore;
            if (team == _teamA) _sawEnemyA = seen.Count > 0; else _sawEnemyB = seen.Count > 0;
            foreach (GameBot bot in team)
            {
                if (!bot.IsAlive || bot.IsCasting || bot.IsIncapacitated)
                    continue;
                // On incoming a healer that only heals stays out of the enemy's reach, and comes
                // in once the fight has settled (bots/pvp/PvpPositioning.cs).
                if (seen.Count > 0 && (team == _teamA ? _squadA : _squadB) != null)
                {
                    PvpWeights weights = PvpWeights.Of(bot);
                    bool onlyHeals = PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Healer &&
                        weights.HealsFirstBelowFor(bot.PvpBuild.Id, PvpMember.ClassOf(bot)) > 100;
                    int nearestSeen = (int)seen.Min(enemy => bot.GetDistanceTo(enemy));
                    // Before the first blow the whole back row stops at range and its fighters go on (PvpPositionWeights.BackRowStopsAt).
                    if (weights.Position.BackRowStops(PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee, nearestSeen, _contactTick != 0))
                    {
                        if (_stoodAtRange.Add(bot))
                            PvpRecorder.Decision(bot, new { kind = "StopAtRange", score = nearestSeen }, [], null);
                        if (bot.IsMoving && !bot.IsCasting)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                    if (weights.Position.HealerHangsBack(onlyHeals, nearestSeen,
                            _contactTick == 0 ? -1 : (GameLoop.GameLoopTime - _contactTick) / 1000.0))
                    {
                        if (_hungBack.Add(bot))
                            PvpRecorder.Decision(bot, new { kind = "HangBack", score = 0 }, [], null);
                        if (bot.IsMoving)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                }
                // Fighting, or about to: an enemy is within reach. Otherwise it marches, even if
                // the brain still holds an old target from before it fell behind.
                bool straggler = !enemies.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, EngageRange));
                // Once the fight has started, a member within reach of the enemy is the brain's
                // alone. A caster whose targets are all asleep has no attack target, and a march
                // order here made it walk in the middle of its casts, which then never started
                // (the Sorcerer made 92 mez decisions for 19 casts).
                if (!straggler && (_contactTick != 0 || bot.InCombat || bot.Brain is BotBrain { HasAggro: true }))
                    continue;
                if (straggler && bot.Brain is BotBrain { HasAggro: true } behind)
                {
                    behind.ClearAggroList();
                    behind.FSM.SetCurrentState(eFSMStateType.IDLE);
                }
                if (driver != null && bot != driver)
                {
                    // Each a little farther back than the one before, so that they string out behind it.
                    if (bot.FollowTarget != driver)
                    {
                        int back = PvpFormation.FollowDistance(members.IndexOf(bot));
                        bot.Follow(driver, back, PvpFormation.FollowGivesUpBeyond);
                    }
                    continue;
                }
                if (bot.FollowTarget != null)
                    bot.StopFollowing();
                // The driver does not leave its group behind: it waits for whoever has dropped back.
                if (driver == bot)
                {
                    int farthest = members.Where(member => member != bot && member.IsAlive)
                        .Select(member => bot.GetDistanceTo(member)).DefaultIfEmpty(0).Max();
                    bool waits = PvpFormation.DriverWaits(farthest, _driversWaiting.Contains(bot));
                    if (waits ? _driversWaiting.Add(bot) : _driversWaiting.Remove(bot))
                        PvpRecorder.Decision(bot, new { kind = waits ? "DriverWaits" : "DriverGoesOn", score = farthest }, [], null);
                    if (waits)
                    {
                        if (bot.IsMoving)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                }
                // The moment the enemy comes into sight the order to take the rows goes out at once.
                if (!sighted && bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(places != null && places.TryGetValue(bot, out Vector3 place) ? place : goal, bot.MaxSpeed);
            }
        }

        /// <summary>
        /// The companions' places around the player once the enemy is in sight: the same two rows
        /// as <see cref="SpreadPlaces"/>, but set on the player, who goes where it likes. The
        /// player's row has the player in its middle; the fighters' row is a row's depth nearer
        /// the enemy than the others'.
        /// </summary>
        private Dictionary<GameBot, Vector3> LeaderPlaces(List<GameBot> members, Vector3 enemyCentre)
        {
            var centre = new Vector2(_leader.X, _leader.Y);
            Vector2 toward = new Vector2(enemyCentre.X, enemyCentre.Y) - centre;
            if (toward.LengthSquared() < 1 || members.Count == 0)
                return null;
            toward = Vector2.Normalize(toward);
            var across = new Vector2(-toward.Y, toward.X);
            bool leaderFights = PvpMember.IsFighter(_leader);
            var nav = PathfindingProvider.Instance;
            var places = new Dictionary<GameBot, Vector3>();
            Vector3 Ground(GameBot bot, Vector2 point) =>
                nav.GetFloorBeneath(bot.CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                    ? floor : new Vector3(point.X, point.Y, _leader.Z);
            foreach (IGrouping<bool, GameBot> row in members.GroupBy(bot => PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee))
            {
                List<GameBot> inRow = row.ToList();
                if (!row.Key)
                {
                    // The back row: a healer at each end, the casters between them.
                    List<GameBot> healers = inRow.Where(bot => PvpMember.RoleOf(bot) == PvpRole.Heal).ToList();
                    if (healers.Count >= 2)
                        inRow = [healers[0], .. inRow.Where(bot => !healers.Contains(bot)), .. healers.Skip(1)];
                }
                float[] offsets = PvpFormation.RowOffsets(inRow.Count, row.Key == leaderFights, SpreadSpacing);
                // Measured from the player: the fighters' row ahead of a player in the back row, the back row behind a player who fights.
                float ahead = row.Key == leaderFights ? 0 : row.Key ? BackRowDepth : -BackRowDepth;
                for (int slot = 0; slot < inRow.Count; slot++)
                    places[inRow[slot]] = Ground(inRow[slot], centre + across * offsets[slot] + toward * ahead);
            }
            // A shield fighter stands beside the member it guards, on the enemy's side of it: guard reaches 256 units.
            foreach (GameBot bot in members)
            {
                GameLiving ward = _squadA?.WardOf(bot);
                if (ward == _leader && ward != null)
                    places[bot] = Ground(bot, centre + toward * 100);
                else if (ward is GameBot wardBot && places.TryGetValue(wardBot, out Vector3 beside))
                    places[bot] = new Vector3(beside.X + toward.X * 100, beside.Y + toward.Y * 100, beside.Z);
            }
            return places;
        }

        /// <summary>
        /// Where each member heads when the group has spotted the enemy: two rows across the line
        /// of approach, fighters aimed at the enemy and the rest a little short of it, neighbours
        /// <see cref="SpreadSpacing"/> apart.
        /// </summary>
        private Dictionary<GameBot, Vector3> SpreadPlaces(List<GameBot> members, Vector3 enemyCentre)
        {
            var centre = new Vector2((float)members.Average(bot => bot.X), (float)members.Average(bot => bot.Y));
            Vector2 toward = new Vector2(enemyCentre.X, enemyCentre.Y) - centre;
            if (toward.LengthSquared() < 1)
                return null;
            toward = Vector2.Normalize(toward);
            var across = new Vector2(-toward.Y, toward.X);
            var nav = PathfindingProvider.Instance;
            var places = new Dictionary<GameBot, Vector3>();
            foreach (IGrouping<bool, GameBot> row in members.GroupBy(bot => PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee))
            {
                List<GameBot> inRow = row.ToList();
                // The back row: one healer at each end, the casters between them (PvpSpreadWeights.HealerSpacing).
                if (!row.Key)
                {
                    List<GameBot> healers = inRow.Where(bot => PvpMember.RoleOf(bot) == PvpRole.Heal).ToList();
                    if (healers.Count >= 2)
                        inRow = [healers[0], .. inRow.Where(bot => !healers.Contains(bot)), .. healers.Skip(1)];
                }
                for (int slot = 0; slot < inRow.Count; slot++)
                {
                    Vector2 point = new Vector2(enemyCentre.X, enemyCentre.Y)
                                    + across * ((slot - (inRow.Count - 1) / 2f) * SpreadSpacing)
                                    - toward * (row.Key ? 0 : BackRowDepth);
                    // The place needs a height the navmesh knows, or no path is found.
                    places[inRow[slot]] = nav.GetFloorBeneath(inRow[slot].CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters)
                        is { } floor && floor.Z < 19_999 ? floor : new Vector3(point.X, point.Y, enemyCentre.Z);
                }
            }
            // A shield fighter travels beside the groupmate it guards, on the enemy's side of it:
            // guard reaches 256 units, and its own place in the front row is 700 from the back row.
            foreach (GameBot bot in members)
            {
                if ((_squadA?.WardOf(bot) ?? _squadB?.WardOf(bot)) is GameBot ward && places.TryGetValue(ward, out Vector3 beside))
                    places[bot] = new Vector3(beside.X + toward.X * 100, beside.Y + toward.Y * 100, beside.Z);
            }
            return places;
        }

        private void SetSelfBuffsOnly(bool value)
        {
            foreach (GameBot bot in _teamA.Concat(_teamB))
            {
                if (bot.Brain is BotBrain brain)
                    brain.PvpSelfBuffsOnly = value;
            }
        }

        /// <summary>
        /// A buff counts once per caster, spell and target: a song or chant recast over and over
        /// keeps nobody waiting, a buff cast on each member in turn does.
        /// </summary>
        internal void NoteCast(GameLiving caster, Spell spell, GameLiving target)
        {
            if (_phase != Phase.Buffing || !_members.Contains(caster))
                return;
            if (_buffsCast.TryAdd((caster, spell.ID, target), 0))
                _lastNewBuffTick[caster] = GameLoop.GameLoopTime;
        }

        // Drivers standing to let their group catch up on the road.
        private readonly HashSet<GameBot> _driversWaiting = new();

        private bool StillBuffing(GameBot bot, long now)
        {
            if (bot.IsCasting && bot.castingComponent?.SpellHandler is { Spell: { } spell } handler &&
                !_buffsCast.ContainsKey((bot, spell.ID, handler.Target)))
                return true;
            // A resistance buff for the whole group still to come from this member.
            if (bot.Brain is BotBrain { } brain && brain.PvpHasGroupResistToCast())
                return true;
            return _lastNewBuffTick.TryGetValue(bot, out long last) && now - last < QuietSeconds * 1000;
        }

        /// <summary>
        /// Both groups are ready (or the wait ran out). Everyone starts the match rested: full
        /// health, power and endurance, as a group that sat down after buffing would. The match
        /// clock starts here, so buffing time is not part of the match.
        /// </summary>
        /// <summary>
        /// Both groups are buffed: each takes its rows facing where the other starts, as a group
        /// does that has seen to its buffs and stands up. In a play session the companions take
        /// theirs around the player when the enemy starts within sight (a lane); in the basin they
        /// go on travelling with the player.
        /// </summary>
        private void TakePlaces()
        {
            if (_leader == null)
                FormUp(_teamA, _spawnB);
            else if (_leader.IsWithinRadius(new Point2D(_spawnB.X, _spawnB.Y), PvpFormation.FormRange + 1_500) &&
                     LeaderPlaces(_teamA.Where(bot => bot.IsAlive).ToList(), new Vector3(_spawnB.X, _spawnB.Y, _leader.Z)) is { } start)
            {
                foreach ((GameBot bot, Vector3 place) in start)
                {
                    bot.StopFollowing();
                    bot.MoveTo(bot.CurrentRegionID, (int)Math.Round(place.X), (int)Math.Round(place.Y), (int)Math.Round(place.Z),
                        bot.GetHeading(new Point2D(_spawnB.X, _spawnB.Y)));
                    if (bot.Brain is BotBrain brain)
                    {
                        brain.PvpSpreadOut = true;
                        brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                    }
                }
            }
            FormUp(_teamB, _spawnA);
        }

        private void BeginRoaming(long now, List<GameBot> notReady)
        {
            TakePlaces();
            foreach (GameBot bot in _teamA.Concat(_teamB).Where(bot => bot.IsAlive))
            {
                bot.Health = bot.MaxHealth;
                bot.Mana = bot.MaxMana;
                bot.Endurance = bot.MaxEndurance;
            }
            if (_leader is { IsAlive: true })
            {
                _leader.Health = _leader.MaxHealth;
                _leader.Mana = _leader.MaxMana;
                _leader.Endurance = _leader.MaxEndurance;
                _leader.ResetDisabledSkills();
                _leader.Out.SendMessage("[8v8] Both groups are buffed and rested, and your ability timers are reset. The enemy is roaming from the far end of the basin.",
                    PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
            }
            int seconds = (int)((now - _phaseStartTick) / 1000);
            _lastReady = new { Seconds = seconds, TimedOut = notReady.Count > 0, NotReady = notReady.Select(bot => bot.PvpBuild.Id).ToList() };
            _recording?.Ready(seconds, notReady);
            if (notReady.Count > 0)
                Log.Warn($"8v8 scrim match {_matchNumber}: ready check timed out after {seconds}s, still buffing: {string.Join(", ", notReady.Select(bot => bot.PvpBuild.Id))}");
            _matchStartTick = now;
            _phase = Phase.Roaming;
        }

        private void EndMatch(string winner, string end, int aliveA, int aliveB)
        {
            long now = GameLoop.GameLoopTime;
            var result = new PvpMatchResult(_numberOffset + _matchNumber, winner, end, (int)((now - _matchStartTick) / 1000),
                _contactTick == 0 ? -1 : (int)((_contactTick - _matchStartTick) / 1000), aliveA, aliveB, DateTime.UtcNow);
            lock (Results) Results.Add(result);
            _recording?.End(result);
            Log.Info($"PVP_SCRIM_MATCH batch={_batch} lane={_lane} number={result.Number} winner={winner} end=\"{end}\" seconds={result.Seconds} contact={result.SecondsToContact} alive={aliveA}/{aliveB}");
            if (_teamA.Count > 0)
                PvpObserver.Say(_teamA[0], $"Lane {_lane}: match {result.Number} is over, {winner} by {end} in {result.Seconds} s, alive {aliveA}/{aliveB} " +
                    $"(recording {_batch}-match-{result.Number:000})");
            if (_leader?.Client?.IsPlaying == true)
                _leader.Out.SendMessage($"[8v8] The fight is over: {winner} by {end} in {result.Seconds} s, alive {aliveA}/{aliveB}. " +
                    $"Recording {_batch}-match-{result.Number:000}. /8v8 play again for another.",
                    PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
            PvpArena.Remove(_bots);
            _bots.Clear();
            if (_matchNumber >= _matchesWanted)
            {
                _phase = Phase.Idle;
                return;
            }
            BeginMatch();
        }

        internal string Describe() =>
            $"lane {_lane}: {_groupA.Id} against {_groupB.Id}, match {_numberOffset + _matchNumber} of {_numberOffset + _matchesWanted}, {_phase.ToString().ToLowerInvariant()}" +
            // For a watcher who wants a moment looked up in the recording: the same clock the reports use.
            (_phase == Phase.Fighting && _contactTick > 0 ? $", {(GameLoop.GameLoopTime - _contactTick) / 1000} s after the first blow" : "");

        public object Status()
        {
            long now = GameLoop.GameLoopTime;
            List<PvpMatchResult> results;
            lock (Results) results = Results.ToList();
            object Team(PvpGroupTemplate group, List<GameBot> team) => group == null ? null : new
            {
                Group = group.Id,
                Alive = team.Count(bot => bot.IsAlive),
                InCombat = team.Count(bot => bot.IsAlive && bot.InCombat),
                Moving = team.Count(bot => bot.IsAlive && bot.IsMoving),
                Centre = team.Any(bot => bot.IsAlive)
                    ? $"{(int)team.Where(bot => bot.IsAlive).Average(bot => bot.X)},{(int)team.Where(bot => bot.IsAlive).Average(bot => bot.Y)}"
                    : null,
                Health = team.Select(bot => $"{bot.PvpBuild.Id} {(bot.IsAlive ? bot.HealthPercent + "%" : "dead")}").ToList(),
            };
            return new
            {
                Lane = _lane,
                Batch = _batch,
                A = _groupA.Id,
                B = _groupB.Id,
                SetA = _versus == 0 ? null : _versus == 1 ? "new" : "old",
                SetB = _versus == 0 ? null : _versus == 1 ? "old" : "new",
                Phase = _phase.ToString(),
                Match = _matchNumber,
                Of = _matchesWanted,
                Seconds = Running ? (int)((now - _matchStartTick) / 1000) : 0,
                // The ready check of the current match (null while it is still buffing).
                Ready = _phase == Phase.Buffing ? null : _lastReady,
                StillBuffing = _phase == Phase.Buffing
                    ? _teamA.Concat(_teamB).Where(bot => bot.IsAlive && StillBuffing(bot, now)).Select(bot => bot.PvpBuild.Id).ToList()
                    : null,
                SecondsToContact = _contactTick == 0 ? -1 : (int)((_contactTick - _matchStartTick) / 1000),
                Distance = _teamA.Any(bot => bot.IsAlive) && _teamB.Any(bot => bot.IsAlive)
                    ? (int)Math.Sqrt(Math.Pow(_teamA.Where(bot => bot.IsAlive).Average(bot => bot.X) - _teamB.Where(bot => bot.IsAlive).Average(bot => bot.X), 2) +
                                     Math.Pow(_teamA.Where(bot => bot.IsAlive).Average(bot => bot.Y) - _teamB.Where(bot => bot.IsAlive).Average(bot => bot.Y), 2))
                    : -1,
                TeamA = Team(_groupA, _teamA),
                TeamB = Team(_groupB, _teamB),
                Results = results,
                Recording = _recording?.File,
                SquadBrain = new { A = _squadBrainA, B = _squadBrainB },
                KillTargets = new { A = _squadA?.KillTarget is { } killA ? PvpMember.BuildOf(killA) : null, B = _squadB?.KillTarget is { } killB ? PvpMember.BuildOf(killB) : null },
                LimitSeconds = MatchLimitSeconds,
                Error = _lastError,
            };
        }
    }
}
