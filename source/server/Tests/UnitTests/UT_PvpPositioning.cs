using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpPositioning
    {
        private static readonly PvpPositionWeights Weights = new();

        private static List<Vector2> Steps(Vector2 self, PvpThreat[] threats, PvpWorkPoint[] work, out PvpPositioning.Verdict verdict,
            Vector2[] mates = null, int safe = 900) =>
            PvpPositioning.Steps(self, threats, work, mates ?? [], 400, safe, Weights, out verdict);

        [Test]
        public void WithNoFighterNearItStaysWhereItIs()
        {
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(1200, 0), false, false, false)], [], out var verdict);
            Assert.That(steps, Is.Empty);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Safe));
        }

        [Test]
        public void AFighterGoingForSomebodyElseIsSteppedAwayFrom()
        {
            var fighter = new Vector2(300, 0);
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(fighter, false, false, false)], [], out var verdict);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            Assert.That(Vector2.Distance(steps[0], fighter), Is.GreaterThanOrEqualTo(900), "to a safe distance in one go");
        }

        [TestCase(150)]
        [TestCase(500)]
        public void WithAFreeFighterOnItAndCloseItStays(int away)
        {
            // Both run at the same speed: walking away gains nothing.
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(away, 0), false, false, true)], [], out var verdict);
            Assert.That(steps, Is.Empty);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Pinned));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void OnceTheFighterOnItIsHeldOrSlowedItRuns(bool held, bool slowed)
        {
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(150, 0), held, slowed, true)], [], out var verdict);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            Assert.That(steps[0].X, Is.LessThan(-300));
        }

        [Test]
        public void AFighterComingForItFromADistanceIsRunFrom()
        {
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(700, 0), false, false, true)], [], out var verdict);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
        }

        [Test]
        public void TheStepKeepsItsWorkInRange()
        {
            // The enemy it works on is 1,300 away behind the fighter: straight back would put it out of range.
            var work = new PvpWorkPoint(new Vector2(1300, 0), 1350);
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(300, 0), false, false, false)], [work], out _);
            Assert.That(steps, Is.Not.Empty);
            Assert.That(Vector2.Distance(steps[0], work.At), Is.LessThanOrEqualTo(1360), "still in range");
            Assert.That(Vector2.Distance(steps[0], new Vector2(300, 0)), Is.GreaterThan(450), "and farther from the fighter");
        }

        [Test]
        public void WithNowhereBetterItStays()
        {
            // Fighters on both sides and its work straight ahead: every step loses as much as it gains.
            PvpThreat[] threats = [new(new Vector2(400, 0), false, false, false), new(new Vector2(-400, 0), false, false, false),
                new(new Vector2(0, 400), false, false, false), new(new Vector2(0, -400), false, false, false)];
            List<Vector2> steps = Steps(Vector2.Zero, threats, [], out var verdict);
            Assert.That(steps, Is.Empty);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.NothingBetter));
        }

        [Test]
        public void ItDoesNotStepOntoAGroupmate()
        {
            var mate = new Vector2(-350, 0);
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(300, 0), false, false, false)], [], out _, [mate]);
            Assert.That(steps, Is.Not.Empty);
            Assert.That(Vector2.Distance(steps[0], mate), Is.GreaterThan(200));
        }

        [Test]
        public void AGroupThatExtendsWantsMoreDistance()
        {
            PvpThreat[] threats = [new(new Vector2(1000, 0), false, false, false)];
            Assert.That(Steps(Vector2.Zero, threats, [], out _), Is.Empty, "far enough for a group that engages");
            Assert.That(Steps(Vector2.Zero, threats, [], out _, safe: 1260), Is.Not.Empty, "not for one that extends");
        }

        [Test]
        public void WithoutTheLongStepItGoesOneShortStep()
        {
            var fighter = new Vector2(300, 0);
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, [new PvpThreat(fighter, false, false, false)], [], [], 400, 900,
                new PvpPositionWeights { LongStep = 0 }, out _);
            Assert.That(Vector2.Distance(steps[0], fighter), Is.EqualTo(650).Within(1));
        }

        [Test]
        public void PositioningCanBeTurnedOff()
        {
            Assert.That(Steps(Vector2.Zero, [new PvpThreat(new Vector2(300, 0), false, false, false)], [], out _, safe: 0), Is.Empty);
        }
    }
}
