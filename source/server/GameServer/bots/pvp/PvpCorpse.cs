using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>How a client is shown an 8v8 bot that is dead and still in the world.</summary>
    public enum PvpCorpseShow
    {
        /// <summary>Nothing is said: the client draws the body standing, with no health.</summary>
        Off,
        /// <summary>The bot is drawn as a ghost, see-through, with "Dead" under its name.</summary>
        Ghost,
        /// <summary>The body falls as a dead monster's does, and the ghost stands over it to be targeted.</summary>
        Fall,
        /// <summary>The body falls, and the bot stays as a marker nobody sees: its name with "Dead" over the body, and a target for the group window.</summary>
        Body
    }

    /// <summary>
    /// A bot is a monster to the client, and the client has no dead monster that stays: a monster
    /// that dies is removed, and the removal is what lays it down, as a body the client then no
    /// longer knows. An 8v8 bot stays in the world for a groupmate to raise it, so nothing told
    /// the client it was dead. The owner, 2026-11-20: "When a character dies, its body remains up.
    /// Unless you look at his hp, you don't know he's dead." Until that day the body was sent to
    /// its spawn place as it died, which removed it for the client and hid this.
    /// Tried first and seen by the owner not to work: the packet that lays a dead player down,
    /// with the bot's id, and no position updates for the body ("The corpses still stand up and
    /// after some seconds the corpse disappear. I could not click on the dead enchanter from the
    /// mini group window"): the client ignores that packet for a monster, forgets a monster it
    /// gets no updates for, and cannot target a group member it does not have.
    /// So the client keeps the bot, with its updates, and is told what it does take: the removal
    /// of a dead monster for the falling body, and then the bot again as a ghost with "Dead"
    /// under its name, which can be clicked and targeted from the group window. Raising needs
    /// nothing: it moves the bot, which makes it anew for the client, alive.
    /// That was seen by the owner the same day, three bodies on the ground and three ghosts over
    /// them, and taken for broken ("Dead body fix doesn't work"): a dead character is a body with
    /// a name, not a figure standing. So as built the ghost is left out: the bot is made again
    /// with the model nobody sees (the one of the setup window's menu), its name and "Dead" over
    /// the body, still a target for the group window.
    /// Only 8v8 bots: companions and gamebots are shown as before. <c>/8v8 corpse</c> switches.
    /// </summary>
    public static class PvpCorpse
    {
        public static PvpCorpseShow Show { get; set; } = PvpCorpseShow.Body;

        /// <summary>The model a client draws nothing for (Pvp8v8SetupMenu uses it too), and its size.</summary>
        public const ushort MarkerModel = 150;
        public const byte MarkerSize = 1;

        /// <summary>What stands under the name of a ghost.</summary>
        public const string Tag = "Dead";

        public static bool IsDown(GameObject body) => body is GameBot { IsPvpBot: true, IsAlive: false };

        /// <summary>Whether the bot is made for a client as a ghost.</summary>
        public static bool DrawsGhost(PvpCorpseShow show, bool down) => down && show is PvpCorpseShow.Ghost or PvpCorpseShow.Fall;

        /// <summary>Whether the bot is made for a client as a marker nobody sees.</summary>
        public static bool DrawsMarker(PvpCorpseShow show, bool down) => down && show == PvpCorpseShow.Body;

        /// <summary>Whether a client is sent the removal that makes the body fall.</summary>
        public static bool BodyFalls(PvpCorpseShow show, bool down) => down && show is PvpCorpseShow.Fall or PvpCorpseShow.Body;

        public static bool IsGhost(GameObject body) => DrawsGhost(Show, IsDown(body));
        public static bool IsMarker(GameObject body) => DrawsMarker(Show, IsDown(body));

        /// <summary>The bot has just died: every client that sees it is shown.</summary>
        public static void Announce(GameBot bot)
        {
            if (Show == PvpCorpseShow.Off || !IsDown(bot) || bot.ObjectState is not GameObject.eObjectState.Active)
                return;
            foreach (GamePlayer player in bot.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player.Client.ClientState is not GameClient.eClientState.Playing || !player.CanDetect(bot))
                    continue;
                // With no health left the removal is the one of a dead monster: the body falls and lies.
                if (BodyFalls(Show, true))
                    player.Out.SendObjectRemove(bot);
                // And the bot again, as the packet library now makes it: a marker nobody sees, or a ghost.
                ClientService.CreateObjectForPlayer(player, bot);
            }
        }

        /// <summary>Another way of showing the dead, from now on and for those already dead. Returns how many those are.</summary>
        public static int Switch(PvpCorpseShow show, IEnumerable<GameBot> bots)
        {
            List<GameBot> down = bots.Where(bot => IsDown(bot) && bot.ObjectState is GameObject.eObjectState.Active).ToList();
            Show = show;
            foreach (GameBot bot in down)
            {
                if (show == PvpCorpseShow.Off)
                    ClientService.CreateObjectForPlayers(bot);
                else
                    Announce(bot);
            }
            return down.Count;
        }
    }
}
