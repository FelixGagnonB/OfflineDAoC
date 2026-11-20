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
        public void TheTravellingClumpIsLooseAndHasNoRows()
        {
            Vector2[] places = Enumerable.Range(0, 8).Select(index => PvpFormation.TravelOffset(index, PvpFormation.TravelSpacing)).ToArray();
            // Eight members fit in a disc well inside the reach of their speed song (2,000).
            Assert.That(places.Max(place => place.Length()), Is.LessThan(900));
            // An area mez reaches about 350 units: on the road no member has more than two others that near.
            float nearest = places.SelectMany((a, i) => places.Where((_, j) => j != i).Select(b => Vector2.Distance(a, b))).Min();
            Assert.That(nearest, Is.GreaterThan(250));
            Assert.That(places.Max(a => places.Count(b => Vector2.Distance(a, b) <= 350)), Is.LessThanOrEqualTo(3));
            // No two members level with each other, ahead or across: it is not rows and columns.
            Assert.That(places.Select(place => Math.Round(place.X / 20)).Distinct().Count(), Is.EqualTo(8));
            Assert.That(places.Select(place => Math.Round(place.Y / 20)).Distinct().Count(), Is.EqualTo(8));
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
        public void TheClumpAimsAheadOnTheRoadAndStopsAtTheGoal()
        {
            Vector2 centre = new(0, 0);
            Assert.That(PvpFormation.TravelAnchor(centre, new Vector2(10_000, 0), 3_000), Is.EqualTo(new Vector2(3_000, 0)));
            Assert.That(PvpFormation.TravelAnchor(centre, new Vector2(0, -1_200), 3_000), Is.EqualTo(new Vector2(0, -1_200)));
            Assert.That(PvpFormation.TravelAnchor(centre, centre, 3_000), Is.EqualTo(centre));
        }
    }
}
