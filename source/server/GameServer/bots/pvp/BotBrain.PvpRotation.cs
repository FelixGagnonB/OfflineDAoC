using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS;

namespace DOL.AI.Brain
{
    /// <summary>
    /// An 8v8 caster's turn when its group runs the squad brain: every spell kind it has, on every
    /// enemy it can reach, is scored (<see cref="PvpActionChoice"/>) and the best is cast. Each
    /// decision goes to the recorder with the runners-up, and so does a turn where nothing could
    /// be cast, with what stood in the way.
    /// </summary>
    public partial class BotBrain
    {
        /// <summary>The enemy group as the squad brain sees it; null without a squad brain.</summary>
        public IReadOnlyList<GameBot> PvpEnemies { get; set; }
        /// <summary>The group's called kill target, for every member (an interrupter's own aim is PvpAssignedTarget).</summary>
        public GameLiving PvpKillTarget { get; set; }

        /// <summary>The squad this bot belongs to (bots/pvp/PvpSquad.cs); null without a squad brain.</summary>
        public PvpSquad PvpSquad { get; set; }

        /// <summary>The enemy healer or caster this bot is to keep from casting (bots/pvp/PvpInterruptDuty.cs); null without one.</summary>
        public GameLiving PvpDutyTarget { get; set; }

        /// <summary>
        /// Set by the squad when it sends this bot a step away from enemy fighters
        /// (bots/pvp/PvpPositioning.cs): the step began at From and is the bot's turn until Until.
        /// </summary>
        public long PvpMoveFrom { get; set; }
        public long PvpMoveUntil { get; set; }

        /// <summary>
        /// True while this bot is on such a step. Its brain starts no cast meanwhile: a cast would
        /// stop it where it stands, which is where it did not want to be.
        /// </summary>
        private bool PvpOnTheMove()
        {
            long now = GameLoop.GameLoopTime;
            if (BotBody?.IsPvpBot != true || now >= PvpMoveUntil || Body.IsIncapacitated)
                return false;
            // The path may take a moment to be found.
            if (Body.IsMoving || now - PvpMoveFrom < 400)
                return true;
            PvpMoveUntil = 0;
            return false;
        }

        /// <summary>The enemy of this bot's last scored choice, the range of the spell chosen, and when: what it must stay in range of.</summary>
        public GameLiving PvpWorkTarget { get; private set; }
        public int PvpWorkRange { get; private set; }
        public long PvpWorkTick { get; private set; }

        /// <summary>
        /// Set by the squad for a fighter whose target should be stopped, not killed: an enemy that
        /// is on the group's healers and casters. Its next style is a stun or a snare when it has
        /// one that would land (bots/pvp/PvpStylePolicy.cs).
        /// </summary>
        public bool PvpWantsControlStyle { get; set; }

        /// <summary>True while this bot's group holds: it stands and casts what reaches (bots/pvp/PvpReading.cs).</summary>
        private bool PvpHolding() => BotBody?.IsPvpBot == true && PvpSquad?.Order == PvpOrder.Hold;

        private long _pvpNoActionRecordedTick, _pvpApproachTick;
        // The last cast this routine started, to notice one that was accepted and then never began.
        private Spell _pvpLastCast;
        private GameLiving _pvpLastCastTarget;
        private long _pvpLastCastTick;

        private static readonly eSpellType[] PvpDamageTypes = [eSpellType.DirectDamage, eSpellType.Lifedrain, eSpellType.DirectDamageWithDebuff];

