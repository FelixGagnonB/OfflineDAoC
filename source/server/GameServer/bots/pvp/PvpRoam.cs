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
    public sealed record PvpRoamStart(eRealm Realm, (int X, int Y) Point, (int X, int Y)[] Route);

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
        /// Albion and Midgard at their portal keeps, Hibernia on its hill. "At" a keep is in front
        /// of its outer gate, some 1,800 units out on the way to the middle: the way from the
        /// middle of either keep goes through two keep doors (800 and 1,100 units from it), and in
        /// the first roam of 2026-11-20 the four teams that started inside stood there for eight
        /// minutes while Hibernia's two walked the middle. A bot does not open a keep door.
        /// </summary>
        public static readonly IReadOnlyList<PvpRoamStart> EmainStarts =
        [
            new(eRealm.Albion, (473395, 342169), [South, North]),
            new(eRealm.Midgard, (472166, 297011), [North, South]),
            new(eRealm.Hibernia, HiberniaHill, [South, North]),
        ];

        private static volatile List<PvpRoamTeam> _teams = [];
        // No bot team comes in below this realm level: the average of the player's group, when there is a player.
        private static int _floor = 1;
        private static Timer _timer;
        private static int _ticking;
        private static long _started;
        private static string _lastError;
        private static readonly List<string> Events = [];

        public static bool Running => _teams.Count > 0;

        /// <summary>The names of a realm's teams: "mid-main" and "mid-main#2".</summary>
        public static string TeamName(string groupId, int number) => number <= 1 ? groupId : $"{groupId}#{number}";

        /// <summary>Whether a team that comes on its realm-mate's fight joins it: one draw, <paramref name="roll"/> from 0 to 99.</summary>
        public static bool Joins(int addChancePercent, int roll) => roll < addChancePercent;

        /// <summary>The squad of the team a player leads in the roam; null for a player who leads none.</summary>
        public static PvpSquad SquadOf(GamePlayer player) =>
            player == null ? null : _teams.FirstOrDefault(team => team.Leader == player)?.Team.Squad;

        /// <summary>Where a team of this realm starts, on the ground.</summary>
        public static Point3D StartSpot(eRealm realm)
        {
            PvpRoamStart start = EmainStarts.First(entry => entry.Realm == realm);
            var nav = PathfindingProvider.Instance;
            Zone zone = WorldMgr.GetRegion(EmainRegion)?.GetZone(start.Point.X, start.Point.Y);
            System.Numerics.Vector3 point = zone != null && nav.GetFloorBeneath(zone, new System.Numerics.Vector3(start.Point.X, start.Point.Y, 20_000), 20_000, nav.DefaultFilters)
                is { } floor && floor.Z < 19_999 ? floor : new System.Numerics.Vector3(start.Point.X, start.Point.Y, 4_000);
            return new Point3D((int)point.X, (int)point.Y, (int)point.Z);
        }

        /// <summary><c>/8v8 roam</c>: the player is taken to the start of its realm and leads the first team of it.</summary>
        public static void StartFor(GamePlayer player, Action<string> say)
        {
            if (PvpScrim.Running)
                throw new InvalidOperationException("A fight or a roam is already running. /8v8 stop ends it.");
            if (PvpPlayerReady.EnterReal(player) is { } set)
                say(set);
            PvpScrim.TravelThen(player, EmainRegion, StartSpot(player.Realm), say, () =>
            {
                try
                {
                    Start(2, player.InternalID, player);
                    say("The roam is on: two groups of each realm in Emain Macha, yours with you here. Kills earn realm points that are kept, " +
                        "no timer is reset, and a wipe puts your group back here. /8v8 ranks, /8v8 stop.");
                }
                catch (Exception exception)
                {
                    say("The roam could not start: " + exception.Message);
                }
            });
        }

        public static object Start(int teamsPerRealm, string owner, GamePlayer leader = null)
        {
            if (PvpScrim.Running)
                throw new InvalidOperationException("A scrim or a session is running. Stop it first.");
            PvpArena.Clear();
            PvpWeights.Reload(AppContext.BaseDirectory);
            var teams = new List<PvpRoamTeam>();
            long now = GameLoop.GameLoopTime;
            // The player's own team first: the others are made no lower in rank than it is (PvpRealWeights.EnemyKeepsUp).
            foreach (PvpRoamStart start in EmainStarts.OrderBy(entry => leader != null && entry.Realm == leader.Realm ? 0 : 1))
            {
                PvpGroupTemplate template = PvpBuildCatalog.Groups.First(group => group.Realm == start.Realm);
                for (int number = 1; number <= Math.Clamp(teamsPerRealm, 1, 2); number++)
                {
                    bool led = leader != null && start.Realm == leader.Realm && number == 1;
                    var team = new PvpRoamTeam
                    {
                        Name = TeamName(template.Id, number), Template = template, Realm = start.Realm, Start = start,
                        // The player's companions are kept under the player; every other team under its own name.
                        Roster = led ? owner : PvpRealmPoints.EnemyRoster(owner, TeamName(template.Id, number)),
                        // The second team of a realm sets off a minute and a half after the first.
                        LeavesAt = now + 45_000 + (number - 1) * 90_000,
                    };
                    team.Team.Leader = led ? leader : null;
                    team.Team.Route = start.Route;
                    _floor = leader == null || teams.Count == 0 ? 1 : PvpRealmPoints.EnemyLevel(1,
                        teams[0].Team.Bots.Select(bot => bot.RealmLevel).Append(leader.RealmLevel).ToList(), PvpWeights.Current.Real.EnemyKeepsUp);
                    Spawn(team, now);
                    teams.Add(team);
                }
            }
            _teams = teams;
            _started = now;
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
                : PvpArena.SpawnGroup(team.Template, EmainRegion, team.Start.Point.X, team.Start.Point.Y, 0, team.All, team.Roster, _floor).ToList();
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
                Point3D spot = StartSpot(team.Realm);
                if (!leader.IsAlive)
                {
                    leader.TempProperties.SetProperty(GamePlayer.RESURRECT_REZ_SICK_EFFECTIVENESS, 0.0);
                    leader.StopReleaseTimer();
                    leader.Notify(GamePlayerEvent.Revive, leader, null);
                    (leader.Health, leader.Mana, leader.Endurance) = (leader.MaxHealth, leader.MaxMana, leader.MaxEndurance);
                }
                leader.MoveTo(EmainRegion, spot.X, spot.Y, spot.Z, leader.Heading);
                Tell(team, "Your group stands up at its start.");
            }
            Spawn(team, now);
            team.LeavesAt = now + 45_000;
        }

        private static void Tell(PvpRoamTeam team, string what)
        {
            if (team.Leader?.Client?.IsPlaying == true)
                team.Leader.Out.SendMessage("[8v8] " + what, PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
        }

        private static void Say(string what)
        {
            Log.Info("8v8 roam: " + what);
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

        private static bool Near(PvpRoamTeam a, PvpRoamTeam b, int within)
        {
            (double ax, double ay) = a.Centre();
            (double bx, double by) = b.Centre();
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) <= within;
        }

        private static void Step(PvpRoamTeam team, List<PvpRoamTeam> teams, long now, PvpRealWeights real)
        {
            // Nobody left: it stands up at its start point after a wait, as a new group of the same eight.
            if (team.Alive == 0)
            {
                if (team.State != "Down")
                {
                    (team.State, team.StateSince) = ("Down", now);
                    team.Wipes++;
                    // Its last to fall are paid for before the fight is forgotten.
                    Pay(team, real, now);
                    team.Fighting.Clear();
                    Say($"{team.Name} is wiped (its {team.Wipes}.)");
                    Tell(team, $"Your group is wiped. You stand up at your start in about {real.WipeStandsUpAfterSeconds} seconds; do not release.");
                }
                else if (PvpRealWeights.StandsUp((now - team.StateSince) / 1000.0, EnemyNearStart(team, teams, real.StartIsClearBeyond), real))
                {
                    StandUp(team, now);
                    Say($"{team.Name} stands up at its start");
                }
                return;
            }

            // The teams of another realm that are near, and which of them it is fighting.
            List<PvpRoamTeam> others = teams.Where(other => other.Realm != team.Realm && other.Alive > 0 && Near(team, other, 9_000)).ToList();
            bool inCombat = team.Team.Members.Any(member => member.IsAlive && member.InCombat);
            team.Fighting.RemoveWhere(other => other.Alive == 0 || !Near(team, other, 6_000));
            foreach (PvpRoamTeam other in others)
            {
                // Its realm's other team is already fighting this one: it joins, or it leaves them to it. One draw.
                PvpRoamTeam mate = teams.FirstOrDefault(own => own != team && own.Realm == team.Realm && own.Alive > 0 && own.Fighting.Contains(other));
                // (The player's own team does what the player does.)
                if (mate != null && team.Leader == null && !team.Fighting.Contains(other) && !team.Draws.ContainsKey(other))
                {
                    bool joins = Joins(real.AddChancePercent, Random.Shared.Next(100));
                    team.Draws[other] = joins;
                    if (joins) team.Joined++; else team.LeftAlone++;
                    Say($"{team.Name} comes on {mate.Name} fighting {other.Name}: it {(joins ? "joins" : "leaves them to it")}");
                }
                else if (mate == null)
                    team.Draws.Remove(other);
            }
            // Left alone, unless they come for it.
            List<PvpRoamTeam> foes = others.Where(other => !(team.Draws.TryGetValue(other, out bool joins) && !joins) ||
                other.Team.Members.Any(member => member.IsAlive && member.TargetObject is GameLiving aimed && team.Team.Members.Contains(aimed) && member.IsWithinRadius(aimed, 2_000))).ToList();
            List<GameLiving> enemies = foes.SelectMany(other => other.Team.Members).Where(member => member.IsAlive).ToList();
            foreach (PvpRoamTeam other in foes)
            {
                if ((inCombat || other.Team.Members.Any(member => member.IsAlive && member.InCombat)) && Near(team, other, PvpScrim.EngageRange) && team.Fighting.Add(other))
                {
                    team.Fights++;
                    Say($"{team.Name} ({team.Alive}) fights {other.Name} ({other.Alive})");
                }
            }

            Pay(team, real, now);

            if (enemies.Count > 0)
                team.LastEnemyNearTick = now;
            if (team.ContactTick == 0 && inCombat && enemies.Count > 0)
            {
                team.ContactTick = now;
                team.State = "Fighting";
                team.Team.Squad.FightStarted = true;
                foreach (GameBot bot in team.Team.Bots)
                {
                    if (bot.Brain is BotBrain brain)
                        brain.PvpSelfBuffsOnly = false;
                }
            }
            // The fight is over when no enemy has been near for a while: a fresh squad for the next one.
            else if (team.ContactTick != 0 && !inCombat && now - team.LastEnemyNearTick > 12_000)
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
                    Say($"{team.Name} sets off");
                }
            }

            if (enemies.Count > 0)
            {
                team.Team.Squad.Decide(enemies);
                team.Team.Engage(enemies);
            }
            // The player's seven go where the player goes; a team of bots walks its route.
            if (team.Leader != null)
                team.Team.TravelWithLeader(enemies);
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
                Seconds = Running ? (now - _started) / 1000 : 0,
                Error = _lastError,
                Teams = teams.Select(team =>
                {
                    (double x, double y) = team.Centre();
                    return new
                    {
                        team.Name, Realm = team.Realm.ToString(), team.State, team.Alive, At = $"{(int)x},{(int)y}",
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
