using System.Collections.Generic;
using System.IO;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>A player's lasting companions (bots/pvp/PvpRoster.cs).</summary>
    [TestFixture]
    public class UT_PvpRoster
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "pvp-roster-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [Test]
        public void TheSecondOfABuildIsToldFromTheFirst()
        {
            string[] builds = ["druid-nurture", "bard", "hero", "blademaster", "blademaster", "eldritch-light"];
            Assert.That(PvpRoster.PlaceOf(builds, 3), Is.EqualTo(0));
            Assert.That(PvpRoster.PlaceOf(builds, 4), Is.EqualTo(1));
            Assert.That(PvpRoster.PlaceOf(builds, 0), Is.EqualTo(0));
        }

        [Test]
        public void ACompanionIsFoundByItsBuildAndPlace()
        {
            List<PvpRosterEntry> roster = [new("blademaster", 0, "Aedan", 1), new("blademaster", 1, "Brigh", 2), new("bard", 0, "Cian", 1)];
            Assert.That(PvpRoster.Find(roster, "Blademaster", 1)?.Name, Is.EqualTo("Brigh"));
            Assert.That(PvpRoster.Find(roster, "bard", 0)?.Name, Is.EqualTo("Cian"));
            Assert.That(PvpRoster.Find(roster, "bard", 1), Is.Null);
            Assert.That(PvpRoster.Find(roster, "hero", 0), Is.Null);
        }

        [Test]
        public void ARosterIsKeptByCharacterAndReadBack()
        {
            Assert.That(PvpRoster.Load(_folder, "character-one"), Is.Empty, "nothing kept yet");
            PvpRoster.Save(_folder, "character-one", [new("bard", 0, "Cian", 1)]);
            PvpRoster.Save(_folder, "character-two", [new("skald", 0, "Ulf", 1), new("warrior", 0, "Sigrun", 2)]);
            Assert.That(PvpRoster.Load(_folder, "character-one"), Is.EqualTo(new[] { new PvpRosterEntry("bard", 0, "Cian", 1) }));
            Assert.That(PvpRoster.Load(_folder, "character-two"), Has.Count.EqualTo(2));
            Assert.That(PvpRoster.Load(_folder, "somebody-else"), Is.Empty);
        }

        [Test]
        public void AFileThatCannotBeReadIsAnEmptyRoster()
        {
            File.WriteAllText(Path.Combine(_folder, PvpRoster.FileName), "not json");
            Assert.That(PvpRoster.Load(_folder, "character-one"), Is.Empty);
            // And saving over it works.
            PvpRoster.Save(_folder, "character-one", [new("bard", 0, "Cian", 1)]);
            Assert.That(PvpRoster.Load(_folder, "character-one"), Has.Count.EqualTo(1));
        }
    }
}
