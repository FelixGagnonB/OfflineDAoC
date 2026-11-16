using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    public enum PvpBonusKind { Stat, Hits, Power, Resist, Skill }

    /// <summary>One thing a template should reach: a property, its cap, and how soon it is needed (1 first).</summary>
    public readonly record struct PvpTemplateNeed(eProperty Property, PvpBonusKind Kind, int Target, int Priority);

    /// <summary>An equipment slot and the imbue points its item can hold.</summary>
    public readonly record struct PvpTemplateSlot(eInventorySlot Slot, int Capacity);

    public readonly record struct PvpTemplateBonus(eProperty Property, PvpBonusKind Kind, int Value);

    public sealed record PvpTemplateItem(eInventorySlot Slot, int Capacity, int ImbuePoints, IReadOnlyList<PvpTemplateBonus> Bonuses);

    public sealed record PvpTemplate(
        IReadOnlyList<PvpTemplateItem> Items,
        IReadOnlyDictionary<eProperty, int> Totals,
        IReadOnlyList<(eProperty Property, int Target, int Reached)> Shortfalls);

    /// <summary>
    /// Builds a level-50 gear template for an 8v8 build (docs/PVP-8V8-ROADMAP.md, M1.3): which
    /// bonuses go on which item so the character reaches its stat, hit-point, resist and skill caps.
    /// Every item obeys the spellcrafting rules of this server (craft/SpellCrafting.cs), so a
    /// template is gear a crafter could have made in 1.65, not a cheat:
    ///   - at most four bonuses per item, each of a real gem size, no property twice on one item;
    ///   - an item's imbue points are (sum of its gems + its largest gem) / 2;
    ///   - a level-51, 100% quality item holds 32, plus 5 of overcharge.
    /// Jewelry was dropped loot in 1.65, not crafted; it gets a smaller allowance.
    /// When the gear cannot hold everything, the needs with the highest priority number go short.
    /// </summary>
    public static class PvpTemplateGenerator
    {
        public const int ItemLevel = 51;
        public const int BaseCapacity = 32;       // round(0.625 * 51), 100% quality
        public const int Overcharge = 5;
        public const int CraftedCapacity = BaseCapacity + Overcharge;
        /// <summary>Allowance for a dropped jewelry item, about two thirds of a crafted one.</summary>
        public const int JewelryCapacity = 24;
        public const int MaxBonusesPerItem = 4;

        public const int StatCap = 75;            // level * 1.5
        public const int HitsCap = 200;           // level * 4
        public const int ResistCap = 26;          // level / 2 + 1
        public const int SkillCap = 11;           // level / 5 + 1

        private static readonly int[] StatGems = [1, 4, 7, 10, 13, 16, 19, 22, 25, 28];
        private static readonly int[] HitsGems = [4, 12, 20, 28, 36, 44, 52, 60, 68, 76];
        private static readonly int[] PowerGems = [1, 2, 3, 5, 7, 9, 11, 13, 15, 17];
        private static readonly int[] ResistGems = [1, 2, 3, 5, 7, 9, 11, 13, 15, 17];
        private static readonly int[] SkillGems = [1, 2, 3, 4, 5, 6, 7, 8];

        public static IReadOnlyList<int> GemSizes(PvpBonusKind kind) => kind switch
        {
            PvpBonusKind.Stat => StatGems,
            PvpBonusKind.Hits => HitsGems,
            PvpBonusKind.Power => PowerGems,
            PvpBonusKind.Resist => ResistGems,
            _ => SkillGems,
        };

        /// <summary>Imbue points of one gem: the formulas of SpellCrafting.GetGemImbuePoints.</summary>
        public static int GemPoints(PvpBonusKind kind, int value) => Math.Max(1, kind switch
        {
            PvpBonusKind.Stat => (value - 1) * 2 / 3 + 1,
            PvpBonusKind.Hits => value / 4,
            PvpBonusKind.Power => value * 2 - 2,
            PvpBonusKind.Resist => value * 2 - 2,
            _ => (value - 1) * 5,
        });

        /// <summary>Imbue points of an item: the rule of SpellCrafting.GetTotalImbuePoints.</summary>
        public static int ItemPoints(IEnumerable<PvpTemplateBonus> bonuses)
        {
            int sum = 0, largest = 0;
            foreach (PvpTemplateBonus bonus in bonuses)
            {
                int points = GemPoints(bonus.Kind, bonus.Value);
                sum += points;
                largest = Math.Max(largest, points);
            }
            return (sum + largest) / 2;
        }

        private sealed class WorkItem(PvpTemplateSlot slot)
        {
            public readonly PvpTemplateSlot Slot = slot;
            public readonly List<PvpTemplateBonus> Bonuses = [];
        }

        /// <summary>Largest gem, in imbue points, a need is first split into. Bigger gems pack badly.</summary>
        public const int PreferredGemPoints = 17;

        public static PvpTemplate Generate(IReadOnlyList<PvpTemplateNeed> needs, IReadOnlyList<PvpTemplateSlot> slots)
        {
            List<WorkItem> items = slots.Select(slot => new WorkItem(slot)).ToList();
            Dictionary<eProperty, int> totals = needs.ToDictionary(need => need.Property, _ => 0);

            // First decide the gems: each need becomes a few evenly sized ones that add up to its
            // cap. Then seat them, most needed first and largest first within a priority.
            var gems = needs
                .SelectMany((need, order) => Split(need.Kind, need.Target).Select(value => (Need: need, Order: order, Value: value)))
                .OrderBy(gem => gem.Need.Priority)
                .ThenByDescending(gem => GemPoints(gem.Need.Kind, gem.Value))
                .ThenBy(gem => gem.Order)
                .ToList();

            foreach (var gem in gems)
            {
                // A gem that fits nowhere is tried one size smaller, down to nothing.
                IReadOnlyList<int> sizes = GemSizes(gem.Need.Kind);
                for (int index = sizes.ToList().IndexOf(gem.Value); index >= 0; index--)
                {
                    if (!TrySeat(items, new PvpTemplateBonus(gem.Need.Property, gem.Need.Kind, sizes[index])))
                        continue;
                    totals[gem.Need.Property] += sizes[index];
                    break;
                }
            }

            return new PvpTemplate(
                items.Select(item => new PvpTemplateItem(item.Slot.Slot, item.Slot.Capacity, ItemPoints(item.Bonuses),
                    item.Bonuses.OrderByDescending(bonus => GemPoints(bonus.Kind, bonus.Value)).ToList())).ToList(),
                totals,
                needs.Where(need => totals[need.Property] < need.Target)
                    .Select(need => (need.Property, need.Target, totals[need.Property])).ToList());
        }

        /// <summary>
        /// The gems that add up to a cap: as few as possible, none above
        /// <see cref="PreferredGemPoints"/>, and as even as possible. 75 in a stat is 25 + 25 + 25;
        /// 200 hit points is 52 + 52 + 52 + 44. When no combination is exact the closest one below.
        /// </summary>
        public static IReadOnlyList<int> Split(PvpBonusKind kind, int target)
        {
            int[] sizes = GemSizes(kind).Where(size => GemPoints(kind, size) <= PreferredGemPoints).ToArray();
            // best[sum] = (gem count, sum of squared points, last gem) for the best way to reach sum.
            var best = new (int Count, int Squares, int Last)[target + 1];
            for (int sum = 1; sum <= target; sum++)
            {
                best[sum] = (int.MaxValue, int.MaxValue, 0);
                foreach (int size in sizes)
                {
                    if (size > sum || best[sum - size].Count == int.MaxValue)
                        continue;
                    int points = GemPoints(kind, size);
                    (int Count, int Squares, int Last) candidate = (best[sum - size].Count + 1, best[sum - size].Squares + points * points, size);
                    if (candidate.Count < best[sum].Count ||
                        candidate.Count == best[sum].Count && candidate.Squares < best[sum].Squares)
                        best[sum] = candidate;
                }
            }
            int reached = target;
            while (reached > 0 && best[reached].Count == int.MaxValue)
                reached--;
            var gems = new List<int>();
            for (int sum = reached; sum > 0; sum -= best[sum].Last)
                gems.Add(best[sum].Last);
            return gems.OrderByDescending(size => size).ToList();
        }

        /// <summary>
        /// Puts one gem on the item where it adds the fewest imbue points: next to an equal or
        /// larger gem it counts half. Ties go to the item with the most room left.
        /// </summary>
        private static bool TrySeat(List<WorkItem> items, PvpTemplateBonus gem)
        {
            WorkItem bestItem = null;
            int bestAdded = int.MaxValue, bestRoom = -1;
            foreach (WorkItem item in items)
            {
                if (item.Bonuses.Count >= MaxBonusesPerItem || item.Bonuses.Any(bonus => bonus.Property == gem.Property))
                    continue;
                int before = ItemPoints(item.Bonuses);
                int after = ItemPoints(item.Bonuses.Append(gem));
                if (after > item.Slot.Capacity)
                    continue;
                int added = after - before, room = item.Slot.Capacity - after;
                if (added < bestAdded || added == bestAdded && room > bestRoom)
                    (bestItem, bestAdded, bestRoom) = (item, added, room);
            }
            bestItem?.Bonuses.Add(gem);
            return bestItem != null;
        }

        /// <summary>
        /// The usual gear of a level-50 character: crafted armour and weapons, dropped jewelry.
        /// Only the weapons actually held count toward bonuses (as in the game), so the template
        /// puts nothing on the other weapon slots: a bow or a spare two-hander stays plain.
        /// </summary>
        public static IReadOnlyList<PvpTemplateSlot> SlotsFor(IEnumerable<eInventorySlot> equipped, bool twoHanded) =>
            equipped.Distinct().OrderBy(slot => (int)slot)
                .Select(slot => new PvpTemplateSlot(slot,
                    IsJewelry(slot) ? JewelryCapacity : IsSpareWeapon(slot, twoHanded) ? 0 : CraftedCapacity)).ToList();

        public static bool IsSpareWeapon(eInventorySlot slot, bool twoHanded) => slot switch
        {
            eInventorySlot.DistanceWeapon => true,
            eInventorySlot.TwoHandWeapon => !twoHanded,
            eInventorySlot.RightHandWeapon or eInventorySlot.LeftHandWeapon => twoHanded,
            _ => false,
        };

        public static bool IsJewelry(eInventorySlot slot) => slot is
            eInventorySlot.Jewelry or eInventorySlot.Neck or eInventorySlot.Cloak or eInventorySlot.Waist or
            eInventorySlot.LeftRing or eInventorySlot.RightRing or eInventorySlot.LeftBracer or eInventorySlot.RightBracer;
    }
}
