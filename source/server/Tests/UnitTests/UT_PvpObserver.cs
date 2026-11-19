using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpObserver
    {
        [Test]
        public void EveryLaneMiddleBelongsToItsOwnLane()
        {
            // The owner, 2026-11-19: the feed carried every lane at once. Two middles are 6,000 units apart.
            for (int lane = 0; lane < PvpArenaBox.EmainLanes.Count; lane++)
            {
                PvpArenaBox box = PvpArenaBox.EmainLanes[lane];
                int x = (box.SpawnA.X + box.SpawnB.X) / 2, y = (box.SpawnA.Y + box.SpawnB.Y) / 2;
                Assert.That(PvpObserver.LaneOf(box.Region, x, y), Is.EqualTo(lane));
                // A group at its spawn, and a fight that has drifted 1,500 units off the middle.
                Assert.That(PvpObserver.LaneOf(box.Region, box.SpawnA.X, box.SpawnA.Y), Is.EqualTo(lane));
                Assert.That(PvpObserver.LaneOf(box.Region, box.SpawnB.X, box.SpawnB.Y), Is.EqualTo(lane));
            }
        }

        [Test]
        public void AnotherRegionIsNoLane()
        {
            Assert.That(PvpObserver.LaneOf(1, 500_000, 500_000), Is.EqualTo(-1));
        }
    }
}
