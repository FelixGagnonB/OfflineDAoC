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

        [TestCase(2500, false, 600, 0, false, true, TestName = "A bladeturn on the caster itself is not begun in a fight")]
        [TestCase(2000, false, 1200, 0, false, true, TestName = "Nor a health regeneration buff on a member")]
        [TestCase(2000, false, 0, 12, false, true, TestName = "Nor a concentration buff")]
        [TestCase(4000, false, 0, 0, true, true, TestName = "Nor a healer's pet")]
        [TestCase(4000, false, 0, 0, false, false, TestName = "A caster's pet is its damage: it may be called again")]
        [TestCase(0, false, 30, 0, false, false, TestName = "An instant is not held back here")]
        [TestCase(3000, true, 0, 0, false, false, TestName = "A song has its own routine")]
        [TestCase(2800, false, 0, 0, false, false, TestName = "A heal or cure is not a buff")]
        public void InAFightNobodyStopsToBuff(int castTime, bool pulsing, int duration, int concentration, bool healersPetSummon, bool heldBack)
        {
            Assert.That(BotBrain.IsBuffHeldBackInFight(castTime, pulsing, duration, concentration, healersPetSummon), Is.EqualTo(heldBack));
        }
    }
}
