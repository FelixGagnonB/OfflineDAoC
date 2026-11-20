using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpBuildCatalog
    {
        private static IEnumerable<PvpBuild> AllBuilds => PvpBuildCatalog.Builds;

        // The multiplier each class really has, read from its class definition.
        private static int Multiplier(eCharacterClass characterClass)
        {
            Type type = typeof(GameServer).Assembly.GetTypes().Single(candidate =>
                !candidate.IsAbstract &&
                candidate.GetCustomAttribute<CharacterClassAttribute>(false)?.ID == (int)characterClass);
            return ((ICharacterClass)Activator.CreateInstance(type)).SpecPointsMultiplier;
        }

        [TestCase(10, 1494)]
        [TestCase(15, 2223)]
        [TestCase(20, 2979)]
        public void PointsEarnedByLevelFifty(int multiplier, int expected)
        {
            Assert.That(PvpBuildBudget.TotalPoints(multiplier), Is.EqualTo(expected));
        }

        [TestCase(1, 0)]
        [TestCase(2, 2)]
        [TestCase(42, 902)]
        [TestCase(50, 1274)]
        public void CostOfTrainingALine(int level, int expected)
        {
            Assert.That(PvpBuildBudget.CostToTrain(level), Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void BuildFitsItsClassPoints(PvpBuild build)
        {
            int available = PvpBuildBudget.TotalPoints(Multiplier(build.Class));
            Assert.That(build.Cost, Is.LessThanOrEqualTo(available),
                $"{build.Id} costs {build.Cost} of {available} points");
            // More than one unspent low line means the build wastes points.
            Assert.That(available - build.Cost, Is.LessThan(120), $"{build.Id} leaves {available - build.Cost} points unspent");
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void BuildLinesAreWellFormed(PvpBuild build)
        {
            var known = typeof(Specs).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()).ToHashSet();
            Assert.That(build.Lines, Is.Not.Empty);
            Assert.That(build.Lines.Select(line => line.Spec).Distinct().Count(), Is.EqualTo(build.Lines.Count));
            foreach (PvpSpecLine line in build.Lines)
            {
                Assert.That(known, Does.Contain(line.Spec), $"{build.Id}: unknown line {line.Spec}");
                Assert.That(line.Level, Is.InRange(2, 50), $"{build.Id}: {line.Spec}");
            }
            Assert.That(PvpBuildCatalog.RealmOf(build.Class), Is.Not.EqualTo(eRealm.None), build.Id);
        }

        [Test]
        public void BuildIdsAreUnique()
        {
            Assert.That(AllBuilds.Select(build => build.Id.ToLowerInvariant()).Distinct().Count(),
                Is.EqualTo(PvpBuildCatalog.Builds.Count));
            Assert.That(PvpBuildCatalog.Find("HERO")?.Class, Is.EqualTo(eCharacterClass.Hero));
            Assert.That(PvpBuildCatalog.Find("no-such-build"), Is.Null);
        }

        [Test]
        public void TheGroupsShamanHasItsInstantDisease()
        {
            // The owner, 2026-11-20: "I'd change the shaman spec to have that instant pbaoe disease."
            // Mold Cloud is the Cave line's at 27; the yellow Str/Con and Dex/Qui need Augmentation 37.
            PvpBuild shaman = PvpBuildCatalog.Find("shaman-aug");
            Assert.That(shaman.Lines.Single(line => line.Spec == Specs.Subterranean).Level, Is.GreaterThanOrEqualTo(27));
            Assert.That(shaman.Lines.Single(line => line.Spec == Specs.Augmentation).Level, Is.GreaterThanOrEqualTo(42));
        }

        [Test]
        public void MainGroupsAreEightOfOneRealm()
        {
            Assert.That(PvpBuildCatalog.Groups.Select(group => group.Realm),
                Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
            foreach (PvpGroupTemplate group in PvpBuildCatalog.Groups)
            {
                Assert.That(group.BuildIds, Has.Count.EqualTo(8), group.Id);
                foreach (string id in group.BuildIds)
                {
                    PvpBuild build = PvpBuildCatalog.Find(id);
                    Assert.That(build, Is.Not.Null, $"{group.Id}: {id}");
                    Assert.That(PvpBuildCatalog.RealmOf(build.Class), Is.EqualTo(group.Realm), $"{group.Id}: {id}");
                }
            }
            Assert.That(PvpBuildCatalog.FindGroup("HIB-MAIN")?.Realm, Is.EqualTo(eRealm.Hibernia));
        }

        [Test]
        public void AGroupToTryIsEightOfOneRealmAndFoundByItsName()
        {
            // The owner, 2026-11-20: "We use to run: bard, bard, druid, druid, hero, bm, bm, eld".
            PvpGroupTemplate twoBards = PvpBuildCatalog.FindGroup("hib-2bard");
            Assert.That(twoBards, Is.Not.Null);
            Assert.That(twoBards.BuildIds.Select(id => PvpBuildCatalog.Find(id).Class).OrderBy(c => c.ToString()), Is.EqualTo(new[]
            {
                eCharacterClass.Bard, eCharacterClass.Bard, eCharacterClass.Blademaster, eCharacterClass.Blademaster,
                eCharacterClass.Druid, eCharacterClass.Druid, eCharacterClass.Eldritch, eCharacterClass.Hero
            }));
            foreach (PvpGroupTemplate group in PvpBuildCatalog.Variants)
            {
                Assert.That(group.BuildIds, Has.Count.EqualTo(8), group.Id);
                Assert.That(group.BuildIds.Select(id => PvpBuildCatalog.RealmOf(PvpBuildCatalog.Find(id).Class)), Is.All.EqualTo(group.Realm), group.Id);
                Assert.That(PvpBuildCatalog.Groups.Select(main => main.Id), Has.None.EqualTo(group.Id), "the main groups stay the ones picked by a realm");
            }
        }

        [Test]
        public void MainGroupsCoverTwentyClasses()
        {
            int classes = PvpBuildCatalog.Groups.SelectMany(group => group.BuildIds)
                .Select(id => PvpBuildCatalog.Find(id).Class).Distinct().Count();
            Assert.That(classes, Is.EqualTo(20));
        }

        [TestCaseSource(nameof(AllBuilds))]
        public void PresetSpecTrainsExactlyTheBuild(PvpBuild build)
        {
            BotSpec spec = build.ToBotSpec();
            Assert.That(spec.SpecType, Is.EqualTo(build.SpecType));
            Assert.That(spec.WeaponOneType, Is.EqualTo(build.WeaponOne));
            Assert.That(spec.Is2H, Is.EqualTo(build.TwoHanded));
            Assert.That(spec.SpecLines.Select(line => (line.Spec, (int)line.SpecCap)),
                Is.EquivalentTo(build.Lines.Select(line => (line.Spec, line.Level))));
            // At level 50 the pacing ratio must reach the cap, or the line would stop short.
            foreach (BotSpecLine line in spec.SpecLines)
                Assert.That((int)Math.Round(50 * line.levelRatio), Is.EqualTo((int)line.SpecCap));
        }

        [TestCase(eCharacterClass.Eldritch, eRace.Lurikeen)]
        [TestCase(eCharacterClass.Enchanter, eRace.Lurikeen)]
        [TestCase(eCharacterClass.Mentalist, eRace.Lurikeen)]
        [TestCase(eCharacterClass.Hero, eRace.Firbolg)]
        [TestCase(eCharacterClass.Druid, eRace.Celt)]
        [TestCase(eCharacterClass.Bard, eRace.Celt)]
        [TestCase(eCharacterClass.Blademaster, eRace.Celt)]
        [TestCase(eCharacterClass.Warden, eRace.Celt)]
        [TestCase(eCharacterClass.Champion, eRace.Celt)]
        public void HiberniasRacesAreTheOwners(eCharacterClass characterClass, eRace race)
        {
            // Owner, 2026-11-20: casters Lurikeen, the Hero Firbolg, the rest Celt.
            Assert.That(PvpBuildCatalog.RaceOf(characterClass), Is.EqualTo(race));
        }

        [TestCase(eCharacterClass.Sorcerer, eRace.Saracen)]
        [TestCase(eCharacterClass.Cabalist, eRace.Saracen)]
        [TestCase(eCharacterClass.Theurgist, eRace.Avalonian, Description = "asked for as a Saracen, which a Theurgist cannot be here")]
        [TestCase(eCharacterClass.Healer, eRace.Dwarf)]
        [TestCase(eCharacterClass.Runemaster, eRace.Kobold)]
        public void TheCastersAndHealersTheOwnerMoved(eCharacterClass characterClass, eRace race)
        {
            // Owner, 2026-11-20: casters Lurikeen, the Hero Firbolg, the rest Celt.
            Assert.That(PvpBuildCatalog.RaceOf(characterClass), Is.EqualTo(race));
        }
}
}
