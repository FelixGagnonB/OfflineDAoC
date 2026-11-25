using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DOL.GS
{
    /// <summary>
    /// What a player has chosen for its 8v8 group and keeps from one login to the next
    /// (docs/PVP-8V8-ROADMAP.md, M13).
    /// </summary>
    /// <param name="Lead">
    /// The companions fall back as one by themselves when the group has too many on it
    /// (<c>/8v8 lead</c>). On unless the player turns it off: the owner, 2026-11-24, asked
    /// whether it should be on by default, picked "On by default"; of its calls in capitals:
    /// "I like the messages when the bot decide it's time to kite and run back, it's nice".
    /// Until then it was off at every login.
    /// </param>
    public sealed record PvpPlayerSetting(bool Lead = true);

    /// <summary>The players' settings, kept in <c>pvp-players.json</c> beside the server (a played file: never in the repository).</summary>
    public static class PvpPlayerSettings
    {
        public const string FileName = "pvp-players.json";
        private static readonly object Gate = new();

        private static Dictionary<string, PvpPlayerSetting> ReadAll(string folder)
        {
            try
            {
                string path = Path.Combine(folder, FileName);
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<Dictionary<string, PvpPlayerSetting>>(File.ReadAllText(path)) ?? new()
                    : new();
            }
            catch (Exception)
            {
                // A file that cannot be read is as good as none: everybody has the settings a new player has.
                return new();
            }
        }

        /// <summary>A player's settings; those of a new player when it has none.</summary>
        public static PvpPlayerSetting Get(string folder, string player)
        {
            lock (Gate)
                return player != null && ReadAll(folder).TryGetValue(player, out PvpPlayerSetting setting) && setting != null ? setting : new PvpPlayerSetting();
        }

        /// <summary>Changes a player's settings and keeps them.</summary>
        public static PvpPlayerSetting Set(string folder, string player, Func<PvpPlayerSetting, PvpPlayerSetting> change)
        {
            lock (Gate)
            {
                Dictionary<string, PvpPlayerSetting> all = ReadAll(folder);
                PvpPlayerSetting setting = change(all.TryGetValue(player, out PvpPlayerSetting had) && had != null ? had : new PvpPlayerSetting());
                all[player] = setting;
                File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
                return setting;
            }
        }
    }
}
