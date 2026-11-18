using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpHealPlan
    {
        [Test]
        public void AHealerRunningLowOnPowerKeepsItsHealsForTheReallyHurt()
        {
            var weights = new PvpHealWeights();
            PvpPatient[] scratched = [new(0, 700, 1_000, 0, PvpBuffArchetype.Melee)];
            Assert.That(PvpHealPlan.Choose(scratched, 0, weights), Is.EqualTo(0), "with power to spare, 70% is healed");
            Assert.That(PvpHealPlan.Choose(scratched, 0, weights, 20), Is.EqualTo(-1), "at a fifth of its power it waits");
            Assert.That(PvpHealPlan.Choose([new(0, 500, 1_000, 0, PvpBuffArchetype.Melee)], 0, weights, 20), Is.EqualTo(0), "half dead is healed all the same");
            PvpPatient[] trained = [new(0, 900, 1_000, 0, PvpBuffArchetype.Healer, Trained: true)];
            Assert.That(PvpHealPlan.Choose(trained, 0, weights), Is.EqualTo(0));
            Assert.That(PvpHealPlan.Choose(trained, 0, weights, 20), Is.EqualTo(-1));
            Assert.That(PvpHealPlan.Choose(scratched, 0, new PvpHealWeights { SavePowerBelowPercent = 0 }, 5), Is.EqualTo(0), "turned off");
        }

        [Test]
        public void AHealIsDroppedWhenItsTargetHasBeenToppedUp_ButNotOnTheTrainedMember()
        {
            var weights = new PvpHealWeights();
            Assert.That(PvpHealPlan.DropsHeal(100, false, weights), Is.True);
            Assert.That(PvpHealPlan.DropsHeal(90, false, weights), Is.False, "still hurt");
            Assert.That(PvpHealPlan.DropsHeal(100, true, weights), Is.False, "damage is on its way to it");
            Assert.That(PvpHealPlan.DropsHeal(100, false, new PvpHealWeights { DropHealAtPercent = 0 }), Is.False, "turned off");
        }

        [Test]
        public void TheBackupHealerTakesOverWhileNoHealerCanHeal()
        {
            var weights = new PvpHealWeights();
            Assert.That(PvpHealPlan.BackupHealsBelow(75, true, weights), Is.EqualTo(75), "a healer is healing: control comes first above 75%");
            Assert.That(PvpHealPlan.BackupHealsBelow(75, false, weights), Is.EqualTo(95), "nobody else can heal");
        }

        [Test]
        public void AHealForSeveralIsForSeveralHurtAtOnce()
        {
            var weights = new PvpHealWeights();
            Assert.That(PvpHealPlan.SeveralHurt([60, 70, 75, 100, 100], false, weights), Is.True, "three hurt");
            Assert.That(PvpHealPlan.SeveralHurt([40, 70, 100, 100], false, weights), Is.False, "two hurt: single heals");
            Assert.That(PvpHealPlan.SeveralHurt([60, 70, 75], true, weights), Is.False, "a trained member gets the single heals");
            Assert.That(PvpHealPlan.SeveralHurt([10, 10, 10], false, new PvpHealWeights { SpreadHealMinHurt = 0 }), Is.False, "turned off");
        }

        // The three heals for several of a level-50 healer on this server: spread 295 for 110 power,
        // the spec group heal 283 for 97, the baseline group heal 93 for 25.
        private static readonly (bool Spread, int Value, int Power)[] AreaHeals = [(true, 295, 110), (false, 283, 97), (false, 93, 25)];

        [Test]
        public void ManyLightlyHurtGetTheCheapGroupHeal()
        {
            // Eight members missing 100 each: 744 health for 25 power beats 800 for 97 or 110.
            Assert.That(PvpHealPlan.BestAreaHeal([100, 100, 100, 100, 100, 100, 100, 100], AreaHeals, 8), Is.EqualTo(2));
        }

        [Test]
        public void AFewBadlyHurtGetTheSpreadHeal()
        {
            // Three members missing 600 each: the spread heal gives 590 to each (1,770 for 110 power),
            // the group heals 283 or 93 to each (849 for 97, 279 for 25).
            Assert.That(PvpHealPlan.BestAreaHeal([600, 600, 600, 0, 0, 0, 0, 0], AreaHeals, 8), Is.EqualTo(0));
        }

        [Test]
        public void NobodyHurtGetsNoHealForSeveral()
        {
            Assert.That(PvpHealPlan.BestAreaHeal([0, 0, 0], AreaHeals, 8), Is.EqualTo(-1));
            Assert.That(PvpHealPlan.BestAreaHeal([500, 500, 500], [], 8), Is.EqualTo(-1), "no such heal ready");
        }

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
