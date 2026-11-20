using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The roam: the owner's answers of 2026-11-20 (docs/PVP-8V8-ROADMAP.md, M9.7).</summary>
    [TestFixture]
    public class UT_PvpRoam
    {
        [Test]
        public void EachRealmStartsWhereTheOwnerSaid()
        {
            // "Albion would spawn (and release) at their principal keep. Same for midgard."
            Assert.That(PvpRoam.EmainStarts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
            // In front of the outer gate: a bot does not open a keep door, and the keeps' middles are behind two.
            (int X, int Y) albion = PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Albion).Point;
            (int X, int Y) midgard = PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Midgard).Point;
            Assert.That(System.Math.Sqrt(System.Math.Pow(albion.X - 475844, 2) + System.Math.Pow(albion.Y - 343696, 2)), Is.InRange(1_500, 3_500), "the Albion Portal Keep");
            Assert.That(System.Math.Sqrt(System.Math.Pow(midgard.X - 474129, 2) + System.Math.Pow(midgard.Y - 295207, 2)), Is.InRange(1_500, 3_500), "the Midgard Portal Keep");
            // "Hibernia ... could spawn on the hill near the tower in front of Albion Main Gate": the owner's /loc there.
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Hibernia).Point, Is.EqualTo((440776, 337761)));
            // Every team first walks to its own end of the open middle, then to the other: never a far goal across the mountains.
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Albion).Route.First(), Is.EqualTo(PvpRoam.South));
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Midgard).Route.First(), Is.EqualTo(PvpRoam.North));
        }

        [Test]
        public void TheSecondTeamOfALineUpHasItsOwnNameAndSoItsOwnRoster()
        {
            // "I guess if we add teams, we'd need to make sure to persist all the bot / companions and their rps."
            Assert.That(PvpRoam.TeamName("mid-main", 1), Is.EqualTo("mid-main"));
            Assert.That(PvpRoam.TeamName("mid-main", 2), Is.EqualTo("mid-main#2"));
            Assert.That(PvpRealmPoints.EnemyRoster("abc", PvpRoam.TeamName("mid-main", 2)), Is.Not.EqualTo(PvpRealmPoints.EnemyRoster("abc", PvpRoam.TeamName("mid-main", 1))));
        }

        [Test]
        public void ATeamAddsOnItsRealmMatesFightThreeTimesInTen()
        {
            // "It could be random, like 30% would add 70% won't add or so."
            var real = new PvpRealWeights();
            Assert.That(real.AddChancePercent, Is.EqualTo(30));
            Assert.That(Enumerable.Range(0, 100).Count(roll => PvpRoam.Joins(real.AddChancePercent, roll)), Is.EqualTo(30));
            Assert.That(PvpRoam.Joins(0, 0), Is.False, "never");
            Assert.That(PvpRoam.Joins(100, 99), Is.True, "always");
        }
    }
}