        private static bool IsPvpSpellOfKind(Spell spell, PvpActionKind kind) => kind switch
        {
            PvpActionKind.Nearsight => spell.SpellType == eSpellType.Nearsight,
            PvpActionKind.Disease => spell.SpellType == eSpellType.Disease,
            PvpActionKind.Damage => PvpDamageTypes.Contains(spell.SpellType),
            // A root, an area root, or a snare nuke: whichever the class has (the highest level is taken).
            PvpActionKind.Root => spell.SpellType is eSpellType.SpeedDecrease or eSpellType.DamageSpeedDecrease,
            PvpActionKind.Pet => spell.SpellType == eSpellType.SummonTheurgistPet,
            PvpActionKind.Mez => spell.SpellType == eSpellType.Mesmerize && spell.Radius == 0,
            PvpActionKind.MezArea => spell.SpellType == eSpellType.Mesmerize && spell.Radius > 0,
            PvpActionKind.Stun => spell.SpellType == eSpellType.Stun,
            PvpActionKind.CureMez => spell.SpellType == eSpellType.CureMezz,
            PvpActionKind.Amnesia => spell.SpellType == eSpellType.Amnesia,
            _ => false,
        };

        private static bool PvpMezzedOrImmune(GameLiving living) =>
            living.IsMezzed || living.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity);

