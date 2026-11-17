using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using DOL.AI.Brain;
using DOL.Logging;

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
    }

    public sealed record PvpMatchResult(int Number, string Winner, string End, int Seconds, int SecondsToContact,
        int AliveA, int AliveB, DateTime FinishedUtc);

    /// <summary>
    /// Runs scrims: two groups spawn at opposite ends of the arena, walk toward each other, fight,
    /// and the match ends when one side is dead or time runs out; then the next match starts
    /// (docs/PVP-8V8-ROADMAP.md, M2). This first version is the plain baseline the recorder will
    /// measure: each group simply marches on the enemy spawn, and a bot attacks the nearest enemy
    /// it can see. Everything runs on the game loop, driven by a one-second timer.
    /// </summary>
    public static class PvpScrim
    {
        public const int SightRange = 3_000;          // an enemy this close is noticed
        public static int MatchLimitSeconds { get; private set; } = 300;
        public const int BuffSeconds = 5;             // the buffbot is instant; a short settle time
        private enum Phase { Idle, Buffing, Roaming, Fighting }

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpScrim));
        private static Timer _timer;
        private static int _ticking;

        private static Phase _phase = Phase.Idle;
        private static PvpArenaBox _arena = PvpArenaBox.EmainSouthEast;
        private static PvpGroupTemplate _groupA, _groupB;
        private static int _realmLevel, _matchesWanted, _matchNumber;
        private static long _phaseStartTick, _matchStartTick, _contactTick;
        private static List<GameBot> _teamA = [], _teamB = [];
        private static readonly List<PvpMatchResult> Results = [];
        private static string _lastError;
        private static string _batch;
        private static bool _squadA, _squadB;
        private static GameBot _killA, _killB;
        private static (int X, int Y) _spawnA, _spawnB;
        private static int _waypointA, _waypointB;

        public static bool Running => _phase != Phase.Idle;

        public static void Start(PvpGroupTemplate groupA, PvpGroupTemplate groupB, int matches, int realmLevel, int limitSeconds = 300,
            bool squadA = false, bool squadB = false)
        {
            (_squadA, _squadB) = (squadA, squadB);
            if (Running)
                throw new InvalidOperationException("A scrim is already running. Stop it first.");
            if (groupA.Realm == groupB.Realm)
                throw new ArgumentException("The two groups must be of different realms to be able to fight.");
            PvpArena.Clear();
            (_groupA, _groupB, _matchesWanted, _realmLevel) = (groupA, groupB, Math.Max(1, matches), realmLevel);
            _matchNumber = 0;
            _lastError = null;
            MatchLimitSeconds = Math.Clamp(limitSeconds, 30, 3600);
            _batch = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{groupA.Id}-vs-{groupB.Id}";
            lock (Results) Results.Clear();
            BeginMatch();
            _timer?.Dispose();
            _timer = new Timer(_ => NpcService.Instance.Post(Tick, 0), null, 1_000, 1_000);
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _phase = Phase.Idle;
            PvpRecorder.End(null);
            PvpArena.Clear();
        }

        private static void BeginMatch()
        {
            _matchNumber++;
            // Sides swap every match so neither group always has the same end of the arena.
            bool swapped = _matchNumber % 2 == 0;
            (int X, int Y) spawnA = swapped ? _arena.SpawnB : _arena.SpawnA;
            (int X, int Y) spawnB = swapped ? _arena.SpawnA : _arena.SpawnB;
            (_spawnA, _spawnB, _waypointA, _waypointB) = (spawnA, spawnB, 0, 0);
            _teamA = PvpArena.SpawnGroup(_groupA, _arena.Region, spawnA.X, spawnA.Y, _realmLevel).ToList();
            _teamB = PvpArena.SpawnGroup(_groupB, _arena.Region, spawnB.X, spawnB.Y, _realmLevel).ToList();
            _phase = Phase.Buffing;
            _phaseStartTick = _matchStartTick = GameLoop.GameLoopTime;
            _contactTick = 0;
            _killA = _killB = null;
            PvpRecorder.Begin(_matchNumber, _groupA, _groupB, _teamA, _teamB, _realmLevel, _batch);
        }

        private static void Tick(int _)
        {
            if (_phase == Phase.Idle || Interlocked.Exchange(ref _ticking, 1) != 0)
                return;
            try
            {
                long now = GameLoop.GameLoopTime;
                PvpRecorder.Snapshot();
                int aliveA = _teamA.Count(bot => bot.IsAlive), aliveB = _teamB.Count(bot => bot.IsAlive);

                if (_phase == Phase.Buffing)
                {
                    if (now - _phaseStartTick < BuffSeconds * 1000)
                        return;
                    _phase = Phase.Roaming;
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

                _killA = _squadA ? CallTarget(_teamA, _teamB, _killA) : null;
                _killB = _squadB ? CallTarget(_teamB, _teamA, _killB) : null;
                Engage(_teamA, _teamB);
                Engage(_teamB, _teamA);
                if (_contactTick == 0 && _teamA.Concat(_teamB).Any(bot => bot.InCombat))
                {
                    _contactTick = now;
                    _phase = Phase.Fighting;
                }
                March(_teamA, _teamB, _spawnA, _spawnB, ref _waypointA);
                March(_teamB, _teamA, _spawnB, _spawnA, ref _waypointB);
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                Log.Error("8v8 scrim tick failed", exception);
                Stop();
            }
            finally
            {
                Volatile.Write(ref _ticking, 0);
            }
        }

        /// <summary>
        /// The squad brain's first piece: one kill target for the team, given to everyone who is
        /// not a healer. Distance is measured from the middle of the team's damage dealers.
        /// </summary>
        private static GameBot CallTarget(List<GameBot> team, List<GameBot> enemies, GameBot current)
        {
            List<GameBot> dealers = team.Where(bot => bot.IsAlive && PvpRoles.Of(bot.PvpBuild.Class) == PvpRole.Assist).ToList();
            List<GameBot> alive = enemies.Where(enemy => enemy.IsAlive).ToList();
            GameBot target = null;
            if (dealers.Count > 0 && alive.Count > 0)
            {
                double x = dealers.Average(bot => bot.X), y = dealers.Average(bot => bot.Y);
                var views = alive.Select((enemy, id) => new PvpTargetView(id, PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class),
                    enemy.HealthPercent, enemy.IsMezzed, (int)Math.Sqrt(Math.Pow(enemy.X - x, 2) + Math.Pow(enemy.Y - y, 2)))).ToList();
                int chosen = PvpKillTarget.Choose(views, current != null ? alive.IndexOf(current) : -1);
                target = chosen >= 0 ? alive[chosen] : null;
            }
            foreach (GameBot bot in team)
            {
                if (bot.Brain is not BotBrain brain)
                    continue;
                // By role: assist classes kill; the controller keeps its own crowd-control logic
                // and has no target forced on it; the interrupter is pointed at an enemy healer or
                // caster; both of those join the kill only once the target is nearly dead.
                PvpRole role = PvpRoles.Of(bot.PvpBuild.Class);
                brain.PvpEnemies = enemies;
                brain.PvpKillTarget = target;
                GameBot aim = null;
                if (target != null && PvpRoles.JoinsKill(role, target.HealthPercent))
                    aim = target;
                else if (role == PvpRole.Interrupt && bot.IsAlive)
                {
                    var views = alive.Select((enemy, id) => new PvpInterruptView(id, PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class),
                        enemy.IsCasting, enemy.IsMezzed, (int)bot.GetDistanceTo(enemy))).ToList();
                    int chosen = PvpRoles.ChooseInterruptTarget(views, target != null ? alive.IndexOf(target) : -1);
                    aim = chosen >= 0 ? alive[chosen] : null;
                }
                brain.PvpAssignedTarget = aim;
                if (aim != null && bot.IsAlive)
                {
                    brain.AddToAggroList(aim, 1);
                    if (brain.FSM.GetCurrentState()?.StateType != eFSMStateType.AGGRO)
                        brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                }
            }
            return target;
        }

        /// <summary>A bot that is not fighting takes up the nearest living enemy it can see.</summary>
        private static void Engage(List<GameBot> team, List<GameBot> enemies)
        {
            var nav = PathfindingProvider.Instance;
            foreach (GameBot bot in team)
            {
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || brain.HasAggro)
                    continue;
                GameBot target = enemies
                    .Where(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, SightRange))
                    .OrderBy(enemy => bot.GetDistanceTo(enemy))
                    .FirstOrDefault(enemy => nav.HasLineOfSight(bot.CurrentZone, new Vector3(bot.X, bot.Y, bot.Z),
                        new Vector3(enemy.X, enemy.Y, enemy.Z), nav.DefaultFilters));
                if (target == null)
                    continue;
                brain.AddToAggroList(target, 1);
                brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
            }
        }

        /// <summary>
        /// Bots with nothing to fight move as a group. While no member can see an enemy the group
        /// does not know where the other side is: it patrols (the middle of the arena, the far
        /// end, back to its own end, and round again). Once any member sees enemies it goes for
        /// the ones in sight. The order is repeated every few seconds because the brain's own
        /// idle logic stops a walking bot.
        /// </summary>
        private static void March(List<GameBot> team, List<GameBot> enemies, (int X, int Y) own, (int X, int Y) far, ref int waypoint)
        {
            List<GameBot> members = team.Where(bot => bot.IsAlive).ToList();
            if (members.Count == 0)
                return;
            List<GameBot> seen = enemies.Where(enemy => enemy.IsAlive && members.Any(bot => bot.IsWithinRadius(enemy, SightRange))).ToList();
            Vector3 goal;
            if (seen.Count > 0)
                goal = new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z));
            else
            {
                (int X, int Y)[] route = [((own.X + far.X) / 2, (own.Y + far.Y) / 2), far, ((own.X + far.X) / 2, (own.Y + far.Y) / 2), own];
                (int X, int Y) next = route[waypoint % route.Length];
                double x = members.Average(bot => bot.X), y = members.Average(bot => bot.Y);
                if (Math.Sqrt(Math.Pow(next.X - x, 2) + Math.Pow(next.Y - y, 2)) < 900)
                    next = route[++waypoint % route.Length];
                // The route has no heights: take the ground under the waypoint from the navmesh,
                // as the spawn does. A goal above or below the mesh gives no path and nobody moves.
                var nav = PathfindingProvider.Instance;
                Zone zone = members[0].CurrentZone;
                goal = nav.GetFloorBeneath(zone, new Vector3(next.X, next.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                    ? floor
                    : nav.GetClosestPoint(zone, new Vector3(next.X, next.Y, 5_000), 256, 256, 20_000, nav.DefaultFilters)
                      ?? new Vector3(next.X, next.Y, (float)members.Average(bot => bot.Z));
            }
            foreach (GameBot bot in team)
            {
                if (!bot.IsAlive || bot.InCombat || bot.IsCasting || bot.Brain is BotBrain { HasAggro: true })
                    continue;
                if (bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(goal, bot.MaxSpeed);
            }
        }

        private static void EndMatch(string winner, string end, int aliveA, int aliveB)
        {
            long now = GameLoop.GameLoopTime;
            var result = new PvpMatchResult(_matchNumber, winner, end, (int)((now - _matchStartTick) / 1000),
                _contactTick == 0 ? -1 : (int)((_contactTick - _matchStartTick) / 1000), aliveA, aliveB, DateTime.UtcNow);
            lock (Results) Results.Add(result);
            PvpRecorder.End(result);
            Log.Info($"PVP_SCRIM_MATCH number={result.Number} winner={winner} end=\"{end}\" seconds={result.Seconds} contact={result.SecondsToContact} alive={aliveA}/{aliveB}");
            PvpArena.Clear();
            if (_matchNumber >= _matchesWanted)
            {
                _timer?.Dispose();
                _timer = null;
                _phase = Phase.Idle;
                return;
            }
            BeginMatch();
        }

        public static object Status()
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
                Phase = _phase.ToString(),
                Match = _matchNumber,
                Of = _matchesWanted,
                Seconds = Running ? (int)((now - _matchStartTick) / 1000) : 0,
                SecondsToContact = _contactTick == 0 ? -1 : (int)((_contactTick - _matchStartTick) / 1000),
                Distance = _teamA.Any(bot => bot.IsAlive) && _teamB.Any(bot => bot.IsAlive)
                    ? (int)Math.Sqrt(Math.Pow(_teamA.Where(bot => bot.IsAlive).Average(bot => bot.X) - _teamB.Where(bot => bot.IsAlive).Average(bot => bot.X), 2) +
                                     Math.Pow(_teamA.Where(bot => bot.IsAlive).Average(bot => bot.Y) - _teamB.Where(bot => bot.IsAlive).Average(bot => bot.Y), 2))
                    : -1,
                TeamA = Team(_groupA, _teamA),
                TeamB = Team(_groupB, _teamB),
                Results = results,
                Recording = PvpRecorder.CurrentFile,
                SquadBrain = new { A = _squadA, B = _squadB },
                KillTargets = new { A = _killA?.PvpBuild.Id, B = _killB?.PvpBuild.Id },
                LimitSeconds = MatchLimitSeconds,
                Error = _lastError,
            };
        }
    }
}
