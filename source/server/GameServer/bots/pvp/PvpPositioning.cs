using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>Part of <see cref="PvpWeights"/>: where a group's healers and casters stand in a fight.</summary>
    public sealed class PvpPositionWeights
    {
        /// <summary>A healer or caster wants every enemy fighter at least this far away. 0 turns positioning off.</summary>
        public int SafeDistance { get; set; } = 900;
        /// <summary>
        /// And it wants every enemy of any kind at least this far away, as long as its work stays
        /// in reach: it stands at the edge of its range, not in the middle of it. The owner,
        /// watching on 2026-11-18: "The fight is way too tight. There are casters in the middle of
        /// melee." Recordings of that evening: the middles of the two groups' healers and casters
        /// were 1,870 units apart five seconds after contact, 1,125 at twenty and 850 at forty-five;
        /// casters began their casts from a median of 740 to 925 units with spells that reach
        /// 1,500. Nothing moved a caster back: only an enemy fighter within
        /// <see cref="SafeDistance"/> made it step. 0 = as before.
        /// Four sets of twelve matches in the night of 2026-11-19 (1,200 / 0.75 / a step every
        /// 4 s before): this distance and <see cref="WorkRange"/> wider, the edge counting 1 a
        /// unit with a step every 2.5 s, and walks costing double each moved the two back lines
        /// apart a little; only all three together passed the referee's marks (1,514 units apart
        /// at 20 s and 1,651 at 45 s, casts on healers and casters from a median of 1,346).
        /// </summary>
        public int EdgeDistance { get; set; } = 1_350;
        /// <summary>What a unit inside that distance costs, against a unit gained from an enemy fighter.</summary>
        public double PerUnitInsideEdge { get; set; } = 1;
        /// <summary>
        /// What a unit costs a caster that has no enemy of any kind within <see cref="WorkRange"/>:
        /// it steps back up to the edge. Without it nothing brought a caster forward again but a
        /// cast worth a walk, and with the back lines apart (above) casters stood with no enemy
        /// within 1,500 units for a third of their idle time, cast 17% of the time against 25%,
        /// and three matches of twelve ran to the time limit. Not while the group falls back or
        /// regroups, and not for a healer, whose work is its own group. 0 = as before.
        /// </summary>
        public double PerUnitOutOfReach { get; set; } = 1;
        /// <summary>
        /// How much staying in range of the group's kill target counts when a caster chooses where
        /// to stand, against 1 for the enemy it is itself casting on, and the same for the enemy
        /// it has interrupt duty for. At 1 (as it was) being in range of a kill target in the
        /// enemy's back row outweighed the edge four to one and stood the casters among the
        /// enemy's front casters.
        /// </summary>
        public double KillTargetRangeWeight { get; set; } = 0.1;
        public double DutyTargetRangeWeight { get; set; } = 0.5;
        /// <summary>
        /// A member that can cure mez takes no step while a groupmate is asleep, unless it is
        /// being kept from casting: waking the groupmate is its turn (PvpSquad.StandOff).
        /// </summary>
        public bool CurerStaysForSleeper { get; set; } = true;
        /// <summary>How far one step is.</summary>
        public int Step { get; set; } = 350;
        /// <summary>
        /// A longer step, taken when it is the better one: in the first runs a third of all steps
        /// were followed by another step, a caster walking away 350 units at a time and casting
        /// nothing in between. 0 leaves only the short step.
        /// </summary>
        public int LongStep { get; set; } = 700;
        /// <summary>A step is only taken when it leaves the bot this much better off (in units of distance).</summary>
        public int MinGain { get; set; } = 150;
        /// <summary>A caster stays within this of the enemy it is working on (its spells reach 1,500).</summary>
        public int WorkRange { get; set; } = 1_420;
        /// <summary>
        /// A healer stays within this of the members it heals, and everyone within this of their healers (heals reach 2,000).
        /// 1,700 until the night of 2026-11-19: with the fighters on the enemy's healers, that margin of 300 was
        /// 300 units less between the two back lines.
        /// </summary>
        public int HealRange { get; set; } = 1_850;
        /// <summary>
        /// A healer with this many living members of its group beyond <see cref="HealRange"/>
        /// follows them, without the wait between steps; 0 = it does not. The owner, watching on
        /// 2026-11-19: "Albion will call a regroup and Hibernia will push, but both Druids stay way
        /// too far and are out of range to almost any of their teammates". Recorded: a Druid stood
        /// still for 25 seconds while its group went after the enemy, until its fighters were
        /// 2,850 to 3,700 units away and only the other Druid was in reach. Nothing moved a healer
        /// forward: a step was taken to get away from an enemy, never to stay with the group, and
        /// the scored choice and the healing routine had nobody in range to act on.
        /// </summary>
        public int HealerFollowsAtMembersOutOfRange { get; set; } = 2;
        /// <summary>
        /// No step ends nearer the enemy's healers and casters than the edge, or, for a member
        /// already inside it, more than this much nearer than it stands. A step is scored by what
        /// it gains from a fighter against what it costs in range to the member's work, and range
        /// to work counts double: in 24 matches of the night of 2026-11-19 four steps in ten taken
        /// to get away from an enemy fighter went toward the enemy's back line (a Sorcerer under
        /// Extend from 1,118 units of their nearest caster to 645; a Cleric following its fighters
        /// from 2,065 to 856), and such steps were half of all the times two back lines came
        /// within 1,000 units of each other. A cast worth walking in for is the caster's own
        /// choice, and pays for where the walk ends. Negative = as before.
        /// </summary>
        public int StepNearerTheirBackLineAtMost { get; set; } = 100;
        /// <summary>
        /// A free enemy fighter on the bot and this close pins it: both run at the same speed, so
        /// walking away gains nothing until the fighter is mezzed, stunned, rooted or slowed. The
        /// first recorded run (2026-11-18, pinned only inside 250) had 144 such steps: the fighter
        /// was farther away two seconds later after 43% of them, 7 units nearer at the median.
        /// </summary>
        public int PinnedWithin { get; set; } = 600;
        /// <summary>
        /// A bot pinned like that kites back all the same, a step after another with no rest
        /// between (<see cref="KiteStepEveryMilliseconds"/>). The owner, 2026-11-20: "it really
        /// comes down to the Pac Healer knowing enemies are pushing for it and kiting back while
        /// our fighters and casters try to crowd control and train the enemies." Running gains no
        /// distance, as the count above says; it does spare blows. In 36 recorded matches a healer
        /// with an awake fighter on it stood for seven seconds in ten and finished 13 to 57% of
        /// the few casts it began; moving, the Pacification Healer took 0.48 melee hits a second
        /// against 0.66 standing, the Rejuvenation Cleric 0.35 against 0.65, the Bard 0.20 against
        /// 0.48 (five builds of eight took fewer). Off: it stands, as until 2026-11-20.
        /// </summary>
        public bool KitesWhenPinned { get; set; } = true;

        /// <summary>
        /// A group down to this many members, against at least <see cref="LastStandOutnumberedBy"/>
        /// times as many enemies, has lost: its healers and casters stop kiting, getting away and
        /// keeping their distance, and stand and do what they can. The owner, playing on
        /// 2026-11-20: "There's no point to keep the rules when the fight is almost done. When
        /// there are like 2 vs 6-8, the 2 could just stop moving instead of running around trying
        /// to kite or something." 0 turns it off.
        /// </summary>
        public int LastStandAtMost { get; set; } = 2;
        public int LastStandOutnumberedBy { get; set; } = 3;

        /// <summary>True for a group that has lost and makes its last stand.</summary>
        public bool LastStand(int ownAlive, int enemiesAlive) =>
            LastStandAtMost > 0 && ownAlive > 0 && ownAlive <= LastStandAtMost && enemiesAlive >= ownAlive * Math.Max(1, LastStandOutnumberedBy);

        /// <summary>
        /// The other side of a last stand: a group that has won and has only to finish. The
        /// owner again, 2026-11-22: "When the fight is always over, like my team only have 1-2
        /// members vs 6-8 enemies, there's no point in keeping all the rules actives. It just
        /// drags the fight for no reasons." Counted in that morning's roam, with the last stand
        /// already in: the last one or two of a beaten group lived a median of 15 seconds more
        /// and 54 at the longest, mezzed or stunned for 37% of them, while the winners went on
        /// getting away (98 decisions), spreading (72), standing off (41) and kiting (40).
        /// A group that mops up puts nobody to sleep, keeps no distance, and sends every fighter
        /// at the kill target, its guard and its peel too.
        /// </summary>
        public bool MopsUp(int ownAlive, int enemiesAlive) => LastStand(enemiesAlive, ownAlive);

        /// <summary>What a group that mops up no longer casts: sleep, forgetfulness and blindness only keep the last enemy alive.</summary>
        public static bool NotInAMopUp(PvpActionKind kind) =>
            kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Amnesia or PvpActionKind.Nearsight;
        public int KiteStepEveryMilliseconds { get; set; } = 700;
        /// <summary>
        /// With one of the enemy fighters on it mezzed, stunned, rooted or slowed, the bot is not
        /// pinned by the others: it runs, leaves that one behind, and whoever follows does so
        /// alone. The owner, watching Hibernia against Albion on 2026-11-19: both Blademasters
        /// had snared the Armsman that was on a Druid, the Paladin was on it too, "and the Druid
        /// just stood still. The Druid could have kited away. If the Paladin would have followed
        /// and extended, Hibs could have collapsed on it."
        /// </summary>
        public bool RunsWhenOneAttackerIsHeld { get; set; } = true;
        /// <summary>What a unit outside work or heal range costs, against a unit gained from an enemy fighter.</summary>
        public double PerUnitOutOfRange { get; set; } = 2;
        /// <summary>What a unit inside the keep-apart distance of a groupmate costs.</summary>
        public double PerUnitCrowded { get; set; } = 1;
        /// <summary>A healer does not step away while a member in its reach is below this health, unless the enemies on it are held.</summary>
        public int HealerStaysIfMemberBelow { get; set; } = 60;
        /// <summary>
        /// And a healer that is on such a step breaks it off to heal, unless an enemy fighter is on
        /// it; nor does it take a step to keep apart from a groupmate then. Counted over the first
        /// death of each group in 24 matches at rank 5 (2026-11-19): in the six seconds before it
        /// the group's other healers spent 16% of their seconds walking, and half of that was a
        /// step to stand off, to keep range or to keep apart, not a healer running from a fighter.
        /// No heal at all landed on the one that died in 17 of the 30.
        /// </summary>
        public bool HealerBreaksOffAStepToHeal { get; set; } = true;

        /// <summary>True for a healer that should stop its step and heal: somebody in reach is badly hurt and nobody is on the healer.</summary>
        public bool BreaksOffToHeal(bool healer, bool huntedByAFighter, int lowestHealthInReach) =>
            HealerBreaksOffAStepToHeal && healer && !huntedByAFighter && lowestHealthInReach < HealerStaysIfMemberBelow;
        /// <summary>
        /// After a step the bot works from its new place for at least this long before the next
        /// one. A member running from fighters that are held does not wait.
        /// </summary>
        public int StepEveryMilliseconds { get; set; } = 2_500;
        /// <summary>
        /// That wait counts from when the bot arrives, not from when it set off. A long step takes
        /// about three seconds to walk, so with the wait counted from the start the next step came
        /// as the bot arrived: in 24 matches of the night of 2026-11-19 the six casters took a step
        /// straight after a step, with no cast between, 20 times a match.
        /// </summary>
        public bool StepWaitCountsFromArrival { get; set; } = true;
        /// <summary>
        /// A caster that set off toward a target to cast on it this recently is not stepped back
        /// to the edge of its range: its choice has already paid for where the walk ends
        /// (PvpWeights.WalkInsideEdgePerHundred). In the same 24 matches a walk in was followed
        /// within five seconds by a step back out, with nothing cast, 7.8 times a match. An enemy
        /// fighter beside it still moves it. 0 = as before.
        /// </summary>
        public int ApproachLeftAloneMilliseconds { get; set; } = 3_000;
        /// <summary>
        /// Under Extend a place is worth this much more for each unit it lies farther along the
        /// group's line of retreat (straight away from the enemy), so that the healers and casters
        /// fall back the same way. Playbook, section 3: "a retreat is one group moving one way,
        /// not eight paths". 0 = each steps wherever it is safest for itself.
        /// </summary>
        public double PerUnitAlongRetreat { get; set; } = 0.3;
        /// <summary>
        /// A caster or control healer whose scored choice finds nothing worth casting stands where
        /// it is. Otherwise the old brain takes the turn and walks it to spell range of its attack
        /// target, which for a kill target in the enemy's back row means into the enemy: in the
        /// 150 matches of 2026-11-18 casters stood 41 to 56% of the way to where the enemy group
        /// had been, the Bard and the Pacification Healer died first, and with them the only cure
        /// for their healers' mez. It still walks when a cast that needs a walk is worth it.
        /// </summary>
        public bool CastersStandWithNothingToCast { get; set; } = true;

        /// <summary>
        /// Every member sprints, on the way to the enemy (on top of the speed song) and through
        /// the fight: 30% faster, as for a player. The owner, 2026-11-19: "we were always using speed 6 and sprinting. Everyone
        /// always has sprint on. One of the first realm abilities to get is Tireless, and you can
        /// perma sprint without issues." Until then no bot sprinted: in the recordings every
        /// class moved at 185 to 190 units a second in a fight, the plain run speed, and a bot's
        /// sprint did not change its speed at all (only a player's did). It costs no endurance,
        /// like all of a bot's movement, which stands in for Tireless and the endurance songs;
        /// a player in the group pays for sprint as the game has it.
        /// Since 2026-11-20 it costs endurance (<see cref="SprintCostPerSecond"/>), and the owner
        /// set when it is used: "No sprint when roaming, just speed from Bard/Skald/Minstrel.
        /// Sprint during fight when needed." (<see cref="SprintNeeded"/>).
        /// </summary>
        public bool SprintInFights { get; set; } = true;
        /// <summary>A bot with this far to go in a fight sprints: more than an ordinary step, to a target or a place.</summary>
        public int SprintBeyond { get; set; } = 400;
        /// <summary>Once sprinting it sprints on until it is this near.</summary>
        public int SprintUntilWithin { get; set; } = 150;

        /// <summary>
        /// Whether a bot in a fight has a reason to sprint: it is pressed (an enemy fighter on
        /// it, falling back with its group, or its group chasing), or it has
        /// <paramref name="wayLeft"/> to cover to its target or to where it is going. In melee,
        /// or taking an ordinary step, it runs and keeps its endurance for its styles.
        /// </summary>
        public bool SprintNeeded(bool pressed, int wayLeft, bool sprinting) =>
            pressed || wayLeft >= SprintBeyond || sprinting && wayLeft >= SprintUntilWithin;
        /// <summary>
        /// What a second of sprinting on the move takes from a bot's endurance, as it takes five
        /// from a player's each tick: with an endurance song or buff on it the bot gets as much
        /// back and sprints for ever, as players with Tireless and a Bard did; without one it runs
        /// dry in twenty seconds and is back to its run. The owner, 2026-11-20, after chasing a
        /// Healer across the basin: "the enemy outrunning us makes no sense. If they have no
        /// endurance, they can't sprint." (The recording: its endurance at 100 all the way.) 0:
        /// a bot's sprint is free, as until then.
        /// </summary>
        public int SprintCostPerSecond { get; set; } = 5;

        /// <summary>
        /// What a second of sprint costs a bot with Long Wind, by the player's rule (20% less a level,
        /// rounded down: 4, 3, 2, 1 and nothing at its fifth). Until 2026-11-22 the ability was bought
        /// and did nothing for a bot.
        /// </summary>
        public static int SprintCostWith(int costPerSecond, int longWindPercent) =>
            Math.Max(0, costPerSecond - Math.Clamp(longWindPercent, 0, 100) * costPerSecond / 100);
        /// <summary>A bot out of breath sprints again once it has this share of its endurance back.</summary>
        public int SprintsAgainAtPercent { get; set; } = 20;

        /// <summary>
        /// A fighter stops sprinting when its endurance is down to this share and keeps the rest
        /// for its styles; a chase is the exception. In 24 matches of 2026-11-20, one team of
        /// each paying for sprint and one not: the fighters that paid were under 20% endurance
        /// for 17% of the fight against 1%, and 75% of their swings had a style against 82%.
        /// 0: a fighter sprints to its last breath like everybody else.
        /// Compared that evening at 40, 24 matches with one team of each on it: its fighters were
        /// under 20% endurance 5% of the fight against 21%, 79% of their swings had a style against
        /// 72%, wins 13 against 11, deaths 4.4 against 5.1. A fighter that reached the back line
        /// was stopped within three seconds 33% of the time against 49% (52 and 53 dives), so the
        /// shield fighter, whose work is getting to that fighter, keeps nothing back.
        /// </summary>
        public int FighterKeepsEndurancePercent { get; set; } = 40;

        /// <summary>Whether a bot that wants to sprint can: not on its last breath, and once out of it, not until it has some back.</summary>
        /// <param name="keeps">The share of its endurance it does not sprint away (<see cref="FighterKeepsEndurancePercent"/>); 0 for none.</param>
        public bool CanSprint(int endurancePercent, bool sprinting, int keeps = 0) =>
            SprintCostPerSecond <= 0 ||
            (sprinting ? endurancePercent > Math.Max(5, keeps) : endurancePercent >= Math.Max(SprintsAgainAtPercent, keeps > 0 ? keeps + 15 : 0));

        /// <summary>
        /// A fighter with a style from behind walks behind a target that is stunned
        /// (bots/pvp/PvpWeaponSwap.cs). The owner, 2026-11-19: the Hero opens with Slam and then
        /// uses the level 50 Large Weapons style, which is a style from behind; a bot stood
        /// wherever it had arrived from, so it had that style only by chance.
        /// </summary>
        public bool FightersStepBehindStunned { get; set; } = true;
        /// <summary>How far behind the target the fighter goes to stand.</summary>
        public int StepBehindDistance { get; set; } = 70;
        /// <summary>The least time between two such walks by one fighter.</summary>
        public int StepBehindEveryMilliseconds { get; set; } = 3_000;
        /// <summary>
        /// On incoming a healer that only heals (Cleric, Druid, Mending Healer) walks no closer to
        /// the nearest enemy than this: outside the longest mez (1,875 and the width of its area),
        /// so the opening mez cannot catch it. 0 = it walks in with the others. The owner,
        /// 2026-11-17: a healer may run off on the incoming and come back into range afterwards,
        /// so as not to be targeted or mezzed.
        /// </summary>
        public int HealerHangsBackAt { get; set; } = 2_300;
        /// <summary>It comes in this long after the first blow: the fight has settled and the opening mezzes are spent.</summary>
        public int HealerHangsBackSeconds { get; set; } = 5;

        /// <summary>
        /// Before the first blow every healer and caster stops when the nearest enemy is within
        /// this, and lets its fighters go on; 0 = they run in with the fighters. Seen in the
        /// pictures of an opening drawn second by second (2026-11-19): with the speed song and
        /// sprint two groups close at about 1,000 units a second, the travelling formation aimed
        /// the back row at a place 700 units short of the middle of the enemy, and both back rows
        /// ran in with their fighters until they were about 1,000 units from the enemy's. So the
        /// fight was one ball 1,300 units across two seconds after the first blow, nothing could
        /// be cast on fighters that were coming in, and the enemy's fighters were on the healers
        /// at once. The distance allows for the second that passes before the order is given.
        /// </summary>
        public int BackRowStopsAt { get; set; } = 3_200;

        /// <summary>True for a healer or caster that should stop where it is before the first blow.</summary>
        public bool BackRowStops(bool fighter, int nearestEnemy, bool fightStarted) =>
            !fighter && !fightStarted && BackRowStopsAt > 0 && nearestEnemy < BackRowStopsAt;

        /// <summary>
        /// True while such a healer should stay where it is. <paramref name="secondsSinceContact"/>
        /// is negative before the first blow.
        /// </summary>
        public bool HealerHangsBack(bool onlyHeals, int nearestEnemy, double secondsSinceContact) =>
            onlyHeals && HealerHangsBackAt > 0 && nearestEnemy < HealerHangsBackAt && secondsSinceContact < HealerHangsBackSeconds;
    }

    /// <summary>An enemy fighter as a healer or caster sees it when choosing where to stand.</summary>
    public readonly record struct PvpThreat(Vector2 At, bool Held, bool Slowed, bool OnMe);

    /// <summary>Something a bot must stay in range of: an enemy it works on, a member it heals, its own healer.</summary>
    public readonly record struct PvpWorkPoint(Vector2 At, int Range, double Weight = 1);

    /// <summary>
    /// Where healers and casters stand (docs/PVP-8V8-PLAYBOOK.md, sections 4 and 5). The owner,
    /// 2026-11-18: casters and healers stand near the edge of their range and away from enemy
    /// fighters; a trained member, once the enemies on it are peeled, mezzed or stunned, runs to
    /// get distance. Recordings of that day: a healer or caster had an enemy fighter within 500
    /// units 57% of the time, and for two thirds of that time the fighter was on somebody else or
    /// was itself mezzed or stunned, so stepping away was free.
    /// This is a weighing, not a rule: a step is scored by the distance it gains from enemy
    /// fighters against what it costs in range to the bot's work and in crowding its groupmates.
    /// </summary>
    public static class PvpPositioning
    {
        private const int Directions = 16;

        /// <summary>Why a bot is not stepping, for the recorder and the tests.</summary>
        public enum Verdict { Step, Safe, Pinned, NothingBetter, Kite }

        /// <summary>
        /// Where a bot stops when it walks up to something to reach it with a spell of
        /// <paramref name="range"/>: on the line between them, <paramref name="margin"/> inside the
        /// range, and at a height between the two, not at its own. A healer walking up to its
        /// patient was sent to a point at its own height in a straight line; on sloping ground it
        /// kept that height wherever it went (a Shaman for two and a half minutes and 3,300
        /// units, a Healer for a minute and a half), and stood 150 to 230 units above the ground
        /// the owner's Hero stood on beside it: "I'm unable to attack healer and shaman. Always
        /// says out of range, even if I'm on top of them" (2026-11-20).
        /// </summary>
        public static Vector3 StopShortOf(Vector3 from, Vector3 to, int range, int margin = 150)
        {
            float distance = Vector3.Distance(from, to);
            float keep = distance <= 1 ? 0 : Math.Clamp((range - margin) / distance, 0, 1);
            return to + (from - to) * keep;
        }

        /// <summary>True for a bot with a free enemy fighter on it and close, and none of those on it held or slowed.</summary>
        public static bool Pinned(Vector2 self, IReadOnlyList<PvpThreat> threats, PvpPositionWeights weights) =>
            threats.Any(threat => threat.OnMe && !threat.Held && !threat.Slowed && Vector2.Distance(self, threat.At) <= weights.PinnedWithin) &&
            !(weights.RunsWhenOneAttackerIsHeld && threats.Any(threat => threat.OnMe && (threat.Held || threat.Slowed)));

        /// <summary>
        /// The places worth stepping to, best first; empty when the bot is better off where it
        /// stands. <paramref name="safeDistance"/> is the weights' distance as the group's order
        /// has scaled it.
        /// </summary>
        /// <param name="retreat">The group's line of retreat as a vector of length one; zero when it is not falling back.</param>
        /// <param name="others">The enemy's healers and casters, the ones awake.</param>
        /// <param name="edge">How far the bot wants every enemy, as the group's order has scaled it; 0 = only fighters count.</param>
        /// <param name="reach">A caster wants some enemy within this; 0 = it does not close up.</param>
        /// <param name="apart">For a healer: the group's other healers, kept <paramref name="apartSpacing"/> away (PvpSpreadWeights.HealerSpacing).</param>
        /// <param name="leftBehind">A healer whose group has moved out of its reach: it looks for a step with no enemy near.</param>
        public static List<Vector2> Steps(Vector2 self, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights, out Verdict verdict, Vector2 retreat = default,
            IReadOnlyList<Vector2> others = null, int edge = 0, int reach = 0, IReadOnlyList<Vector2> apart = null, int apartSpacing = 0,
            bool leftBehind = false)
        {
            verdict = Verdict.Safe;
            bool fighterNear = threats.Count > 0 && threats.Min(threat => Vector2.Distance(self, threat.At)) < safeDistance;
            bool insideEdge = edge > 0 && NearestEnemy(self, threats, others) < edge;
            if (safeDistance <= 0 || !fighterNear && !insideEdge && !leftBehind && !OutOfReach(self, threats, others, reach))
                return [];
            // A fighter that can follow is on it and close: running gains no distance. It kites back all the same, or stands.
            bool pinned = Pinned(self, threats, weights);
            if (pinned && !weights.KitesWhenPinned)
            {
                verdict = Verdict.Pinned;
                return [];
            }
            double here = Score(self, threats, work, mates, minSpacing, safeDistance, weights, others, edge, reach, apart, apartSpacing);
            // How near the enemy's healers and casters a step may end (PvpPositionWeights.StepNearerTheirBackLineAtMost).
            double floor = edge > 0 && weights.StepNearerTheirBackLineAtMost >= 0 && others is { Count: > 0 }
                ? Math.Min(edge, NearestEnemy(self, [], others)) - weights.StepNearerTheirBackLineAtMost : 0;
            double Along(Vector2 point) => Vector2.Dot(point - self, retreat) * weights.PerUnitAlongRetreat;
            var better = new List<(Vector2 Point, double Score)>();
            // Kiting, only the long step: fewer changes of way, each of which every client has to be told.
            foreach (int length in pinned && weights.LongStep > weights.Step ? [weights.LongStep]
                         : weights.LongStep > weights.Step ? new[] { weights.Step, weights.LongStep } : [weights.Step])
            {
                for (int i = 0; i < Directions; i++)
                {
                    double angle = 2 * Math.PI * i / Directions;
                    Vector2 point = self + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
                    if (floor > 0 && NearestEnemy(point, [], others) < floor)
                        continue;
                    double score = Score(point, threats, work, mates, minSpacing, safeDistance, weights, others, edge, reach, apart, apartSpacing) + Along(point);
                    // Kiting, any step is taken, the best of them: it is the moving that counts, not the gain.
                    if (score - here >= weights.MinGain || pinned)
                        better.Add((point, score));
                }
            }
            verdict = better.Count == 0 ? Verdict.NothingBetter : pinned ? Verdict.Kite : Verdict.Step;
            return better.OrderByDescending(entry => entry.Score).Select(entry => entry.Point).ToList();
        }

        /// <summary>The distance to the nearest enemy of any kind; very far when there is none.</summary>
        public static double NearestEnemy(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<Vector2> others)
        {
            double nearest = double.MaxValue;
            foreach (PvpThreat threat in threats)
                nearest = Math.Min(nearest, Vector2.Distance(point, threat.At));
            if (others != null)
            {
                foreach (Vector2 other in others)
                    nearest = Math.Min(nearest, Vector2.Distance(point, other));
            }
            return nearest;
        }

        /// <summary>True for a healer that its group has left behind (<see cref="PvpPositionWeights.HealerFollowsAtMembersOutOfRange"/>).</summary>
        public static bool LeftBehind(Vector2 self, IReadOnlyList<Vector2> members, PvpPositionWeights weights) =>
            weights.HealerFollowsAtMembersOutOfRange > 0 &&
            members.Count(member => Vector2.Distance(self, member) > weights.HealRange) >= weights.HealerFollowsAtMembersOutOfRange;

        /// <summary>True for a caster that has enemies and none of them within its reach.</summary>
        public static bool OutOfReach(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<Vector2> others, int reach) =>
            reach > 0 && (threats.Count > 0 || others is { Count: > 0 }) && NearestEnemy(point, threats, others) > reach;

        /// <summary>
        /// How good a place is: distance from the nearest enemy fighter (no better than safe) and,
        /// for less, from the nearest enemy of any kind (no better than the edge), less what it costs.
        /// </summary>
        public static double Score(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights,
            IReadOnlyList<Vector2> others = null, int edge = 0, int reach = 0, IReadOnlyList<Vector2> apart = null, int apartSpacing = 0)
        {
            double score = threats.Count == 0 ? safeDistance : Math.Min(safeDistance, threats.Min(threat => Vector2.Distance(point, threat.At)));
            if (edge > 0)
                score += weights.PerUnitInsideEdge * Math.Min(edge, NearestEnemy(point, threats, others));
            if (OutOfReach(point, threats, others, reach))
                score -= weights.PerUnitOutOfReach * (NearestEnemy(point, threats, others) - reach);
            foreach (PvpWorkPoint item in work)
                score -= Math.Max(0, Vector2.Distance(point, item.At) - item.Range) * weights.PerUnitOutOfRange * item.Weight;
            foreach (Vector2 mate in mates)
                score -= Math.Max(0, minSpacing - Vector2.Distance(point, mate)) * weights.PerUnitCrowded;
            // Two healers side by side are one target for whoever runs in. In the night of 2026-11-19, with the
            // back row stepping away from the enemy more often, a group's healers were within 800 units of each
            // other 30% of the time against 19%: each stepped to the same safest place.
            if (apart != null && apartSpacing > 0)
            {
                foreach (Vector2 other in apart)
                    score -= Math.Max(0, apartSpacing - Vector2.Distance(point, other)) * weights.PerUnitCrowded;
            }
            return score;
        }
    }
}
