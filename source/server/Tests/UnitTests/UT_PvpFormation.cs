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
            // Eight members fit in a disc well inside the reach of their speed song.
            Assert.That(places.Max(place => place.Length()), Is.LessThan(400));
            // Nobody stands on anybody.
            float nearest = places.SelectMany((a, i) => places.Where((_, j) => j != i).Select(b => Vector2.Distance(a, b))).Min();
            Assert.That(nearest, Is.GreaterThan(100));
            // No two members level with each other, ahead or across: it is not rows and columns.
            Assert.That(places.Select(place => Math.Round(place.X / 20)).Distinct().Count(), Is.EqualTo(8));
            Assert.That(places.Select(place => Math.Round(place.Y / 20)).Distinct().Count(), Is.EqualTo(8));
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
