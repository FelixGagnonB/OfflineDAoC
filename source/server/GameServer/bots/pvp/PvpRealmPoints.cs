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
        /// is some 500 kills shared by eight; 5 as built, so that an evening moves a rank.
        /// </summary>
        public double RealmPointRate { get; set; } = 5;

        /// <summary>
        /// On: no member of an enemy group is of a lower rank than the average of the player's
        /// group, whatever that enemy group has earned itself. A group that never wins would
        /// otherwise stay at rank 1 while the player's climbs.
        /// </summary>
        public bool EnemyKeepsUp { get; set; } = true;

        /// <summary>A member of the killing group this far from the one that died has no share.</summary>
        public int ShareWithin { get; set; } = 6_000;
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

        /// <summary>The realm level (rank 1 level 0 is 0, rank 5 level 0 is 40) that so many points make.</summary>
        public static int LevelFor(long points) => AutonomousBotRealmPointRewards.RealmLevelFor(points);

        /// <summary>The name under which a player's own earned realm points are kept in the roster, beside the companions' builds.</summary>
        public const string PlayerBuild = "player";

        /// <summary>A rank as players write it: 0 is 1L0, 40 is 5L0, 45 is 5L5.</summary>
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
