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
        public void ProjectedHealthStaysBetweenZeroAndAHundred()
        {
            Assert.That(PvpHealPlan.Projected(Patient(0, 90, incoming: 900)), Is.EqualTo(100));
            Assert.That(PvpHealPlan.Projected(Patient(0, 0)), Is.EqualTo(0));
        }
    }
}
