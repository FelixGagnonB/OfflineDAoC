using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DOL.GS;

namespace DOL.AI.Brain
{
    /// <summary>
    /// 8v8 bots hand out their concentration buffs from one plan for the whole group
    /// (<see cref="PvpBuffPlan"/>, docs/PVP-8V8-ROADMAP.md M1.7) instead of each bot buffing a
    /// random member with whatever it knows.
    /// </summary>
    public partial class BotBrain
    {
        private sealed class PvpGroupBuffPlan
        {
            public long ExpiresTick;
            public List<GameLiving> Members = [];
            public PvpBuffPlanResult Plan;
            public Dictionary<(int Caster, PvpBuffKind Kind), Spell> Spells = [];
        }

        // One plan per group (or per lone bot), rebuilt every few seconds so it follows deaths,
        // joins and members coming into range.
        private static readonly ConditionalWeakTable<object, PvpGroupBuffPlan> PvpBuffPlans = new();
        private const int PvpBuffPlanLifetimeMilliseconds = 5_000;

        /// <summary>The single-target concentration buff of each planned kind this bot casts best.</summary>
        public static Dictionary<PvpBuffKind, Spell> PvpBuffSpells(GameBot bot) =>
            (bot.MiscSpells ?? []).Concat(bot.InstantMiscSpells ?? [])
                .Where(spell => spell != null && spell.Level <= bot.Level)
                .Select(spell => (Spell: spell, Kind: PvpBuffPlan.KindOf(spell.SpellType, spell.Target, spell.Concentration)))
                .Where(entry => entry.Kind.HasValue)
                .GroupBy(entry => entry.Kind.Value)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.Spell.Value).First().Spell);

        /// <summary>
        /// Set by the scrim from spawn until first contact (owner's rule, 2026-11-17): before the
        /// fight the buffbot buffs the group, and a member casts only what is its own: buffs on
        /// itself, its pet, and its songs or chants. Buffing others starts with the fight.
        /// </summary>
        public bool PvpSelfBuffsOnly { get; set; }

        /// <summary>A buff on someone else, which a bot holds back while <see cref="PvpSelfBuffsOnly"/> is set.</summary>
        public static bool IsBuffOnOthers(eSpellTarget target, int duration, int concentration, bool pulsing) =>
            (duration > 0 || concentration > 0) && !pulsing &&
            target is not (eSpellTarget.SELF or eSpellTarget.PET or eSpellTarget.CONTROLLED);

        /// <summary>
        /// True for a cast that is not begun once the group's fight has started: a buff with a
        /// cast time on anybody, the caster included, and a healer's pet. The owner, watching on
        /// 2026-11-18: a Cabalist cast its bubble during a fight, "which should not happen". In
        /// the 150 matches of that day the two Clerics began eight buffs on others a match after
        /// contact (health regeneration, defensive proc), two seconds each; the Druids a pet
        /// three times, four seconds each; the Cabalist a speed buff on itself 1.7 times.
        /// A song or chant is not meant: those have their own routines.
        /// </summary>
        public static bool IsBuffHeldBackInFight(int castTime, bool pulsing, int duration, int concentration, bool healersPetSummon) =>
            castTime > 0 && !pulsing && (duration > 0 || concentration > 0 || healersPetSummon);

        /// <summary>
        /// The group's fight has begun: its squad has had its first blow, or an enemy is within
        /// reach of this bot. A healer standing far back is in the fight all the same; going only
        /// by its own distance to the enemy is what let the Clerics buff through it.
        /// </summary>
        private bool PvpGroupIsFighting() => BotBody?.IsPvpBot == true && (PvpSquad?.FightStarted == true || PvpFightIsOn());

        // Before the fight the buffbot has done it; during one, a damage add or resist buff on a
        // member is not worth the turn or the power, and with BuffsInFight off nothing with a
        // cast time is begun at all (the rebuff of a member who was raised included, for now).
        private bool HoldsBackPvpBuff(Spell spell)
        {
            if (spell == null || spell.IsHarmful)
                return false;
            if (PvpHeldBackInFight(spell))
                return true;
            // Power regeneration is cast on the group before the fight by whoever has it (owner,
            // 2026-11-19: "Sorc should cast that buff before the fight, for sure. All realms that
            // have a mana regen buff should cast it before fight"). It was held back with every
            // other buff on others, so nobody had it: a Theurgist was out of power 42% of its fights.
            if (spell.SpellType == eSpellType.PowerRegenBuff && !PvpGroupIsFighting())
                return false;
            return IsBuffOnOthers(spell.Target, spell.Duration, spell.Concentration, spell.IsPulsing) &&
                   (PvpSelfBuffsOnly || PvpFightIsOn());
        }

        private bool PvpIsHealer() => BotBody?.CharacterClass != null &&
            PvpBuffPlan.ArchetypeOf((eCharacterClass)BotBody.CharacterClass.ID) == PvpBuffArchetype.Healer;

        /// <summary>
        /// True for a spell this 8v8 bot does not begin once its group's fight has started
        /// (<see cref="IsBuffHeldBackInFight"/>). A summon is only held back for a healer: a
        /// Theurgist's pets and a Cabalist's simulacrum are their damage.
        /// </summary>
        private bool PvpHeldBackInFight(Spell spell)
        {
            if (spell == null || spell.IsHarmful || !PvpGroupIsFighting() || PvpWeights.Of(BotBody).BuffsInFight)
                return false;
            if (spell.SpellType.ToString().StartsWith("Summon", StringComparison.Ordinal))
                return IsBuffHeldBackInFight(spell.CastTime, false, 0, 0, PvpIsHealer() && AutonomousPetSupport.IsMainPetSummon(spell.SpellType));
            // Speed is for the run to the fight. It is a pulsing spell, so the rule below lets it through; in the
            // night of 2026-11-19 a Theurgist, Sorcerer, Runemaster or Healer still stopped to cast it about once
            // a match, with the nearest enemy 600 to 1,300 units away.
            if (spell.SpellType == eSpellType.SpeedEnhancement && spell.CastTime > 0)
                return true;
            return IsBuffHeldBackInFight(spell.CastTime, spell.IsPulsing, spell.Duration, spell.Concentration, false);
        }

        /// <summary>
        /// Pet upkeep in a fight (the summon, the pet's heals and its buffs have a path of their
        /// own, AutonomousPetSupport.Maintain). The owner, watching on 2026-11-18: "the Druid
        /// recast its pet mid fight and both Druid and Bard stopped to buff it." A healer does
        /// none of it once the fight has started; anybody else still calls a lost pet again but
        /// does not stop to buff it. Null = no limit (not an 8v8 fight).
        /// </summary>
        private bool PvpSkipsPetUpkeep() => PvpIsHealer() && PvpGroupIsFighting() && !PvpWeights.Of(BotBody).BuffsInFight;

        private Func<Spell, bool> PvpPetUpkeepFilter() =>
            PvpGroupIsFighting() && !PvpWeights.Of(BotBody).BuffsInFight ? spell => !PvpHeldBackInFight(spell) : null;

        /// <summary>True for a buff that only the group plan may cast: every generic buff path skips it.</summary>
        private bool IsPlannedPvpBuff(Spell spell) => spell != null && BotBody?.IsPvpBot == true &&
            PvpBuffPlan.KindOf(spell.SpellType, spell.Target, spell.Concentration).HasValue;

        private PvpGroupBuffPlan CurrentPvpBuffPlan()
        {
            object scope = (object)Body.Group ?? Body;
            PvpGroupBuffPlan cached = PvpBuffPlans.GetOrCreateValue(scope);
            lock (cached)
            {
                long now = GameLoop.GameLoopTime;
                if (cached.Plan != null && now < cached.ExpiresTick)
                    return cached;

                List<GameLiving> members = Body.Group?.GetMembersInTheGroup().Where(member => member.IsAlive).ToList() ?? [Body];
                var planMembers = new List<PvpBuffMember>();
                var offers = new List<PvpBuffOffer>();
                var spells = new Dictionary<(int, PvpBuffKind), Spell>();
                for (int id = 0; id < members.Count; id++)
                {
                    GameLiving member = members[id];
                    // GamePlayer does not implement IGamePlayer, so a human member is read separately.
                    int classId = member switch
                    {
                        GamePlayer { CharacterClass: { } playerClass } => playerClass.ID,
                        IGamePlayer { CharacterClass: { } botClass } => botClass.ID,
                        _ => 0,
                    };
                    // Only 8v8 bots cast from the plan; a human member is buffed but never told what to cast.
                    GameBot caster = member as GameBot is { IsPvpBot: true } pvpBot ? pvpBot : null;
                    // Free concentration, not the maximum: what is already cast stays where it is.
                    planMembers.Add(new(id, PvpBuffPlan.ArchetypeOf((eCharacterClass)classId), caster?.Concentration ?? 0));
                    if (caster == null || caster.MaxConcentration > 100_000)
                        continue;
                    foreach ((PvpBuffKind kind, Spell spell) in PvpBuffSpells(caster))
                    {
                        offers.Add(new(id, kind, (int)spell.Value, spell.Concentration));
                        spells[(id, kind)] = spell;
                    }
                }

                cached.Members = members;
                cached.Spells = spells;
                // Any caster's spell of a kind tells whether a member already holds that kind.
                Dictionary<PvpBuffKind, Spell> probe = spells.GroupBy(pair => pair.Key.Item2)
                    .ToDictionary(group => group.Key, group => group.First().Value);
                cached.Plan = PvpBuffPlan.Build(planMembers, offers,
                    (target, kind) => probe.TryGetValue(kind, out Spell spell) && LivingHasEffect(members[target], spell));
                cached.ExpiresTick = now + PvpBuffPlanLifetimeMilliseconds;
                return cached;
            }
        }

        /// <summary>Casts this bot's next missing buff from the group plan. True when a cast started.</summary>
        private bool TryCastPlannedPvpBuff()
        {
            if (PvpSelfBuffsOnly)
                return false;
            PvpGroupBuffPlan plan = CurrentPvpBuffPlan();
            int me = plan.Members.IndexOf(Body);
            if (me < 0)
                return false;

            foreach (PvpBuffAssignment assignment in plan.Plan.Assignments)
            {
                if (assignment.Caster != me || !plan.Spells.TryGetValue((me, assignment.Kind), out Spell spell))
                    continue;
                GameLiving target = plan.Members[assignment.Target];
                if (target == null || !target.IsAlive || target.CurrentRegion != Body.CurrentRegion ||
                    !Body.IsWithinRadius(target, Math.Max(350, spell.CalculateEffectiveRange(Body))) ||
                    LivingHasEffect(target, spell) ||
                    !BotBody.CanAffordConcentration(spell) || Body.Mana < BotBody.PowerCost(spell))
                    continue;

                GameObject previousTarget = Body.TargetObject;
                if (spell.CastTime > 0)
                {
                    Body.StopMovingOnPath();
                    Body.StopMoving();
                }
                Body.TargetObject = target;
                bool cast = CastCoordinatedBuff(spell, false);
                Body.TargetObject = previousTarget;
                if (cast)
                    return true;
            }
            return false;
        }

        /// <summary>The group's buff shortfall and assignments, for the scrim status report.</summary>
        public (IReadOnlyList<string> Assignments, IReadOnlyList<string> Unfilled) DescribePvpBuffPlan()
        {
            PvpGroupBuffPlan plan = CurrentPvpBuffPlan();
            string Name(int id) => plan.Members[id].Name;
            return (plan.Plan.Assignments.Select(a => $"{Name(a.Caster)} -> {Name(a.Target)}: {a.Kind} {a.Value}").ToList(),
                plan.Plan.Unfilled.Select(u => $"{Name(u.Target)}: {u.Kind}").ToList());
        }
    }
}
