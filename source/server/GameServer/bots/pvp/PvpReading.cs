using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What a group is trying to do right now (docs/PVP-8V8-PLAYBOOK.md, section 3). An order shifts weights; it is not a script.</summary>
    public enum PvpOrder
    {
        /// <summary>Close, mez, kill the called target. The default.</summary>
        Engage,
        /// <summary>Before the fight: stand and let the enemy come into the group's longer mez range.</summary>
        Hold,
        /// <summary>Go onto them: their healers are down or busy and their fighters held.</summary>
        Push,
        /// <summary>Fall back: healers and casters keep more distance, and the fighters turn on whoever follows.</summary>
        Extend,
    }

    /// <summary>Part of <see cref="PvpWeights"/>: how a group reads the fight into an order.</summary>
    public sealed class PvpOrderWeights
    {
        /// <summary>False keeps every group on Engage.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>An order stands at least this long.</summary>
        public int KeepSeconds { get; set; } = 3;
        /// <summary>What the order in force scores above its reading, so the group does not flip on every small change.</summary>
        public int KeepCurrent { get; set; } = 15;

        public int PushBase { get; set; } = -45;
        /// <summary>Times the share of the enemy's healers that are dead, asleep, stunned or interrupted.</summary>
        public int PushPerEnemyHealerDown { get; set; } = 60;
        /// <summary>Times the share of the enemy's living fighters that are mezzed, stunned, rooted or slowed.</summary>
        public int PushPerEnemyFighterHeld { get; set; } = 30;
        /// <summary>For each member the group has alive more than the enemy (less than, when negative).</summary>
        public int PushPerMemberAhead { get; set; } = 10;

        public int ExtendBase { get; set; } = -45;
        /// <summary>Times the share of the group's own healers that are alive but cannot heal. Dead healers do not count: there is nobody left to protect.</summary>
        public int ExtendPerOwnHealerBusy { get; set; } = 60;
        /// <summary>Times the share of the enemy's living fighters that are free.</summary>
        public int ExtendPerEnemyFighterFree { get; set; } = 30;
        /// <summary>For each of the group's healers and casters with a free enemy fighter in melee on it.</summary>
        public int ExtendPerBacklineUnderAttack { get; set; } = 15;

        /// <summary>
        /// On incoming a group holds when its longest mez outranges the enemy's by at least this
        /// much (the Sorcerer's 1,875 against 1,500 for every Healer and Bard mez). 0 turns holding off.
        /// </summary>
        public int HoldRangeAdvantage { get; set; } = 200;
        /// <summary>A hold ends after this long: the enemy is holding too, or not coming.</summary>
        public int HoldMaxSeconds { get; set; } = 10;
        /// <summary>A hold ends when an enemy is this close to a member: they are on the group.</summary>
        public int HoldBreakDistance { get; set; } = 1_200;

        /// <summary>Under Extend, healers and casters want this many times the usual distance from enemy fighters.</summary>
        public double ExtendSafeFactor { get; set; } = 1.4;
        /// <summary>Under Push they accept this share of it, to keep up with their fighters.</summary>
        public double PushSafeFactor { get; set; } = 0.6;
        /// <summary>Under Extend the group's fighters leave the kill target for an enemy fighter that is on their healers and casters.</summary>
        public bool ExtendFightersTurnOnPursuers { get; set; } = true;
    }

    /// <summary>One member as the reading counts it.</summary>
    public readonly record struct PvpMemberState(bool Fighter, bool Healer, bool Alive, bool Held, bool Slowed, bool Interrupted);

    /// <summary>
    /// One group's state in the three readings the owner named (2026-11-18): "how the enemies'
    /// melee are controlled (stun, snare, mez), how casters are interrupted and how busy the
    /// opposite healers are". Shares are 0 to 1; a share of nobody is 0.
    /// </summary>
    public readonly record struct PvpReading(int Alive, int Fighters, int FightersHeld, int Casters, int CastersFree, int Healers, int HealersAlive, int HealersFree)
    {
        /// <summary>Share of the living fighters that are mezzed, stunned, rooted or slowed.</summary>
        public double FightersHeldShare => Fighters == 0 ? 0 : (double)FightersHeld / Fighters;
        /// <summary>Share of the living healers and casters that can cast.</summary>
        public double CastersFreeShare => Casters == 0 ? 0 : (double)CastersFree / Casters;
        /// <summary>Share of all the group's healers, dead ones included, that can heal.</summary>
        public double HealersFreeShare => Healers == 0 ? 0 : (double)HealersFree / Healers;
        /// <summary>Share of all the group's healers that are alive and cannot heal.</summary>
        public double HealersBusyShare => Healers == 0 ? 0 : (double)(HealersAlive - HealersFree) / Healers;

        public static PvpReading Of(IEnumerable<PvpMemberState> members)
        {
            int alive = 0, fighters = 0, held = 0, casters = 0, free = 0, healers = 0, healersAlive = 0, healersFree = 0;
            foreach (PvpMemberState member in members)
            {
                if (member.Healer)
                    healers++;
                if (!member.Alive)
                    continue;
                alive++;
                if (member.Fighter)
                {
                    fighters++;
                    if (member.Held || member.Slowed)
                        held++;
                    continue;
                }
                bool canCast = !member.Held && !member.Interrupted;
                casters++;
                if (canCast)
                    free++;
                if (member.Healer)
                {
                    healersAlive++;
                    if (canCast)
                        healersFree++;
                }
            }
            return new PvpReading(alive, fighters, held, casters, free, healers, healersAlive, healersFree);
        }
    }

    /// <summary>
    /// Choosing the group's order from the two readings (docs/PVP-8V8-PLAYBOOK.md, section 3).
    /// Engage scores 0; Push and Extend have to beat it. Hold is for the time before the fight.
    /// Regroup is not built yet.
    /// </summary>
    public static class PvpOrders
    {
        public static double Score(PvpOrder order, PvpReading own, PvpReading theirs, int backlineUnderAttack, PvpOrderWeights weights) => order switch
        {
            PvpOrder.Push => weights.PushBase
                + weights.PushPerEnemyHealerDown * (1 - theirs.HealersFreeShare)
                + weights.PushPerEnemyFighterHeld * theirs.FightersHeldShare
                + weights.PushPerMemberAhead * (own.Alive - theirs.Alive),
            PvpOrder.Extend => weights.ExtendBase
                + weights.ExtendPerOwnHealerBusy * own.HealersBusyShare
                + weights.ExtendPerEnemyFighterFree * (theirs.Fighters == 0 ? 0 : 1 - theirs.FightersHeldShare)
                + weights.ExtendPerBacklineUnderAttack * backlineUnderAttack,
            _ => 0,
        };

        /// <summary>
        /// Before the fight: true while the group should stand and let the enemy walk into its
        /// longer mez range (owner, 2026-11-17: the Sorcerer's mez outranges the Healer's and the
        /// Bard's, and the realms open differently because of it).
        /// </summary>
        public static bool Holds(int ownMezRange, int theirMezRange, double secondsHeld, int nearestEnemy, PvpOrderWeights weights) =>
            weights.Enabled && weights.HoldRangeAdvantage > 0 && ownMezRange - theirMezRange >= weights.HoldRangeAdvantage &&
            secondsHeld < weights.HoldMaxSeconds && nearestEnemy > weights.HoldBreakDistance;

        /// <summary>The order for this second, given the one in force and how long it has stood.</summary>
        public static PvpOrder Choose(PvpReading own, PvpReading theirs, int backlineUnderAttack, PvpOrder current, double secondsInForce, PvpOrderWeights weights)
        {
            if (!weights.Enabled || theirs.Alive == 0 || own.Alive == 0)
                return PvpOrder.Engage;
            if (secondsInForce < weights.KeepSeconds)
                return current;
            PvpOrder best = PvpOrder.Engage;
            double bestScore = current == PvpOrder.Engage ? weights.KeepCurrent : 0;
            foreach (PvpOrder order in new[] { PvpOrder.Push, PvpOrder.Extend })
            {
                double score = Score(order, own, theirs, backlineUnderAttack, weights) + (order == current ? weights.KeepCurrent : 0);
                if (score > bestScore)
                    (best, bestScore) = (order, score);
            }
            return best;
        }
    }
}
