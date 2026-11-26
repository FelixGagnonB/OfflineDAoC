using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.AI.Brain;
using DOL.GS.SkillHandler;
using static DOL.GS.PvpScrim;

namespace DOL.GS
{
    /// <summary>
    /// The shared mind of one 8v8 group (docs/PVP-8V8-ROADMAP.md, M5.1): what the group as a whole
    /// has decided, which its members then act on. It calls the kill target, hands out interrupt
    /// duty, has the shield fighters guard and peel, and keeps the healers and casters apart.
    /// It is ticked once a second on the game loop, before its members think. A scrim makes one
    /// for each side; it holds nothing of the scrim, so the owner's own group can have one too.
    /// </summary>
    public sealed class PvpSquad
    {
        private readonly List<GameBot> _members;
        // Shield fighters: whom each guards, what each was last recorded doing, and which of them
        // are staying beside their ward with no enemy to go for.
        private readonly Dictionary<GameBot, GameLiving> Wards = new();
        private readonly Dictionary<GameBot, (string Kind, GameLiving Target)> LastPeel = new();
        private readonly HashSet<GameBot> Holding = new();
        // Interrupt duty: the enemy each interrupter has, kept so that the pairs hold from one second to the next.
        private readonly Dictionary<GameBot, GameLiving> Duties = new();
        // When each healer or caster last took a step away from a groupmate.
        private readonly Dictionary<GameBot, long> LastSpreadStep = new();

        /// <param name="others">Players in the group: not steered by the squad, but guarded, healed and defended like its bots.</param>
        public PvpSquad(IEnumerable<GameBot> members, IEnumerable<GameLiving> others = null)
        {
            _members = members.ToList();
            _allies = _members.Cast<GameLiving>().Concat(others ?? []).ToList();
            // Its bots know it from now on. They were told only when it first decided something, which needs an
            // enemy: in a roam a group gets a new squad when a fight is over, and until the next enemy its bots
            // went on reading the old one, whose fight had begun and never ends. So between fights the Bard held
            // its speed song back as "in a fight" (the owner's roam of 2026-11-22: endurance from the second
            // fight on, on the road and on a retreat, for four and a half minutes), nobody cast a buff with a
            // cast time on anybody else, and a call the player made went to a squad no bot was listening to.
            foreach (GameBot bot in _members)
            {
                if (bot?.Brain is BotBrain brain)
                    brain.PvpSquad = this;
            }
        }

        /// <summary>Everybody in the group: the squad's bots and the players with them (docs/PVP-8V8-ROADMAP.md, M9).</summary>
        private readonly List<GameLiving> _allies;
        public IReadOnlyList<GameLiving> Allies => _allies;

        public IReadOnlyList<GameBot> Members => _members;

        /// <summary>The enemy the group is killing now; null when it has none.</summary>
        public GameLiving KillTarget { get; private set; }

        /// <summary>Set once the first blow has fallen. Before it the group travels in formation and nothing is recorded as a fight decision.</summary>
        public bool FightStarted { get; set; }

        /// <summary>The healer or caster this shield fighter guards; null for anyone else.</summary>
        public GameLiving WardOf(GameBot bot) => Wards.GetValueOrDefault(bot);

        /// <summary>True for a shield fighter that is staying beside its ward: it has chosen not to attack.</summary>
        public bool IsHolding(GameBot bot) => Holding.Contains(bot) || FallingBack.Contains(bot);

        // Fighters on their way back to the group's healers and casters under the Extend order.
        private readonly HashSet<GameBot> FallingBack = new();
        // When an enemy fighter was last seen to be on a healer or caster: a reason to sprint.
        private readonly Dictionary<GameBot, long> _huntedTick = new();

        /// <summary>How far a bot has to go: to the enemy it is on, for a fighter, or to where it is walking.</summary>
        private static int WayLeft(GameBot bot)
        {
            int way = 0;
            if (bot.movementComponent is NpcMovementComponent { IsDestinationValid: true } legs)
                way = (int)Vector2.Distance(new Vector2(bot.X, bot.Y), new Vector2(legs.Destination.X, legs.Destination.Y));
            if (IsFighter(bot) && bot.TargetObject is GameLiving { IsAlive: true } aim && aim.Realm != bot.Realm)
                way = Math.Max(way, bot.GetDistanceTo(aim));
            return way;
        }

        /// <summary>True for an enemy fighter that is in melee on one of the group's healers or casters.</summary>
        public bool IsPursuer(GameLiving enemy) => _pursuers.Contains(enemy) || _incoming.Contains(enemy);

        /// <summary>True for an enemy that is on the member of this group that is being trained.</summary>
        public bool IsOnTrained(GameLiving enemy) => _trainers.Contains(enemy);

        /// <summary>
        /// Set for a team of bots in a roam: the group falls back as one by its own choice when it
        /// has more on it than it can stand against (bots/pvp/PvpKite.cs). Without it, only a
        /// player's call moves the group back.
        /// </summary>
        public bool KitesOnItsOwn
        {
            get => _kitesOnItsOwn || LeadsOnThePlayersWord && _allies.OfType<GamePlayer>().Any(PvpCalls.Leads);
            set => _kitesOnItsOwn = value;
        }
        private bool _kitesOnItsOwn;
        /// <summary>Set for the player's group in a roam: it falls back by itself too once the player has said so (/8v8 lead on).</summary>
        public bool LeadsOnThePlayersWord { get; set; }
        /// <summary>Where the group came into the zone: of two ways back that are as good, it takes the one nearer this.</summary>
        public (int X, int Y)? Home { get; set; }
        /// <summary>The living members of other groups of its realm that are near it, set by the roam each second.</summary>
        public IReadOnlyList<GameLiving> Beside { get; set; } = [];
        /// <summary>Those of them within this of the middle of the group are on its side of a fight.</summary>
        public const int BesideWithin = 3_000;
        /// <summary>Its side as it counted it this second: its own living and those of its realm beside it.</summary>
        public int OwnSide { get; private set; }
        /// <summary>The free enemies that aim at one of its side.</summary>
        public int OnIt { get; private set; }
        /// <summary>True while the group falls back as one: until <see cref="PvpKiteWeights.StandsSeconds"/> after its last leg.</summary>
        public bool Kiting => _kiteUntil != 0 && GameLoop.GameLoopTime < _kiteUntil;
        /// <summary>The legs it has gone back in this kite; 0 when it is not falling back.</summary>
        public int KiteLegs { get; private set; }
        // The group's place and the way of its last leg; when the kite runs out, when the last leg was called, since when the
        // group has been outnumbered and pressed, how long its caller takes to call, and until when it has no ground to go to.
        private Vector2 _place, _kiteWay, _body;
        private long _kiteUntil, _lastLeg, _pressedSince, _corneredUntil;
        private int _kiteCalledAfter;
        // Where each healer and caster stands from the place, where each fighter stands on this leg, and the leg each last set off on.
        private readonly Dictionary<GameBot, Vector2> _kiteOffsets = new();
        private Dictionary<GameBot, Vector3> _screen = new();
        private readonly Dictionary<GameBot, int> _kiteWalk = new();
        /// <summary>Falling back: the enemy that has followed out of its healers' reach, which the group turns on (PvpKite.Prey); null when there is none.</summary>
        public GameLiving Prey { get; private set; }
        /// <summary>Falling back: the leg is walked and the group stands and fights whoever followed. False on the walk, when everybody pulls back, its fighters too.</summary>
        public bool KiteStands { get; private set; }

        /// <summary>True for a group that counts what is on it and has too much (PvpKite.Outnumbered): it raises nobody.</summary>
        public bool TooManyOnIt { get; private set; }
        /// <summary>True once the group has lost and broken off (PvpKite.BreaksOff): everybody runs for <see cref="LeavesTo"/>, nobody fights.</summary>
        public bool Leaving => _leaveSince != 0;
        /// <summary>Where a group that has broken off is running to now; null otherwise.</summary>
        public Vector3? LeavesTo { get; private set; }
        /// <summary>Where it was when it broke off: a team of bots follows it only so far from there.</summary>
        public Vector2 LeftFrom { get; private set; }
        // When it broke off, since when it has lost, how long its caller takes to say so, when its point was last picked,
        // the way it runs, where the enemy was last seen, and that it has turned to fight and does not leave again.
        private long _leaveSince, _lostSince, _leavePickedAt;
        private int _leaveCalledAfter;
        private Vector2 _leaveWay, _leaveAway;
        private bool _turnedToFight;

        /// <summary>
        /// The group leaves a fight it has not lost: its kind keeps its fights clean, and a second
        /// enemy group has come into this one (PvpTeamProfile.LeavesDirtyFights). It runs as a
        /// group that has broken off does. False when it cannot: no fight, nobody alive, no
        /// ground to run to, or it has turned to fight already.
        /// </summary>
        public bool LeaveNow(IReadOnlyList<GameLiving> enemies)
        {
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            if (Leaving || _turnedToFight || !FightStarted || alive.Count == 0)
                return false;
            long now = GameLoop.GameLoopTime;
            EndKite();
            var all = new Vector2((float)alive.Average(bot => bot.X), (float)alive.Average(bot => bot.Y));
            List<GameLiving> foes = enemies.Where(enemy => enemy.IsAlive).ToList();
            _leaveAway = PvpKite.AwayFrom(all, foes.Select(enemy => new Vector2(enemy.X, enemy.Y)).ToList());
            (_leaveSince, LeftFrom, _leavePickedAt) = (now, all, 0);
            LeavesTo = null;
            PvpRecorder.Decision(alive[0], new { kind = "LeavesDirtyFight", score = alive.Count, enemies = foes.Count }, [], null);
            PvpObserver.Say(alive[0], $"{PvpObserver.Realm(alive[0])} leaves a fight that is no longer clean");
            Leave(now);
            return Leaving;
        }

        /// <summary>It could not get away: it turns and fights, and does not break off again.</summary>
        public void StopLeaving()
        {
            (_leaveSince, _lostSince, _turnedToFight) = (0, 0, true);
            LeavesTo = null;
            _leaveWay = default;
        }

