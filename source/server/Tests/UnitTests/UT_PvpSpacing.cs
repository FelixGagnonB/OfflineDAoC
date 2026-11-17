using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpSpacing
    {
        [Test]
        public void ALoneCasterWalksStraightToItsRange()
        {
            Vector2 point = PvpSpacing.ApproachPoint(new Vector2(0, 0), new Vector2(3000, 0), 1450, [], 400);
            Assert.That(point.X, Is.EqualTo(1550).Within(1));
            Assert.That(point.Y, Is.EqualTo(0).Within(1));
        }

        [Test]
        public void ACasterDoesNotStopBesideAGroupmate()
        {
            var aim = new Vector2(3000, 0);
            var mate = new Vector2(1550, 100);
            Vector2 point = PvpSpacing.ApproachPoint(new Vector2(0, 0), aim, 1450, [mate], 400);
            Assert.That(Vector2.Distance(point, aim), Is.EqualTo(1450).Within(1), "still in range");
            Assert.That(Vector2.Distance(point, mate), Is.GreaterThan(350), "outside one area mez");
            Assert.That(point.Y, Is.LessThan(0), "away from the groupmate, not through it");
        }

        [Test]
        public void SpreadingCanBeTurnedOff()
        {
            Vector2 point = PvpSpacing.ApproachPoint(new Vector2(0, 0), new Vector2(3000, 0), 1450, [new Vector2(1550, 10)], 0);
            Assert.That(point.Y, Is.EqualTo(0).Within(1));
            Assert.That(PvpSpacing.StepApart(new Vector2(0, 0), [new Vector2(10, 0)], new Vector2(2000, 0), 0, 200), Is.Null);
        }

        [Test]
        public void NobodyCloseMeansNoStep()
        {
            Assert.That(PvpSpacing.StepApart(new Vector2(0, 0), [new Vector2(0, 500)], new Vector2(2000, 0), 400, 200), Is.Null);
        }

        [Test]
        public void ACrowdedCasterStepsAwayFromItsGroupmate()
        {
            Vector2? step = PvpSpacing.StepApart(new Vector2(0, 0), [new Vector2(0, 150)], new Vector2(2000, 0), 400, 200);
            Assert.That(step, Is.Not.Null);
            Assert.That(step.Value.Y, Is.EqualTo(-200).Within(1));
        }

        [Test]
        public void TheStepNeverLeadsTowardTheEnemy()
        {
            // The groupmate is behind: stepping straight away from it would mean walking at the enemy.
            Vector2? step = PvpSpacing.StepApart(new Vector2(0, 0), [new Vector2(-150, 20)], new Vector2(2000, 0), 400, 200);
            Assert.That(step, Is.Not.Null);
            Assert.That(step.Value.X, Is.LessThanOrEqualTo(1));
            Assert.That(step.Value.Length(), Is.EqualTo(200).Within(1));
        }
    }
}
