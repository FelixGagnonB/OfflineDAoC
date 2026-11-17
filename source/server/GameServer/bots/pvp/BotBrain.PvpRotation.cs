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
                blocked["previous cast lost"] = $"{lost.Name} after {GameLoop.GameLoopTime - _pvpLastCastTick} ms; state {FSM?.GetCurrentState()?.StateType}, " +
                    $"moving {Body.IsMoving}, interrupted {Body.IsBeingInterrupted}, attacking {Body.IsAttacking}";
            _pvpLastCast = null;

            // The best castable spell of each kind this class uses.
            List<Spell> known = (Body.HarmfulSpells ?? []).Concat(Body.InstantHarmfulSpells ?? [])
                .Concat(BotBody.CrowdControlSpells ?? []).Concat(BotBody.InstantCrowdControlSpells ?? [])
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
            int tooFar = 0;
            var options = new List<PvpOption>();
            var sight = new Dictionary<GameBot, bool>();
            // Whom each area mez would put to sleep, by option (kind and primary target).
            var caughtBy = new Dictionary<(PvpActionKind, int), List<GameBot>>();
            foreach ((PvpActionKind kind, Spell spell) in spells)
            {
                bool mez = kind is PvpActionKind.Mez or PvpActionKind.MezArea;
                // A spell centred on the caster reaches as far as its radius.
                bool aroundCaster = spell.Target == eSpellTarget.SELF || spell.Range <= 0;
                int range = aroundCaster ? spell.Radius : spell.CalculateEffectiveRange(Body);
                int cost = Body.MaxMana > 0 ? 100 * BotBody.PowerCost(spell) / Body.MaxMana : 0;
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
                    bool hasEffect = kind switch
                    {
                        // Another mezzer already has this one in hand.
                        PvpActionKind.Mez or PvpActionKind.MezArea => PvpMezzedOrImmune(enemy) || BotPvpCrowdControl.Protected(Body, enemy),
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

            List<(PvpOption Option, int Score)> ranked = PvpActionChoice.Rank(characterClass, options, weights);
            object Describe((PvpOption Option, int Score) scored) => new
            {
                kind = scored.Option.Kind.ToString(), d = enemies[scored.Option.TargetId].Name, score = scored.Score, walk = scored.Option.Short,
                caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
            };

            foreach ((PvpOption Option, int Score) scored in ranked.TakeWhile(scored => scored.Score >= weights.MinScore))
            {
                Spell spell = spells[scored.Option.Kind];
                GameBot aim = enemies[scored.Option.TargetId];
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
                if (mez && !BotPvpCrowdControl.Reserve(BotBody, aim, spell.CastTime + 2_000))
                    continue;
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
                    (_pvpLastCast, _pvpLastCastTick) = (spell, GameLoop.GameLoopTime);
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
                if (mez)
                    BotPvpCrowdControl.Release(BotBody, aim);
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
