using System;
using System.Collections.Concurrent;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// What a GM who watches a scrim reads in the chat window: each group's calls as it makes
    /// them (its order, its kill target, who is being trained, who died), so that what is seen can
    /// be set beside what the group meant to do. Nothing here decides anything; with nobody
    /// watching it costs one read.
    /// </summary>
    public static class PvpObserver
    {
        /// <summary>A watcher reads what happens within this distance of where it stands.</summary>
        public const int Reach = 7_000;

        private static readonly ConcurrentDictionary<GamePlayer, byte> Watchers = new();

        public static bool Any => !Watchers.IsEmpty;

        public static bool Watches(GamePlayer player) => player != null && Watchers.ContainsKey(player);

        public static void Set(GamePlayer player, bool on)
        {
            if (player == null)
                return;
            if (on)
                Watchers[player] = 0;
            else
                Watchers.TryRemove(player, out _);
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
            foreach (GamePlayer player in Watchers.Keys)
            {
                try
                {
                    if (player.ObjectState != GameObject.eObjectState.Active || player.Client?.IsPlaying != true)
                    {
                        Watchers.TryRemove(player, out _);
                        continue;
                    }
                    if (player.CurrentRegionID != where.CurrentRegionID || !player.IsWithinRadius(where, Reach))
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
