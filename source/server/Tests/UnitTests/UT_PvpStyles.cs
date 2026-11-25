using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// How each kind of team plays (docs/PVP-8V8-ROADMAP.md, M12.6). The owner, 2026-11-24: "all
    /// kinds should behave differently regarding Targeting, positioning, healing, crowd control,
    /// peeling and interrupting instead of sharing the same value".
    /// </summary>
    [TestFixture]
    public class UT_PvpStyles
    {
        private static readonly PvpWeights Weights = new();

        [Test]
        public void ADecentTeamPlaysByTheWeightsAsTheyAre()
        {
            Assert.That(PvpStyles.For(PvpTeamKind.Decent, Weights), Is.Null);
            // Every other kind has a set of its own, and making one changes nothing in the weights it was made from.
            foreach (PvpTeamKind kind in PvpTeamProfile.All.Select(profile => profile.Kind).Where(kind => kind != PvpTeamKind.Decent))
            {
                PvpWeights style = PvpStyles.For(kind, Weights);
                Assert.That(style, Is.Not.Null.And.Not.SameAs(Weights), kind.ToString());
                Assert.That(PvpStyles.For(kind, Weights), Is.SameAs(style), "made once");
            }
            Assert.That(Weights.Kill.Assists && Weights.Peel.Enabled && Weights.Order.Enabled, Is.True);
            Assert.That(Weights.Kill.CollapseOnCallSeconds, Is.EqualTo(0));
            Assert.That(Weights.ClassBase["Sorcerer"]["Mez"], Is.EqualTo(new PvpWeights().ClassBase["Sorcerer"]["Mez"]));
        }

        [Test]
        public void ABadGroupHasNoAssistNoPeelAndStandsInTheMelee()
        {
            PvpWeights bad = PvpStyles.For(PvpTeamKind.Bad, Weights);
            Assert.That(bad.Kill.Assists, Is.False, "each fighter takes the nearest enemy");
            Assert.That(bad.Peel.Enabled, Is.False, "its shield fighter is one more fighter");
            Assert.That(bad.Order.Enabled, Is.False, "nobody is called back");
            Assert.That(bad.Position.SafeDistance, Is.LessThan(Weights.Position.SafeDistance));
            Assert.That(bad.Position.EdgeDistance, Is.EqualTo(0));
            Assert.That(bad.Spread.MinSpacing, Is.EqualTo(0), "one clump");
            Assert.That(bad.Heal.HealBelowPercent, Is.LessThan(Weights.Heal.HealBelowPercent), "it heals late");
            Assert.That(bad.ClassBase["Sorcerer"]["Mez"], Is.LessThan(Weights.ClassBase["Sorcerer"]["Mez"]), "its mezzer mostly does damage");
            Assert.That(bad.ClassBase["Eldritch"]["Damage"], Is.GreaterThan(Weights.ClassBase["Eldritch"]["Damage"]));
            Assert.That(bad.TargetMezzed, Is.GreaterThan(Weights.TargetMezzed), "it wakes the sleeping");
            Assert.That(bad.Interrupt.MinScore, Is.GreaterThan(1_000), "nobody has interrupt duty");
        }

        [Test]
        public void TheKindsDifferFromEachOther()
        {
            PvpWeights clingy = PvpStyles.For(PvpTeamKind.Clingy, Weights), adder = PvpStyles.For(PvpTeamKind.Adder, Weights),
                strong = PvpStyles.For(PvpTeamKind.Strong, Weights), elite = PvpStyles.For(PvpTeamKind.Elite, Weights),
                hunter = PvpStyles.For(PvpTeamKind.Hunter, Weights);
            // The casual group near others calls the nearest enemy whatever its class; the one that adds, the most hurt.
            Assert.That((clingy.Kill.OnHealer, clingy.Kill.OnFighter), Is.EqualTo((100, 100)));
            Assert.That(adder.Kill.PerMissingHealthPercent, Is.GreaterThan(Weights.Kill.PerMissingHealthPercent));
            Assert.That(clingy.Kill.Assists && adder.Kill.Assists, Is.True, "they do assist");
            // They break off more readily than a decent team, and each as often as was said.
            Assert.That((clingy.Kite.BreaksOffChancePercent, adder.Kite.BreaksOffChancePercent, Weights.Kite.BreaksOffChancePercent), Is.EqualTo((60, 50, 35)));
            // The strong, the elite and the hunters collapse on a call and meet a fighter before it arrives.
            foreach (PvpWeights good in new[] { strong, elite, hunter })
            {
                Assert.That(good.Kill.CollapseOnCallSeconds, Is.GreaterThan(0));
                Assert.That(good.Peel.InterceptWithin, Is.GreaterThan(Weights.Peel.InterceptWithin));
                Assert.That(good.ControlOnEnemyController, Is.GreaterThan(Weights.ControlOnEnemyController));
            }
            Assert.That(elite.Kill.PerHundredUnitsFromItsHealers, Is.GreaterThan(strong.Kill.PerHundredUnitsFromItsHealers), "an elite team punishes whoever is out of position");
            Assert.That(hunter.Kill.MaxDistance, Is.LessThan(elite.Kill.MaxDistance), "the hunters do not go in deep");
            Assert.That(hunter.Kill.OnHealer, Is.LessThan(elite.Kill.OnHealer));
        }

        [Test]
        public void FiveLevelsOfPlay()
        {
            // "Should we also have the same for bad and elite?"
            PvpHumanWeights bad = PvpHumanWeights.For(PvpSkill.Bad), casual = PvpHumanWeights.For(PvpSkill.Casual), seasoned = new(),
                sharp = PvpHumanWeights.For(PvpSkill.Sharp), elite = PvpHumanWeights.For(PvpSkill.Elite);
            PvpHumanWeights[] levels = [bad, casual, seasoned, sharp, elite];
            for (int index = 1; index < levels.Length; index++)
            {
                Assert.That(levels[index].ReactionMaxMilliseconds, Is.LessThan(levels[index - 1].ReactionMaxMilliseconds), $"level {index}");
                Assert.That(levels[index].AssistSwitchMaxMilliseconds, Is.LessThan(levels[index - 1].AssistSwitchMaxMilliseconds), $"level {index}");
                Assert.That(levels[index].DistractedPercent, Is.LessThanOrEqualTo(levels[index - 1].DistractedPercent), $"level {index}");
                Assert.That(levels[index].ImmunityEndNoticedAfterMaxSeconds, Is.LessThan(levels[index - 1].ImmunityEndNoticedAfterMaxSeconds), $"level {index}");
            }
            Assert.That(PvpSkills.Parse("bad"), Is.EqualTo(PvpSkill.Bad));
            Assert.That(PvpSkills.Parse("elite"), Is.EqualTo(PvpSkill.Elite));
            // Each kind has its level: the two ends their own, and the casual group that adds no longer that of a decent team.
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Bad).Skill, Is.EqualTo(PvpSkill.Bad));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).Skill, Is.EqualTo(PvpSkill.Casual));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Adder).Skill, Is.EqualTo(PvpSkill.Casual));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Decent).Skill, Is.EqualTo(PvpSkill.Seasoned));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Strong).Skill, Is.EqualTo(PvpSkill.Sharp));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Elite).Skill, Is.EqualTo(PvpSkill.Elite));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Hunter).Skill, Is.EqualTo(PvpSkill.Elite));
        }

        [Test]
        public void AnEliteTeamLeavesAFightThatIsNoLongerClean()
        {
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Elite).LeavesDirtyFights, Is.True);
            Assert.That(PvpTeamProfile.All.Count(profile => profile.LeavesDirtyFights), Is.EqualTo(1), "not the hunters, who look for the crowd");
            Assert.That(PvpKite.FightIsDirty(1), Is.False);
            Assert.That(PvpKite.FightIsDirty(2), Is.True);
        }
    }
}
