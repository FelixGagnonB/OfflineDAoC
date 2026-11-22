using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>Where a realm's teams start in a roam, and the points they walk between.</summary>
    /// <param name="SecondRoute">The way of the realm's second team, when it is not the first team's.</param>
    public sealed record PvpRoamStart(eRealm Realm, (int X, int Y) Point, (int X, int Y)[] Route, (int X, int Y)[] SecondRoute = null)
    {
        public (int X, int Y)[] RouteOf(int teamNumber) => teamNumber >= 2 && SecondRoute != null ? SecondRoute : Route;
    }

    /// <summary>A zone a roam is held in: its region and where each realm's teams start and walk.</summary>
    public sealed record PvpRoamMap(string Id, string Name, ushort Region, IReadOnlyList<PvpRoamStart> Starts)
    {
        public PvpRoamStart StartOf(eRealm realm) => Starts.First(start => start.Realm == realm);
    }

    /// <summary>One team of a roam.</summary>
    public sealed class PvpRoamTeam
    {
        public string Name;
        public PvpGroupTemplate Template;
        public eRealm Realm;
        public PvpRoamStart Start;
        public string Roster;
        public readonly PvpTeam Team = new();
        /// <summary>Everything it put in the world: its eight and its buffbot.</summary>
        public readonly List<GameBot> All = [];
        public string State = "Buffing";
        public long StateSince, LeavesAt, ContactTick, LastEnemyNearTick;
        /// <summary>No enemy team near: the recorder writes it every fifth second only.</summary>
        public bool Quiet = true;
        /// <summary>Standing by a fight it has left to its realm-mate.</summary>
        public bool Waiting;
        /// <summary>It has had its rest since its last fight.</summary>
        public bool Rested;
        /// <summary>Out of a fight with dead members since this tick (0: none); and gone back to its start to form up again.</summary>
        public long DeadSince;
        public bool Reforming;
        /// <summary>The enemy teams it is in a fight with now.</summary>
        public readonly HashSet<PvpRoamTeam> Fighting = [];
        /// <summary>Its draw for each enemy team its realm's other team is fighting: true, it joins.</summary>
        public readonly Dictionary<PvpRoamTeam, bool> Draws = [];
        public readonly HashSet<GameLiving> Paid = [];
        public int Wipes, Kills, Deaths, Fights, Joined, LeftAlone;
        public long Earned;

        /// <summary>The player at its head; null for a team of bots.</summary>
        public GamePlayer Leader => Team.Leader;
        public int Alive => Team.Members.Count(member => member.IsAlive);
        public (double X, double Y) Centre()
        {
            List<GameLiving> living = Team.Members.Where(member => member.IsAlive).ToList();
            if (living.Count == 0)
                living = Team.Members.ToList();
            return living.Count == 0 ? (Start.Point.X, Start.Point.Y) : (living.Average(member => member.X), living.Average(member => member.Y));
        }
    }

    /// <summary>
    /// The roam (docs/PVP-8V8-ROADMAP.md, M9.7): several teams in one zone, in a session that
    /// goes on. The owner, 2026-11-20: "There could be 2 group for each realm. PvP ruleset is not
    /// enable on this so they would be fighting any of the opposite realm team"; "We revive at
    /// our start point"; Emain Macha, "Albion would spawn (and release) at their principal keep.
    /// Same for midgard. Hibernia ... on the hill near the tower in front of Albion Main Gate."
    /// Bot teams alone (from the control channel, to be read from its status), or with a player
    /// at the head of the first team of its realm (<c>/8v8 roam</c>): seven companions, the
    /// rules of a real session (ranks as earned, realm points kept, no timer reset), and after a
    /// wipe the player stands up at the start with the companions, without sickness.
    /// </summary>
    public static class PvpRoam
    {
        private static readonly Logger Log = LoggerManager.Create(typeof(PvpRoam));

        public const ushort EmainRegion = 200;
        // The two ends of the open middle of Emain Macha, west of both portal keeps, 38,000 units
        // apart on foot (UT_PvpRoamStartRoutes): from the Midgard keep to the north end is 26,600,
        // from the Albion keep to the south end 35,500.
        public static readonly (int X, int Y) North = (449336, 299008), South = (445970, 334336);

        /// <summary>
        /// "The hill near the tower in front of Albion Main Gate": the owner stood there on
        /// 2026-11-20 and sent its place, 22984,51041 in the zone. It is 6,600 units on foot from
        /// the south end of the middle, 37,100 from the Albion keep and 42,700 from the north end.
        /// </summary>
        public static readonly (int X, int Y) HiberniaHill = (417792 + 22984, 286720 + 51041);

        /// <summary>
        /// Where Hibernia starts since 2026-11-21: in front of Dun Crauchon, its keep in the zone
        /// (the keep's middle is at 425877,318720), 2,400 units out on the way to the middle. The
        /// owner, after an afternoon of roams from the hill: "hibernia could start at Dun Crauchon
        /// to balance the starting location". From the hill the south end of the middle was 6,600
        /// units away, against 33,700 for Albion and 24,800 from Midgard's start to the north end,
        /// so Hibernia was at every first fight and was fought on its own doorstep. From here it
        /// is 26,400 on foot to the south end and 33,000 to the north end (the installed navmesh),
        /// and both are reached directly: its first team goes south first, its second north.
        /// </summary>
        public static readonly (int X, int Y) DunCrauchon = (427772, 320193);

        /// <summary>
        /// Albion and Midgard at their portal keeps, Hibernia at Dun Crauchon. "At" a keep is in front
        /// of its outer gate, some 1,800 units out on the way to the middle: the way from the
        /// middle of either keep goes through two keep doors (800 and 1,100 units from it), and in
        /// the first roam of 2026-11-20 the four teams that started inside stood there for eight
        /// minutes while Hibernia's two walked the middle. A bot does not open a keep door.
        /// </summary>
        public static readonly IReadOnlyList<PvpRoamStart> EmainStarts =
        [
            new(eRealm.Albion, (473395, 342169), [South, North]),
            new(eRealm.Midgard, (472166, 297011), [North, South]),
            new(eRealm.Hibernia, DunCrauchon, [South, North], [North, South]),
        ];

        // Thidranki (region 252, M9.7 step 5): three portal keeps round a middle, 33,700 to 40,900
        // units from each other on foot, and a neutral keep with its guards between them
        // (33089,38271). Each realm starts 2,500 units in front of its portal keep, on the way to
        // the middle. Three points to walk between, one between each two realms and none within
        // 8,500 of the neutral keep: HM between Hibernia and Midgard, HA between Hibernia and
        // Albion, MA between Midgard and Albion. A realm's first team goes round them one way and
        // its second the other, so that the six meet two by two: at their first points Hibernia's
        // first and Midgard's second (16,800 and 17,600 to walk), Hibernia's second and Albion's
        // first (15,400 and 20,300), Midgard's first and Albion's second (12,300 and 17,600).
        // Every leg checked on the installed navmesh, 2026-11-21: each is within 8% of the straight line.
        public static readonly (int X, int Y) ThidHM = (36207, 21468), ThidHA = (26000, 33500), ThidMA = (47000, 36500);

        public static readonly IReadOnlyList<PvpRoamStart> ThidrankiStarts =
        [
            new(eRealm.Albion, (36585, 49967), [ThidHA, ThidHM, ThidMA], [ThidMA, ThidHM, ThidHA]),
            new(eRealm.Midgard, (51955, 26040), [ThidMA, ThidHA, ThidHM], [ThidHM, ThidHA, ThidMA]),
            new(eRealm.Hibernia, (19844, 20271), [ThidHM, ThidMA, ThidHA], [ThidHA, ThidMA, ThidHM]),
        ];

        public static readonly PvpRoamMap Emain = new("emain", "Emain Macha", EmainRegion, EmainStarts);
        public static readonly PvpRoamMap Thidranki = new("thid", "Thidranki", 252, ThidrankiStarts);

        /// <summary>The map a word stands for ("thid", "thidranki", "emain"); null for a word that names none.</summary>
        public static PvpRoamMap MapFor(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "thid" or "thidranki" => Thidranki,
            "emain" or "emainmacha" => Emain,
            _ => null,
        };

        private static volatile PvpRoamMap _map = Emain;
        private static volatile List<PvpRoamTeam> _teams = [];
        // No bot team comes in below this realm level: the average of the player's group, when there is a player.
        private static int _floor = 1;
        private static Timer _timer;
        private static int _ticking;
        private static long _started;
        private static string _lastError;
        private static readonly List<string> Events = [];
        private static PvpRecording _recording;

        public static bool Running => _teams.Count > 0;

        /// <summary>The names of a realm's teams: "mid-main" and "mid-main#2".</summary>
        public static string TeamName(string groupId, int number) => number <= 1 ? groupId : $"{groupId}#{number}";

        /// <summary>Whether a team that comes on a fight joins it: one draw, <paramref name="roll"/> from 0 to 99.</summary>
        public static bool Joins(int addChancePercent, int roll) => roll < addChancePercent;

        /// <summary>
        /// Whether an enemy team is in a fight this team has come on from outside, which is what
        /// the draw is for: it fights somebody else, this team is not fighting it, and that
        /// somebody is not one this team is fighting (then the enemy team came into this team's
        /// fight, and is simply an enemy). The somebody may be this team's realm-mate or a team
        /// of the third realm: "should we assjam or not an ongoing fight. Most of the respected
        /// 8v8 team were not." Until 2026-11-21 the draw was for a realm-mate's fight only, and
        /// in the first roam in Thidranki each of the two 8v8 fights had a team of the third
        /// realm in it within a minute.
        /// </summary>
        public static bool ComesOnAFight(bool theyFightSomebodyElse, bool thatSomebodyIsInOurFight, bool weFightThemAlready) =>
            theyFightSomebodyElse && !thatSomebodyIsInOurFight && !weFightThemAlready;

        /// <summary>
        /// A fight left alone is left alone by both sides: the team that drew "leaves them to it"
        /// does not attack, and the team it left to its fight does not turn on it for walking
        /// past, unless one of them does attack the other, or the player picks one of them as a
        /// target. Until 2026-11-21 only the first half held: in the owner's first roam the second
        /// Hibernian team left an Albion team to the owner's and was attacked by it eight seconds
        /// later, and the owner's companions went for a Midgard team that had left them alone.
        /// </summary>
        public static bool LeavesAlone(bool weLeftThemToIt, bool theyLeftUsToIt, bool theyAttackUs, bool ourPlayerAimsAtThem) =>
            (weLeftThemToIt || theyLeftUsToIt) && !theyAttackUs && !ourPlayerAimsAtThem;

        /// <summary>
        /// A team of bots that has left a fight near it to its realm-mate, and has nobody of its
        /// own to fight, waits where the draw fell (some 9,000 units off) until that fight is
        /// over. Its road is the fight's road: in the first roam with the half-minute gap
        /// (2026-11-21) the second Hibernian team drew "leaves them to it" at 142 s, walked on
        /// into the fight and was in it at 166 s, with the second Midgard team that had done the
        /// same from the other side.
        /// </summary>
        public static bool WaitsOutAFight(bool leftAFightNearToIt, int enemiesToFight, bool ledByAPlayer) =>
            leftAFightNearToIt && enemiesToFight == 0 && !ledByAPlayer;

        /// <summary>
        /// Which enemy teams the player's own group reckons with in a roam: one it is fighting,
        /// one that attacks it, and one the player has picked a member of as a target, within
        /// the range the group forms up at. Any other is passed by: the companions stay in the
        /// clump they travel in behind the player and the Bard keeps its speed song. The owner,
        /// 2026-11-21: "When /8v8 roam, group should not spread / bard play endurance if we do
        /// not engage". In a single fight (lane, basin) there is one enemy and it is the fight:
        /// there the group takes its rows on sight, as before.
        /// </summary>
        public static bool PlayerTakesItOn(bool fightingItAlready, bool itAttacksUs, bool playerAimsAtItInReach) =>
            fightingItAlready || itAttacksUs || playerAimsAtItInReach;

        /// <summary>
        /// A team of bots that is in a fight takes on nobody new: a group that walks by is left
        /// alone until it attacks. The owner, 2026-11-21: "Let say mids are fighting alb and I
        /// walk by. Neither mids or albs will try to attack my group, as they are already
        /// fighting. Only if I decide to add on their fight, they will need to react." Until then
        /// only a team of bots that had drawn "leaves them to it" was spared; the player's group,
        /// which makes no draw, was attacked by both.
        /// </summary>
        public static bool TooBusyFor(int teamsItIsFighting, bool fightingThatOne, bool thatOneAttacksIt) =>
            teamsItIsFighting > 0 && !fightingThatOne && !thatOneAttacksIt;

        /// <summary>A team's fight is over when it has had no enemy to reckon with for this long: twelve seconds.</summary>
        public const int FightOverAfterMilliseconds = 12_000;

        public static bool FightIsOver(long millisecondsWithNoEnemy) => millisecondsWithNoEnemy > FightOverAfterMilliseconds;

        /// <summary>
        /// A team that left a fair fight alone does not go on watching when the other side adds:
        /// with two teams of one realm on its realm-mate, it joins after all. In the roam of
        /// 2026-11-21 a Midgard team stood 8,000 units off while its realm-mate fought two
        /// Hibernian teams, the second of which had drawn "joins". One team of each other realm
        /// on its realm-mate is a three-way fight and nobody's sixteen against eight: in the
        /// second roam in Thidranki that was taken for an add, and the fight had four teams.
        /// </summary>
        /// <param name="itsRealmMateAddedItself">
        /// The realm-mate is in that fight by its own draw: it jumped two teams that were
        /// fighting, has two of one realm on it for that, and is not owed any help. In the
        /// owner's roam of 2026-11-21 this was most of the sixteen times the rule fired.
        /// </param>
        public static bool EvensTheOdds(int mostTeamsOfOneRealmOnItsRealmMate, bool itsRealmMateAddedItself = false) =>
            mostTeamsOfOneRealmOnItsRealmMate >= 2 && !itsRealmMateAddedItself;

        private static int MostOfOneRealm(IEnumerable<PvpRoamTeam> teams) =>
            teams.GroupBy(team => team.Realm).Select(realm => realm.Count()).DefaultIfEmpty(0).Max();

        /// <summary>The squad of the team a player leads in the roam; null for a player who leads none.</summary>
        public static PvpSquad SquadOf(GamePlayer player) =>
            player == null ? null : _teams.FirstOrDefault(team => team.Leader == player)?.Team.Squad;

        /// <summary>Where a team of this realm starts, on the ground.</summary>
        public static Point3D StartSpot(PvpRoamMap map, eRealm realm)
        {
            PvpRoamStart start = map.StartOf(realm);
            var nav = PathfindingProvider.Instance;
            Zone zone = WorldMgr.GetRegion(map.Region)?.GetZone(start.Point.X, start.Point.Y);
            System.Numerics.Vector3 point = zone != null && nav.GetFloorBeneath(zone, new System.Numerics.Vector3(start.Point.X, start.Point.Y, 20_000), 20_000, nav.DefaultFilters)
                is { } floor && floor.Z < 19_999 ? floor : new System.Numerics.Vector3(start.Point.X, start.Point.Y, 4_000);
            return new Point3D((int)point.X, (int)point.Y, (int)point.Z);
        }

        /// <summary>
        /// Where a player who releases goes: the start of the team it leads in a running roam
        /// ("Albion would spawn (and release) at their principal keep"); null for anybody else,
        /// who releases as the game has it. The bind point is in another region, and a player
        /// who left the zone was a player who had left the game: the roam ended.
        /// </summary>
        public static (ushort Region, Point3D Spot)? ReleasePoint(GamePlayer player) =>
            player != null && _teams.Any(team => team.Leader == player) ? (_map.Region, StartSpot(_map, player.Realm)) : null;

        /// <summary>
        /// Whether a team is wiped: nobody alive, or, for the player's, every companion dead and
        /// the player dead or back at the start (released). A player who releases while
        /// companions still fight waits for them at the start; they come to the player if they win.
        /// </summary>
        public static bool IsWiped(int botsAlive, bool hasPlayer, bool playerAlive, bool playerAtStart) =>
            botsAlive == 0 && (!hasPlayer || !playerAlive || playerAtStart);

        /// <summary><c>/8v8 roam</c>: the player is taken to the start of its realm and leads the first team of it.</summary>
        /// <param name="own">The player's group when it is not the realm's main one (PvpBuildCatalog.Variants).</param>
        public static void StartFor(GamePlayer player, Action<string> say, PvpGroupTemplate own = null, PvpRoamMap map = null)
        {
            map ??= Emain;
            if (own != null && own.Realm != player.Realm)
                own = null;
            if (PvpScrim.Running)
                throw new InvalidOperationException("A fight or a roam is already running. /8v8 stop ends it.");
            if (PvpPlayerReady.EnterReal(player) is { } set)
                say(set);
            PvpScrim.TravelThen(player, map.Region, StartSpot(map, player.Realm), say, () =>
            {
                try
                {
                    Start(2, player.InternalID, player, own, map);
                    say($"The roam is on: two groups of each realm in {map.Name}, yours with you here. Kills earn realm points that are kept, " +
                        "no timer is reset, and a wipe puts your group back here (so does /release). Your group travels behind you until you " +
                        "target an enemy within sight or one attacks you; then it takes its rows and fights. /8v8 ranks, /8v8 stop.");
                }
                catch (Exception exception)
                {
                    say("The roam could not start: " + exception.Message);
                }
            });
        }

        /// <param name="lineups">Groups to use for their realms' teams of bots in place of the main ones (a test of a line-up; null: the main ones).</param>
        public static object Start(int teamsPerRealm, string owner, GamePlayer leader = null, PvpGroupTemplate own = null, PvpRoamMap map = null,
            IReadOnlyList<PvpGroupTemplate> lineups = null)
        {
            map ??= Emain;
            if (PvpScrim.Running)
                throw new InvalidOperationException("A scrim or a session is running. Stop it first.");
            PvpArena.Clear();
            PvpWeights.Reload(AppContext.BaseDirectory);
            _map = map;
            int cleared = PvpArena.ClearMonsters(map.Region, map.Starts.Select(start => start.Point));
            if (cleared > 0)
                Log.Info($"8v8 roam: {cleared} monsters taken out of {map.Name}");
            var teams = new List<PvpRoamTeam>();
            long now = GameLoop.GameLoopTime;
            PvpRealWeights real = PvpWeights.Current.Real;
            // The player's own team first: the others are made no lower in rank than it is (PvpRealWeights.EnemyKeepsUp).
            foreach (PvpRoamStart start in map.Starts.OrderBy(entry => leader != null && entry.Realm == leader.Realm ? 0 : 1))
            {
                PvpGroupTemplate template = lineups?.FirstOrDefault(group => group.Realm == start.Realm) ?? PvpBuildCatalog.Groups.First(group => group.Realm == start.Realm);
                for (int number = 1; number <= Math.Clamp(teamsPerRealm, 1, 2); number++)
                {
                    bool led = leader != null && start.Realm == leader.Realm && number == 1;
                    var team = new PvpRoamTeam
                    {
                        Name = TeamName(template.Id, number), Template = led && own != null ? own : template, Realm = start.Realm, Start = start,
                        // The player's companions are kept under the player; every other team under its own name.
                        Roster = led ? owner : PvpRealmPoints.EnemyRoster(owner, TeamName(template.Id, number)),
                        // A realm's teams of bots set off one after the other; the player's own goes when the player does.
                        LeavesAt = now + 1000L * PvpRealWeights.SetsOffAfterSeconds(teams.Count(other => other.Realm == start.Realm && other.Leader == null), real),
                    };
                    team.Team.Leader = led ? leader : null;
                    team.Team.Route = start.RouteOf(number);
                    _floor = leader == null || teams.Count == 0 ? 1 : PvpRealmPoints.EnemyLevel(1,
                        teams[0].Team.Bots.Select(bot => bot.RealmLevel).Append(leader.RealmLevel).ToList(), PvpWeights.Current.Real.EnemyKeepsUp);
                    Spawn(team, now);
                    teams.Add(team);
                }
            }
            _teams = teams;
            _started = now;
            // One recording for the session (M9.7 step 4): every team under its own name.
            _recording = PvpRecorder.BeginRoam((leader != null ? "roam-play" : "roam") + (map == Emain ? string.Empty : "-" + map.Id),
                teams.Select(team => (team.Name, team.Template.Id, team.Realm, team.Team.Members)).ToList());
            lock (Events) Events.Clear();
            _timer?.Dispose();
            _timer = new Timer(_ => NpcService.Instance.Post(Tick, 0), null, 1_000, 1_000);
            return Status();
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            List<PvpRoamTeam> teams = _teams;
            _teams = [];
            // A test run may have switched the effect fault back on (roam-start StaleCopies=off): never past its own roam.
            EffectListComponent.RetireStaleCopies = true;
            PvpRecording recording = Interlocked.Exchange(ref _recording, null);
            recording?.Finish(new
            {
                k = "end", t = recording.Now, Seconds = (int)((GameLoop.GameLoopTime - _started) / 1000),
                teams = teams.Select(team => new { name = team.Name, team.Fights, team.Kills, team.Deaths, team.Wipes, team.Joined, team.LeftAlone, team.Earned }),
            });
            foreach (PvpRoamTeam team in teams)
                PvpArena.Remove(team.All);
        }

        private static void Spawn(PvpRoamTeam team, long now)
        {
            PvpArena.Remove(team.All);
            team.All.Clear();
            GamePlayer leader = team.Leader;
            // The player's seven are put around the player, who stands at the start; a team of bots around its start.
            team.Team.Bots = leader != null
                ? PvpArena.SpawnCompanions(leader, 0, team.All, team.Template, true).ToList()
                : PvpArena.SpawnGroup(team.Template, _map.Region, team.Start.Point.X, team.Start.Point.Y, 0, team.All, team.Roster, _floor).ToList();
            team.Team.Home = team.Start.Point;
            team.Team.Waypoint = 0;
            team.Team.NewMatch(leader);
            team.Team.Squad = new PvpSquad(team.Team.Bots, leader != null ? [leader] : null);
            foreach (GameBot bot in team.Team.Bots)
            {
                if (bot.Brain is BotBrain brain)
                    brain.PvpSelfBuffsOnly = true;
            }
            team.Paid.Clear();
            team.Fighting.Clear();
            team.Draws.Clear();
            team.Waiting = false;
            (team.DeadSince, team.Reforming) = (0, false);
            (team.State, team.StateSince, team.ContactTick, team.LastEnemyNearTick) = (leader != null ? "Roaming" : "Buffing", now, 0, now);
        }

        /// <summary>
        /// A wiped team stands up at its start: bots as a new group of the same eight; a player
        /// alive again there, without sickness, with the seven around.
        /// </summary>
        private static void StandUp(PvpRoamTeam team, long now)
        {
            if (team.Leader is { } leader)
            {
                Point3D spot = StartSpot(_map, team.Realm);
                if (!leader.IsAlive)
                {
                    leader.TempProperties.SetProperty(GamePlayer.RESURRECT_REZ_SICK_EFFECTIVENESS, 0.0);
                    leader.StopReleaseTimer();
                    leader.Notify(GamePlayerEvent.Revive, leader, null);
                }
                (leader.Health, leader.Mana, leader.Endurance) = (leader.MaxHealth, leader.MaxMana, leader.MaxEndurance);
                // A player who released is there already.
                if (!leader.IsWithinRadius(spot, 1_500))
                    leader.MoveTo(_map.Region, spot.X, spot.Y, spot.Z, leader.Heading);
                Tell(team, "Your group stands up at its start.");
            }
            Spawn(team, now);
            team.LeavesAt = now + 1000L * PvpWeights.Current.Real.BuffsAtStartSeconds;
            PvpRecorder.Regroup(_recording, team.Name, team.Team.Members);
        }

        private static void Tell(PvpRoamTeam team, string what)
        {
            if (team.Leader?.Client?.IsPlaying == true)
                team.Leader.Out.SendMessage("[8v8] " + what, PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
        }

        /// <summary>For the log and the status; <paramref name="row"/>, when given, is what the recording keeps of it.</summary>
        private static void Say(string what, object row = null)
        {
            Log.Info("8v8 roam: " + what);
            if (_recording is { } recording)
                recording.Line(new { k = "ev", t = recording.Now, text = what, ev = row });
            lock (Events)
            {
                Events.Add($"{(GameLoop.GameLoopTime - _started) / 1000,5}s {what}");
                if (Events.Count > 400)
                    Events.RemoveAt(0);
            }
        }

        private static void Tick(int _)
        {
            if (Interlocked.Exchange(ref _ticking, 1) != 0)
                return;
            try
            {
                List<PvpRoamTeam> teams = _teams;
                long now = GameLoop.GameLoopTime;
                PvpRealWeights real = PvpWeights.Current.Real;
                // The player has left the game: the roam is over.
                if (teams.Any(team => team.Leader is { } leader && (leader.ObjectState != GameObject.eObjectState.Active || leader.Client?.IsPlaying != true)))
                {
                    Stop();
                    return;
                }
                foreach (PvpRoamTeam team in teams)
                    Step(team, teams, now, real);
                // A monster that was dead when the roam began has come back: once a minute.
                if ((now - _started) / 1000 % 60 == 59)
                    PvpArena.ClearMonsters(_map.Region, _map.Starts.Select(start => start.Point));
                if (_recording is { } recording)
                {
                    foreach (PvpRoamTeam team in teams)
                    {
                        if (team.Quiet && (now - _started) / 1000 % 5 != 0)
                            continue;
                        (double x, double y) = team.Centre();
                        recording.Line(new
                        {
                            k = "team", t = recording.Now, name = team.Name, state = team.State, x = (int)x, y = (int)y, alive = team.Alive,
                            wp = team.Team.Waypoint, fighting = team.Fighting.Select(other => other.Name).ToList(),
                        });
                    }
                    recording.Snapshot(name => teams.FirstOrDefault(team => team.Name == name)?.Quiet != false);
                }
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                Log.Error("8v8 roam tick failed", exception);
                Stop();
            }
            finally
            {
                Volatile.Write(ref _ticking, 0);
            }
        }

        /// <summary>True while a living team of another realm is within this of the team's start point.</summary>
        private static bool EnemyNearStart(PvpRoamTeam team, List<PvpRoamTeam> teams, int within) =>
            teams.Any(other => other.Realm != team.Realm && other.Team.Members.Any(member => member.IsAlive &&
                member.IsWithinRadius(new Point2D(team.Start.Point.X, team.Start.Point.Y), within)));

        /// <summary>
        /// Two teams are in a fight with each other when somebody of one is in combat and aimed
        /// at somebody of the other. Until 2026-11-21 it was enough for one of them to be in
        /// combat, with anybody, and the other to be within reach: a team standing by a fight was
        /// counted in it and paid for its deaths (the owner: "I think I am also gaining rps for
        /// just being close to the fight").
        /// </summary>
        private static bool AtEachOther(PvpRoamTeam a, PvpRoamTeam b)
        {
            IReadOnlyList<GameLiving> ours = a.Team.Members, theirs = b.Team.Members;
            return ours.Any(member => IsAt(member.IsAlive, member.InCombat, member.TargetObject is GameLiving aimed && theirs.Contains(aimed))) ||
                   theirs.Any(member => IsAt(member.IsAlive, member.InCombat, member.TargetObject is GameLiving aimed && ours.Contains(aimed)));
        }

        /// <summary>Whether a member is fighting the other team: alive, in combat, and its target one of theirs.</summary>
        public static bool IsAt(bool alive, bool inCombat, bool aimedAtOneOfTheirs) => alive && inCombat && aimedAtOneOfTheirs;

        private static bool Near(PvpRoamTeam a, PvpRoamTeam b, int within)
        {
            (double ax, double ay) = a.Centre();
            (double bx, double by) = b.Centre();
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) <= within;
        }

        private static void Step(PvpRoamTeam team, List<PvpRoamTeam> teams, long now, PvpRealWeights real)
        {
            // Nobody left: it stands up at its start point after a wait, as a new group of the same eight.
            if (team.Reforming || IsWiped(team.Team.Bots.Count(bot => bot.IsAlive), team.Leader != null, team.Leader?.IsAlive == true,
                    team.Leader?.IsWithinRadius(new Point2D(team.Start.Point.X, team.Start.Point.Y), real.StartIsSafeWithin) == true))
            {
                if (team.State != "Down" && team.Reforming)
                {
                    // Gone from the field at once (nobody is near to see it), and back at its start after the wait of a wipe.
                    (team.State, team.StateSince) = ("Down", now);
                    team.Fighting.Clear();
                    PvpArena.Remove(team.All);
                }
                else if (team.State != "Down")
                {
                    (team.State, team.StateSince) = ("Down", now);
                    team.Wipes++;
                    // Its last to fall are paid for before the fight is forgotten.
                    Pay(team, real, now);
                    team.Fighting.Clear();
                    Say($"{team.Name} is wiped (its {team.Wipes}.)", new { what = "wipe", team = team.Name });
                    Tell(team, team.Leader?.IsAlive == true
                        ? $"Your group is down. It stands up here in about {real.WipeStandsUpAfterSeconds} seconds."
                        : $"Your group is wiped. You stand up at your start in about {real.WipeStandsUpAfterSeconds} seconds, or release to go there now.");
                }
                else if (PvpRealWeights.StandsUp((now - team.StateSince) / 1000.0, EnemyNearStart(team, teams, real.StartIsClearBeyond), real,
                             team.Leader is { IsAlive: true }))
                {
                    StandUp(team, now);
                    Say($"{team.Name} stands up at its start", new { what = "standup", team = team.Name });
                }
                team.Quiet = true;
                return;
            }

            // The teams of another realm that are near, and which of them it is fighting.
            List<PvpRoamTeam> others = teams.Where(other => other.Realm != team.Realm && other.Alive > 0 && !other.Reforming && Near(team, other, 9_000)).ToList();
            team.Quiet = others.Count == 0 && team.ContactTick == 0;
            bool inCombat = team.Team.Members.Any(member => member.IsAlive && member.InCombat);
            team.Fighting.RemoveWhere(other => other.Alive == 0 || !Near(team, other, 6_000));
            foreach (PvpRoamTeam other in others)
            {
                // This enemy team is already in a fight, with this team's realm-mate or with a team of the
                // third realm: this team joins, or it leaves them to it and waits. One draw for the fight.
                List<PvpRoamTeam> theirFight = teams.Where(third => third != team && third != other && third.Alive > 0 &&
                    (other.Fighting.Contains(third) || third.Fighting.Contains(other))).ToList();
                bool onAFight = ComesOnAFight(theirFight.Count > 0, theirFight.Any(team.Fighting.Contains), team.Fighting.Contains(other));
                // (The player's own team does what the player does.)
                if (onAFight && team.Leader == null && !team.Draws.ContainsKey(other))
                {
                    // What it drew for another team of the same fight holds for this one.
                    bool? drawn = team.Draws.Where(draw => theirFight.Contains(draw.Key)).Select(draw => (bool?)draw.Value).FirstOrDefault();
                    bool joins = drawn ?? Joins(real.AddChancePercent, Random.Shared.Next(100));
                    team.Draws[other] = joins;
                    if (drawn == null)
                    {
                        if (joins) team.Joined++; else team.LeftAlone++;
                        PvpRoamTeam mate = theirFight.FirstOrDefault(third => third.Realm == team.Realm);
                        Say(mate != null
                                ? $"{team.Name} comes on {mate.Name} fighting {other.Name}: it {(joins ? "joins" : "leaves them to it")}"
                                : $"{team.Name} comes on {other.Name} fighting {string.Join(" and ", theirFight.Select(third => third.Name))}: it {(joins ? "joins" : "waits for it to end")}",
                            new { what = "draw", team = team.Name, mate = mate?.Name, other = other.Name, joins });
                    }
                }
                else if (theirFight.Count == 0)
                    team.Draws.Remove(other);
            }
            // It left a fair fight to its realm-mate; the other side has added, so it does too.
            if (team.Leader == null && teams.FirstOrDefault(own => own != team && own.Realm == team.Realm && own.Alive > 0 &&
                    EvensTheOdds(MostOfOneRealm(own.Fighting), own.Draws.Any(draw => draw.Value && own.Fighting.Contains(draw.Key))) && own.Fighting.Any(enemy => team.Draws.TryGetValue(enemy, out bool joined) && !joined)) is { } pressed)
            {
                foreach (PvpRoamTeam enemy in pressed.Fighting)
                    team.Draws[enemy] = true;
                team.Joined++;
                Say($"{team.Name} joins {pressed.Name} after all: two teams of one realm are on it",
                    new { what = "evens", team = team.Name, mate = pressed.Name, others = pressed.Fighting.Select(enemy => enemy.Name).ToList() });
            }
            // Left alone, unless they come for it: by its draw, or because they stand at their own start.
            // (A player who only looks at one of them is not attacking: a player counts once it is in combat.)
            bool Attacks(PvpRoamTeam other) => other.Team.Members.Any(member => member.IsAlive && member.TargetObject is GameLiving aimed &&
                (member is not GamePlayer || member.InCombat) &&
                team.Team.Members.Contains(aimed) && member.IsWithinRadius(aimed, 2_000));
            double FromItsStart(PvpRoamTeam other)
            {
                (double x, double y) = other.Centre();
                return Math.Sqrt(Math.Pow(x - other.Start.Point.X, 2) + Math.Pow(y - other.Start.Point.Y, 2));
            }
            List<PvpRoamTeam> foes = others.Where(other =>
            {
                bool attacks = Attacks(other);
                if (LeavesAlone(team.Draws.TryGetValue(other, out bool joins) && !joins, other.Draws.TryGetValue(team, out bool theyJoin) && !theyJoin, attacks,
                        team.Leader?.TargetObject is GameLiving aimed && other.Team.Members.Contains(aimed)))
                    return false;
                // Already in a fight, a team of bots lets anybody else walk by.
                if (team.Leader == null && TooBusyFor(team.Fighting.Count, team.Fighting.Contains(other), attacks))
                    return false;
                if (team.Leader is { } leader)
                    return PlayerTakesItOn(team.Fighting.Contains(other), attacks,
                        leader.TargetObject is GameLiving picked && picked.IsAlive && other.Team.Members.Contains(picked) &&
                        leader.IsWithinRadius(picked, PvpFormation.FormRange));
                return !PvpRealWeights.LeftAtItsStart(FromItsStart(other), team.Fighting.Contains(other), attacks, real);
            }).ToList();
            List<GameLiving> enemies = foes.SelectMany(other => other.Team.Members).Where(member => member.IsAlive).ToList();
            foreach (PvpRoamTeam other in foes)
            {
                if (AtEachOther(team, other) && Near(team, other, PvpScrim.EngageRange) && team.Fighting.Add(other))
                {
                    team.Fights++;
                    Say($"{team.Name} ({team.Alive}) fights {other.Name} ({other.Alive})",
                        new { what = "fight", team = team.Name, other = other.Name, alive = team.Alive, theirs = other.Alive });
                }
            }

            Pay(team, real, now);

            if (enemies.Count > 0)
                team.LastEnemyNearTick = now;
            if (team.ContactTick == 0 && inCombat && enemies.Count > 0)
            {
                team.ContactTick = now;
                team.Rested = false;
                team.State = "Fighting";
                team.Team.Squad.FightStarted = true;
                foreach (GameBot bot in team.Team.Bots)
                {
                    if (bot.Brain is BotBrain brain)
                        brain.PvpSelfBuffsOnly = false;
                }
            }
            // The fight is over when no enemy has been near for a while: a fresh squad for the next one.
            else if (team.ContactTick != 0 && !inCombat && FightIsOver(now - team.LastEnemyNearTick))
            {
                team.ContactTick = 0;
                team.State = "Roaming";
                team.Fighting.Clear();
                team.Team.Squad = new PvpSquad(team.Team.Bots, team.Leader != null ? [team.Leader] : null);
                team.Team.NewMatch(team.Leader);
            }

            if (team.State == "Buffing")
            {
                // A member that has buffed itself dry rests and goes on, as before a scrim.
                foreach (GameBot bot in team.Team.Bots)
                {
                    if (bot.IsAlive && !bot.IsCasting && bot.MaxMana > 0 && bot.ManaPercent < 40)
                        bot.Mana = bot.MaxMana;
                }
                if (now >= team.LeavesAt)
                {
                    foreach (GameBot bot in team.Team.Bots.Where(bot => bot.IsAlive))
                        (bot.Health, bot.Mana, bot.Endurance) = (bot.MaxHealth, bot.MaxMana, bot.MaxEndurance);
                    team.State = "Roaming";
                    Say($"{team.Name} sets off", new { what = "setoff", team = team.Name });
                }
            }

            if (enemies.Count > 0)
            {
                team.Team.Squad.Decide(enemies);
                team.Team.Engage(enemies);
            }
            // A lull is not the end of a fight: with nobody to reckon with for a moment (the player between two
            // targets, the enemy dead or asleep) the group's Bard went to its speed song and back, twice in the
            // owner's fight of 2026-11-21, each change a three-second cast and twenty seconds without the other song.
            else if (team.Leader != null && FightIsOver(now - team.LastEnemyNearTick))
                team.Team.Squad.LoseSight();
            // "/8v8 call retreat": the player's group fights nobody and runs with the player.
            if (team.Leader != null && team.Team.Squad.RetreatsTo != null)
            {
                team.Team.TravelWithLeader([]);
                return;
            }
            // After a fight a team of bots rests when somebody is low, before it walks on (PvpRealWeights.TeamRestsBelowPercent).
            bool resting = false;
            if (team.Leader == null && !inCombat && enemies.Count == 0 && others.Count == 0 && team.State == "Roaming")
            {
                List<GameBot> up = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
                int lowest = up.Count == 0 ? 100 : up.Min(bot => Math.Min((int)bot.HealthPercent, bot.MaxMana > 0 ? (int)bot.ManaPercent : 100));
                if (!team.Rested && PvpRealWeights.TeamRests(lowest, real))
                {
                    team.Rested = true;
                    team.Team.Squad.RestAsATeam(real.TeamRestsAtMostSeconds);
                    Say($"{team.Name} rests (its lowest member at {lowest}%)", new { what = "rest", team = team.Name, lowest });
                }
                resting = team.Team.Squad.Resting;
            }
            // After a fight a team of bots sees to its dead before it walks on: it raises them, or goes back to its start.
            GameBot body = null;
            if (team.Leader == null && !inCombat && enemies.Count == 0 && team.Team.Bots.Any(bot => !bot.IsAlive))
            {
                if (team.DeadSince == 0)
                    team.DeadSince = now;
                List<GameBot> living = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
                bool canRaise = living.Any(bot => bot.ResurrectionSpell != null);
                if (PvpRealWeights.Reforms(canRaise, (now - team.DeadSince) / 1000.0, real))
                {
                    team.Reforming = true;
                    Say($"{team.Name} goes back to its start to form up again ({team.Team.Bots.Count - living.Count} dead, {(canRaise ? "not raised in time" : "nobody to raise them")})",
                        new { what = "reform", team = team.Name, dead = team.Team.Bots.Count - living.Count, canRaise });
                    return;
                }
                (double x, double y) = team.Centre();
                body = team.Team.Bots.Where(bot => !bot.IsAlive).OrderBy(bot => (bot.X - x) * (bot.X - x) + (bot.Y - y) * (bot.Y - y)).First();
            }
            else
                team.DeadSince = 0;
            // The player's seven go where the player goes; a team of bots walks its route, or waits out a fight it has left alone.
            // (Once it waits it goes on waiting while that fight is within 12,000: at 9,000 it would walk and stop by turns.)
            bool waits = WaitsOutAFight(team.Draws.Any(draw => !draw.Value && draw.Key.Alive > 0 && Near(team, draw.Key, team.Waiting ? 12_000 : 9_000)),
                enemies.Count, team.Leader != null);
            if (waits != team.Waiting)
            {
                team.Waiting = waits;
                if (waits)
                    Say($"{team.Name} waits where it is", new { what = "wait", team = team.Name });
            }
            if (team.Leader != null)
                team.Team.TravelWithLeader(enemies);
            else if (body != null)
                team.Team.GatherAt(body, 500);
            else if (resting)
                team.Team.Halt();
            else if (waits)
                team.Team.Halt();
            else if (team.State != "Buffing")
                team.Team.March(enemies, team.Start.Point, null, null, team.ContactTick);
            if (enemies.Count > 0)
            {
                team.Team.Squad.ClearPets();
                team.Team.Squad.StandOff();
                team.Team.Squad.KeepApart();
            }
        }

        /// <summary>Every death of the team not yet paid for gives the teams it was fighting their realm points, into their rosters.</summary>
        private static void Pay(PvpRoamTeam team, PvpRealWeights real, long now)
        {
            foreach (GameLiving dead in team.Team.Members)
            {
                if (dead.IsAlive)
                {
                    team.Paid.Remove(dead);
                    continue;
                }
                if (!team.Paid.Add(dead))
                    continue;
                team.Deaths++;
                List<(PvpRoamTeam Team, GameLiving Member)> near = team.Fighting
                    .SelectMany(killers => killers.Team.Members.Where(member => member.IsWithinRadius(dead, real.ShareWithin)).Select(member => (killers, member))).ToList();
                long share = PvpRealmPoints.Share(PvpRealmPoints.ValueOf(dead.Level, PvpRealmPoints.RealmLevelOf(dead)), near.Count, real.RealmPointRate);
                foreach (IGrouping<PvpRoamTeam, (PvpRoamTeam Team, GameLiving Member)> byTeam in near.GroupBy(entry => entry.Team))
                {
                    byTeam.Key.Kills++;
                    byTeam.Key.Earned += share * byTeam.Count();
                    var earned = new Dictionary<(string, int), long>();
                    foreach ((_, GameLiving member) in byTeam)
                    {
                        if (member is GamePlayer player)
                        {
                            // On the character, and in the roster beside the companions.
                            player.GainRealmPoints(share, false);
                            earned[(PvpRealmPoints.PlayerBuild, 0)] = share;
                        }
                        else if (member is GameBot bot)
                        {
                            bot.PvpPoints += share;
                            earned[(bot.PvpBuild.Id.ToLowerInvariant(), bot.PvpRosterPlace)] = share;
                        }
                    }
                    PvpRoster.Credit(AppContext.BaseDirectory, byTeam.Key.Roster, earned);
                    if (_recording is { } recording)
                        recording.Line(new { k = "ev", t = recording.Now, ev = new { what = "pay", dead = dead.Name, team = team.Name, to = byTeam.Key.Name, share, members = byTeam.Count() } });
                    Tell(byTeam.Key, $"{dead.Name} of {team.Name} is down: {share} realm points each.");
                }
            }
        }

        public static object Status()
        {
            List<PvpRoamTeam> teams = _teams;
            long now = GameLoop.GameLoopTime;
            List<string> events;
            lock (Events) events = Events.TakeLast(60).ToList();
            return new
            {
                Running,
                Map = _map.Name,
                Seconds = Running ? (now - _started) / 1000 : 0,
                Error = _lastError,
                Recording = _recording?.File,
                EffectListComponent.StaleCopiesRetired,
                // A check: effects on a member whose caster is in another team of the roam (a song is for its singer's group only).
                FromOtherTeams = teams.SelectMany(team => team.Team.Members.Where(member => member.IsAlive).SelectMany(member =>
                    member.effectListComponent.GetSpellEffects()
                        .Where(effect => effect?.SpellHandler?.Caster is GameLiving caster && !effect.SpellHandler.Spell.IsHarmful &&
                                         teams.Any(other => other != team && other.Realm == team.Realm && other.Team.Members.Contains(caster)))
                        .Select(effect => $"{member.Name} ({team.Name}) has {effect.SpellHandler.Spell.Name} of {effect.SpellHandler.Caster.Name}"))).Take(40).ToList(),
                Teams = teams.Select(team =>
                {
                    (double x, double y) = team.Centre();
                    return new
                    {
                        team.Name, Realm = team.Realm.ToString(), team.State, team.Waiting, team.Alive, At = $"{(int)x},{(int)y}",
                        Waypoint = team.Team.Waypoint, Moving = team.Team.Bots.Count(bot => bot.IsAlive && bot.IsMoving),
                        Fighting = team.Fighting.Select(other => other.Name).ToList(),
                        team.Fights, team.Kills, team.Deaths, team.Wipes, team.Joined, team.LeftAlone, team.Earned,
                        Rank = PvpRealmPoints.RankName((int)Math.Round(team.Team.Bots.Select(bot => bot.RealmLevel).DefaultIfEmpty(1).Average())),
                        Leader = team.Leader?.Name,
                    };
                }).ToList(),
                Events = events,
            };
        }
    }
}
