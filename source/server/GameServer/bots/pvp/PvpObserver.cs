using System;
using System.Collections.Concurrent;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// What a GM who watches a scrim reads in the chat window: each group's calls as it makes
    /// them (its order, its kill target, who is being trained, who died), so that what is seen can
    /// be set beside what the group meant to do. A watcher reads one lane: the one it asked for
    /// with /8v8 watch, else the one it stands in. Nothing here decides anything; with nobody
    /// watching it costs one read.
    /// </summary>
    public static class PvpObserver
    {
        /// <summary>Outside the arena's lanes a watcher reads what happens within this distance of where it stands.</summary>
        public const int Reach = 7_000;

        /// <summary>Each watcher and the lane it asked for; -1 for the lane it stands in.</summary>
        private static readonly ConcurrentDictionary<GamePlayer, int> Watchers = new();

        public static bool Any => !Watchers.IsEmpty;

        public static bool Watches(GamePlayer player) => player != null && Watchers.ContainsKey(player);

        public static void Set(GamePlayer player, bool on, int lane = -1)
        {
            if (player == null)
                return;
            if (on)
                Watchers[player] = lane;
            else
                Watchers.TryRemove(player, out _);
        }

        /// <summary>
        /// The arena lane a place belongs to: the one whose middle is nearest; -1 in another
        /// region. The owner, 2026-11-19: the feed carried every lane's calls at once and could
        /// not be followed. Two lanes' middles are 6,000 units apart, inside the old reach.
        /// </summary>
        public static int LaneOf(ushort region, int x, int y)
        {
            int best = -1;
            long nearest = long.MaxValue;
            for (int lane = 0; lane < PvpArenaBox.EmainLanes.Count; lane++)
            {
                PvpArenaBox box = PvpArenaBox.EmainLanes[lane];
                if (box.Region != region)
                    continue;
                long dx = (box.SpawnA.X + box.SpawnB.X) / 2 - x, dy = (box.SpawnA.Y + box.SpawnB.Y) / 2 - y;
                if (dx * dx + dy * dy < nearest)
                {
                    nearest = dx * dx + dy * dy;
                    best = lane;
                }
            }
            return best;
        }

        /// <summary>"Sigenvar (Midgard healer-pac)": the name over its head, then what it is.</summary>
        public static string Name(GameBot bot) =>
            bot == null ? "nobody" : $"{bot.Name} ({GlobalConstants.RealmToName(bot.Realm)} {bot.PvpBuild?.Id ?? "bot"})";

        public static string Realm(GameBot bot) => bot == null ? "?" : GlobalConstants.RealmToName(bot.Realm);

        /// <summary>One line to every watcher near <paramref name="where"/>. Never throws: a watcher must not be able to stop a match.</summary>
        public static void Say(GameObject where, string text)
        {
            if (Watchers.IsEmpty || where == null)
                return;
            int lane = LaneOf(where.CurrentRegionID, where.X, where.Y);
            foreach ((GamePlayer player, int asked) in Watchers)
            {
                try
                {
                    if (player.ObjectState != GameObject.eObjectState.Active || player.Client?.IsPlaying != true)
                    {
                        Watchers.TryRemove(player, out _);
                        continue;
                    }
                    if (player.CurrentRegionID != where.CurrentRegionID)
                        continue;
                    if (lane < 0 ? !player.IsWithinRadius(where, Reach) : lane != (asked >= 0 ? asked : LaneOf(player.CurrentRegionID, player.X, player.Y)))
                        continue;
                    player.Out.SendMessage("[8v8] " + text, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                catch (Exception)
                {
                    Watchers.TryRemove(player, out _);
                }
            }
        }
    }
}
