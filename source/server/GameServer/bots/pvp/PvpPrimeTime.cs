using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DOL.GS
{
    /// <summary>
    /// The kinds of team of a prime-time evening (docs/PVP-8V8-ROADMAP.md, M11). The owner,
    /// 2026-11-22, on the evenings of the early 2000s: "Bad group (those groups running with more
    /// than 1 other group, sometime up to 5-6 groups sticking with each others. Most of these
    /// groups would run sub optimal composition). Casual that were roaming but staying close to
    /// other groups. Casual 8v8 that would not hesitate to add and collect rps. Decent 8v8 teams
    /// that would not add. Strong 8v8 teams. Elite 8v8 teams that would be only 8v8 in specific
    /// area to avoid not clean fights."
    /// </summary>
    public enum PvpTeamKind
    {
        Bad,
        Clingy,
        Adder,
        Decent,
        Strong,
        Elite,
    }

    /// <summary>Whom a team walks with: nobody, within sight of a friendly group, or in a pack of its own kind.</summary>
    public enum PvpCompany
    {
        Alone,
        Near,
        Zerg,
    }

    /// <summary>How a team's eight are picked: the realm's main group, that with a member or two changed, two healers and speed and anybody, or anybody.</summary>
    public enum PvpLineup
    {
        Meta,
        NearMeta,
        Loose,
        Random,
    }

    /// <summary>
    /// What a kind of team is like. Nothing in it makes a bot stronger or weaker in itself: a
    /// kind differs in who it walks with, whether it adds, whether it counts the enemy before
    /// it starts a fight, who is in it, its rank and its level of play (the three levels that
    /// exist, PvpSkills).
    /// </summary>
    /// <param name="SharePercent">Its share of the teams ("like 5% elite, 15% strong, 20% decent, 30% casual and 30% bad, or something like that"; the casual 30 are the two casual kinds).</param>
    /// <param name="AddChancePercent">How often it joins a fight it comes on.</param>
    /// <param name="CountsHeads">It does not start on more enemy groups than it has friends beside it.</param>
    /// <param name="MinRealmLevel">The realm level its members begin with is drawn between the two (10 is rank 2, 70 is rank 8).</param>
    /// <param name="KeepsToQuietGround">It leans to the quiet points of a map, away from the road.</param>
    public sealed record PvpTeamProfile(PvpTeamKind Kind, string Name, int SharePercent, PvpCompany Company, int AddChancePercent, bool CountsHeads,
        int MinRealmLevel, int MaxRealmLevel, PvpSkill Skill, PvpLineup Lineup, bool KeepsToQuietGround)
    {
        public static readonly IReadOnlyList<PvpTeamProfile> All =
        [
            new(PvpTeamKind.Bad, "bad", 30, PvpCompany.Zerg, 100, false, 1, 29, PvpSkill.Casual, PvpLineup.Random, false),
            new(PvpTeamKind.Clingy, "casual, stays near others", 15, PvpCompany.Near, 90, true, 10, 39, PvpSkill.Casual, PvpLineup.Loose, false),
            new(PvpTeamKind.Adder, "casual 8v8, adds", 15, PvpCompany.Alone, 80, true, 20, 49, PvpSkill.Seasoned, PvpLineup.NearMeta, false),
            new(PvpTeamKind.Decent, "decent 8v8", 20, PvpCompany.Alone, 0, true, 30, 59, PvpSkill.Seasoned, PvpLineup.Meta, false),
            new(PvpTeamKind.Strong, "strong 8v8", 15, PvpCompany.Alone, 0, true, 50, 79, PvpSkill.Sharp, PvpLineup.Meta, false),
            new(PvpTeamKind.Elite, "elite 8v8", 5, PvpCompany.Alone, 0, true, 70, 95, PvpSkill.Sharp, PvpLineup.Meta, true),
        ];

        public static PvpTeamProfile Of(PvpTeamKind kind) => All.First(profile => profile.Kind == kind);

        /// <summary>The profile a stored word stands for; a word that names none is a decent team.</summary>
        public static PvpTeamProfile Of(string kind) => Enum.TryParse(kind, true, out PvpTeamKind parsed) ? Of(parsed) : Of(PvpTeamKind.Decent);
    }

    /// <summary>
    /// One team of the world: a guild's eight, the same from evening to evening. Its members'
    /// names and realm points are kept under its id in the roster file (PvpRoster); this is who
    /// it is and what it has done.
    /// </summary>
    /// <param name="SeedLevel">The realm level its members had on their first evening; what they earn comes on top.</param>
    public sealed record PvpGuildTeam(string Id, string Guild, string Realm, string Kind, List<string> Builds, int SeedLevel,
        int Evenings = 0, int Fights = 0, int Kills = 0, int Deaths = 0, int Wipes = 0)
    {
        public eRealm ToRealm() => Enum.TryParse(Realm, true, out eRealm realm) ? realm : eRealm.None;
        public PvpTeamProfile ToProfile() => PvpTeamProfile.Of(Kind);
        public PvpGroupTemplate ToTemplate() => new(Id, ToRealm(), Builds);
    }

    /// <summary>
    /// The teams of the world (M11): made once, with a guild name and a kind each in the owner's
    /// ratio, and kept beside the server in <see cref="FileName"/> by pool ("world" for play; a
    /// test names its own, so that it does not touch the world's). It is the player's own data
    /// and is not part of any release.
    /// </summary>
    public static class PvpTeamPool
    {
        public const string FileName = "pvp-teams.json";
        public const string World = "world";
        private static readonly object Gate = new();
        private static readonly eRealm[] Realms = [eRealm.Albion, eRealm.Midgard, eRealm.Hibernia];

        // Invented, in the manner of the time. The owner may put guilds they remember in the file.
        private static readonly Dictionary<eRealm, string[]> Names = new()
        {
            [eRealm.Albion] =
            [
                "Gilded Thorn", "Camelot Nightwatch", "Lions of the Marsh", "The Pendragon Levy", "Sauvage Irregulars", "Salisbury Road Wardens",
                "The Last Cohort", "Lyonesse Company", "Black Rose Vanguard", "Order of the Broken Lance", "Benowyc Reserves", "The Cornwall Dogs",
                "Avalon Free Lances", "Humberton Militia", "Knights of the Grey Tower", "The Ludlow Nine", "Sons of the Round Table", "Snowdonia Rangers",
                "Crimson Chapter", "Pennine Watch", "The Lost Legion", "Caer Ulfwych Guard", "Dawn Crusade", "Prydwen Sellswords", "The Cotswold Mob",
                "Albion Vigil",
            ],
            [eRealm.Midgard] =
            [
                "Ravens of Jordheim", "The Iron Longship", "Sons of Svealand", "Fenrir's Leash", "Vindsaul Raiders", "The Frozen Oath",
                "Wolves of Uppland", "Bledmeer Berserks", "Hammer of Mularn", "The Odin's Gate Toll", "Skaldic Thunder", "Yggdra Wardens",
                "Nine Worlds Warband", "The Galplen Drunks", "Jamtland Reavers", "Blood of Ymir", "Nalliten Shieldwall", "The Black Fjord",
                "Glacier Kin", "Valkyrie's Chosen", "Huginn and Muninn", "The Mead Hall Brawlers", "Trollheim Irregulars", "Gna Faste Pack",
                "Winter's Teeth", "Midgard Vigil",
            ],
            [eRealm.Hibernia] =
            [
                "Children of Lugh", "The Emerald Host", "Wardens of Tir na Nog", "Druim Ligen Regulars", "The Silver Hand", "Crauchon Watch",
                "Lurikeen Mafia", "The Hollow Hill", "Sidhe Courtiers", "Stags of Connla", "The Sheeroe Riders", "Cruachan Wolfhounds",
                "Mag Mell Wanderers", "Thorn and Thistle", "The Green Tide", "Sons of Nuada", "Breifine Hunters", "The Veiled Path",
                "Ardagh Irregulars", "Moonlit Grove", "The Shrouded Isles Nine", "Collory Highlanders", "Briar Kin", "Daingean Rovers",
                "The Wild Hunt", "Hibernia Vigil",
            ],
        };

        /// <summary>How many guild names a realm has: a pool holds no more teams of a realm than this.</summary>
        public static int MostPerRealm => Names.Values.Min(names => names.Length);

        private static readonly eCharacterClass[] Healers = [eCharacterClass.Cleric, eCharacterClass.Healer, eCharacterClass.Druid];
        private static readonly eCharacterClass[] Speed = [eCharacterClass.Minstrel, eCharacterClass.Skald, eCharacterClass.Bard];

        /// <summary>
        /// How many teams of each kind make up <paramref name="count"/> teams in the owner's ratio.
        /// Each kind has its whole share; the places left over go to the largest remainders (the
        /// larger share first), or, with <paramref name="random"/>, are drawn by the remainders, so
        /// that among eight teams there is an elite one some evenings and not never.
        /// </summary>
        public static Dictionary<PvpTeamKind, int> Quotas(int count, Random random = null)
        {
            var quotas = PvpTeamProfile.All.ToDictionary(profile => profile.Kind, profile => count * profile.SharePercent / 100);
            List<PvpTeamProfile> open = PvpTeamProfile.All
                .OrderByDescending(profile => count * profile.SharePercent % 100).ThenByDescending(profile => profile.SharePercent).ToList();
            for (int left = Math.Max(0, count - quotas.Values.Sum()); left > 0 && open.Count > 0; left--)
            {
                PvpTeamProfile next = open[0];
                int total = open.Sum(profile => count * profile.SharePercent % 100);
                if (random != null && total > 0)
                {
                    int roll = random.Next(total);
                    next = open.First(profile => (roll -= count * profile.SharePercent % 100) < 0);
                }
                open.Remove(next);
                quotas[next.Kind]++;
            }
            return quotas;
        }

        /// <summary>The builds of a realm a team may be made of: every 8v8 build but the buffbots.</summary>
        public static List<PvpBuild> BuildsOf(eRealm realm) =>
            PvpBuildCatalog.Builds.Where(build => PvpBuildCatalog.RealmOf(build.Class) == realm && !build.Id.StartsWith("buffbot-", StringComparison.Ordinal)).ToList();

        /// <summary>
        /// Eight builds for a team of a realm. Meta: the realm's main group. NearMeta: that with
        /// one or two of its fighters and casters changed. Loose: two healers, the speed class
        /// and five of anything. Random: one healer or two, the speed class six times in ten, and
        /// anything ("most of these groups would run sub optimal composition").
        /// </summary>
        public static List<string> Compose(eRealm realm, PvpLineup lineup, Random random)
        {
            List<string> main = PvpBuildCatalog.Groups.First(group => group.Realm == realm).BuildIds.ToList();
            if (lineup == PvpLineup.Meta)
                return main;
            List<PvpBuild> all = BuildsOf(realm);
            List<PvpBuild> healers = all.Where(build => Healers.Contains(build.Class)).ToList();
            List<PvpBuild> speed = all.Where(build => Speed.Contains(build.Class)).ToList();
            List<PvpBuild> others = all.Where(build => !Healers.Contains(build.Class) && !Speed.Contains(build.Class)).ToList();
            if (lineup == PvpLineup.NearMeta)
            {
                List<int> changeable = Enumerable.Range(0, main.Count)
                    .Where(index => others.Any(build => build.Id == main[index])).OrderBy(_ => random.Next()).Take(1 + random.Next(2)).ToList();
                foreach (int index in changeable)
                {
                    List<PvpBuild> choice = others.Where(build => build.Id != main[index] && main.Count(id => id == build.Id) < 2).ToList();
                    if (choice.Count > 0)
                        main[index] = choice[random.Next(choice.Count)].Id;
                }
                return main;
            }
            var builds = new List<string>();
            const int most = 2;
            void Add(List<PvpBuild> from)
            {
                List<PvpBuild> choice = from.Where(build => builds.Count(id => id == build.Id) < most).ToList();
                if (choice.Count > 0)
                    builds.Add(choice[random.Next(choice.Count)].Id);
            }
            if (lineup == PvpLineup.Loose)
            {
                foreach (PvpBuild healer in healers.OrderBy(_ => random.Next()).Take(2))
                    builds.Add(healer.Id);
                Add(speed);
            }
            else
            {
                // One healer, a second four times in ten, the speed class six times in ten.
                Add(healers);
                if (random.Next(10) < 4)
                    Add(healers);
                if (random.Next(10) < 6)
                    Add(speed);
            }
            // Anybody for the rest, two of a build at most: no third healer by chance and no second speed class. (The first
            // bad group of Midgard that was drawn, 2026-11-22, had three Healers and three Shamans.)
            for (int guard = 0; builds.Count < 8 && guard < 100; guard++)
                Add(others);
            return builds;
        }

        /// <summary>A short id for a guild's team: the realm and the letters of its name ("hib-thewildhunt").</summary>
        public static string IdOf(eRealm realm, string guild) =>
            (realm switch { eRealm.Albion => "alb-", eRealm.Midgard => "mid-", _ => "hib-" }) +
            new string((guild ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).Take(20).ToArray());

        /// <summary>
        /// Teams for a pool: <paramref name="perRealm"/> of each realm, the kinds in the owner's
        /// ratio within each realm, each with a guild name no team in <paramref name="existing"/> has.
        /// </summary>
        public static List<PvpGuildTeam> Generate(int perRealm, Random random, IReadOnlyCollection<PvpGuildTeam> existing = null)
        {
            var teams = new List<PvpGuildTeam>();
            foreach (eRealm realm in Realms)
            {
                var taken = new HashSet<string>((existing ?? []).Where(team => team.ToRealm() == realm).Select(team => team.Guild), StringComparer.OrdinalIgnoreCase);
                List<string> names = Names[realm].Where(name => !taken.Contains(name)).OrderBy(_ => random.Next()).ToList();
                List<PvpTeamKind> kinds = Quotas(perRealm).SelectMany(quota => Enumerable.Repeat(quota.Key, quota.Value)).ToList();
                for (int index = 0; index < kinds.Count && index < names.Count; index++)
                {
                    PvpTeamProfile profile = PvpTeamProfile.Of(kinds[index]);
                    teams.Add(new PvpGuildTeam(IdOf(realm, names[index]), names[index], PvpRoam.NameOf(realm), profile.Kind.ToString(),
                        Compose(realm, profile.Lineup, random), random.Next(profile.MinRealmLevel, profile.MaxRealmLevel + 1)));
                }
            }
            return teams;
        }

        /// <summary>
        /// Who is out this evening: <paramref name="perRealm"/> teams of each realm drawn from the
        /// pool, the kinds in the owner's ratio as far as the pool has them.
        /// </summary>
        /// <param name="fewer">A realm that gets one team less: the player's own group is its first.</param>
        public static List<PvpGuildTeam> Tonight(IReadOnlyCollection<PvpGuildTeam> pool, int perRealm, Random random, eRealm fewer = eRealm.None)
        {
            var out_ = new List<PvpGuildTeam>();
            foreach (eRealm realm in Realms)
            {
                int count = Math.Max(0, perRealm - (realm == fewer ? 1 : 0));
                List<PvpGuildTeam> ofRealm = pool.Where(team => team.ToRealm() == realm).OrderBy(_ => random.Next()).ToList();
                var picked = new List<PvpGuildTeam>();
                foreach (KeyValuePair<PvpTeamKind, int> quota in Quotas(count, random))
                    picked.AddRange(ofRealm.Where(team => team.ToProfile().Kind == quota.Key).Take(quota.Value));
                // A kind the pool is short of is made up with any other team of the realm.
                picked.AddRange(ofRealm.Except(picked).Take(count - picked.Count));
                out_.AddRange(picked.Take(count));
            }
            return out_;
        }

        /// <summary>
        /// Who comes out when a team has logged off: a team of its realm that has not been out
        /// this evening, of the same kind when there is one, so that the evening keeps its
        /// ratio; null when the realm has nobody left.
        /// </summary>
        public static PvpGuildTeam NextOut(IReadOnlyCollection<PvpGuildTeam> pool, eRealm realm, PvpTeamKind kind, IReadOnlySet<string> outOrGone, Random random)
        {
            List<PvpGuildTeam> free = pool.Where(team => team.ToRealm() == realm && !outOrGone.Contains(team.Id)).OrderBy(_ => random.Next()).ToList();
            return free.FirstOrDefault(team => team.ToProfile().Kind == kind) ?? free.FirstOrDefault();
        }

        /// <summary>
        /// The packs of an evening: the teams of a realm that walk in a pack (the bad ones) are
        /// put together by two to <paramref name="most"/>, each pack behind its first team. Returns, for each team that
        /// follows, the team it follows. One such team alone joins the pack before it, or walks alone.
        /// </summary>
        public static Dictionary<T, T> Packs<T>(IReadOnlyList<T> packTeams, Random random, int most = 5) where T : class
        {
            var follows = new Dictionary<T, T>();
            int index = 0;
            while (index < packTeams.Count)
            {
                int left = packTeams.Count - index;
                // Never one team left over: a pack of two to five, and what is left is two at least or nothing.
                int size = left <= most ? left : Math.Min(most, 2 + random.Next(most - 1));
                if (left - size == 1)
                    size = size < most ? size + 1 : size - 1;
                for (int member = 1; member < size; member++)
                    follows[packTeams[index + member]] = packTeams[index];
                index += size;
            }
            return follows;
        }

        /// <summary>The teams of a pool; empty when it has none or the file cannot be read.</summary>
        public static List<PvpGuildTeam> Load(string folder, string pool)
        {
            lock (Gate)
                return ReadAll(folder).TryGetValue(pool ?? World, out List<PvpGuildTeam> teams) ? teams : [];
        }

        /// <summary>A pool with at least <paramref name="perRealm"/> teams of each realm: what is kept, and new teams made for what is missing (and kept).</summary>
        public static List<PvpGuildTeam> LoadOrCreate(string folder, string pool, int perRealm, Random random)
        {
            lock (Gate)
            {
                List<PvpGuildTeam> teams = Load(folder, pool);
                int missing = Realms.Max(realm => Math.Min(perRealm, MostPerRealm) - teams.Count(team => team.ToRealm() == realm));
                if (missing <= 0)
                    return teams;
                // The same number more for every realm, so that each realm keeps the ratio of kinds.
                teams.AddRange(Generate(missing, random, teams));
                Save(folder, pool, teams);
                return teams;
            }
        }

        /// <summary>Adds an evening to the record of the teams that were out. Never throws.</summary>
        public static void Record(string folder, string pool, IReadOnlyDictionary<string, (int Fights, int Kills, int Deaths, int Wipes)> evening)
        {
            if (evening == null || evening.Count == 0)
                return;
            lock (Gate)
            {
                List<PvpGuildTeam> teams = Load(folder, pool);
                if (teams.Count == 0)
                    return;
                Save(folder, pool, teams.Select(team => evening.TryGetValue(team.Id, out (int Fights, int Kills, int Deaths, int Wipes) did)
                    ? team with
                    {
                        Evenings = team.Evenings + 1, Fights = team.Fights + did.Fights, Kills = team.Kills + did.Kills,
                        Deaths = team.Deaths + did.Deaths, Wipes = team.Wipes + did.Wipes,
                    }
                    : team).ToList());
            }
        }

        private static void Save(string folder, string pool, List<PvpGuildTeam> teams)
        {
            try
            {
                Dictionary<string, List<PvpGuildTeam>> all = ReadAll(folder);
                all[pool ?? World] = teams;
                File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
                // A pool that cannot be written is only not kept.
            }
        }

        private static Dictionary<string, List<PvpGuildTeam>> ReadAll(string folder)
        {
            try
            {
                string path = Path.Combine(folder, FileName);
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<Dictionary<string, List<PvpGuildTeam>>>(File.ReadAllText(path)) ?? new()
                    : new();
            }
            catch (Exception)
            {
                return new();
            }
        }
    }
}
