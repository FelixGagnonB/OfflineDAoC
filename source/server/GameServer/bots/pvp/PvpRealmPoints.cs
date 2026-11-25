using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>The settings of a real session (docs/PVP-8V8-ROADMAP.md, M9.1 and M1.5).</summary>
    public sealed class PvpRealWeights
    {
        /// <summary>
        /// What a kill is worth is multiplied by this. 1 is the game's own pace, at which rank 5
        /// is some 500 kills shared by eight; 5 as built, so that an evening moves a rank (the
        /// owner, 2026-11-20: "We could earn rps at x5 or x10").
        /// </summary>
        public double RealmPointRate { get; set; } = 5;

        /// <summary>
        /// On: no member of an enemy group is of a lower rank than the average of the player's
        /// group, whatever that enemy group has earned itself. A group that never wins would
        /// otherwise stay at rank 1 while the player's climbs.
        /// </summary>
        public bool EnemyKeepsUp { get; set; } = true;

        /// <summary>
        /// The evening begins when a player with an 8v8 character enters the game
        /// (bots/pvp/PvpEveningStart.cs), with this many groups of each realm (PvpTeamPool.MostGroups at the most).
        /// Twelve since 2026-11-25 (eight before): the owner, "I feel like the population is low".
        /// </summary>
        public bool EveningAtLogin { get; set; } = true;
        public int EveningGroups { get; set; } = 12;

        /// <summary>
        /// The guilds a player's evenings are made with begin at realm rank 1 level 1, whatever
        /// their kind, and earn what they come to. The owner, 2026-11-24: "I do want everyone to
        /// start at realm rank 1 level 1 and grind it out. There could already be group profile
        /// regardeless of level at 'release'." Off: a guild begins at the rank of its kind (a
        /// bad group at 1 to 3, an elite team at 8 to 10), as the guilds made before that day
        /// did and as an evening of bots still has them.
        /// </summary>
        public bool GuildsBeginAtTheBottom { get; set; } = true;

        /// <summary>A member of the killing group this far from the one that died has no share.</summary>
        public int ShareWithin { get; set; } = 6_000;

        /// <summary>
        /// A roam: a bot team that comes on a fight that is on (its realm's other team's, or one
        /// between teams of the two other realms) joins it this often, in percent, and otherwise
        /// leaves the teams in it alone until the fight is over (unless it is attacked). The owner, 2026-11-20: "It could be random, like 30% would add
        /// 70% won't add or so. It always been a heated discussion, should we assjam or not an
        /// ongoing fight. Most of the respected 8v8 team were not."
        /// </summary>
        /// <remarks>
        /// 30 until 2026-11-21, when the owner had played a roam of 48 minutes: "I feel like 30%
        /// add 70% run by is too high. I feel like it always ends up like a huge brawl with all
        /// the teams". Counted in its recording: 36 draws, 8 of them "joins"; and "joins after
        /// all" (PvpRoam.EvensTheOdds) had overturned 16 of the 28 others, so that two draws in
        /// three ended in an add, and three of the nine fights had all six teams in them. With
        /// four teams passing a fight, one in ten each is an add in a third of the fights where
        /// three in ten is one in three fights out of four. The owner's number: "maybe 15% add
        /// 85% run by".
        /// </remarks>
        public int AddChancePercent { get; set; } = 15;

        /// <summary>
        /// A roam: a team of bots buffs at its start for this long and sets off, and each further
        /// team of bots of its realm follows that much later, so that a realm's two do not walk
        /// as one group of sixteen. The gap was ninety seconds until the owner's first roam
        /// (2026-11-21): "The 2nd hib team never left the spawn point". It did, 135 seconds in,
        /// and five seconds later the first Albion team, which needs about a hundred seconds to
        /// the south end of the middle, was at Hibernia's hill. With the player at the head of a
        /// realm's first team, the second is that realm's first team of bots.
        /// </summary>
        public int BuffsAtStartSeconds { get; set; } = 45;
        public int NextTeamFollowsAfterSeconds { get; set; } = 30;

        /// <summary>How long after the start a team of bots sets off: <paramref name="botTeamsBefore"/> of its realm leave before it.</summary>
        public static int SetsOffAfterSeconds(int botTeamsBefore, PvpRealWeights real) =>
            real.BuffsAtStartSeconds + Math.Max(0, botTeamsBefore) * real.NextTeamFollowsAfterSeconds;

        /// <summary>
        /// A roam, after a fight: a team of bots with dead members does not walk on without them.
        /// With a member alive that can raise, it gathers at its dead and raises them, for up to
        /// the first number of seconds; with nobody to raise them, or when that time is up, it
        /// goes back to its start after the second (as players release and form up again) and
        /// stands up there as after a wipe. Until 2026-11-21 it walked on short, the dead left
        /// where they fell, until the next fight wiped it.
        /// </summary>
        public int RaisesItsDeadForSeconds { get; set; } = 90;
        public int ReformsAfterSeconds { get; set; } = 20;

        /// <summary>Whether a team out of a fight, with dead it has had for this long, gives up on them and goes back to its start.</summary>
        public static bool Reforms(bool someoneCanRaise, double secondsWithItsDead, PvpRealWeights real) =>
            secondsWithItsDead >= (someoneCanRaise ? real.RaisesItsDeadForSeconds : real.ReformsAfterSeconds);

        /// <summary>
        /// Resting (the player's call, and a team of bots between two fights): a bot regains this
        /// share of its health, power and endurance a second, where a companion of the other
        /// game modes regains a tenth. At 5 an empty caster is full in twenty seconds.
        /// </summary>
        public int RestRegainsPercentPerSecond { get; set; } = 5;

        /// <summary>
        /// A roam: after a fight a team of bots with a member below this share of its health or
        /// power rests before it walks on, for at most the seconds below. It walked on as it
        /// was, and met its next enemy with what it had left.
        /// </summary>
        public int TeamRestsBelowPercent { get; set; } = 60;
        public int TeamRestsAtMostSeconds { get; set; } = 40;

        /// <summary>
        /// Before it rests a team of bots walks off the road it is on, to one side, as far as the
        /// first of these that the ground allows (a place that is there and that it can walk to
        /// without a long way round), and comes back to its road afterwards. The owner,
        /// 2026-11-22: "it would be nice a team decides to go hide somewhere off, or out of the
        /// route the teams make to meet each other and rest." A group sees another from 6,500
        /// units. With no such place it rests where it is. Empty: it always does.
        /// </summary>
        public int[] RestsOffTheRoadBy { get; set; } = [7_500, 5_500];
        /// <summary>A team that rests gets up when an enemy team is within this of it.</summary>
        public int RestEndsWithEnemyWithin { get; set; } = 5_000;

        /// <summary>
        /// The 8v8 zone (PvpRoam.EightZone, M13.15): one zone of every evening where nobody adds.
        /// The owner, 2026-11-25: "Could we build a zone for dedicated 8v8 without adds? ... use
        /// an unuse zone and make it a dedicated 8v8 only zone". Which zone it is, by the word
        /// that names it ("thid", "collory", ...; empty: there is none); how many guilds of each
        /// realm are in it at the most (a third of the realm's, and one at the least); and for
        /// how long after its fight a group that is not whole again is left alone by the kinds
        /// of group that wait (PvpRoam.WaitsForARestedGroup).
        /// </summary>
        public string EightZone { get; set; } = "thid";
        /// <summary>
        /// The clean zones of an evening (the nine frontier zones behind the three a realm ported
        /// to) are played as the 8v8 zone is: nobody joins a fight that is on, a group picks one
        /// enemy group, a group that has just fought is left to rest (M13.16). The owner,
        /// 2026-11-25: "I have yet to find a clean fight in any of the frontier zone"; in the
        /// clean zone of that evening a group that adds came into two of its three fights.
        /// </summary>
        public bool CleanZonesHaveNoAdds { get; set; } = true;
        public int EightZoneGroups { get; set; } = 2;
        public int EightZoneRestSeconds { get; set; } = 120;

        /// <summary>Whether a team of bots rests now: a member is low, and nobody is near to fight.</summary>
        public static bool TeamRests(int lowestPercent, PvpRealWeights real) =>
            real.TeamRestsBelowPercent > 0 && lowestPercent < real.TeamRestsBelowPercent;

        /// <summary>A roam: a team with nobody left alive stands up at its start point after this many seconds.</summary>
        public int WipeStandsUpAfterSeconds { get; set; } = 30;

        /// <summary>
        /// A wiped team does not stand up while an enemy is this near its start point, for up to
        /// <see cref="WipeWaitsAtMostSeconds"/>: in the second roam of 2026-11-20 a Hibernian team
        /// stood up on its hill with the Albion team that had wiped it still there, and was in a
        /// fight twelve seconds later, before it had buffed.
        /// </summary>
        public int StartIsClearBeyond { get; set; } = 5_000;
        public int WipeWaitsAtMostSeconds { get; set; } = 120;

        /// <summary>
        /// A team of bots does not start a fight with a team that is this near its own start
        /// point: nobody camped a portal keep, whose guards saw to that. One it is already
        /// fighting it goes on fighting, and one that attacks it from there it answers. In the
        /// third roam of 2026-11-20 a Hibernian team waited its two minutes, stood up on its hill
        /// and was attacked a second later by the Albion team that had stayed there. 0: off.
        /// The player's team goes where the player takes it.
        /// </summary>
        public int StartIsSafeWithin { get; set; } = 3_500;

        /// <summary>Whether a team of bots leaves an enemy team alone because it is at its own start.</summary>
        public static bool LeftAtItsStart(double fromItsStart, bool fightingItAlready, bool itAttacksUs, PvpRealWeights real) =>
            real.StartIsSafeWithin > 0 && fromItsStart <= real.StartIsSafeWithin && !fightingItAlready && !itAttacksUs;

        /// <summary>Whether a wiped team stands up now: after its wait, and with no enemy at its start, or after the longest wait whatever is there.</summary>
        /// <param name="playerIsBack">
        /// The player of the group has released and stands at the start: the group does not make
        /// it wait. The owner, 2026-11-22, having released ten seconds after a wipe: "I used
        /// /release and respawn at correct location... The whole team did not release". They
        /// came twenty seconds later, when the thirty of the wipe were up.
        /// </param>
        public static bool StandsUp(double secondsDown, bool enemyAtStart, PvpRealWeights real, bool playerIsBack = false) =>
            (secondsDown >= real.WipeStandsUpAfterSeconds || playerIsBack) && (!enemyAtStart || secondsDown >= real.WipeWaitsAtMostSeconds);
    }

    /// <summary>
    /// Realm points in a real 8v8 session. The owner, 2026-11-20: "we have a nice way to playtest
    /// 8v8 in line and basin, but everything is just for testing. All abilities reset, no rps
    /// gain, only 2 groups, no real roaming". A practice session stays as it was (a rank picked
    /// for everybody, timers reset, nothing earned). In a real one a kill is worth what a player
    /// of that level and rank is worth, shared out among the killing group; the player's share
    /// goes on the character, a companion's into the player's roster, an enemy's into that enemy
    /// group's roster, and each bot comes into the next session at the rank its points make.
    /// And then: "the real will have us start at rr1 and gain rps and see how it goes. We would
    /// need to persist bots and companions data (or at least, rps gained). I guess if we add
    /// teams, we'd need to make sure to persist all the bot / companions and their rps." So
    /// everybody starts with nothing, the player too: what the character has earned in real
    /// sessions is kept in the roster beside its companions (<see cref="PlayerBuild"/>), and a
    /// real session puts the character at that rank, whatever a practice session had given it.
    /// </summary>
    public static class PvpRealmPoints
    {
        /// <summary>What a kill is worth before the rate: the game's rule before 1.81, 900 for a level 50 of rank 1 (GamePlayer.RealmPointsValue).</summary>
        public static int ValueOf(int level, int realmLevel) =>
            Math.Max(1, (level - 20) * (level - 20)) + Math.Max(0, realmLevel);

        /// <summary>One member's share of a kill: the worth times the rate, split evenly, never under one point.</summary>
        public static long Share(int value, int members, double rate) =>
            members <= 0 || value <= 0 ? 0 : Math.Max(1, (long)Math.Round(value * Math.Max(0, rate) / members, MidpointRounding.AwayFromZero));

        /// <summary>
        /// The realm level that so many points make: 1 is rank 1 level 1, 40 is rank 5 level 0.
        /// Never under 1: the owner, 2026-11-20, "Start at Realm Rank 1 Level 1" (which is what no
        /// points at all make in the game's own table).
        /// </summary>
        public static int LevelFor(long points) => Math.Max(1, AutonomousBotRealmPointRewards.RealmLevelFor(points));

        /// <summary>The realm points a realm level begins at: what a team of a prime-time evening starts with (PvpGuildTeam.SeedLevel).</summary>
        public static long PointsFor(int realmLevel) =>
            GamePlayer.REALMPOINTS_FOR_LEVEL[Math.Clamp(realmLevel, 0, GamePlayer.REALMPOINTS_FOR_LEVEL.Length - 1)];

        /// <summary>The name under which a player's own earned realm points are kept in the roster, beside the companions' builds.</summary>
        public const string PlayerBuild = "player";

        /// <summary>A rank as players write it: 1 is 1L1, 40 is 5L0, 45 is 5L5.</summary>
        public static string RankName(int realmLevel) => $"{Math.Max(0, realmLevel) / 10 + 1}L{Math.Max(0, realmLevel) % 10}";

        /// <summary>The realm level an enemy bot fights at (PvpRealWeights.EnemyKeepsUp).</summary>
        public static int EnemyLevel(int own, IReadOnlyCollection<int> playerGroup, bool keepsUp) =>
            keepsUp && playerGroup is { Count: > 0 } ? Math.Max(own, (int)Math.Floor(playerGroup.Average())) : own;

        /// <summary>The roster an enemy group of a player is kept under: each character has its own enemies.</summary>
        public static string EnemyRoster(string playerId, string groupId) => $"{playerId}|{groupId}";

        public static int RealmLevelOf(GameLiving member) => member switch
        {
            GamePlayer player => player.RealmLevel,
            GameBot bot => bot.RealmLevel,
            _ => 0,
        };
    }
}
