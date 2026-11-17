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
        public const int EngageRange = 3_500;         // a member this close to its target fights; farther, it marches
        // Owner, 2026-11-17: a group that sees another spreads out at once, so one area mez does
        // not catch it whole. Two groups close 1,500 units in a second or two, too little for a
        // bot to move aside, so for now a group simply travels spread: two rows, fighters in
        // front, casters and healers behind. Enemies are spotted from farther than they are engaged.
        public const int SpotRange = 4_500;
        public const int SpreadSpacing = 450;         // between neighbours in a row; an area mez reaches about 350
        public const int BackRowDepth = 700;
        public static int MatchLimitSeconds { get; private set; } = 300;
        // The ready check before a match. The buffbot is instant, but casters also have buffs of
        // their own to put up. A member is ready once it has gone QuietSeconds without casting a
        // buff it had not cast yet; the match starts when both groups are ready, or at the timeout.
        public const int MinBuffSeconds = 5;
        public const int QuietSeconds = 8;
        public const int ReadyTimeoutSeconds = 120;
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
        // Shield fighters: whom each guards, what each was last recorded doing, and which of them
        // are staying beside their ward with no enemy to go for.
        private static readonly Dictionary<GameBot, GameBot> Wards = new();
        private static readonly Dictionary<GameBot, (string Kind, GameBot Target)> LastPeel = new();
        private static readonly HashSet<GameBot> Holding = new();
        // Interrupt duty: the enemy each interrupter has, kept so that the pairs hold from one second to the next.
        private static readonly Dictionary<GameBot, GameBot> Duties = new();
        // When each healer or caster last took a step away from a groupmate.
        private static readonly Dictionary<GameBot, long> LastSpreadStep = new();
        private static (int X, int Y) _spawnA, _spawnB;
        private static int _waypointA, _waypointB;
        // Ready check: the buffs cast in this buffing phase (caster, spell, target), and when each
        // member last finished one that was new. Filled from game threads by OnCastFinished.
        private static ConcurrentDictionary<(GameLiving Caster, int Spell, GameLiving Target), byte> _buffsCast = new();
        private static ConcurrentDictionary<GameLiving, long> _lastNewBuffTick = new();
        private static HashSet<GameLiving> _members = [];
        private static bool _hooked;
        private static object _lastReady;

        public static bool Running => _phase != Phase.Idle;

        public static void Start(PvpGroupTemplate groupA, PvpGroupTemplate groupB, int matches, int realmLevel, int limitSeconds = 300,
            bool squadA = false, bool squadB = false)
        {
            (_squadA, _squadB) = (squadA, squadB);
            Log.Info($"8v8 scrim weights: {PvpWeights.Reload(AppContext.BaseDirectory)}");
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
            if (!_hooked)
            {
                GameEventMgr.AddHandler(GameLivingEvent.CastFinished, OnCastFinished);
                _hooked = true;
            }
            _buffsCast = new();
            _lastNewBuffTick = new();
            _members = _teamA.Concat(_teamB).Cast<GameLiving>().ToHashSet();
            SetSelfBuffsOnly(true);
            _phase = Phase.Buffing;
            _phaseStartTick = _matchStartTick = GameLoop.GameLoopTime;
            _contactTick = 0;
            _killA = _killB = null;
            Wards.Clear();
            LastPeel.Clear();
            Holding.Clear();
            Duties.Clear();
            LastSpreadStep.Clear();
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

                _killA = _squadA ? CallTarget(_teamA, _teamB, _killA) : null;
                _killB = _squadB ? CallTarget(_teamB, _teamA, _killB) : null;
                Engage(_teamA, _teamB);
                Engage(_teamB, _teamA);
                if (_contactTick == 0 && _teamA.Concat(_teamB).Any(bot => bot.InCombat))
                {
                    _contactTick = now;
                    SetSelfBuffsOnly(false);
                    _phase = Phase.Fighting;
                }
                March(_teamA, _teamB, _spawnA, _spawnB, ref _waypointA);
                March(_teamB, _teamA, _spawnB, _spawnA, ref _waypointB);
                if (_squadA)
                    Spread(_teamA, _teamB);
                if (_squadB)
                    Spread(_teamB, _teamA);
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
            var guarded = new HashSet<GameBot>();
            Dictionary<GameBot, GameBot> duty = InterruptDuty(team, alive, target);
            bool critical = target != null && target.HealthPercent <= PvpRoles.CriticalHealthPercent;
            foreach (GameBot bot in team)
            {
                if (bot.Brain is not BotBrain brain)
                    continue;
                // By role: assist classes kill; the controller keeps its own crowd-control logic
                // and has no target forced on it; a shield fighter guards and peels. Interrupt duty
                // cuts across the roles: pets and fighters with duty go to their enemy and join
                // the kill only once the target is nearly dead, while a spell caster keeps its
                // target and gets the duty as a leaning in its scored choice.
                PvpRole role = PvpRoles.Of(bot.PvpBuild.Class);
                brain.PvpEnemies = enemies;
                brain.PvpKillTarget = target;
                duty.TryGetValue(bot, out GameBot onDuty);
                brain.PvpDutyTarget = onDuty;
                PvpInterruptMeans means = PvpInterruptDuty.MeansOf(bot.PvpBuild.Class, PvpWeights.Current.Interrupt);
                GameBot aim = null;
                if (onDuty != null && means is PvpInterruptMeans.Pets or PvpInterruptMeans.Melee && !critical && bot.IsAlive)
                    aim = onDuty;
                else if (target != null && PvpRoles.JoinsKill(role, target.HealthPercent))
                    aim = target;
                else if (role == PvpRole.Peel && bot.IsAlive)
                    aim = Peel(bot, brain, team, alive, target, guarded);
                brain.PvpAssignedTarget = aim;
                // A member still far behind (it stopped to cast while the group walked on) is not
                // put in attack state: the brain does not walk a caster that far, so it would stand
                // where it is for the whole fight. It keeps marching until it has caught up.
                if (aim != null && bot.IsAlive && bot.IsWithinRadius(aim, EngageRange))
                {
                    brain.AddToAggroList(aim, 1);
                    if (brain.FSM.GetCurrentState()?.StateType != eFSMStateType.AGGRO)
                        brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                }
            }
            return target;
        }

        /// <summary>
        /// Pairs the team's interrupters with the enemy healers and casters that are neither the
        /// kill target (the damage dealers interrupt that one) nor asleep (bots/pvp/PvpInterruptDuty.cs).
        /// </summary>
        private static Dictionary<GameBot, GameBot> InterruptDuty(List<GameBot> team, List<GameBot> enemies, GameBot kill)
        {
            PvpInterruptWeights weights = PvpWeights.Current.Interrupt;
            List<GameBot> interrupters = team.Where(bot => bot.IsAlive && bot.Brain is BotBrain &&
                PvpInterruptDuty.MeansOf(bot.PvpBuild.Class, weights) != PvpInterruptMeans.None).ToList();
            List<GameBot> open = enemies.Where(enemy => enemy != kill && !enemy.IsMezzed &&
                PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) != PvpBuffArchetype.Melee).ToList();
            var pairs = new List<PvpDutyPair>();
            for (int i = 0; i < interrupters.Count; i++)
            {
                Duties.TryGetValue(interrupters[i], out GameBot current);
                for (int e = 0; e < open.Count; e++)
                    pairs.Add(new PvpDutyPair(i, e, PvpBuffPlan.ArchetypeOf(open[e].PvpBuild.Class), open[e].IsCasting,
                        !open[e].IsBeingInterrupted, open[e] == current, (int)interrupters[i].GetDistanceTo(open[e])));
            }
            Dictionary<int, int> assigned = PvpInterruptDuty.Assign(pairs, weights);
            var duty = new Dictionary<GameBot, GameBot>();
            for (int i = 0; i < interrupters.Count; i++)
            {
                GameBot bot = interrupters[i];
                Duties.TryGetValue(bot, out GameBot before);
                if (!assigned.TryGetValue(i, out int e))
                {
                    Duties.Remove(bot);
                    continue;
                }
                duty[bot] = Duties[bot] = open[e];
                // Recorded when it changes, not every second.
                if (open[e] != before && _contactTick != 0)
                    PvpRecorder.Decision(bot, new
                    {
                        kind = "Duty", d = open[e].Name,
                        score = Math.Round(PvpInterruptDuty.Score(pairs.First(pair => pair.Interrupter == i && pair.Enemy == e), weights), 1),
                    }, [], null);
            }
            return duty;
        }

        /// <summary>
        /// A shield fighter's turn (bots/pvp/PvpPeel.cs): it guards the healer or caster that
        /// needs it most and goes for the enemy that scores best, which is first of all one in
        /// melee on that groupmate. With nobody worth going for it stays beside its ward, where
        /// guard works. Returns the enemy to attack, or null.
        /// </summary>
        private static GameBot Peel(GameBot bot, BotBrain brain, List<GameBot> team, List<GameBot> enemies, GameBot kill, HashSet<GameBot> guarded)
        {
            PvpPeelWeights weights = PvpWeights.Current.Peel;
            Holding.Remove(bot);
            List<GameBot> backline = team.Where(member => member.IsAlive && member != bot &&
                PvpBuffPlan.ArchetypeOf(member.PvpBuild.Class) != PvpBuffArchetype.Melee).ToList();
            Wards.TryGetValue(bot, out GameBot current);
            if (backline.Count == 0)
            {
                // Nobody left to guard: one more fighter on the kill target.
                if (current != null)
                {
                    foreach (GuardECSGameEffect guard in bot.effectListComponent.GetAbilityEffects(eEffect.Guard).OfType<GuardECSGameEffect>().Where(guard => guard.Source == bot))
                        guard.End();
                    Wards.Remove(bot);
                }
                return kill;
            }

            bool InMeleeOn(GameBot enemy, GameBot member) =>
                !enemy.IsMezzed && enemy.TargetObject == member && enemy.IsWithinRadius(member, weights.MeleeReach) &&
                (PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) == PvpBuffArchetype.Melee || enemy.attackComponent?.AttackState == true);

            List<PvpWardView> wards = backline.Select((member, id) => new PvpWardView(id, PvpBuffPlan.ArchetypeOf(member.PvpBuild.Class),
                enemies.Count(enemy => InMeleeOn(enemy, member)), member.HealthPercent, (int)bot.GetDistanceTo(member),
                member == current, guarded.Contains(member))).ToList();
            GameBot ward = backline[PvpPeel.ChooseWard(wards, weights)];
            guarded.Add(ward);
            if (ward != current)
            {
                Wards[bot] = ward;
                GuardAbilityHandler.CheckExistingEffectsOnTarget(bot, ward, false, out _, out GuardECSGameEffect another);
                bool set = bot.HasAbility(Abilities.Guard) && another == null;
                if (set)
                    GuardAbilityHandler.CancelOurEffectThenAddOnTarget(bot, ward);
                PvpRecorder.Decision(bot, new
                {
                    kind = "Guard", d = ward.Name, score = Math.Round(PvpPeel.Score(wards[backline.IndexOf(ward)], weights), 1),
                    set, attackers = wards[backline.IndexOf(ward)].Attackers,
                }, [], null);
            }

            List<GameBot> near = enemies.Where(enemy => enemy.IsWithinRadius(ward, weights.ConsiderRange) || InMeleeOn(enemy, bot)).ToList();
            List<PvpPeelView> views = near.Select((enemy, id) => new PvpPeelView(id, InMeleeOn(enemy, ward),
                backline.Any(member => member != ward && InMeleeOn(enemy, member)), InMeleeOn(enemy, bot), enemy == kill,
                !(enemy.IsStunned || enemy.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity)),
                enemy.IsMezzed, (int)ward.GetDistanceTo(enemy), enemy == brain.PvpAssignedTarget)).ToList();
            int chosen = PvpPeel.ChooseTarget(views, weights, out double score);
            GameBot aim = chosen >= 0 ? near[chosen] : null;
            string kind = chosen >= 0 ? PvpPeel.Describe(views[chosen]) : "Hold";

            // Recorded when it changes, not every second.
            if (_contactTick != 0 && (!LastPeel.TryGetValue(bot, out var last) || last.Kind != kind || last.Target != aim))
            {
                LastPeel[bot] = (kind, aim);
                // Enemies that are on nobody and are not the kill target have no score and are left out.
                PvpRecorder.Decision(bot, new { kind, d = aim?.Name, ward = ward.Name, score = score > double.MinValue ? Math.Round(score, 1) : 0 },
                    views.Where(view => PvpPeel.Score(view, weights) > double.MinValue)
                        .OrderByDescending(view => PvpPeel.Score(view, weights)).Take(4).Select(view => (object)new
                    {
                        kind = PvpPeel.Describe(view), d = near[view.Id].Name, score = Math.Round(PvpPeel.Score(view, weights), 1),
                        view.OnWard, view.OnBackline, view.OnSelf, view.IsKill, view.FromWard,
                    }), null);
            }

            // Staying beside the ward. Before contact the group's formation places everybody.
            if (aim == null && _contactTick != 0 && enemies.Any(enemy => bot.IsWithinRadius(enemy, EngageRange)))
            {
                Holding.Add(bot);
                if (brain.HasAggro)
                {
                    brain.ClearAggroList();
                    brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                }
                if (!bot.IsWithinRadius(ward, weights.StayWithin) && !bot.IsCasting && !bot.IsIncapacitated)
                    bot.PathTo(new Vector3(ward.X, ward.Y, ward.Z), bot.MaxSpeed);
            }
            return aim;
        }

        /// <summary>A bot that is not fighting takes up the nearest living enemy it can see.</summary>
        private static void Engage(List<GameBot> team, List<GameBot> enemies)
        {
            var nav = PathfindingProvider.Instance;
            foreach (GameBot bot in team)
            {
                // A shield fighter staying beside its ward has chosen not to attack.
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || brain.HasAggro || Holding.Contains(bot))
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
        /// end, back to its own end, and round again). Once any member spots enemies it goes for
        /// them spread out, each member to its own place in two rows. The order is repeated every
        /// few seconds because the brain's own idle logic stops a walking bot.
        /// </summary>
        private static void March(List<GameBot> team, List<GameBot> enemies, (int X, int Y) own, (int X, int Y) far, ref int waypoint)
        {
            List<GameBot> members = team.Where(bot => bot.IsAlive).ToList();
            if (members.Count == 0)
                return;
            List<GameBot> seen = enemies.Where(enemy => enemy.IsAlive && members.Any(bot => bot.IsWithinRadius(enemy, SpotRange))).ToList();
            Vector3 goal;
            Dictionary<GameBot, Vector3> places = null;
            if (seen.Count > 0)
            {
                goal = new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z));
            }
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
            places = SpreadPlaces(members, goal);
            foreach (GameBot bot in team)
            {
                if (!bot.IsAlive || bot.IsCasting || bot.IsIncapacitated)
                    continue;
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
                if (bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(places != null && places.TryGetValue(bot, out Vector3 place) ? place : goal, bot.MaxSpeed);
            }
        }

        /// <summary>
        /// Healers and casters keep apart once the enemy is in sight (bots/pvp/PvpSpacing.cs). A
        /// member standing within mez radius of another takes a step away, on a turn it is not
        /// using: one that is casting, walking or unable to act is left alone, and its brain can
        /// stop the step at any moment to cast. Fighters are left out: they stack on the kill
        /// target, and a shield fighter belongs beside its ward.
        /// </summary>
        private static void Spread(List<GameBot> team, List<GameBot> enemies)
        {
            PvpSpreadWeights weights = PvpWeights.Current.Spread;
            List<GameBot> foes = enemies.Where(enemy => enemy.IsAlive).ToList();
            if (weights.MinSpacing <= 0 || foes.Count == 0)
                return;
            List<GameBot> backline = team.Where(bot => bot.IsAlive &&
                PvpBuffPlan.ArchetypeOf(bot.PvpBuild.Class) != PvpBuffArchetype.Melee).ToList();
            var enemyCentre = new Vector2((float)foes.Average(enemy => enemy.X), (float)foes.Average(enemy => enemy.Y));
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            foreach (GameBot bot in backline)
            {
                if (bot.IsCasting || bot.IsMoving || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true ||
                    !foes.Any(enemy => bot.IsWithinRadius(enemy, SpotRange)) ||
                    LastSpreadStep.TryGetValue(bot, out long last) && now - last < 2_000)
                    continue;
                List<Vector2> mates = backline.Where(member => member != bot).Select(member => new Vector2(member.X, member.Y)).ToList();
                if (PvpSpacing.StepApart(new Vector2(bot.X, bot.Y), mates, enemyCentre, weights.MinSpacing, weights.Step) is not { } to)
                    continue;
                LastSpreadStep[bot] = now;
                bot.PathTo(nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters)
                    is { } floor && floor.Z < 19_999 ? floor : new Vector3(to.X, to.Y, bot.Z), bot.MaxSpeed);
                PvpRecorder.Decision(bot, new { kind = "Spread", score = 0 }, [], null);
            }
        }

        /// <summary>
        /// Where each member heads when the group has spotted the enemy: two rows across the line
        /// of approach, fighters aimed at the enemy and the rest a little short of it, neighbours
        /// <see cref="SpreadSpacing"/> apart.
        /// </summary>
        private static Dictionary<GameBot, Vector3> SpreadPlaces(List<GameBot> members, Vector3 enemyCentre)
        {
            var centre = new Vector2((float)members.Average(bot => bot.X), (float)members.Average(bot => bot.Y));
            Vector2 toward = new Vector2(enemyCentre.X, enemyCentre.Y) - centre;
            if (toward.LengthSquared() < 1)
                return null;
            toward = Vector2.Normalize(toward);
            var across = new Vector2(-toward.Y, toward.X);
            var nav = PathfindingProvider.Instance;
            var places = new Dictionary<GameBot, Vector3>();
            foreach (IGrouping<bool, GameBot> row in members.GroupBy(bot => PvpBuffPlan.ArchetypeOf(bot.PvpBuild.Class) == PvpBuffArchetype.Melee))
            {
                List<GameBot> inRow = row.ToList();
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
                if (Wards.TryGetValue(bot, out GameBot ward) && places.TryGetValue(ward, out Vector3 beside))
                    places[bot] = new Vector3(beside.X + toward.X * 100, beside.Y + toward.Y * 100, beside.Z);
            }
            return places;
        }

        private static void SetSelfBuffsOnly(bool value)
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
        private static void OnCastFinished(DOLEvent e, object sender, EventArgs args)
        {
            if (_phase != Phase.Buffing || sender is not GameLiving caster || !_members.Contains(caster) ||
                args is not CastingEventArgs { SpellHandler: { Spell: { } spell } } cast)
                return;
            if (_buffsCast.TryAdd((caster, spell.ID, cast.Target), 0))
                _lastNewBuffTick[caster] = GameLoop.GameLoopTime;
        }

        private static bool StillBuffing(GameBot bot, long now)
        {
            if (bot.IsCasting && bot.castingComponent?.SpellHandler is { Spell: { } spell } handler &&
                !_buffsCast.ContainsKey((bot, spell.ID, handler.Target)))
                return true;
            return _lastNewBuffTick.TryGetValue(bot, out long last) && now - last < QuietSeconds * 1000;
        }

        /// <summary>
        /// Both groups are ready (or the wait ran out). Everyone starts the match rested: full
        /// health, power and endurance, as a group that sat down after buffing would. The match
        /// clock starts here, so buffing time is not part of the match.
        /// </summary>
        private static void BeginRoaming(long now, List<GameBot> notReady)
        {
            foreach (GameBot bot in _teamA.Concat(_teamB).Where(bot => bot.IsAlive))
            {
                bot.Health = bot.MaxHealth;
                bot.Mana = bot.MaxMana;
                bot.Endurance = bot.MaxEndurance;
            }
            int seconds = (int)((now - _phaseStartTick) / 1000);
            _lastReady = new { Seconds = seconds, TimedOut = notReady.Count > 0, NotReady = notReady.Select(bot => bot.PvpBuild.Id).ToList() };
            PvpRecorder.Ready(seconds, notReady);
            if (notReady.Count > 0)
                Log.Warn($"8v8 scrim match {_matchNumber}: ready check timed out after {seconds}s, still buffing: {string.Join(", ", notReady.Select(bot => bot.PvpBuild.Id))}");
            _matchStartTick = now;
            _phase = Phase.Roaming;
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
                Recording = PvpRecorder.CurrentFile,
                SquadBrain = new { A = _squadA, B = _squadB },
                KillTargets = new { A = _killA?.PvpBuild.Id, B = _killB?.PvpBuild.Id },
                LimitSeconds = MatchLimitSeconds,
                Error = _lastError,
            };
        }
    }
}
