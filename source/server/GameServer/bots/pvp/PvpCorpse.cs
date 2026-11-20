using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.GS.PacketHandler;

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
        Body,
        /// <summary>The client is given a dead player in the bot's place: it lies on the ground in its gear and can be clicked.</summary>
        Player
    }

    /// <summary>
    /// A bot is a monster to the client, and the client has no dead monster that stays: a monster
    /// that dies is removed, and the removal is what lays it down, as a body the client then no
    /// longer knows. An 8v8 bot stays in the world for a groupmate to raise it, so nothing told
    /// the client it was dead. Until 2026-11-20 the body was sent to its spawn place as it died,
    /// which removed it for the client and hid this. The owner that day, try after try:
    /// - "When a character dies, its body remains up. Unless you look at his hp, you don't know
    ///   he's dead."
    /// - of the packet that lays a dead player down, sent with the bot's id, and no position
    ///   updates: "The corpses still stand up and after some seconds the corpse disappear. I could
    ///   not click on the dead enchanter from the mini group window". The client ignores that
    ///   packet for a monster, forgets a monster it gets no updates for, and cannot target a
    ///   group member it does not have.
    /// - of the monster's removal for the falling body and the bot again as a ghost over it:
    ///   "Dead body fix doesn't work."
    /// - "When a bot dies, it should lay on the ground, like a human and be clickable. It needs to
    ///   be targetable to be rez. It cannot stay standup, it's way too confusing and wrong".
    /// So the client is given a human: while the bot is dead the monster is taken from the client
    /// and a dead player is made in its place, under the same object id (what a click or the group
    /// window sends back, and the server finds the bot by) and a session id no client has. It
    /// gets a player's position packet in the dead state where the monster got its updates.
    /// Raising moves the bot, which removes that player and makes the monster anew, alive.
    /// Only 8v8 bots: companions and gamebots are shown as before. <c>/8v8 corpse</c> switches.
    /// </summary>
    public static class PvpCorpse
    {
        public static PvpCorpseShow Show { get; set; } = PvpCorpseShow.Player;

        /// <summary>The model a client draws nothing for (Pvp8v8SetupMenu uses it too), and its size.</summary>
        public const ushort MarkerModel = 150;
        public const byte MarkerSize = 1;

        /// <summary>What stands under the name of a ghost or a marker.</summary>
        public const string Tag = "Dead";

        /// <summary>A player's position packet: sitting and swimming together is dead (PlayerPositionUpdateHandler.StateFlags.DEAD).</summary>
        private const byte DeadState = 0x14;

        public static bool IsDown(GameObject body) => body is GameBot { IsPvpBot: true, IsAlive: false };

        /// <summary>Whether the bot is made for a client as a ghost.</summary>
        public static bool DrawsGhost(PvpCorpseShow show, bool down) => down && show is PvpCorpseShow.Ghost or PvpCorpseShow.Fall;

        /// <summary>Whether the bot is made for a client as a marker nobody sees.</summary>
        public static bool DrawsMarker(PvpCorpseShow show, bool down) => down && show == PvpCorpseShow.Body;

        /// <summary>Whether a client is sent the removal that makes a monster's body fall.</summary>
        public static bool BodyFalls(PvpCorpseShow show, bool down) => down && show is PvpCorpseShow.Fall or PvpCorpseShow.Body;

        /// <summary>Whether the client is given a dead player in the bot's place.</summary>
        public static bool DrawsPlayer(PvpCorpseShow show, bool down) => down && show == PvpCorpseShow.Player;

        public static bool IsGhost(GameObject body) => DrawsGhost(Show, IsDown(body));
        public static bool IsMarker(GameObject body) => DrawsMarker(Show, IsDown(body));
        public static bool IsPlayerBody(GameObject body) => DrawsPlayer(Show, IsDown(body));

        /// <summary>
        /// The model word of a player: the model in the low eleven bits and a size of 1 to 3 above
        /// them (a player with size 0 is refused at creation). A bot's model is the bare number.
        /// </summary>
        public static ushort PlayerModel(ushort model) => (ushort)((model & 0x7FF) | (2 << 11));

        /// <summary>A session id for a body: above any a client of this server gets, and well inside what the client takes.</summary>
        public static ushort SessionFor(int counter) => (ushort)(1000 + Math.Abs(counter % 2000));

        private static int _sessions;

        /// <summary>Set while a bot that has just died is made: it is made standing, and the death packet lays it down.</summary>
        [ThreadStatic]
        private static bool _falling;

        /// <summary>The bot has just died: every client that sees it is shown.</summary>
        public static void Announce(GameBot bot)
        {
            if (Show == PvpCorpseShow.Off || !IsDown(bot) || bot.ObjectState is not GameObject.eObjectState.Active)
                return;
            foreach (GamePlayer player in bot.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player.Client.ClientState is not GameClient.eClientState.Playing || !player.CanDetect(bot))
                    continue;
                if (Show == PvpCorpseShow.Player)
                {
                    // The monster goes, a player comes in its place standing, and dies as a player does.
                    player.Out.SendObjectDelete(bot);
                    _falling = true;
                    try
                    {
                        ClientService.CreateObjectForPlayer(player, bot);
                    }
                    finally
                    {
                        _falling = false;
                    }
                    SendDied(player, bot);
                    SendBodyPosition(player, bot);
                    continue;
                }
                // With no health left the removal is the one of a dead monster: the body falls and lies.
                if (BodyFalls(Show, true))
                    player.Out.SendObjectRemove(bot);
                // And the bot again, as the packet library now makes it: a marker nobody sees, or a ghost.
                ClientService.CreateObjectForPlayer(player, bot);
            }
        }

        /// <summary>The dead player a client gets in the bot's place (the layout of PacketLib1124.SendPlayerCreate).</summary>
        public static void SendBodyCreate(GamePlayer viewer, GameBot bot)
        {
            if (bot.CurrentZone == null)
                return;
            if (bot.PvpBodySession == 0)
                bot.PvpBodySession = SessionFor(Interlocked.Increment(ref _sessions));
            bot.PvpShownAsBody = true;
            using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(AbstractPacketLib.GetPacketCode(eServerPackets.PlayerCreate172));
            pak.WriteFloatLowEndian(bot.X);
            pak.WriteFloatLowEndian(bot.Y);
            pak.WriteFloatLowEndian(bot.Z);
            pak.WriteShort(bot.PvpBodySession);
            pak.WriteShort((ushort)bot.ObjectID);
            pak.WriteShort(bot.Heading);
            pak.WriteShort(PlayerModel(bot.Model));
            pak.WriteByte(bot.GetDisplayLevel(viewer));
            int flags = (GameServer.ServerRules.GetLivingRealm(viewer, bot) & 0x03) << 2;
            if (!_falling)
                flags |= 0x01; // dead
            pak.WriteByte((byte)flags);
            pak.Fill(0x00, 7);  // the face: the client's own for the model
            pak.Fill(0x00, 3);
            pak.WritePascalString(bot.Name);
            pak.WritePascalString(bot.GuildName ?? string.Empty);
            pak.WritePascalString(string.Empty); // last name
            pak.WritePascalString(string.Empty); // prefix
            pak.WritePascalString(string.Empty); // title
            pak.WriteByte(0);                    // no horse
            viewer.Out.SendTCP(pak);
        }

        /// <summary>A player's position packet in the dead state, where the monster got its update (PlayerPositionUpdateHandler).</summary>
        public static void SendBodyPosition(GamePlayer viewer, GameBot bot)
        {
            if (bot.CurrentZone == null || bot.PvpBodySession == 0 || viewer.Client.Version < GameClient.eClientVersion.Version1124)
                return;
            bool since1127 = viewer.Client.Version >= GameClient.eClientVersion.Version1127;
            GSUDPPacketOut pak = PooledObjectFactory.GetForTick<GSUDPPacketOut>().Init(AbstractPacketLib.GetPacketCode(eServerPackets.PlayerPosition));
            pak.WriteFloatLowEndian(bot.X);
            pak.WriteFloatLowEndian(bot.Y);
            pak.WriteFloatLowEndian(bot.Z);
            pak.WriteFloatLowEndian(0);          // speed
            pak.WriteFloatLowEndian(0);          // fall speed
            pak.WriteShort(bot.PvpBodySession);
            if (since1127)
                pak.WriteShort((ushort)bot.ObjectID);
            pak.WriteShort(bot.CurrentZone.ID);
            pak.WriteByte(DeadState);
            pak.WriteByte(0);
            pak.WriteShort(0);                   // no steed
            pak.WriteShort(bot.Heading);
            pak.WriteByte(0);                    // action flags
            pak.WriteByte(0);                    // roleplay flag
            pak.WriteByte(0);
            pak.WriteByte(0);                    // health
            pak.WriteByte(bot.ManaPercent);
            pak.WriteByte(bot.EndurancePercent);
            if (since1127)
                pak.WriteShort(0);
            viewer.Out.SendUDP(pak);
        }

        /// <summary>The packet a client gets when a player dies.</summary>
        public static void SendDied(GamePlayer player, GameObject body)
        {
            using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(AbstractPacketLib.GetPacketCode(eServerPackets.PlayerDeath));
            pak.WriteShort((ushort)body.ObjectID);
            pak.WriteShort(0x00);
            pak.Fill(0x0, 4);
            player.Out.SendTCP(pak);
        }

        /// <summary>Another way of showing the dead, from now on and for those already dead. Returns how many those are.</summary>
        public static int Switch(PvpCorpseShow show, IEnumerable<GameBot> bots)
        {
            List<GameBot> down = bots.Where(bot => IsDown(bot) && bot.ObjectState is GameObject.eObjectState.Active).ToList();
            // What a client has of each is taken away first: a player (the removal knows which), or a monster.
            foreach (GameBot bot in down)
            {
                foreach (GamePlayer player in bot.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (bot.PvpShownAsBody)
                        player.Out.SendObjectRemove(bot);
                    else
                        player.Out.SendObjectDelete(bot);
                }
                bot.PvpShownAsBody = false;
            }
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
