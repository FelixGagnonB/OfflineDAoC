using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DOL.GS
{
    /// <summary>
    /// The third layer of the learning tool (docs/PVP-8V8-ROADMAP.md, M6): a copy of the weights in
    /// which a share of them, picked at random, is moved one step down or up. Each team of each
    /// match of a batch fights with its own draw, and the recording keeps which weights went which
    /// way, so that several hundred matches show which weights move the result at all
    /// (source/tools/pvp_insight.py --weights). Nothing here decides anything in a fight.
    /// </summary>
    public static class PvpWeightDraw
    {
        /// <summary>
        /// Never moved: facts of the game (reaches and ranges), a switch that is off on purpose,
        /// and a weight read once when a bot is built, before its team's draw exists.
        /// </summary>
        private static readonly HashSet<string> Fixed =
        [
            "ConsiderRange", "Peel.ConsiderRange", "Peel.MeleeReach", "Interrupt.Range", "Purge.GroupRange",
            "Position.WorkRange", "Position.HealRange", "Heal.CureNearsightMaxCastTime", "Heal.AnytimeHealPercentOfBig",
            "Kill.MaxDistance",
        ];

        // A percentage of health or of matches: moved by points and kept between 0 and 100.
        private static readonly Regex Threshold = new("Percent|Below|Chance", RegexOptions.Compiled);
        // A distance, a time or a count: moved by a share of itself.
        private static readonly Regex Physical = new("Distance|Within|Spacing|Step|Seconds|Milliseconds|Margin|Gain|Advantage|MinHurt|Attackers", RegexOptions.Compiled);

        /// <summary>
        /// A copy of <paramref name="from"/> with about <paramref name="sharePercent"/> of its
        /// weights moved, half of those down and half up. A score moves by
        /// <paramref name="spreadPercent"/> of itself and at least ten points (four for a small
        /// one), a distance, time or factor by that share of itself, a health percentage by fifteen
        /// points at the default spread of forty; a switch is flipped half as often as a number is
        /// moved. <see cref="PvpWeights.Drawn"/> of the copy lists what moved: -1 down, +1 up or flipped.
        /// </summary>
        public static PvpWeights Vary(PvpWeights from, Random random, int sharePercent, int spreadPercent = 40)
        {
            PvpWeights copy = JsonSerializer.Deserialize<PvpWeights>(JsonSerializer.Serialize(from));
            var drawn = new Dictionary<string, int>();
            Walk(copy, "", random, Math.Clamp(sharePercent, 0, 100), Math.Clamp(spreadPercent, 1, 100) / 100.0, drawn);
            copy.Drawn = drawn;
            return copy;
        }

        private static int Level(Random random, int sharePercent) =>
            random.Next(100) >= sharePercent ? 0 : random.Next(2) == 0 ? -1 : 1;

        private static void Walk(object weights, string prefix, Random random, int share, double spread, Dictionary<string, int> drawn)
        {
            foreach (PropertyInfo property in weights.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite && property.Name != nameof(PvpWeights.Drawn))
                .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                string path = prefix + property.Name;
                if (Fixed.Contains(path))
                    continue;
                object value = property.GetValue(weights);
                switch (value)
                {
                    case int number:
                        if (Level(random, share) is not 0 and int level && MovedInt(property.Name, number, level, spread) is int moved && moved != number)
                        {
                            property.SetValue(weights, moved);
                            drawn[path] = level;
                        }
                        break;
                    case double number:
                        if (Level(random, share) is not 0 and int step && number != 0)
                        {
                            property.SetValue(weights, Math.Round(number * (1 + step * spread), 3));
                            drawn[path] = step;
                        }
                        break;
                    case bool flag:
                        if (Level(random, share) == 1)
                        {
                            property.SetValue(weights, !flag);
                            drawn[path] = 1;
                        }
                        break;
                    case Dictionary<string, int> entries:
                        Move(entries, property.Name, path, random, share, spread, drawn);
                        break;
                    case Dictionary<string, Dictionary<string, int>> groups:
                        foreach (string key in groups.Keys.OrderBy(key => key, StringComparer.Ordinal))
                            Move(groups[key], property.Name, $"{path}.{key}", random, share, spread, drawn);
                        break;
                    default:
                        if (value != null && property.PropertyType.Name.EndsWith("Weights", StringComparison.Ordinal))
                            Walk(value, path + ".", random, share, spread, drawn);
                        break;
                }
            }
        }

        private static void Move(Dictionary<string, int> entries, string name, string path, Random random, int share, double spread, Dictionary<string, int> drawn)
        {
            foreach (string key in entries.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList())
            {
                int level = Level(random, share);
                int moved = level == 0 ? entries[key] : MovedInt(name, entries[key], level, spread);
                if (moved == entries[key])
                    continue;
                entries[key] = moved;
                drawn[$"{path}.{key}"] = level;
            }
        }

        /// <summary>One whole number moved one step down or up, by what kind of number its name says it is.</summary>
        public static int MovedInt(string name, int value, int level, double spread)
        {
            double scale = spread / 0.4;
            if (Threshold.IsMatch(name))
                return Math.Clamp(value + level * Math.Max(1, (int)Math.Round(15 * scale)), 0, 100);
            if (Physical.IsMatch(name))
                return Math.Max(0, value + level * Math.Max(1, (int)Math.Round(Math.Abs(value) * spread)));
            int floor = Math.Max(1, (int)Math.Round((Math.Abs(value) < 10 ? 4 : 10) * scale));
            return value + level * Math.Max(floor, (int)Math.Round(Math.Abs(value) * spread));
        }
    }
}
