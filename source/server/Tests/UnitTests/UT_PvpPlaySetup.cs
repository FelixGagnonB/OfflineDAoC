using System.Collections.Generic;
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
        public void APlayerThatHasLeftTheGameSinceIsKnownByItsTraining()
        {
            // The owner's Cleric, 2026-11-25: Enhancement 42, Rejuvenation 33, Smite 7. Back in the game, its companion was a
            // second Enhancement Cleric, and the group had no Rejuvenation Cleric.
            List<PvpBuild> clerics = AlbMain.Select(PvpBuildCatalog.Find).Where(build => build.Class == eCharacterClass.Cleric).ToList();
            Assert.That(clerics.Select(build => build.Id), Is.EqualTo(new[] { "cleric-rejuv", "cleric-enhance" }));
            var owner = new Dictionary<string, int> { [Specs.Enhancement] = 42, [Specs.Rejuvenation] = 33, [Specs.Smite] = 7 };
            string build = PvpPlayerReady.BuildOf(clerics, spec => owner.GetValueOrDefault(spec));
            Assert.That(build, Is.EqualTo("cleric-enhance"));
            Assert.That(PvpPlayerReady.SlotOf(AlbMain, eCharacterClass.Cleric, build), Is.EqualTo(1), "the Rejuvenation Cleric stays among the companions");
            // Each build is nearest to itself; one that has trained nothing is the first of its class, as before.
            foreach (PvpBuild cleric in clerics)
                Assert.That(PvpPlayerReady.BuildOf(clerics, spec => cleric.Lines.FirstOrDefault(line => line.Spec == spec).Level), Is.EqualTo(cleric.Id));
            Assert.That(PvpPlayerReady.BuildOf([], _ => 0), Is.Null);
            Assert.That(PvpPlayerReady.BuildOf(null, _ => 0), Is.Null);
        }

        [Test]
        public void An8v8BotDoesNotMakeItselfAMonsterToCharm()
        {
            // The owner, 2026-11-25: "sorc seems to have issue with it's controlled pet and losing speed".
            Assert.That(AutonomousPetSupport.MakesItsOwnCharmPet(isABot: true, isAn8v8Bot: true), Is.False);
            Assert.That(AutonomousPetSupport.MakesItsOwnCharmPet(true, false), Is.True, "a gamebot, and a companion of the game as it was, as before");
            Assert.That(AutonomousPetSupport.MakesItsOwnCharmPet(false, false), Is.False, "never a player");
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

        [Test]
        public void ARealmWithASecondGroupOffersItInTheWindow()
        {
            // The owner, 2026-11-20: asked for two Bards, started from the window, and got the main group.
            Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(eRealm.Hibernia);
            Assert.That(setup.Own, Is.Null, "the main group unless another is picked");
            Assert.That(setup.Text(eRealm.Hibernia), Does.Contain("<hib-main>").And.Contain("[hib-2bard]"));
            setup = setup.After("hib-2bard", eRealm.Hibernia);
            Assert.That(setup.Own, Is.EqualTo("hib-2bard"));
            Assert.That(setup.Text(eRealm.Hibernia), Does.Contain("[hib-main]").And.Contain("<hib-2bard>"));
            Assert.That(setup.After("hib-main", eRealm.Hibernia).Own, Is.Null);
            // Another realm's player cannot pick it, and a realm with one group has no such line.
            Assert.That(Pvp8v8Setup.DefaultFor(eRealm.Midgard).After("hib-2bard", eRealm.Midgard).Own, Is.Null);
            Assert.That(Pvp8v8Setup.DefaultFor(eRealm.Midgard).Text(eRealm.Midgard), Does.Not.Contain("Your group"));
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
        public void ARankStandsForItsRealmPoints()
        {
            // Rank 5 is realm level 40; rank 1 level 0 is nothing.
            Assert.That(PvpPlayerReady.RealmPointsFor(40), Is.EqualTo(GamePlayer.REALMPOINTS_FOR_LEVEL[40]));
            Assert.That(PvpPlayerReady.RealmPointsFor(40), Is.GreaterThan(PvpPlayerReady.RealmPointsFor(20)));
            Assert.That(PvpPlayerReady.RealmPointsFor(0), Is.EqualTo(0));
            Assert.That(PvpPlayerReady.RaiseRealmRank(null, 40), Is.Null);
        }

        [Test]
        public void TheWindowStartsOnAnotherRealmFaceToFaceAtRankFiveWithThePlayerLeading()
        {
            foreach (eRealm own in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(own);
                Assert.That(setup.Enemy, Is.Not.EqualTo(own));
                Assert.That(setup.Start, Is.EqualTo("lane"));
                // Rank 5: at rank 1 nobody has Purge and the first mez decides the fight.
                Assert.That(setup.Rank, Is.EqualTo(5));
                Assert.That(setup.BotDrives, Is.False);
            }
        }

        [Test]
        public void AClickChangesOneChoice()
        {
            Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(eRealm.Albion);
            Assert.That(setup.After("Hibernia", eRealm.Albion).Enemy, Is.EqualTo(eRealm.Hibernia));
            Assert.That(setup.After("Basin", eRealm.Albion).Start, Is.EqualTo("basin"));
            Assert.That(setup.After("Basin", eRealm.Albion).After("Lane", eRealm.Albion).Start, Is.EqualTo("lane"));
            // The owner, 2026-11-21, having picked Real and found two groups: "There should be 6 teams total?" The roam is a start.
            Assert.That(setup.After("Roam", eRealm.Albion).Start, Is.EqualTo("roam"));
            Assert.That(setup.After("Roam", eRealm.Albion).Text(eRealm.Albion), Does.Contain("<Roam>").And.Contain("[Lane]").And.Contain("six groups"));
            Assert.That(setup.Text(eRealm.Albion), Does.Contain("[Roam]").And.Contain("[Thidranki]"));
            Assert.That(setup.After("Thidranki", eRealm.Albion).Start, Is.EqualTo("thid"));
            Assert.That(setup.After("Thidranki", eRealm.Albion).Text(eRealm.Albion), Does.Contain("<Thidranki>").And.Contain("[Roam]"));
            Assert.That(setup.After("Rank7", eRealm.Albion).Rank, Is.EqualTo(7));
            Assert.That(setup.After("Follow", eRealm.Albion).BotDrives, Is.True);
            // M10.2: a level for the player's group and one for the others, each its own clickable words.
            Assert.That(setup.After("G-Sharp", eRealm.Albion).Group, Is.EqualTo(PvpSkill.Sharp));
            Assert.That(setup.After("O-Casual", eRealm.Albion).Others, Is.EqualTo(PvpSkill.Casual));
            Assert.That(setup.After("O-Casual", eRealm.Albion).Group, Is.Null, "the other line is left as it is set");
            Assert.That(setup.After("G-Sharp", eRealm.Albion).Text(eRealm.Albion), Does.Contain("<G-Sharp>").And.Contain("[G-Casual]").And.Contain("[O-Sharp]"));
            Assert.That(setup.After("Follow", eRealm.Albion).After("Lead", eRealm.Albion).BotDrives, Is.False);
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
            Assert.That(text, Does.Contain("<Lead>").And.Contain("[Follow]"));
            Assert.That(new Pvp8v8Setup(eRealm.Midgard, "basin", 5, true).Text(eRealm.Albion), Does.Contain("[Lead]").And.Contain("<Follow>"));
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
