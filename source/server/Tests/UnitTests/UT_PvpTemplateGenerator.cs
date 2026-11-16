using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpTemplateGenerator
    {
        private static IEnumerable<PvpBuild> AllBuilds => PvpBuildCatalog.Builds;

        private static eStat ManaStat(eCharacterClass characterClass)
        {
            Type type = typeof(GameServer).Assembly.GetTypes().Single(candidate =>
                !candidate.IsAbstract &&
                candidate.GetCustomAttribute<CharacterClassAttribute>(false)?.ID == (int)characterClass);
            return ((ICharacterClass)Activator.CreateInstance(type)).ManaStat;
        }

        // SkillBase needs a running server, so the tests map lines to +skill properties themselves:
        // the three the assertions name, and one distinct skill property for every other line.
        private static readonly Dictionary<string, eProperty> SkillOf = new()
        {
            [Specs.Blades] = eProperty.Skill_Blades, [Specs.Shields] = eProperty.Skill_Shields, [Specs.Parry] = eProperty.Skill_Parry,
        };

        private static eProperty SpecToSkill(string spec)
        {
            lock (SkillOf)
            {
                if (!SkillOf.TryGetValue(spec, out eProperty skill))
                {
                    skill = Enumerable.Range((int)eProperty.Skill_First, eProperty.Skill_Last - eProperty.Skill_First + 1)
                        .Select(value => (eProperty)value).First(candidate => !SkillOf.ContainsValue(candidate));
                    SkillOf[spec] = skill;
                }
                return skill;
            }
        }

        // What a character of this build wears: six armour pieces, its weapons, eight jewelry slots.
        private static IReadOnlyList<PvpTemplateSlot> Slots(PvpBuild build)
        {
            var worn = new List<eInventorySlot>
            {
                eInventorySlot.HeadArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor,
                eInventorySlot.TorsoArmor, eInventorySlot.LegsArmor, eInventorySlot.ArmsArmor,
                eInventorySlot.Jewelry, eInventorySlot.Neck, eInventorySlot.Cloak, eInventorySlot.Waist,
                eInventorySlot.LeftRing, eInventorySlot.RightRing, eInventorySlot.LeftBracer, eInventorySlot.RightBracer,
            };
            // Everyone also carries a spare the template must leave plain.
            worn.Add(eInventorySlot.TwoHandWeapon);
            worn.Add(eInventorySlot.RightHandWeapon);
            worn.Add(eInventorySlot.LeftHandWeapon);
            worn.Add(eInventorySlot.DistanceWeapon);
            return PvpTemplateGenerator.SlotsFor(worn, build.TwoHanded || build.WeaponOne == eObjectType.Staff);
        }

        private static PvpTemplate Make(PvpBuild build) => PvpTemplateGenerator.Generate(
            PvpTemplateNeeds.For(build, ManaStat(build.Class), SpecToSkill), Slots(build));

        [TestCase(PvpBonusKind.Stat, 28, 19)]
        [TestCase(PvpBonusKind.Stat, 1, 1)]
        [TestCase(PvpBonusKind.Hits, 76, 19)]
        [TestCase(PvpBonusKind.Resist, 11, 20)]
        [TestCase(PvpBonusKind.Resist, 1, 1)]
        [TestCase(PvpBonusKind.Skill, 4, 15)]
        [TestCase(PvpBonusKind.Skill, 1, 1)]
        public void GemImbuePointsFollowTheSpellcraftingFormulas(PvpBonusKind kind, int value, int expected)
        {
            Assert.That(PvpTemplateGenerator.GemPoints(kind, value), Is.EqualTo(expected));
        }

        [Test]
        public void AnItemCostsHalfTheSumOfItsGemsPlusHalfTheLargest()
        {
            PvpTemplateBonus[] gems =
            [
                new(eProperty.Strength, PvpBonusKind.Stat, 28),        // 19
                new(eProperty.Constitution, PvpBonusKind.Stat, 28),    // 19
                new(eProperty.Resist_Body, PvpBonusKind.Resist, 9),    // 16
                new(eProperty.MaxHealth, PvpBonusKind.Hits, 40),       // 10
            ];
            Assert.That(PvpTemplateGenerator.ItemPoints(gems), Is.EqualTo((19 + 19 + 16 + 10 + 19) / 2));
            Assert.That(PvpTemplateGenerator.CraftedCapacity, Is.EqualTo(37));
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void EveryItemObeysTheCraftingRules(PvpBuild build)
        {
            PvpTemplate template = Make(build);
            foreach (PvpTemplateItem item in template.Items)
            {
                Assert.That(item.Bonuses.Count, Is.LessThanOrEqualTo(PvpTemplateGenerator.MaxBonusesPerItem), $"{build.Id} {item.Slot}");
                Assert.That(item.Bonuses.Select(bonus => bonus.Property).Distinct().Count(), Is.EqualTo(item.Bonuses.Count), $"{build.Id} {item.Slot}");
                Assert.That(PvpTemplateGenerator.ItemPoints(item.Bonuses), Is.EqualTo(item.ImbuePoints));
                Assert.That(item.ImbuePoints, Is.LessThanOrEqualTo(item.Capacity), $"{build.Id} {item.Slot}");
                foreach (PvpTemplateBonus bonus in item.Bonuses)
                    Assert.That(PvpTemplateGenerator.GemSizes(bonus.Kind), Does.Contain(bonus.Value), $"{build.Id} {item.Slot} {bonus.Property}");
            }
            Assert.That(template.Items.Where(item => PvpTemplateGenerator.IsJewelry(item.Slot)).Select(item => item.Capacity).Distinct(),
                Is.EqualTo(new[] { PvpTemplateGenerator.JewelryCapacity }));
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void NothingGoesOverItsCap(PvpBuild build)
        {
            PvpTemplate template = Make(build);
            foreach (PvpTemplateNeed need in PvpTemplateNeeds.For(build, ManaStat(build.Class), SpecToSkill))
            {
                int worn = template.Items.SelectMany(item => item.Bonuses).Where(bonus => bonus.Property == need.Property).Sum(bonus => bonus.Value);
                Assert.That(worn, Is.EqualTo(template.Totals[need.Property]), $"{build.Id} {need.Property}");
                Assert.That(worn, Is.LessThanOrEqualTo(need.Target), $"{build.Id} {need.Property}");
            }
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void FirstPriorityNeedsAreAllCapped(PvpBuild build)
        {
            PvpTemplate template = Make(build);
            foreach (PvpTemplateNeed need in PvpTemplateNeeds.For(build, ManaStat(build.Class), SpecToSkill).Where(need => need.Priority == 1))
                Assert.That(template.Totals[need.Property], Is.EqualTo(need.Target), $"{build.Id} {need.Property}");
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void ResistsAreNearTheirCapAndEvenlySpread(PvpBuild build)
        {
            PvpTemplate template = Make(build);
            int[] resists = template.Totals.Where(pair => pair.Key >= eProperty.Resist_First && pair.Key <= eProperty.Resist_Last)
                .Select(pair => pair.Value).ToArray();
            Assert.That(resists, Has.Length.EqualTo(9));
            Assert.That(resists.Min(), Is.GreaterThanOrEqualTo(15), $"{build.Id}: lowest resist {resists.Min()}");
            Assert.That(resists.Max() - resists.Min(), Is.LessThanOrEqualTo(8), $"{build.Id}: {string.Join(",", resists)}");
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void OnlyTheWeaponsInHandCarryBonuses(PvpBuild build)
        {
            bool twoHanded = build.TwoHanded || build.WeaponOne == eObjectType.Staff;
            PvpTemplate template = Make(build);
            IEnumerable<eInventorySlot> withBonuses = template.Items.Where(item => item.Bonuses.Count > 0).Select(item => item.Slot);
            Assert.That(withBonuses, Has.None.EqualTo(eInventorySlot.DistanceWeapon));
            if (twoHanded)
                Assert.That(withBonuses, Has.None.EqualTo(eInventorySlot.RightHandWeapon).And.None.EqualTo(eInventorySlot.LeftHandWeapon));
            else
                Assert.That(withBonuses, Has.None.EqualTo(eInventorySlot.TwoHandWeapon));
        }

        [Test]
        public void TheSameBuildAlwaysGetsTheSameTemplate()
        {
            PvpBuild hero = PvpBuildCatalog.Find("hero");
            string Text(PvpTemplate t) => string.Join("|", t.Items.Select(item =>
                $"{item.Slot}:{string.Join(",", item.Bonuses.Select(bonus => $"{bonus.Property}{bonus.Value}"))}"));
            Assert.That(Text(Make(hero)), Is.EqualTo(Text(Make(hero))));
        }

        [Test]
        public void FightersAndCastersWantDifferentStats()
        {
            IReadOnlyList<PvpTemplateNeed> hero = PvpTemplateNeeds.For(PvpBuildCatalog.Find("hero"), eStat.UNDEFINED, SpecToSkill);
            IReadOnlyList<PvpTemplateNeed> sorcerer = PvpTemplateNeeds.For(PvpBuildCatalog.Find("sorcerer-mind"), eStat.INT, SpecToSkill);
            Assert.That(hero.Where(need => need.Priority == 1 && need.Kind == PvpBonusKind.Stat).Select(need => need.Property),
                Is.EquivalentTo(new[] { eProperty.Strength, eProperty.Constitution, eProperty.Dexterity }));
            Assert.That(sorcerer.Where(need => need.Priority == 1 && need.Kind == PvpBonusKind.Stat).Select(need => need.Property),
                Is.EquivalentTo(new[] { eProperty.Intelligence, eProperty.Dexterity, eProperty.Constitution }));
            Assert.That(hero.Select(need => need.Property), Does.Contain(eProperty.Skill_Blades).And.Contain(eProperty.Skill_Shields));
            Assert.That(sorcerer.Select(need => need.Property), Has.None.EqualTo(eProperty.Strength));
        }
    }
}
