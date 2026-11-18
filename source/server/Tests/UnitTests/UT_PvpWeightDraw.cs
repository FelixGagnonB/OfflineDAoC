using System;
using System.Linq;
using System.Text.Json;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpWeightDraw
    {
        [Test]
        public void WithNoShareNothingMoves()
        {
            var from = new PvpWeights();
            PvpWeights drawn = PvpWeightDraw.Vary(from, new Random(1), 0);
            Assert.That(drawn.Drawn, Is.Empty);
            drawn.Drawn = null;
            Assert.That(JsonSerializer.Serialize(drawn), Is.EqualTo(JsonSerializer.Serialize(from)));
        }

        [Test]
        public void EveryWeightCanMove_AndTheFactsOfTheGameDoNot()
        {
            var from = new PvpWeights();
            PvpWeights drawn = PvpWeightDraw.Vary(from, new Random(7), 100);
            Assert.That(drawn.Drawn.Count, Is.GreaterThan(120), "the main weights, the seven nested sets and the class leanings");
            Assert.That(drawn.Drawn.Values.All(level => level is -1 or 1), Is.True);
            Assert.That(drawn.MinScore, Is.EqualTo(30).Or.EqualTo(70));
            Assert.That(drawn.Drawn["MinScore"], Is.EqualTo(drawn.MinScore > 50 ? 1 : -1));
            Assert.That(drawn.ClassBase["Sorcerer"]["Mez"], Is.EqualTo(72).Or.EqualTo(168));
            Assert.That(drawn.Drawn.ContainsKey("ClassBase.Sorcerer.Mez"), Is.True);
            Assert.That(drawn.Position.SafeDistance, Is.EqualTo(540).Or.EqualTo(1260));
            Assert.That(drawn.Heal.TrainedHealBelowPercent, Is.EqualTo(82).Or.EqualTo(100), "a health percentage stays a percentage");
            Assert.That(drawn.ConsiderRange, Is.EqualTo(from.ConsiderRange));
            Assert.That(drawn.Position.WorkRange, Is.EqualTo(from.Position.WorkRange));
            Assert.That(drawn.Peel.MeleeReach, Is.EqualTo(from.Peel.MeleeReach));
            Assert.That(drawn.Interrupt.Means, Is.EqualTo(from.Interrupt.Means));
            Assert.That(from.MinScore, Is.EqualTo(50), "the set it was drawn from is untouched");
            Assert.That(from.ClassBase["Sorcerer"]["Mez"], Is.EqualTo(120));
        }

        [Test]
        public void AShareOfTheWeightsMoves_HalfDownAndHalfUp()
        {
            int total = PvpWeightDraw.Vary(new PvpWeights(), new Random(7), 100).Drawn.Count;
            var levels = Enumerable.Range(0, 40).SelectMany(seed => PvpWeightDraw.Vary(new PvpWeights(), new Random(seed), 50).Drawn.Values).ToList();
            Assert.That(levels.Count / 40.0, Is.InRange(total * 0.4, total * 0.6));
            Assert.That(levels.Count(level => level > 0) / (double)levels.Count, Is.InRange(0.45, 0.55));
        }

        [Test]
        public void TheSameNumbersGiveTheSameDraw()
        {
            string Draw(int seed) => JsonSerializer.Serialize(PvpWeightDraw.Vary(new PvpWeights(), new Random(seed), 50));
            Assert.That(Draw(3), Is.EqualTo(Draw(3)));
            Assert.That(Draw(3), Is.Not.EqualTo(Draw(4)));
        }

        [Test]
        public void ANumberMovesByWhatItIs()
        {
            Assert.That(PvpWeightDraw.MovedInt("NearsightOnHealer", 15, 1, 0.4), Is.EqualTo(25), "a score: at least ten points");
            Assert.That(PvpWeightDraw.MovedInt("TargetMezzed", -120, -1, 0.4), Is.EqualTo(-168));
            Assert.That(PvpWeightDraw.MovedInt("PatientIsHealer", 8, -1, 0.4), Is.EqualTo(4), "a small score: four points");
            Assert.That(PvpWeightDraw.MovedInt("HealBelowPercent", 80, 1, 0.4), Is.EqualTo(95));
            Assert.That(PvpWeightDraw.MovedInt("HoldMaxSeconds", 10, -1, 0.4), Is.EqualTo(6));
            Assert.That(PvpWeightDraw.MovedInt("SpreadHealMinHurt", 3, 1, 0.4), Is.EqualTo(4));
        }
    }
}
