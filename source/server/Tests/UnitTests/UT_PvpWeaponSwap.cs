using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpWeaponSwap
    {
        // has a target, guards somebody, the Slam would land, endurance for it -> shield out
        [TestCase(true, true, true, true, true, TestName = "A target that can be stunned: the shield, for the Slam")]
        [TestCase(true, false, true, true, true, TestName = "The same for a fighter that guards nobody")]
        [TestCase(true, true, false, true, false, TestName = "A target stunned or immune: the two-hander, even for a guard")]
        [TestCase(true, true, true, false, false, TestName = "No endurance for the Slam: the two-hander")]
        [TestCase(false, true, true, true, true, TestName = "Nobody to attack and somebody to guard: the shield")]
        [TestCase(false, true, false, false, true, TestName = "Guarding needs no endurance")]
        [TestCase(false, false, true, true, false, TestName = "Nobody to attack and nobody to guard: the two-hander stays out")]
        public void TheShieldIsForTheSlamAndForGuard(bool hasTarget, bool guards, bool slamWouldLand, bool endurance, bool expected)
        {
            Assert.That(PvpWeaponSwap.ShieldOut(hasTarget, guards, slamWouldLand, endurance), Is.EqualTo(expected));
        }

        // target stunned, has a style from behind, angle, distance, reach -> walks behind
        [TestCase(true, true, 0f, 150, 200, true, TestName = "In front of a stunned target: walks behind")]
        [TestCase(true, true, 100f, 150, 200, true, TestName = "Beside it: walks behind")]
        [TestCase(true, true, 180f, 150, 200, false, TestName = "Behind already: stays")]
        [TestCase(true, true, 150f, 150, 200, true, TestName = "The game counts 60 degrees, the edges not included")]
        [TestCase(false, true, 0f, 150, 200, false, TestName = "A target that can turn: no walk")]
        [TestCase(true, false, 0f, 150, 200, false, TestName = "No style from behind for this weapon: no walk")]
        [TestCase(true, true, 0f, 400, 200, false, TestName = "Not in reach yet: it comes in first")]
        public void AFighterWalksBehindAStunnedTarget(bool stunned, bool backStyle, float angle, int distance, int reach, bool expected)
        {
            Assert.That(PvpWeaponSwap.StepsBehind(stunned, backStyle, angle, distance, reach), Is.EqualTo(expected));
        }

        // The owner, 2026-11-19: Hero, Warrior and Armsman fight two-handed and Slam; the Paladin stays one-hand and shield.
        [TestCase("hero", true)]
        [TestCase("warrior", true)]
        [TestCase("armsman-polearm", true)]
        [TestCase("paladin", false)]
        public void WhichShieldFightersFightTwoHanded(string build, bool twoHanded)
        {
            PvpBuild found = PvpBuildCatalog.Find(build);
            Assert.That(found.TwoHanded, Is.EqualTo(twoHanded));
            Assert.That(found.Lines.Single(line => line.Spec == Specs.Shields).Level, Is.GreaterThanOrEqualTo(42), "Slam is the level 42 shield style");
        }

        [Test]
        public void TheHeroIsLargeWeaponsWithAShieldForSlam()
        {
            PvpBuild hero = PvpBuildCatalog.Find("hero");
            Assert.Multiple(() =>
            {
                Assert.That(hero.TwoHanded, Is.True);
                Assert.That(hero.WeaponTwo, Is.EqualTo(eObjectType.LargeWeapons));
                Assert.That(hero.Lines.Single(line => line.Spec == Specs.Large_Weapons).Level, Is.EqualTo(50));
                Assert.That(hero.Lines.Single(line => line.Spec == Specs.Shields).Level, Is.EqualTo(42), "Slam is the level 42 shield style");
                Assert.That(hero.Lines.Any(line => line.Spec == Specs.Blades), Is.False);
            });
        }
    }
}
