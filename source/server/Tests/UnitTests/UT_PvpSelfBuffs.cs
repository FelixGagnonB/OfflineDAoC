using DOL.AI.Brain;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpSelfBuffs
    {
        // target, duration, concentration, pulsing, held back before the fight
        [TestCase(eSpellTarget.SELF, 600, 0, false, false, TestName = "A self buff (bladeturn, absorption) is cast")]
        [TestCase(eSpellTarget.PET, 0, 12, false, false, TestName = "A buff on its own pet is cast")]
        [TestCase(eSpellTarget.GROUP, 0, 0, true, false, TestName = "A song or chant is sung")]
        [TestCase(eSpellTarget.GROUP, 600, 0, false, true, TestName = "A group resist buff waits")]
        [TestCase(eSpellTarget.REALM, 600, 0, false, true, TestName = "A damage add or haste on a member waits")]
        [TestCase(eSpellTarget.REALM, 0, 20, false, true, TestName = "A concentration buff on a member waits")]
        [TestCase(eSpellTarget.REALM, 0, 0, false, false, TestName = "A heal or cure is not a buff")]
        public void BeforeTheFightOnlyOwnBuffs(eSpellTarget target, int duration, int concentration, bool pulsing, bool heldBack)
        {
            Assert.That(BotBrain.IsBuffOnOthers(target, duration, concentration, pulsing), Is.EqualTo(heldBack));
        }
    }
}
