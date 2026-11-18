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
        private readonly Dictionary<GameBot, GameBot> Wards = new();
        private readonly Dictionary<GameBot, (string Kind, GameBot Target)> LastPeel = new();
        private readonly HashSet<GameBot> Holding = new();
        // Interrupt duty: the enemy each interrupter has, kept so that the pairs hold from one second to the next.
        private readonly Dictionary<GameBot, GameBot> Duties = new();
        // When each healer or caster last took a step away from a groupmate.
        private readonly Dictionary<GameBot, long> LastSpreadStep = new();

        public PvpSquad(IEnumerable<GameBot> members)
        {
            _members = members.ToList();
        }

        public IReadOnlyList<GameBot> Members => _members;

        /// <summary>The enemy the group is killing now; null when it has none.</summary>
        public GameBot KillTarget { get; private set; }

        /// <summary>Set once the first blow has fallen. Before it the group travels in formation and nothing is recorded as a fight decision.</summary>
        public bool FightStarted { get; set; }

        /// <summary>The healer or caster this shield fighter guards; null for anyone else.</summary>
        public GameBot WardOf(GameBot bot) => Wards.GetValueOrDefault(bot);

        /// <summary>True for a shield fighter that is staying beside its ward: it has chosen not to attack.</summary>
        public bool IsHolding(GameBot bot) => Holding.Contains(bot) || FallingBack.Contains(bot);

        // Fighters on their way back to the group's healers and casters under the Extend order.
        private readonly HashSet<GameBot> FallingBack = new();

        /// <summary>True for an enemy fighter that is in melee on one of the group's healers or casters.</summary>
        public bool IsPursuer(GameBot enemy) => _pursuers.Contains(enemy);

        // Heals being cast right now: healer, patient, spell, when it began and when it is surely over.
        private readonly Dictionary<GameBot, (GameBot Target, Spell Spell, long Started, long Expires)> _heals = new();

        /// <summary>
        /// Whom a healer's heal goes to, given the heals the group's other healers are already
        /// casting (bots/pvp/PvpHealPlan.cs): its own pick, a needier member, or null when the
        /// others have everyone covered and the turn is free. Called from brain threads.
        /// </summary>
        public GameLiving AssignHeal(GameBot healer, Spell spell, GameLiving own)
        {
            // Only a single heal with a cast time is shared out: an instant lands at once, and a
            // group heal or a heal over time is not for one patient.
            if (own is not GameBot pick || !spell.IsHealing || spell.Target != eSpellTarget.REALM || spell.IsInstantCast || spell.Duration > 0)
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
                List<GameBot> reach = _members.Where(member => member.IsAlive && (member == healer || healer.IsWithinRadius(member, range))).ToList();
                // Its own pick is out of reach: it walks there, as before.
                if (!reach.Contains(pick))
                    return own;
                List<PvpPatient> patients = reach.Select((member, id) => new PvpPatient(id, member.Health, member.MaxHealth,
                    (int)_heals.Values.Where(heal => heal.Target == member).Sum(heal => Math.Max(0, GameBot.HealAmount(heal.Spell, member))),
                    PvpBuffPlan.ArchetypeOf(member.PvpBuild.Class), member == Trained)).ToList();
                int chosen = PvpHealPlan.Choose(patients, reach.IndexOf(pick), weights, healer.ManaPercent);
                if (chosen < 0)
                    return null;
                _heals[healer] = (reach[chosen], spell, now, now + Math.Clamp(spell.CastTime + 500, 1_000, 15_000));
                return reach[chosen];
            }
        }

        /// <summary>An enemy is seen from this far, with open ground between (the range at which a group spots another).</summary>
        public const int ViewRange = SpotRange;
        /// <summary>An enemy that drops out of sight is still known for this long, where it was last seen.</summary>
        public const int MemoryMilliseconds = 10_000;

        // When each enemy was last seen by a member of the group.
        private readonly Dictionary<GameBot, long> _lastSeen = new();
        // The enemy each fighter on the kill was last recorded attacking.
        private readonly Dictionary<GameBot, GameBot> _lastAim = new();

        /// <summary>
        /// The enemies the group knows about (M5.2): those a living member sees now, within
        /// <see cref="ViewRange"/> and with open ground between, and those seen in the last ten
        /// seconds. Everything the group decides, and every choice its members score, is about
        /// these and no others. Stealth is not handled: the main groups have no stealther.
        /// </summary>
        public IReadOnlyList<GameBot> Known { get; private set; } = [];

        private List<GameBot> Look(IReadOnlyList<GameBot> enemies)
        {
            var nav = PathfindingProvider.Instance;
            long now = GameLoop.GameLoopTime;
            List<GameBot> watchers = _members.Where(member => member.IsAlive).ToList();
            var known = new List<GameBot>();
            foreach (GameBot enemy in enemies)
            {
                if (!enemy.IsAlive || enemy.ObjectState != GameObject.eObjectState.Active)
                    continue;
                var there = new Vector3(enemy.X, enemy.Y, enemy.Z);
                // Nearest first: the first member that sees it is enough.
                bool seen = watchers.Where(member => member.IsWithinRadius(enemy, ViewRange))
                    .OrderBy(member => member.GetDistanceTo(enemy))
                    .Any(member => nav.HasLineOfSight(member.CurrentZone, new Vector3(member.X, member.Y, member.Z), there, nav.DefaultFilters));
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
        public GameBot Trained { get; private set; }

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
        private static int MezRange(IEnumerable<GameBot> bots) => bots.Where(bot => bot.IsAlive)
            .SelectMany(bot => bot.CrowdControlSpells ?? [])
            .Where(spell => spell.SpellType == eSpellType.Mesmerize && spell.CastTime > 0)
            .Select(spell => spell.Range).DefaultIfEmpty(0).Max();
        // Free enemy fighters in melee on the group's healers and casters.
        private List<GameBot> _pursuers = [];
        // When each healer or caster last took a step away from enemy fighters.
        private readonly Dictionary<GameBot, long> LastStandStep = new();

        private static bool IsSlowed(GameBot bot) =>
            bot.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
            bot.effectListComponent.ContainsEffectForEffectType(eEffect.Snare);

        private static bool IsFighter(GameBot bot) => PvpBuffPlan.ArchetypeOf(bot.PvpBuild.Class) == PvpBuffArchetype.Melee;

        private static PvpMemberState StateOf(GameBot bot, bool seen = true) => new(IsFighter(bot),
            bot.PvpBuild.Class is eCharacterClass.Cleric or eCharacterClass.Druid or eCharacterClass.Healer or eCharacterClass.Friar,
            bot.IsAlive, seen && (bot.IsMezzed || bot.IsStunned), seen && IsSlowed(bot), seen && (bot.IsBeingInterrupted || bot.IsSilenced));

        /// <summary>Takes the readings for both groups and chooses the order from them.</summary>
        private void Read(IReadOnlyList<GameBot> enemies, List<GameBot> known)
        {
            PvpWeights weights = PvpWeights.Of(_members.FirstOrDefault());
            Own = PvpReading.Of(_members.Select(member => StateOf(member)));
            Theirs = PvpReading.Of(enemies.Select(enemy => StateOf(enemy, known.Contains(enemy))));
            _pursuers = known.Where(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                (IsFighter(enemy) || enemy.attackComponent?.AttackState == true) &&
                enemy.TargetObject is GameBot { IsAlive: true } victim && _members.Contains(victim) && !IsFighter(victim) &&
                enemy.IsWithinRadius(victim, weights.Peel.MeleeReach)).ToList();
            long now = GameLoop.GameLoopTime;
            PvpOrder order = PvpOrder.Engage;
            if (FightStarted || _holdOver)
                Incoming = null;
            if (FightStarted)
            {
                // Strung out: the share of the living fighters that are out of the healers' reach.
                List<GameBot> fighters = _members.Where(member => member.IsAlive && IsFighter(member)).ToList();
                List<GameBot> soft = _members.Where(member => member.IsAlive && !IsFighter(member)).ToList();
                double far = 0;
                if (fighters.Count > 0 && soft.Count > 0)
                {
                    var middle = new Vector2((float)soft.Average(member => member.X), (float)soft.Average(member => member.Y));
                    far = fighters.Count(fighter => Vector2.Distance(new Vector2(fighter.X, fighter.Y), middle) > weights.Order.RegroupFighterIsFarAt) / (double)fighters.Count;
                }
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

        private void FindTrained(List<GameBot> known)
        {
            PvpWeights weights = PvpWeights.Of(_members.FirstOrDefault());
            List<GameBot> alive = _members.Where(member => member.IsAlive).ToList();
            List<int> attackers = alive.Select(member => known.Count(enemy =>
                enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject == member &&
                enemy.IsWithinRadius(member, weights.Peel.MeleeReach) &&
                (PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) == PvpBuffArchetype.Melee || enemy.attackComponent?.AttackState == true))).ToList();
            int chosen = PvpHealPlan.Trained(attackers, Trained != null ? alive.IndexOf(Trained) : -1, weights.Heal);
            GameBot now = chosen >= 0 ? alive[chosen] : null;
            if (now != Trained && now != null && FightStarted)
            {
                PvpRecorder.Decision(now, new { kind = "Trained", score = attackers[chosen] }, [], null);
                PvpObserver.Say(now, $"{PvpObserver.Name(now)} is being trained: {attackers[chosen]} on it");
            }
            Trained = now;
        }

        /// <summary>The group's decisions for this second: what it sees, who is being trained, kill target, interrupt duty, guard and peel.</summary>
        public void Decide(IReadOnlyList<GameBot> enemies)
        {
            List<GameBot> known = Look(enemies);
            Known = known;
            FindTrained(known);
            Read(enemies, known);
            KillTarget = CallTarget(_members, known, KillTarget is { IsAlive: true } current && known.Contains(current) ? current : null);
        }

        /// <summary>
        /// One kill target for the group, given to everyone who is not a healer; interrupt duty;
        /// guard and peel. Distance is measured from the middle of the group's damage dealers.
        /// </summary>
        private GameBot CallTarget(List<GameBot> team, List<GameBot> enemies, GameBot current)
        {
            List<GameBot> dealers = team.Where(bot => bot.IsAlive && PvpRoles.Of(bot.PvpBuild.Class) == PvpRole.Assist).ToList();
            List<GameBot> alive = enemies.Where(enemy => enemy.IsAlive).ToList();
            GameBot target = null;
            if (dealers.Count > 0 && alive.Count > 0)
            {
                double x = dealers.Average(bot => bot.X), y = dealers.Average(bot => bot.Y);
                PvpKillWeights kill = PvpWeights.Of(team[0]).Kill;
                // Out of position: how far each enemy stands from the middle of its own healers, the ones in sight.
                List<GameBot> theirHealers = alive.Where(enemy => PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) == PvpBuffArchetype.Healer).ToList();
                double hx = theirHealers.Count == 0 ? 0 : theirHealers.Average(bot => bot.X), hy = theirHealers.Count == 0 ? 0 : theirHealers.Average(bot => bot.Y);
                var views = alive.Select((enemy, id) => new PvpTargetView(id, PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class),
                    enemy.HealthPercent, enemy.IsMezzed, (int)Math.Sqrt(Math.Pow(enemy.X - x, 2) + Math.Pow(enemy.Y - y, 2)),
                    theirHealers.Count == 0 ? 0 : (int)Math.Sqrt(Math.Pow(enemy.X - hx, 2) + Math.Pow(enemy.Y - hy, 2)))).ToList();
                int chosen = PvpKillTarget.Choose(views, current != null ? alive.IndexOf(current) : -1, kill);
                target = chosen >= 0 ? alive[chosen] : null;
                // The call is recorded when it changes, with what else could have been called.
                if (target != current && target != null)
                    PvpObserver.Say(dealers[0], $"{PvpObserver.Realm(dealers[0])} calls {PvpObserver.Name(target)}, at {target.HealthPercent}% health");
                if (target != current && target != null)
                    PvpRecorder.Call(team[0], target, PvpKillTarget.Score(views[chosen], kill), current,
                        views.OrderByDescending(view => PvpKillTarget.Score(view, kill)).Take(4).Select(view => (object)new
                        {
                            d = alive[view.Id].Name, score = PvpKillTarget.Score(view, kill), view.HealthPercent, view.Mezzed, view.Distance, view.FromItsHealers,
                        }));
            }
            var guarded = new HashSet<GameBot>();
            Dictionary<GameBot, GameBot> duty = InterruptDuty(team, alive, target);
            // The middle of the group's healers and casters: where its fighters fall back to.
            List<GameBot> soft = team.Where(member => member.IsAlive && !IsFighter(member)).ToList();
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
                PvpRole role = PvpRoles.Of(bot.PvpBuild.Class);
                brain.PvpSquad = this;
                brain.PvpEnemies = enemies;
                brain.PvpKillTarget = target;
                duty.TryGetValue(bot, out GameBot onDuty);
                brain.PvpDutyTarget = onDuty;
                PvpInterruptMeans means = PvpInterruptDuty.MeansOf(bot.PvpBuild.Class, PvpWeights.Of(bot).Interrupt);
                GameBot aim = null;
                if (onDuty != null && means is PvpInterruptMeans.Pets or PvpInterruptMeans.Melee && !critical && bot.IsAlive)
                    aim = onDuty;
                else if (target != null && PvpRoles.JoinsKill(role, target.HealthPercent))
                    aim = target;
                else if (role == PvpRole.Peel && bot.IsAlive)
                    aim = Peel(bot, brain, team, alive, target, guarded);
                // Under Extend the group's fighters turn on whoever followed onto its healers and
                // casters (owner, 2026-11-17: an enemy that pushed deep is turned on).
                if (Order == PvpOrder.Extend && _pursuers.Count > 0 && role == PvpRole.Assist && onDuty == null && bot.IsAlive &&
                    IsFighter(bot) && PvpWeights.Of(bot).Order.ExtendFightersTurnOnPursuers)
                    aim = _pursuers.OrderBy(pursuer => bot.GetDistanceTo(pursuer)).First();
                // With nobody to turn on, a fighter that is far from its healers and casters comes
                // back to them: the group falls back as one, and does not leave its fighters behind.
                // Under Regroup every fighter that is far comes back, the ones with interrupt duty too.
                int stayWithin = PvpWeights.Of(bot).Order.ExtendFightersStayWithin;
                float fromHome = home is { } middle ? Vector2.Distance(new Vector2(bot.X, bot.Y), new Vector2(middle.X, middle.Y)) : 0;
                bool fallsBack = home != null && role == PvpRole.Assist && bot.IsAlive && IsFighter(bot) &&
                    (Order == PvpOrder.Extend && _pursuers.Count == 0 && stayWithin > 0 && onDuty == null && fromHome > stayWithin ||
                     Order == PvpOrder.Regroup && fromHome > PvpWeights.Of(bot).Order.RegroupWithin);
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
                    if (!bot.IsCasting && !bot.IsIncapacitated)
                        bot.PathTo(home.Value, bot.MaxSpeed);
                }
                else
                    FallingBack.Remove(bot);
                // A fighter going for an enemy that is on the group's healers and casters wants it
                // stopped: Slam or a snare style before damage (owner, 2026-11-18).
                brain.PvpWantsControlStyle = aim != null && IsFighter(bot) && _pursuers.Contains(aim);
                // A caster's pet is sent where its owner works: the enemy it has interrupt duty for,
                // else the kill target. (It was summoned and buffed and then never attacked: the
                // owner's scored casts take every turn, and the old brain ordered the pet only on
                // a turn it took itself.) Never left on an enemy that has since been mezzed.
                if (FightStarted && bot.IsAlive && bot.PvpBuild.Class is eCharacterClass.Cabalist or eCharacterClass.Enchanter or eCharacterClass.Spiritmaster &&
                    bot.ControlledBrain is ControlledMobBrain { Body: { IsAlive: true } } pet)
                {
                    // Its brain runs with no player near (AutonomousSummonActivity.IsPvpBotSummon); started here if it is not.
                    if (!pet.ServiceObjectId.IsSet)
                        pet.Start();
                    GameLiving petAim = onDuty is { IsAlive: true, IsMezzed: false } ? onDuty : target is { IsAlive: true } ? target : null;
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
                // A fighter on the kill: its target is recorded when it changes (guards and
                // interrupters record their own choices).
                if (FightStarted && bot.IsAlive && role == PvpRole.Assist && onDuty == null &&
                    PvpBuffPlan.ArchetypeOf(bot.PvpBuild.Class) == PvpBuffArchetype.Melee &&
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
        /// Pairs the team's interrupters with the enemy healers and casters that are neither the
        /// kill target (the damage dealers interrupt that one) nor asleep (bots/pvp/PvpInterruptDuty.cs).
        /// </summary>
        private Dictionary<GameBot, GameBot> InterruptDuty(List<GameBot> team, List<GameBot> enemies, GameBot kill)
        {
            PvpInterruptWeights weights = PvpWeights.Of(team.FirstOrDefault()).Interrupt;
            List<GameBot> interrupters = team.Where(bot => bot.IsAlive && bot.Brain is BotBrain &&
                PvpInterruptDuty.MeansOf(bot.PvpBuild.Class, weights) != PvpInterruptMeans.None).ToList();
            // Not one a mezzer of this team is putting to sleep right now either: the interrupter's
            // first hit would wake it and leave it immune.
            eRealm realm = team.Count > 0 ? team[0].Realm : eRealm.None;
            List<GameBot> open = enemies.Where(enemy => enemy != kill && !enemy.IsMezzed &&
                !BotPvpCrowdControl.Claimed(realm, enemy) &&
                PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) != PvpBuffArchetype.Melee).ToList();
            var pairs = new List<PvpDutyPair>();
            for (int i = 0; i < interrupters.Count; i++)
            {
                Duties.TryGetValue(interrupters[i], out GameBot current);
                for (int e = 0; e < open.Count; e++)
                    pairs.Add(new PvpDutyPair(i, e, PvpBuffPlan.ArchetypeOf(open[e].PvpBuild.Class), open[e].IsCasting,
                        !open[e].IsBeingInterrupted, open[e] == current, (int)interrupters[i].GetDistanceTo(open[e]),
                        PvpActionChoice.IsController(open[e].PvpBuild.Class, PvpWeights.Of(interrupters[i]))));
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
        private GameBot Peel(GameBot bot, BotBrain brain, List<GameBot> team, List<GameBot> enemies, GameBot kill, HashSet<GameBot> guarded)
        {
            PvpPeelWeights weights = PvpWeights.Of(bot).Peel;
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
            if (FightStarted && (!LastPeel.TryGetValue(bot, out var last) || last.Kind != kind || last.Target != aim))
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

        /// <summary>
        /// Healers and casters keep apart once the enemy is in sight (bots/pvp/PvpSpacing.cs). A
        /// member standing within mez radius of another takes a step away, on a turn it is not
        /// using: one that is casting, walking or unable to act is left alone, and its brain can
        /// stop the step at any moment to cast. Fighters are left out: they stack on the kill
        /// target, and a shield fighter belongs beside its ward.
        /// </summary>
        public void KeepApart()
        {
            PvpSpreadWeights weights = PvpWeights.Of(_members.FirstOrDefault()).Spread;
            List<GameBot> foes = Known.Where(enemy => enemy.IsAlive).ToList();
            if (weights.MinSpacing <= 0 || foes.Count == 0 || Order == PvpOrder.Hold)
                return;
            List<GameBot> backline = _members.Where(bot => bot.IsAlive &&
                PvpBuffPlan.ArchetypeOf(bot.PvpBuild.Class) != PvpBuffArchetype.Melee).ToList();
            var enemyCentre = new Vector2((float)foes.Average(enemy => enemy.X), (float)foes.Average(enemy => enemy.Y));
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            foreach (GameBot bot in backline)
            {
                if (bot.IsCasting || bot.IsMoving || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true ||
                    !foes.Any(enemy => bot.IsWithinRadius(enemy, SpotRange)) ||
                    LastSpreadStep.TryGetValue(bot, out long last) && now - last < 2_000 ||
                    LastStandStep.TryGetValue(bot, out long stood) && now - stood < 1_500)
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
        /// Healers and casters keep away from enemy fighters (bots/pvp/PvpPositioning.cs): a
        /// member with a fighter nearer than it wants takes a step to a better place, staying in
        /// range of its work. One in melee with a fighter that can follow stays: running gains
        /// nothing until the fighter is mezzed, stunned, rooted or slowed, and then it does run.
        /// The group's order scales the distance wanted. The step is the bot's turn: its brain
        /// casts again when it has arrived.
        /// </summary>
        public void StandOff()
        {
            PvpWeights all = PvpWeights.Of(_members.FirstOrDefault());
            PvpPositionWeights weights = all.Position;
            if (weights.SafeDistance <= 0 || !FightStarted)
                return;
            List<GameBot> fighters = Known.Where(enemy => enemy.IsAlive && IsFighter(enemy)).ToList();
            if (fighters.Count == 0)
                return;
            int safe = (int)(weights.SafeDistance * (Order == PvpOrder.Extend ? all.Order.ExtendSafeFactor : Order == PvpOrder.Push ? all.Order.PushSafeFactor : 1));
            List<GameBot> alive = _members.Where(bot => bot.IsAlive).ToList();
            List<GameBot> backline = alive.Where(bot => !IsFighter(bot)).ToList();
            List<GameBot> healers = backline.Where(bot => PvpRoles.Of(bot.PvpBuild.Class) == PvpRole.Heal).ToList();
            static Vector2 At(GameObject thing) => new(thing.X, thing.Y);
            long now = GameLoop.GameLoopTime;
            var nav = PathfindingProvider.Instance;
            // Under Extend they fall back the same way: from the middle of the enemy through the middle of themselves.
            Vector2 retreat = default;
            List<GameBot> enemiesAlive = Known.Where(enemy => enemy.IsAlive).ToList();
            if (Order == PvpOrder.Extend && backline.Count > 0 && enemiesAlive.Count > 0)
            {
                Vector2 away = new Vector2((float)backline.Average(bot => bot.X), (float)backline.Average(bot => bot.Y)) -
                               new Vector2((float)enemiesAlive.Average(enemy => enemy.X), (float)enemiesAlive.Average(enemy => enemy.Y));
                if (away.LengthSquared() > 1)
                    retreat = Vector2.Normalize(away);
            }
            foreach (GameBot bot in backline)
            {
                if (bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.castingComponent?.HasPendingSkillRequests == true)
                    continue;
                List<PvpThreat> threats = fighters.Select(enemy => new PvpThreat(At(enemy), enemy.IsMezzed || enemy.IsStunned, IsSlowed(enemy), enemy.TargetObject == bot)).ToList();
                bool hunted = threats.Any(threat => threat.OnMe);
                // After a step it works from its new place for a while; one running from held fighters does not wait.
                if (LastStandStep.TryGetValue(bot, out long last) && now - last < (hunted ? 1_500 : weights.StepEveryMilliseconds))
                    continue;
                bool isHealer = healers.Contains(bot);
                // A healer with somebody to heal stays at its work, unless it is the one being attacked.
                if (isHealer && !hunted && alive.Any(member => member != bot && member.HealthPercent < weights.HealerStaysIfMemberBelow &&
                        bot.IsWithinRadius(member, weights.HealRange + 300)))
                    continue;
                var work = new List<PvpWorkPoint>();
                if (isHealer)
                    work.AddRange(alive.Where(member => member != bot).Select(member => new PvpWorkPoint(At(member), weights.HealRange, member == Trained ? 2 : 0.5)));
                else
                {
                    // What it is working on: the enemy of its last choice, its duty and the kill target, at
                    // the range it really has (a nearsighted caster's is cut, and stepping out of it
                    // only makes it walk back in).
                    bool recent = now - brain.PvpWorkTick < 6_000 && brain.PvpWorkRange > 0;
                    int reach = recent ? Math.Min(weights.WorkRange, Math.Max(200, brain.PvpWorkRange - 100)) : weights.WorkRange;
                    foreach (GameLiving aim in new[] { recent ? brain.PvpWorkTarget : null, brain.PvpDutyTarget, brain.PvpKillTarget }
                                 .Where(aim => aim is { IsAlive: true }).Distinct())
                        work.Add(new PvpWorkPoint(At(aim), reach));
                    // Everybody stays where its healers reach it.
                    work.AddRange(healers.Select(healer => new PvpWorkPoint(At(healer), weights.HealRange, 0.5)));
                }
                List<Vector2> mates = backline.Where(member => member != bot).Select(member => At(member)).ToList();
                var self = At(bot);
                List<Vector2> steps = PvpPositioning.Steps(self, threats, work, mates, all.Spread.MinSpacing, safe, weights, out _, retreat);
                foreach (Vector2 to in steps.Take(4))
                {
                    // A place the navmesh knows; the next best otherwise.
                    if (nav.GetFloorBeneath(bot.CurrentZone, new Vector3(to.X, to.Y, 20_000), 20_000, nav.DefaultFilters) is not { } floor || floor.Z >= 19_999)
                        continue;
                    LastStandStep[bot] = now;
                    brain.PvpMoveFrom = now;
                    // The walk is the bot's turn for as long as it takes.
                    brain.PvpMoveUntil = now + Math.Clamp((long)(Vector2.Distance(self, to) / Math.Max(100, (int)bot.MaxSpeed) * 1000) + 300, 800, 4_500);
                    bot.PathTo(floor, bot.MaxSpeed);
                    PvpRecorder.Decision(bot, new
                    {
                        kind = hunted ? "GetAway" : "StandOff", order = Order.ToString(),
                        score = (int)threats.Min(threat => Vector2.Distance(self, threat.At)),
                    }, [], null);
                    break;
                }
            }
        }
    }
}
