using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.AI.Brain;
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
        public readonly HashSet<GameBot> Paid = [];
        public int Wipes, Kills, Deaths, Fights, Joined, LeftAlone;
        public long Earned;

        public int Alive => Team.Bots.Count(bot => bot.IsAlive);
        public (double X, double Y) Centre()
        {
            List<GameBot> living = Team.Bots.Where(bot => bot.IsAlive).ToList();
            if (living.Count == 0)
                living = Team.Bots;
            return living.Count == 0 ? (Start.Point.X, Start.Point.Y) : (living.Average(bot => bot.X), living.Average(bot => bot.Y));
        }
    }

    /// <summary>
    /// The roam (docs/PVP-8V8-ROADMAP.md, M9.7): several teams in one zone, in a session that
    /// goes on. The owner, 2026-11-20: "There could be 2 group for each realm. PvP ruleset is not
    /// enable on this so they would be fighting any of the opposite realm team"; "We revive at
    /// our start point"; Emain Macha, "Albion would spawn (and release) at their principal keep.
    /// Same for midgard. Hibernia ... on the hill near the tower in front of Albion Main Gate."
    /// This is the session for bot teams only, started from the control channel and read from
    /// its status; the player comes into it next.
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

        /// <summary>Albion and Midgard at their portal keeps, Hibernia on its hill.</summary>
        public static readonly IReadOnlyList<PvpRoamStart> EmainStarts =
        [
            new(eRealm.Albion, (475844, 343696), [South, North]),
            new(eRealm.Midgard, (474129, 295207), [North, South]),
            new(eRealm.Hibernia, HiberniaHill, [South, North]),
        ];

        private static volatile List<PvpRoamTeam> _teams = [];
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

        public static object Start(int teamsPerRealm, string owner)
        {
            if (PvpScrim.Running)
                throw new InvalidOperationException("A scrim or a session is running. Stop it first.");
            PvpArena.Clear();
            PvpWeights.Reload(AppContext.BaseDirectory);
            var teams = new List<PvpRoamTeam>();
            long now = GameLoop.GameLoopTime;
            foreach (PvpRoamStart start in EmainStarts)
            {
                PvpGroupTemplate template = PvpBuildCatalog.Groups.First(group => group.Realm == start.Realm);
                for (int number = 1; number <= Math.Clamp(teamsPerRealm, 1, 2); number++)
                {
                    var team = new PvpRoamTeam
                    {
                        Name = TeamName(template.Id, number), Template = template, Realm = start.Realm, Start = start,
                        Roster = PvpRealmPoints.EnemyRoster(owner, TeamName(template.Id, number)),
                        // The second team of a realm sets off a minute and a half after the first.
                        LeavesAt = now + 45_000 + (number - 1) * 90_000,
                    };
                    team.Team.Route = start.Route;
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
            team.Team.Bots = PvpArena.SpawnGroup(team.Template, EmainRegion, team.Start.Point.X, team.Start.Point.Y, 0, team.All, team.Roster, 1).ToList();
            team.Team.Home = team.Start.Point;
            team.Team.Waypoint = 0;
            team.Team.NewMatch(null);
            team.Team.Squad = new PvpSquad(team.Team.Bots);
            foreach (GameBot bot in team.Team.Bots)
            {
                if (bot.Brain is BotBrain brain)
                    brain.PvpSelfBuffsOnly = true;
            }
            team.Paid.Clear();
            team.Fighting.Clear();
            team.Draws.Clear();
            (team.State, team.StateSince, team.ContactTick, team.LastEnemyNearTick) = ("Buffing", now, 0, now);
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
                    team.Fighting.Clear();
                    Say($"{team.Name} is wiped (its {team.Wipes}.)");
                }
                else if (now - team.StateSince >= real.WipeStandsUpAfterSeconds * 1000)
                {
                    Spawn(team, now);
                    team.LeavesAt = now + 45_000;
                    Say($"{team.Name} stands up at its start");
                }
                return;
            }

            // The teams of another realm that are near, and which of them it is fighting.
            List<PvpRoamTeam> others = teams.Where(other => other.Realm != team.Realm && other.Alive > 0 && Near(team, other, 9_000)).ToList();
            bool inCombat = team.Team.Bots.Any(bot => bot.IsAlive && bot.InCombat);
            team.Fighting.RemoveWhere(other => other.Alive == 0 || !Near(team, other, 6_000));
            foreach (PvpRoamTeam other in others)
            {
                // Its realm's other team is already fighting this one: it joins, or it leaves them to it. One draw.
                PvpRoamTeam mate = teams.FirstOrDefault(own => own != team && own.Realm == team.Realm && own.Alive > 0 && own.Fighting.Contains(other));
                if (mate != null && !team.Fighting.Contains(other) && !team.Draws.ContainsKey(other))
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
                other.Team.Bots.Any(bot => bot.IsAlive && bot.TargetObject is GameBot aimed && team.Team.Bots.Contains(aimed) && bot.IsWithinRadius(aimed, 2_000))).ToList();
            List<GameLiving> enemies = foes.SelectMany(other => other.Team.Bots).Where(bot => bot.IsAlive).Cast<GameLiving>().ToList();
            foreach (PvpRoamTeam other in foes)
            {
                if ((inCombat || other.Team.Bots.Any(bot => bot.IsAlive && bot.InCombat)) && Near(team, other, PvpScrim.EngageRange) && team.Fighting.Add(other))
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
                team.Team.Squad = new PvpSquad(team.Team.Bots);
                team.Team.NewMatch(null);
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
            if (team.State != "Buffing")
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
            foreach (GameBot dead in team.Team.Bots)
            {
                if (dead.IsAlive)
                {
                    team.Paid.Remove(dead);
                    continue;
                }
                if (!team.Paid.Add(dead))
                    continue;
                team.Deaths++;
                List<(PvpRoamTeam Team, GameBot Bot)> near = team.Fighting
                    .SelectMany(killers => killers.Team.Bots.Where(bot => bot.IsWithinRadius(dead, real.ShareWithin)).Select(bot => (killers, bot))).ToList();
                long share = PvpRealmPoints.Share(PvpRealmPoints.ValueOf(dead.Level, dead.RealmLevel), near.Count, real.RealmPointRate);
                foreach (IGrouping<PvpRoamTeam, (PvpRoamTeam Team, GameBot Bot)> byTeam in near.GroupBy(entry => entry.Team))
                {
                    byTeam.Key.Kills++;
                    byTeam.Key.Earned += share * byTeam.Count();
                    PvpRoster.Credit(AppContext.BaseDirectory, byTeam.Key.Roster,
                        byTeam.ToDictionary(entry => (entry.Bot.PvpBuild.Id.ToLowerInvariant(), entry.Bot.PvpRosterPlace), _ => share));
                    foreach ((_, GameBot bot) in byTeam)
                        bot.PvpPoints += share;
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
                    };
                }).ToList(),
                Events = events,
            };
        }
    }
}
