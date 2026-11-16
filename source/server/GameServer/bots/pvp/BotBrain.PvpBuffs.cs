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
