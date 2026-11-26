using System.Collections.Generic;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// The line every player of a frontier read when somebody of its realm died there or killed
    /// there: "Arngenmund was just killed by Jade in Agramon." The game says it of a player's
    /// character that dies (GamePlayer.Die); an 8v8 bot is not one, and of its death only those
    /// who stood by it read "... dies!". The owner, 2026-11-26: "on real server, when someone
    /// die, there's a message broadcasted. Who killed who and region (like: Jade killed BotA in
    /// pennine). I just saw there's no indication in-game", and of where enemies are: "In real
    /// life, we have no clue where enemies are, beside checking realm chat and asking people
    /// dying."
    /// </summary>
    public static class PvpDeathNews
    {
        /// <summary>
        /// Who reads of a death, as the game has it for a player's character on a server of three
        /// realms: the realm of the one that died and the realm of the one that killed it, in the
        /// same region. A death between the two other realms is not this realm's news.
        /// </summary>
        public static bool Reads(eRealm reader, eRealm died, eRealm? killedBy, bool inTheSameRegion) =>
            inTheSameRegion && reader != eRealm.None && (reader == died || reader == killedBy);

        /// <summary>The game's own words (GamePlayer.Die.KilledByLocation, GamePlayer.Die.KilledBy), and "was just killed" with nobody to name.</summary>
        public static string Text(string died, string killedBy, string zone) =>
            killedBy == null ? (zone == null ? $"{died} was just killed." : $"{died} was just killed in {zone}.")
            : zone == null ? $"{died} was just killed by {killedBy}." : $"{died} was just killed by {killedBy} in {zone}.";

        /// <summary>The colour of the line: the realm that killed, as the game has it.</summary>
        public static eChatType Colour(eRealm? killedBy) => killedBy switch
        {
            eRealm.Albion => eChatType.CT_KilledByAlb,
            eRealm.Midgard => eChatType.CT_KilledByMid,
            eRealm.Hibernia => eChatType.CT_KilledByHib,
            _ => eChatType.CT_PlayerDied,
        };

        /// <summary>
        /// Tells the players of an evening of a bot's death. (Of a player's own death the game
        /// tells already.) Never throws: a line of chat must not be able to stop a death.
        /// </summary>
        /// <param name="killerName">The one that killed it; of a pet, its owner.</param>
        public static void Tell(GameLiving died, GameLiving killer, string killerName, IEnumerable<GamePlayer> players)
        {
            try
            {
                if (died is not GameBot)
                    return;
                eRealm? killedBy = killer?.Realm;
                string text = Text(died.Name, killerName, died.CurrentZone?.Description);
                foreach (GamePlayer player in players)
                {
                    if (player?.Client?.IsPlaying == true && Reads(player.Realm, died.Realm, killedBy, player.CurrentRegionID == died.CurrentRegionID))
                        player.Out.SendMessage(text, Colour(killedBy), eChatLoc.CL_SystemWindow);
                }
            }
            catch
            {
                // (Nothing to tell.)
            }
        }
    }
}
