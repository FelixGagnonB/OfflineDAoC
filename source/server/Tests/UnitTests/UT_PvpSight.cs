using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Sight over the ground (bots/pvp/PvpSight.cs).</summary>
    [TestFixture]
    public class UT_PvpSight
    {
        [Test]
        public void LevelGroundHidesNothing()
        {
            Assert.That(PvpSight.Blocked([100, 100, 100, 100], 100, 100), Is.False);
            Assert.That(PvpSight.Blocked([], 100, 100), Is.False, "too near to have ground between");
        }

        [Test]
        public void AHillBetweenHidesOneFromTheOther()
        {
            // Both stand at 100 and see from 160: a rise to 200 in the middle is above the line.
            Assert.That(PvpSight.Blocked([110, 200, 110], 100, 100), Is.True);
            // A rise that stays under their eyes hides nothing.
            Assert.That(PvpSight.Blocked([110, 150, 110], 100, 100), Is.False);
        }

        [Test]
        public void FromAHillOneSeesOverALowerRise()
        {
            // One at 400, one at 100, eyes 60 higher: the line passes 385 a quarter of the way, 310 half way, 235 at three quarters.
            Assert.That(PvpSight.Blocked([200, 200, 150], 400, 100), Is.False);
            // The same rise hides the two from each other when both stand low.
            Assert.That(PvpSight.Blocked([200, 200, 150], 100, 100), Is.True);
        }

        [Test]
        public void ASlopeBetweenTwoHeightsHidesNothing()
        {
            // Ground that climbs evenly from one to the other lies under the line all the way.
            Assert.That(PvpSight.Blocked([200, 300, 400], 100, 500), Is.False);
            // The brow of the hill just before the higher one does.
            Assert.That(PvpSight.Blocked([200, 300, 520], 100, 500), Is.True);
        }

        [Test]
        public void GroundThatIsNotKnownHidesNothing()
        {
            Assert.That(PvpSight.Blocked([float.NaN, float.NaN], 100, 100), Is.False);
            Assert.That(PvpSight.Blocked([float.NaN, 500], 100, 100), Is.True);
        }

        [TestCase(200, 0)]
        [TestCase(500, 1)]
        [TestCase(1_000, 3)]
        [TestCase(6_500, 25)]
        public void TheGroundIsLookedAtEvery250Units(float distance, int samples)
        {
            Assert.That(PvpSight.Samples(distance), Is.EqualTo(samples));
        }
    }
}
