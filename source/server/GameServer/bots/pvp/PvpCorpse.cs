using System.Collections.Generic;
using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>How a client is shown an 8v8 bot that is dead and still in the world.</summary>
    public enum PvpCorpseShow
    {
        /// <summary>Nothing is said: the client draws the body standing, with no health.</summary>
        Off,
        /// <summary>The packet that lays a dead player down, with the bot's id. The client keeps the body, so it can be targeted.</summary>
        Died,
        /// <summary>The removal a dead monster gets: the client lays the body down and forgets it until it is raised.</summary>
        Removed
    }

    /// <summary>
    /// A bot is a monster to the client, and the client has no dead monster that stays: a monster
    /// that dies is removed, and the removal is what lays it down. An 8v8 bot stays in the world
    /// for a groupmate to raise it, so nothing told the client it was dead. The owner, 2026-11-20:
    /// "When a character dies, its body remains up. Unless you look at his hp, you don't know he's
    /// dead." Until that day the body was sent to its spawn place as it died, which removed it for
    /// the client and hid this.
    /// Which way the client takes was not known when this was written, so there is a switch
    /// (<c>/8v8 corpse</c>). Raising needs nothing: it moves the bot, which makes it anew for the client.
    /// Only 8v8 bots: companions and gamebots are shown as before.
    /// </summary>
    public static class PvpCorpse
    {
        public static PvpCorpseShow Show { get; set; } = PvpCorpseShow.Died;

        public static bool IsDown(GameObject body) => body is GameBot { IsPvpBot: true, IsAlive: false };

        /// <summary>Whether a position update goes out. One for a body would stand it up again, or ask the client to make it anew.</summary>
        public static bool SendsUpdate(PvpCorpseShow show, bool down) => !down || show == PvpCorpseShow.Off;

        /// <summary>Whether the body is made for a client that does not have it.</summary>
        public static bool SendsCreate(PvpCorpseShow show, bool down) => !down || show != PvpCorpseShow.Removed;

        /// <summary>Whether a body just made for a client is laid down at once.</summary>
        public static bool SendsDied(PvpCorpseShow show, bool down) => down && show == PvpCorpseShow.Died;

        public static bool HidesUpdate(GameObject body) => !SendsUpdate(Show, IsDown(body));
        public static bool HidesCreate(GameObject body) => !SendsCreate(Show, IsDown(body));
        public static bool LaysDown(GameObject body) => SendsDied(Show, IsDown(body));

        /// <summary>The bot has just died: every client that sees it is told.</summary>
        public static void Announce(GameBot bot)
        {
            if (Show == PvpCorpseShow.Off || !IsDown(bot) || bot.ObjectState is not GameObject.eObjectState.Active)
                return;
            foreach (GamePlayer player in bot.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player.Client.ClientState is not GameClient.eClientState.Playing || !player.CanDetect(bot))
                    continue;
                // Where it fell and that it has no health left, in order with what follows.
                player.Out.SendObjectUpdate(bot, false);
                if (Show == PvpCorpseShow.Died)
                    SendDied(player, bot);
                else
                    player.Out.SendObjectRemove(bot);
            }
        }

        /// <summary>The packet a client gets when a player dies, for a bot.</summary>
        public static void SendDied(GamePlayer player, GameObject body)
        {
            using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(AbstractPacketLib.GetPacketCode(eServerPackets.PlayerDeath));
            pak.WriteShort((ushort)body.ObjectID);
            pak.WriteShort(0x00);
            pak.Fill(0x0, 4);
            player.Out.SendTCP(pak);
        }

        /// <summary>Another way of showing the dead, from now on and for the bodies already lying. Returns how many those are.</summary>
        public static int Switch(PvpCorpseShow show, IEnumerable<GameBot> bots)
        {
            List<GameBot> down = bots.Where(bot => IsDown(bot) && bot.ObjectState is GameObject.eObjectState.Active).ToList();
            Show = PvpCorpseShow.Off;
            foreach (GameBot bot in down)
                ClientService.CreateObjectForPlayers(bot);
            Show = show;
            foreach (GameBot bot in down)
                Announce(bot);
            return down.Count;
        }
    }
}