        private static bool PvpStunnedOrImmune(GameLiving living) =>
            living.IsStunned || living.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity);

        /// <summary>True when this class's mez and stun are chosen by score here, not by the old crowd-control routine.</summary>
        private bool PvpCrowdControlIsScored() => PvpEnemies != null && BotBody?.IsPvpBot == true &&
            PvpActionChoice.KindsFor((eCharacterClass)BotBody.CharacterClass.ID, PvpWeights.Of(BotBody))
                .Any(kind => kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Stun);

        /// <summary>Whom to wake first, for the healing routine's cure mez; 0 for everyone outside an 8v8 group.</summary>
        private int PvpCureMezPriority(GameLiving member)
        {
            if (BotBody?.IsPvpBot != true || member is not GameBot { PvpBuild: { } build })
                return 0;
            PvpWeights weights = PvpWeights.Of(BotBody);
            return PvpBuffPlan.ArchetypeOf(build.Class) switch
            {
                PvpBuffArchetype.Healer => weights.CureMezOnHealer,
                PvpBuffArchetype.Caster => weights.CureMezOnCaster,
                _ => 0,
            } + (PvpActionChoice.IsController(build.Class, weights) ? weights.CureMezOnController : 0);
        }

        /// <summary>A fight is on: a living enemy is within reach of this bot.</summary>
        private bool PvpFightIsOn() => PvpEnemies != null &&
            PvpEnemies.Any(enemy => enemy.IsAlive && Body.IsWithinRadius(enemy, PvpScrim.EngageRange));

        /// <summary>
        /// Walks into casting range of <paramref name="aim"/>, to a place apart from the group's
        /// other healers and casters (bots/pvp/PvpSpacing.cs): everybody heading straight for the
        /// same enemy is what put eight bots inside one area mez.
        /// </summary>
        private void PvpWalkIntoRange(GameLiving aim, Spell spell)
        {
            PvpSpreadWeights spread = PvpWeights.Of(BotBody).Spread;
            int range = (spell.Target == eSpellTarget.SELF || spell.Range <= 0 ? spell.Radius : spell.CalculateEffectiveRange(Body)) - spread.RangeMargin;
            // In range already, so the trouble is something in the way; or spreading is off: straight at it.
            if (spread.MinSpacing <= 0 || range <= 0 || Body.IsWithinRadius(aim, range))
            {
                Body.PathTo(new Vector3(aim.X, aim.Y, aim.Z), Body.MaxSpeed);
                return;
            }
            List<Vector2> mates = Body.Group?.GetMembersInTheGroup()
                .Where(member => member != Body && member.IsAlive && member is GameBot { PvpBuild: { } build } &&
                                 PvpBuffPlan.ArchetypeOf(build.Class) != PvpBuffArchetype.Melee)
                .Select(member => new Vector2(member.X, member.Y)).ToList() ?? [];
            Vector2 point = PvpSpacing.ApproachPoint(new Vector2(Body.X, Body.Y), new Vector2(aim.X, aim.Y), range, mates, spread.MinSpacing);
            var nav = PathfindingProvider.Instance;
            // The place needs a height the navmesh knows, or no path is found.
            Body.PathTo(nav.GetFloorBeneath(Body.CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters)
                is { } floor && floor.Z < 19_999 ? floor : new Vector3(point.X, point.Y, Body.Z), Body.MaxSpeed);
        }

        /// <summary>
        /// True when this healer's scored choice goes before its healing routine this turn: its
        /// build leans to control or interrupts (<see cref="PvpWeights.HealsFirstBelow"/>) and
        /// nobody of the group in reach is hurt badly enough to claim the turn.
        /// </summary>
        private bool PvpScoredChoiceBeforeHeals()
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || BotBody.PvpBuild == null)
                return false;
            int below = PvpWeights.Of(BotBody).HealsFirstBelowFor(BotBody.PvpBuild.Id, BotBody.PvpBuild.Class);
            if (below > 100)
                return false;
            // The trained member too only claims this healer's turn below that mark: healing it
            // from the first blow is the heal-first healers' work.
            return Body.Group?.GetMembersInTheGroup().Any(member => member.IsAlive && member.HealthPercent < below && InSupportReach(member)) != true;
        }

        /// <summary>
        /// True when this healer, built to heal, leaves waking <paramref name="sleeper"/> to the
        /// group's control healer: one that has cure mez, is itself able to act and is near enough
        /// (<see cref="PvpHealWeights.HealerLeavesCureMezToController"/>).
        /// </summary>
        private bool PvpLeavesCureMezToController(GameLiving sleeper)
        {
            if (BotBody?.IsPvpBot != true || BotBody.PvpBuild == null)
                return false;
            PvpWeights weights = PvpWeights.Of(BotBody);
            if (!weights.Heal.HealerLeavesCureMezToController ||
                weights.HealsFirstBelowFor(BotBody.PvpBuild.Id, BotBody.PvpBuild.Class) <= 100)
                return false;
            return Body.Group?.GetMembersInTheGroup().OfType<GameBot>().Any(mate =>
                mate != BotBody && mate != sleeper && mate.IsAlive && !mate.IsMezzed && !mate.IsStunned && mate.CureMezz != null &&
                mate.PvpBuild != null && weights.HealsFirstBelowFor(mate.PvpBuild.Id, mate.PvpBuild.Class) <= 100 &&
                mate.IsWithinRadius(sleeper, 2_000)) == true;
        }

        private long _pvpIdleRecordedTick;

        /// <summary>
        /// For the recording: a caster whose casts are scored spent this turn on something else
        /// (its healing routine, a cast still waiting to start), so the scored choice did not run.
        /// Written at most every two seconds, and only while a fight is on.
        /// </summary>
        private void RecordPvpTurnTaken(string by)
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || GameLoop.GameLoopTime - _pvpIdleRecordedTick < 2_000 ||
                Body.IsCasting || Body.IsIncapacitated || !PvpFightIsOn() ||
                !PvpActionChoice.Covers((eCharacterClass)BotBody.CharacterClass.ID, PvpWeights.Of(BotBody)))
                return;
            _pvpIdleRecordedTick = GameLoop.GameLoopTime;
            PvpRecorder.Decision(BotBody, null, [], new Dictionary<string, string> { ["turn taken by"] = by });
        }

        private bool TryPvpRotation()
        {
            if (PvpEnemies == null || BotBody?.IsPvpBot != true || Body.IsIncapacitated)
                return false;
            if (Body.castingComponent?.HasPendingSkillRequests == true)
            {
                RecordPvpTurnTaken("a cast waiting to start");
                return false;
            }
            // In the middle of a cast only an instant can be added, as a player does (owner, 2026-11-17:
            // a Bard fires its amnesia while it casts its mez).
            bool midCast = Body.IsCasting;
            var characterClass = (eCharacterClass)BotBody.CharacterClass.ID;
            PvpWeights weights = PvpWeights.Of(BotBody);
            if (!PvpActionChoice.Covers(characterClass, weights))
                return false;

            // What stood in the way, for the recorder: kind or count by reason.
            var blocked = new Dictionary<string, string>();

            // Choosing again while the last cast should still be running means it never began or was cut.
            // Half the listed cast time: dexterity shortens a cast to about 60% of it, and the two-second
            // nearsights that finished in 1.2 to 1.4 seconds were being counted as lost (2026-11-17).
            if (!midCast && _pvpLastCast is { CastTime: > 0 } lost && GameLoop.GameLoopTime - _pvpLastCastTick < Math.Min(lost.CastTime / 2, 1_500))
            {
                GameLiving aimed = _pvpLastCastTarget;
                int reach = lost.Target == eSpellTarget.SELF || lost.Range <= 0 ? lost.Radius : lost.CalculateEffectiveRange(Body);
                // Numbers before the semicolon, yes/no facts after it, so the analyzer can add equal causes up.
                blocked["previous cast lost"] = $"{lost.Name} after {GameLoop.GameLoopTime - _pvpLastCastTick} ms, {(aimed == null ? -1 : (int)Body.GetDistanceTo(aimed))} of {reach}; " +
                    $"moving {Body.IsMoving}, interrupted {Body.IsBeingInterrupted}, " +
                    $"target {(aimed == null ? "none" : !aimed.IsAlive ? "dead" : aimed.IsMezzed ? "mezzed" : "awake")}, " +
                    $"in range {aimed != null && Body.IsWithinRadius(aimed, reach)}, " +
                    $"still targeted {Body.TargetObject == aimed}, " +
                    $"may attack {aimed != null && GameServer.ServerRules.IsAllowedToAttack(Body, aimed, true)}, " +
                    $"in front {aimed != null && Body.IsObjectInFront(aimed, 180)}";
            }
            if (!midCast)
                _pvpLastCast = null;

            // The best castable spell of each kind this class uses, and the best instant: an instant
            // is an option of its own, because it can do what a spell with a cast time cannot.
            List<Spell> known = (Body.HarmfulSpells ?? []).Concat(Body.InstantHarmfulSpells ?? [])
                .Concat(BotBody.CrowdControlSpells ?? []).Concat(BotBody.InstantCrowdControlSpells ?? [])
                .Append(BotBody.CureMezz)
                // The old brain leaves amnesia out of its lists on purpose (it does nothing to a monster's cast).
                .Concat(Body.Spells?.Where(spell => spell?.SpellType == eSpellType.Amnesia) ?? [])
                .Where(spell => spell != null).DistinctBy(spell => spell.ID).ToList();
            var spells = new Dictionary<PvpActionKind, Spell>();
            var instants = new Dictionary<PvpActionKind, Spell>();
            foreach (PvpActionKind kind in PvpActionChoice.KindsFor(characterClass, weights))
            {
                List<Spell> ofKind = known.Where(spell => IsPvpSpellOfKind(spell, kind) && spell.Level <= Body.Level)
                    .OrderByDescending(spell => spell.Level).ToList();
                bool Ready(Spell spell) => Body.GetSkillDisabledDuration(spell) == 0 && Body.Mana >= BotBody.PowerCost(spell);
                Spell cast = midCast ? null : ofKind.FirstOrDefault(spell => spell.CastTime > 0 && Ready(spell) &&
                    (spell.Uninterruptible || !Body.IsBeingInterrupted));
                Spell instant = ofKind.FirstOrDefault(spell => spell.CastTime == 0 && Ready(spell));
                if (cast != null)
                    spells[kind] = cast;
                if (instant != null)
                    instants[kind] = instant;
                if (cast == null && instant == null)
                    blocked[kind.ToString()] = ofKind.Count == 0 ? "no such spell"
                        : ofKind.All(spell => Body.Mana < BotBody.PowerCost(spell)) ? "no power"
                        : Body.IsBeingInterrupted ? "interrupted" : "recast timer";
            }

            List<GameBot> enemies = PvpEnemies.Where(enemy => enemy.IsAlive && enemy.ObjectState == GameObject.eObjectState.Active).ToList();
            GameLiving kill = PvpKillTarget is { IsAlive: true } called ? called : null;
            var nav = PathfindingProvider.Instance;
            var here = new Vector3(Body.X, Body.Y, Body.Z);
            int tooFar = 0, offLimitsCount = 0;
            var options = new List<PvpOption>();
            var sight = new Dictionary<GameBot, bool>();
            // Whom each area mez would put to sleep, by option (kind and primary target).
            var caughtBy = new Dictionary<(PvpActionKind, int, bool), List<GameBot>>();
            // Enemies first, then the mezzed members of this bot's own group (for cure mez); an option's target is an index here.
            List<GameLiving> friends = Body.Group?.GetMembersInTheGroup()
                .Where(member => member != Body && member.IsAlive && member.IsMezzed).ToList() ?? [];
            List<GameLiving> targets = enemies.Cast<GameLiving>().Concat(friends).ToList();
            foreach ((PvpActionKind kind, Spell spell, bool instant) in spells.Select(pair => (pair.Key, pair.Value, false))
                         .Concat(instants.Select(pair => (pair.Key, pair.Value, true))))
            {
                bool mez = kind is PvpActionKind.Mez or PvpActionKind.MezArea;
                // A spell centred on the caster reaches as far as its radius.
                bool aroundCaster = spell.Target == eSpellTarget.SELF || spell.Range <= 0;
                int range = aroundCaster ? spell.Radius : spell.CalculateEffectiveRange(Body);
                int cost = Body.MaxMana > 0 ? 100 * BotBody.PowerCost(spell) / Body.MaxMana : 0;
                if (kind == PvpActionKind.CureMez)
                {
                    for (int friend = 0; friend < friends.Count; friend++)
                    {
                        GameLiving member = friends[friend];
                        int away = (int)Body.GetDistanceTo(member);
                        bool visible = nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(member.X, member.Y, member.Z), nav.DefaultFilters);
                        int classId = member switch
                        {
                            GamePlayer { CharacterClass: { } playerClass } => playerClass.ID,
                            IGamePlayer { CharacterClass: { } botClass } => botClass.ID,
                            _ => 0,
                        };
                        options.Add(new PvpOption(kind, enemies.Count + friend, PvpBuffPlan.ArchetypeOf((eCharacterClass)classId), false, false,
                            member.HealthPercent, false, false, false, true, Body.ManaPercent, cost,
                            Math.Max(away > range ? away - range + 50 : 0, visible ? 0 : 300))
                        {
                            Instant = instant, OnController = PvpActionChoice.IsController((eCharacterClass)classId, weights),
                        });
                    }
                    continue;
                }
                for (int id = 0; id < enemies.Count; id++)
                {
                    GameBot enemy = enemies[id];
                    int distance = (int)Body.GetDistanceTo(enemy);
                    if (distance > weights.ConsiderRange)
                    {
                        tooFar++;
                        continue;
                    }
                    if (!sight.TryGetValue(enemy, out bool seen))
                        sight[enemy] = seen = nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(enemy.X, enemy.Y, enemy.Z), nav.DefaultFilters);
                    // Out of range or behind something: still an option, at the price of the walk.
                    int walk = Math.Max(distance > range ? distance - range + 50 : 0, seen ? 0 : 300);
                    // Asleep, or claimed by a mezzer of this group for the length of its cast, and not
                    // the kill target: the server refuses a bot any attack on it (the casts that were
                    // accepted and never began), so it is no option. The exception is an area mez,
                    // which may be aimed at a sleeping enemy to catch the ones around it.
                    bool offLimits = BotPvpCrowdControl.Protected(Body, enemy);
                    if (offLimits && kind != PvpActionKind.MezArea)
                    {
                        offLimitsCount++;
                        continue;
                    }
                    bool hasEffect = kind switch
                    {
                        PvpActionKind.Mez => PvpMezzedOrImmune(enemy),
                        // The enemy an area mez is aimed at may itself be immune; the ones around it count.
                        PvpActionKind.MezArea => false,
                        PvpActionKind.Stun => PvpStunnedOrImmune(enemy),
                        PvpActionKind.Pet => false,
                        _ => LivingHasEffect(enemy, spell),
                    };
                    int caught = 1;
                    bool catchesKill = false;
                    if (kind == PvpActionKind.MezArea)
                    {
                        GameObject centre = aroundCaster ? Body : enemy;
                        List<GameBot> inside = enemies.Where(other => other.IsWithinRadius(centre, spell.Radius)).ToList();
                        List<GameBot> asleep = inside.Where(other => other != kill && !PvpMezzedOrImmune(other) && !BotPvpCrowdControl.Protected(Body, other)).ToList();
                        caughtBy[(kind, id, instant)] = asleep;
                        caught = asleep.Count;
                        catchesKill = kill != null && inside.Contains(kill) && !PvpMezzedOrImmune(kill);
                    }
                    int wakes = 0;
                    if (kind == PvpActionKind.Amnesia)
                    {
                        // Counted: the enemies in it that would lose a cast, and the sleepers it would wake.
                        List<GameBot> inside = spell.Radius > 0 ? enemies.Where(other => other.IsWithinRadius(enemy, spell.Radius)).ToList() : [enemy];
                        caught = inside.Count(other => !other.IsMezzed && other.IsCasting && other.CurrentSpellHandler?.Spell.InstrumentRequirement == 0);
                        wakes = inside.Count(other => other.IsMezzed && other != kill);
                    }
                    options.Add(new PvpOption(kind, id, PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class), enemy == kill,
                        enemy == PvpAssignedTarget || enemy == PvpDutyTarget, enemy.HealthPercent, hasEffect,
                        enemy.IsCasting, enemy.IsBeingInterrupted, !mez && enemy.IsMezzed, Body.ManaPercent, cost, walk, caught, catchesKill, wakes,
                        instant, PvpActionChoice.IsController(enemy.PvpBuild.Class, weights),
                        PvpSquad?.IsPursuer(enemy) == true, PvpSquad?.Order == PvpOrder.Extend));
                }
            }
            if (tooFar > 0)
                blocked["too far"] = tooFar.ToString();
            if (offLimitsCount > 0)
                blocked["asleep or claimed"] = offLimitsCount.ToString();

            List<(PvpOption Option, int Score)> ranked = PvpActionChoice.Rank(characterClass, options, weights, BotBody.PvpBuild?.Id);
            object Describe((PvpOption Option, int Score) scored) => new
            {
                kind = scored.Option.Kind.ToString(), d = targets[scored.Option.TargetId].Name, score = scored.Score, walk = scored.Option.Short,
                caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
                // For a root: the target is on this group's healers and casters; it is rooted, slowed or immune already.
                pursuer = scored.Option.Pursuer ? true : (bool?)null,
                has = scored.Option.Kind == PvpActionKind.Root && scored.Option.HasEffect ? true : (bool?)null,
            };

            foreach ((PvpOption Option, int Score) scored in ranked.Where(scored => PvpActionChoice.WorthIt(scored.Option, scored.Score, weights)))
            {
                Spell spell = (scored.Option.Instant ? instants : spells)[scored.Option.Kind];
                GameLiving aim = targets[scored.Option.TargetId];
                // No walking in the middle of a cast: it would end the cast. And none while the group holds.
                if ((midCast || PvpHolding()) && scored.Option.Short > 0)
                    continue;
                if (aim is GameBot && !friends.Contains(aim))
                    (PvpWorkTarget, PvpWorkRange, PvpWorkTick) = (aim,
                        spell.Target == eSpellTarget.SELF || spell.Range <= 0 ? spell.Radius : spell.CalculateEffectiveRange(Body), GameLoop.GameLoopTime);
                if (scored.Option.Short > 0)
                {
                    // The best thing to do is out of reach: walk toward it. The order is renewed
                    // once a second; the choice is made again on every think tick on the way.
                    if (GameLoop.GameLoopTime - _pvpApproachTick >= 1_000 || !Body.IsMoving)
                    {
                        _pvpApproachTick = GameLoop.GameLoopTime;
                        PvpWalkIntoRange(aim, spell);
                        PvpRecorder.Decision(BotBody, new { kind = "Approach", spell = spell.Name, d = aim.Name, score = scored.Score },
                            ranked.Take(5).Select(Describe), blocked.Count > 0 ? blocked : null);
                    }
                    return true;
                }
                bool mez = scored.Option.Kind is PvpActionKind.Mez or PvpActionKind.MezArea;
                // One mezzer per enemy: claim it for the length of the cast, or leave it to whoever has.
                bool overSleeper = scored.Option.Kind == PvpActionKind.MezArea && BotPvpCrowdControl.Protected(Body, aim);
                if (mez && !overSleeper && !BotPvpCrowdControl.Reserve(BotBody, aim, spell.CastTime + 2_000))
                    continue;
                if (overSleeper)
                {
                    // Lets this one cast through the server's rule against attacking a sleeping enemy.
                    _pvpAreaMezAim = aim;
                    _pvpControlCastUntil = GameLoop.GameLoopTime + spell.CastTime + 2_000;
                }
                GameObject previous = Body.TargetObject;
                Body.TargetObject = aim;
                if (spell.CastTime > 0)
                {
                    Body.StopMovingOnPath();
                    Body.StopMoving();
                    Body.StopAttack();
                }
                if (Body.CastSpell(spell, m_mobSpellLine))
                {
                    if (midCast)
                    {
                        // The running cast keeps its own target and its own record.
                        Body.TargetObject = previous;
                    }
                    else
                    {
                        (_pvpLastCast, _pvpLastCastTarget, _pvpLastCastTick) = (spell, aim, GameLoop.GameLoopTime);
                        // A healer's attack spell is otherwise stopped by its support routine.
                        _pvpControlSpellId = spell.ID;
                        _pvpControlCastUntil = GameLoop.GameLoopTime + spell.CastTime + 2_000;
                    }
                    if (caughtBy.TryGetValue((scored.Option.Kind, scored.Option.TargetId, scored.Option.Instant), out List<GameBot> asleep))
                    {
                        foreach (GameBot other in asleep)
                            BotPvpCrowdControl.Reserve(BotBody, other, spell.CastTime + 2_000);
                    }
                    PvpRecorder.Decision(BotBody, new
                        {
                            kind = scored.Option.Kind.ToString(), spell = spell.Name, d = aim.Name, score = scored.Score,
                            caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
                            instant = scored.Option.Instant ? true : (bool?)null, midCast = midCast ? true : (bool?)null,
                        },
                        ranked.Take(5).Select(Describe), blocked.Count > 0 ? blocked : null);
                    return true;
                }
                if (mez && !overSleeper)
                    BotPvpCrowdControl.Release(BotBody, aim);
                if (overSleeper)
                    _pvpAreaMezAim = null;
                Body.TargetObject = previous;
                blocked["cast refused"] = spell.Name;
            }

            // Nothing cast. Say why, but not on every think tick, and not for a look taken in the middle of a cast.
            // A class with only its cure mez scored has nothing to say while nobody is asleep.
            if (!midCast && GameLoop.GameLoopTime - _pvpNoActionRecordedTick >= 2_000 &&
                (ranked.Count > 0 || PvpActionChoice.ScoresOffense(characterClass, weights)))
            {
                _pvpNoActionRecordedTick = GameLoop.GameLoopTime;
                if (ranked.Count > 0 && !ranked.Any(scored => PvpActionChoice.WorthIt(scored.Option, scored.Score, weights)))
                    blocked["best score too low"] = ranked[0].Score.ToString();
                PvpRecorder.Decision(BotBody, null, ranked.Take(5).Select(Describe), blocked);
            }
            return false;
        }
    }
}
