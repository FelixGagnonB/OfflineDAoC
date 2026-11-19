using System.Linq;
using DOL.GS;
using DOL.GS.Commands;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The choices before a play session: the setup window and the build a player takes.</summary>
    [TestFixture]
    public class UT_PvpPlaySetup
    {
        private static readonly string[] AlbMain =
            ["cleric-rejuv", "cleric-enhance", "sorcerer-mind", "minstrel", "theurgist-air", "paladin", "armsman-polearm", "cabalist-tri"];

        [Test]
        public void TheCompanionsLeaveOutTheBuildThePlayerTook()
        {
            // Albion has two Clerics: a player made ready as the second keeps the first among the companions.
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Cleric, "cleric-enhance"), Is.EqualTo(1));
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Cleric, "cleric-rejuv"), Is.EqualTo(0));
        }

        [Test]
        public void WithoutABuildTheFirstOfThePlayersClassIsLeftOut()
        {
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Cleric, null), Is.EqualTo(0));
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Paladin, null), Is.EqualTo(5));
            // A build that is not in the group (another realm's, or an old one) falls back to the class.
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Minstrel, "druid-nurture"), Is.EqualTo(3));
        }

        [Test]
        public void AClassTheGroupDoesNotHaveTakesTheLastPlace()
        {
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Wizard, null), Is.EqualTo(7));
        }

        [TestCase(eRealm.Albion, "alb-main")]
        [TestCase(eRealm.Midgard, "mid-main")]
        [TestCase(eRealm.Hibernia, "hib-main")]
        public void EveryBuildOfTheMainGroupCanBeTakenAndNoBuffbot(eRealm realm, string group)
        {
            string[] offered = PvpPlayerReady.BuildsFor(realm).Select(build => build.Id).ToArray();
            Assert.That(offered, Is.SupersetOf(PvpBuildCatalog.FindGroup(group).BuildIds.Distinct()));
            Assert.That(offered.Any(id => id.StartsWith("buffbot")), Is.False);
            Assert.That(PvpPlayerReady.BuildsFor(realm).All(build => PvpBuildCatalog.RealmOf(build.Class) == realm), Is.True);
        }

        [Test]
        public void ABuildIsFoundByItsNameOrItsClass()
        {
            Assert.That(PvpPlayerReady.Find(eRealm.Albion, "Cleric-Enhance")?.Id, Is.EqualTo("cleric-enhance"));
            Assert.That(PvpPlayerReady.Find(eRealm.Albion, "cleric")?.Id, Is.EqualTo("cleric-rejuv"));
            Assert.That(PvpPlayerReady.Find(eRealm.Albion, "druid"), Is.Null);
            Assert.That(PvpPlayerReady.Find(eRealm.Albion, "buffbot-cleric"), Is.Null);
        }

        [Test]
        public void ABuildOfAnotherRealmOrInAFightIsRefused()
        {
            PvpBuild cleric = PvpBuildCatalog.Find("cleric-rejuv");
            Assert.That(PvpPlayerReady.Refusal(eRealm.Albion, false, true, cleric), Is.Null);
            Assert.That(PvpPlayerReady.Refusal(eRealm.Hibernia, false, true, cleric), Is.Not.Null);
            Assert.That(PvpPlayerReady.Refusal(eRealm.Albion, true, true, cleric), Is.Not.Null);
            Assert.That(PvpPlayerReady.Refusal(eRealm.Albion, false, false, cleric), Is.Not.Null);
            Assert.That(PvpPlayerReady.Refusal(eRealm.Albion, false, true, null), Is.Not.Null);
        }

        [Test]
        public void TheWindowStartsOnAnotherRealmFaceToFaceAtRankOne()
        {
            foreach (eRealm own in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(own);
                Assert.That(setup.Enemy, Is.Not.EqualTo(own));
                Assert.That(setup.Start, Is.EqualTo("lane"));
                Assert.That(setup.Rank, Is.EqualTo(1));
            }
        }

        [Test]
        public void AClickChangesOneChoice()
        {
            Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(eRealm.Albion);
            Assert.That(setup.After("Hibernia", eRealm.Albion).Enemy, Is.EqualTo(eRealm.Hibernia));
            Assert.That(setup.After("Basin", eRealm.Albion).Start, Is.EqualTo("basin"));
            Assert.That(setup.After("Basin", eRealm.Albion).After("Lane", eRealm.Albion).Start, Is.EqualTo("lane"));
            Assert.That(setup.After("Rank5", eRealm.Albion).Rank, Is.EqualTo(5));
            Assert.That(setup.After(" rank10 ", eRealm.Albion), Is.EqualTo(setup with { Rank = 10 }));
        }

        [Test]
        public void AClickThatIsNoChoiceChangesNothing()
        {
            Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(eRealm.Albion);
            // Nobody fights their own realm, and only the ranks shown can be picked.
            Assert.That(setup.After("Albion", eRealm.Albion), Is.EqualTo(setup));
            Assert.That(setup.After("Rank4", eRealm.Albion), Is.EqualTo(setup));
            Assert.That(setup.After("Rank", eRealm.Albion), Is.EqualTo(setup));
            Assert.That(setup.After("anything", eRealm.Albion), Is.EqualTo(setup));
            Assert.That(setup.After(null, eRealm.Albion), Is.EqualTo(setup));
        }

        [Test]
        public void TheWindowOffersTheOtherRealmsAndMarksWhatIsChosen()
        {
            string text = new Pvp8v8Setup(eRealm.Midgard, "basin", 5).Text(eRealm.Albion);
            Assert.That(text, Does.Contain("<Midgard>").And.Contain("[Hibernia]").And.Not.Contain("Albion"));
            Assert.That(text, Does.Contain("<Basin>").And.Contain("[Lane]"));
            Assert.That(text, Does.Contain("<Rank5>").And.Contain("[Rank1]").And.Contain("[Start]"));
        }

        [Test]
        public void TheLaneIsFaceToFaceAndTheBasinIsFar()
        {
            PvpArenaBox lane = PvpScrim.PlayBox("lane"), basin = PvpScrim.PlayBox("Basin");
            Assert.That(System.Math.Abs(lane.SpawnA.Y - lane.SpawnB.Y) + System.Math.Abs(lane.SpawnA.X - lane.SpawnB.X), Is.EqualTo(6_000));
            Assert.That(basin, Is.SameAs(PvpArenaBox.EmainSouthEast));
            Assert.That(basin.SpawnB.X - basin.SpawnA.X, Is.EqualTo(21_500));
            Assert.That(PvpScrim.PlayBox(null), Is.SameAs(lane));
        }
    }
}
