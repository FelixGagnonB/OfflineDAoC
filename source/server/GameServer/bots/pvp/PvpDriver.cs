using System;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// A bot at the wheel of the player's group (docs/PVP-8V8-ROADMAP.md, M13.24). The owner,
    /// 2026-11-25: "Would there be a way that when I do not play bard/sorc/minstrel/skald, 1 of
    /// the bot drive? It could say in group chat 'Stick moving out' so we when we are moving.
    /// Ideally, the driver would be: bard; sorc or minstrel; pac healer or skald". Asked how far
    /// the driving goes: "Roam and engage"; asked when: "Automatic, with an off switch".
    ///
    /// So: unless the player's own character is one of those that drove a group (or has said
    /// <c>/8v8 drive off</c>), its group is driven by one of its seven. The group then goes from
    /// point to point of its zone as a guild of bots does and takes on what it meets as a decent
    /// one does: never into a fight that is on, not into more groups than it is, by the rules of
    /// the zone it is in. The driver says in group chat when it moves out, waits for a player
    /// that has fallen behind and stays by one that is down. Everything the player calls
    /// (/8v8 call ...) still holds, and an enemy the player strikes or calls is the group's.
    /// The player's group is never taken off the field by itself, as a group of bots that
    /// cannot raise its dead is: that stays the player's to say (/8v8 call die, /release).
    /// </summary>
    public static class PvpDriver
    {
        /// <summary>What a player has said this login: "on", "off"; nothing said: what it keeps, else by its class.</summary>
        public const string Property = "PVP_8V8_DRIVE";

        /// <summary>The driver stops when its player is farther behind than the first, and goes on when it is within the second.</summary>
        public const int WaitsBeyond = 1_500, GoesOnWithin = 800;
        /// <summary>A group that has just stood up is not driven off before its buffs are on; and the driver does not say the same thing again sooner than this.</summary>
        public const int HoldsAtStartSeconds = 30, SaysAgainAfterSeconds = 20;

        /// <summary>The characters that drove a group: the owner's list. A player of one of them drives its own.</summary>
        public static bool IsADriver(eCharacterClass characterClass, bool pacification) =>
            characterClass is eCharacterClass.Bard or eCharacterClass.Sorcerer or eCharacterClass.Minstrel or eCharacterClass.Skald ||
            characterClass == eCharacterClass.Healer && pacification;

        /// <summary>
        /// Who of a group drives, the lower the sooner: the Bard; the Sorcerer, then the Minstrel;
        /// the Pacification Healer, then the Skald. -1 for a member that does not.
        /// </summary>
        public static int Rank(eCharacterClass characterClass, string build) => characterClass switch
        {
            eCharacterClass.Bard or eCharacterClass.Sorcerer => 0,
            eCharacterClass.Healer when build?.Contains("pac", StringComparison.OrdinalIgnoreCase) == true => 0,
            eCharacterClass.Minstrel or eCharacterClass.Skald => 1,
            _ => -1,
        };

        /// <summary>Whether a bot drives the player's group: what the player has said, else whenever the player's own character is no driver; and one of its seven is there to do it.</summary>
        public static bool BotDrives(bool? said, bool playerIsADriver, bool aCompanionIsUp) => aCompanionIsUp && (said ?? !playerIsADriver);

        /// <summary>The driver stands for its player: when it is farther behind than it should be, until it has come up.</summary>
        public static bool Waits(bool waitingAlready, double playerFromTheDriver, bool playerIsUp) =>
            playerIsUp && playerFromTheDriver > (waitingAlready ? GoesOnWithin : WaitsBeyond);

        /// <summary>
        /// Beyond this the driver does not stand and wait: it brings the group to its player. The owner, 2026-11-25, in
        /// Thidranki: "we are 3 dead after the fight... I released. I'm at our spawn point ... and bard is waiting for me ?"
        /// It stood 20,000 units away with the other six and said "Waiting for you". A player a little behind is on its
        /// way and is waited for; one this far has released, or is lost.
        /// </summary>
        public const int ComesBackBeyond = 4_000;

        /// <summary>The driver brings the group to its player: when it is this far behind (<see cref="ComesBackBeyond"/>), until the group has come up to it.</summary>
        public static bool ComesForItsPlayer(bool comingAlready, double playerFromTheDriver, bool playerIsUp) =>
            playerIsUp && playerFromTheDriver > (comingAlready ? GoesOnWithin : ComesBackBeyond);

        /// <summary>
        /// Whether a group with a companion at the wheel goes for a fight it is not in yet: with its player up and no
        /// farther behind than the driver waits for (<see cref="WaitsBeyond"/>). The owner, 2026-11-25, swimming a river in
        /// Thidranki that its group had run across: the group picked an enemy 8,000 units on and went for it, and the fight
        /// began with its player 7,700 behind ("they just flew and abandonned me").
        /// </summary>
        public static bool GoesForAFight(bool playerIsUp, double playerFromTheDriver) => playerIsUp && playerFromTheDriver <= WaitsBeyond;

        /// <summary>For how long a group one of its own drives still knows of a group that has gone out of its sight (behind a hill).</summary>
        public const int RemembersForSeconds = 20;

        /// <summary>
        /// Which enemy groups a group with a companion at the wheel knows of, and so may go for:
        /// one it is fighting, one that attacks it, and one that one of its seven sees (as far as
        /// they call "INC!", PvpRoam.SightingWithin, and not through a hill) or has seen within
        /// <see cref="RemembersForSeconds"/>. It picked any group within 9,000 units, seen or
        /// not. The owner, 2026-11-26: "Maybe we should reduce the distance my group see bot? In
        /// real life, we have no clue where enemies are, beside checking realm chat and asking
        /// people dying. 9000+ units away with no LoS feels a bit cheating."
        /// </summary>
        /// <param name="secondsSinceItSawIt">Null: it has not seen that group.</param>
        public static bool KnowsOf(bool fightingIt, bool itAttacks, double? secondsSinceItSawIt) =>
            fightingIt || itAttacks || secondsSinceItSawIt is { } ago && ago <= RemembersForSeconds;

        /// <summary>
        /// The pace of a group one of its own drives: no faster than its player can run right now, and a little slower
        /// while its player is behind, so that it comes up. 0: its own speed. The owner, 2026-11-26: "make sticking to the
        /// driving bot a smoother experience". In its last session the group set off to rest after a fight at 490 units a
        /// second while the speed song was not on its player yet (190, then 260 to 300, for three seconds): it was 1,300
        /// units behind at once. And a player 500 units behind a driver as fast as itself stayed 500 behind, second after
        /// second: on /stick it never came up. Not in a fight, and not for a player that cannot move at all.
        /// </summary>
        public const int EasesOffBeyond = 300, EasesOffUntilWithin = 150, NoPaceUnder = 150;
        public const double EasedShare = 0.85;

        public static int Pace(bool aCompanionDrives, bool playerIsUp, int playerCanRun, bool inAFight, bool eased) =>
            !aCompanionDrives || !playerIsUp || inAFight || playerCanRun < NoPaceUnder ? 0 : eased ? (int)(playerCanRun * EasedShare) : playerCanRun;

        /// <summary>Whether the driver eases off (<see cref="Pace"/>): its player is behind it by more than the first, until it is within the second.</summary>
        public static bool EasesOff(bool easedAlready, double playerFromTheDriver, bool playerIsBehindIt) =>
            playerIsBehindIt && playerFromTheDriver > (easedAlready ? EasesOffUntilWithin : EasesOffBeyond);

        public static string ComingFor(string player) => $"Coming to you, {player}.";

        /// <summary>
        /// Said to a player that lies dead with nobody left to fight, by a companion that can raise: the owner lay a minute
        /// beside a silent group that was getting its power back to raise it, and released.
        /// </summary>
        public static string WillRaise(string player) => $"Stay there, {player}: we will raise you.";

        /// <summary>See <see cref="HoldsAtStartSeconds"/>.</summary>
        public static bool HoldsAtStart(long millisecondsSinceItStoodUp) => millisecondsSinceItStoodUp < HoldsAtStartSeconds * 1_000L;

        public static string MovingOut(string place) => string.IsNullOrEmpty(place) ? "Stick, moving out." : $"Stick, moving out: {place}.";
        public static string WaitingFor(string player) => $"Waiting for you, {player}.";
        public const string Resting = "Resting here.";

        /// <summary>
        /// The driver does not stand by a fight that is not the group's, as a guild of bots does for a while: it goes on,
        /// back the way it came or round the fight, and says nothing of it. The owner, 2026-11-25, after its group had
        /// stood half a minute by a fight of a group of its realm ("we are idle ... I assume there's no enemy groups
        /// available to fight ?"), and offered a line of chat that says why: "In that case, the group and still run
        /// around, while waiting. We are not suppose to know there are no available fight".
        /// </summary>
        public static bool GoesOnPastAFight(bool aCompanionDrives, bool aFightItLeftAloneIsNear, bool goingOnAlready) =>
            aCompanionDrives && aFightItLeftAloneIsNear && !goingOnAlready;

        /// <summary>What a player has said of it (<c>/8v8 drive on|off</c>): this login, else what it keeps; null when it has never said.</summary>
        public static bool? Said(GamePlayer player)
        {
            if (player == null)
                return null;
            if (player.TempProperties.GetProperty<string>(Property) is { } said)
                return said == "on" ? true : said == "off" ? false : null;
            bool? kept = PvpPlayerSettings.Get(AppContext.BaseDirectory, player.InternalID).Drive;
            player.TempProperties.SetProperty(Property, kept == true ? "on" : kept == false ? "off" : "auto");
            return kept;
        }

        /// <summary>The player says who drives, and it is kept.</summary>
        public static void Set(GamePlayer player, bool on)
        {
            player.TempProperties.SetProperty(Property, on ? "on" : "off");
            PvpPlayerSettings.Set(AppContext.BaseDirectory, player.InternalID, setting => setting with { Drive = on });
        }

        /// <summary>Whether the player's own character is one that drove a group (a Healer: when Pacification is the line it has trained most).</summary>
        public static bool PlayerIsADriver(GamePlayer player)
        {
            if (player == null)
                return false;
            var itsClass = (eCharacterClass)player.CharacterClass.ID;
            bool pacification = itsClass == eCharacterClass.Healer &&
                                player.GetBaseSpecLevel(Specs.Pacification) >= Math.Max(player.GetBaseSpecLevel(Specs.Mending), player.GetBaseSpecLevel(Specs.Augmentation));
            return IsADriver(itsClass, pacification);
        }

        /// <summary>What <c>/8v8 drive</c> answers.</summary>
        public static string Answer(bool botDrives, bool? said, bool playerIsADriver) =>
            botDrives
                ? "One of your companions drives: it takes the group from point to point and at the enemy groups it meets, says when it moves out, and waits for you. " +
                  "Stick to it (/stick). Your calls still hold. /8v8 drive off to drive yourself; it is kept."
                : said == false ? "You drive: your companions go where you go. /8v8 drive on to let one of them drive; it is kept."
                : playerIsADriver ? "You drive: your character is one that drove a group. /8v8 drive on to let one of your companions drive; it is kept."
                : "You drive for now: none of your companions is up to do it.";
    }
}
