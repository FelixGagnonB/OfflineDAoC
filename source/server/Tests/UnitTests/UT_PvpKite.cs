using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// The group that falls back as one (docs/PVP-8V8-ROADMAP.md, M12). The owner, 2026-11-24:
    /// "it's time to work on the group reaction, awareness and cohesion"; and, the day before, of
    /// an elite eight against a pack: "They will most probably start the fight at a loc and
    /// finish much futher back".
    /// </summary>
    [TestFixture]
    public class UT_PvpKite
    {
        private static readonly PvpKiteWeights Weights = new();

        [Test]
        public void AGroupKnowsWhenTooManyAreOnIt()
        {
            // An even fight: five or six of the enemy's eight aim at the group.
            Assert.That(PvpKite.Outnumbered(8, 6, Weights), Is.False);
            Assert.That(PvpKite.Outnumbered(8, 8, Weights), Is.False);
            // Two groups on one.
            Assert.That(PvpKite.Outnumbered(8, 10, Weights), Is.True);
            Assert.That(PvpKite.Outnumbered(8, 12, Weights), Is.True);
            // The same two groups on a pack of two are an even fight.
            Assert.That(PvpKite.Outnumbered(16, 12, Weights), Is.False);
            // A group that has lost three has more on it than it can stand against.
            Assert.That(PvpKite.Outnumbered(5, 6, Weights), Is.False);
            Assert.That(PvpKite.Outnumbered(4, 6, Weights), Is.True);
            Assert.That(PvpKite.Outnumbered(0, 6, Weights), Is.False, "nobody left to fall back");
            Assert.That(PvpKite.Outnumbered(8, 12, new PvpKiteWeights { Enabled = false }), Is.False);
        }

        [Test]
        public void ItsCallerTakesAMomentThenItGoesBackLegByLeg()
        {
            // Outnumbered and pressed for a second: a caller that needs three has not called yet.
            Assert.That(PvpKite.TakesLeg(true, 3, 1_000, 3_000, false, long.MaxValue, Weights), Is.False);
            Assert.That(PvpKite.TakesLeg(true, 3, 3_000, 3_000, false, long.MaxValue, Weights), Is.True);
            // Not pressed: the enemy is asleep or far, and the group stands and works.
            Assert.That(PvpKite.TakesLeg(true, 1, 9_000, 3_000, false, long.MaxValue, Weights), Is.False);
            Assert.That(PvpKite.TakesLeg(false, 5, 9_000, 3_000, false, long.MaxValue, Weights), Is.False);
            // Falling back already: the next leg when the last is walked and it has stood and worked its time.
            Assert.That(PvpKite.TakesLeg(true, 3, 0, 3_000, true, Weights.LegEveryMilliseconds, Weights), Is.True);
            Assert.That(PvpKite.TakesLeg(true, 3, 0, 3_000, true, Weights.LegEveryMilliseconds - 1, Weights), Is.False);

            // The three levels of play call it at different speeds.
            Assert.That(PvpHumanWeights.For(PvpSkill.Sharp).KiteCalledAfterMaxMilliseconds, Is.LessThan(new PvpHumanWeights().KiteCalledAfterMinMilliseconds));
            Assert.That(PvpHumanWeights.For(PvpSkill.Casual).KiteCalledAfterMinMilliseconds, Is.GreaterThan(new PvpHumanWeights().KiteCalledAfterMaxMilliseconds));
        }

        [Test]
        public void ItGoesStraightAwayFromTheEnemyOrToTheSideNearerItsHome()
        {
            // The enemy to the west, home to the north-east (y grows to the south).
            List<Vector2> ways = PvpKite.Ways(new Vector2(1_000, 0), default, new Vector2(3_000, -3_000));
            Assert.That(ways, Has.Count.EqualTo(7));
            Assert.That(ways.All(way => System.Math.Abs(way.Length() - 1) < 0.001f), Is.True);
            Assert.That(ways[0].X, Is.EqualTo(1).Within(0.001f), "straight away first");
            Assert.That(ways[1].Y, Is.LessThan(0), "then thirty degrees to the side its home is on");
            Assert.That(ways[2].Y, Is.GreaterThan(0));
            Assert.That(Vector2.Dot(ways[1], ways[0]), Is.EqualTo(System.Math.Cos(System.Math.PI / 6)).Within(0.001));
            Assert.That(Vector2.Dot(ways[5], ways[0]), Is.EqualTo(0).Within(0.001), "ninety degrees last");
            Assert.That(ways.All(way => Vector2.Dot(way, ways[0]) > -0.001f), Is.True, "never toward the enemy");

            // Falling back already, it keeps near the way it was going and does not turn at every leg.
            List<Vector2> next = PvpKite.Ways(new Vector2(0, 1_000), new Vector2(1, 0), default);
            Assert.That(next[0].X, Is.EqualTo(next[0].Y).Within(0.001f), "between the way it was going and straight away");
            // Unless the enemy is now on the other side.
            Assert.That(PvpKite.Ways(new Vector2(-1_000, 0), new Vector2(1, 0), default)[0].X, Is.EqualTo(-1).Within(0.001f));
            Assert.That(PvpKite.Ways(Vector2.Zero, default, default), Is.Empty);
        }

        [Test]
        public void EverybodyKeepsItsPlaceInTheGroup()
        {
            var middle = new Vector2(10_000, 10_000);
            // A caster 300 from the middle keeps that; a healer 1,400 away comes in to the radius.
            Assert.That(PvpKite.Offset(new Vector2(10_300, 10_000), middle, Weights.PlaceRadius), Is.EqualTo(new Vector2(300, 0)));
            Vector2 far = PvpKite.Offset(new Vector2(10_000, 11_400), middle, Weights.PlaceRadius);
            Assert.That(far.Length(), Is.EqualTo(Weights.PlaceRadius).Within(0.01f));
            Assert.That(far.X, Is.EqualTo(0).Within(0.01f));

            // Three fighters in a row across the way, on the enemy's side of the place.
            var place = new Vector2(20_000, 20_000);
            var way = new Vector2(1, 0);
            List<Vector2> row = Enumerable.Range(0, 3).Select(index => PvpKite.ScreenPlace(place, way, index, 3, Weights)).ToList();
            Assert.That(row.All(spot => System.Math.Abs(spot.X - (place.X - Weights.Screen)) < 0.01f), Is.True);
            Assert.That(row[1].Y, Is.EqualTo(place.Y).Within(0.01f), "the middle one on the line");
            Assert.That(row[2].Y - row[1].Y, Is.EqualTo(Weights.ScreenSpacing).Within(0.01f));

            // A fighter takes whoever has followed to the place, and nobody farther off.
            Assert.That(PvpKite.FighterTakes(600, Weights), Is.True);
            Assert.That(PvpKite.FighterTakes(Weights.FightersReach + 1, Weights), Is.False);
        }

        [Test]
        public void ABadGroupDoesNotFallBackAsOne()
        {
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Bad).KitesAsOne, Is.False);
            foreach (PvpTeamProfile profile in PvpTeamProfile.All.Where(profile => profile.Kind != PvpTeamKind.Bad))
                Assert.That(profile.KitesAsOne, Is.True, profile.Name);
        }

        [Test]
        public void ATestEveningMayHaveMoreHunters()
        {
            var tonight = new List<PvpGuildTeam>
            {
                new("alb-a", "A", "Albion", nameof(PvpTeamKind.Bad), [], 5),
                new("alb-b", "B", "Albion", nameof(PvpTeamKind.Decent), [], 40),
                new("alb-c", "C", "Albion", nameof(PvpTeamKind.Strong), [], 60),
                new("alb-d", "D", "Albion", nameof(PvpTeamKind.Clingy), [], 20),
                new("mid-a", "A", "Midgard", nameof(PvpTeamKind.Hunter), [], 80),
                new("mid-b", "B", "Midgard", nameof(PvpTeamKind.Elite), [], 90),
            };
            List<PvpGuildTeam> made = PvpTeamPool.WithHunters(tonight, 2);
            Assert.That(made.Where(team => team.Kind == nameof(PvpTeamKind.Hunter)).Select(team => team.Id),
                Is.EquivalentTo(new[] { "alb-b", "alb-c", "mid-a", "mid-b" }));
            Assert.That(made.Single(team => team.Id == "alb-a").Kind, Is.EqualTo(nameof(PvpTeamKind.Bad)), "a pack stays a pack");
            Assert.That(made.Single(team => team.Id == "alb-d").Kind, Is.EqualTo(nameof(PvpTeamKind.Clingy)));
            Assert.That(PvpTeamPool.WithHunters(tonight, 1).Count(team => team.Kind == nameof(PvpTeamKind.Hunter)), Is.EqualTo(2), "one of each realm");
        }
    }
}
