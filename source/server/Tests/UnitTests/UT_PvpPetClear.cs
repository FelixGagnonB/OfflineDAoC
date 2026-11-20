using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpPetClear
    {
        private static readonly PvpPetWeights Weights = new();

        [Test]
        public void TheGroupGathersAtThreePetsAndStaysUntilNoneIsLeft()
        {
            // The owner, 2026-11-19: against pets Hibernia stacked on the Eldritch, which cast its point-blank spell on them.
            Assert.Multiple(() =>
            {
                Assert.That(PvpPetClear.Gathers(2, Weights), Is.False);
                Assert.That(PvpPetClear.Gathers(3, Weights), Is.True);
                Assert.That(PvpPetClear.StaysGathered(1), Is.True);
                Assert.That(PvpPetClear.StaysGathered(0), Is.False);
                Assert.That(PvpPetClear.Gathers(9, new PvpPetWeights { GatherAtPets = 0 }), Is.False, "switched off");
            });
        }

        [Test]
        public void OnlyAMemberWithAPetOnItComes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PvpPetClear.Comes(1, 900, Weights), Is.True);
                Assert.That(PvpPetClear.Comes(0, 900, Weights), Is.False, "no pet on it: it keeps its place");
                Assert.That(PvpPetClear.Comes(2, 100, Weights), Is.False, "beside the caster already");
                Assert.That(PvpPetClear.Comes(2, 2_500, Weights), Is.False, "too far to come");
            });
        }

        [Test]
        public void AnAreaMezCountsTwoPetsAsOneEnemy()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PvpPetClear.MezCount(0, 1), Is.EqualTo(0), "one pet alone is not worth an area mez");
                Assert.That(PvpPetClear.MezCount(0, 2), Is.EqualTo(1));
                Assert.That(PvpPetClear.MezCount(1, 3), Is.EqualTo(2));
                Assert.That(PvpPetClear.MezCount(2, 0), Is.EqualTo(2));
            });
        }

        [Test]
        public void TheSpellNeedsTwoPetsAndNoSleeperInside()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PvpPetClear.Blasts(1, false, Weights), Is.False);
                Assert.That(PvpPetClear.Blasts(2, false, Weights), Is.True);
                Assert.That(PvpPetClear.Blasts(4, true, Weights), Is.False, "it would wake a mezzed enemy");
            });
        }
    }
}
