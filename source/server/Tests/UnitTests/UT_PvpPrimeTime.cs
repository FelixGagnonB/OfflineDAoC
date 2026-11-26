using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// A prime-time evening (docs/PVP-8V8-ROADMAP.md, M11): the owner, 2026-11-22, on the teams
    /// that were out in the early 2000s, their kinds and a ratio between them.
    /// </summary>
    [TestFixture]
    public class UT_PvpPrimeTime
    {
        [Test]
        public void ABotOnGroundItCannotLeaveIsFreedAndAColdFightIsOver()
        {
            // The owner, 2026-11-25: "2 druids are stuck in a tree and my alb team still consider being in fight".
            // Asked to walk 600 units eight ways, the two got 0 to 90: trapped. One that gets somewhere one way is not.
            Assert.That(PvpTrapped.IsTrapped([0, 90, 53, 74, 0, 74, 0, 12]), Is.True);
            Assert.That(PvpTrapped.IsTrapped([0, 90, 53, 600, 0, 74, 0, 12]), Is.False);
            Assert.That(PvpTrapped.IsTrapped([]), Is.False, "nothing asked, nothing said");
            Assert.That(PvpTrapped.IsNear(48), Is.True, "the way in ended 29 to 48 units short of them");
            Assert.That(PvpTrapped.IsNear(PvpTrapped.WayOutWithin + 1), Is.False, "a bot is not carried across a zone");
            // A fight in which nobody has been in combat for half a minute is over, whoever still stands near.
            // "It's the time we were standing before moving away from the fighting spot, at the end of the fight": with
            // nobody left to fight a fight is over in three seconds, not twelve.
            Assert.That(PvpRoam.NobodyLeftToFight(picksItsFightsAsBotsDo: true, enemiesInSight: 0, groupsInItsFight: 0, millisecondsWithNoEnemy: 3_500), Is.True);
            Assert.That(PvpRoam.NobodyLeftToFight(true, 0, 0, 2_000), Is.False);
            Assert.That(PvpRoam.NobodyLeftToFight(true, 2, 0, 9_000), Is.False, "an enemy is still there");
            Assert.That(PvpRoam.NobodyLeftToFight(true, 0, 1, 9_000), Is.False, "a group is still in the fight, out of sight for a moment");
            Assert.That(PvpRoam.NobodyLeftToFight(false, 0, 0, 9_000), Is.False, "with the player at the wheel a lull is not the end");
            Assert.That(PvpRoam.HasGoneCold(31_000), Is.True);
            Assert.That(PvpRoam.HasGoneCold(12_000), Is.False);
            Assert.That(PvpRoam.ColdAfterSeconds * 1_000, Is.GreaterThan(PvpRoam.FightOverAfterMilliseconds));
        }

        [Test]
        public void AHillHidesACastersTarget()
        {
            // The owner, 2026-11-25: "Eld killed a warrior by casting through a hill".
            // Two at the same height, eyes at 60: flat ground hides nobody, a rise of 60 neither, a hill of 200 does.
            Assert.That(PvpSight.HiddenAt([0f, 0f, 0f], 0, 0, PvpSight.CastTolerance), Is.EqualTo(-1));
            Assert.That(PvpSight.HiddenAt([30f, 60f, 30f], 0, 0, PvpSight.CastTolerance), Is.EqualTo(-1));
            Assert.That(PvpSight.HiddenAt([100f, 200f, 100f], 0, 0, PvpSight.CastTolerance), Is.EqualTo(0));
            Assert.That(PvpSight.HiddenAt([85f, 200f, 85f], 0, 0, PvpSight.CastTolerance), Is.EqualTo(1), "just above the line is not hidden for a cast");
            Assert.That(PvpSight.HiddenAt([100f, 200f, 100f], 0, 0, PvpSight.CastTolerance, start: 1), Is.EqualTo(1), "the next place, for one that turned out to be a bridge");
            Assert.That(PvpSight.HiddenAt([100f, 200f, 100f], 0, 0, PvpSight.CastTolerance, start: 3), Is.EqualTo(-1));
            // One on the hill looks down on one at its foot: the slope between them is under the line.
            Assert.That(PvpSight.HiddenAt([200f, 100f, 0f], 300, 0, PvpSight.CastTolerance), Is.EqualTo(-1));
            // Where the navmesh has no ground (water, a slope too steep to walk) nothing is said: its own ray has stopped there.
            Assert.That(PvpSight.HiddenAt([float.NaN, float.NaN], 0, 0, PvpSight.CastTolerance), Is.EqualTo(-1));
            Assert.That(PvpSight.LineAt(100, 200, 0, 1), Is.EqualTo(150));
            Assert.That(PvpSight.ClearForACast(null, null, null), Is.True);
        }

        [Test]
        public void AGroupWithNoAssistDoesNotStandBeforeSleepers()
        {
            // The owner, 2026-11-25: "we were 5 out of 5 mezzed. The mid team just stayed idle waiting for mezz to wear off
            // instead of picking a target and killing it".
            Assert.That(PvpKillTarget.TakesTheNearest(itsGroupAssists: false, hasATargetOfItsOwn: false), Is.True);
            Assert.That(PvpKillTarget.TakesTheNearest(false, true), Is.False, "one that somebody has made angry is on that one, as a bad group is");
            Assert.That(PvpKillTarget.TakesTheNearest(true, false), Is.False, "a group that assists has its kill target");
            // An awake enemy before a sleeping one, the nearer first; the one taken before is kept; none out of reach.
            Assert.That(PvpKillTarget.Nearest([(900, true), (1_400, false), (2_000, false)], -1, 3_500), Is.EqualTo(1));
            Assert.That(PvpKillTarget.Nearest([(900, true), (400, true), (600, true)], -1, 3_500), Is.EqualTo(1), "all asleep: the nearest sleeper");
            Assert.That(PvpKillTarget.Nearest([(900, true), (400, true), (600, false)], 0, 3_500), Is.EqualTo(0), "the one it has");
            Assert.That(PvpKillTarget.Nearest([(4_000, false)], -1, 3_500), Is.EqualTo(-1));
            Assert.That(PvpKillTarget.Nearest([], -1, 3_500), Is.EqualTo(-1));
            // A caster's spell is not marked down for a sleeping target when everybody is asleep.
            Assert.That(PvpActionChoice.SleepCounts(targetIsAsleep: true, spellIsAMez: false, everyEnemyIsAsleep: false), Is.True);
            Assert.That(PvpActionChoice.SleepCounts(true, false, true), Is.False);
            Assert.That(PvpActionChoice.SleepCounts(true, true, false), Is.False);
            Assert.That(PvpActionChoice.SleepCounts(false, false, false), Is.False);
        }

        [Test]
        public void TheEightZoneIsForOneGroupAgainstOne()
        {
            // The owner, 2026-11-25: "Could we build a zone for dedicated 8v8 without adds? ... use an unuse zone and make it a
            // dedicated 8v8 only zone"; who is in it: "Every kind, no adds"; a group that has just fought: "Could be a mix of 1
            // and 2, based on what kind of group is waiting".
            Assert.That(PvpRoam.EightZone, Is.SameAs(PvpRoam.Thidranki), "the one ground no evening used");
            Assert.That(PvpRoam.Frontiers, Does.Not.Contain(PvpRoam.Thidranki));
            // Agramon, the island no realm holds: ground for the 8v8 zone when the settings name it, and no zone of an evening otherwise.
            Assert.That(PvpRoam.MapFor("agramon"), Is.SameAs(PvpRoamZones.Agramon));
            Assert.That(PvpRoam.Frontiers, Does.Not.Contain(PvpRoamZones.Agramon));
            Assert.That(PvpRoamZones.Agramon.Starts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
            // "it looks like we are running the same triangle ... Could we add more routes ?": ten points, and from each a way on.
            Assert.That(PvpRoamZones.Agramon.Places, Has.Count.EqualTo(10));
            Assert.That(PvpRoamZones.Agramon.Ways, Has.Count.EqualTo(17));
            foreach (((int X, int Y) point, string name) in PvpRoamZones.Agramon.Places)
                Assert.That(PvpRoamZones.Agramon.From(point), Is.Not.Empty, name);
            Assert.That(PvpRoamZones.Agramon.Places.Select(place => place.Name), Is.Unique);
            Assert.That(PvpRoam.EveningZones(PvpRoam.Emain, 12, new Random(1), PvpRoamZones.Agramon)[^1], Is.SameAs(PvpRoamZones.Agramon));
            Assert.That(PvpRoam.CanBeWalked(null), Is.False);
            // An evening is in its frontier zones as ever, and in the 8v8 zone.
            for (int seed = 1; seed <= 20; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.EveningZones(PvpRoam.Emain, 12, new Random(seed), PvpRoam.Thidranki);
                Assert.That(zones[0], Is.SameAs(PvpRoam.Emain));
                Assert.That(zones[^1], Is.SameAs(PvpRoam.Thidranki));
                Assert.That(zones, Has.Count.EqualTo(4));
                Assert.That(zones, Is.Unique);
                // A player that comes in standing in the 8v8 zone: the evening is in the frontier all the same.
                List<PvpRoamMap> from = PvpRoam.EveningZones(PvpRoam.Thidranki, 8, new Random(seed), PvpRoam.Thidranki);
                Assert.That(from[0], Is.SameAs(PvpRoam.Emain));
                Assert.That(from.Count(zone => zone == PvpRoam.Thidranki), Is.EqualTo(1));
                // Another zone named for it is not drawn as an ordinary zone as well.
                List<PvpRoamMap> other = PvpRoam.EveningZones(PvpRoam.Emain, 40, new Random(seed), PvpRoamZones.MountCollory);
                Assert.That(other.Count(zone => zone == PvpRoamZones.MountCollory), Is.EqualTo(1));
                Assert.That(other[^1], Is.SameAs(PvpRoamZones.MountCollory));
            }
            Assert.That(PvpRoam.EveningZones(PvpRoam.Emain, 2, new Random(1), PvpRoam.Thidranki), Does.Not.Contain(PvpRoam.Thidranki), "too few groups to spare any");
            Assert.That(PvpRoam.EveningZones(PvpRoam.Emain, 8, new Random(1), null), Does.Not.Contain(PvpRoam.Thidranki), "no 8v8 zone named");
            // A third of a realm's guilds, two at the most as it is set, one at the least.
            Assert.That(PvpRoam.EightZoneGroups(8, 2), Is.EqualTo(2));
            Assert.That(PvpRoam.EightZoneGroups(7, 2), Is.EqualTo(2));
            Assert.That(PvpRoam.EightZoneGroups(4, 2), Is.EqualTo(1));
            Assert.That(PvpRoam.EightZoneGroups(2, 2), Is.EqualTo(1));
            Assert.That(PvpRoam.EightZoneGroups(12, 4), Is.EqualTo(4));
            Assert.That(PvpRoam.EightZoneGroups(0, 2), Is.Zero);
            // The kinds that play 8v8 leave a group to rest after its fight; a bad or a casual group takes it as it finds it.
            foreach (PvpTeamKind kind in new[] { PvpTeamKind.Decent, PvpTeamKind.Strong, PvpTeamKind.Elite })
                Assert.That(PvpRoam.WaitsForARestedGroup(kind), Is.True, kind.ToString());
            foreach (PvpTeamKind kind in new[] { PvpTeamKind.Bad, PvpTeamKind.Clingy, PvpTeamKind.Adder, PvpTeamKind.Hunter })
                Assert.That(PvpRoam.WaitsForARestedGroup(kind), Is.False, kind.ToString());
            Assert.That(PvpRoam.IsWhole(dead: 0, lowestHealthPercent: 100, lowestPowerPercent: 100), Is.True);
            Assert.That(PvpRoam.IsWhole(1, 100, 100), Is.False);
            Assert.That(PvpRoam.IsWhole(0, 84, 100), Is.False);
            Assert.That(PvpRoam.IsWhole(0, 90, 59), Is.False);
            Assert.That(PvpRoam.IsRecovering(inAFight: false, itsFightHasJustEnded: true, secondsSinceItsLastFight: -1, whole: false, leftForSeconds: 120), Is.True);
            Assert.That(PvpRoam.IsRecovering(false, false, 60, false, 120), Is.True);
            Assert.That(PvpRoam.IsRecovering(false, false, 60, true, 120), Is.False, "whole again");
            Assert.That(PvpRoam.IsRecovering(false, false, 120, false, 120), Is.False, "not left alone for ever");
            Assert.That(PvpRoam.IsRecovering(true, false, 60, false, 120), Is.False, "in a fight, it is in a fight");
            Assert.That(PvpRoam.IsRecovering(false, false, -1, false, 120), Is.False, "it has had no fight");
            Assert.That(PvpRoam.HoldsOff(waitsForARestedGroup: true, itIsRecovering: false, theOtherIsRecovering: true), Is.True);
            Assert.That(PvpRoam.HoldsOff(true, true, false), Is.True, "nor does it start one hurt itself");
            Assert.That(PvpRoam.HoldsOff(true, false, false), Is.False);
            Assert.That(PvpRoam.HoldsOff(false, true, true), Is.False);
            // One group against one: a group is picked by one group only, and picks one only.
            Assert.That(PvpRoam.MayPick(itIsPickedByAThird: false, itHasPickedAThird: false, weArePickedByAThird: false, weHavePickedAThird: false), Is.True);
            Assert.That(PvpRoam.MayPick(true, false, false, false), Is.False);
            Assert.That(PvpRoam.MayPick(false, true, false, false), Is.False);
            Assert.That(PvpRoam.MayPick(false, false, true, false), Is.False);
            Assert.That(PvpRoam.MayPick(false, false, false, true), Is.False);
            Assert.That(PvpRoam.ZonesLine([PvpRoam.Emain, PvpRoam.Thidranki], PvpRoam.Emain, PvpRoam.Thidranki), Does.Contain("Thidranki (8v8 only, nobody adds)"));
            // "I feel like we had 2 fg mids": two fights of one against one, begun a second apart and 600 units from each other.
            Assert.That(PvpRoam.StartsBesideAFight(aBusyGroupIsNearUs: false, aBusyGroupIsNearIt: true), Is.True);
            Assert.That(PvpRoam.StartsBesideAFight(true, false), Is.True);
            Assert.That(PvpRoam.StartsBesideAFight(false, false), Is.False);
            Assert.That(PvpRoam.OneFightWithin, Is.GreaterThanOrEqualTo(PvpRoam.GoesRoundAFightAt), "as far off as a group goes round a fight");
        }

        [Test]
        public void EveryGuildWearsAnEmblemOfItsOwn()
        {
            // The owner, 2026-11-25: "We all wear the default all green, red, blue. It would be nice if each guild has it's own
            // embleem and using it".
            Assert.That(PvpGuildEmblem.Pack(logo: 5, pattern: 2, primaryColor: 9, secondaryColor: 3), Is.EqualTo((5 << 9) | (2 << 7) | (9 << 3) | 3));
            int raiders = PvpGuildEmblem.Made("Vindsaul Raiders");
            Assert.That(raiders, Is.Not.Zero);
            Assert.That(PvpGuildEmblem.Made("vindsaul raiders "), Is.EqualTo(raiders), "the same every evening, however the name is written");
            Assert.That(PvpGuildEmblem.Made(""), Is.Zero);
            // Of the guilds of an evening hardly any two wear the same, the logo is one the client draws, and the colours differ.
            List<string> names = PvpTeamPool.Generate(16, new Random(3)).Select(team => team.Guild).ToList();
            List<int> emblems = names.Select(PvpGuildEmblem.Made).ToList();
            Assert.That(emblems.Distinct().Count(), Is.GreaterThanOrEqualTo(names.Count - 1));
            foreach (int emblem in emblems)
            {
                Assert.That(emblem >> 9, Is.InRange(1, 90));
                Assert.That(emblem, Is.LessThan(0x10000), "it fits the two bytes the client is sent");
                Assert.That((emblem >> 3) & 7, Is.Not.EqualTo(emblem & 7));
            }
            // A player's guild wears what its player picked; until it has, one made from its name.
            Assert.That(PvpGuildEmblem.Worn("Stags of Emain", 4_321), Is.EqualTo(4_321));
            Assert.That(PvpGuildEmblem.Worn("Stags of Emain", 0), Is.EqualTo(PvpGuildEmblem.Made("Stags of Emain")));
            Assert.That(PvpGuildEmblem.Worn(null, 4_321), Is.Zero, "a bot with no guild wears none");
            Assert.That(PvpGuildEmblem.CannotAsk(hasAGuild: true, isItsGuildmaster: true), Is.Null);
            Assert.That(PvpGuildEmblem.CannotAsk(false, false), Does.Contain("/8v8 guild <name>"));
            Assert.That(PvpGuildEmblem.CannotAsk(true, false), Does.Contain("guildmaster"));
        }

        [Test]
        public void TheEveningBeginsWhenThePlayerComesIn()
        {
            // The owner, 2026-11-24: "Yes, I would do it as I log in".
            Assert.That(new PvpRealWeights().EveningAtLogin, Is.True);
            Assert.That(PvpEveningStart.AtLogin(beginsAtLogin: true, hasAn8v8Character: true, somethingRuns: false, itIsThePlayersOwn: false), Is.EqualTo((true, (string)null)));
            // A new character is told what to type; "/8v8 ready" is typed by hand ("I would type it").
            (bool begins, string told) = PvpEveningStart.AtLogin(true, false, false, false);
            Assert.That(begins, Is.False);
            Assert.That(told, Does.Contain("/8v8 ready"));
            // An evening of bots is running: the player's waits.
            (begins, told) = PvpEveningStart.AtLogin(true, true, true, false);
            Assert.That(begins, Is.False);
            Assert.That(told, Does.Contain("running on this server already"));
            // The player's own evening runs (it has just made its character in it): nothing to do and nothing to say.
            Assert.That(PvpEveningStart.AtLogin(true, true, true, true), Is.EqualTo((false, (string)null)));
            Assert.That(PvpEveningStart.AtLogin(false, true, false, false), Is.EqualTo((false, (string)null)));
            // Only the login begins it: the game tells of a player "entering" after every change of region too (a walk to the
            // trainer in the capital would have ended with the player taken back to the frontier).
            Assert.That(PvpEveningStart.IsLogin(enteredBeforeThisSession: false), Is.True);
            Assert.That(PvpEveningStart.IsLogin(true), Is.False);
            Assert.That(PvpEveningStart.IsUnderWay(now: 20_000, begunAt: 10_000), Is.True, "the first start is still taking the player there");
            Assert.That(PvpEveningStart.IsUnderWay(50_000, 10_000), Is.False);
            Assert.That(PvpEveningStart.IsUnderWay(20_000, 0), Is.False, "none was begun");
            // The evening is its player's session, not its player's place: "Regardless if I leave a frontier zone, to go to the capital
            // city for example, evening should keep goigng" (the owner, 2026-11-25).
            Assert.That(PvpRoam.HasLeftTheGame(characterIsDeleted: false, clientIsPlaying: true), Is.False, "in the capital, or between two regions");
            Assert.That(PvpRoam.HasLeftTheGame(true, true), Is.True);
            Assert.That(PvpRoam.HasLeftTheGame(false, false), Is.True, "at the character screen");
            // Its seven wait off the field while it is out of the frontier, and are beside it again when it is back.
            Assert.That(PvpRoam.PresenceOf(playerIsInTheWorld: true, onFrontierGround: false, absent: false), Is.EqualTo(PvpRoam.Presence.Leaves));
            Assert.That(PvpRoam.PresenceOf(true, false, true), Is.EqualTo(PvpRoam.Presence.Stays));
            Assert.That(PvpRoam.PresenceOf(true, true, true), Is.EqualTo(PvpRoam.Presence.Returns));
            Assert.That(PvpRoam.PresenceOf(true, true, false), Is.EqualTo(PvpRoam.Presence.Stays));
            Assert.That(PvpRoam.PresenceOf(false, false, false), Is.EqualTo(PvpRoam.Presence.Stays), "while its client loads a zone nothing is decided");
            Assert.That(PvpRoam.PresenceOf(false, true, true), Is.EqualTo(PvpRoam.Presence.Stays));
            // "When the bot are done buffing, after we release, we are 'low' on hp ... We have to wait at the keep for full hp."
            Assert.That(PvpRoam.TopsUp(millisecondsSinceItStoodUp: 20_000, hasMetAnEnemy: false, memberIsInCombat: false), Is.True);
            Assert.That(PvpRoam.TopsUp(20_000, true, false), Is.False, "not once it has met somebody");
            Assert.That(PvpRoam.TopsUp(20_000, false, true), Is.False, "nor a member that is being hit");
            Assert.That(PvpRoam.TopsUp(PvpRoam.WholeAfterStandingUpSeconds * 1_000L, false, false), Is.False, "its first minute only");
            Assert.That(PvpEveningStart.Is8v8Character(50, true), Is.True);
            Assert.That(PvpEveningStart.Is8v8Character(49, true), Is.False);
            Assert.That(PvpEveningStart.Is8v8Character(50, false), Is.False);

            // "I do want everyone to start at realm rank 1 level 1 and grind it out. There could already be group profile".
            Assert.That(new PvpRealWeights().GuildsBeginAtTheBottom, Is.True);
            List<PvpGuildTeam> release = PvpTeamPool.Generate(20, new Random(5), null, fromTheBottom: true);
            Assert.That(release.Select(team => team.SeedLevel), Is.All.EqualTo(1));
            Assert.That(PvpRealmPoints.RankName(1), Is.EqualTo("1L1"));
            Assert.That(release.Select(team => team.Kind).Distinct().Count(), Is.GreaterThanOrEqualTo(6), "and every kind is there from the first evening");
            Assert.That(PvpTeamPool.Generate(20, new Random(5)).Max(team => team.SeedLevel), Is.GreaterThan(60), "an evening of bots still has an elite team at its rank");

            // What a player keeps from one evening to the next.
            var kept = new PvpPlayerSetting();
            Assert.That((kept.Lead, kept.Lineup, kept.Zone, kept.Groups), Is.EqualTo((true, (string)null, (string)null, 0)));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.LineupAnswer(null, false, "hib-main", ["hib-main (a, b)", "hib-2bard (c, d)"]),
                Is.EqualTo("Your group's line-up is hib-main. /8v8 guild lineup <name>: hib-main (a, b); hib-2bard (c, d)."));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.LineupAnswer("hib-2bard", true, "hib-2bard", []), Does.Contain("from your next evening on"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.LineupAnswer("alb-main", false, "hib-main", []), Does.Contain("is not a line-up of your realm"));
        }

        [Test]
        public void WhoSaysWhichZoneHasWhatKindOfGroupAndHowMany()
        {
            // The owner, 2026-11-24: "the idea would be to know which zone has what kind of group and how many groups."
            Assert.That(PvpRoam.WhoLine("Emain Macha", true,
                [
                    (eRealm.Albion, "bad", false), (eRealm.Albion, "bad", false), (eRealm.Albion, "decent", false),
                    (eRealm.Midgard, "casual", false), (eRealm.Hibernia, "elite", false), (eRealm.Hibernia, "yours", true),
                ]),
                Is.EqualTo("Emain Macha (you are here): Albion 3: 2 bad, 1 decent. Midgard 1: 1 casual. Hibernia 2: yours, 1 elite."));
            Assert.That(PvpRoam.WhoLine("Odin's Gate", false, []), Is.EqualTo("Odin's Gate: nobody."));
            Assert.That(PvpRoam.KindWord(PvpTeamKind.Hunter), Is.EqualTo("zerg hunters"));
            Assert.That(PvpRoam.KindWord(null), Is.EqualTo("other"));
            // What a player has of the command on the play install, and what is kept for a test server or a game master.
            foreach (string word in new[] { "call", "calls", "lead", "ready", "guild", "evening", "who", "rivals", "ranks", "go", "corpse" })
                Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.IsKeptFromAPlayer(word, false, false), Is.False, word);
            // ("stop" is for trying things: the owner, 2026-11-25, "I want it on scrim, it makes testing easy, but not on the real server".)
            foreach (string word in new[] { "prime", "roam", "play", "setup", "group", "skill", "status", "stop", "clear", "lanes", "watch", "feed", "" })
            {
                Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.IsKeptFromAPlayer(word, false, false), Is.True, word);
                Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.IsKeptFromAPlayer(word, true, false), Is.False, word + " on a test server");
                Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.IsKeptFromAPlayer(word, false, true), Is.False, word + " for a game master");
            }
        }

        [Test]
        public void InAFightAClickPicksNobody()
        {
            // The owner, 2026-11-24: "say we are in a fight and I click on a member of an idle team, does that trigger them to add on the fight?"
            Assert.That(PvpTeamPool.PackMost, Is.EqualTo(3), "and the packs are of two to three");
            // Out of a fight the player's target is the group its own takes on, as before.
            Assert.That(PvpRoam.PlayerPicks(targetIsOfThatGroup: true, groupFightsSomebodyElse: false, playerHasStruckIt: false, playerCalledOnIt: false), Is.True);
            // In a fight with somebody else a target alone is a look.
            Assert.That(PvpRoam.PlayerPicks(true, true, false, false), Is.False);
            // A blow, or a call on it, takes that group on.
            Assert.That(PvpRoam.PlayerPicks(true, true, true, false), Is.True);
            Assert.That(PvpRoam.PlayerPicks(false, true, false, true), Is.True, "the call stands though the player has another target now");
            Assert.That(PvpRoam.PlayerPicks(false, false, false, false), Is.False);
        }

        [Test]
        public void ATeamThatWalksOnPastAFightGoesRoundIt()
        {
            // The owner, 2026-11-24: "I'd go with making the group go around, if they decide not to add, instead of walking in middle."
            // Its road runs east, straight through a fight 8,000 ahead: it goes to a point beside the fight, 4,500 from its middle.
            int side = 0;
            (int X, int Y)? round = PvpRoam.AroundAFight((0, 0), (16_000, 0), (8_000, 0), ref side);
            Assert.That(round, Is.Not.Null);
            Assert.That(round.Value.X, Is.EqualTo(8_000));
            Assert.That(Math.Abs(round.Value.Y), Is.EqualTo(PvpRoam.GoesRoundAFightAt));
            Assert.That(side, Is.Not.Zero, "and it keeps to that side");
            // Its road leans north of the fight already: it goes round by the north (less Y), not across to the south.
            side = 0;
            round = PvpRoam.AroundAFight((0, -1_000), (16_000, -1_000), (8_000, 0), ref side);
            Assert.That(round.Value.Y, Is.EqualTo(-PvpRoam.GoesRoundAFightAt));
            // Half way round, the side is kept though the fight is now on the other hand of the line it walks.
            int kept = side;
            round = PvpRoam.AroundAFight((7_000, -4_400), (16_000, -1_000), (8_000, 0), ref side);
            Assert.That(side, Is.EqualTo(kept));
            // Beside the fight and past it, the rest of its road is clear: it walks its road.
            Assert.That(PvpRoam.AroundAFight((9_500, -4_500), (16_000, -1_000), (8_000, 0), ref side), Is.Null);
            // A road that does not come near the fight is walked as it is.
            side = 0;
            Assert.That(PvpRoam.AroundAFight((0, 0), (16_000, 0), (8_000, 6_000), ref side), Is.Null);
            Assert.That(PvpRoam.AroundAFight((0, 0), (0, 0), (100, 0), ref side), Is.Null);
            // The point it walks to is in the fight: it goes as near to it as it may, on its own side of the fight, and stands there.
            side = 0;
            round = PvpRoam.AroundAFight((0, 0), (7_000, 0), (8_000, 0), ref side);
            Assert.That(round, Is.EqualTo((8_000 - PvpRoam.GoesRoundAFightAt, 0)));
            Assert.That(PvpRoam.AroundAFight((3_600, 0), (7_000, 0), (8_000, 0), ref side), Is.Null, "it is there");
            // Wherever it is sent, the point is never nearer to the fight than it goes round at.
            var random = new Random(3);
            for (int i = 0; i < 500; i++)
            {
                side = 0;
                (double X, double Y) from = (random.Next(-12_000, 12_000), random.Next(-12_000, 12_000)), to = (random.Next(-12_000, 12_000), random.Next(-12_000, 12_000));
                if (PvpRoam.AroundAFight(from, to, (0, 0), ref side) is { } point)
                    Assert.That(Math.Sqrt((double)point.X * point.X + (double)point.Y * point.Y), Is.GreaterThan(PvpRoam.GoesRoundAFightAt - 2));
            }
        }

        [Test]
        public void ThePlayerFormsAGuildForItsGroup()
        {
            // The owner, 2026-11-24: "How can I form a guild with my 7 bots?"
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("Stags of Emain", "", true, false, false), Is.Null);
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("", "", false, false, false), Does.Contain("/8v8 guild <name>"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("Another", "Stags of Emain", true, false, false), Does.Contain("Stags of Emain already"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem(new string('a', 31), "", true, false, false), Does.Contain("too long"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("Stags 2", "", false, false, false), Does.Contain("letters and spaces"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("Stags", "", true, true, false), Does.Contain("already"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.GuildProblem("Vindsaul Raiders", "", true, false, true), Does.Contain("guilds of bots"));
            // On a prime-time evening its group goes by the guild's name, unless a guild of bots that is out has the same.
            Assert.That(PvpRoam.OwnTeamName("Stags of Emain", "hib-main", ["Vindsaul Raiders"]), Is.EqualTo("Stags of Emain"));
            Assert.That(PvpRoam.OwnTeamName("", "hib-main", ["Vindsaul Raiders"]), Is.EqualTo("hib-main"));
            Assert.That(PvpRoam.OwnTeamName("vindsaul raiders", "hib-main", ["Vindsaul Raiders"]), Is.EqualTo("hib-main"));
        }

        [Test]
        public void TheKindsAreInTheOwnersRatio()
        {
            // First "like 5% elite, 15% strong, 20% decent, 30% casual and 30% bad, or something like that". Since 2026-11-25,
            // after the owner's "mostly bad groups running 2fg or more. I have yet to find a clean fight": half and more of
            // the groups are of the kinds that fight one group against one.
            Assert.That(PvpTeamProfile.All.Sum(profile => profile.SharePercent), Is.EqualTo(100));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Elite).SharePercent, Is.EqualTo(10));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Strong).SharePercent, Is.EqualTo(20));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Decent).SharePercent, Is.EqualTo(25));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Hunter).SharePercent, Is.EqualTo(5));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).SharePercent + PvpTeamProfile.Of(PvpTeamKind.Adder).SharePercent, Is.EqualTo(20), "the two casual kinds");
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Bad).SharePercent, Is.EqualTo(20));
            Assert.That(PvpTeamProfile.All.Where(profile => profile.AddChancePercent == 0 && profile.Company == PvpCompany.Alone).Sum(profile => profile.SharePercent),
                Is.GreaterThanOrEqualTo(55), "those that neither add nor walk with anybody");

            Dictionary<PvpTeamKind, int> twenty = PvpTeamPool.Quotas(20);
            Assert.That(twenty[PvpTeamKind.Bad], Is.EqualTo(4));
            Assert.That(twenty[PvpTeamKind.Decent], Is.EqualTo(5));
            Assert.That(twenty[PvpTeamKind.Hunter], Is.EqualTo(1));
            Assert.That(twenty[PvpTeamKind.Strong], Is.EqualTo(4));
            Assert.That(twenty[PvpTeamKind.Elite], Is.EqualTo(2));
            foreach (int count in new[] { 1, 3, 4, 7, 8, 12, 16, 26 })
            {
                Assert.That(PvpTeamPool.Quotas(count).Values.Sum(), Is.EqualTo(count), count.ToString());
                Assert.That(PvpTeamPool.Quotas(count, new Random(count)).Values.Sum(), Is.EqualTo(count), "drawn: " + count);
            }
            // Among eight there is an elite team most evenings, and not every one.
            int withElite = Enumerable.Range(0, 400).Count(seed => PvpTeamPool.Quotas(8, new Random(seed))[PvpTeamKind.Elite] > 0);
            Assert.That(withElite, Is.InRange(200, 395));
        }

        [Test]
        public void WhatEachKindIsLike()
        {
            // "Bad group (those groups running with more than 1 other group ... sub optimal composition)"
            PvpTeamProfile bad = PvpTeamProfile.Of(PvpTeamKind.Bad);
            Assert.That(bad.Company, Is.EqualTo(PvpCompany.Zerg));
            Assert.That(bad.Lineup, Is.EqualTo(PvpLineup.Random));
            Assert.That(bad.CountsHeads, Is.False);
            // "Casual that were roaming but staying close to other groups"
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).Company, Is.EqualTo(PvpCompany.Near));
            // "Casual 8v8 that would not hesitate to add and collect rps"
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Adder).AddChancePercent, Is.GreaterThanOrEqualTo(75));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Adder).Company, Is.EqualTo(PvpCompany.Alone));
            // "Decent 8v8 teams that would not add", "Strong 8v8 teams", "Elite 8v8 teams ... to avoid not clean fights"
            foreach (PvpTeamKind kind in new[] { PvpTeamKind.Decent, PvpTeamKind.Strong, PvpTeamKind.Elite })
            {
                Assert.That(PvpTeamProfile.Of(kind).AddChancePercent, Is.Zero, kind.ToString());
                Assert.That(PvpTeamProfile.Of(kind).Lineup, Is.EqualTo(PvpLineup.Meta), kind.ToString());
                Assert.That(PvpTeamProfile.Of(kind).CountsHeads, Is.True, kind.ToString());
            }
            Assert.That(PvpTeamProfile.All.Where(profile => profile.KeepsToQuietGround).Select(profile => profile.Kind), Is.EqualTo(new[] { PvpTeamKind.Elite }));
            // "There was 8v8 elite team that would run to these 'zerg' (many teams running together) to kill them"
            PvpTeamProfile hunter = PvpTeamProfile.Of(PvpTeamKind.Hunter);
            PvpTeamProfile elite = PvpTeamProfile.Of(PvpTeamKind.Elite);
            Assert.That(PvpTeamProfile.All.Where(profile => profile.HuntsPacks).Select(profile => profile.Kind), Is.EqualTo(new[] { PvpTeamKind.Hunter }));
            Assert.That(hunter.CountsHeads, Is.False, "eight walk into twenty-four");
            Assert.That((hunter.Company, hunter.AddChancePercent, hunter.Lineup), Is.EqualTo((PvpCompany.Alone, 0, PvpLineup.Meta)));
            Assert.That((hunter.MinRealmLevel, hunter.MaxRealmLevel, hunter.Skill), Is.EqualTo((elite.MinRealmLevel, elite.MaxRealmLevel, elite.Skill)), "an elite team");
            // "If my group gets to the zerg 1st, the bot elite team won't add if they get there 2nd and 3rd right ?"
            Assert.That(PvpRoam.Joins(hunter.AddChancePercent, 0), Is.False, "it does not add, on a pack no more than on anybody");
            // It is where the crowd is, and not in a clean zone.
            Assert.That(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Crowded), Is.GreaterThan(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Mixed)));
            Assert.That(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Clean), Is.Zero);
            // The better the kind, the higher it begins and the sharper it plays; never above what a rank can be.
            for (int index = 1; index < PvpTeamProfile.All.Count; index++)
            {
                Assert.That(PvpTeamProfile.All[index].MinRealmLevel, Is.GreaterThanOrEqualTo(PvpTeamProfile.All[index - 1].MinRealmLevel));
                Assert.That(PvpTeamProfile.All[index].Skill, Is.GreaterThanOrEqualTo(PvpTeamProfile.All[index - 1].Skill));
            }
            Assert.That(PvpTeamProfile.All.Max(profile => profile.MaxRealmLevel), Is.LessThan(GamePlayer.REALMPOINTS_FOR_LEVEL.Length));
            Assert.That(PvpTeamProfile.Of("no such kind").Kind, Is.EqualTo(PvpTeamKind.Decent));
        }

        [Test]
        public void ARealmLevelAndItsRealmPoints()
        {
            foreach (int level in new[] { 1, 10, 29, 43, 70, 95 })
                Assert.That(PvpRealmPoints.LevelFor(PvpRealmPoints.PointsFor(level)), Is.EqualTo(level), level.ToString());
            Assert.That(PvpRealmPoints.PointsFor(-3), Is.Zero);
        }

        [Test]
        public void ATeamIsEightBuildsOfItsRealm()
        {
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<string> main = PvpBuildCatalog.Groups.First(group => group.Realm == realm).BuildIds.ToList();
                Assert.That(PvpTeamPool.Compose(realm, PvpLineup.Meta, new Random(1)), Is.EqualTo(main));
                HashSet<string> ofRealm = PvpTeamPool.BuildsOf(realm).Select(build => build.Id).ToHashSet();
                Assert.That(ofRealm, Has.None.StartsWith("buffbot-"));
                for (int seed = 0; seed < 60; seed++)
                {
                    foreach (PvpLineup lineup in Enum.GetValues<PvpLineup>())
                    {
                        List<string> builds = PvpTeamPool.Compose(realm, lineup, new Random(seed));
                        string what = $"{realm} {lineup} {seed}: {string.Join(",", builds)}";
                        Assert.That(builds, Has.Count.EqualTo(8), what);
                        Assert.That(builds.All(ofRealm.Contains), Is.True, what);
                        List<eCharacterClass> classes = builds.Select(id => PvpBuildCatalog.Find(id).Class).ToList();
                        int healers = classes.Count(c => c is eCharacterClass.Cleric or eCharacterClass.Healer or eCharacterClass.Druid);
                        int speed = classes.Count(c => c is eCharacterClass.Minstrel or eCharacterClass.Skald or eCharacterClass.Bard);
                        Assert.That(healers, Is.InRange(1, 2), what);
                        Assert.That(speed, Is.LessThanOrEqualTo(1), what);
                        Assert.That(builds.GroupBy(id => id).Max(same => same.Count()), Is.LessThanOrEqualTo(2), what);
                        if (lineup == PvpLineup.NearMeta)
                        {
                            Assert.That(builds.Where((id, index) => id != main[index]).Count(), Is.InRange(1, 2), what);
                            Assert.That(healers, Is.EqualTo(2), what);
                            Assert.That(speed, Is.EqualTo(1), what);
                        }
                        if (lineup == PvpLineup.Loose)
                        {
                            Assert.That(healers, Is.EqualTo(2), what);
                            Assert.That(speed, Is.EqualTo(1), what);
                        }
                    }
                }
                // "Most of these groups would run sub optimal composition": some bad groups have no speed, and few are the main group.
                List<List<string>> bad = Enumerable.Range(0, 100).Select(seed => PvpTeamPool.Compose(realm, PvpLineup.Random, new Random(seed))).ToList();
                Assert.That(bad.Count(builds => !builds.Any(id => PvpBuildCatalog.Find(id).Class is eCharacterClass.Minstrel or eCharacterClass.Skald or eCharacterClass.Bard)),
                    Is.InRange(20, 60), realm.ToString());
            }
        }

        [Test]
        public void APoolHasANameAndAKindForEveryTeam()
        {
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(7));
            Assert.That(pool, Has.Count.EqualTo(48));
            Assert.That(pool.Select(team => team.Id).Distinct().Count(), Is.EqualTo(48));
            Assert.That(pool.Select(team => team.Guild).Distinct().Count(), Is.EqualTo(48));
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<PvpGuildTeam> ofRealm = pool.Where(team => team.ToRealm() == realm).ToList();
                Assert.That(ofRealm, Has.Count.EqualTo(16), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Bad), Is.EqualTo(3), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Decent), Is.EqualTo(4), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Elite), Is.EqualTo(1), realm.ToString());
            }
            foreach (PvpGuildTeam team in pool)
            {
                PvpTeamProfile profile = team.ToProfile();
                Assert.That(team.SeedLevel, Is.InRange(profile.MinRealmLevel, profile.MaxRealmLevel), team.Guild);
                Assert.That(team.Builds, Has.Count.EqualTo(8), team.Guild);
                Assert.That(team.ToTemplate().Realm, Is.EqualTo(team.ToRealm()));
                Assert.That(team.Realm, Is.AnyOf("Albion", "Midgard", "Hibernia"), "as a person would write it in the file");
                Assert.That(team.Guild.Length, Is.LessThanOrEqualTo(30), team.Guild);
            }
            // More teams for a pool that has some: none of the names that are taken.
            List<PvpGuildTeam> more = PvpTeamPool.Generate(6, new Random(8), pool);
            Assert.That(more, Has.Count.EqualTo(18));
            Assert.That(more.Select(team => team.Guild).Intersect(pool.Select(team => team.Guild)), Is.Empty);
            Assert.That(PvpTeamPool.MostPerRealm, Is.GreaterThanOrEqualTo(48));
            // "We could stress test and add more guilds and have a bigger pool": three evenings' worth, and no more than there are names.
            Assert.That(PvpTeamPool.PoolFor(12), Is.EqualTo(36));
            Assert.That(PvpTeamPool.PoolFor(4), Is.EqualTo(16));
            Assert.That(PvpTeamPool.PoolFor(PvpTeamPool.MostGroups), Is.EqualTo(PvpTeamPool.MostPerRealm));
            List<PvpGuildTeam> all = PvpTeamPool.Generate(PvpTeamPool.MostPerRealm, new Random(2));
            Assert.That(all, Has.Count.EqualTo(3 * PvpTeamPool.MostPerRealm));
            Assert.That(all.Select(team => team.Guild).Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(all.Count), "no name twice, in any realm");
            Assert.That(all.Select(team => team.Id).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.Max(team => team.Guild.Length), Is.LessThanOrEqualTo(30));
        }

        [Test]
        public void WhoIsOutThisEvening()
        {
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(3));
            List<PvpGuildTeam> tonight = PvpTeamPool.Tonight(pool, 8, new Random(4));
            Assert.That(tonight, Has.Count.EqualTo(24));
            Assert.That(tonight.Distinct().Count(), Is.EqualTo(24));
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<PvpGuildTeam> ofRealm = tonight.Where(team => team.ToRealm() == realm).ToList();
                Assert.That(ofRealm, Has.Count.EqualTo(8), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Bad), Is.InRange(1, 2), realm.ToString());
            }
            // The player's own group is one of its realm's eight.
            List<PvpGuildTeam> withPlayer = PvpTeamPool.Tonight(pool, 8, new Random(4), eRealm.Hibernia);
            Assert.That(withPlayer.Count(team => team.ToRealm() == eRealm.Hibernia), Is.EqualTo(7));
            Assert.That(withPlayer.Count(team => team.ToRealm() == eRealm.Albion), Is.EqualTo(8));
            // Not the same teams every evening.
            Assert.That(PvpTeamPool.Tonight(pool, 8, new Random(5)).Select(team => team.Id), Is.Not.EquivalentTo(tonight.Select(team => team.Id)));
            // A pool too small gives what it has.
            Assert.That(PvpTeamPool.Tonight(PvpTeamPool.Generate(2, new Random(1)), 8, new Random(1)), Has.Count.EqualTo(6));
        }

        [Test]
        public void BadGroupsWalkInPacksOfTwoToThree()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                for (int count = 0; count <= 12; count++)
                {
                    List<string> teams = Enumerable.Range(0, count).Select(index => "team" + index).ToList();
                    Dictionary<string, string> follows = PvpTeamPool.Packs(teams, new Random(seed));
                    List<int> sizes = teams.Where(team => !follows.ContainsKey(team)).Select(head => 1 + follows.Count(pair => pair.Value == head)).ToList();
                    string what = $"{count} teams, seed {seed}: {string.Join(",", sizes)}";
                    Assert.That(sizes.Sum(), Is.EqualTo(count), what);
                    Assert.That(follows.Values.All(head => !follows.ContainsKey(head)), Is.True, what);
                    if (count >= 2)
                        Assert.That(sizes, Is.All.InRange(2, PvpTeamPool.PackMost), what);
                }
            }
        }

        [Test]
        public void ATeamGoesFromPointToPointAsItPleases()
        {
            PvpRoamMap emain = PvpRoam.Emain;
            (int X, int Y)[] six = [PvpRoam.South, PvpRoam.Middle, PvpRoam.North, PvpRoam.WestNorth, PvpRoam.WestMiddle, PvpRoam.WestSouth];
            // Every point can be left, every way joins two of the six, and a realm's first points are among them.
            foreach ((int X, int Y) point in six)
                Assert.That(emain.From(point), Has.Count.GreaterThanOrEqualTo(2), point.ToString());
            Assert.That(emain.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct(), Is.EquivalentTo(six));
            foreach (PvpRoamStart start in emain.Starts)
            {
                Assert.That(six, Does.Contain(start.RouteOf(1)[0]));
                Assert.That(six, Does.Contain(start.RouteOf(2)[0]));
            }
            foreach ((int X, int Y) point in new[] { PvpRoam.ThidHM, PvpRoam.ThidHA, PvpRoam.ThidMA })
                Assert.That(PvpRoam.Thidranki.From(point), Has.Count.EqualTo(2));

            // Never back the way it came while there is another way, and only along a way.
            for (int roll = 0; roll < 50; roll++)
            {
                (int X, int Y) next = PvpRoam.NextPoint(emain, PvpRoam.Middle, PvpRoam.South, 0, roll);
                Assert.That(next, Is.Not.EqualTo(PvpRoam.South));
                Assert.That(emain.From(PvpRoam.Middle), Does.Contain(next));
            }
            // From the middle of the road: north along it, or across to the west side. An elite group leans west, the head of a pack to the road.
            int Count(int leaning, (int X, int Y) to) =>
                Enumerable.Range(0, 1000).Count(roll => PvpRoam.NextPoint(emain, PvpRoam.Middle, PvpRoam.South, leaning, roll) == to);
            Assert.That(Count(0, PvpRoam.WestMiddle), Is.EqualTo(500));
            Assert.That(Count(1, PvpRoam.WestMiddle), Is.EqualTo(800));
            Assert.That(Count(-1, PvpRoam.North), Is.EqualTo(800));
            // A point with one way only: back it goes. A map without ways: it stays.
            var deadEnd = new PvpRoamMap("x", "x", 1, [], [((0, 0), (10, 10))]);
            Assert.That(PvpRoam.NextPoint(deadEnd, (10, 10), (0, 0), 0, 3), Is.EqualTo((0, 0)));
            Assert.That(PvpRoam.NextPoint(new PvpRoamMap("y", "y", 1, []), (5, 5), null, 0, 3), Is.EqualTo((5, 5)));
        }

        [Test]
        public void ManyGroupsDoNotStartInOneAnother()
        {
            (int X, int Y) start = (100_000, 200_000);
            List<(int X, int Y)> spots = Enumerable.Range(0, 13).Select(index => PvpRoam.SpawnPoint(start, index)).ToList();
            Assert.That(spots[0], Is.EqualTo(start));
            for (int a = 0; a < spots.Count; a++)
            {
                Assert.That(Math.Sqrt(Math.Pow(spots[a].X - start.X, 2) + Math.Pow(spots[a].Y - start.Y, 2)), Is.LessThanOrEqualTo(1_000), a.ToString());
                for (int b = a + 1; b < spots.Count; b++)
                    Assert.That(Math.Sqrt(Math.Pow(spots[a].X - spots[b].X, 2) + Math.Pow(spots[a].Y - spots[b].Y, 2)), Is.GreaterThan(350), $"{a} and {b}");
            }
        }

        [Test]
        public void WhatARealmCallsOut()
        {
            // "which team will we face, how many teams (1 team, a zerg [many teams]), where"
            Assert.That(PvpRoam.SightingText(2, eRealm.Albion, "south road", false), Is.EqualTo("2fg albs at south road"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Midgard, "mid road", true), Is.EqualTo("fighting 1fg mids at mid road"));
            Assert.That(PvpRoam.SightingText(3, eRealm.Hibernia, "north road", false, true), Is.EqualTo("wiped at north road, 3fg hibs"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Albion, null, false), Is.EqualTo("1fg albs"));
            // A spot called by its side of a point takes no "at".
            Assert.That(PvpRoam.SightingText(1, eRealm.Midgard, "east of north road (Odin's)", false), Is.EqualTo("1fg mids east of north road (Odin's)"));
            Assert.That(PvpRoam.SightingText(2, eRealm.Albion, "south-west of Crimthain (Breifine)", true), Is.EqualTo("fighting 2fg albs south-west of Crimthain (Breifine)"));
            Assert.That(PvpRoam.SightingText(3, eRealm.Hibernia, "north of middle", false, true), Is.EqualTo("wiped north of middle, 3fg hibs"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Albion, "north-west hills", false), Is.EqualTo("1fg albs at north-west hills"), "a point whose name begins with a side");
            Assert.That(PvpRoam.OnTheRunText(4, eRealm.Midgard, "west of mid road"), Is.EqualTo("4 mids on the run west of mid road"));
            // A team that has said it joins a fight goes to it: to the nearest enemy team it drew "joins" for.
            Assert.That(PvpRoam.FightItJoined([((1_000.0, 0.0), false), ((9_000.0, 0.0), true), ((0.0, 7_000.0), true)], (0.0, 0.0)), Is.EqualTo((0, 7_000)));
            Assert.That(PvpRoam.FightItJoined([((1_000.0, 0.0), false)], (0.0, 0.0)), Is.Null, "it joined nothing: it walks its road");
            Assert.That(PvpRoam.FightItJoined([], (0.0, 0.0)), Is.Null);
            // The words after "/8v8 prime": a word that is not understood starts nothing.
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["emain", "8", "rank", "8", "hib-2bard"], out string[] words, out int rank), Is.Null);
            Assert.That(words, Is.EqualTo(new[] { "emain", "8", "hib-2bard" }));
            Assert.That(rank, Is.EqualTo(8));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["emain", "8", "rank", "hib-2bard"], out _, out _), Does.Contain("needs a number"),
                "what the owner typed on 2026-11-24: it began an evening at the owner's own rank with the main line-up");
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["emain", "rank"], out _, out _), Does.Contain("needs a number"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["emain", "rank", "14"], out _, out _), Does.Contain("needs a number"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["emain", "hib-2bards"], out _, out _), Does.Contain("hib-2bards"));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords(["thid", "6", "one"], out words, out rank), Is.Null);
            Assert.That(rank, Is.EqualTo(0));
            Assert.That(DOL.GS.Commands.Pvp8v8CommandHandler.PrimeWords([], out words, out _), Is.Null);
            Assert.That(words, Is.Empty);
            // An evening at a picked rank (/8v8 prime emain 8 rank 8): rank 8 is realm level 70, and the player's group earns nothing.
            Assert.That(PvpRoam.TrialLevel(8), Is.EqualTo(70));
            Assert.That(PvpRoam.TrialLevel(1), Is.EqualTo(0), "rank 1 is what a new group has");
            Assert.That(PvpRoam.TrialLevel(0), Is.EqualTo(0));
            Assert.That(PvpRoam.TrialLevel(14), Is.EqualTo(0));
            Assert.That(PvpRoam.Earns(true, 70), Is.False);
            Assert.That(PvpRoam.Earns(false, 70), Is.True, "the guilds earn as always");
            Assert.That(PvpRoam.Earns(true, 0), Is.True);
            // A group that breaks off (M12.5): its own realm hears that it runs, the other that it is on the run.
            Assert.That(PvpRoam.RunningText(2, eRealm.Albion, "south road (Emain)"), Is.EqualTo("running from 2fg albs at south road (Emain)"));
            Assert.That(PvpRoam.OnTheRunText(4, eRealm.Midgard, "mid road"), Is.EqualTo("4 mids on the run at mid road"));
            Assert.That(PvpRoam.OnTheRunText(3, eRealm.Hibernia, null), Is.EqualTo("3 hibs on the run"));
            // Not a flood: about one realm at one place one call, until the number there changes or three minutes go by.
            Assert.That(PvpRoam.CallsOut(3, 500, false), Is.False);
            Assert.That(PvpRoam.CallsOut(20, 90, false), Is.False, "said already, and nothing has changed");
            Assert.That(PvpRoam.CallsOut(20, 200, false), Is.True);
            Assert.That(PvpRoam.CallsOut(20, 40, true), Is.True, "a third group has come");
            Assert.That(PvpRoam.CallsOut(20, 10, true), Is.False);
            Assert.That(PvpRoam.NoNewsWithin, Is.LessThan(PvpRoam.SightingWithin));

            // Every point of a map has a name, and a place by a realm's start is "the keep".
            PvpRoamMap emain = PvpRoam.Emain;
            Assert.That(emain.Places.Select(place => place.Point), Is.EquivalentTo(emain.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct()));
            Assert.That(emain.PlaceOf(PvpRoam.South.X + 800, PvpRoam.South.Y - 500), Is.EqualTo("south road"));
            Assert.That(emain.PlaceOf(PvpRoam.WestMiddle.X, PvpRoam.WestMiddle.Y), Is.EqualTo("west hills"));
            // Far from its nearest named point, a spot is called by its side of it (north is less Y, east is more X).
            PvpRoamMap breifine = PvpRoamZones.Breifine;
            Assert.That(breifine.PlaceOf(431792, 378024), Is.EqualTo("Crimthain"));
            Assert.That(breifine.PlaceOf(431792 + 2_000, 378024 - 2_000), Is.EqualTo("Crimthain"), "within 3,000 it is at the point");
            Assert.That(breifine.PlaceOf(431792 + 4_000, 378024 - 4_500), Is.EqualTo("north-east of Crimthain"));
            Assert.That(breifine.PlaceOf(431792, 378024 - 5_000), Is.EqualTo("north of Crimthain"));
            Assert.That(breifine.PlaceOf(431792 - 5_000, 378024 + 500), Is.EqualTo("west of Crimthain"));
            Assert.That(breifine.PlaceOf(431792 - 5_000, 378024 + 500, true), Is.EqualTo("Crimthain"), "the point alone is what counts as news");
            Assert.That(PvpRoam.Compass(1, 0), Is.EqualTo("east"));
            Assert.That(PvpRoam.Compass(-1, 0), Is.EqualTo("west"));
            Assert.That(PvpRoam.Compass(0, 1), Is.EqualTo("south"));
            Assert.That(PvpRoam.Compass(0, -1), Is.EqualTo("north"));
            Assert.That(PvpRoam.Compass(-1, -1), Is.EqualTo("north-west"));
            Assert.That(PvpRoam.Compass(1, 1), Is.EqualTo("south-east"));
            Assert.That(PvpRoam.Compass(-1, 1), Is.EqualTo("south-west"));
            Assert.That(PvpRoam.Compass(100, -30), Is.EqualTo("east"), "a little north of east is east");
            // What the player is told with the line: how far, to the nearest 500, and which way.
            Assert.That(PvpRoam.FromYouText(-11_000, -11_300), Is.EqualTo("16,000 north-west of you"));
            Assert.That(PvpRoam.FromYouText(0, 4_200), Is.EqualTo("4,000 south of you"));
            Assert.That(PvpRoam.FromYouText(300, -200), Is.EqualTo("beside you"));
            (int X, int Y) albion = emain.StartOf(eRealm.Albion).Point;
            Assert.That(emain.PlaceOf(albion.X - 2_000, albion.Y), Is.EqualTo("the alb keep"));
            Assert.That(PvpRoam.Thidranki.Places, Has.Count.EqualTo(3));
            Assert.That(new PvpRoamMap("x", "x", 1, []).PlaceOf(1, 1), Is.Null);

            // What a player is told of the other realms: how many groups, and no more.
            Assert.That(PvpRoam.OutAgainst([(eRealm.Albion, 8), (eRealm.Midgard, 1)]),
                Is.EqualTo("Out against you: 8 groups of Albion and 1 group of Midgard. Where they are, your realm says when it sees them."));
            Assert.That(PvpRoam.OutAgainst([]), Is.EqualTo("Nobody else is out."));
        }

        [Test]
        public void AGuildsEveningEnds()
        {
            // Wiped three times: it has had enough.
            Assert.That(PvpRoam.LogsOff(3, false, true, false), Is.True);
            Assert.That(PvpRoam.LogsOff(2, false, true, false), Is.False);
            Assert.That(PvpRoam.LogsOff(5, false, false, false), Is.False, "not while it is up and fighting");
            // Its time is up: at its next wipe, or on the road with nobody about. Never out of a fight.
            Assert.That(PvpRoam.LogsOff(0, true, false, true), Is.True);
            Assert.That(PvpRoam.LogsOff(1, true, true, false), Is.True);
            Assert.That(PvpRoam.LogsOff(0, true, false, false), Is.False);
            Assert.That(PvpRoam.LogsOff(0, false, false, true), Is.False);
            foreach (int roll in new[] { 0, 1, 1499, 1500, 1501, 99_999, int.MaxValue })
                Assert.That(PvpRoam.EveningSeconds(roll), Is.InRange(25 * 60, 50 * 60), roll.ToString());

            // Another of its realm comes out: of its kind when there is one, never one that is out or has been.
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(11));
            List<PvpGuildTeam> midgard = pool.Where(team => team.ToRealm() == eRealm.Midgard).ToList();
            PvpGuildTeam elite = midgard.Single(team => team.ToProfile().Kind == PvpTeamKind.Elite);
            List<PvpGuildTeam> bad = midgard.Where(team => team.ToProfile().Kind == PvpTeamKind.Bad).ToList();
            var taken = new HashSet<string> { bad[0].Id, elite.Id };
            for (int seed = 0; seed < 30; seed++)
            {
                PvpGuildTeam next = PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, taken, new Random(seed));
                Assert.That(next.ToRealm(), Is.EqualTo(eRealm.Midgard));
                Assert.That(next.ToProfile().Kind, Is.EqualTo(PvpTeamKind.Bad));
                Assert.That(taken, Does.Not.Contain(next.Id));
                // The realm's one elite team is out: somebody else comes.
                PvpGuildTeam other = PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Elite, taken, new Random(seed));
                Assert.That(other.ToProfile().Kind, Is.Not.EqualTo(PvpTeamKind.Elite));
                Assert.That(taken, Does.Not.Contain(other.Id));
            }
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, midgard.Select(team => team.Id).ToHashSet(), new Random(1)), Is.Null, "nobody is left");
            // An evening that outlasts its pool (the owner, an hour and a half in: "Emain : 4 albion, 1 midgard, Odin's : 0"):
            // a guild that logged off long enough ago comes out again, of the kind when there is one, the longest gone first.
            var everybody = midgard.Select(team => team.Id).ToHashSet();
            List<string> gone = [midgard.First(team => team.ToProfile().Kind == PvpTeamKind.Decent).Id, bad[1].Id, bad[0].Id];
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, everybody, new Random(1), gone).Id, Is.EqualTo(bad[1].Id));
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Elite, everybody, new Random(1), gone).Id, Is.EqualTo(gone[0]), "none of its kind: the longest gone");
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Albion, PvpTeamKind.Bad, pool.Select(team => team.Id).ToHashSet(), new Random(1), gone), Is.Null, "not another realm's");
            // (One that has not been out comes before one that comes back.)
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, taken, new Random(1), gone).Id, Is.Not.AnyOf(gone.ToArray<object>()).And.Not.AnyOf(taken.ToArray<object>()));
            Assert.That(PvpTeamPool.ComesBack(PvpTeamPool.ComesBackAfterSeconds), Is.True);
            Assert.That(PvpTeamPool.ComesBack(120), Is.False, "not the moment it has logged off");
        }

        [Test]
        public void AGuildRemembers()
        {
            // "a team beaten twice comes looking; a team that was added on remembers"
            Assert.That(PvpRoam.ComesLooking(1, false), Is.False);
            Assert.That(PvpRoam.ComesLooking(2, false), Is.True);
            Assert.That(PvpRoam.ComesLooking(0, true), Is.True);
            Assert.That(PvpRoam.PaysBack(true, true), Is.True);
            Assert.That(PvpRoam.PaysBack(true, false), Is.False, "somebody else's fight: it does as its kind does");
            Assert.That(PvpRoam.PaysBack(false, true), Is.False);
            // Added on: the player's group came into a fight it had with somebody else.
            Assert.That(PvpRoam.WasAddedOn(true, 1, false), Is.True);
            Assert.That(PvpRoam.WasAddedOn(true, 0, false), Is.False, "a fight of the two of them");
            Assert.That(PvpRoam.WasAddedOn(true, 1, true), Is.False, "it was the one that added");
            Assert.That(PvpRoam.WasAddedOn(false, 2, false), Is.False);
            // It goes toward the player's group: from the south end of the road, north along it for a group in the north, west for one in the west.
            Assert.That(PvpRoam.PointToward(PvpRoam.Emain, PvpRoam.South, (PvpRoam.North.X, PvpRoam.North.Y)), Is.EqualTo(PvpRoam.Middle));
            Assert.That(PvpRoam.PointToward(PvpRoam.Emain, PvpRoam.South, (PvpRoam.WestSouth.X - 500, PvpRoam.WestSouth.Y)), Is.EqualTo(PvpRoam.WestSouth));
            Assert.That(PvpRoam.PointToward(new PvpRoamMap("y", "y", 1, []), (5, 5), (0, 0)), Is.EqualTo((5, 5)));

            // Its record against the player is kept from evening to evening.
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(2, new Random(5));
            var evening = new Dictionary<string, PvpEvening> { [pool[0].Id] = new PvpEvening(4, 20, 16, 2, 2, 1) };
            List<PvpGuildTeam> after = PvpTeamPool.Recorded(PvpTeamPool.Recorded(pool, evening), evening);
            Assert.That(after[0].Evenings, Is.EqualTo(2));
            Assert.That(after[0].WipedByPlayer, Is.EqualTo(4));
            Assert.That(after[0].WipedPlayer, Is.EqualTo(2));
            Assert.That(after[0].Wipes, Is.EqualTo(4));
            Assert.That(after[1], Is.EqualTo(pool[1]), "it was not out");
            Assert.That(PvpRoam.RivalLine("The Iron Longship", "Midgard", 3, 2, 1, 0),
                Is.EqualTo("The Iron Longship <Midgard>: you wiped them 3 times (2 tonight), they were in on your wipe 1 time"));
        }

        [Test]
        public void AnEveningOverSeveralZones()
        {
            // "Maps? I would assume more than just emain"
            // A team takes the zone that has the fewest groups of its realm, any of them when several have as few.
            Assert.That(PvpRoam.ZoneWithFewest([3, 1, 2], 7), Is.EqualTo(1));
            Assert.That(Enumerable.Range(0, 30).Select(roll => PvpRoam.ZoneWithFewest([2, 0, 0], roll)).Distinct(), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(Enumerable.Range(0, 30).Select(roll => PvpRoam.ZoneWithFewest([0, 0, 0], roll)).Distinct(), Is.EquivalentTo(new[] { 0, 1, 2 }), "a pack is not always in the first zone");
            Assert.That(PvpRoam.ZoneWithFewest([4], 9), Is.Zero);
            Assert.That(PvpRoam.ZoneWithFewest([], 9), Is.Zero);
            // Eight groups of a realm, a pack of three among them: three, three and two.
            var load = new[] { 0, 0, 0 };
            foreach (int size in new[] { 3, 1, 1, 1, 1, 1 })
                load[PvpRoam.ZoneWithFewest(load, size * 31)] += size;
            Assert.That(load.OrderBy(count => count), Is.EqualTo(new[] { 2, 3, 3 }));
            // A guild that walks alone and has been wiped ports elsewhere one time in three; a pack and a follower stay.
            Assert.That(Enumerable.Range(0, 300).Count(roll => PvpRoam.PortsElsewhere(3, false, false, roll)), Is.EqualTo(100));
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(1, false, false, roll)), Is.False, "there is nowhere else");
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(3, true, false, roll)), Is.False, "it walks with somebody");
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(3, false, true, roll)), Is.False, "somebody walks with it");
            // Every frontier has a start for each realm, points with names, ways between them, and a word that names it.
            Assert.That(PvpRoam.Frontiers, Does.Contain(PvpRoam.Emain));
            Assert.That(PvpRoam.Frontiers, Has.Count.EqualTo(12), "four zones of each realm's frontier");
            Assert.That(PvpRoam.Frontiers.GroupBy(zone => zone.Region).Select(region => region.Count()), Is.EqualTo(new[] { 4, 4, 4 }));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Zone).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Zone), Has.None.Zero);
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Tag).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Id).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            foreach (PvpRoamMap zone in PvpRoam.Frontiers)
            {
                Assert.That(PvpRoam.MapFor(zone.Id), Is.EqualTo(zone), zone.Name);
                Assert.That(PvpRoam.MapFor(zone.Name.Replace(" ", string.Empty).Replace("'", string.Empty)), Is.EqualTo(zone), zone.Name);
                // The three starts are far from one another: nobody stands up in somebody else's start.
                foreach (PvpRoamStart one in zone.Starts)
                    foreach (PvpRoamStart other in zone.Starts.Where(other => other != one))
                        Assert.That(Math.Sqrt(Math.Pow(one.Point.X - other.Point.X, 2) + Math.Pow(one.Point.Y - other.Point.Y, 2)), Is.GreaterThan(25_000), $"{zone.Name} {one.Realm} {other.Realm}");
                Assert.That(zone.Starts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }), zone.Name);
                List<(int X, int Y)> points = zone.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct().ToList();
                Assert.That(points, Has.Count.GreaterThanOrEqualTo(4), zone.Name);
                Assert.That(zone.Places.Select(place => place.Point), Is.EquivalentTo(points), zone.Name);
                Assert.That(zone.Places.Select(place => place.Name).Distinct().Count(), Is.EqualTo(points.Count), zone.Name);
                foreach ((int X, int Y) point in points)
                    Assert.That(zone.From(point), Is.Not.Empty, $"{zone.Name} {point}");
                foreach (PvpRoamStart start in zone.Starts)
                {
                    Assert.That(points, Does.Contain(start.RouteOf(1)[0]), $"{zone.Name} {start.Realm}");
                    Assert.That(points, Does.Contain(start.RouteOf(2)[0]), $"{zone.Name} {start.Realm}");
                }
                Assert.That(PvpRoam.MapFor(zone.Tag.ToLowerInvariant().Replace("'s", string.Empty)), Is.EqualTo(zone), zone.Name);
            }
            Assert.That(PvpRoam.Emain.Tag, Is.EqualTo("Emain"));
        }

        [Test]
        public void TheZonesBehindTheFirstThree()
        {
            // "adding more zones and spreading teams (and type of teams) across them all"
            Assert.That(PvpRoamZones.All, Has.Count.EqualTo(9));
            Assert.That(PvpRoam.MapFor("sauvage"), Is.SameAs(PvpRoamZones.ForestSauvage));
            Assert.That(PvpRoam.MapFor("ForestSauvage"), Is.SameAs(PvpRoamZones.ForestSauvage));
            Assert.That(PvpRoamZones.MountCollory.Tag, Is.EqualTo("Collory"));
            var home = new Dictionary<ushort, eRealm> { [1] = eRealm.Albion, [100] = eRealm.Midgard, [200] = eRealm.Hibernia };
            foreach (PvpRoamMap zone in PvpRoamZones.All)
            {
                Assert.That(zone.Crowd, Is.EqualTo(PvpZoneCrowd.Clean), zone.Name);
                // The realm of the zone is in front of one of its keeps; the two others have none there.
                Assert.That(zone.Starts.Where(start => start.AtKeep).Select(start => start.Realm), Is.EqualTo(new[] { home[zone.Region] }), zone.Name);
                List<(int X, int Y)> points = zone.Places.Select(place => place.Point).ToList();
                Assert.That(points, Has.Count.InRange(5, 9), zone.Name);
                Assert.That(zone.Quiet, Has.Count.EqualTo(1), zone.Name);
                Assert.That(points, Does.Contain(zone.Quiet[0]), zone.Name);
                foreach (((int X, int Y) a, (int X, int Y) b) in zone.Ways)
                    Assert.That(Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)), Is.InRange(10_000, 27_000), $"{zone.Name} {a} {b}");
                // Every point can be walked to from every other.
                var reached = new HashSet<(int X, int Y)> { points[0] };
                for (int round = 0; round < points.Count; round++)
                    foreach ((int X, int Y) point in reached.ToList())
                        reached.UnionWith(zone.From(point));
                Assert.That(reached, Has.Count.EqualTo(points.Count), zone.Name);
                // Where a realm comes in without a keep is called by the point it is near, not "the keep".
                PvpRoamStart camp = zone.Starts.First(start => !start.AtKeep);
                Assert.That(zone.PlaceOf(camp.Point.X, camp.Point.Y), Does.Not.Contain("keep"), zone.Name);
                PvpRoamStart keep = zone.Starts.First(start => start.AtKeep);
                Assert.That(zone.PlaceOf(keep.Point.X, keep.Point.Y), Is.EqualTo($"the {PvpRoam.Short(keep.Realm, false)} keep"), zone.Name);
            }
            Assert.That(PvpRoam.Emain.Crowd, Is.EqualTo(PvpZoneCrowd.Crowded));
            Assert.That(PvpRoam.OdinsGate.Crowd, Is.EqualTo(PvpZoneCrowd.Mixed));
            Assert.That(PvpRoam.HadriansWall.Crowd, Is.EqualTo(PvpZoneCrowd.Mixed));
        }

        [Test]
        public void EachKindOfTeamHasItsKindOfZone()
        {
            // "Elite 8v8 teams that would be only 8v8 in specific area to avoid not clean fights"
            Assert.That(PvpRoam.Leans(PvpTeamKind.Elite, PvpZoneCrowd.Crowded), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Bad, PvpZoneCrowd.Clean), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Clingy, PvpZoneCrowd.Clean), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Adder, PvpZoneCrowd.Clean), Is.Zero, "nobody adds in a clean zone, so a group that adds is not in one (M13.16)");
            foreach (PvpTeamKind kind in Enum.GetValues<PvpTeamKind>())
                Assert.That(Enum.GetValues<PvpZoneCrowd>().Sum(crowd => PvpRoam.Leans(kind, crowd)), Is.GreaterThan(0), kind.ToString());
            PvpZoneCrowd[] zones = [PvpZoneCrowd.Crowded, PvpZoneCrowd.Mixed, PvpZoneCrowd.Clean];
            int[] nobody = [0, 0, 0];
            List<int> Picks(PvpTeamKind kind, PvpZoneCrowd[] among, int[] load) => Enumerable.Range(0, 1000).Select(roll => PvpRoam.ZoneFor(kind, among, load, roll * 7919)).ToList();
            Assert.That(Picks(PvpTeamKind.Elite, zones, nobody), Has.None.EqualTo(0), "an elite team is not in the crowd");
            Assert.That(Picks(PvpTeamKind.Elite, zones, nobody).Count(pick => pick == 2), Is.GreaterThan(800));
            Assert.That(Picks(PvpTeamKind.Bad, zones, nobody), Has.None.EqualTo(2), "a pack is not in a clean zone");
            Assert.That(Picks(PvpTeamKind.Bad, zones, nobody).Count(pick => pick == 0), Is.GreaterThan(650));
            Assert.That(Picks(PvpTeamKind.Decent, zones, nobody).Distinct(), Is.EquivalentTo(new[] { 0, 1, 2 }), "a decent team is everywhere");
            // Of the zones its kind leans to, rather the one that has few of its realm, and one that has none first.
            Assert.That(Picks(PvpTeamKind.Decent, [PvpZoneCrowd.Mixed, PvpZoneCrowd.Mixed], [4, 0]).Count(pick => pick == 1), Is.GreaterThan(900));
            Assert.That(Picks(PvpTeamKind.Decent, [PvpZoneCrowd.Mixed, PvpZoneCrowd.Mixed], [3, 1]).Count(pick => pick == 1), Is.InRange(580, 760));
            // Where its kind leans to none of the zones there are, it takes the one with the fewest.
            Assert.That(Picks(PvpTeamKind.Bad, [PvpZoneCrowd.Clean, PvpZoneCrowd.Clean], [3, 1]), Has.All.EqualTo(1));
            Assert.That(Picks(PvpTeamKind.Elite, [PvpZoneCrowd.Crowded], [5]), Has.All.EqualTo(0));
            // The choosiest take their zones first.
            Assert.That(new[] { PvpTeamKind.Decent, PvpTeamKind.Bad, PvpTeamKind.Elite, PvpTeamKind.Adder }.OrderBy(PvpRoam.Choosiness).Take(2),
                Is.EqualTo(new[] { PvpTeamKind.Elite, PvpTeamKind.Bad }));
            // The draw among weights.
            Assert.That(PvpRoam.Weighted([0, 0], 5), Is.EqualTo(-1));
            Assert.That(PvpRoam.Weighted([], 5), Is.EqualTo(-1));
            Assert.That(Enumerable.Range(0, 8).Select(roll => PvpRoam.Weighted([1, 0, 3], roll)), Is.EqualTo(new[] { 0, 2, 2, 2, 0, 2, 2, 2 }));
            Assert.That(PvpRoam.Weighted([1, 0, 3], -6), Is.EqualTo(2), "a roll below zero");
        }

        [Test]
        public void TheZonesOfAnEvening()
        {
            // One zone for every four groups of a realm (three until 2026-11-25, when the owner found "the population is low").
            Assert.That(new[] { 1, 2, 4, 5, 8, 12, 16 }.Select(groups => PvpRoam.ZonesFor(groups, 12)), Is.EqualTo(new[] { 1, 1, 1, 1, 2, 3, 4 }));
            Assert.That(PvpRoam.ZonesFor(16, 3), Is.EqualTo(3), "no more than there are");
            Assert.That(PvpRoam.ZonesFor(8, 0), Is.EqualTo(1));
            var seen = new HashSet<PvpRoamMap>();
            for (int seed = 0; seed < 300; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoam.Emain, 12, PvpRoam.Frontiers, new Random(seed));
                Assert.That(zones, Has.Count.EqualTo(3));
                Assert.That(zones[0], Is.SameAs(PvpRoam.Emain), "where it begins");
                Assert.That(zones.Distinct().Count(), Is.EqualTo(3));
                seen.UnionWith(zones);
            }
            Assert.That(seen, Has.Count.EqualTo(12), "every zone has its evenings");
            // It begins where the player is, and the crowd is somewhere all the same.
            for (int seed = 0; seed < 50; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoamZones.Breifine, 12, PvpRoam.Frontiers, new Random(seed));
                Assert.That(zones.Take(2), Is.EqualTo(new[] { PvpRoamZones.Breifine, PvpRoam.Emain }));
                Assert.That(zones, Has.Count.EqualTo(3));
            }
            Assert.That(PvpRoam.ZonesTonight(PvpRoamZones.Breifine, 4, PvpRoam.Frontiers, new Random(1)), Is.EqualTo(new[] { PvpRoamZones.Breifine }), "few groups: the one zone");
            // A zone the realms ported to comes up more often than one behind it.
            int ported = 0, behind = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoam.Emain, 12, PvpRoam.Frontiers, new Random(seed));
                ported += zones.Count(zone => zone == PvpRoam.OdinsGate);
                behind += zones.Count(zone => zone == PvpRoamZones.Uppland);
            }
            Assert.That(ported, Is.GreaterThan(behind * 2));
            Assert.That(behind, Is.GreaterThan(0));
            // What the player is told of them.
            Assert.That(PvpRoam.ZonesLine([PvpRoam.Emain, PvpRoam.OdinsGate, PvpRoamZones.Breifine], PvpRoam.Emain),
                Is.EqualTo("Tonight: Emain Macha (the crowd; you are here), Odin's Gate, Breifine (clean, nobody adds). /8v8 go odin, breifine."));
            Assert.That(PvpRoam.ZonesLine([PvpRoam.Emain, PvpRoamZones.Pennine], PvpRoamZones.Pennine),
                Is.EqualTo("Tonight: Emain Macha (the crowd), Pennine Mountains (clean, nobody adds; you are here). /8v8 go emain."));
            // More groups than there are zones for: all twelve.
            Assert.That(PvpRoam.ZonesTonight(PvpRoam.Emain, 50, PvpRoam.Frontiers, new Random(3)), Is.EquivalentTo(PvpRoam.Frontiers));
        }

        [Test]
        public void TeamsComeOutOneByOne()
        {
            var real = new PvpRealWeights();
            // No two in the same moment: a realm's teams half a minute apart, the realms ten seconds apart.
            List<int> times = Enumerable.Range(0, 3).SelectMany(realm => Enumerable.Range(0, 8).Select(before => PvpRoam.ComesOutAfterSeconds(before, realm, real))).ToList();
            Assert.That(times.Distinct().Count(), Is.EqualTo(times.Count));
            Assert.That(PvpRoam.ComesOutAfterSeconds(0, 0, real), Is.Zero, "somebody is out when the evening begins");
            Assert.That(PvpRoam.ComesOutAfterSeconds(1, 0, real) - PvpRoam.ComesOutAfterSeconds(0, 0, real), Is.EqualTo(real.NextTeamFollowsAfterSeconds));
            Assert.That(PvpRoam.Doing("Away", false, false, false, false, [], 0, 0), Is.EqualTo("not out yet"));
        }

        [Test]
        public void APackKeepsTogether()
        {
            Assert.That(PvpRoam.TrailsBehind, Is.LessThan(PvpRoam.KeepsWithin(PvpCompany.Near)), "a group that stays near has room to trail in");
            Assert.That(PvpRoam.KeepsWithin(PvpCompany.Zerg), Is.LessThan(PvpRoam.KeepsWithin(PvpCompany.Near)));
            Assert.That(PvpRoam.KeepsWithin(PvpCompany.Zerg), Is.LessThanOrEqualTo(PvpRoam.BesideWithin), "a pack counts as together");
            // A guild that does not add gives a fight three quarters of a minute to end, then goes.
            Assert.That(PvpRoam.GivesUpWaiting(20), Is.False);
            Assert.That(PvpRoam.GivesUpWaiting(60), Is.True);
            Assert.That(PvpRoam.PackWaits(true, 5), Is.True);
            Assert.That(PvpRoam.PackWaits(true, 30), Is.False, "it does not wait for ever");
            Assert.That(PvpRoam.PackWaits(false, 5), Is.False);
        }
    }
}