        /// <summary>
        /// A second of a group on the run: who can sprints, and every few seconds a new point to
        /// run for, away from where the enemy was last seen and toward its start where the ground
        /// lets it. With no ground to run to it turns and fights.
        /// </summary>
        private void Leave(long now)
        {
            PvpWeights all = PvpWeights.Of(_members.FirstOrDefault());
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            if (alive.Count == 0)
                return;
            PvpPositionWeights legs = all.Position;
            foreach (GameBot bot in alive)
            {
                bot.PvpSprinting = legs.SprintInFights && legs.CanSprint(bot.EndurancePercent, bot.PvpSprinting);
                if (bot.PvpSprinting && bot.IsMoving && legs.SprintCostPerSecond > 0)
                    bot.Endurance = Math.Max(0, bot.Endurance - PvpPositionWeights.SprintCostWith(legs.SprintCostPerSecond,
                        bot.GetAbility<DOL.GS.RealmAbilities.AtlasOF_LongWindAbility>() is { } longWind ? longWind.GetAmountForLevel(longWind.Level) : 0));
            }
            if (LeavesTo != null && now - _leavePickedAt < all.Kite.LeaveLegEveryMilliseconds)
                return;
            var nav = PathfindingProvider.Instance;
            Zone zone = alive[0].CurrentZone;
            Vector3? Ground(Vector2 point) =>
                nav.GetFloorBeneath(zone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999 ? floor : null;
            var middle = new Vector2((float)alive.Average(bot => bot.X), (float)alive.Average(bot => bot.Y));
            Vector2 home = Home is { } start ? new Vector2(start.X, start.Y) - middle : default;
            Vector3? begin = Ground(middle);
            foreach (Vector2 candidate in PvpKite.Ways(PvpKite.RunsAway(_leaveAway, _leaveWay, home), default, home))
            {
                if (Ground(middle + candidate * all.Kite.LeaveLeg) is not { } floor ||
                    begin is { } here && !nav.HasLineOfSight(zone, here, floor, nav.DefaultFilters))
                    continue;
                (LeavesTo, _leaveWay, _leavePickedAt) = (floor, candidate, now);
                return;
            }
            // (It keeps the point it has while it has one; with none, it has nowhere to go.)
            if (LeavesTo == null)
            {
                PvpRecorder.Decision(alive[0], new { kind = "Cornered", score = alive.Count }, [], null);
                StopLeaving();
            }
            else
                _leavePickedAt = now;
        }

        private void EndKite()
        {
            if (_kiteUntil == 0)
                return;
            _kiteUntil = 0;
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
            {
                PvpRecorder.Decision(voice, new { kind = "KiteEnds", score = KiteLegs }, [], null);
                PvpObserver.Say(voice, $"{PvpObserver.Realm(voice)} stands again after {KiteLegs} legs back");
                Call(voice, "kite-over", PvpCalls.KiteOver, 0, 2);
            }
            KiteLegs = 0;
            (Prey, KiteStands) = (null, false);
            _kiteOffsets.Clear();
            _screen = new();
            _kiteWalk.Clear();
            _kiteWay = default;
        }

        /// <summary>A line in group chat to the players of the group, from one of its bots, for what the roam sees (PvpRoam.TellOfIncoming).</summary>
        public void Tell(string kind, string text, int times = 1) => Call(_members.FirstOrDefault(member => member.IsAlive), kind, text, 0, times);

        /// <summary>
        /// A group with a player in it that has lost says so (PvpCalls.Lost) and goes on doing
        /// what it did: it never breaks off by itself. True when it has lost.
        /// </summary>
        private bool TellIfLost(PvpKiteWeights weights, List<GameBot> alive, List<GameLiving> foes)
        {
            if (alive.Count == 0 || !_allies.Any(member => member is GamePlayer))
                return false;
            int healers = _members.Count(bot => PvpMember.RoleOf(bot) == PvpRole.Heal), healersAlive = alive.Count(bot => PvpMember.RoleOf(bot) == PvpRole.Heal);
            int up = _allies.Count(member => member.IsAlive);
            if (!PvpKite.BreaksOff(up, _allies.Count, healersAlive, healers, OwnSide, foes.Count, weights))
                return false;
            _lostSaidAt = GameLoop.GameLoopTime;
            Call(alive[0], "lost", PvpCalls.Lost(up, foes.Count), PvpCalls.TooManyEveryMilliseconds, PvpCalls.LoudTimes);
            return true;
        }

        // When the group last found its fight lost (TellIfLost); 0 before the first time.
        private long _lostSaidAt;

        // Who of the group last used each ability that is for the whole group, and when (PvpRealmAbilities.KeptApartSeconds).
        private readonly Dictionary<string, (GameBot By, long When)> _groupAbilities = new();

        /// <summary>
        /// A member takes the turn for an ability that is for the whole group; false when
        /// another has it (two brains may decide in the same moment: one of them gets it).
        /// </summary>
        public bool ClaimGroupAbility(string key, GameBot by, int apartSeconds)
        {
            lock (_groupAbilities)
            {
                long now = GameLoop.GameLoopTime;
                if (_groupAbilities.TryGetValue(key, out (GameBot By, long When) last) &&
                    PvpRealmAbilities.TakenBySomebodyElse(last.By == by, now, last.When, apartSeconds))
                    return false;
                _groupAbilities[key] = (by, now);
                return true;
            }
        }

        /// <summary>The ability was not used after all (the game refused it): the turn is free again.</summary>
        public void ReleaseGroupAbility(string key, GameBot by)
        {
            lock (_groupAbilities)
            {
                if (_groupAbilities.TryGetValue(key, out (GameBot By, long When) last) && last.By == by)
                    _groupAbilities.Remove(key);
            }
        }

        // Since when the player's group has had too many on it without a word said.
        private long _tooManySince, _tooManyCalledAfter;

        /// <summary>
        /// The group of a player who has not given it the lead (/8v8 lead) does not fall back by
        /// itself, and until 2026-11-24 it did not count either: in the owner's first three
        /// fights of that evening it had sixteen, twenty-four and sixteen against its eight,
        /// stood where it was and was wiped in 64 to 130 seconds with nothing said. It counts
        /// as a group of bots does and, after its caller's moment, says so loud: the call to
        /// fall back, to run or to stay is the player's.
        /// </summary>
        private void WarnOfTooMany(PvpKiteWeights weights, List<GameBot> alive, List<GameLiving> foes, long now)
        {
            static Vector2 At(GameObject thing) => new(thing.X, thing.Y);
            List<GameBot> soft = alive.Where(bot => !IsFighter(bot)).ToList();
            List<GameBot> body = soft.Count > 0 ? soft : alive;
            var middle = new Vector2((float)body.Average(bot => bot.X), (float)body.Average(bot => bot.Y));
            List<GameLiving> beside = Beside.Where(friend => friend.IsAlive && Vector2.Distance(At(friend), middle) <= BesideWithin).ToList();
            OwnSide = _allies.Count(member => member.IsAlive) + beside.Count;
            OnIt = foes.Count(enemy => !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject is GameLiving { IsAlive: true } aimed &&
                                       (_allies.Contains(aimed) || beside.Contains(aimed)));
            // (Lost altogether is said in place of "too many".)
            if (TellIfLost(weights, alive, foes))
                return;
            if (!PvpKite.Outnumbered(OwnSide, OnIt, weights))
            {
                _tooManySince = 0;
                return;
            }
            if (_tooManySince == 0)
            {
                _tooManySince = now;
                PvpHumanWeights slow = PvpNotice.HumanOf(alive[0]);
                _tooManyCalledAfter = Util.Random(Math.Max(0, slow.KiteCalledAfterMinMilliseconds),
                    Math.Max(slow.KiteCalledAfterMinMilliseconds, slow.KiteCalledAfterMaxMilliseconds));
            }
            else if (now - _tooManySince >= _tooManyCalledAfter)
                Call(alive[0], "toomany", PvpCalls.TooMany(OnIt, OwnSide), PvpCalls.TooManyEveryMilliseconds, PvpCalls.LoudTimes);
        }

        /// <summary>
        /// The group's kite for this second (bots/pvp/PvpKite.cs): it counts its side and what is
        /// on it, moves its place back a leg when that is too much and the enemy is on its
        /// healers and casters, and sends each of those to its own spot around the place. True
        /// while it falls back: nobody then takes a step of its own.
        /// </summary>
        private bool Kite()
        {
            PvpKiteWeights weights = PvpWeights.Of(_members.FirstOrDefault()).Kite;
            long now = GameLoop.GameLoopTime;
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            List<GameLiving> foes = Known.Where(enemy => enemy.IsAlive).ToList();
            // (A player's call for anything but the kite itself stands over the group's own: it does not fall back under a called push.)
            bool calledOtherwise = _calledOrder is { } called && called != PvpOrder.Extend && now < _calledUntil;
            if (!KitesOnItsOwn || !weights.Enabled || !FightStarted || MopsUp || alive.Count == 0 || foes.Count == 0 || calledOtherwise || MakesLastStand())
            {
                EndKite();
                (OwnSide, OnIt, _pressedSince, _lostSince, TooManyOnIt) = (alive.Count, 0, 0, 0, false);
                // A player's group that does not fall back by itself counts all the same, and says so when it is too much.
                // (Not under a call of the player's: that is the player's answer.)
                if (!KitesOnItsOwn && weights.Enabled && FightStarted && !MopsUp && alive.Count > 0 && foes.Count > 0 && !(_calledOrder != null && now < _calledUntil) &&
                    _allies.Any(member => member is GamePlayer))
                    WarnOfTooMany(weights, alive, foes, now);
                else
                    _tooManySince = 0;
                return false;
            }
            static Vector2 At(GameObject thing) => new(thing.X, thing.Y);
            var nav = PathfindingProvider.Instance;
            Zone zone = alive[0].CurrentZone;
            Vector3? Ground(Vector2 point) =>
                nav.GetFloorBeneath(zone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999 ? floor : null;
            List<GameBot> soft = alive.Where(bot => !IsFighter(bot)).ToList();
            List<GameBot> body = soft.Count > 0 ? soft : alive;
            var middle = new Vector2((float)body.Average(bot => bot.X), (float)body.Average(bot => bot.Y));
            bool kiting = Kiting;
            _body = middle;
            if (!kiting)
            {
                EndKite();
                _place = middle;
            }
            // Its side, and what is on it: the free enemies that aim at one of its side.
            List<GameLiving> beside = Beside.Where(friend => friend.IsAlive && Vector2.Distance(At(friend), middle) <= BesideWithin).ToList();
            OwnSide = _allies.Count(member => member.IsAlive) + beside.Count;
            OnIt = foes.Count(enemy => !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject is GameLiving { IsAlive: true } aimed &&
                                       (_allies.Contains(aimed) || beside.Contains(aimed)));
            int pressed = foes.Count(enemy => !enemy.IsMezzed && !enemy.IsStunned && body.Any(bot => bot.IsWithinRadius(enemy, weights.PressedWithin)));
            bool outnumbered = PvpKite.Outnumbered(OwnSide, OnIt, weights);
            TooManyOnIt = outnumbered;
            TellIfLost(weights, alive, foes);
            // It has lost: after its caller's moment it breaks off, and everybody runs.
            int healers = _members.Count(bot => PvpMember.RoleOf(bot) == PvpRole.Heal), healersAlive = alive.Count(bot => PvpMember.RoleOf(bot) == PvpRole.Heal);
            // (A group with a player in it does not break off by itself: the retreat is the player's call.)
            if (_turnedToFight || _allies.Any(member => member is GamePlayer) ||
                !PvpKite.BreaksOff(alive.Count, _members.Count, healersAlive, healers, OwnSide, foes.Count, weights, _draw))
                _lostSince = 0;
            else if (_lostSince == 0)
            {
                _lostSince = now;
                PvpHumanWeights slow = PvpNotice.HumanOf(alive[0]);
                _leaveCalledAfter = Util.Random(Math.Max(0, slow.KiteCalledAfterMinMilliseconds),
                    Math.Max(slow.KiteCalledAfterMinMilliseconds, slow.KiteCalledAfterMaxMilliseconds));
            }
            else if (now - _lostSince >= _leaveCalledAfter)
            {
                EndKite();
                var all = new Vector2((float)alive.Average(bot => bot.X), (float)alive.Average(bot => bot.Y));
                // The side with the fewest enemies: where it sets off to, and then keeps to.
                _leaveAway = PvpKite.AwayFrom(all, foes.Select(enemy => At(enemy)).ToList());
                (_leaveSince, LeftFrom, _leavePickedAt) = (now, all, 0);
                LeavesTo = null;
                PvpRecorder.Decision(alive[0], new { kind = "BreakOff", score = alive.Count, on = OnIt, side = OwnSide, enemies = foes.Count, healers = healersAlive }, [], null);
                PvpObserver.Say(alive[0], $"{PvpObserver.Realm(alive[0])} breaks off: {alive.Count} left against {foes.Count}");
                Leave(now);
                if (Leaving)
                    return true;
            }
            if (!outnumbered || pressed < weights.PressedBy)
                _pressedSince = 0;
            else if (_pressedSince == 0)
            {
                _pressedSince = now;
                PvpHumanWeights human = PvpNotice.HumanOf(alive[0]);
                _kiteCalledAfter = Util.Random(Math.Max(0, human.KiteCalledAfterMinMilliseconds),
                    Math.Max(human.KiteCalledAfterMinMilliseconds, human.KiteCalledAfterMaxMilliseconds));
            }
            // The leg walked, it stands; and it turns on whoever has followed it out of the reach of a healer of its own.
            KiteStands = kiting && PvpKite.Stands(Vector2.Distance(middle, _place), now - _lastLeg, weights);
            GameLiving prey = null;
            if (KiteStands)
            {
                List<GameLiving> theirHealers = foes.Where(enemy => PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Healer && !enemy.IsMezzed && !enemy.IsStunned).ToList();
                List<GameLiving> followers = foes.Where(enemy => Vector2.Distance(At(enemy), middle) <= weights.KillWithin).ToList();
                List<PvpFollower> views = followers.Select(enemy => new PvpFollower(
                    theirHealers.Where(healer => healer != enemy && healer.Realm == enemy.Realm).Select(healer => (int)healer.GetDistanceTo(enemy)).DefaultIfEmpty(int.MaxValue).Min(),
                    enemy.HealthPercent, (int)Vector2.Distance(At(enemy), middle), enemy.IsMezzed)).ToList();
                int turnOn = PvpKite.Prey(views, Prey != null ? followers.IndexOf(Prey) : -1, weights);
                if (turnOn >= 0)
                {
                    prey = followers[turnOn];
                    if (prey != Prey)
                        PvpRecorder.Decision(alive[0], new { kind = "TurnOn", d = prey.Name, score = views[turnOn].HealthPercent,
                            fromItsHealer = views[turnOn].FromItsHealer == int.MaxValue ? -1 : views[turnOn].FromItsHealer, followers = followers.Count }, [], null);
                    // It stays for the kill: the kite does not run out under it.
                    _kiteUntil = Math.Max(_kiteUntil, now + weights.StaysForPreyMilliseconds);
                }
            }
            Prey = prey;
            // Fallen back already, it goes again only when the pack itself is on it: those who came first it fights where it stands.
            int come = foes.Count(enemy => !enemy.IsMezzed && !enemy.IsStunned && Vector2.Distance(At(enemy), middle) <= weights.PackWithin);
            bool overrun = PvpKite.Outnumbered(OwnSide, come, weights);
            if (KiteStands && come > 0)
                _kiteUntil = Math.Max(_kiteUntil, now + weights.StaysForPreyMilliseconds);
            if (now >= _corneredUntil && PvpKite.TakesLeg(kiting ? overrun : outnumbered, pressed, _pressedSince == 0 ? 0 : now - _pressedSince, _kiteCalledAfter,
                    kiting, now - _lastLeg, weights, Prey != null))
            {
                (Prey, KiteStands) = (null, false);
                // Away from the enemies that are near, over ground the group can walk in a straight line.
                List<GameLiving> near = foes.Where(enemy => Vector2.Distance(At(enemy), middle) <= ViewRange / 2).ToList();
                if (near.Count == 0)
                    near = foes;
                var theirs = new Vector2((float)near.Average(enemy => enemy.X), (float)near.Average(enemy => enemy.Y));
                // (From its place; from where it really is when it has not got there: the place does not run away from the group.)
                Vector2 from = kiting && Vector2.Distance(middle, _place) <= weights.ArrivedWithin ? _place : middle;
                Vector2 home = Home is { } start ? new Vector2(start.X, start.Y) - from : default;
                Vector3? begin = Ground(from);
                Vector3? to = null;
                Vector2 way = default;
                foreach (Vector2 candidate in PvpKite.Ways(middle - theirs, kiting ? _kiteWay : default, home))
                {
                    if (Ground(from + candidate * weights.Leg) is not { } floor ||
                        begin is { } here && !nav.HasLineOfSight(zone, here, floor, nav.DefaultFilters))
                        continue;
                    (to, way) = (floor, candidate);
                    break;
                }
                if (to is not { } next)
                {
                    // Nowhere to go: it stands and fights where it is for a while.
                    _corneredUntil = now + weights.CorneredSeconds * 1_000L;
                    PvpRecorder.Decision(alive[0], new { kind = "Cornered", score = OnIt, side = OwnSide }, [], null);
                }
                else
                {
                    if (!kiting)
                    {
                        _kiteOffsets.Clear();
                        foreach (GameBot bot in soft)
                            _kiteOffsets[bot] = PvpKite.Offset(At(bot), middle, weights.PlaceRadius);
                        KiteLegs = 0;
                        PvpObserver.Say(alive[0], $"{PvpObserver.Realm(alive[0])} falls back as one: {OnIt} on its {OwnSide}");
                    }
                    // To a player of the group: loud, so that it is not lost among the other calls.
                    if (kiting)
                        Call(alive[0], "kite", PvpCalls.KiteAgain, 0, 2);
                    else
                        Call(alive[0], "kite", PvpCalls.KiteCalled(OnIt, OwnSide), 0, PvpCalls.LoudTimes);
                    (_kiteWay, _place, _lastLeg) = (way, new Vector2(next.X, next.Y), now);
                    _kiteUntil = now + weights.StandsSeconds * 1_000L;
                    KiteLegs++;
                    PvpRecorder.Decision(alive[0], new { kind = kiting ? "KiteLeg" : "KiteCalled", score = OnIt, side = OwnSide, pressed, legs = KiteLegs }, [], null);
                    // Its fighters in a row between the place and the enemy.
                    List<GameBot> row = alive.Where(bot => IsFighter(bot) && PvpMember.RoleOf(bot) == PvpRole.Assist).ToList();
                    var places = new Dictionary<GameBot, Vector3>();
                    for (int index = 0; index < row.Count; index++)
                        places[row[index]] = Ground(PvpKite.ScreenPlace(_place, way, index, row.Count, weights)) ?? next;
                    _screen = places;
                }
            }
            if (!Kiting)
                return false;
            foreach (GameBot bot in soft)
            {
                if (bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true)
                    continue;
                // (One that was raised since the kite began takes a spot from where it stands.)
                if (!_kiteOffsets.TryGetValue(bot, out Vector2 offset))
                    _kiteOffsets[bot] = offset = PvpKite.Offset(At(bot), middle, weights.PlaceRadius);
                Vector2 spot = _place + offset;
                float away = Vector2.Distance(At(bot), spot);
                // There, or on its way there on this leg.
                if (away <= weights.AtItsPlaceWithin || _kiteWalk.GetValueOrDefault(bot) == KiteLegs && bot.IsMoving && now < brain.PvpMoveUntil)
                    continue;
                // Not set off on this leg yet, and no enemy fighter on it: for a moment it works from where it stands, on whoever
                // is on its groupmates, and then follows.
                if (_kiteWalk.GetValueOrDefault(bot) != KiteLegs && now - _lastLeg < weights.CoversMilliseconds &&
                    !foes.Any(enemy => IsFighter(enemy) && !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject == bot &&
                                       enemy.IsWithinRadius(bot, weights.CoverNotWithin)))
                    continue;
                Vector3 ground = Ground(spot) ?? Ground(_place) ?? new Vector3(spot.X, spot.Y, bot.Z);
                LastStandStep[bot] = now;
                brain.PvpMoveFrom = now;
                // The walk is the bot's turn for as long as it takes: it casts from where the leg ends.
                brain.PvpMoveUntil = now + Math.Clamp((long)(away / Math.Max(100, (int)bot.MaxSpeed) * 1000) + 300, 800, 12_000);
                StandStepEnds[bot] = brain.PvpMoveUntil;
                _huntedTick[bot] = now;
                if (bot.attackComponent.AttackState)
                    bot.StopAttack();
                bot.StopFollowing();
                bot.PathTo(ground, bot.MaxSpeed);
                if (_kiteWalk.GetValueOrDefault(bot) != KiteLegs)
                    PvpRecorder.Decision(bot, new { kind = "KiteBack", order = Order.ToString(), score = (int)away, legs = KiteLegs }, [], null);
                _kiteWalk[bot] = KiteLegs;
            }
            return true;
        }

        // Heals being cast right now: healer, patient, spell, when it began and when it is surely over.
        private readonly Dictionary<GameBot, (GameLiving Target, Spell Spell, long Started, long Expires)> _heals = new();

        /// <summary>
        /// Whom a healer's heal goes to, given the heals the group's other healers are already
        /// casting (bots/pvp/PvpHealPlan.cs): its own pick, a needier member, or null when the
        /// others have everyone covered and the turn is free. Called from brain threads.
        /// </summary>
        public GameLiving AssignHeal(GameBot healer, Spell spell, GameLiving own)
        {
            // Only a single heal with a cast time is shared out: an instant lands at once, and a
            // group heal or a heal over time is not for one patient.
            if (own is not { } pick || !spell.IsHealing || spell.Target != eSpellTarget.REALM || spell.IsInstantCast || spell.Duration > 0)
                return own;
            PvpHealWeights weights = PvpWeights.Of(healer).Heal;
            int range = healer.castingComponent.CalculateSpellRange(spell);
            lock (_heals)
            {
                long now = GameLoop.GameLoopTime;
                foreach (var entry in _heals.ToArray())
                {
                    // Over, or given up: half a second after it was announced the healer is casting something else.
                    if (entry.Value.Expires <= now || !entry.Key.IsAlive ||
                        now - entry.Value.Started > 500 && entry.Key.castingComponent.SpellHandler?.Spell != entry.Value.Spell)
                        _heals.Remove(entry.Key);
                }
                _heals.Remove(healer);
                List<GameLiving> reach = _allies.Where(member => member.IsAlive && (member == healer || healer.IsWithinRadius(member, range))).ToList();
                // Its own pick is out of reach: it walks there, as before.
                if (!reach.Contains(pick))
                    return own;
                List<PvpPatient> patients = reach.Select((member, id) => new PvpPatient(id, member.Health, member.MaxHealth,
                    (int)_heals.Values.Where(heal => heal.Target == member).Sum(heal => Math.Max(0, GameBot.HealAmount(heal.Spell, member))),
                    PvpMember.ArchetypeOf(member), member == Trained)).ToList();
                int chosen = PvpHealPlan.Choose(patients, reach.IndexOf(pick), weights, healer.ManaPercent);
                if (chosen < 0)
                    return null;
                _heals[healer] = (reach[chosen], spell, now, now + Math.Clamp(spell.CastTime + 500, 1_000, 15_000));
                return reach[chosen];
            }
        }

        /// <summary>An enemy is seen from this far, with open ground between (the range at which a group spots another).</summary>
        public const int ViewRange = SpotRange;
        /// <summary>An enemy fighting a member of the group from within this distance is known without being seen.</summary>
        public const int FeltWithin = 2_000;
        /// <summary>An enemy that drops out of sight is still known for this long, where it was last seen.</summary>
        public const int MemoryMilliseconds = 10_000;

        // When each enemy was last seen by a member of the group.
        private readonly Dictionary<GameLiving, long> _lastSeen = new();
        // The enemy each fighter on the kill was last recorded attacking.
        private readonly Dictionary<GameBot, GameLiving> _lastAim = new();

        /// <summary>
        /// The enemies the group knows about (M5.2): those a living member sees now, within
        /// <see cref="ViewRange"/> and with open ground between, and those seen in the last ten
        /// seconds. Everything the group decides, and every choice its members score, is about
        /// these and no others. Stealth is not handled: the main groups have no stealther.
        /// </summary>
        public IReadOnlyList<GameLiving> Known { get; private set; } = [];

        /// <summary>
        /// Nobody is in sight any more. In a roam the player's group knows only the enemies the
        /// player has taken on, and when there is none its Bard goes back to its speed song.
        /// </summary>
        public void LoseSight()
        {
            if (Known.Count > 0)
                Known = [];
        }

        private List<GameLiving> Look(IReadOnlyList<GameLiving> enemies)
        {
            var nav = PathfindingProvider.Instance;
            long now = GameLoop.GameLoopTime;
            List<GameBot> watchers = _members.Where(member => member.IsAlive).ToList();
            var known = new List<GameLiving>();
            // An enemy is a bot or a player: a typed loop variable here would cast, and did (the first play session, 2026-11-19).
            foreach (GameLiving enemy in enemies)
            {
                if (!enemy.IsAlive || enemy.ObjectState != GameObject.eObjectState.Active)
                    continue;
                var there = new Vector3(enemy.X, enemy.Y, enemy.Z);
                // Nearest first: the first member that sees it is enough.
                PvpWeights sight = PvpWeights.Of(_members.FirstOrDefault());
                bool seen = watchers.Where(member => member.IsWithinRadius(enemy, ViewRange))
                    .OrderBy(member => member.GetDistanceTo(enemy))
                    .Any(member => PvpSight.Sees(member, enemy, ViewRange, sight) &&
                                   nav.HasLineOfSight(member.CurrentZone, new Vector3(member.X, member.Y, member.Z), there, nav.DefaultFilters));
                // An enemy that is fighting one of the group from near is known, rise or no rise:
                // whoever is being struck knows by whom.
                seen = seen || enemy.InCombat && enemy.TargetObject is GameLiving victim && _allies.Contains(victim) &&
                    enemy.IsWithinRadius(victim, FeltWithin);
                if (seen)
                    _lastSeen[enemy] = now;
                if (_lastSeen.TryGetValue(enemy, out long last) && now - last <= MemoryMilliseconds)
                    known.Add(enemy);
            }
            return known;
        }

        /// <summary>
        /// The member the enemy's assist train is on right now (bots/pvp/PvpHealPlan.cs); null for
        /// nobody. The group's healers put it first. A human group hears this on voice; the squad
        /// counts the enemy fighters in melee on each member.
        /// </summary>
        public GameLiving Trained { get; private set; }

        /// <summary>The group's own state this second in the three readings (bots/pvp/PvpReading.cs).</summary>
        public PvpReading Own { get; private set; }
        /// <summary>The enemy's state as the group sees it: an enemy out of sight counts as free.</summary>
        public PvpReading Theirs { get; private set; }
        /// <summary>What the group is trying to do right now.</summary>
        public PvpOrder Order { get; private set; }
        private long _orderSince;
        // The hold on incoming: when the enemy was first seen, and whether the hold has ended (it is not taken up again).
        private long _firstSeen;
        private bool _holdOver;
        // This match's number for choices made by chance (whether an outranged group waits out a hold).
        private readonly int _draw = Util.Random(0, 99);

        /// <summary>Before the fight: the facts the group's choice to hold or walk in was made from; null once it is over.</summary>
        public string Incoming { get; private set; }

        // The longest cast mez among these bots: what a player knows of a class.
        // A player's spells are not known to the squad: only bots count here.
        private static int MezRange(IEnumerable<GameLiving> bots) => bots.Where(bot => bot.IsAlive).OfType<GameBot>()
            .SelectMany(bot => bot.CrowdControlSpells ?? [])
            .Where(spell => spell.SpellType == eSpellType.Mesmerize && spell.CastTime > 0)
            .Select(spell => spell.Range).DefaultIfEmpty(0).Max();
        // Free enemy fighters in melee on the group's healers and casters.
        private List<GameLiving> _pursuers = [];
        // Since when each enemy fighter has been on the group's healers and casters.
        private readonly Dictionary<GameLiving, long> _onBackLineSince = new();
        // Enemy fighters running at the group's healers and casters, not yet in melee on them (PvpPeelWeights.InterceptWithin).
        private List<GameLiving> _incoming = [];
        // Replaced whole every second, never changed in place: brains read it from their own threads.
        private HashSet<GameLiving> _trainers = [];
        // When each healer or caster last took a step away from enemy fighters.
        private readonly Dictionary<GameBot, long> LastStandStep = new();
        // When the walk of that step should be over.
        private readonly Dictionary<GameBot, long> StandStepEnds = new();

        private static bool IsSlowed(GameLiving bot) =>
            bot.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
            bot.effectListComponent.ContainsEffectForEffectType(eEffect.Snare);

        private static bool IsFighter(GameLiving bot) => PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee;

        private static PvpMemberState StateOf(GameLiving bot, bool seen = true) => new(IsFighter(bot),
            PvpMember.ClassOf(bot) is eCharacterClass.Cleric or eCharacterClass.Druid or eCharacterClass.Healer or eCharacterClass.Friar,
            bot.IsAlive, seen && (bot.IsMezzed || bot.IsStunned), seen && IsSlowed(bot), seen && (bot.IsBeingInterrupted || bot.IsSilenced));

        /// <summary>Takes the readings for both groups and chooses the order from them.</summary>
        private void Read(IReadOnlyList<GameLiving> enemies, List<GameLiving> known)
        {
            PvpWeights weights = PvpWeights.Of(_members.FirstOrDefault());
            NoteRaised();
            Own = PvpReading.Of(_allies.Select(member => StateOf(member)));
            Theirs = PvpReading.Of(enemies.Select(enemy => StateOf(enemy, known.Contains(enemy))));
            _pursuers = known.Where(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                (IsFighter(enemy) || enemy.attackComponent?.AttackState == true) &&
                enemy.TargetObject is GameLiving { IsAlive: true } victim && _allies.Contains(victim) && !IsFighter(victim) &&
                enemy.IsWithinRadius(victim, weights.Peel.MeleeReach)).ToList();
            // The group answers a fighter on its healers and casters after its reaction time, not in the tick it arrives
            // (PvpHumanWeights.ReactionMinMilliseconds; one draw for the group for each arrival).
            long tickNow = GameLoop.GameLoopTime;
            foreach (GameLiving gone in _onBackLineSince.Keys.Where(enemy => !_pursuers.Contains(enemy)).ToList())
                _onBackLineSince.Remove(gone);
            if (_members.FirstOrDefault(member => member.IsAlive) is { } watcher)
            {
                _pursuers = _pursuers.Where(enemy =>
                {
                    if (!_onBackLineSince.TryGetValue(enemy, out long since))
                        _onBackLineSince[enemy] = since = tickNow;
                    return PvpNotice.Reacted(watcher, enemy, PvpEvent.OnTheBackLine, since);
                }).ToList();
            }
            List<GameLiving> inMelee = _pursuers;
            _incoming = weights.Peel.InterceptWithin <= 0 ? [] : known.Where(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                IsFighter(enemy) && !inMelee.Contains(enemy) &&
                enemy.TargetObject is GameLiving { IsAlive: true } victim && _allies.Contains(victim) && !IsFighter(victim) &&
                enemy.IsWithinRadius(victim, weights.Peel.InterceptWithin)).ToList();
            long now = GameLoop.GameLoopTime;
            PvpOrder order = PvpOrder.Engage;
            if (FightStarted || _holdOver)
                Incoming = null;
            if (FightStarted)
            {
                // Strung out: the share of the living fighters that are out of the healers' reach.
                double far = weights.Order.FightersFarShare(
                    _members.Where(member => member.IsAlive && IsFighter(member)).Select(member => new Vector2(member.X, member.Y)).ToList(),
                    _members.Where(member => member.IsAlive && PvpMember.RoleOf(member) == PvpRole.Heal).Select(member => new Vector2(member.X, member.Y)).ToList());
                order = PvpOrders.Choose(Own, Theirs, _pursuers.Select(enemy => enemy.TargetObject).Distinct().Count(),
                    Order == PvpOrder.Hold ? PvpOrder.Engage : Order, (now - _orderSince) / 1000.0, weights.Order, far);
            }
            else if (!_holdOver && known.Count > 0)
            {
                if (_firstSeen == 0)
                    _firstSeen = now;
                List<GameBot> alive = _members.Where(member => member.IsAlive).ToList();
                int nearest = alive.Count == 0 ? 0 : (int)known.Min(enemy => alive.Min(member => member.GetDistanceTo(enemy)));
                // The enemy's reach is its realm's, whoever has come into sight: a group that sees Midgard's
                // fighters knows a Healer is behind them.
                int ownReach = MezRange(_members), theirReach = MezRange(enemies);
                double waited = (now - _firstSeen) / 1000.0;
                // For the recording: what the choice to hold was made from.
                Incoming = $"mez reach {ownReach} against {theirReach}, nearest enemy {nearest}, seen for {waited:0} s, draw {_draw}";
                if (PvpOrders.Holds(ownReach, theirReach, waited, nearest, weights.Order) ||
                    PvpOrders.WaitsOutAHold(ownReach, theirReach, waited, nearest, _draw, weights.Order))
                    order = PvpOrder.Hold;
                else if (Order == PvpOrder.Hold)
                    _holdOver = true;
            }
            // A group that falls back as one is under Extend for as long as it does (bots/pvp/PvpKite.cs).
            if (Kiting)
                order = PvpOrder.Extend;
            // A player's call stands over the squad's own choice while it lasts.
            order = PvpCalls.InForce(order, _calledOrder, now, _calledUntil);
            if (order != Order)
            {
                Order = order;
                _orderSince = now;
                if (PvpObserver.Any && _members.FirstOrDefault(member => member.IsAlive) is { } voice)
                    PvpObserver.Say(voice, $"{PvpObserver.Realm(voice)} order: {order}" + (order == PvpOrder.Hold ? $" ({Incoming})"
                        : FightStarted ? $" (its healers free {Own.HealersFreeShare:0%}, enemy healers free {Theirs.HealersFreeShare:0%}, " +
                                         $"enemy fighters held {Theirs.FightersHeldShare:0%}, {_pursuers.Count} enemies on its healers and casters)" : ""));
            }
        }

        // The order last said in group chat.
        private PvpOrder? _saidOrder;

        /// <summary>
        /// An order is said in group chat once it has stood for a moment, and only if it is not the
        /// one said last: a third of the orders last under five seconds (Engage and Extend take
        /// turns), and saying each as it came, or dropping those that came too soon after another,
        /// would leave the player reading an order that is no longer in force.
        /// </summary>
        private void SayOrder(long now)
        {
            if (!(FightStarted || Order == PvpOrder.Hold) || _saidOrder == Order || !PvpCalls.OrderIsSettled(now, _orderSince))
                return;
            // "This one is lost" is the last word of a group with a player in it (PvpCalls.OrderAfterLost).
            if (!PvpCalls.OrderAfterLost(now, _lostSaidAt))
                return;
            _saidOrder = Order;
            // (Falling back as one, it has said so already, and louder.)
            if (Kiting)
                return;
            // An order that moves the group is shouted (PvpCalls.LoudOrder), the others said once.
            if (PvpCalls.ShoutsOrder(Order, _calledOrder == Order && now < _calledUntil, now, _shoutedOrderAt))
            {
                _shoutedOrderAt = now;
                Call(_members.FirstOrDefault(member => member.IsAlive), "order", PvpCalls.LoudOrder(Order), times: PvpCalls.LoudTimes);
                return;
            }
            Call(_members.FirstOrDefault(member => member.IsAlive), "order", PvpCalls.Order(Order));
        }

        // When the group last shouted an order.
        private long _shoutedOrderAt;

        private void FindTrained(List<GameLiving> known)
        {
            PvpWeights weights = PvpWeights.Of(_members.FirstOrDefault());
            List<GameLiving> alive = _allies.Where(member => member.IsAlive).ToList();
            List<HashSet<GameLiving>> onEach = alive.Select(member =>
            {
                HashSet<GameLiving> on = known.Where(enemy =>
                    enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject == member &&
                    enemy.IsWithinRadius(member, weights.Peel.MeleeReach) &&
                    (PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee || enemy.attackComponent?.AttackState == true)).ToHashSet();
                if (!weights.Heal.TrainCountsEveryAttacker)
                    return on;
                // Whoever hit it in the last few seconds, as its own combat log would show: a
                // caster at range, and a pet, which counts as the enemy that owns it.
                foreach (GameLiving attacker in member.attackComponent.AttackerTracker.Attackers)
                {
                    // A pet counts as its owner; anybody else, bot or player, as itself.
                    GameLiving enemy = attacker is GameBot or GamePlayer ? attacker
                        : attacker is GameNPC { IsAlive: true, Brain: IControlledBrain brain } ? brain.GetLivingOwner() : null;
                    if (enemy is { IsAlive: true } && known.Contains(enemy) && (enemy != attacker || !enemy.IsMezzed && !enemy.IsStunned))
                        on.Add(enemy);
                }
                return on;
            }).ToList();
            List<int> attackers = onEach.Select(on => on.Count).ToList();
            int chosen = PvpHealPlan.Trained(attackers, Trained != null ? alive.IndexOf(Trained) : -1, weights.Heal);
            GameLiving now = chosen >= 0 ? alive[chosen] : null;
            _trainers = chosen >= 0 ? onEach[chosen] : [];
            if (now != Trained && now != null && FightStarted)
            {
                // Recorded as the bot's own decision; for a player in the group, under the squad's first bot with its name.
                if (now is GameBot trainedBot)
                    PvpRecorder.Decision(trainedBot, new { kind = "Trained", score = attackers[chosen] }, [], null);
                else if (_members.FirstOrDefault(member => member.IsAlive) is { } witness)
                    PvpRecorder.Decision(witness, new { kind = "TrainedPlayer", d = now.Name, score = attackers[chosen] }, [], null);
                PvpObserver.Say(now, $"{PvpObserver.Name(now)} is being trained: {attackers[chosen]} on it");
                if (PvpCalls.WorthIncoming(attackers[chosen]))
                    Call(now is GameBot ? now : _members.FirstOrDefault(member => member.IsAlive), "incoming",
                        PvpCalls.Incoming(now.Name, attackers[chosen], now is not GameBot), PvpCalls.IncomingEveryMilliseconds);
            }
            Trained = now;
        }

        /// <summary>
        /// True while the group is chasing: its fight has begun and no member has had a known enemy
        /// within <see cref="PvpOrders.ChaseBeyond"/> for a few seconds. The speed song is for then.
        /// </summary>
        public bool Chasing { get; private set; }
        private long _noContactSince;

        /// <summary>The bot that leads the roam in a play session where a bot drives; null otherwise.</summary>
        public GameBot Driver { get; set; }

        // A call by a player of the group (bots/pvp/PvpCalls.cs, M9.3): the order it stands for and until
        // when, who called, and the target of an assist call.
        private PvpOrder? _calledOrder;
        private long _calledUntil, _calledTargetUntil;
        private GameLiving _caller, _calledTarget;

        /// <summary>
        /// A player of the group calls an order: the squad takes it as its own for
        /// <see cref="PvpCalls.PlayerCallSeconds"/> and then decides for itself again. Under a
        /// called regroup every member comes back to the caller, not only the fighters.
        /// </summary>
        public void CallOrder(GameLiving caller, PvpOrder order)
        {
            _retreatUntil = 0;
            _restUntil = 0;
            _calledOrder = order;
            _caller = caller;
            _calledUntil = GameLoop.GameLoopTime + PvpCalls.PlayerCallSeconds * 1_000L;
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
            {
                PvpRecorder.Decision(voice, new { kind = "PlayerCall", order = order.ToString(), d = caller.Name }, [], null);
                Call(voice, "answer", PvpCalls.Answer(order, caller.Name));
            }
        }

        /// <summary>
        /// A player of the group calls its own target as the kill target. False when the target
        /// is not an enemy the group knows. It stands while the target lives and is known, for at
        /// most <see cref="PvpCalls.PlayerAssistSeconds"/>, or until the player calls another.
        /// </summary>
        /// <param name="standsBy">The target is of an enemy group that stands by the group's fight (PvpRoam.StandsBy): the call takes that group on.</param>
        public bool CallAssist(GameLiving caller, GameLiving target, bool standsBy = false)
        {
            if (target is not { IsAlive: true } || !(Known.Contains(target) || standsBy))
                return false;
            _retreatUntil = 0;
            _restUntil = 0;
            _calledTarget = target;
            (_calledTargetAt, _calledTargetUntil) = (GameLoop.GameLoopTime, GameLoop.GameLoopTime + PvpCalls.PlayerAssistSeconds * 1_000L);
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
            {
                PvpRecorder.Decision(voice, new { kind = "PlayerAssist", d = target.Name, by = caller.Name }, [], null);
                Call(voice, "answer", PvpCalls.Assist(target.Name, PvpMember.ClassOf(target), target.HealthPercent));
            }
            return true;
        }

        // When the player called its assist: the target of a group that stood by is known to the group a moment later.
        private long _calledTargetAt;

        /// <summary>The enemy a player's assist call or control call stands on; null for none (PvpRoam.PlayerPicks).</summary>
        public GameLiving CalledByPlayer =>
            _calledTarget is { IsAlive: true } assist && GameLoop.GameLoopTime < _calledTargetUntil ? assist
            : _calledControl is { IsAlive: true } control && GameLoop.GameLoopTime < _calledControlUntil ? control : null;

        // "Control that one": the enemy a player of the group has called for control on, and until when.
        private GameLiving _calledControl;
        private long _calledControlUntil;
        // How many of the enemy were asleep a second ago ("mez landed").
        private int _asleepBefore;

        /// <summary>
        /// A player of the group calls for control on its own target (M9.3): the group's mez,
        /// stun, root and nearsight lean to it (<see cref="PvpWeights.ControlCalledByPlayer"/>)
        /// for <see cref="PvpCalls.PlayerControlSeconds"/>. False when the target is not an enemy
        /// the group knows.
        /// </summary>
        public bool CallControl(GameLiving caller, GameLiving target, bool standsBy = false)
        {
            if (target is not { IsAlive: true } || !(Known.Contains(target) || standsBy))
                return false;
            _retreatUntil = 0;
            _restUntil = 0;
            _calledControl = target;
            _calledControlUntil = GameLoop.GameLoopTime + PvpCalls.PlayerControlSeconds * 1_000L;
            // Answered by the member that mezzes for the group, when it has one alive.
            GameBot voice = _members.FirstOrDefault(member => member.IsAlive && PvpActionChoice.IsController(PvpMember.ClassOf(member), PvpWeights.Of(member)))
                            ?? _members.FirstOrDefault(member => member.IsAlive);
            if (voice != null)
            {
                PvpRecorder.Decision(voice, new { kind = "PlayerControl", d = target.Name, by = caller.Name }, [], null);
                Call(voice, "answer", PvpCalls.Control(target.Name, PvpMember.ClassOf(target)));
            }
            return true;
        }

        private long _retreatUntil;

        /// <summary>The player the group runs with while a call to retreat stands (PvpCalls.PlayerRetreatSeconds); null otherwise.</summary>
        public GameLiving RetreatsTo => _retreatUntil != 0 && _caller is { IsAlive: true } && GameLoop.GameLoopTime < _retreatUntil ? _caller : null;

        /// <summary>
        /// A player of the group calls a retreat: every bot of it drops its target and its cast
        /// and runs with the caller, whatever attacks it (BotBrain.TryPvpRetreat), until the call
        /// runs out or the player makes another.
        /// </summary>
        private long _restUntil, _restSince;
        private Point3D _restCalledAt;
        private bool _restUntilFull;

        /// <summary>
        /// True while the group rests: the player called it (PvpCalls.RestStands), or a team of
        /// bots took it between two fights and somebody is still not full.
        /// </summary>
        public bool Resting
        {
            get
            {
                if (_restUntil == 0)
                    return false;
                // A blow given or taken since the rest began: the flag of the fight just over lingers and is no reason.
                bool combat = _allies.Any(member => member.IsAlive && Math.Max(member.LastAttackTick, member.LastAttackedByEnemyTick) > _restSince + 500);
                int moved = _restCalledAt != null && _caller is { IsAlive: true } ? (int)_caller.GetDistanceTo(_restCalledAt) : 0;
                bool stands = PvpCalls.RestStands(GameLoop.GameLoopTime, _restUntil, combat, moved) &&
                    (!_restUntilFull || _members.Any(member => member.IsAlive &&
                        (member.HealthPercent < 95 || member.MaxMana > 0 && member.ManaPercent < 95)));
                if (!stands)
                    _restUntil = 0;
                return stands;
            }
        }

        /// <summary>
        /// "/8v8 call abilities": each companion that has an active realm ability says in group chat
        /// which are up and which are down (PvpCalls.AbilitiesLine). How many spoke. Said to the
        /// player that asked, whether the companions' calls are on or off, the dead too.
        /// </summary>
        public int SayAbilities(GamePlayer player)
        {
            int spoke = 0;
            foreach (GameBot bot in _members)
            {
                string line = PvpCalls.AbilitiesLine(AutonomousBotRealmAbilities.ActivesOf(bot)
                    .Select(ability => (ability.Name, bot.GetSkillDisabledDuration(ability))).ToList());
                if (line == null)
                    continue;
                player.Out.SendMessage($"[Party] {bot.Name}: \"{line}\"", PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);
                spoke++;
            }
            return spoke;
        }

        /// <summary>A member says a line in group chat to the group's players, no sooner than <paramref name="every"/> milliseconds after the last of its kind (the driver's "Stick, moving out").</summary>
        public void Tell(GameLiving speaker, string kind, string text, int every = 0) => Call(speaker, kind, text, every);

        /// <summary>A player of the group calls a rest (PvpCalls.PlayerRestSeconds).</summary>
        public void CallRest(GameLiving caller)
        {
            _caller = caller;
            _restCalledAt = new Point3D(caller.X, caller.Y, caller.Z);
            RestsAt = null;   // (its companions are about their player as they follow it)
            _restUntilFull = false;
            _restSince = GameLoop.GameLoopTime;
            _restUntil = GameLoop.GameLoopTime + PvpCalls.PlayerRestSeconds * 1_000L;
            _retreatUntil = 0;
            _calledOrder = null;
            _calledTarget = null;
            _calledControl = null;
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
            {
                PvpRecorder.Decision(voice, new { kind = "PlayerRest", d = caller.Name }, [], null);
                Call(voice, "answer", PvpCalls.Resting);
            }
        }

        /// <summary>The rest is over (an enemy has come near, or the player's group has other things to do).</summary>
        public void EndRest() => _restUntil = 0;

        /// <summary>
        /// Where a team that rests sits down together (PvpPositionWeights.RestWithin): beside one of its own that is down
        /// (its player first), so that it is in reach to be raised; else where its driver stands, which is where its
        /// player is; else where the member in its middle stands.
        /// </summary>
        public Vector3? RestsAt { get; private set; }

        /// <summary>The place its members still walk in to, in the first seconds of a rest; null once they sit where they are.</summary>
        public Vector3? GathersToRest =>
            RestsAt is { } place && Resting && GameLoop.GameLoopTime - _restSince < PvpWeights.Of(_members.FirstOrDefault()).Position.RestWalkInSeconds * 1_000L
                ? place : null;

        /// <summary>Whether this member still walks in to the group's resting place before it sits (PvpPositionWeights.WalksInToRest).</summary>
        public bool WalksInToRest(GameBot bot) =>
            bot != null && RestsAt is { } place && PvpWeights.Of(bot).Position.WalksInToRest(
                Vector3.Distance(place, new Vector3(bot.X, bot.Y, bot.Z)), GameLoop.GameLoopTime - _restSince);

        private Vector3? RestPlace()
        {
            GameLiving down = _allies.FirstOrDefault(member => member is GamePlayer { IsAlive: false, ObjectState: GameObject.eObjectState.Active });
            List<GameBot> up = _members.Where(member => member.IsAlive).ToList();
            if (up.Count == 0)
                return null;
            var middle = new Vector2((float)up.Average(member => member.X), (float)up.Average(member => member.Y));
            GameLiving at = down
                ?? _members.Where(member => !member.IsAlive && member.ObjectState == GameObject.eObjectState.Active)
                    .OrderBy(member => Vector2.Distance(middle, new Vector2(member.X, member.Y))).FirstOrDefault()
                ?? (GameLiving)PvpFormation.DriverOf(up)
                ?? up.OrderBy(member => Vector2.Distance(middle, new Vector2(member.X, member.Y))).First();
            return new Vector3(at.X, at.Y, at.Z);
        }

        /// <summary>A team of bots rests between two fights, until everybody is full or the time is up.</summary>
        public void RestAsATeam(int seconds)
        {
            if (_restUntil != 0)
                return;
            RestsAt = RestPlace();
            _restCalledAt = null;
            _restUntilFull = true;
            _restSince = GameLoop.GameLoopTime;
            _restUntil = GameLoop.GameLoopTime + seconds * 1_000L;
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
                PvpRecorder.Decision(voice, new { kind = "TeamRest", score = seconds }, [], null);
        }

        public void CallRetreat(GameLiving caller)
        {
            // A retreat is run with the caller, and a dead caller runs nowhere: the group says so and fights on. In the
            // owner's evening of 2026-11-24 it answered "Retreating with Talveh." to a call made seventeen seconds
            // after the caller had died, and went on fighting for the 35 seconds it took to die.
            if (!caller.IsAlive)
            {
                Call(_members.FirstOrDefault(member => member.IsAlive), "answer", PvpCalls.CannotRetreat(caller.Name));
                return;
            }
            _restUntil = 0;
            _caller = caller;
            _retreatUntil = GameLoop.GameLoopTime + PvpCalls.PlayerRetreatSeconds * 1_000L;
            _calledOrder = null;
            _calledTarget = null;
            _calledControl = null;
            LoseSight();
            if (_members.FirstOrDefault(member => member.IsAlive) is { } voice)
            {
                PvpRecorder.Decision(voice, new { kind = "PlayerRetreat", d = caller.Name }, [], null);
                Call(voice, "answer", PvpCalls.Retreating(caller.Name));
            }
        }

        /// <summary>True for the enemy a player's call for control stands on.</summary>
        public bool IsCalledControl(GameLiving enemy) =>
            enemy != null && enemy == _calledControl && enemy.IsAlive && GameLoop.GameLoopTime < _calledControlUntil;

        /// <summary>True for the enemy a player has called with <c>/8v8 call assist</c>, while that call stands.</summary>
        public bool IsCalledAssist(GameLiving enemy) => enemy != null && enemy == _calledTarget;

        // What each member last took for the group's kill target.
        private readonly Dictionary<GameBot, GameLiving> _heard = new();

        /// <summary>
        /// The kill target as this member has it: the group's call once its time to switch has passed
        /// since the call was made; until then the one it had, while that one lives.
        /// </summary>
        private GameLiving Hears(GameBot bot, GameLiving called)
        {
            _heard.TryGetValue(bot, out GameLiving had);
            if (had == called)
                return called;
            if (called == null || PvpNotice.Reacted(bot, called, PvpEvent.KillCalled, _killCalledTick))
            {
                _heard[bot] = called;
                return called;
            }
            return had is { IsAlive: true } ? had : null;
        }

        /// <summary>A member has used its Purge or Group Purge: said in group chat (M9.2).</summary>
        public void SayPurged(GameBot bot, bool group) => Call(bot, "purge-" + bot.Name, PvpCalls.Purged(group), 2_000);

        /// <summary>The player a called regroup gathers on, while that call stands; null otherwise.</summary>
        private GameLiving RegroupsOn => _calledOrder == PvpOrder.Regroup && _caller is { IsAlive: true } &&
                                         GameLoop.GameLoopTime < _calledUntil ? _caller : null;

        // When the group's kill target last changed (PvpKillWeights.CollapseOnCallSeconds).
        private long _killCalledTick;
        // Who is raising whom, and until when the claim holds (a resurrection is a four-second cast).
        private readonly Dictionary<GameLiving, (GameBot By, long Until)> _raising = new();

        private readonly HashSet<GameLiving> _seenDead = new();
        private readonly HashSet<GameLiving> _raised = new();

        /// <summary>Who of the group, the player too, has been dead and is alive again: raised, by a bot or by a player.</summary>
        private void NoteRaised()
        {
            lock (_raised)
            {
                foreach (GameLiving member in _allies)
                {
                    if (!member.IsAlive)
                        _seenDead.Add(member);
                    else if (_seenDead.Remove(member))
                        _raised.Add(member);
                }
            }
        }

        /// <summary>True for a member that was raised in this fight: it came back without its buffs, and those it had on others went when it died.</summary>
        public bool WasRaised(GameLiving member)
        {
            lock (_raised)
                return _raised.Contains(member);
        }

        /// <summary>True while another healer of the group is raising somebody: one healer at a time leaves the living.</summary>
        public bool AnotherHealerIsRaising(GameBot bot)
        {
            long now = GameLoop.GameLoopTime;
            lock (_raising)
                return _raising.Values.Any(claim => claim.By != bot && claim.Until > now && claim.By.IsAlive &&
                                                    PvpMember.RoleOf(claim.By) == PvpRole.Heal) && PvpMember.RoleOf(bot) == PvpRole.Heal;
        }

        /// <summary>True when nobody else is raising this body; the claim is then the bot's for the length of the cast.</summary>
        public bool ClaimRaise(GameBot bot, GameLiving corpse, int castMilliseconds)
        {
            long now = GameLoop.GameLoopTime;
            lock (_raising)
            {
                if (_raising.TryGetValue(corpse, out var held) && held.By != bot && held.Until > now && held.By.IsAlive)
                    return false;
                _raising[corpse] = (bot, now + castMilliseconds + 2_000);
                return true;
            }
        }

        // When each kind of call was last said in group chat.
        private readonly Dictionary<string, long> _lastCall = new();

        /// <summary>
        /// A call in group chat to the players of the group (bots/pvp/PvpCalls.cs), said by one of
        /// its bots. Nothing is said in a group of bots only. Never throws: a chat line must not
        /// be able to stop a fight.
        /// </summary>
        /// <param name="times">How many times it is said: more than once for a call that moves the group (PvpCalls.LoudTimes).</param>
        private void Call(GameLiving speaker, string kind, string text, int every = 0, int times = 1)
        {
            try
            {
                if (speaker == null || !_allies.Any(ally => ally is GamePlayer))
                    return;
                long now = GameLoop.GameLoopTime;
                lock (_lastCall)
                {
                    if (!PvpCalls.Due(now, _lastCall.GetValueOrDefault(kind), every))
                        return;
                    _lastCall[kind] = now;
                }
                foreach (GamePlayer player in _allies.OfType<GamePlayer>())
                {
                    if (player.Client?.IsPlaying != true || player.TempProperties.GetProperty<bool>(PvpCalls.OffProperty))
                        continue;
                    for (int said = 0; said < Math.Max(1, times); said++)
                        player.Out.SendMessage($"[Party] {speaker.Name}: \"{text}\"", PacketHandler.eChatType.CT_Group, PacketHandler.eChatLoc.CL_ChatWindow);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>The group's decisions for this second: what it sees, who is being trained, kill target, interrupt duty, guard and peel.</summary>
        public void Decide(IReadOnlyList<GameLiving> enemies)
        {
            // Under a called retreat the group decides nothing: it runs.
            if (RetreatsTo != null)
            {
                LoseSight();
                return;
            }
            // Nor does one that has broken off (bots/pvp/PvpKite.cs): it runs.
            if (Leaving)
            {
                LoseSight();
                Leave(GameLoop.GameLoopTime);
                return;
            }
            List<GameLiving> known = Look(enemies);
            Known = known;
            bool mopsUp = FightStarted && PvpWeights.Of(_members.FirstOrDefault()).Position.MopsUp(_allies.Count(member => member.IsAlive), known.Count(enemy => enemy.IsAlive));
            if (mopsUp && !MopsUp && _members.FirstOrDefault(member => member.IsAlive) is { } finisher)
                PvpRecorder.Decision(finisher, new { kind = "MopUp", score = known.Count(enemy => enemy.IsAlive) }, [], null);
            MopsUp = mopsUp;
            // A chase: the fight is on, and for some seconds no member has had an enemy near it.
            long tick = GameLoop.GameLoopTime;
            bool contact = known.Any(enemy => enemy.IsAlive && _allies.Any(member => member.IsAlive && member.IsWithinRadius(enemy, PvpOrders.ChaseBeyond)));
            if (!FightStarted || contact || !known.Any(enemy => enemy.IsAlive))
                _noContactSince = 0;
            else if (_noContactSince == 0)
                _noContactSince = tick;
            Chasing = PvpOrders.Chases(FightStarted, _noContactSince, tick);
            FindTrained(known);
            Read(enemies, known);
            SayOrder(GameLoop.GameLoopTime);
            KillTarget = CallTarget(_members, known, KillTarget is { IsAlive: true } current && known.Contains(current) ? current : null);
            // "Mez landed" (M9.2): two or more of them asleep that were not a second ago, said by the member that mezzes.
            int asleep = known.Count(enemy => enemy.IsAlive && enemy.IsMezzed);
            if (FightStarted && PvpCalls.WorthMezLanded(asleep, _asleepBefore))
                Call(_members.FirstOrDefault(member => member.IsAlive && PvpActionChoice.IsController(PvpMember.ClassOf(member), PvpWeights.Of(member)))
                     ?? _members.FirstOrDefault(member => member.IsAlive), "mez", PvpCalls.MezLanded(asleep), PvpCalls.MezLandedEveryMilliseconds);
            _asleepBefore = asleep;
            // A player who heals is told when the group is out of its reach: the bots' own healers follow by themselves.
            if (FightStarted)
            {
                foreach (GamePlayer healer in _allies.OfType<GamePlayer>().Where(player => player.IsAlive && PvpMember.RoleOf(player) == PvpRole.Heal))
                {
                    int reach = PvpWeights.Of(_members.FirstOrDefault()).Position.HealRange;
                    List<GameBot> beyond = _members.Where(member => member.IsAlive && !member.IsWithinRadius(healer, reach)).ToList();
                    if (PvpCalls.WorthOutOfReach(beyond.Count))
                        Call(beyond.OrderByDescending(member => member.GetDistanceTo(healer)).First(), "reach-" + healer.Name,
                            PvpCalls.OutOfYourReach(healer.Name, beyond.Count), PvpCalls.OutOfReachEveryMilliseconds);
                }
            }
        }

        /// <summary>
        /// One kill target for the group, given to everyone who is not a healer; interrupt duty;
        /// guard and peel. Distance is measured from the middle of the group's damage dealers.
        /// </summary>
        private GameLiving CallTarget(List<GameBot> team, List<GameLiving> enemies, GameLiving current)
        {
            List<GameBot> dealers = team.Where(bot => bot.IsAlive && PvpMember.RoleOf(bot) == PvpRole.Assist).ToList();
            List<GameLiving> alive = enemies.Where(enemy => enemy.IsAlive).ToList();
            GameLiving target = null;
            if (dealers.Count > 0 && alive.Count > 0)
            {
                double x = dealers.Average(bot => bot.X), y = dealers.Average(bot => bot.Y);
                PvpKillWeights kill = PvpWeights.Of(team[0]).Kill;
                // Out of position: how far each enemy stands from the middle of its own healers, the ones in sight.
                List<GameLiving> theirHealers = alive.Where(enemy => PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Healer).ToList();
                double hx = theirHealers.Count == 0 ? 0 : theirHealers.Average(bot => bot.X), hy = theirHealers.Count == 0 ? 0 : theirHealers.Average(bot => bot.Y);
                // Whose pets are in melee on this group's healers and casters (PvpKillWeights.PerPetOnOurBackline).
                var petsOf = new Dictionary<GameLiving, int>();
                if (kill.PerPetOnOurBackline != 0)
                {
                    foreach (GameBot member in team.Where(bot => bot.IsAlive && !IsFighter(bot)))
                    {
                        foreach (GameNPC npc in member.GetNPCsInRadius(400))
                        {
                            if (npc.IsAlive && npc.TargetObject == member && npc.Brain is IControlledBrain owned && owned.GetLivingOwner() is { } owner)
                                petsOf[owner] = petsOf.GetValueOrDefault(owner) + 1;
                        }
                    }
                }
                // A target that is not going down goes stale; one given up alive is left for a while (PvpKillWeights.PerStaleSecond).
                long tick = GameLoop.GameLoopTime;
                if (current == null || current.HealthPercent <= kill.StaleIfHealthAbove)
                    _killProgressTick = tick;
                // A group that falls back as one calls its target among the enemies that have followed to its place.
                List<GameLiving> pool = !Kiting ? alive
                    : alive.Where(enemy => Vector2.Distance(new Vector2(enemy.X, enemy.Y), _body) <= PvpWeights.Of(team[0]).Kite.KillWithin).ToList();
                var views = pool.Select((enemy, id) => new PvpTargetView(id, PvpMember.ArchetypeOf(enemy),
                    enemy.HealthPercent, enemy.IsMezzed, (int)Math.Sqrt(Math.Pow(enemy.X - x, 2) + Math.Pow(enemy.Y - y, 2)),
                    theirHealers.Count == 0 ? 0 : (int)Math.Sqrt(Math.Pow(enemy.X - hx, 2) + Math.Pow(enemy.Y - hy, 2)),
                    petsOf.GetValueOrDefault(enemy),
                    enemy == current ? PvpKillTarget.StaleSeconds((tick - _killProgressTick) / 1000.0, enemy.HealthPercent, kill) : 0,
                    _givenUp.TryGetValue(enemy, out long left) && tick - left < kill.AbandonedForSeconds * 1000L)).ToList();
                int chosen = PvpKillTarget.Choose(views, current != null ? pool.IndexOf(current) : -1, kill);
                target = chosen >= 0 ? pool[chosen] : null;
                // A player's assist call names the kill target while that target lives and is known. Here, before
                // the target is handed to the members: set afterwards, the group's call changed and nobody
                // went for it (the first test of the call, 2026-11-20: 24 seconds as the kill target, no blow).
                if (_calledTarget != null)
                {
                    if (_calledTarget.IsAlive && pool.Contains(_calledTarget) && tick < _calledTargetUntil)
                    {
                        target = _calledTarget;
                        chosen = pool.IndexOf(target);
                    }
                    // (Called on a group that stood by: it is one of the group's enemies within a turn or two.)
                    else if (!(_calledTarget.IsAlive && tick - _calledTargetAt < 3_000))
                        _calledTarget = null;
                }
                // A group that has fallen back turns on whoever followed it out of its healers' reach (bots/pvp/PvpKite.cs).
                if (Kiting && Prey is { IsAlive: true } followed && pool.Contains(followed))
                {
                    target = followed;
                    chosen = pool.IndexOf(followed);
                }
                if (target != current)
                {
                    if (current is { IsAlive: true })
                        _givenUp[current] = tick;
                    _killProgressTick = tick;
                    _killCalledTick = tick;
                }
                // The call is recorded when it changes, with what else could have been called.
                if (target != current && target != null)
                {
                    PvpObserver.Say(dealers[0], $"{PvpObserver.Realm(dealers[0])} calls {PvpObserver.Name(target)}, at {target.HealthPercent}% health");
                    Call(dealers[0], "assist", PvpCalls.Assist(target.Name, PvpMember.ClassOf(target), target.HealthPercent));
                }
                if (target != current && target != null)
                    PvpRecorder.Call(team[0], target, PvpKillTarget.Score(views[chosen], kill), current,
                        views.OrderByDescending(view => PvpKillTarget.Score(view, kill)).Take(4).Select(view => (object)new
                        {
                            d = pool[view.Id].Name, score = PvpKillTarget.Score(view, kill), view.HealthPercent, view.Mezzed, view.Distance, view.FromItsHealers,
                        }));
            }
            var guarded = new HashSet<GameLiving>();
            // Under Extend: which of the enemies on the group's healers and casters a fighter has turned on already.
            var turnedOn = new HashSet<int>();
            Dictionary<GameBot, GameLiving> duty = InterruptDuty(team, alive, target);
            // The enemies a healer's pet has been sent at this turn: two Druids, two enemies.
            var petsOn = new HashSet<GameLiving>();
            // The middle of the group's healers and casters: where its fighters fall back to.
            List<GameLiving> soft = _allies.Where(member => member.IsAlive && !IsFighter(member)).ToList();
            Vector3? home = soft.Count == 0 ? null
                : new Vector3((float)soft.Average(member => member.X), (float)soft.Average(member => member.Y), (float)soft.Average(member => member.Z));
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
                PvpRole role = PvpMember.RoleOf(bot);
                // (A group that does not peel: its shield fighter is one more fighter.)
                if (role == PvpRole.Peel && !PvpWeights.Of(bot).Peel.Enabled)
                    role = PvpRole.Assist;
                brain.PvpSquad = this;
                // From the run-in on: the owner, 2026-11-19, "we were using speed + sprint all the time until initial contact", and after it.
                PvpPositionWeights legs = PvpWeights.Of(bot).Position;
                // The owner, 2026-11-20: "No sprint when roaming, just speed from Bard/Skald/Minstrel. Sprint during
                // fight when needed." On the road nobody sprints. In a fight a bot sprints when it is pressed or has
                // ground to cover (PvpPositionWeights.SprintNeeded), and that costs it endurance on the move, as it
                // costs a player (SprintCostPerSecond; this runs once a second).
                // (And on a retreat the player has called. The owner's evening of 2026-11-24: three Midgard groups ran the
                // group down at 480 to 500 units a second, sprinting on their Skald's song, while the seven ran behind the
                // player at 350 to 400 with their endurance at 100 from the call to the last death.)
                bool pressed = Chasing || Kiting || RetreatsTo != null || FallingBack.Contains(bot) || _huntedTick.TryGetValue(bot, out long huntedAt) && GameLoop.GameLoopTime - huntedAt < 2_500;
                // (Before the first blow it is still on the road: SprintOnTheRoad has said whether it sprints, and taken its cost.)
                if (FightStarted)
                {
                    bot.PvpSprinting = bot.IsAlive && legs.SprintInFights &&
                                       legs.SprintNeeded(pressed, WayLeft(bot), bot.PvpSprinting) &&
                                       legs.CanSprint(bot.EndurancePercent, bot.PvpSprinting, IsFighter(bot) && !Chasing && role != PvpRole.Peel ? legs.FighterKeepsEndurancePercent : 0);
                    if (bot.PvpSprinting && bot.IsMoving && legs.SprintCostPerSecond > 0)
                        bot.Endurance = Math.Max(0, bot.Endurance - PvpPositionWeights.SprintCostWith(legs.SprintCostPerSecond,
                            bot.GetAbility<DOL.GS.RealmAbilities.AtlasOF_LongWindAbility>() is { } longWind ? longWind.GetAmountForLevel(longWind.Level) : 0));
                }
                // For a watcher: a member that has run out of power does nothing, and that cannot be seen from above
                // (owner, 2026-11-19, of a Theurgist that stood still: "I do not have that information when I observe").
                if (FightStarted && bot.IsAlive && bot.MaxMana > 0)
                {
                    if (bot.ManaPercent < 10 && OutOfPower.Add(bot))
                    {
                        PvpObserver.Say(bot, $"{PvpObserver.Name(bot)} is out of power");
                        Call(bot, "power-" + bot.Name, PvpCalls.OutOfPower);
                    }
                    else if (bot.ManaPercent >= 30)
                        OutOfPower.Remove(bot);
                }
                brain.PvpEnemies = enemies;
                // What this member takes for the kill target: the group's call once it has heard it, and the
                // one it had until then (assist discipline, PvpHumanWeights.AssistSwitchMinMilliseconds).
                // (A group with no assist: the call is made and nobody takes it. PvpKillWeights.Assists.)
                GameLiving mine = PvpWeights.Of(bot).Kill.Assists ? Hears(bot, target) : null;
                PvpNotice.HurtSince(bot, PvpNotice.HumanOf(bot).HurtNoticedBelowPercent);
                brain.PvpKillTarget = mine;
                duty.TryGetValue(bot, out GameLiving onDuty);
                // The player called this target: the group's damage dealers all go for it while the call
                // stands, whatever they were on. In the first test of the call the Warrior was the kill
                // target for 24 seconds and took no blow: the Minstrel had interrupt duty for a healer,
                // the Armsman was turned on an enemy that had followed onto the back line, and the
                // Cabalist stood 1,400 units off. The shield fighter keeps its guard, the healers heal,
                // and whoever interrupts with pets keeps them where they are.
                bool calledOnto = mine != null && role == PvpRole.Assist && bot.IsAlive &&
                    (mine == _calledTarget || PvpWeights.Of(bot).Kill.Collapses(GameLoop.GameLoopTime, _killCalledTick));
                // (Its pet keeps the duty: a called assist is for the members themselves, PvpCalls.PetAim.)
                GameLiving petsDuty = onDuty;
                if (calledOnto)
                    onDuty = null;
                brain.PvpDutyTarget = onDuty;
                PvpInterruptMeans means = PvpInterruptDuty.MeansOf(PvpMember.ClassOf(bot), PvpWeights.Of(bot).Interrupt);
                GameLiving aim = null;
                if (onDuty != null && means is PvpInterruptMeans.Pets or PvpInterruptMeans.Melee && !critical && bot.IsAlive)
                    aim = onDuty;
                else if (mine != null && PvpRoles.JoinsKill(role, mine.HealthPercent))
                    aim = mine;
                else if (role == PvpRole.Peel && bot.IsAlive)
                    aim = Peel(bot, brain, team, alive, mine, guarded);
                // Under Extend the group's fighters turn on whoever followed onto its healers and
                // casters (owner, 2026-11-17: an enemy that pushed deep is turned on).
                if (calledOnto)
                    aim = mine;
                // The fight is won: every fighter goes for the kill, its guard and its peel too.
                if (MopsUp && mine != null && bot.IsAlive && IsFighter(bot))
                    aim = mine;
                if (!calledOnto && Order == PvpOrder.Extend && _pursuers.Count > 0 && role == PvpRole.Assist && onDuty == null && bot.IsAlive &&
                    IsFighter(bot) && PvpWeights.Of(bot).Order.ExtendFightersTurnOnPursuers)
                {
                    // It stays on the one it has, and two fighters take two different ones (PvpOrders.ChoosePursuer).
                    int chosen = PvpOrders.ChoosePursuer(_pursuers.Select(pursuer => (int)bot.GetDistanceTo(pursuer)).ToList(),
                        brain.PvpAssignedTarget is { } held ? _pursuers.IndexOf(held) : -1, turnedOn);
                    turnedOn.Add(chosen);
                    aim = _pursuers[chosen];
                }
                // With nobody to turn on, a fighter that is far from its healers and casters comes
                // back to them: the group falls back as one, and does not leave its fighters behind.
                // Under Regroup every fighter that is far comes back, the ones with interrupt duty too.
                int stayWithin = PvpWeights.Of(bot).Order.ExtendFightersStayWithin;
                float fromHome = home is { } middle ? Vector2.Distance(new Vector2(bot.X, bot.Y), new Vector2(middle.X, middle.Y)) : 0;
                bool fallsBack = home != null && role == PvpRole.Assist && bot.IsAlive && IsFighter(bot) && !calledOnto &&
                    (Order == PvpOrder.Extend && _pursuers.Count == 0 && stayWithin > 0 && onDuty == null && fromHome > stayWithin ||
                     Order == PvpOrder.Regroup && RegroupsOn == null && FromNearestHealer(bot, team) > PvpWeights.Of(bot).Order.RegroupWithin);
                // A group that mops up does not fall back from the one or two that are left.
                if (MopsUp)
                    fallsBack = false;
                // The player called the regroup: every member that is far from the player comes back to it, whatever its job.
                Vector3? fallBackTo = home;
                if (RegroupsOn is { } gatherOn && bot.IsAlive &&
                    PvpCalls.ComesBack((int)bot.GetDistanceTo(gatherOn), FallingBack.Contains(bot)))
                {
                    fallsBack = true;
                    fallBackTo = new Vector3(gatherOn.X, gatherOn.Y, gatherOn.Z);
                }
                // The group falls back as one (bots/pvp/PvpKite.cs): a fighter takes an enemy that is on its healers and
                // casters or near the group's place, and otherwise stands in its row between the place and the enemy.
                bool inItsRow = false;
                if (Kiting && bot.IsAlive && IsFighter(bot) && role == PvpRole.Assist && !calledOnto && RegroupsOn == null)
                {
                    PvpKiteWeights kite = PvpWeights.Of(bot).Kite;
                    bool Reached(GameLiving enemy) => PvpKite.FighterTakes(Vector2.Distance(new Vector2(enemy.X, enemy.Y), _body), kite);
                    if (!KiteStands)
                        // The leg: "The group should pull back, fighter included".
                        aim = null;
                    else if (Prey is { IsAlive: true } open && Reached(open))
                        // "...ready to take on the ones over extending before thier healer would be in range."
                        aim = open;
                    else if (aim == null || !IsPursuer(aim) && !Reached(aim))
                        // "...and then handle whichever enemies is closer."
                        aim = alive.Where(enemy => !enemy.IsMezzed && Reached(enemy)).OrderBy(enemy => bot.GetDistanceTo(enemy)).FirstOrDefault();
                    fallsBack = aim == null;
                    if (fallsBack && _screen.TryGetValue(bot, out Vector3 stand))
                    {
                        fallBackTo = stand;
                        inItsRow = Vector2.Distance(new Vector2(bot.X, bot.Y), new Vector2(stand.X, stand.Y)) <= kite.AtItsPlaceWithin;
                    }
                    fallsBack = fallsBack && fallBackTo != null;
                }
                // A group with no assist: a damage dealer with no target of its own takes the nearest enemy, an awake one
                // before a sleeping one, and keeps it while it lives (PvpKillTarget.TakesTheNearest).
                if (aim == null && !fallsBack && FightStarted && bot.IsAlive && role == PvpRole.Assist && onDuty == null && alive.Count > 0)
                {
                    GameLiving before = brain.PvpAssignedTarget is { IsAlive: true } kept && alive.Contains(kept) ? kept : null;
                    bool ownTarget = bot.TargetObject is GameLiving { IsAlive: true } has && has != before && alive.Contains(has);
                    if (PvpKillTarget.TakesTheNearest(PvpWeights.Of(bot).Kill.Assists, ownTarget))
                    {
                        int taken = PvpKillTarget.Nearest(alive.Select(enemy => ((int)bot.GetDistanceTo(enemy), enemy.IsMezzed)).ToList(),
                            before == null ? -1 : alive.IndexOf(before), PvpWeights.Of(bot).Kill.MaxDistance);
                        aim = taken >= 0 ? alive[taken] : null;
                        if (aim != null && aim != before)
                            PvpRecorder.Decision(bot, new { kind = "TakesNearest", d = aim.Name, score = 0, asleep = aim.IsMezzed }, [], null);
                    }
                }
                if (fallsBack)
                {
                    aim = null;
                    if (FallingBack.Add(bot))
                        PvpRecorder.Decision(bot, new { kind = "FallBack", score = 0 }, [], null);
                    if (brain.HasAggro)
                    {
                        brain.ClearAggroList();
                        brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                    }
                    if (!bot.IsCasting && !bot.IsIncapacitated && !inItsRow)
                        bot.PathTo(fallBackTo.Value, bot.MaxSpeed);
                }
                else
                    FallingBack.Remove(bot);
                // A fighter going for an enemy that is on the group's healers and casters wants it
                // stopped: Slam or a snare style before damage (owner, 2026-11-18).
                brain.PvpWantsControlStyle = aim != null && IsFighter(bot) && IsPursuer(aim);
                // A caster's pet is sent where its owner works: the enemy it has interrupt duty for,
                // else the kill target. (It was summoned and buffed and then never attacked: the
                // owner's scored casts take every turn, and the old brain ordered the pet only on
                // a turn it took itself.) Never left on an enemy that has since been mezzed.
                // A healer's pet (a Druid's) keeps an enemy healer or caster from casting
                // (PvpInterruptWeights.HealersPetsInterrupt): it had no orders and defended its owner.
                bool castersPet = PvpMember.ClassOf(bot) is eCharacterClass.Cabalist or eCharacterClass.Enchanter or eCharacterClass.Spiritmaster;
                bool healersPet = !castersPet && role == PvpRole.Heal && PvpWeights.Of(bot).Interrupt.HealersPetsInterrupt;
                if (FightStarted && bot.IsAlive && (castersPet || healersPet) &&
                    bot.ControlledBrain is ControlledMobBrain { Body: { IsAlive: true } } pet)
                {
                    // Its brain runs with no player near (AutonomousSummonActivity.IsPvpBotSummon); started here if it is not.
                    if (!pet.ServiceObjectId.IsSet)
                        pet.Start();
                    GameLiving has = pet.OrderedAttackTarget is { IsAlive: true, IsMezzed: false } keeps ? keeps : null;
                    GameLiving petAim = healersPet
                        ? HealersPetAim(bot, pet, alive, target, duty, petsOn, has)
                        : PvpCalls.PetAim(
                            petsDuty is { IsAlive: true, IsMezzed: false } ? petsDuty : null,
                            mine is { IsAlive: true } ? mine : null,
                            has,
                            IsCalledAssist(mine));
                    if (healersPet && petAim != null)
                        petsOn.Add(petAim);
                    if (petAim == null)
                    {
                        if (pet.OrderedAttackTarget is { IsMezzed: true })
                            pet.Disengage();
                    }
                    else if (pet.OrderedAttackTarget != petAim)
                    {
                        string was = pet.OrderedAttackTarget?.Name;
                        pet.Attack(petAim);
                        // With what the pet was doing, to see why an order does not hold.
                        PvpRecorder.Decision(bot, new
                        {
                            kind = "PetOn", d = petAim.Name, score = 0, was, took = pet.OrderedAttackTarget == petAim,
                            fromOwner = (int)pet.Body.GetDistanceTo(bot), toAim = (int)pet.Body.GetDistanceTo(petAim),
                            stance = pet.AggressionState.ToString(), state = pet.FSM?.GetCurrentState()?.StateType.ToString(),
                            attacking = pet.Body.attackComponent?.AttackState == true, petTarget = (pet.Body.TargetObject as GameLiving)?.Name,
                        }, [], null);
                    }
                }
                brain.PvpAssignedTarget = aim;
                brain.PvpStaysPut = aim == null && bot.IsAlive && (Holding.Contains(bot) || FallingBack.Contains(bot));
                if (bot.IsAlive)
                    brain.PvpChooseHands();
                // A fighter on the kill: its target is recorded when it changes (guards and
                // interrupters record their own choices).
                if (FightStarted && bot.IsAlive && role == PvpRole.Assist && onDuty == null &&
                    PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee &&
                    aim != null && _lastAim.GetValueOrDefault(bot) != aim)
                {
                    _lastAim[bot] = aim;
                    PvpRecorder.Decision(bot, new { kind = "Attack", d = aim.Name, score = 0 }, [], null);
                }
                // A member still far behind (it stopped to cast while the group walked on) is not
                // put in attack state: the brain does not walk a caster that far, so it would stand
                // where it is for the whole fight. It keeps marching until it has caught up.
                // While the group holds, its fighters do not go for anyone.
                if (aim != null && bot.IsAlive && bot.IsWithinRadius(aim, EngageRange) && !(Order == PvpOrder.Hold && IsFighter(bot)))
                {
                    brain.AddToAggroList(aim, 1);
                    if (brain.FSM.GetCurrentState()?.StateType != eFSMStateType.AGGRO)
                        brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                }
            }
            return target;
        }

        /// <summary>
        /// The enemy a healer's pet is sent at (PvpInterruptDuty.PetOfAHealer): a healer or caster that is
        /// neither the kill target nor asleep nor being put to sleep, within reach of the pet.
        /// </summary>
        private static GameLiving HealersPetAim(GameBot owner, ControlledMobBrain pet, List<GameLiving> enemies, GameLiving kill,
            Dictionary<GameBot, GameLiving> duty, HashSet<GameLiving> petsOn, GameLiving has)
        {
            List<GameLiving> open = enemies.Where(enemy => enemy.IsAlive && enemy != kill && !enemy.IsMezzed &&
                !BotPvpCrowdControl.Claimed(owner.Realm, enemy) && PvpMember.ArchetypeOf(enemy) != PvpBuffArchetype.Melee).ToList();
            int chosen = PvpInterruptDuty.PetOfAHealer(open.Select((enemy, e) =>
                (new PvpDutyPair(0, e, PvpMember.ArchetypeOf(enemy), enemy.IsCasting, !enemy.IsBeingInterrupted, enemy == has,
                    (int)pet.Body.GetDistanceTo(enemy), PvpActionChoice.IsController(PvpMember.ClassOf(enemy), PvpWeights.Of(owner))),
                 duty.ContainsValue(enemy) || petsOn.Contains(enemy))), PvpWeights.Of(owner).Interrupt);
            return chosen < 0 ? null : open[chosen];
        }

        /// <summary>
        /// Pairs the team's interrupters with the enemy healers and casters that are neither the
        /// kill target (the damage dealers interrupt that one) nor asleep (bots/pvp/PvpInterruptDuty.cs).
        /// </summary>
        private Dictionary<GameBot, GameLiving> InterruptDuty(List<GameBot> team, List<GameLiving> enemies, GameLiving kill)
        {
            PvpInterruptWeights weights = PvpWeights.Of(team.FirstOrDefault()).Interrupt;
            List<GameBot> interrupters = team.Where(bot => bot.IsAlive && bot.Brain is BotBrain &&
                PvpInterruptDuty.MeansOf(PvpMember.ClassOf(bot), weights) != PvpInterruptMeans.None).ToList();
            // Not one a mezzer of this team is putting to sleep right now either: the interrupter's
            // first hit would wake it and leave it immune.
            eRealm realm = team.Count > 0 ? team[0].Realm : eRealm.None;
            List<GameLiving> open = enemies.Where(enemy => enemy != kill && !enemy.IsMezzed &&
                !BotPvpCrowdControl.Claimed(realm, enemy) &&
                PvpMember.ArchetypeOf(enemy) != PvpBuffArchetype.Melee).ToList();
            var pairs = new List<PvpDutyPair>();
            for (int i = 0; i < interrupters.Count; i++)
            {
                Duties.TryGetValue(interrupters[i], out GameLiving current);
                for (int e = 0; e < open.Count; e++)
                    pairs.Add(new PvpDutyPair(i, e, PvpMember.ArchetypeOf(open[e]), open[e].IsCasting,
                        !open[e].IsBeingInterrupted, open[e] == current, (int)interrupters[i].GetDistanceTo(open[e]),
                        PvpActionChoice.IsController(PvpMember.ClassOf(open[e]), PvpWeights.Of(interrupters[i]))));
            }
            Dictionary<int, int> assigned = PvpInterruptDuty.Assign(pairs, weights);
            var duty = new Dictionary<GameBot, GameLiving>();
            for (int i = 0; i < interrupters.Count; i++)
            {
                GameBot bot = interrupters[i];
                Duties.TryGetValue(bot, out GameLiving before);
                if (!assigned.TryGetValue(i, out int e))
                {
                    Duties.Remove(bot);
                    continue;
                }
                duty[bot] = Duties[bot] = open[e];
                // Recorded when it changes, not every second.
                if (open[e] != before && FightStarted)
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
        private GameLiving Peel(GameBot bot, BotBrain brain, List<GameBot> team, List<GameLiving> enemies, GameLiving kill, HashSet<GameLiving> guarded)
        {
            PvpPeelWeights weights = PvpWeights.Of(bot).Peel;
            Holding.Remove(bot);
            // The group's healers and casters, a player among them: any of them may be the one it guards.
            List<GameLiving> backline = _allies.Where(member => member.IsAlive && member != bot &&
                PvpMember.ArchetypeOf(member) != PvpBuffArchetype.Melee).ToList();
            Wards.TryGetValue(bot, out GameLiving current);
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

            bool InMeleeOn(GameLiving enemy, GameLiving member) =>
                !enemy.IsMezzed && enemy.TargetObject == member && enemy.IsWithinRadius(member, weights.MeleeReach) &&
                (PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee || enemy.attackComponent?.AttackState == true);

            bool ComingFor(GameLiving enemy, GameLiving member) =>
                weights.InterceptWithin > 0 && !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject == member &&
                PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee &&
                enemy.IsWithinRadius(member, weights.InterceptWithin) && !enemy.IsWithinRadius(member, weights.MeleeReach);

            List<PvpWardView> wards = backline.Select((member, id) => new PvpWardView(id, PvpMember.ArchetypeOf(member),
                enemies.Count(enemy => InMeleeOn(enemy, member)), member.HealthPercent, (int)bot.GetDistanceTo(member),
                member == current, guarded.Contains(member))).ToList();
            GameLiving ward = backline[PvpPeel.ChooseWard(wards, weights)];
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

            // The kill target is looked at however far it is: it is where the shield fighter goes with nobody to peel.
            List<GameLiving> near = enemies.Where(enemy => enemy.IsWithinRadius(ward, weights.ConsiderRange) || InMeleeOn(enemy, bot) || enemy == kill).ToList();
            List<PvpPeelView> views = near.Select((enemy, id) => new PvpPeelView(id, InMeleeOn(enemy, ward),
                backline.Any(member => member != ward && InMeleeOn(enemy, member)), InMeleeOn(enemy, bot), enemy == kill,
                !(enemy.IsStunned || PvpNotice.ThinksImmune(bot, enemy, eEffect.StunImmunity)),
                enemy.IsMezzed, (int)ward.GetDistanceTo(enemy), enemy == brain.PvpAssignedTarget,
                backline.Any(member => ComingFor(enemy, member)), enemy.IsStunned)).ToList();
            int chosen = PvpPeel.ChooseTarget(views, weights, out double score);
            GameLiving aim = chosen >= 0 ? near[chosen] : null;
            string kind = chosen >= 0 ? PvpPeel.Describe(views[chosen]) : "Hold";

            // Recorded when it changes, not every second.
            if (FightStarted && (!LastPeel.TryGetValue(bot, out var last) || last.Kind != kind || last.Target != aim))
            {
                LastPeel[bot] = (kind, aim);
                // Enemies that are on nobody and are not the kill target have no score and are left out.
                PvpRecorder.Decision(bot, new { kind, d = aim?.Name, ward = ward.Name, score = score > double.MinValue ? Math.Round(score, 1) : 0 },
                    views.Where(view => PvpPeel.Score(view, weights) > double.MinValue)
                        .OrderByDescending(view => PvpPeel.Score(view, weights)).Take(4).Select(view => (object)new
                    {
                        kind = PvpPeel.Describe(view), d = near[view.Id].Name, score = Math.Round(PvpPeel.Score(view, weights), 1),
                        view.OnWard, view.OnBackline, view.OnSelf, view.IsKill, view.FromWard, view.Coming,
                    }), null);
            }

            // Staying beside the ward. Before contact the group's formation places everybody.
            if (aim == null && FightStarted && enemies.Any(enemy => bot.IsWithinRadius(enemy, EngageRange)))
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

        /// <summary>How far a member is from the nearest living healer of its group; 0 when there is none.</summary>
        private static float FromNearestHealer(GameBot bot, List<GameBot> team)
        {
            float nearest = float.MaxValue;
            foreach (GameBot member in team)
            {
                if (member != bot && member.IsAlive && PvpMember.RoleOf(member) == PvpRole.Heal)
                    nearest = Math.Min(nearest, (float)bot.GetDistanceTo(member));
            }
            return nearest == float.MaxValue ? 0 : nearest;
        }

        // The kill target: when it last lost ground (or was called), and the targets given up alive and when.
        private long _killProgressTick;
        private readonly Dictionary<GameLiving, long> _givenUp = new();

        // Members announced to a watcher as out of power, until they are back above 30%.
        private readonly HashSet<GameBot> OutOfPower = new();

        // Enemy pets: the member the group has gathered on, who came, and when each last walked.
        private GameBot _gatherOn;
        private readonly HashSet<GameBot> Gathering = new();
        private readonly Dictionary<GameBot, long> LastGatherStep = new();

        /// <summary>
        /// Once a second while no fight is on: who of the group sprints on the road, and what it costs them
        /// (<see cref="PvpPositionWeights.SprintsOnTheRoad"/>). Nobody, in a group of bots alone; behind a player who
        /// sprints, whoever gets back from a song or a buff what the sprint takes.
        /// </summary>
        public void SprintOnTheRoad()
        {
            if (FightStarted || Leaving || RetreatsTo != null)
                return;
            bool playerSprints = _allies.OfType<GamePlayer>().Any(player => player.IsAlive && player.IsSprinting);
            foreach (GameBot bot in _members)
            {
                PvpPositionWeights legs = PvpWeights.Of(bot).Position;
                int cost = PvpPositionWeights.SprintCostWith(legs.SprintCostPerSecond,
                    bot.GetAbility<DOL.GS.RealmAbilities.AtlasOF_LongWindAbility>() is { } longWind ? longWind.GetAmountForLevel(longWind.Level) : 0);
                bool sprints = bot.IsAlive && legs.SprintsOnTheRoad(playerSprints, bot.GetModified(eProperty.EnduranceRegenerationAmount), cost,
                    bot.EndurancePercent, bot.PvpSprinting);
                if (sprints != bot.PvpSprinting)
                {
                    bot.PvpSprinting = sprints;
                    bot.OnMaxSpeedChange();   // it is on its way already: at its new speed from here
                }
                if (sprints && bot.IsMoving && cost > 0)
                    bot.Endurance = Math.Max(0, bot.Endurance - cost);
            }
        }

        private void EndGather()
        {
            _gatherOn = null;
            Gathering.Clear();
        }

        /// <summary>
        /// What the group does about enemy pets (bots/pvp/PvpPetClear.cs): its healers and casters
        /// with a pet on them walk to the member that has a point-blank area spell, and that
        /// member casts it once enough pets are around it. Those gathered are left out of the
        /// spacing rules until no pet is left on the group's healers and casters.
        /// </summary>
        public void ClearPets()
        {
            if (RetreatsTo != null || Leaving)
                return;
            PvpPetWeights weights = PvpWeights.Of(_members.FirstOrDefault()).Pets;
            if (!FightStarted || weights.GatherAtPets <= 0 && weights.BlastAtPets <= 0 || Kiting)
            {
                EndGather();
                return;
            }
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            // Every member with such a spell casts it; the others gather on the first of them (the one
            // they gathered on already, while it can still cast).
            List<GameBot> casters = alive.Where(bot => !bot.IsMezzed && !bot.IsStunned && (bot.Brain as BotBrain)?.PvpPointBlank() != null)
                .OrderByDescending(bot => bot == _gatherOn).ThenByDescending(bot => ((BotBrain)bot.Brain).PvpPointBlank().Level).ThenBy(bot => bot.Name).ToList();
            if (casters.Count == 0)
            {
                EndGather();
                return;
            }
            GameBot caster = casters[0];
            Spell spell = ((BotBrain)caster.Brain).PvpPointBlank();
            HashSet<GameLiving> foes = Known.ToHashSet();
            List<GameNPC> pets = caster.GetNPCsInRadius((ushort)weights.ConsiderRange)
                .Where(npc => npc.IsAlive && npc.Brain is IControlledBrain owned && owned.GetLivingOwner() is { } owner && foes.Contains(owner)).ToList();

            // The spell, whether or not anybody has gathered: enough pets around a caster is enough.
            foreach (GameBot each in casters)
            {
                var eachBrain = (BotBrain)each.Brain;
                int radius = eachBrain.PvpPointBlank().Radius;
                int inside = pets.Count(pet => pet.IsWithinRadius(each, radius));
                bool wouldWake = foes.Any(enemy => enemy.IsAlive && enemy.IsMezzed && enemy.IsWithinRadius(each, radius));
                if (PvpPetClear.Blasts(inside, wouldWake, weights))
                    eachBrain.PvpBlast(inside);
            }

            List<GameBot> backline = alive.Where(bot => !IsFighter(bot)).ToList();
            int onBackline = pets.Count(pet => pet.TargetObject is GameLiving victim && _allies.Contains(victim) && !IsFighter(victim));
            // Nobody gathers on a caster that has a free enemy fighter beside it: in 16 matches 22% of the
            // walks to gather went to one, an Enchanter joining an Eldritch that an Armsman was on.
            bool FighterBeside(GameBot bot) => foes.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned && IsFighter(enemy) &&
                enemy.IsWithinRadius(bot, weights.NoGatherWithFighterWithin));
            caster = casters.FirstOrDefault(each => !FighterBeside(each));
            if (caster == null || !(_gatherOn != null ? PvpPetClear.StaysGathered(onBackline) : PvpPetClear.Gathers(onBackline, weights)))
            {
                EndGather();
                return;
            }
            spell = ((BotBrain)caster.Brain).PvpPointBlank();
            if (_gatherOn != caster)
            {
                Gathering.Clear();
                _gatherOn = caster;
                Gathering.Add(caster);
                PvpRecorder.Decision(caster, new { kind = "GatherOn", spell = spell.Name, score = onBackline }, [], null);
                PvpObserver.Say(caster, $"{PvpObserver.Realm(caster)} gathers on {PvpObserver.Name(caster)}: {onBackline} pets on its healers and casters");
                Call(caster, "gather", PvpCalls.Gather(onBackline));
            }
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            var centre = new Vector2(caster.X, caster.Y);
            foreach (GameBot bot in backline)
            {
                if (bot == caster || bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true ||
                    LastGatherStep.TryGetValue(bot, out long last) && now - last < 2_000)
                    continue;
                // One with an enemy fighter on it is left to its own running (StandOff): the two orders undid each other.
                if (foes.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && enemy.TargetObject == bot && IsFighter(enemy) && enemy.IsWithinRadius(bot, 600)))
                    continue;
                int onIt = pets.Count(pet => pet.TargetObject == bot);
                int away = (int)bot.GetDistanceTo(caster);
                // Another member with such a spell comes whether or not a pet is on it: two spells on the same pets.
                if (!PvpPetClear.Comes(casters.Contains(bot) ? Math.Max(1, onIt) : onIt, away, weights))
                    continue;
                // Beside the caster, on the side it comes from: not on top of it.
                Vector2 from = new Vector2(bot.X, bot.Y) - centre;
                Vector2 to = centre + (from.LengthSquared() > 1 ? Vector2.Normalize(from) : Vector2.UnitX) * (weights.GatherWithin * 0.6f);
                if (nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters) is not { } floor || floor.Z >= 19_999)
                    continue;
                LastGatherStep[bot] = now;
                Gathering.Add(bot);
                brain.PvpMoveFrom = now;
                brain.PvpMoveUntil = now + Math.Clamp((long)(away / (double)Math.Max(100, (int)bot.MaxSpeed) * 1000) + 300, 800, 6_000);
                if (bot.attackComponent.AttackState)
                    bot.StopAttack();
                bot.StopFollowing();
                bot.PathTo(floor, bot.MaxSpeed);
                PvpRecorder.Decision(bot, new { kind = "Gather", d = caster.Name, score = onIt, away }, [], null);
            }
        }

        /// <summary>
        /// Healers and casters keep apart once the enemy is in sight (bots/pvp/PvpSpacing.cs). A
        /// member standing within mez radius of another takes a step away, on a turn it is not
        /// using: one that is casting, walking or unable to act is left alone, and its brain can
        /// stop the step at any moment to cast. Fighters are left out: they stack on the kill
        /// target, and a shield fighter belongs beside its ward.
        /// </summary>
        public void KeepApart()
        {
            if (RetreatsTo != null || Leaving)
                return;
            PvpSpreadWeights weights = PvpWeights.Of(_members.FirstOrDefault()).Spread;
            List<GameLiving> foes = Known.Where(enemy => enemy.IsAlive).ToList();
            if (weights.MinSpacing <= 0 || foes.Count == 0 || Order == PvpOrder.Hold || MakesLastStand() || MopsUp || Kiting)
                return;
            List<GameBot> backline = _members.Where(bot => bot.IsAlive &&
                PvpMember.ArchetypeOf(bot) != PvpBuffArchetype.Melee).ToList();
            var enemyCentre = new Vector2((float)foes.Average(enemy => enemy.X), (float)foes.Average(enemy => enemy.Y));
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            foreach (GameBot bot in backline)
            {
                // Gathered against enemy pets (ClearPets): standing together is the point.
                if (Gathering.Contains(bot))
                    continue;
                // A healer with a badly hurt groupmate in reach heals; it does not step aside to keep apart.
                if (bot.Brain is BotBrain healing && healing.PvpBreaksOffToHeal())
                    continue;
                if (bot.IsCasting || bot.IsMoving || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true ||
                    !foes.Any(enemy => bot.IsWithinRadius(enemy, SpotRange)) ||
                    LastSpreadStep.TryGetValue(bot, out long last) && now - last < 2_000 ||
                    LastStandStep.TryGetValue(bot, out long stood) && now - stood < 1_500)
                    continue;
                List<Vector2> mates = backline.Where(member => member != bot).Select(member => new Vector2(member.X, member.Y)).ToList();
                // A healer also keeps well away from the group's other healers (PvpSpreadWeights.HealerSpacing).
                List<Vector2> otherHealers = weights.HealerSpacing <= 0 || PvpMember.RoleOf(bot) != PvpRole.Heal ? []
                    : backline.Where(member => member != bot && PvpMember.RoleOf(member) == PvpRole.Heal)
                        .Select(member => new Vector2(member.X, member.Y)).ToList();
                var self = new Vector2(bot.X, bot.Y);
                if ((PvpSpacing.StepApart(self, mates, enemyCentre, weights.MinSpacing, weights.Step) ??
                     PvpSpacing.StepApart(self, otherHealers, enemyCentre, weights.HealerSpacing, weights.Step)) is not { } to)
                    continue;
                LastSpreadStep[bot] = now;
                bot.PathTo(nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters)
                    is { } floor && floor.Z < 19_999 ? floor : new Vector3(to.X, to.Y, bot.Z), bot.MaxSpeed);
                PvpRecorder.Decision(bot, new { kind = "Spread", score = 0 }, [], null);
            }
        }

        /// <summary>
        /// Healers and casters keep away from enemy fighters (bots/pvp/PvpPositioning.cs): a
        /// member with a fighter nearer than it wants takes a step to a better place, staying in
        /// range of its work. One in melee with a fighter that can follow stays: running gains
        /// nothing until the fighter is mezzed, stunned, rooted or slowed, and then it does run.
        /// The group's order scales the distance wanted. The step is the bot's turn: its brain
        /// casts again when it has arrived.
        /// </summary>
        private bool _lastStandSaid;

        /// <summary>True while the group has won and only has to finish (PvpPositionWeights.MopsUp); set by <see cref="Decide"/>.</summary>
        public bool MopsUp { get; private set; }

        /// <summary>True once the group has lost (PvpPositionWeights.LastStand): the few left stand and fight.</summary>
        public bool MakesLastStand()
        {
            if (!FightStarted)
                return false;
            List<GameLiving> left = _allies.Where(member => member.IsAlive).ToList();
            bool stands = PvpWeights.Of(_members.FirstOrDefault()).Position.LastStand(left.Count, Known.Count(enemy => enemy.IsAlive));
            if (stands && !_lastStandSaid && left.OfType<GameBot>().FirstOrDefault() is { } bot)
                PvpRecorder.Decision(bot, new { kind = "LastStand", score = left.Count }, [], null);
            _lastStandSaid = stands;
            return stands;
        }

        public void StandOff()
        {
            if (RetreatsTo != null || Leaving)
                return;
            // The group falls back as one: nobody takes a step of its own.
            if (Kite())
                return;
            PvpWeights all = PvpWeights.Of(_members.FirstOrDefault());
            PvpPositionWeights weights = all.Position;
            if (weights.SafeDistance <= 0 || !FightStarted || MakesLastStand() || MopsUp)
                return;
            List<GameLiving> fighters = Known.Where(enemy => enemy.IsAlive && IsFighter(enemy)).ToList();
            // The enemy's healers and casters that are awake: kept at the edge of range (PvpPositionWeights.EdgeDistance).
            List<Vector2> others = weights.EdgeDistance <= 0 ? []
                : Known.Where(enemy => enemy.IsAlive && !IsFighter(enemy) && !enemy.IsMezzed).Select(enemy => new Vector2(enemy.X, enemy.Y)).ToList();
            if (fighters.Count == 0 && others.Count == 0)
                return;
            double byOrder = all.Order.SafeFactor(Order, (GameLoop.GameLoopTime - _orderSince) / 1000.0);
            int safe = (int)(weights.SafeDistance * byOrder);
            int edge = (int)(weights.EdgeDistance * byOrder);
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            List<GameBot> backline = alive.Where(bot => !IsFighter(bot)).ToList();
            List<GameBot> healers = backline.Where(bot => PvpMember.RoleOf(bot) == PvpRole.Heal).ToList();
            static Vector2 At(GameObject thing) => new(thing.X, thing.Y);
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            // Under Extend they fall back the same way: from the middle of the enemy through the middle of themselves.
            Vector2 retreat = default;
            List<GameLiving> enemiesAlive = Known.Where(enemy => enemy.IsAlive).ToList();
            if (Order == PvpOrder.Extend && backline.Count > 0 && enemiesAlive.Count > 0)
            {
                Vector2 away = new Vector2((float)backline.Average(bot => bot.X), (float)backline.Average(bot => bot.Y)) -
                               new Vector2((float)enemiesAlive.Average(enemy => enemy.X), (float)enemiesAlive.Average(enemy => enemy.Y));
                if (away.LengthSquared() > 1)
                    retreat = Vector2.Normalize(away);
            }
            bool calledKite = _calledOrder == PvpOrder.Extend && now < _calledUntil;
            foreach (GameBot bot in backline)
            {
                if (bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true)
                    continue;
                List<PvpThreat> threats = fighters.Select(enemy => new PvpThreat(At(enemy), enemy.IsMezzed || enemy.IsStunned, IsSlowed(enemy), enemy.TargetObject == bot)).ToList();
                bool hunted = threats.Any(threat => threat.OnMe);
                if (hunted)
                    _huntedTick[bot] = now;
                // The player called the kite: the back line goes back, away from the enemy, whatever else it would weigh.
                if (calledKite && retreat != default)
                {
                    var here = At(bot);
                    float nearest = enemiesAlive.Min(enemy => Vector2.Distance(here, At(enemy)));
                    if (PvpCalls.KiteStepWanted(nearest, LastStandStep.TryGetValue(bot, out long lastKite) ? now - lastKite : long.MaxValue))
                    {
                        Vector2 to = here + retreat * PvpCalls.CalledKiteStep;
                        if (nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters) is { } ground && ground.Z < 19_999)
                        {
                            LastStandStep[bot] = now;
                            brain.PvpMoveFrom = now;
                            brain.PvpMoveUntil = now + Math.Clamp((long)(PvpCalls.CalledKiteStep / (float)Math.Max(100, (int)bot.MaxSpeed) * 1000) + 300, 800, 4_500);
                            StandStepEnds[bot] = brain.PvpMoveUntil;
                            if (bot.attackComponent.AttackState)
                                bot.StopAttack();
                            bot.StopFollowing();
                            bot.PathTo(ground, bot.MaxSpeed);
                            PvpRecorder.Decision(bot, new { kind = "CalledKite", order = Order.ToString(), score = (int)nearest }, [], null);
                        }
                    }
                    continue;
                }
                // Gathered against enemy pets (ClearPets): it stays, unless an enemy fighter is on it.
                if (Gathering.Contains(bot) && !hunted)
                    continue;
                // After a step it works from its new place for a while; one running from held fighters does not wait.
                // (The wait counts from its arrival: PvpPositionWeights.StepWaitCountsFromArrival.)
                var self = At(bot);
                // A healer that its group has left behind follows it, and does not wait between steps either.
                bool leftBehind = healers.Contains(bot) &&
                    PvpPositioning.LeftBehind(self, _allies.Where(member => member.IsAlive && member != bot).Select(member => At(member)).ToList(), weights);
                bool kiting = hunted && weights.KitesWhenPinned && PvpPositioning.Pinned(self, threats, weights);
                // Kiting, the next step is taken when this one ends, not in the middle of it.
                if (LastStandStep.TryGetValue(bot, out long last) && (kiting
                        ? now - last < weights.KiteStepEveryMilliseconds || StandStepEnds.TryGetValue(bot, out long walking) && now < walking - 300
                        : hunted || leftBehind ? now - last < 1_500
                        : now - (weights.StepWaitCountsFromArrival && StandStepEnds.TryGetValue(bot, out long ends) ? ends : last) < weights.StepEveryMilliseconds))
                    continue;
                bool fighterNear = threats.Any(threat => Vector2.Distance(self, threat.At) < safe);
                // On its way in to cast: only an enemy fighter beside it turns it round.
                if (!hunted && !fighterNear && weights.ApproachLeftAloneMilliseconds > 0 && brain.PvpApproachTick > 0 &&
                    now - brain.PvpApproachTick < weights.ApproachLeftAloneMilliseconds)
                    continue;
                bool isHealer = healers.Contains(bot);
                // A healer with somebody to heal stays at its work, unless it is the one being attacked.
                if (isHealer && !hunted && alive.Any(member => member != bot && member.HealthPercent < weights.HealerStaysIfMemberBelow &&
                        bot.IsWithinRadius(member, weights.HealRange + 300)))
                    continue;
                // One that can wake a sleeping groupmate goes and does it, unless it is being kept from casting.
                // Recorded in the night of 2026-11-19: a Bard walking to a sleeping Druid (cure mez, score 113)
                // was sent the other way by this step and by the next four, twelve seconds in all, and a
                // Pacification Healer stepped away for ten seconds with both other healers asleep. Of 47
                // healers left asleep for more than eight seconds, nine had a curer free like that.
                if (weights.CurerStaysForSleeper && !bot.IsBeingInterrupted && alive.Any(member => member != bot && member.IsMezzed) &&
                    PvpActionChoice.KindsFor(PvpMember.ClassOf(bot), all).Contains(PvpActionKind.CureMez))
                    continue;
                var work = new List<PvpWorkPoint>();
                // A caster with no enemy in reach closes up to the edge of its range; not while the group
                // falls back or regroups, and not a nearsighted one, whose reach is no reason to walk in.
                int closeUp = 0;
                if (isHealer)
                    work.AddRange(_allies.Where(member => member.IsAlive && member != bot).Select(member => new PvpWorkPoint(At(member), weights.HealRange, member == Trained ? 2 : 0.5)));
                else
                {
                    // What it is working on: the enemy of its last choice, its duty and the kill target, at
                    // the range it really has (a nearsighted caster's is cut, and stepping out of it
                    // only makes it walk back in).
                    bool recent = now - brain.PvpWorkTick < 6_000 && brain.PvpWorkRange > 0;
                    int reach = recent ? Math.Min(weights.WorkRange, Math.Max(200, brain.PvpWorkRange - 100)) : weights.WorkRange;
                    GameLiving own = recent ? brain.PvpWorkTarget : null;
                    foreach (GameLiving aim in new[] { own, brain.PvpDutyTarget, brain.PvpKillTarget }
                                 .Where(aim => aim is { IsAlive: true }).Distinct())
                        work.Add(new PvpWorkPoint(At(aim), reach,
                            aim == own ? 1 : aim == brain.PvpDutyTarget ? weights.DutyTargetRangeWeight : weights.KillTargetRangeWeight));
                    // Everybody stays where its healers reach it.
                    work.AddRange(_allies.Where(member => member.IsAlive && PvpMember.RoleOf(member) == PvpRole.Heal).Select(healer => new PvpWorkPoint(At(healer), weights.HealRange, 0.5)));
                    if (weights.PerUnitOutOfReach > 0 && Order is not (PvpOrder.Extend or PvpOrder.Regroup) &&
                        !bot.effectListComponent.ContainsEffectForEffectType(eEffect.Nearsight))
                        closeUp = weights.WorkRange;
                }
                List<Vector2> mates = backline.Where(member => member != bot).Select(member => At(member)).ToList();
                List<Vector2> otherHealers = isHealer && all.Spread.HealerSpacing > 0
                    ? healers.Where(member => member != bot).Select(member => At(member)).ToList() : null;
                // Kiting, it goes back: away from the middle of the enemy through the middle of its own back line, whatever the order.
                Vector2 back = retreat;
                if (kiting && back == default && enemiesAlive.Count > 0)
                {
                    Vector2 away = new Vector2((float)backline.Average(member => member.X), (float)backline.Average(member => member.Y)) -
                                   new Vector2((float)enemiesAlive.Average(enemy => enemy.X), (float)enemiesAlive.Average(enemy => enemy.Y));
                    if (away.LengthSquared() > 1)
                        back = Vector2.Normalize(away);
                }
                List<Vector2> steps = PvpPositioning.Steps(self, threats, work, mates, all.Spread.MinSpacing, safe, weights, out _, back, others, edge,
                    closeUp, otherHealers, all.Spread.HealerSpacing, leftBehind);
                bool closing = !fighterNear && PvpPositioning.OutOfReach(self, threats, others, closeUp);
                foreach (Vector2 to in steps.Take(4))
                {
                    // A place the navmesh knows; the next best otherwise.
                    if (nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters) is not { } floor || floor.Z >= 19_999)
                        continue;
                    LastStandStep[bot] = now;
                    brain.PvpMoveFrom = now;
                    // The walk is the bot's turn for as long as it takes.
                    brain.PvpMoveUntil = now + Math.Clamp((long)(Vector2.Distance(self, to) / Math.Max(100, (int)bot.MaxSpeed) * 1000) + 300, 800, 4_500);
                    StandStepEnds[bot] = brain.PvpMoveUntil;
                    // Hitting a pet with its staff: the follow that goes with an attack would stop the walk.
                    if (bot.attackComponent.AttackState)
                        bot.StopAttack();
                    bot.StopFollowing();
                    bot.PathTo(floor, bot.MaxSpeed);
                    PvpRecorder.Decision(bot, new
                    {
                        kind = kiting ? "Kite" : hunted ? "GetAway" : fighterNear ? "StandOff" : closing ? "CloseUp" : leftBehind ? "Follow" : "KeepRange", order = Order.ToString(),
                        score = (int)PvpPositioning.NearestEnemy(self, threats, others),
                    }, [], null);
                    break;
                }
            }
        }
    }
}
