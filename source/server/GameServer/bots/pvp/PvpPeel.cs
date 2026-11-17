using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>A healer or caster of the group that a shield fighter could guard.</summary>
    /// <param name="Attackers">Enemies in melee on it right now.</param>
    /// <param name="Distance">From the shield fighter.</param>
    /// <param name="IsCurrent">The one it guards already.</param>
    /// <param name="GuardedByAnother">Another shield fighter of the group has it.</param>
    public readonly record struct PvpWardView(int Id, PvpBuffArchetype Archetype, int Attackers, int HealthPercent,
        int Distance, bool IsCurrent, bool GuardedByAnother);

    /// <summary>An enemy a shield fighter could go for.</summary>
    /// <param name="OnWard">In melee on the groupmate it guards.</param>
    /// <param name="OnBackline">In melee on another healer or caster of the group.</param>
    /// <param name="OnSelf">In melee on the shield fighter itself.</param>
    /// <param name="CanStun">Neither stunned nor immune: Slam would land.</param>
    /// <param name="FromWard">Distance from the guarded groupmate; guard only works beside it.</param>
    /// <param name="IsCurrent">The one it is on already.</param>
    public readonly record struct PvpPeelView(int Id, bool OnWard, bool OnBackline, bool OnSelf, bool IsKill, bool CanStun,
        bool Mezzed, int FromWard, bool IsCurrent);

    /// <summary>
    /// What a shield fighter weighs (docs/PVP-8V8-ROADMAP.md, M6.3). Part of <see cref="PvpWeights"/>,
    /// so the weights file can change any of it and every recording carries the values used.
    /// </summary>
    public sealed class PvpPeelWeights
    {
        public int WardIsHealer { get; set; } = 40;
        public int WardIsCaster { get; set; } = 20;
        /// <summary>For each enemy in melee on that groupmate.</summary>
        public int WardPerAttacker { get; set; } = 35;
        public double WardPerMissingHealth { get; set; } = 0.3;
        /// <summary>Keeps guard from hopping between two groupmates in the same trouble.</summary>
        public int WardIsCurrent { get; set; } = 20;
        public int WardGuardedByAnother { get; set; } = -60;
        public double WardPerHundredUnitsAway { get; set; } = -3;

        /// <summary>An enemy hitting the guarded groupmate: the reason the shield fighter is there.</summary>
        public int OnWard { get; set; } = 100;
        public int OnBackline { get; set; } = 60;
        public int OnSelf { get; set; } = 40;
        /// <summary>Helping with the kill; it loses to distance, because guard stops working away from the ward.</summary>
        public int KillTarget { get; set; } = 55;
        /// <summary>Slam would land on an enemy that is on the backline.</summary>
        public int CanStun { get; set; } = 20;
        /// <summary>Waking a mezzed enemy that is not the kill target.</summary>
        public int Mezzed { get; set; } = -120;
        public double PerHundredUnitsFromWard { get; set; } = -4;
        public int IsCurrent { get; set; } = 15;
        /// <summary>With no enemy scoring this much the shield fighter stays beside its ward.</summary>
        public int MinScore { get; set; } = 30;
        /// <summary>How close "beside its ward" is; guard reaches 256 units.</summary>
        public int StayWithin { get; set; } = 150;
        /// <summary>Enemies farther than this from the ward are not looked at.</summary>
        public int ConsiderRange { get; set; } = 1_500;
        /// <summary>An enemy counts as in melee on somebody within this distance of them.</summary>
        public int MeleeReach { get; set; } = 350;
    }

    /// <summary>
    /// The shield fighter's two choices, both by score: which healer or caster it guards, and
    /// which enemy it goes for, if any. The ten-match runs of 2026-11-17 showed why: 80 attacks a
    /// match landed on the losing side's healers and casters, who were then interrupted on most
    /// of their free turns, while every fighter was away on the kill target.
    /// </summary>
    public static class PvpPeel
    {
        public static double Score(PvpWardView ward, PvpPeelWeights weights) =>
            (ward.Archetype switch { PvpBuffArchetype.Healer => weights.WardIsHealer, PvpBuffArchetype.Caster => weights.WardIsCaster, _ => 0 })
            + ward.Attackers * weights.WardPerAttacker
            + (100 - ward.HealthPercent) * weights.WardPerMissingHealth
            + (ward.IsCurrent ? weights.WardIsCurrent : 0)
            + (ward.GuardedByAnother ? weights.WardGuardedByAnother : 0)
            + ward.Distance / 100.0 * weights.WardPerHundredUnitsAway;

        /// <summary>The groupmate to guard; -1 when there is none.</summary>
        public static int ChooseWard(IReadOnlyList<PvpWardView> wards, PvpPeelWeights weights) =>
            wards.Count == 0 ? -1 : wards.OrderByDescending(ward => Score(ward, weights)).ThenBy(ward => ward.Id).First().Id;

        public static double Score(PvpPeelView enemy, PvpPeelWeights weights)
        {
            // The strongest reason counts, not the sum: an enemy on the ward is not a better
            // target for also being the kill target.
            double reason = double.MinValue;
            if (enemy.OnWard) reason = weights.OnWard;
            if (enemy.OnBackline && weights.OnBackline > reason) reason = weights.OnBackline;
            if (enemy.IsKill && weights.KillTarget > reason) reason = weights.KillTarget;
            if (enemy.OnSelf && weights.OnSelf > reason) reason = weights.OnSelf;
            if (reason == double.MinValue)
                return double.MinValue;
            return reason
                + (enemy.CanStun && (enemy.OnWard || enemy.OnBackline) ? weights.CanStun : 0)
                + (enemy.Mezzed && !enemy.IsKill ? weights.Mezzed : 0)
                + (enemy.IsCurrent ? weights.IsCurrent : 0)
                + enemy.FromWard / 100.0 * weights.PerHundredUnitsFromWard;
        }

        /// <summary>The enemy to go for; -1 to stay beside the ward.</summary>
        public static int ChooseTarget(IReadOnlyList<PvpPeelView> enemies, PvpPeelWeights weights, out double score)
        {
            score = 0;
            if (enemies.Count == 0)
                return -1;
            PvpPeelView best = enemies.OrderByDescending(enemy => Score(enemy, weights)).ThenBy(enemy => enemy.Id).First();
            score = Score(best, weights);
            return score >= weights.MinScore ? best.Id : -1;
        }

        /// <summary>What the choice is called in the recording.</summary>
        public static string Describe(PvpPeelView enemy) =>
            enemy.OnWard || enemy.OnBackline ? "Peel" : enemy.IsKill ? "Assist" : "Defend";
    }
}
