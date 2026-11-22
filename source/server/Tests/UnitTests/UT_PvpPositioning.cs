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
        [Test]
        public void FallingBack_TheStepGoesAlongTheGroupsLineOfRetreat()
        {
            var weights = new PvpPositionWeights();
            // A held fighter 300 units to the west. East and north-east are about as safe; the group retreats north.
            PvpThreat[] threats = [new(new Vector2(-300, 0), true, false, false)];
            List<Vector2> alone = PvpPositioning.Steps(Vector2.Zero, threats, [], [], 400, 900, weights, out _);
            List<Vector2> together = PvpPositioning.Steps(Vector2.Zero, threats, [], [], 400, 900, weights, out _, new Vector2(0, 1));
            Assert.That(alone[0].Y, Is.EqualTo(0).Within(1), "alone it steps straight away from the fighter");
            Assert.That(together[0].Y, Is.GreaterThan(100), "with the group it bends its step the way the group is going");
            Assert.That(together[0].X, Is.GreaterThan(0), "and still away from the fighter");
        }

        [Test]
        public void AHealerThatOnlyHealsHangsBackOnIncoming_AndComesInOnceTheFightHasSettled()
        {
            var weights = new PvpPositionWeights();
            Assert.That(weights.HealerHangsBack(true, 2_000, -1), Is.True, "the enemy is coming and nobody has struck");
            Assert.That(weights.HealerHangsBack(true, 3_000, -1), Is.False, "still far: it walks with the group");
            Assert.That(weights.HealerHangsBack(false, 2_000, -1), Is.False, "a Pacification Healer, Bard or Shaman goes in");
            Assert.That(weights.HealerHangsBack(true, 2_000, 3), Is.True, "the opening is not over");
            Assert.That(weights.HealerHangsBack(true, 2_000, 6), Is.False, "the fight has settled");
            Assert.That(new PvpPositionWeights { HealerHangsBackAt = 0 }.HealerHangsBack(true, 500, -1), Is.False, "turned off");
        }

        [Test]
        public void ACasterStandsAtTheEdgeOfItsRange_NotInTheMiddleOfIt()
        {
            var weights = new PvpPositionWeights();
            // An Eldritch 700 units from the enemy caster it is working on; no enemy fighter anywhere near.
            Vector2[] others = [new Vector2(700, 0)];
            PvpWorkPoint[] work = [new(new Vector2(700, 0), weights.WorkRange)];
            Assert.That(PvpPositioning.Steps(Vector2.Zero, [], work, [], 400, 900, weights, out var before), Is.Empty, "as it was: only a fighter made it move");
            Assert.That(before, Is.EqualTo(PvpPositioning.Verdict.Safe));
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, [], work, [], 400, 900, weights, out var verdict, default, others, weights.EdgeDistance);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            float after = Vector2.Distance(steps[0], others[0]);
            Assert.That(after, Is.GreaterThan(1_000), "it backs off");
            Assert.That(after, Is.LessThanOrEqualTo(weights.WorkRange + 60), "and stays in range of its work");
            // At the edge already it stays, and it never trades its work for distance.
            Assert.That(PvpPositioning.Steps(new Vector2(-550, 0), [], work, [], 400, 900, weights, out _, default, others, weights.EdgeDistance), Is.Empty);
            // A fighter still counts for more: with one beside it, the step is the one that gets away from the fighter.
            PvpThreat[] fighter = [new(new Vector2(0, 300), false, false, false)];
            List<Vector2> fromFighter = PvpPositioning.Steps(Vector2.Zero, fighter, work, [], 400, 900, weights, out _, default, others, weights.EdgeDistance);
            Assert.That(Vector2.Distance(fromFighter[0], fighter[0].At), Is.GreaterThan(700));
        }

        [Test]
        public void AHealerBreaksOffAStepForAMemberThatIsBadlyHurt()
        {
            // Before a group's first death its other healers were walking 16% of the time, half of it on steps of position.
            var weights = new PvpPositionWeights();
            Assert.Multiple(() =>
            {
                Assert.That(weights.BreaksOffToHeal(true, false, 45), Is.True);
                Assert.That(weights.BreaksOffToHeal(true, false, 80), Is.False, "nobody badly hurt: it finishes its step");
                Assert.That(weights.BreaksOffToHeal(true, true, 20), Is.False, "a fighter is on it: it keeps running");
                Assert.That(weights.BreaksOffToHeal(false, false, 20), Is.False, "a caster is not a healer");
                Assert.That(new PvpPositionWeights { HealerBreaksOffAStepToHeal = false }.BreaksOffToHeal(true, false, 20), Is.False);
            });
        }

        [Test]
        public void TheBackRowStopsAtRangeBeforeTheFirstBlow()
        {
            // Seen in the pictures of an opening, 2026-11-19: healers and casters ran in with their fighters.
            var weights = new PvpPositionWeights();
            Assert.Multiple(() =>
            {
                Assert.That(weights.BackRowStops(false, 3_000, false), Is.True, "a healer or caster with the enemy coming");
                Assert.That(weights.BackRowStops(false, 4_000, false), Is.False, "still far: it keeps its place in the formation");
                Assert.That(weights.BackRowStops(true, 3_000, false), Is.False, "a fighter goes on");
                Assert.That(weights.BackRowStops(false, 3_000, true), Is.False, "once the fight is on its brain decides");
                Assert.That(new PvpPositionWeights { BackRowStopsAt = 0 }.BackRowStops(false, 500, false), Is.False, "switched off");
            });
        }

        [Test]
        public void AHealerLeftBehindFollowsItsGroup()
        {
            // The owner, 2026-11-19: Hibernia pushes and both Druids stay out of range of almost all their teammates.
            var weights = new PvpPositionWeights();
            var self = new Vector2(0, 0);
            Vector2[] group = [new(2_600, 0), new(2_900, 200), new(3_200, -200), new(900, 0)];
            List<PvpWorkPoint> work = group.Select(member => new PvpWorkPoint(member, weights.HealRange, 0.5)).ToList();
            Assert.That(PvpPositioning.LeftBehind(self, group, weights), Is.True);
            Assert.That(PvpPositioning.LeftBehind(self, [new(2_600, 0), new(900, 0)], weights), Is.False, "one member gone deep is not the group");
            List<Vector2> steps = PvpPositioning.Steps(self, [], work, [], 350, weights.SafeDistance, weights, out _, leftBehind: true);
            Assert.That(steps, Is.Not.Empty);
            Assert.That(steps[0].X, Is.GreaterThan(300), "toward the members it cannot reach");
            // As it was: with no enemy near, no step at all.
            Assert.That(PvpPositioning.Steps(self, [], work, [], 350, weights.SafeDistance, weights, out _), Is.Empty);
        }

        [Test]
        public void ACasterWithNoEnemyInReachClosesUpToTheEdge()
        {
            var weights = new PvpPositionWeights();
            // The nearest enemy, a caster, is 1,900 units away; the kill target stands behind it.
            Vector2[] others = [new Vector2(1_900, 0), new Vector2(2_400, 300)];
            PvpWorkPoint[] work = [new(new Vector2(2_400, 300), weights.WorkRange, weights.KillTargetRangeWeight)];
            Assert.That(PvpPositioning.Steps(Vector2.Zero, [], work, [], 400, 900, weights, out _, default, others, weights.EdgeDistance), Is.Empty,
                "as it was: nothing brought it forward");
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, [], work, [], 400, 900, weights, out var verdict, default, others,
                weights.EdgeDistance, weights.WorkRange);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            float after = Vector2.Distance(steps[0], others[0]);
            Assert.That(after, Is.LessThan(1_900 - 300), "it walks toward them");
            Assert.That(after, Is.GreaterThanOrEqualTo(weights.EdgeDistance - 200), "and no farther than the edge");
            // In reach already, it stays; and so does a healer (no reach given), wherever the enemy is.
            Assert.That(PvpPositioning.Steps(new Vector2(500, 0), [], work, [], 400, 900, weights, out _, default, others,
                weights.EdgeDistance, weights.WorkRange), Is.Empty);
            // With the weight at nothing it stands as before.
            var off = new PvpPositionWeights { PerUnitOutOfReach = 0 };
            Assert.That(PvpPositioning.Steps(Vector2.Zero, [], work, [], 400, 900, off, out _, default, others, off.EdgeDistance, off.WorkRange), Is.Empty);
        }

        [Test]
        public void AHealerSteppingAwayDoesNotStepBesideTheOtherHealer()
        {
            var weights = new PvpPositionWeights();
            // A fighter comes at the healer from the east; the other healer stands 700 units to the west.
            PvpThreat[] fighter = [new(new Vector2(500, 0), true, false, false)];
            Vector2[] otherHealer = [new Vector2(-700, 0)];
            // As it was, the place beside the other healer was as good as any other that far from the fighter.
            var beside = new Vector2(-700, 100);
            var clear = new Vector2(-300, 700);
            Assert.That(PvpPositioning.Score(beside, fighter, [], [], 400, 900, weights), Is.EqualTo(PvpPositioning.Score(clear, fighter, [], [], 400, 900, weights)));
            Assert.That(PvpPositioning.Score(beside, fighter, [], [], 400, 900, weights, null, 0, 0, otherHealer, 1_000),
                Is.LessThan(PvpPositioning.Score(clear, fighter, [], [], 400, 900, weights, null, 0, 0, otherHealer, 1_000) - 500));
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, fighter, [], [], 400, 900, weights, out var verdict, default, null, 0, 0, otherHealer, 1_000);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            Assert.That(Vector2.Distance(steps[0], otherHealer[0]), Is.GreaterThan(700), "it steps away from both");
            Assert.That(Vector2.Distance(steps[0], fighter[0].At), Is.GreaterThan(500));
        }

        [Test]
        public void AStepAwayFromAFighterDoesNotEndAmongTheEnemysCasters()
        {
            var weights = new PvpPositionWeights();
            // A Sorcerer with an enemy fighter 500 units behind it, and the enemy caster it works on 1,900 ahead: out of its range.
            PvpThreat[] fighter = [new(new Vector2(-500, 0), false, false, false)];
            Vector2[] others = [new Vector2(1_900, 0)];
            PvpWorkPoint[] work = [new(others[0], weights.WorkRange)];
            var asBefore = new PvpPositionWeights { StepNearerTheirBackLineAtMost = -1 };
            List<Vector2> was = PvpPositioning.Steps(Vector2.Zero, fighter, work, [], 400, 900, asBefore, out _, default, others, asBefore.EdgeDistance);
            Assert.That(Vector2.Distance(was[0], others[0]), Is.LessThan(asBefore.EdgeDistance - 100), "as it was: away from the fighter and into its target's range, among them");
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, fighter, work, [], 400, 900, weights, out var verdict, default, others, weights.EdgeDistance);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            Assert.That(steps.All(step => Vector2.Distance(step, others[0]) >= weights.EdgeDistance - weights.StepNearerTheirBackLineAtMost), Is.True);
            Assert.That(Vector2.Distance(steps[0], fighter[0].At), Is.GreaterThan(600), "and still away from the fighter");
            // One already among them does not go deeper.
            Vector2[] close = [new Vector2(600, 0)];
            List<Vector2> deep = PvpPositioning.Steps(Vector2.Zero, fighter, [], [], 400, 900, weights, out _, default, close, weights.EdgeDistance);
            Assert.That(deep, Is.Not.Empty);
            Assert.That(deep.All(step => Vector2.Distance(step, close[0]) >= 500), Is.True);
        }

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

        [Test]
        public void WithOneOfItsAttackersSnaredItRunsFromTheOther()
        {
            // The owner, 2026-11-19: the Armsman on the Druid was snared, the Paladin was on it too, and the Druid stood still.
            PvpThreat snared = new(new Vector2(150, 0), false, true, true), free = new(new Vector2(200, 100), false, false, true);
            List<Vector2> steps = Steps(Vector2.Zero, [snared, free], [], out var verdict);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Step));
            Assert.That(steps, Is.Not.Empty);
            Assert.That(steps[0].X, Is.LessThan(0), "away from both");
        }

        [TestCase(150)]
        [TestCase(500)]
        public void WithAFreeFighterOnItAndCloseItKitesBack(int away)
        {
            // Both run at the same speed, so it gains no distance; it moves all the same, away from the fighter.
            List<Vector2> steps = Steps(Vector2.Zero, [new PvpThreat(new Vector2(away, 0), false, false, true)], [], out var verdict);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Kite));
            Assert.That(steps, Is.Not.Empty);
            Assert.That(steps[0].X, Is.LessThan(0), "away from the fighter");
        }

        [TestCase(150)]
        [TestCase(500)]
        public void WithKitingOffItStaysAsItDid(int away)
        {
            var standing = new PvpPositionWeights { KitesWhenPinned = false };
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, [new PvpThreat(new Vector2(away, 0), false, false, true)], [], [], 400, 900,
                standing, out var verdict);
            Assert.That(steps, Is.Empty);
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Pinned));
        }

        [Test]
        public void ItKitesTheWayItIsToldToFallBack()
        {
            // A fighter beside it to the north; the group's way back is west: of the steps that leave the fighter, the western ones lead.
            var threat = new PvpThreat(new Vector2(0, 150), false, false, true);
            List<Vector2> steps = PvpPositioning.Steps(Vector2.Zero, [threat], [], [], 400, 900, Weights, out var verdict, new Vector2(-1, 0));
            Assert.That(verdict, Is.EqualTo(PvpPositioning.Verdict.Kite));
            Assert.That(steps[0].X, Is.LessThan(0));
            Assert.That(steps[0].Y, Is.LessThanOrEqualTo(0), "and not towards the fighter");
        }

        [Test]
        public void SprintingTakesEnduranceAndABotOutOfBreathRuns()
        {
            Assert.That(Weights.CanSprint(100, false), Is.True);
            Assert.That(Weights.CanSprint(6, true), Is.True, "it sprints on to its last breath");
            Assert.That(Weights.CanSprint(5, true), Is.False, "and then it is out");
            Assert.That(Weights.CanSprint(12, false), Is.False, "it does not start again on a few points");
            Assert.That(Weights.CanSprint(20, false), Is.True, "but once it has a fifth back");
            Assert.That(new PvpPositionWeights { SprintCostPerSecond = 0 }.CanSprint(0, false), Is.True, "free, as it was");
        }

        [Test]
        public void TheLastTwoOfABeatenGroupStandAndFight()
        {
            // The owner, 2026-11-20: "When there are like 2 vs 6-8, the 2 could just stop moving instead of running
            // around trying to kite or something."
            Assert.That(Weights.LastStand(2, 6), Is.True);
            Assert.That(Weights.LastStand(2, 8), Is.True);
            Assert.That(Weights.LastStand(1, 3), Is.True);
            Assert.That(Weights.LastStand(2, 5), Is.False, "two against five still play it out");
            Assert.That(Weights.LastStand(3, 8), Is.False, "three is still a group");
            // And the winners: "there's no point in keeping all the rules actives. It just drags the fight for no reasons."
            Assert.That(Weights.MopsUp(6, 2), Is.True);
            Assert.That(Weights.MopsUp(8, 1), Is.True);
            Assert.That(Weights.MopsUp(5, 2), Is.False, "five against two still play it out");
            Assert.That(Weights.MopsUp(8, 3), Is.False);
            Assert.That(PvpPositionWeights.NotInAMopUp(PvpActionKind.Mez), Is.True, "nobody puts the last of them to sleep");
            Assert.That(PvpPositionWeights.NotInAMopUp(PvpActionKind.MezArea), Is.True);
            Assert.That(PvpPositionWeights.NotInAMopUp(PvpActionKind.Damage), Is.False);
            Assert.That(PvpPositionWeights.NotInAMopUp(PvpActionKind.Stun), Is.False, "a stun holds it for the kill");
            Assert.That(PvpPositionWeights.NotInAMopUp(PvpActionKind.Root), Is.False, "a root catches one that runs");
            Assert.That(Weights.LastStand(0, 8), Is.False, "nobody left is no stand");
            Assert.That(new PvpPositionWeights { LastStandAtMost = 0 }.LastStand(1, 8), Is.False, "off");
        }

        [Test]
        public void AFighterCanKeepEnduranceForItsStyles()
        {
            Assert.That(Weights.FighterKeepsEndurancePercent, Is.EqualTo(40), "as built since the comparison of 2026-11-20");
            Assert.That(Weights.CanSprint(30, true, 40), Is.False, "it stops sprinting at what it keeps");
            Assert.That(Weights.CanSprint(41, true, 40), Is.True);
            Assert.That(Weights.CanSprint(50, false, 40), Is.False, "and starts again only with some to spare");
            Assert.That(Weights.CanSprint(55, false, 40), Is.True);
            Assert.That(Weights.CanSprint(6, true, 0), Is.True, "nothing kept: to its last breath, as before");
        }

        [Test]
        public void InAFightABotSprintsWhenItIsPressedOrHasGroundToCover()
        {
            // The owner, 2026-11-20: "Sprint during fight when needed."
            Assert.That(Weights.SprintNeeded(false, 0, false), Is.False, "in melee, or standing to cast");
            Assert.That(Weights.SprintNeeded(false, 350, false), Is.False, "an ordinary step");
            Assert.That(Weights.SprintNeeded(false, 700, false), Is.True, "a long step, or a target that far");
            Assert.That(Weights.SprintNeeded(false, 300, true), Is.True, "it sprints on until it is there");
            Assert.That(Weights.SprintNeeded(false, 100, true), Is.False, "and then it runs");
            Assert.That(Weights.SprintNeeded(true, 0, false), Is.True, "a fighter on it, falling back, or a chase");
        }

        [Test]
        public void ABotWalkingIntoRangeStopsAtAHeightBetweenItsOwnAndItsTargets()
        {
            // A healer 2,500 from its patient, 300 above it, with a spell that reaches 1,850.
            var from = new System.Numerics.Vector3(2_500, 0, 3_900);
            var to = new System.Numerics.Vector3(0, 0, 3_600);
            System.Numerics.Vector3 stop = PvpPositioning.StopShortOf(from, to, 1_850);
            Assert.That(System.Numerics.Vector3.Distance(stop, to), Is.EqualTo(1_700).Within(1), "150 inside its range");
            Assert.That(stop.Z, Is.LessThan(3_900).And.GreaterThan(3_600), "down the slope, not at its own height");
            // Already in range: it stays where it is.
            Assert.That(PvpPositioning.StopShortOf(new System.Numerics.Vector3(1_000, 0, 3_700), to, 1_850), Is.EqualTo(new System.Numerics.Vector3(1_000, 0, 3_700)));
        }

        [Test]
        public void AFighterThatIsNotOnItDoesNotPinIt()
        {
            Assert.That(PvpPositioning.Pinned(Vector2.Zero, [new PvpThreat(new Vector2(150, 0), false, false, false)], Weights), Is.False);
            Assert.That(PvpPositioning.Pinned(Vector2.Zero, [new PvpThreat(new Vector2(150, 0), false, false, true)], Weights), Is.True);
            Assert.That(PvpPositioning.Pinned(Vector2.Zero, [new PvpThreat(new Vector2(900, 0), false, false, true)], Weights), Is.False, "too far to be pinned by");
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
