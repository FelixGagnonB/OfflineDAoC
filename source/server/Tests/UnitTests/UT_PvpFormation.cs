using System;
using System.Linq;
using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The shapes of a group off the fight (bots/pvp/PvpFormation.cs).</summary>
    [TestFixture]
    public class UT_PvpFormation
    {
        [Test]
        public void ARowIsCentredWithItsMembersASpacingApart()
        {
            Assert.That(PvpFormation.RowOffsets(4, false, 450), Is.EqualTo(new[] { -675f, -225f, 225f, 675f }));
            Assert.That(PvpFormation.RowOffsets(3, false, 450), Is.EqualTo(new[] { -450f, 0f, 450f }));
            Assert.That(PvpFormation.RowOffsets(0, false, 450), Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TheLeadersPlaceInItsRowIsLeftFree(int members)
        {
            // An area mez reaches about 350 units: nobody stands nearer than a spacing to the leader or to a neighbour.
            float[] offsets = PvpFormation.RowOffsets(members, true, 450);
            Assert.That(offsets, Has.Length.EqualTo(members));
            Assert.That(offsets.All(offset => Math.Abs(offset) >= 450), Is.True);
            Assert.That(offsets, Is.Ordered);
            Assert.That(offsets.Zip(offsets.Skip(1), (left, right) => right - left).All(gap => gap >= 450), Is.True);
            // As many to one side as to the other, give or take one.
            Assert.That(Math.Abs(offsets.Count(offset => offset < 0) - offsets.Count(offset => offset > 0)), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TheSpeedSongDrives()
        {
            // The owner, 2026-11-25: "Ideally, the driver would be: bard; sorc or minstrel; pac healer or skald".
            Assert.That(PvpDriver.Rank(eCharacterClass.Bard, "bard"), Is.EqualTo(0));
            Assert.That(PvpDriver.Rank(eCharacterClass.Sorcerer, "sorcerer-mind"), Is.LessThan(PvpDriver.Rank(eCharacterClass.Minstrel, "minstrel")));
            Assert.That(PvpDriver.Rank(eCharacterClass.Healer, "healer-pac"), Is.LessThan(PvpDriver.Rank(eCharacterClass.Skald, "skald")));
            Assert.That(PvpDriver.Rank(eCharacterClass.Healer, "healer-mend"), Is.EqualTo(-1), "the Mending Healer does not");
            Assert.That(PvpDriver.Rank(eCharacterClass.Hero, "hero"), Is.EqualTo(-1));
            // "when I do not play bard/sorc/minstrel/skald, 1 of the bot drive": asked when, "Automatic, with an off switch".
            Assert.That(PvpDriver.IsADriver(eCharacterClass.Cleric, false), Is.False);
            Assert.That(PvpDriver.IsADriver(eCharacterClass.Minstrel, false), Is.True);
            Assert.That(PvpDriver.IsADriver(eCharacterClass.Healer, pacification: true), Is.True);
            Assert.That(PvpDriver.IsADriver(eCharacterClass.Healer, false), Is.False);
            Assert.That(PvpDriver.BotDrives(said: null, playerIsADriver: false, aCompanionIsUp: true), Is.True);
            Assert.That(PvpDriver.BotDrives(null, true, true), Is.False, "a player that drives its own");
            Assert.That(PvpDriver.BotDrives(false, false, true), Is.False, "/8v8 drive off");
            Assert.That(PvpDriver.BotDrives(true, true, true), Is.True, "/8v8 drive on");
            Assert.That(PvpDriver.BotDrives(true, false, false), Is.False, "nobody up to do it");
            // The driver stands for a player that has fallen behind, until it has come up; not for one that is down (it stays by it).
            Assert.That(PvpDriver.Waits(waitingAlready: false, playerFromTheDriver: 1_600, playerIsUp: true), Is.True);
            Assert.That(PvpDriver.Waits(false, 1_200, true), Is.False);
            Assert.That(PvpDriver.Waits(true, 1_200, true), Is.True, "until it has come up");
            Assert.That(PvpDriver.Waits(true, 700, true), Is.False);
            // "I released. I'm at our spawn point ... and bard is waiting for me ?": 20,000 units off, it comes for its player.
            Assert.That(PvpDriver.ComesForItsPlayer(comingAlready: false, playerFromTheDriver: 20_000, playerIsUp: true), Is.True);
            Assert.That(PvpDriver.ComesForItsPlayer(false, 3_000, true), Is.False, "a little behind: it is on its way, and is waited for");
            Assert.That(PvpDriver.ComesForItsPlayer(true, 3_000, true), Is.True, "until the group has come up to it");
            Assert.That(PvpDriver.ComesForItsPlayer(true, 700, true), Is.False);
            Assert.That(PvpDriver.ComesForItsPlayer(false, 20_000, playerIsUp: false), Is.False, "down, it is stood by where it lies");
            Assert.That(PvpDriver.ComingFor("Jade"), Is.EqualTo("Coming to you, Jade."));
            // "So we are 3 dead after the fight... I released": after the fight a raiser needs the spell's power, not half
            // its pool, and its player comes first.
            var heal = new PvpHealWeights();
            Assert.That(heal.RaisesAfterTheFight(distance: 400, range: 1500, hasThePowerForIt: true), Is.True);
            Assert.That(heal.RaisesAfterTheFight(400, 1500, hasThePowerForIt: false), Is.False);
            Assert.That(heal.RaisesAfterTheFight(1600, 1500, true), Is.False);
            Assert.That(heal.Raises(400, 1500, powerPercent: 30, beingHit: false, lowestHealthInReach: 100, freeFightersAtCorpse: 0, anotherHealerIsRaising: false),
                Is.False, "in a fight it keeps half its power for its heals");
            Assert.That(PvpHealWeights.RaisedInTurn(fightIsOver: true, isThePlayer: true, isAHealer: false),
                Is.LessThan(PvpHealWeights.RaisedInTurn(true, false, isAHealer: true)));
            Assert.That(PvpHealWeights.RaisedInTurn(fightIsOver: false, isThePlayer: true, isAHealer: false),
                Is.GreaterThan(PvpHealWeights.RaisedInTurn(false, false, isAHealer: true)), "in a fight a healer first");
            Assert.That(PvpDriver.WillRaise("Jade"), Is.EqualTo("Stay there, Jade: we will raise you."));
            // "they just flew and abandonned me": no fight is gone for with its player 1,849 units behind, or down.
            Assert.That(PvpDriver.GoesForAFight(playerIsUp: true, playerFromTheDriver: 400), Is.True);
            Assert.That(PvpDriver.GoesForAFight(true, 1_849), Is.False);
            Assert.That(PvpDriver.GoesForAFight(playerIsUp: false, 100), Is.False);
            // "Bots are running on water like on ground but not me": on the water a bot has half its speed.
            Assert.That(PvpWater.SwimShare, Is.EqualTo(0.5));
            Assert.That(PvpWater.IsOn(floorUnderIt: 2960, waterUnderIt: 2960), Is.True);
            Assert.That(PvpWater.IsOn(3443, null), Is.False, "ground");
            Assert.That(PvpWater.IsOn(3040, 2960), Is.False, "a bridge: the deck is the nearer floor");
            Assert.That(PvpWater.IsOn(null, null), Is.False);
            Assert.That(PvpDriver.Waits(false, 5_000, false), Is.False);
            Assert.That(PvpDriver.HoldsAtStart(10_000), Is.True, "not before the buffs are on");
            Assert.That(PvpDriver.HoldsAtStart(31_000), Is.False);
            Assert.That(PvpDriver.MovingOut("mid road"), Is.EqualTo("Stick, moving out: mid road."));
            Assert.That(PvpDriver.MovingOut(null), Is.EqualTo("Stick, moving out."));
            // "the group and still run around, while waiting. We are not suppose to know there are no available fight":
            // the driver does not stand by a fight that is not the group's.
            Assert.That(PvpDriver.GoesOnPastAFight(aCompanionDrives: true, aFightItLeftAloneIsNear: true, goingOnAlready: false), Is.True);
            Assert.That(PvpDriver.GoesOnPastAFight(true, true, true), Is.False, "once");
            Assert.That(PvpDriver.GoesOnPastAFight(true, false, false), Is.False);
            Assert.That(PvpDriver.GoesOnPastAFight(false, true, false), Is.False, "a guild of bots waits a while, as before");
            Assert.That(PvpDriver.Answer(true, null, false), Does.Contain("/8v8 drive off"));
            Assert.That(PvpDriver.Answer(false, false, false), Does.Contain("/8v8 drive on"));
            Assert.That(PvpFormation.DrivesByClass(eCharacterClass.Bard), Is.True);
            Assert.That(PvpFormation.DrivesByClass(eCharacterClass.Skald), Is.True);
            Assert.That(PvpFormation.DrivesByClass(eCharacterClass.Minstrel), Is.True);
            Assert.That(PvpFormation.DrivesByClass(eCharacterClass.Druid), Is.False);
            Assert.That(PvpFormation.DrivesByClass(eCharacterClass.Hero), Is.False);
        }

        [Test]
        public void ARoamGoesBetweenNinePointsInsideTheBox()
        {
            (int X, int Y)[] points = PvpFormation.RoamPoints(0, 0, 26_000, 18_000, 3_000);
            Assert.That(points, Has.Length.EqualTo(9));
            Assert.That(points, Is.Unique);
            Assert.That(points.All(point => point.X >= 3_000 && point.X <= 23_000 && point.Y >= 3_000 && point.Y <= 15_000), Is.True);
            Assert.That(points, Does.Contain((13_000, 9_000)), "the middle is one of them");
        }

        [Test]
        public void TheNextPointOfARoamIsNeverTheOneItIsAt()
        {
            for (int current = 0; current < 9; current++)
            {
                for (int draw = -20; draw < 40; draw++)
                {
                    int next = PvpFormation.NextRoamPoint(current, 9, draw);
                    Assert.That(next, Is.InRange(0, 8));
                    Assert.That(next, Is.Not.EqualTo(current));
                }
            }
            // Every other point can come up.
            Assert.That(Enumerable.Range(0, 8).Select(draw => PvpFormation.NextRoamPoint(4, 9, draw)).Distinct().Count(), Is.EqualTo(8));
            Assert.That(PvpFormation.NextRoamPoint(0, 1, 5), Is.EqualTo(0));
        }

        [Test]
        public void OnTheRoadTheGroupStringsOutBehindItsDriver()
        {
            int[] back = Enumerable.Range(0, 7).Select(PvpFormation.FollowDistance).ToArray();
            Assert.That(back, Is.Ordered.Ascending);
            Assert.That(back, Is.Unique);
            // Close behind it, the way players on /stick run: the last of seven within a few hundred units.
            Assert.That(back.First(), Is.GreaterThanOrEqualTo(60));
            Assert.That(back.Last(), Is.LessThan(400));
            // A follow ends when its leader is farther than the distance it is given: a follower must not lose
            // its driver by falling a little behind (the seven stood still, 2026-11-20).
            Assert.That(PvpFormation.FollowGivesUpBeyond, Is.GreaterThan(30_000));
            // The mover squares it as an int.
            Assert.That((long)PvpFormation.FollowGivesUpBeyond * PvpFormation.FollowGivesUpBeyond, Is.LessThan(int.MaxValue));
        }

        [Test]
        public void TheDriverWaitsForAMemberThatHasDroppedBack()
        {
            Assert.That(PvpFormation.DriverWaits(350, false), Is.False, "strung out behind it as they should be");
            Assert.That(PvpFormation.DriverWaits(900, false), Is.True);
            Assert.That(PvpFormation.DriverWaits(500, true), Is.True, "it waits until they are close again");
            Assert.That(PvpFormation.DriverWaits(380, true), Is.False);
            Assert.That(PvpFormation.DriverWaits(0, false), Is.False, "nobody else alive");
        }
    }
}
