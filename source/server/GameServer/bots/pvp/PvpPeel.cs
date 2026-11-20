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
    /// <param name="Coming">An enemy fighter running at a healer or caster of the group, not yet in melee on it.</param>
    /// <param name="Stunned">Stunned right now: it does nothing until that ends.</param>
    public readonly record struct PvpPeelView(int Id, bool OnWard, bool OnBackline, bool OnSelf, bool IsKill, bool CanStun,
        bool Mezzed, int FromWard, bool IsCurrent, bool Coming = false, bool Stunned = false);

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
        /// <summary>Counted only for a groupmate with an enemy in melee on it: guard blocks blows, and does nothing for one that spells are hurting.</summary>
        public double WardPerMissingHealth { get; set; } = 0.3;
        /// <summary>Keeps guard from hopping between two groupmates in the same trouble.</summary>
        public int WardIsCurrent { get; set; } = 20;
        public int WardGuardedByAnother { get; set; } = -60;
        public double WardPerHundredUnitsAway { get; set; } = -3;

        /// <summary>An enemy hitting the guarded groupmate: the reason the shield fighter is there.</summary>
        public int OnWard { get; set; } = 100;
        public int OnBackline { get; set; } = 60;
        /// <summary>
        /// An enemy fighter running at a healer or caster of the group and within
        /// <see cref="InterceptWithin"/> of it: the shield fighter goes to meet it. The owner,
        /// watching Hibernia against Midgard on 2026-11-18: "the Skald simply ran past everything
        /// to reach both Druids", with a Hero and two Blademasters there. Until then nothing
        /// looked at an enemy before it was within melee reach of its victim; in the recordings a
        /// fighter that reached a healer or caster in the opening was first stunned, rooted or
        /// mezzed a median of two to five seconds after it arrived, and a third not once in fifteen.
        /// </summary>
        public int Coming { get; set; } = 65;
        /// <summary>0 = nobody is met on the way in.</summary>
        public int InterceptWithin { get; set; } = 1_200;
        public int OnSelf { get; set; } = 40;
        /// <summary>Helping with the kill, which is what a shield fighter does when nobody is on or coming for its healers and casters.</summary>
        public int KillTarget { get; set; } = 55;
        /// <summary>
        /// What distance from the ward costs the kill target, in place of
        /// <see cref="PerHundredUnitsFromWard"/>. That was -4, so a shield fighter helped with the
        /// kill only within 625 units of its ward, and once the two back lines stood 1,250 to
        /// 1,580 apart it never did: the Paladin had nobody to go for during 52% of its fights
        /// and stood beside its ward. The owner, watching on 2026-11-19: with one Blademaster
        /// stunned and the other mezzed the Paladin "could have went on another duty", and it
        /// stood beside a caster that enemy casters were hitting, "which was useless": guard
        /// blocks blows, not spells. An enemy on, or running at, the back line still scores
        /// higher, so the shield fighter comes back for it.
        ///
        /// Only while no enemy that is awake and free is on, or running at, any healer or caster
        /// of the group: then distance costs the kill target what it always did. Seen in the
        /// pictures of a Midgard fight the same day: the Warrior on the enemy's Druid, 800 units
        /// off, while a Blademaster killed the Healer it did not guard. That Blademaster scored
        /// 44 (on another member of the back line, 900 units from the Warrior's own ward) against
        /// 56 for the kill.
        /// </summary>
        public double KillPerHundredUnitsFromWard { get; set; } = -1;
        /// <summary>Slam would land on an enemy that is on the backline.</summary>
        public int CanStun { get; set; } = 20;
        /// <summary>Waking a mezzed enemy that is not the kill target.</summary>
        public int Mezzed { get; set; } = -120;
        /// <summary>
        /// An enemy that is stunned is dealt with for as long as that lasts: the next one comes
        /// first. The owner, watching Hibernia against Albion on 2026-11-19: two Blademasters on
        /// a Cleric, the Paladin "slam 1 and kept attacking it. Not handling the 2nd". The one it
        /// was on kept 115 (on the ward, and its current target) against 120 for the other, and
        /// the other being 125 units farther from the ward was enough to lose. In the 48 matches
        /// before, a shield fighter was on a stunned enemy while another enemy fighter was free
        /// on its healers and casters for a quarter of the time it spent on stunned enemies.
        /// </summary>
        public int Stunned { get; set; } = -40;
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
            + (ward.Attackers > 0 ? (100 - ward.HealthPercent) * weights.WardPerMissingHealth : 0)
            + (ward.IsCurrent ? weights.WardIsCurrent : 0)
            + (ward.GuardedByAnother ? weights.WardGuardedByAnother : 0)
            + ward.Distance / 100.0 * weights.WardPerHundredUnitsAway;

        /// <summary>The groupmate to guard; -1 when there is none.</summary>
        public static int ChooseWard(IReadOnlyList<PvpWardView> wards, PvpPeelWeights weights) =>
            wards.Count == 0 ? -1 : wards.OrderByDescending(ward => Score(ward, weights)).ThenBy(ward => ward.Id).First().Id;

        public static double Score(PvpPeelView enemy, PvpPeelWeights weights, bool backlineThreatened = false)
        {
            // The strongest reason counts, not the sum: an enemy on the ward is not a better
            // target for also being the kill target.
            double reason = double.MinValue;
            if (enemy.OnWard) reason = weights.OnWard;
            if (enemy.OnBackline && weights.OnBackline > reason) reason = weights.OnBackline;
            if (enemy.Coming && weights.Coming > reason) reason = weights.Coming;
            if (enemy.IsKill && weights.KillTarget > reason) reason = weights.KillTarget;
            if (enemy.OnSelf && weights.OnSelf > reason) reason = weights.OnSelf;
            if (reason == double.MinValue)
                return double.MinValue;
            return reason
                + (enemy.CanStun && (enemy.OnWard || enemy.OnBackline || enemy.Coming) ? weights.CanStun : 0)
                + (enemy.Mezzed && !enemy.IsKill ? weights.Mezzed : 0)
                + (enemy.Stunned && !enemy.IsKill ? weights.Stunned : 0)
                + (enemy.IsCurrent ? weights.IsCurrent : 0)
                + enemy.FromWard / 100.0 * (!backlineThreatened && enemy.IsKill && !enemy.OnWard && !enemy.OnBackline && !enemy.Coming && !enemy.OnSelf
                    ? weights.KillPerHundredUnitsFromWard : weights.PerHundredUnitsFromWard);
        }

        /// <summary>True when an enemy that is awake and free is on, or running at, a healer or caster of the group.</summary>
        public static bool Threatened(IReadOnlyList<PvpPeelView> enemies) =>
            enemies.Any(enemy => (enemy.OnWard || enemy.OnBackline || enemy.Coming) && !enemy.Mezzed && !enemy.Stunned);

        /// <summary>The enemy to go for; -1 to stay beside the ward.</summary>
        public static int ChooseTarget(IReadOnlyList<PvpPeelView> enemies, PvpPeelWeights weights, out double score)
        {
            score = 0;
            if (enemies.Count == 0)
                return -1;
            bool threatened = Threatened(enemies);
            PvpPeelView best = enemies.OrderByDescending(enemy => Score(enemy, weights, threatened)).ThenBy(enemy => enemy.Id).First();
            score = Score(best, weights, threatened);
            return score >= weights.MinScore ? best.Id : -1;
        }

        /// <summary>What the choice is called in the recording.</summary>
        public static string Describe(PvpPeelView enemy) =>
            enemy.OnWard || enemy.OnBackline ? "Peel" : enemy.Coming ? "Intercept" : enemy.IsKill ? "Assist" : "Defend";
    }
}
