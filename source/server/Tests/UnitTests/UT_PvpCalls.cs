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
