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
            PvpActionKind.Root => spell.SpellType == eSpellType.SpeedDecrease,
            PvpActionKind.Pet => spell.SpellType == eSpellType.SummonTheurgistPet,
            PvpActionKind.Mez => spell.SpellType == eSpellType.Mesmerize && spell.Radius == 0,
            PvpActionKind.MezArea => spell.SpellType == eSpellType.Mesmerize && spell.Radius > 0,
            PvpActionKind.Stun => spell.SpellType == eSpellType.Stun,
            PvpActionKind.CureMez => spell.SpellType == eSpellType.CureMezz,
            _ => false,
        };

        private static bool PvpMezzedOrImmune(GameLiving living) =>
            living.IsMezzed || living.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity);

        private static bool PvpStunnedOrImmune(GameLiving living) =>
            living.IsStunned || living.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity);

        /// <summary>True when this class's mez and stun are chosen by score here, not by the old crowd-control routine.</summary>
        private bool PvpCrowdControlIsScored() => PvpEnemies != null && BotBody?.IsPvpBot == true &&
            PvpActionChoice.KindsFor((eCharacterClass)BotBody.CharacterClass.ID, PvpWeights.Current)
                .Any(kind => kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Stun);

        /// <summary>Whom to wake first, for the healing routine's cure mez; 0 for everyone outside an 8v8 group.</summary>
        private int PvpCureMezPriority(GameLiving member)
        {
            if (BotBody?.IsPvpBot != true || member is not GameBot { PvpBuild: { } build })
                return 0;
            PvpWeights weights = PvpWeights.Current;
            return PvpBuffPlan.ArchetypeOf(build.Class) switch
            {
                PvpBuffArchetype.Healer => weights.CureMezOnHealer,
                PvpBuffArchetype.Caster => weights.CureMezOnCaster,
                _ => 0,
            };
        }

        /// <summary>A fight is on: a living enemy is within reach of this bot.</summary>
        private bool PvpFightIsOn() => PvpEnemies != null &&
            PvpEnemies.Any(enemy => enemy.IsAlive && Body.IsWithinRadius(enemy, PvpScrim.EngageRange));

        private bool TryPvpRotation()
        {
            if (PvpEnemies == null || BotBody?.IsPvpBot != true || Body.IsCasting || Body.IsIncapacitated ||
                Body.castingComponent?.HasPendingSkillRequests == true)
                return false;
            var characterClass = (eCharacterClass)BotBody.CharacterClass.ID;
            PvpWeights weights = PvpWeights.Current;
            if (!PvpActionChoice.Covers(characterClass, weights))
                return false;

            // What stood in the way, for the recorder: kind or count by reason.
            var blocked = new Dictionary<string, string>();

            // Choosing again while the last cast should still be running means it never began or was cut.
            if (_pvpLastCast is { CastTime: > 0 } lost && GameLoop.GameLoopTime - _pvpLastCastTick < Math.Min(lost.CastTime, 1_500))
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
            _pvpLastCast = null;

            // The best castable spell of each kind this class uses.
            List<Spell> known = (Body.HarmfulSpells ?? []).Concat(Body.InstantHarmfulSpells ?? [])
                .Concat(BotBody.CrowdControlSpells ?? []).Concat(BotBody.InstantCrowdControlSpells ?? [])
                .Append(BotBody.CureMezz)
                .Where(spell => spell != null).DistinctBy(spell => spell.ID).ToList();
            var spells = new Dictionary<PvpActionKind, Spell>();
            foreach (PvpActionKind kind in PvpActionChoice.KindsFor(characterClass, weights))
            {
                List<Spell> ofKind = known.Where(spell => IsPvpSpellOfKind(spell, kind) && spell.Level <= Body.Level)
                    .OrderByDescending(spell => spell.Level).ToList();
                Spell ready = ofKind.FirstOrDefault(spell => Body.GetSkillDisabledDuration(spell) == 0 &&
                    Body.Mana >= BotBody.PowerCost(spell) &&
                    (spell.CastTime == 0 || spell.Uninterruptible || !Body.IsBeingInterrupted));
                if (ready != null)
                    spells[kind] = ready;
                else
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
            var caughtBy = new Dictionary<(PvpActionKind, int), List<GameBot>>();
            // Enemies first, then the mezzed members of this bot's own group (for cure mez); an option's target is an index here.
            List<GameLiving> friends = Body.Group?.GetMembersInTheGroup()
                .Where(member => member != Body && member.IsAlive && member.IsMezzed).ToList() ?? [];
            List<GameLiving> targets = enemies.Cast<GameLiving>().Concat(friends).ToList();
            foreach ((PvpActionKind kind, Spell spell) in spells)
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
                            Math.Max(away > range ? away - range + 50 : 0, visible ? 0 : 300)));
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
                        caughtBy[(kind, id)] = asleep;
                        caught = asleep.Count;
                        catchesKill = kill != null && inside.Contains(kill) && !PvpMezzedOrImmune(kill);
                    }
                    options.Add(new PvpOption(kind, id, PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class), enemy == kill,
                        enemy == PvpAssignedTarget, enemy.HealthPercent, hasEffect,
                        enemy.IsCasting, enemy.IsBeingInterrupted, !mez && enemy.IsMezzed, Body.ManaPercent, cost, walk, caught, catchesKill));
                }
            }
            if (tooFar > 0)
                blocked["too far"] = tooFar.ToString();
            if (offLimitsCount > 0)
                blocked["asleep or claimed"] = offLimitsCount.ToString();

            List<(PvpOption Option, int Score)> ranked = PvpActionChoice.Rank(characterClass, options, weights);
            object Describe((PvpOption Option, int Score) scored) => new
            {
                kind = scored.Option.Kind.ToString(), d = targets[scored.Option.TargetId].Name, score = scored.Score, walk = scored.Option.Short,
                caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
            };

            foreach ((PvpOption Option, int Score) scored in ranked.TakeWhile(scored => scored.Score >= weights.MinScore))
            {
                Spell spell = spells[scored.Option.Kind];
                GameLiving aim = targets[scored.Option.TargetId];
                if (scored.Option.Short > 0)
                {
                    // The best thing to do is out of reach: walk toward it. The order is renewed
                    // once a second; the choice is made again on every think tick on the way.
                    if (GameLoop.GameLoopTime - _pvpApproachTick >= 1_000 || !Body.IsMoving)
                    {
                        _pvpApproachTick = GameLoop.GameLoopTime;
                        Body.PathTo(new Vector3(aim.X, aim.Y, aim.Z), Body.MaxSpeed);
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
                    (_pvpLastCast, _pvpLastCastTarget, _pvpLastCastTick) = (spell, aim, GameLoop.GameLoopTime);
                    // A healer's attack spell is otherwise stopped by its support routine.
                    _pvpControlSpellId = spell.ID;
                    _pvpControlCastUntil = GameLoop.GameLoopTime + spell.CastTime + 2_000;
                    if (caughtBy.TryGetValue((scored.Option.Kind, scored.Option.TargetId), out List<GameBot> asleep))
                    {
                        foreach (GameBot other in asleep)
                            BotPvpCrowdControl.Reserve(BotBody, other, spell.CastTime + 2_000);
                    }
                    PvpRecorder.Decision(BotBody, new
                        {
                            kind = scored.Option.Kind.ToString(), spell = spell.Name, d = aim.Name, score = scored.Score,
                            caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
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

            // Nothing cast. Say why, but not on every think tick.
            if (GameLoop.GameLoopTime - _pvpNoActionRecordedTick >= 2_000)
            {
                _pvpNoActionRecordedTick = GameLoop.GameLoopTime;
                if (ranked.Count > 0 && ranked[0].Score < weights.MinScore)
                    blocked["best score too low"] = ranked[0].Score.ToString();
                PvpRecorder.Decision(BotBody, null, ranked.Take(5).Select(Describe), blocked);
            }
            return false;
        }
    }
}
