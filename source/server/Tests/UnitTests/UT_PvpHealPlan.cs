using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpHealPlan
    {
        private static readonly PvpHealWeights Weights = new();

        private static PvpPatient Patient(int id, int healthPercent, int incoming = 0, PvpBuffArchetype archetype = PvpBuffArchetype.Melee) =>
            new(id, healthPercent * 10, 1_000, incoming, archetype);

        [Test]
        public void TheSecondHealerGoesToTheNextPatient()
        {
            // The first healer's heal on member 0 will bring it to 90%: member 1 is now the one in need.
            List<PvpPatient> patients = [Patient(0, 30, incoming: 600), Patient(1, 50)];
            Assert.That(PvpHealPlan.Choose(patients, 0, Weights), Is.EqualTo(1));
        }

        [Test]
        public void AHealThatIsNotEnoughStillLeavesThePatientInNeed()
        {
            List<PvpPatient> patients = [Patient(0, 10, incoming: 300), Patient(1, 70)];
            Assert.That(PvpHealPlan.Choose(patients, 0, Weights), Is.EqualTo(0), "40% after the first heal is still the worst off");
        }

        [Test]
        public void WithEveryoneCoveredTheTurnIsFree()
        {
            List<PvpPatient> patients = [Patient(0, 40, incoming: 500), Patient(1, 85)];
            Assert.That(PvpHealPlan.Choose(patients, 0, Weights), Is.EqualTo(-1));
            Assert.That(PvpHealPlan.Choose([], 0, Weights), Is.EqualTo(-1));
        }

        [Test]
        public void TheHealersOwnPickHoldsAgainstASlightlyWorsePatient()
        {
            List<PvpPatient> patients = [Patient(0, 52), Patient(1, 50)];
            Assert.That(PvpHealPlan.Choose(patients, 0, Weights), Is.EqualTo(0));
            Assert.That(PvpHealPlan.Choose([Patient(0, 60), Patient(1, 40)], 0, Weights), Is.EqualTo(1));
        }

        [Test]
        public void AHealerIsHealedBeforeAFighterAtTheSameHealth()
        {
            List<PvpPatient> patients = [Patient(0, 50), Patient(1, 50, archetype: PvpBuffArchetype.Healer)];
            Assert.That(PvpHealPlan.Choose(patients, -1, Weights), Is.EqualTo(1));
        }

        [Test]
        public void TheMemberWithTheEnemyFightersOnItIsTheTrainedOne()
        {
            Assert.That(PvpHealPlan.Trained([0, 1, 3, 0], -1, Weights), Is.EqualTo(2));
            Assert.That(PvpHealPlan.Trained([0, 1, 1, 0], -1, Weights), Is.EqualTo(-1), "one fighter on someone is not a train");
            Assert.That(PvpHealPlan.Trained([2, 0, 2, 0], 2, Weights), Is.EqualTo(2), "a tie leaves the call where it was");
        }

        [Test]
        public void ATrainedMemberIsHealedBeforeItIsBadlyHurt()
        {
            // Member 0 is at 90% with the train on it; member 1 is at 70% and left alone.
            List<PvpPatient> patients = [new(0, 900, 1_000, 0, PvpBuffArchetype.Healer, Trained: true), Patient(1, 70)];
            Assert.That(PvpHealPlan.Choose(patients, 1, Weights), Is.EqualTo(0));
            // Untouched, it needs nothing yet.
            Assert.That(PvpHealPlan.Choose([new(0, 1_000, 1_000, 0, PvpBuffArchetype.Healer, Trained: true)], 0, Weights), Is.EqualTo(-1));
        }

        [Test]
        public void AHealersTwoHealsAreTheGreaterAndTheMajor()
        {
            // A Regrowth Druid's single heals in this server's data: Greater, Major, plain, Minor.
            (double, int)[] druid = [(549, 109), (387, 57), (231, 42), (119, 17)];
            Assert.That(PvpHealPlan.PickHeals(druid, Weights), Is.EqualTo((1, 0)), "the Major heal for any time, the Greater for big heals");
            // A Shaman has only the base line: the plain heal is its big one, the Minor its other.
            (double, int)[] shaman = [(231, 42), (119, 17)];
            Assert.That(PvpHealPlan.PickHeals(shaman, Weights), Is.EqualTo((1, 0)));
            Assert.That(PvpHealPlan.PickHeals([(300, 50)], Weights), Is.EqualTo((0, 0)));
            Assert.That(PvpHealPlan.PickHeals([], Weights), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void ProjectedHealthStaysBetweenZeroAndAHundred()
        {
            Assert.That(PvpHealPlan.Projected(Patient(0, 90, incoming: 900)), Is.EqualTo(100));
            Assert.That(PvpHealPlan.Projected(Patient(0, 0)), Is.EqualTo(0));
        }
    }
}
