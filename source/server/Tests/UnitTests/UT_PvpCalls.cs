using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The wording and the pace of a group's calls to its player (bots/pvp/PvpCalls.cs).</summary>
    [TestFixture]
    public class UT_PvpCalls
    {
        [Test]
        public void EveryOrderHasItsOwnCall()
        {
            string[] calls = [.. System.Enum.GetValues<PvpOrder>().Select(PvpCalls.Order)];
            Assert.That(calls, Is.Unique);
            Assert.That(calls, Has.None.Empty);
            Assert.That(PvpCalls.Order(PvpOrder.Regroup), Does.Contain("healers"));
        }

        [Test]
        public void AnAssistNamesTheTargetItsClassAndItsHealthWhenHurt()
        {
            Assert.That(PvpCalls.Assist("Finnarar", eCharacterClass.Healer, 100), Is.EqualTo("Assist Finnarar (Healer)."));
            Assert.That(PvpCalls.Assist("Finnarar", eCharacterClass.Healer, 62), Is.EqualTo("Assist Finnarar (Healer), at 62%."));
        }

        [Test]
        public void IncomingIsSaidByTheVictimOrToIt()
        {
            Assert.That(PvpCalls.Incoming("Aldiorren", 3, false), Is.EqualTo("Incoming on me: 3."));
            Assert.That(PvpCalls.Incoming("Nayas", 2, true), Is.EqualTo("2 on you, Nayas!"));
        }

        [Test]
        public void AnOrderIsSaidOnceItHasStoodAMoment()
        {
            Assert.That(PvpCalls.OrderIsSettled(10_000, 8_000), Is.False, "two seconds old: it may yet change back");
            Assert.That(PvpCalls.OrderIsSettled(11_000, 8_000), Is.True);
        }

        [Test]
        public void OneAttackerIsNotWorthACall()
        {
            Assert.That(PvpCalls.WorthIncoming(1), Is.False);
            Assert.That(PvpCalls.WorthIncoming(2), Is.True);
        }

        [TestCase("push", PvpOrder.Push)]
        [TestCase("Regroup", PvpOrder.Regroup)]
        [TestCase(" back ", PvpOrder.Regroup)]
        [TestCase("hold", PvpOrder.Hold)]
        [TestCase("engage", PvpOrder.Engage)]
        [TestCase("extend", PvpOrder.Extend)]
        public void ThePlayersWordIsAnOrder(string word, PvpOrder expected)
        {
            Assert.That(PvpCalls.OrderFor(word), Is.EqualTo(expected));
        }

        [Test]
        public void ControlThatOneIsACallOfItsOwn()
        {
            Assert.That(PvpCalls.IsControlCall("control"), Is.True);
            Assert.That(PvpCalls.IsControlCall(" Mez "), Is.True);
            Assert.That(PvpCalls.IsControlCall("cc"), Is.True);
            Assert.That(PvpCalls.IsControlCall("push"), Is.False);
            Assert.That(PvpCalls.IsControlCall(null), Is.False);
            Assert.That(PvpCalls.OrderFor("control"), Is.Null, "it names a target and no order");
            Assert.That(PvpCalls.Control("Finnarar", eCharacterClass.Healer), Is.EqualTo("Control on Finnarar (Healer)."));

            // The player's call puts a mez on that enemy ahead of the same mez on another, and does nothing for damage.
            var weights = new PvpWeights();
            var mez = new PvpOption(PvpActionKind.Mez, 0, PvpBuffArchetype.Caster, false, false, 100, false, false, false, false, 100);
            Assert.That(PvpActionChoice.Score(eCharacterClass.Sorcerer, mez with { Called = true }, weights) -
                        PvpActionChoice.Score(eCharacterClass.Sorcerer, mez, weights), Is.EqualTo(weights.ControlCalledByPlayer));
            var root = mez with { Kind = PvpActionKind.Root };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Sorcerer, root with { Called = true }, weights) -
                        PvpActionChoice.Score(eCharacterClass.Sorcerer, root, weights), Is.EqualTo(weights.ControlCalledByPlayer));
            var damage = mez with { Kind = PvpActionKind.Damage };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Sorcerer, damage with { Called = true }, weights),
                Is.EqualTo(PvpActionChoice.Score(eCharacterClass.Sorcerer, damage, weights)));
        }

        [Test]
        public void ExtendIsTheKiteAndIsSaidSo()
        {
            Assert.That(PvpCalls.OrderFor("kite"), Is.EqualTo(PvpOrder.Extend));
            Assert.That(PvpCalls.OrderFor("chase"), Is.Null, "a chase is the opposite of what the order does");
            Assert.That(PvpCalls.Order(PvpOrder.Extend), Does.Contain("fall back"));
            Assert.That(PvpCalls.Answer(PvpOrder.Extend, "Talveh"), Does.Contain("falling back"));
        }

        [Test]
        public void ACalledExtendIsAKiteYouCanSee()
        {
            // "Extend should be us kiting back, in the opposite direction of the enemy team."
            Assert.That(PvpCalls.KiteStepWanted(900, 3_000), Is.True, "an enemy is near: another step back");
            Assert.That(PvpCalls.KiteStepWanted(900, 1_000), Is.False, "between two steps there is time for a cast");
            Assert.That(PvpCalls.KiteStepWanted(2_500, 10_000), Is.False, "far enough: it stands and works");
            Assert.That(PvpCalls.CalledKiteStep, Is.InRange(300, 800));
        }

        [Test]
        public void RestIsACallThatEndsWhenThePlayerWalksOnOrIsAttacked()
        {
            // "to tell the bot to redo their self-timed buffs, druid resists, sit to regen endurance/mana"
            Assert.That(PvpCalls.IsRestCall("rest"), Is.True);
            Assert.That(PvpCalls.IsRestCall(" Sit "), Is.True);
            Assert.That(PvpCalls.IsRestCall("retreat"), Is.False);
            Assert.That(PvpCalls.OrderFor("rest"), Is.Null);
            Assert.That(PvpCalls.RestStands(10_000, 130_000, false, 0), Is.True);
            Assert.That(PvpCalls.RestStands(131_000, 130_000, false, 0), Is.False, "two minutes are up");
            Assert.That(PvpCalls.RestStands(10_000, 130_000, true, 0), Is.False, "somebody is in combat: everybody gets up");
            Assert.That(PvpCalls.RestStands(10_000, 130_000, false, 2_000), Is.False, "the player has walked on");
            Assert.That(PvpCalls.RestStands(10_000, 0, false, 0), Is.False, "nobody called it");
            // An 8v8 bot regains a twentieth a second where a companion of the other modes regains a tenth.
            Assert.That(BotRestRecovery.RecoveryAmount(3, 1_000, 5), Is.EqualTo(50));
            Assert.That(BotRestRecovery.RecoveryAmount(80, 1_000, 5), Is.EqualTo(80), "a stronger native regeneration is kept");
            var real = new PvpRealWeights();
            Assert.That(PvpRealWeights.TeamRests(40, real), Is.True, "a team of bots with a member at 40% rests before it walks on");
            Assert.That(PvpRealWeights.TeamRests(80, real), Is.False);
        }

        [Test]
        public void RetreatIsACallOfItsOwnAndNotARegroup()
        {
            // The owner, 2026-11-21: "retreat: everyone stop doing what they do and follow me".
            Assert.That(PvpCalls.IsRetreatCall("retreat"), Is.True);
            Assert.That(PvpCalls.IsRetreatCall(" Run "), Is.True);
            Assert.That(PvpCalls.IsRetreatCall("follow"), Is.True);
            Assert.That(PvpCalls.IsRetreatCall("regroup"), Is.False);
            Assert.That(PvpCalls.IsRetreatCall(null), Is.False);
            Assert.That(PvpCalls.OrderFor("retreat"), Is.Null, "it was another word for regroup, under which the group goes on fighting");
            Assert.That(PvpCalls.OrderFor("back"), Is.EqualTo(PvpOrder.Regroup));
            Assert.That(PvpCalls.Retreating("Talveh"), Is.EqualTo("Retreating with Talveh."));
            Assert.That(PvpCalls.PlayerRetreatSeconds, Is.EqualTo(30));
        }

        [Test]
        public void AMezThatLandedAndAPurgeAreSaid()
        {
            Assert.That(PvpCalls.WorthMezLanded(3, 0), Is.True);
            Assert.That(PvpCalls.WorthMezLanded(1, 0), Is.False, "one asleep is not worth a line");
            Assert.That(PvpCalls.WorthMezLanded(3, 3), Is.False, "said when it lands, not while it lasts");
            Assert.That(PvpCalls.WorthMezLanded(2, 4), Is.False, "they are waking");
            Assert.That(PvpCalls.MezLanded(4), Is.EqualTo("4 of them are mezzed."));
            Assert.That(PvpCalls.Purged(false), Is.EqualTo("Purge used."));
            Assert.That(PvpCalls.Purged(true), Is.EqualTo("Group Purge used."));
        }

        [Test]
        public void AssistAndAnythingElseAreNoOrder()
        {
            Assert.That(PvpCalls.OrderFor("assist"), Is.Null);
            Assert.That(PvpCalls.OrderFor("dance"), Is.Null);
            Assert.That(PvpCalls.OrderFor(null), Is.Null);
        }

        [Test]
        public void ThePlayersCallStandsForItsTimeAndThenTheSquadDecidesAgain()
        {
            Assert.That(PvpCalls.InForce(PvpOrder.Extend, PvpOrder.Regroup, 5_000, 12_000), Is.EqualTo(PvpOrder.Regroup));
            Assert.That(PvpCalls.InForce(PvpOrder.Extend, PvpOrder.Regroup, 12_000, 12_000), Is.EqualTo(PvpOrder.Extend));
            Assert.That(PvpCalls.InForce(PvpOrder.Push, null, 5_000, 12_000), Is.EqualTo(PvpOrder.Push));
        }

        [Test]
        public void UnderACalledRegroupAMemberComesRightBack()
        {
            Assert.That(PvpCalls.ComesBack(1_200, false), Is.True, "far: it comes");
            Assert.That(PvpCalls.ComesBack(800, false), Is.False, "near enough to stay where it is");
            Assert.That(PvpCalls.ComesBack(800, true), Is.True, "on its way back it keeps coming");
            Assert.That(PvpCalls.ComesBack(500, true), Is.False, "arrived");
        }

        [Test]
        public void EveryCalledOrderIsAnswered()
        {
            Assert.That(PvpCalls.Answer(PvpOrder.Regroup, "Nayas"), Is.EqualTo("Regrouping on Nayas."));
            Assert.That(System.Enum.GetValues<PvpOrder>().Select(order => PvpCalls.Answer(order, "Nayas")), Has.None.Empty);
        }

        [Test]
        public void TheHealerIsToldWhenTwoOrMoreAreOutOfItsReach()
        {
            Assert.That(PvpCalls.WorthOutOfReach(1), Is.False);
            Assert.That(PvpCalls.WorthOutOfReach(2), Is.True);
            Assert.That(PvpCalls.OutOfYourReach("Nayas", 3), Is.EqualTo("Nayas, 3 of us are out of your reach."));
        }

        [Test]
        public void ACallOfAKindWaitsItsTime()
        {
            Assert.That(PvpCalls.Due(10_000, 0, 5_000), Is.True, "the first is always said");
            Assert.That(PvpCalls.Due(10_000, 7_000, 5_000), Is.False);
            Assert.That(PvpCalls.Due(12_000, 7_000, 5_000), Is.True);
            Assert.That(PvpCalls.Due(7_001, 7_000, 0), Is.True, "no pace set");
        }
    }
}
