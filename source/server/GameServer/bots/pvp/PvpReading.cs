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
        /// <summary>Stop chasing and come back together: the fighters return to within reach of the healers.</summary>
        Regroup,
    }

    /// <summary>Part of <see cref="PvpWeights"/>: how a group reads the fight into an order.</summary>
    public sealed class PvpOrderWeights
    {
        /// <summary>False keeps every group on Engage.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// An order stands at least this long. Tried at 8 on 2026-11-19, because a group changed
        /// its order 3.8 times a minute and an order lasted a median of five to seven seconds (in
        /// the pictures of one fight Midgard's fighters went to and fro between their kill target
        /// and their own healers for a minute and a half). In twelve matches the changes fell only
        /// to 3.3 a minute, and a fighter running at the healers was stopped within three seconds
        /// 38% of the time against 58%: a group that had to wait to answer. Back at 3; the to and
        /// fro wants another cure than time.
        /// </summary>
        public int KeepSeconds { get; set; } = 3;
        /// <summary>
        /// What the order in force scores above its reading, so the group does not flip on every
        /// small change. Not raised with <see cref="KeepSeconds"/>: at 25 a group that had gone to
        /// Extend never left it while the enemy's fighters were free, and one whose fighters were
        /// strung out no longer regrouped.
        /// </summary>
        public int KeepCurrent { get; set; } = 15;

        public int PushBase { get; set; } = -45;
        /// <summary>Times the share of the enemy's healers that are dead, asleep, stunned or interrupted.</summary>
        public int PushPerEnemyHealerDown { get; set; } = 60;
        /// <summary>Times the share of the enemy's living fighters that are mezzed, stunned, rooted or slowed.</summary>
        public int PushPerEnemyFighterHeld { get; set; } = 30;
        /// <summary>For each member the group has alive more than the enemy (less than, when negative).</summary>
        public int PushPerMemberAhead { get; set; } = 10;

        /// <summary>
        /// A group with this many casters or more plays from its back line (0: none does): it
        /// does not push, and in Engage as under Extend its fighters turn on whoever is on its
        /// healers and casters and otherwise keep to their reach (<see cref="ExtendFightersStayWithin"/>):
        /// the enemy that comes too far is theirs, and the rest is its casters'. Of the groups of
        /// 2026-11-26 that is Albion's main one (Sorcerer, Theurgist, Cabalist). The owner, on its
        /// Cleric in that group: "i feel like albion fighter / minstrel are pushing way too hard on
        /// contact. we are mostly caster, we probably focus on extend/kite and kill overextending.
        /// i'm mostly always out of range to heal either". In its evening of eight fights the
        /// group was under its own Push for 42% of its fighting time, and its Armsman and its
        /// Minstrel were more than 2,000 units from their player for a fifth of theirs (the
        /// Armsman a median 1,849 in the first fifteen seconds of a fight).
        /// </summary>
        public int CasterGroupFrom { get; set; } = 3;

        /// <summary>
        /// Only a group with a player in it plays from its back line; a group of bots pushes as before. Measured in 24
        /// matches of bots in lanes, 2026-11-26 (Albion's main group against Midgard's and Hibernia's, half of them with
        /// the rule): its Armsman and Minstrel stood a mean 691 and 641 units from the middle of their healers and
        /// casters where they had stood 1,262 and 1,186, and were beyond 2,000 for 1% of their time where it had been 9
        /// and 7%: what was asked. And the group won 7 of 12 where without the rule it won 11 of 12 (32 of 36 with the
        /// twenty-four of a batch before it), lost 4.0 members a match for 1.3, its Clerics one a match for a quarter of
        /// one, and its matches took 292 seconds for 216: among bots, whose healers go with their fighters, the push is
        /// what wins. A player that heals does not go with them. So the rule is for the player's group, and what it does
        /// there is for the owner to say.
        /// </summary>
        public bool CasterGroupOnlyWithAPlayer { get; set; } = true;

        /// <summary>See <see cref="CasterGroupFrom"/> and <see cref="CasterGroupOnlyWithAPlayer"/>.</summary>
        public bool PlaysFromItsBackLine(int casters, bool hasAPlayer = true) =>
            CasterGroupFrom > 0 && casters >= CasterGroupFrom && (hasAPlayer || !CasterGroupOnlyWithAPlayer);

        /// <summary>The order of a group that plays from its back line: never Push.</summary>
        public static PvpOrder WithoutPush(PvpOrder order, bool playsFromItsBackLine) =>
            playsFromItsBackLine && order == PvpOrder.Push ? PvpOrder.Engage : order;

        public int ExtendBase { get; set; } = -45;
        /// <summary>Times the share of the group's own healers that are alive but cannot heal. Dead healers do not count: there is nobody left to protect.</summary>
        public int ExtendPerOwnHealerBusy { get; set; } = 60;
        /// <summary>Times the share of the enemy's living fighters that are free.</summary>
        public int ExtendPerEnemyFighterFree { get; set; } = 30;
        /// <summary>For each of the group's healers and casters with a free enemy fighter in melee on it.</summary>
        public int ExtendPerBacklineUnderAttack { get; set; } = 15;

        /// <summary>
        /// On incoming a group holds when its longest mez outranges the enemy's by at least this
        /// much (the Sorcerer's 1,875 against 1,500 for every Healer and Bard mez). 0 turns holding off,
        /// and the outranged group's wait with it.
        ///
        /// Off since 2026-11-19 (it was 200). The owner, watching on 2026-11-18: "Mids are just
        /// standing there, doing nothing, until the Sorcerer is in range"; and on 2026-11-19, of a
        /// match in which Albion held: it "made no sense", and of another: "Both stood still,
        /// waiting for the other to push in. Then at some point Albs pushed and mezzed all 3
        /// fighters." And: "we were using speed + sprint all the time until initial contact."
        /// Nobody stood and waited in the open; until the opening is rebuilt (roadmap, open
        /// questions) both groups run in, and the mezzer casts when it is in range.
        /// </summary>
        public int HoldRangeAdvantage { get; set; } = 0;
        /// <summary>A hold ends after this long: the enemy is holding too, or not coming.</summary>
        public int HoldMaxSeconds { get; set; } = 10;
        /// <summary>A hold ends when an enemy is this close to a member: they are on the group.</summary>
        public int HoldBreakDistance { get; set; } = 1_200;
        /// <summary>
        /// The outranged group's answer. In this many matches of a hundred it stands as well and
        /// lets the holder come: its nearsight reaches 2,300, farther than any mez, so the enemy
        /// mezzer walking in can be nearsighted before it casts. In the rest it walks in as
        /// before. A chance and not a rule (owner, 2026-11-18: sometimes the answer is to work on
        /// them as they push in), so that recorded batches hold both openings and can be compared.
        /// </summary>
        public int HoldWhenOutrangedChance { get; set; } = 50;
        /// <summary>Such a wait lasts at most this long: longer than the holder's own hold, so the holder moves first.</summary>
        public int HoldWhenOutrangedMaxSeconds { get; set; } = 25;
        /// <summary>
        /// And it ends when the nearest enemy is within the enemy's mez reach plus this, when
        /// that is farther than <see cref="HoldBreakDistance"/>. The owner, watching Albion
        /// against Midgard on 2026-11-18: "Albion is running toward Mids and Mids are just
        /// standing there, doing nothing, until the Sorcerer is in range." The wait used to end
        /// at 1,200 units, and the Sorcerer mezzes from 1,875.
        /// </summary>
        public int HoldWhenOutrangedBreakMargin { get; set; } = 500;

        /// <summary>Under Extend, healers and casters want this many times the usual distance from enemy fighters.</summary>
        public double ExtendSafeFactor { get; set; } = 1.4;
        /// <summary>Under Push they accept this share of it, to keep up with their fighters.</summary>
        public double PushSafeFactor { get; set; } = 0.6;
        /// <summary>
        /// An order moves the back line (it stands 1.4 times as far from enemy fighters under
        /// Extend, 0.6 times under Push) only once it has stood this many seconds. A group changes
        /// its order four or five times in the first minute of a fight and an order lasts five
        /// seconds at the median (26 team-fights of the owner's and 30 of bots, 2026-11-20); its
        /// healers and casters walked 42% of their time, and half their steps away from a fighter
        /// were taken under Extend. Its fighters answer an order at once either way. 0: the back
        /// line moves with the order from its first second, as before.
        /// </summary>
        public int SafeFactorAfterSeconds { get; set; }

        /// <summary>How far the back line keeps from enemy fighters under an order, as a factor of its usual distance.</summary>
        public double SafeFactor(PvpOrder order, double secondsInForce) =>
            secondsInForce < SafeFactorAfterSeconds ? 1
            : order == PvpOrder.Extend ? ExtendSafeFactor : order == PvpOrder.Push ? PushSafeFactor : 1;

        /// <summary>Under Extend the group's fighters leave the kill target for an enemy fighter that is on their healers and casters.</summary>
        public bool ExtendFightersTurnOnPursuers { get; set; } = true;
        /// <summary>
        /// Under Extend a fighter with nobody to turn on, and farther than this from the middle of
        /// its healers and casters, comes back to them: the group falls back as one. 0 turns it off.
        /// </summary>
        public int ExtendFightersStayWithin { get; set; } = 1_000;
        /// <summary>
        /// Regroup (playbook, section 3): the group is strung out, a kill or a chase has pulled its
        /// fighters out of its healers' reach. Its score is this base plus the next weight times
        /// the share of its living fighters that are farther than <see cref="RegroupFighterIsFarAt"/>
        /// from the middle of its healers and casters: with these numbers, half of them.
        /// </summary>
        public int RegroupBase { get; set; } = -45;
        public int RegroupPerFighterFar { get; set; } = 130;
        /// <summary>
        /// A fighter this far from the nearest living healer of its group is out of reach. It was
        /// 1,800 from the middle of the group's healers and casters. The owner, watching Hibernia
        /// against Albion on 2026-11-19: the two Blademasters "are mostly running forward then
        /// back then forward then back". In that match Hibernia went Engage, Regroup, Engage,
        /// Regroup, Engage, Regroup, Engage in the first 28 seconds and the Blademasters were
        /// called back 11 times. Since the two back lines stand 1,300 to 1,600 apart, a fighter on
        /// a target in the enemy's back row was always more than 1,800 from the middle of its own,
        /// so the group regrouped, the fighters came back to 1,100, the order lapsed, and they
        /// went again. Now it is measured from a healer, healers follow their group
        /// (<see cref="PvpPositionWeights.HealerFollowsAtMembersOutOfRange"/>), and regrouping is
        /// for a group that is really strung out.
        /// </summary>
        public int RegroupFighterIsFarAt { get; set; } = 2_300;
        /// <summary>Under Regroup a fighter farther than this from its nearest living healer leaves its target and comes back (it was 1,100 from the middle).</summary>
        public int RegroupWithin { get; set; } = 1_700;

        /// <summary>
        /// The share of a group's living fighters that are out of its healers' reach: farther
        /// than <see cref="RegroupFighterIsFarAt"/> from the nearest living healer. 0 with no
        /// fighter or no healer alive: there is nobody to regroup with.
        /// </summary>
        public double FightersFarShare(IReadOnlyList<System.Numerics.Vector2> fighters, IReadOnlyList<System.Numerics.Vector2> healers) =>
            fighters.Count == 0 || healers.Count == 0 ? 0
                : fighters.Count(fighter => healers.Min(healer => System.Numerics.Vector2.Distance(fighter, healer)) > RegroupFighterIsFarAt) / (double)fighters.Count;
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
    /// Engage scores 0; Push, Extend and Regroup have to beat it. Hold is for the time before the fight.
    /// </summary>
    public static class PvpOrders
    {
        /// <param name="fightersFarShare">Share of the group's living fighters that are out of its healers' reach.</param>
        public static double Score(PvpOrder order, PvpReading own, PvpReading theirs, int backlineUnderAttack, PvpOrderWeights weights,
            double fightersFarShare = 0) => order switch
        {
            PvpOrder.Regroup => weights.RegroupBase + weights.RegroupPerFighterFar * fightersFarShare,
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

        /// <summary>
        /// Before the fight: true while a group that is outranged stands and lets the holder come
        /// to it. <paramref name="draw"/> is the match's own number from 0 to 99.
        /// </summary>
        public static bool WaitsOutAHold(int ownMezRange, int theirMezRange, double secondsHeld, int nearestEnemy, int draw, PvpOrderWeights weights) =>
            weights.Enabled && weights.HoldRangeAdvantage > 0 && draw < weights.HoldWhenOutrangedChance &&
            theirMezRange - ownMezRange >= weights.HoldRangeAdvantage &&
            secondsHeld < weights.HoldWhenOutrangedMaxSeconds &&
            nearestEnemy > Math.Max(weights.HoldBreakDistance, theirMezRange + weights.HoldWhenOutrangedBreakMargin);

        /// <summary>
        /// Which enemy on the group's healers and casters a fighter turns on under Extend: the
        /// one it is on already while that one is still there; else the nearest that no other
        /// fighter of the group has taken; else the nearest. -1 with nobody to turn on.
        /// The owner, watching on 2026-11-19: the two Blademasters run "forward then back then
        /// forward then back" and "are also stuck to each other". Each took the nearest such
        /// enemy anew every second, so in that match both went Armsman, Minstrel, Armsman,
        /// Minstrel, Paladin... a new target every one to three seconds for forty seconds.
        /// </summary>
        /// <summary>For how long an enemy that was on the group's healers and casters is still one to turn on, once it no longer is.</summary>
        public const int PursuerForMilliseconds = 3_000;

        /// <summary>
        /// Under Extend with nobody on its back line, a fighter takes an enemy only within the
        /// reach of its own healers and casters (PvpOrderWeights.ExtendFightersStayWithin from
        /// their middle; a quarter more for the one it is on already). It took the group's kill
        /// target wherever that stood, was sent back as soon as it was farther from its back
        /// line than that reach, was within it a second later and set out again. The owner,
        /// 2026-11-26, of a decent Midgard group it had just wiped: "Something strange was going
        /// on with their fighters". Its Savage, Berserker and Warrior landed 30 blows between
        /// them in 146 seconds; their kill target, a Druid at the back, was 1,000 to 2,000 units
        /// off; each changed its target 21 to 28 times.
        /// </summary>
        public static bool ExtendFighterTakes(double enemyFromItsBackLine, bool isOnItAlready, int stayWithin) =>
            stayWithin <= 0 || enemyFromItsBackLine <= stayWithin * (isOnItAlready ? 1.25 : 1.0);

        /// <summary>
        /// Under Extend with nobody on its back line: whether a fighter comes back to it. With an
        /// enemy to take near its back line (<see cref="ExtendFighterTakes"/>) only from far
        /// beyond the reach; with none, always: it goes to its healers and casters and stands
        /// there until somebody follows ("fall back and kite, fighters on whoever follows").
        /// </summary>
        public static bool ExtendFighterComesBack(bool hasAnEnemyToTake, double fromItsBackLine, int stayWithin) =>
            stayWithin > 0 && (!hasAnEnemyToTake || fromItsBackLine > stayWithin * 1.5);

        /// <summary>A fighter that has come back stands once it is within half the reach of its back line: it does not walk on to its middle.</summary>
        public static bool ExtendFighterStands(double fromItsBackLine, int stayWithin) => fromItsBackLine <= stayWithin * 0.5;

        public static int ChoosePursuer(IReadOnlyList<int> distances, int current, IReadOnlyCollection<int> taken)
        {
            if (current >= 0 && current < distances.Count)
                return current;
            int best = -1, bestFree = -1;
            for (int id = 0; id < distances.Count; id++)
            {
                if (best < 0 || distances[id] < distances[best])
                    best = id;
                if (!taken.Contains(id) && (bestFree < 0 || distances[id] < distances[bestFree]))
                    bestFree = id;
            }
            return bestFree >= 0 ? bestFree : best;
        }

        /// <summary>The order for this second, given the one in force and how long it has stood.</summary>
        /// <summary>No member with an enemy inside this distance: the group is not in contact.</summary>
        public const int ChaseBeyond = 900;
        /// <summary>Out of contact for this long in a fight, the group is chasing.</summary>
        public const int ChaseAfterMilliseconds = 3_000;

        /// <summary>
        /// True for a group in a chase: the fight is on and it has been out of contact for a few
        /// seconds (<paramref name="noContactSince"/> is 0 while it is in contact). The owner,
        /// 2026-11-20: "Bard should be aware when we are chasing someone and sing speed. Chasing
        /// one enemy across the basin without speed and endurance... makes no sense."
        /// </summary>
        public static bool Chases(bool fightStarted, long noContactSince, long now) =>
            fightStarted && noContactSince > 0 && now - noContactSince >= ChaseAfterMilliseconds;

        public static PvpOrder Choose(PvpReading own, PvpReading theirs, int backlineUnderAttack, PvpOrder current, double secondsInForce, PvpOrderWeights weights,
            double fightersFarShare = 0)
        {
            if (!weights.Enabled || theirs.Alive == 0 || own.Alive == 0)
                return PvpOrder.Engage;
            if (secondsInForce < weights.KeepSeconds)
                return current;
            PvpOrder best = PvpOrder.Engage;
            double bestScore = current == PvpOrder.Engage ? weights.KeepCurrent : 0;
            foreach (PvpOrder order in new[] { PvpOrder.Push, PvpOrder.Extend, PvpOrder.Regroup })
            {
                double score = Score(order, own, theirs, backlineUnderAttack, weights, fightersFarShare) + (order == current ? weights.KeepCurrent : 0);
                if (score > bestScore)
                    (best, bestScore) = (order, score);
            }
            return best;
        }
    }
}
