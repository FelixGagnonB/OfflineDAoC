using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DOL.GS
{
    /// <summary>
    /// One companion of a player's 8v8 group, or one member of an enemy group of that player: its
    /// build, which of that build it is, its name and sex, and the realm points it has earned in
    /// real sessions (bots/pvp/PvpRealmPoints.cs; 0 for one that has been in none).
    /// </summary>
    public sealed record PvpRosterEntry(string Build, int Place, string Name, int Gender, long RealmPoints = 0);

    /// <summary>
    /// A player's lasting companions (docs/PVP-8V8-ROADMAP.md, M9.1, the first half): the same
    /// seven by name from one session to the next, so that the Cleric who was out of place
    /// yesterday is the same Cleric today. Kept beside the server in <see cref="FileName"/>, by
    /// character; it is the player's own data and is not part of any release. Realm points
    /// earned by the companions are not in it yet.
    /// </summary>
    public static class PvpRoster
    {
        public const string FileName = "pvp-rosters.json";
        private static readonly object Gate = new();

        /// <summary>Which of its build a member of a group is: 0 for the first Blademaster, 1 for the second.</summary>
        public static int PlaceOf(IReadOnlyList<string> builds, int index) =>
            builds.Take(index).Count(build => string.Equals(build, builds[index], StringComparison.OrdinalIgnoreCase));

        public static PvpRosterEntry Find(IEnumerable<PvpRosterEntry> roster, string build, int place) =>
            roster.FirstOrDefault(entry => entry.Place == place && string.Equals(entry.Build, build, StringComparison.OrdinalIgnoreCase));

        /// <summary>The companions kept for a character; empty when there are none, or the file cannot be read.</summary>
        public static List<PvpRosterEntry> Load(string folder, string owner)
        {
            lock (Gate)
                return ReadAll(folder).TryGetValue(owner ?? string.Empty, out List<PvpRosterEntry> roster) ? roster : [];
        }

        /// <summary>Keeps a character's companions. Never throws: a roster that cannot be written is only not kept.</summary>
        public static void Save(string folder, string owner, List<PvpRosterEntry> roster)
        {
            if (string.IsNullOrEmpty(owner))
                return;
            lock (Gate)
            {
                try
                {
                    Dictionary<string, List<PvpRosterEntry>> all = ReadAll(folder);
                    all[owner] = roster;
                    File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>A roster with realm points added to some of its members (those it has; the others are left out). Nothing is written.</summary>
        public static List<PvpRosterEntry> Credited(IEnumerable<PvpRosterEntry> roster, IReadOnlyDictionary<(string Build, int Place), long> earned) =>
            roster.Select(entry => earned.TryGetValue((entry.Build.ToLowerInvariant(), entry.Place), out long points) && points > 0
                ? entry with { RealmPoints = entry.RealmPoints + points } : entry).ToList();

        /// <summary>Adds realm points to members of a kept roster. Never throws.</summary>
        public static void Credit(string folder, string owner, IReadOnlyDictionary<(string Build, int Place), long> earned)
        {
            if (string.IsNullOrEmpty(owner) || earned.Count == 0)
                return;
            lock (Gate)
                Save(folder, owner, Credited(Load(folder, owner), earned));
        }

        private static Dictionary<string, List<PvpRosterEntry>> ReadAll(string folder)
        {
            try
            {
                string path = Path.Combine(folder, FileName);
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<Dictionary<string, List<PvpRosterEntry>>>(File.ReadAllText(path)) ?? new()
                    : new();
            }
            catch (Exception)
            {
                return new();
            }
        }
    }
}
