using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What a group knows about one enemy when it picks whom to kill.</summary>
    /// <param name="FromItsHealers">How far it stands from the middle of its own group's healers; 0 when none of them is known.</param>
    public readonly record struct PvpTargetView(int Id, PvpBuffArchetype Archetype, int HealthPercent, bool Mezzed, int Distance, int FromItsHealers = 0);

    /// <summary>
    /// What the choice of a kill target weighs. None of it is a rule (the owner, 2026-11-18:
    /// "maybe sometimes a good target would be a melee while other times it might be a caster"):
    /// a batch that draws its weights at random moves these like all the others.
    /// </summary>
    public sealed class PvpKillWeights
    {
        public int OnHealer { get; set; } = 200;
        public int OnCaster { get; set; } = 150;
        public int OnFighter { get; set; } = 50;
        /// <summary>For each percent of health the enemy has lost.</summary>
        public double PerMissingHealthPercent { get; set; } = 1;
        /// <summary>Hitting a mezzed enemy wakes it.</summary>
        public int Mezzed { get; set; } = -120;
        /// <summary>For each hundred units from the middle of the group's damage dealers: running time.</summary>
        public double PerHundredUnits { get; set; } = -2.5;
        /// <summary>
        /// For each hundred units the enemy stands from its own healers, up to 2,000: out of
        /// position, it is harder to heal and easier to reach. The owner, 2026-11-17: the kill
        /// target is whoever is easiest to kill and out of position; a caster or healer that came
        /// too far forward is jumped.
        /// </summary>
        public double PerHundredUnitsFromItsHealers { get; set; } = 2;
        /// <summary>The current target is kept unless another scores this much higher.</summary>
        public int SwitchMargin { get; set; } = 60;
        /// <summary>Enemies farther than this from the damage dealers are not called.</summary>
        public int MaxDistance { get; set; } = 3_500;
    }

    /// <summary>
    /// The group's kill target (docs/PVP-8V8-ROADMAP.md, M5.5): one enemy that every damage
    /// dealer attacks until it is dead. The recorded old-brain fights had each fighter on seven to
    /// nine targets in two and a half minutes and nobody died.
    /// </summary>
    public static class PvpKillTarget
    {
        private static readonly PvpKillWeights Defaults = new();
        private const int ExposureCap = 2_000;

        /// <summary>
        /// Higher is a better kill. Healers first, then casters: they are the softest and matter
        /// most. A wounded enemy is closer to dead. A mezzed one is left alone while there is
        /// anyone else, because hitting it wakes it. Far ones cost running time.
        /// </summary>
        public static int Score(PvpTargetView enemy, PvpKillWeights weights = null)
        {
            weights ??= Defaults;
            return (enemy.Archetype switch { PvpBuffArchetype.Healer => weights.OnHealer, PvpBuffArchetype.Caster => weights.OnCaster, _ => weights.OnFighter })
                + (int)(weights.PerMissingHealthPercent * (100 - enemy.HealthPercent))
                + (enemy.Mezzed ? weights.Mezzed : 0)
                + (int)(weights.PerHundredUnits * enemy.Distance / 100.0)
                + (int)(weights.PerHundredUnitsFromItsHealers * Math.Min(enemy.FromItsHealers, ExposureCap) / 100.0);
        }

        /// <summary>The enemy to kill now, given the one being killed already (-1 for none).</summary>
        public static int Choose(IReadOnlyList<PvpTargetView> enemies, int currentId, PvpKillWeights weights = null)
        {
            weights ??= Defaults;
            List<PvpTargetView> reachable = enemies.Where(enemy => enemy.Distance <= weights.MaxDistance).ToList();
            if (reachable.Count == 0)
                return -1;
            PvpTargetView best = reachable.OrderByDescending(enemy => Score(enemy, weights)).ThenBy(enemy => enemy.Id).First();
            int current = reachable.FindIndex(enemy => enemy.Id == currentId);
            return current >= 0 && Score(best, weights) < Score(reachable[current], weights) + weights.SwitchMargin ? currentId : best.Id;
        }
    }
}
