using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpStylePolicy
    {
        // taunt, detaunt, stuns, target stunned or immune -> allowed
        [TestCase(false, false, false, false, true)]
        [TestCase(true, false, false, false, false)]
        [TestCase(false, true, false, false, false)]
        [TestCase(false, false, true, false, true)]
        [TestCase(false, false, true, true, false)]
        [TestCase(false, false, false, true, true)]
        public void TauntsNeverAndStunsOnlyWhereTheyLand(bool taunt, bool detaunt, bool stuns, bool blocked, bool expected)
        {
            Assert.That(PvpStylePolicy.Allows(taunt, detaunt, stuns, blocked), Is.EqualTo(expected));
        }
    }
}
