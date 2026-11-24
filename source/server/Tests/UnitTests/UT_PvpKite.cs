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
        public void AGroupThatHasLostBreaksOff()
        {
            // Half of it dead and the enemy half again as many as its side: it leaves.
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 4, 6, Weights), Is.True);
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 4, 5, Weights), Is.False, "four against five may still be won");
            Assert.That(PvpKite.BreaksOff(5, 8, 1, 2, 5, 16, Weights), Is.False, "five of eight with a healer fight on");
            // Its healers dead, it does not wait for the fourth to fall.
            Assert.That(PvpKite.BreaksOff(6, 8, 0, 2, 6, 9, Weights), Is.True);
            Assert.That(PvpKite.BreaksOff(6, 8, 0, 2, 6, 8, Weights), Is.False);
            Assert.That(PvpKite.BreaksOff(8, 8, 0, 0, 8, 16, Weights), Is.False, "a group that never had a healer has not lost one");
            // With its realm fighting beside it, its four are not alone.
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 12, 16, Weights), Is.False);
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 4, 8, new PvpKiteWeights { BreaksOffAtAlivePercent = 0 }), Is.False);
            Assert.That(PvpKite.BreaksOff(0, 8, 0, 2, 0, 8, Weights), Is.False);
            // In some of the fights it loses, not in all: "you would sometimes see a beaten group turn and run".
            Assert.That(Weights.BreaksOffChancePercent, Is.InRange(20, 50));
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 4, 6, Weights, Weights.BreaksOffChancePercent - 1), Is.True);
            Assert.That(PvpKite.BreaksOff(4, 8, 1, 2, 4, 6, Weights, Weights.BreaksOffChancePercent), Is.False, "this time it fights to its last");

            // It runs to the side with the fewest enemies; toward its start when no side is better; and once it runs it keeps its way.
            Assert.That(PvpKite.RunsAway(new Vector2(1_000, 0), default, new Vector2(0, 5_000)), Is.EqualTo(new Vector2(1_000, 0)));
            Assert.That(PvpKite.RunsAway(default, default, new Vector2(0, 5_000)), Is.EqualTo(new Vector2(0, 5_000)));
            Vector2 kept = PvpKite.RunsAway(new Vector2(-1_000, 0), new Vector2(0, 1), new Vector2(5_000, 0));
            Assert.That(kept, Is.EqualTo(new Vector2(0, 1_000)));
            // (A kept way is still a way: one unit long, it was taken for none, and the group stood where its first stretch ended.)
            Assert.That(PvpKite.Ways(kept, default, default), Is.Not.Empty);
            Assert.That(PvpKite.Ways(kept, default, default)[0].Y, Is.EqualTo(1).Within(0.001f));

            // Two enemy groups to the west and the south, one enemy to the north: the way is east, a little south of it.
            var self = new Vector2(10_000, 10_000);
            var around = new List<Vector2>();
            for (int index = 0; index < 8; index++)
            {
                around.Add(new Vector2(9_200 - index * 30, 10_000));
                around.Add(new Vector2(10_000, 10_800 + index * 30));
            }
            around.Add(new Vector2(10_000, 9_300));
            Vector2 away = PvpKite.AwayFrom(self, around);
            Assert.That(away.Length(), Is.EqualTo(1_000).Within(0.5f));
            Assert.That(away.X, Is.GreaterThan(500));
            Assert.That(away.Y, Is.LessThan(0), "y grows to the south: more of them stand there than to the north");
            // Enemies on every side alike: no side is better.
            Assert.That(PvpKite.AwayFrom(self, [new Vector2(9_000, 10_000), new Vector2(11_000, 10_000), new Vector2(10_000, 9_000), new Vector2(10_000, 11_000)]),
                Is.EqualTo(Vector2.Zero));
            Assert.That(PvpKite.AwayFrom(self, []), Is.EqualTo(Vector2.Zero));

            // A team of bots follows it only so far, and it is away once nobody has been near for a moment.
            Assert.That(PvpKite.Followed(Weights.FollowedFor, Weights), Is.True);
            Assert.That(PvpKite.Followed(Weights.FollowedFor + 1, Weights), Is.False);
            Assert.That(PvpKite.GotAway(Weights.ClearSeconds - 1, Weights), Is.False);
            Assert.That(PvpKite.GotAway(Weights.ClearSeconds, Weights), Is.True);
            Assert.That(PvpKite.RunIsOver(Weights.LeavesAtMostSeconds - 1, Weights), Is.False);
            Assert.That(PvpKite.RunIsOver(Weights.LeavesAtMostSeconds, Weights), Is.True);
            // Away, it is taken off the field to form up at its start; not where a player would see it vanish.
            Assert.That(PvpKite.FormsUpAgain(true, false), Is.True);
            Assert.That(PvpKite.FormsUpAgain(true, true), Is.False);
            Assert.That(PvpKite.FormsUpAgain(false, false), Is.False);
            Assert.That(Weights.NotGoneWithAPlayerWithin, Is.GreaterThan(WorldMgr.VISIBILITY_DISTANCE), "farther than a player sees a bot");
        }

        [Test]
        public void ACallThatMovesTheGroupIsLoud()
        {
            // "spam it in capital or something, 4-5 times, so I am aware"
            Assert.That(PvpCalls.LoudTimes, Is.InRange(4, 5));
            foreach (string call in new[] { PvpCalls.KiteCalled(12, 8), PvpCalls.KiteAgain, PvpCalls.KiteOver })
                Assert.That(call, Is.EqualTo(call.ToUpperInvariant()), call);
            Assert.That(PvpCalls.KiteCalled(12, 8), Does.Contain("12").And.Contain("8"));
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
