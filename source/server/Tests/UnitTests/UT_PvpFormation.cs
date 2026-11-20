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
