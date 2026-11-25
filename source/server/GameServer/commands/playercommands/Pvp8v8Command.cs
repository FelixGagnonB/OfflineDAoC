using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>The 8v8 mode from inside the game (docs/PVP-8V8-ROADMAP.md, M1.6).</summary>
    [CmdAttribute("&8v8", ePrivLevel.Player, "8v8 group: seven level-50 companions built around your class",
        "/8v8 group [realm rank 1-13]   your realm's main group without your own class, fully buffed",
        "/8v8 setup                     a window to pick your group, the enemy realm, the start (lane, basin, or a roam in Emain Macha or Thidranki), the rules and the realm rank, then start",
        "/8v8 ready [build]             your character becomes level 50 with that build's class, spec and gear (lists the builds without one)",
        "/8v8 play <alb|mid|hib> [rank] [lane|basin] [follow]  a fight: you and seven companions against that realm's group of eight, in Emain Macha; follow: a bot drives",
        "/8v8 play hib-2bard ...  or  /8v8 play mid hib-2bard ...  another group by its name: as the enemy (first), or as your own (later)",
        "/8v8 play <alb|mid|hib> real [lane|basin] [follow]  a real fight: everybody at the rank it has earned, kills earn realm points that are kept, no timer is reset",
        "/8v8 roam [thid] [group]       the roam: two groups of each realm in Emain Macha (thid: in Thidranki), yours with you; real rules, and a wipe or a release puts your group back at its start",
        "/8v8 skill [group|others] [casual|seasoned|sharp]  how sharp your group and the other groups play: reaction time, attention, mistakes; alone it says what is set",
        "/8v8 prime [zone] [number] [one|all]   a prime-time evening in some of the frontier zones: that many groups of each realm (8 if you give none, 16 at most), yours one of them, the others guilds of every kind: packs of bad groups, groups that add, groups that do not. One zone for every three groups of a realm: the one you begin in, the crowded one (Emain Macha) and others that change from evening to evening. zone: where you begin (emain, odin, hadrian, breifine, cruachan, collory, pennine, sauvage, snowdonia, jamtland, uppland, yggdra; thid for Thidranki alone). one: in that zone only; all: in all twelve",
        "/8v8 prime ... rank <1-13>     the same evening to try things: your group at that realm rank for the evening, and it earns nothing (your next real fight puts you back at what you have earned)",
        "/8v8 go <zone>                 on a prime-time evening: your group ports to your realm's start in another zone of the evening (/8v8 who names them); not out of a fight",
        "/8v8 guild [name]              forms a guild with you as its guildmaster, at no cost and wherever you stand: your seven companions wear its name, and on a prime-time evening your group goes by it; alone it says what guild you have",
        "/8v8 evening                   begins your evening when none is running: it begins by itself when you log in with an 8v8 character, and ends when you leave the frontier",
        "/8v8 who                       on a prime-time evening: each zone of the evening, how many groups every realm has in it and of what kinds (bad, casual, decent, strong, elite)",
        "/8v8 rivals                    the enemy guilds you have wiped or been wiped by on prime-time evenings, and how often",
        "/8v8 status                    in a roam: what every team is doing (buffing at its start, roaming, fighting whom, resting, waiting by a fight, wiped), how many of it are alive and how far it is",
        "/8v8 ranks                     the realm ranks of your companions, and of the enemy groups you have fought for real",
        "/8v8 call <push|regroup|hold|engage|extend|assist|control|retreat|rest>  your call to the group: an order stands twelve seconds; assist puts the damage dealers on your target while it lives (45 s at most); control asks for a mez, stun, root or nearsight on your target (20 s); retreat: everybody drops what it does and runs with you (30 s, or until your next call); rest: everybody redoes its buffs and rests where you stand (2 minutes, or until you walk on, call something else or are attacked)",
        "/8v8 calls <on|off>            your companions' calls in group chat (assist, incoming, orders, out of power, a mez that landed, a Purge used)",
        "/8v8 lead <on|off>             in a roam: your companions fall back as one by themselves when the group has too many on it, and call it loud (on unless you turn it off; kept from one login to the next)",
        "/8v8 corpse <player|body|fall|ghost|off>  how a dead 8v8 bot is shown: lying as a dead player does, to click (player); a body under its name that only the group window targets (body); the body and a ghost standing over it (fall); the ghost only (ghost); standing as if alive (off)",
        "/8v8 stop                      ends the fight and removes every 8v8 bot",
        "/8v8 clear                     removes every 8v8 bot",
        "/8v8 lanes                     (GM) what is fighting in each lane of the arena",
        "/8v8 watch <lane>              (GM) takes you to the middle of that lane and turns the feed on; then /fly",
        "/8v8 feed <on|off>             (GM) each group's calls in your chat window, for the lane you watch or stand in")]
    public sealed class Pvp8v8CommandHandler : AbstractCommandHandler, ICommandHandler
    {
        /// <summary>
        /// Reads the words after "/8v8 prime": a zone, a number of groups, "one" or "all", one of
        /// the realm's line-ups, and "rank" with its number. Returns what to tell the player when
        /// a word is not one of those, and nothing is started then. The owner, 2026-11-24, typed
        /// "/8v8 prime emain 8 rank hib-2bard": the word after "rank" was taken for its number,
        /// so the evening began at the rank the owner had, with the main line-up, and said
        /// nothing of either.
        /// </summary>
        public static string PrimeWords(string[] words, out string[] rest, out int rank)
        {
            const string example = "For example: /8v8 prime emain 8 rank 8 hib-2bard";
            var kept = new System.Collections.Generic.List<string>();
            rank = 0;
            rest = [];
            for (int index = 0; index < words.Length; index++)
            {
                string word = words[index];
                if (word.Equals("rank", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= words.Length || !int.TryParse(words[index + 1], out rank) || rank is < 1 or > 13)
                        return $"\"rank\" needs a number from 1 to 13 after it. {example}";
                    index++;
                }
                else if (int.TryParse(word, out _) || PvpRoam.MapFor(word) != null || PvpBuildCatalog.FindGroup(word) != null ||
                         word.Equals("one", System.StringComparison.OrdinalIgnoreCase) || word.Equals("all", System.StringComparison.OrdinalIgnoreCase))
                    kept.Add(word);
                else
                    return $"\"{word}\" is not a zone, a number of groups, a line-up, \"one\", \"all\" or \"rank\". {example}";
            }
            rest = kept.ToArray();
            return null;
        }

        /// <summary>
        /// Why a guild of that name cannot be formed by "/8v8 guild"; null when it can. The
        /// owner, 2026-11-24: "How can I form a guild with my 7 bots?" The game's own way
        /// (/gc form at a guild registrar, a gold piece) is in a capital, and an 8v8 character
        /// has no coin and no reason to be there.
        /// </summary>
        public static string GuildProblem(string name, string guildItHas, bool lettersAreValid, bool aGuildHasIt, bool aTeamOfBotsHasIt) =>
            !string.IsNullOrEmpty(guildItHas) ? $"You are in {guildItHas} already; your companions wear its name. (/gc quit leaves it.)"
            : string.IsNullOrWhiteSpace(name) ? "You have no guild. /8v8 guild <name> forms one: you its guildmaster, your seven companions wearing its name."
            : name.Length > 30 ? "That name is too long: thirty letters at most."
            : !lettersAreValid ? "A guild's name is letters and spaces only."
            : aGuildHasIt ? $"There is a guild called {name} already."
            : aTeamOfBotsHasIt ? $"{name} is one of the guilds of bots that are out on a prime-time evening. Pick another name."
            : null;

        /// <summary>
        /// What a player has of this command where the game is played and not tried
        /// (docs/PVP-8V8-ROADMAP.md, M13). The owner, 2026-11-24: "I consider the /8v8 as testing
        /// commands, beside the 8v8 calls. Those need to stay in for sure. Same with the /8v8 to
        /// instant 50 + gear"; and of the rest: "I would keep /8v8 rivals. Not sure about ranks.
        /// I don't think I would keep status. I would probably change /8v8 who." Everything else
        /// (prime, roam, play, setup, group, skill, status, stop, clear and the GM's three) is
        /// for a game master, or for a server with <see cref="TestMarker"/> beside it, which the
        /// scrim server has and the play install has not.
        /// </summary>
        /// ("stop" is not one of them. The owner, 2026-11-25: "In v0.1.1 on D:\OfflineDAoC, do we really ahve the
        /// /8v8 stop ?", and of having it: "I want it on scrim, it makes testing easy, but not on the real server". So
        /// "/8v8 ready" on a character whose evening runs, which used to answer "/8v8 stop first", ends and begins that
        /// evening by itself.)
        public static readonly string[] PlayerWords = ["call", "calls", "lead", "ready", "guild", "evening", "who", "rivals", "ranks", "go", "corpse"];

        public const string TestMarker = "pvp-test-commands.txt";

        public static readonly string[] PlayerHelp =
        [
            "/8v8 ready [build]   your character becomes level 50 with that build's class, spec and gear (lists the builds without one)",
            "/8v8 guild [name]    forms your guild: your seven companions wear its name and your group goes by it",
            "/8v8 guild lineup [name]   which eight your group is, from your next evening on",
            "/8v8 guild emblem    opens the emblem window: your guild's emblem, free, worn by you and your companions",
            "/8v8 call <push|regroup|hold|engage|extend|assist|control|retreat|rest|die>   your call to the group",
            "/8v8 calls <on|off>  your companions' calls in group chat",
            "/8v8 lead <on|off>   your companions fall back as one by themselves when the group has too many on it",
            "/8v8 evening         begins your evening when none is running (it begins by itself when you log in; it ends when you leave the frontier)",
            "/8v8 who             each zone of the evening: how many groups every realm has in it, and of what kinds",
            "/8v8 go <zone>       your group ports to your realm's start in another zone of the evening",
            "/8v8 rivals          the enemy guilds you have wiped or been wiped by, and how often",
            "/8v8 ranks           the realm ranks of your companions",
            "/8v8 corpse <player|body|fall|ghost|off>   how a dead 8v8 bot is shown",
        ];

        /// <summary>Whether a word of the command is one a player may not use here.</summary>
        public static bool IsKeptFromAPlayer(string word, bool testServer, bool gameMaster) =>
            !testServer && !gameMaster && !PlayerWords.Contains((word ?? string.Empty).ToLowerInvariant());

        /// <summary>What "/8v8 guild lineup [name]" answers: the line-up the group has, and the ones its realm has to pick from.</summary>
        public static string LineupAnswer(string asked, bool known, string has, System.Collections.Generic.IReadOnlyList<string> ofItsRealm) =>
            (asked == null ? $"Your group's line-up is {has}."
                : known ? $"Your group's line-up is {has} from your next evening on (your own class takes its place in it)."
                : $"\"{asked}\" is not a line-up of your realm; yours stays {has}.") +
            " /8v8 guild lineup <name>: " + string.Join("; ", ofItsRealm) + ".";

        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null)
                return;
            void Say(string text) => client.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_SystemWindow);
            if (IsKeptFromAPlayer(args.Length > 1 ? args[1] : string.Empty,
                    System.IO.File.Exists(System.IO.Path.Combine(System.AppContext.BaseDirectory, TestMarker)), client.Account.PrivLevel > (uint)ePrivLevel.Player))
            {
                foreach (string line in PlayerHelp)
                    Say(line);
                return;
            }

            switch (args.Length > 1 ? args[1].ToLowerInvariant() : string.Empty)
            {
                case "group":
                    if (PvpArena.Members.Any())
                    {
                        Say("8v8 bots are already in the world. Use /8v8 clear first.");
                        return;
                    }
                    int rank = args.Length > 2 && int.TryParse(args[2], out int parsed) ? System.Math.Clamp(parsed, 1, 13) : 1;
                    int count = PvpArena.SpawnCompanions(player, (rank - 1) * 10).Count;
                    Say($"{count} companions joined you at realm rank {rank}, and the buffbot has buffed the group.");
                    return;

                case "play":
                {
                    PvpGroupTemplate enemy = args.Length > 2 ? PvpBuildCatalog.FindGroup(args[2]) ?? PvpBuildCatalog.Groups.FirstOrDefault(group =>
                        group.Id.StartsWith(args[2].ToLowerInvariant().Substring(0, System.Math.Min(3, args[2].Length)))) : null;
                    // Another group of the player's own realm, by its name (PvpBuildCatalog.Variants).
                    PvpGroupTemplate ownGroup = args.Skip(3).Select(PvpBuildCatalog.FindGroup).FirstOrDefault(group => group?.Realm == player.Realm);
                    if (enemy == null || enemy.Realm == player.Realm)
                    {
                        Say("Which realm do you fight? /8v8 play alb, mid or hib (not your own), then a realm rank if you want one: /8v8 play mid 5");
                        return;
                    }
                    if (PvpScrim.Running)
                    {
                        Say("A fight or a scrim is already running. /8v8 stop ends it.");
                        return;
                    }
                    int playRank = args.Skip(3).Select(arg => int.TryParse(arg, out int wanted) ? wanted : 0).FirstOrDefault(wanted => wanted > 0);
                    string start = args.Skip(3).Any(arg => arg.ToLowerInvariant() == "basin") ? "basin" : "lane";
                    bool follow = args.Skip(3).Any(arg => arg.ToLowerInvariant() == "follow");
                    bool real = args.Skip(3).Any(arg => arg.ToLowerInvariant() == "real");
                    PvpScrim.TravelAndStartPlay(player, enemy, (System.Math.Clamp(playRank == 0 ? 5 : playRank, 1, 13) - 1) * 10, start, Say, follow, ownGroup, real);
                    if (ownGroup != null)
                        Say($"Your group is {ownGroup.Id}: {string.Join(", ", ownGroup.BuildIds)} (without your own place).");
                    return;
                }

                case "call":
                {
                    PvpSquad squad = PvpScrim.SquadOf(player);
                    if (squad == null)
                    {
                        Say(PvpRoam.Leads(player) ? "Your companions wait off the field while you are out of the frontier: /8v8 go <zone> takes you back with them."
                            : "You call to your group in a fight. No evening is running: /8v8 evening begins one.");
                        return;
                    }
                    string word = args.Length > 2 ? args[2].ToLowerInvariant() : string.Empty;
                    if (word == "assist")
                    {
                        if (!squad.CallAssist(player, player.TargetObject as GameLiving, PvpRoam.StandsBy(player, player.TargetObject as GameLiving)))
                            Say("Target an enemy your group can see, then /8v8 call assist.");
                        return;
                    }
                    if (PvpCalls.IsRestCall(word))
                    {
                        squad.CallRest(player);
                        return;
                    }
                    if (PvpCalls.IsRetreatCall(word))
                    {
                        squad.CallRetreat(player);
                        return;
                    }
                    if (PvpCalls.IsDieCall(word))
                    {
                        Say(PvpRoam.GiveUp(player));
                        return;
                    }
                    if (PvpCalls.IsControlCall(word))
                    {
                        if (!squad.CallControl(player, player.TargetObject as GameLiving, PvpRoam.StandsBy(player, player.TargetObject as GameLiving)))
                            Say("Target an enemy your group can see, then /8v8 call control.");
                        return;
                    }
                    if (PvpCalls.OrderFor(word) is not { } called)
                    {
                        Say("/8v8 call push, regroup, hold, engage, extend, assist, control, retreat, rest or die.");
                        return;
                    }
                    squad.CallOrder(player, called);
                    return;
                }

                case "skill":
                {
                    // "/8v8 skill": what is set. "/8v8 skill group sharp", "/8v8 skill others casual": for the bots made from now on.
                    string side = args.Length > 2 ? args[2].ToLowerInvariant() : string.Empty;
                    PvpSkill? level = args.Length > 3 ? PvpSkills.Parse(args[3]) : null;
                    if (level != null && side is "group" or "mine" or "own")
                        PvpSkills.Group = level.Value;
                    else if (level != null && side is "others" or "enemy" or "enemies")
                        PvpSkills.Others = level.Value;
                    else if (side.Length > 0)
                    {
                        Say("/8v8 skill group <casual|seasoned|sharp>   or   /8v8 skill others <casual|seasoned|sharp>");
                        return;
                    }
                    Say($"Your group plays {PvpSkills.Group}, the other groups {PvpSkills.Others}. It holds for the bots made from now on, until the server restarts.");
                    Say(PvpSkills.Describe(PvpSkill.Casual) + ".");
                    Say(PvpSkills.Describe(PvpSkill.Seasoned) + ".");
                    Say(PvpSkills.Describe(PvpSkill.Sharp) + ".");
                    return;
                }

                case "calls":
                {
                    bool off = args.Length > 2 && args[2].ToLowerInvariant() == "off";
                    if (off)
                        player.TempProperties.SetProperty(PvpCalls.OffProperty, true);
                    else
                        player.TempProperties.RemoveProperty(PvpCalls.OffProperty);
                    Say(off ? "Your companions' calls are off." : "Your companions call in group chat: assist, incoming, orders, out of power.");
                    return;
                }

                case "evening":
                    // The evening begins at login. It ends when its player is gone from the frontier (to a trainer, say): this begins it again.
                    if (PvpScrim.IsPlaying(player) || PvpRoam.Leads(player))
                        Say("Your evening is running. /8v8 who says where everybody is.");
                    else
                        PvpEveningStart.BeginFor(player, true);
                    return;

                case "guild":
                {
                    // "/8v8 guild emblem": the game's emblem window, wherever the player stands and at no cost.
                    if (args.Length > 2 && args[2].ToLowerInvariant() == "emblem")
                    {
                        Say(PvpGuildEmblem.Ask(player));
                        return;
                    }
                    // "/8v8 guild lineup [name]": which eight the group is, from the next evening on.
                    if (args.Length > 2 && args[2].ToLowerInvariant() == "lineup")
                    {
                        System.Collections.Generic.List<PvpGroupTemplate> mine = PvpBuildCatalog.Groups.Concat(PvpBuildCatalog.Variants).Where(group => group.Realm == player.Realm).ToList();
                        PvpGroupTemplate lineup = args.Length > 3 ? mine.FirstOrDefault(group => group.Id.Equals(args[3], System.StringComparison.OrdinalIgnoreCase)) : null;
                        if (lineup != null)
                            PvpPlayerSettings.Set(System.AppContext.BaseDirectory, player.InternalID,
                                kept => kept with { Lineup = PvpBuildCatalog.Groups.Contains(lineup) ? null : lineup.Id });
                        string has = PvpPlayerSettings.Get(System.AppContext.BaseDirectory, player.InternalID).Lineup ?? mine.First().Id;
                        Say(LineupAnswer(args.Length > 3 ? args[3] : null, lineup != null, has, mine.Select(group => $"{group.Id} ({string.Join(", ", group.BuildIds)})").ToList()));
                        return;
                    }
                    string name = string.Join(" ", args.Skip(2)).Trim();
                    bool letters = name.Length > 0 && GuildCommandHandler.IsValidGuildName(name);
                    if (GuildProblem(name, player.GuildName, letters, letters && GuildMgr.DoesGuildExist(name),
                            PvpTeamPool.Load(System.AppContext.BaseDirectory, PvpTeamPool.World)
                                .Any(team => string.Equals(team.Guild, name, System.StringComparison.OrdinalIgnoreCase))) is { } problem)
                    {
                        Say(problem);
                        return;
                    }
                    Guild guild = GuildMgr.CreateGuild(player.Realm, name, player);
                    if (guild == null || !guild.AddPlayer(player, guild.GetRankByID(0)))
                    {
                        Say($"The guild {name} could not be formed.");
                        return;
                    }
                    // The companions that are out wear it at once; the next ones when they are made (PvpArena.SpawnCompanions).
                    int wearing = 0;
                    foreach (GameBot bot in player.Group?.GetMembersInTheGroup().OfType<GameBot>().Where(bot => bot.IsPvpBot) ?? [])
                    {
                        (bot.PvpGuild, bot.GuildName) = (guild.Name, guild.Name);
                        wearing++;
                    }
                    Say($"{guild.Name} is formed, and you are its guildmaster. " +
                        (wearing > 0 ? $"Your {wearing} companions wear its name. " : "Your companions will wear its name. ") +
                        "On a prime-time evening your group goes by it from the next evening you start.");
                    return;
                }

                case "lead":
                {
                    // Alone it says what is set; on or off is kept from one login to the next (on for a new player).
                    string leadWord = args.Length > 2 ? args[2].ToLowerInvariant() : string.Empty;
                    if (leadWord is "on" or "off")
                        PvpCalls.SetLead(player, leadWord == "on");
                    Say(PvpCalls.Leads(player)
                        ? "In a roam your companions fall back as one by themselves when the group has too many on it. They call it in capitals, four times: KITE BACK. /8v8 lead off to stop it; it is kept."
                        : "Your companions move back only on your call (/8v8 call extend, /8v8 call retreat). /8v8 lead on to let them; it is kept.");
                    return;
                }

                case "setup":
                    if (!Pvp8v8SetupMenu.Open(player))
                        Say("The setup window could not open here. Use /8v8 play <alb|mid|hib> [rank] [lane|basin] instead.");
                    return;

                case "ready":
                {
                    if (args.Length < 3)
                    {
                        Say("Builds of your realm: " + string.Join(", ", PvpPlayerReady.BuildsFor(player.Realm).Select(build => build.Id)) +
                            ". /8v8 ready <build> makes this character level 50 with that build's class, spec and gear.");
                        return;
                    }
                    PvpBuild build = PvpPlayerReady.Find(player.Realm, args[2]);
                    // Its own evening is ended for the change and begun again after it; a fight of another sort has to be stopped.
                    bool ownEvening = PvpRoam.Leads(player);
                    string refusal = PvpScrim.IsPlaying(player) && !ownEvening ? "A fight is running. /8v8 stop first."
                        : PvpPlayerReady.Refusal(player.Realm, player.InCombat, player.IsAlive, build);
                    if (refusal != null)
                    {
                        Say(refusal + " Builds of your realm: " + string.Join(", ", PvpPlayerReady.BuildsFor(player.Realm).Select(each => each.Id)));
                        return;
                    }
                    if (args.Length < 4 || args[3].ToLowerInvariant() != "confirm")
                    {
                        Say($"This changes {player.Name} for good: class {build.Class}, level 50, the spec of {build.Id} " +
                            $"({string.Join(", ", build.Lines.Select(line => $"{line.Spec} {line.Level}"))}), and its gear in place of what you wear. " +
                            $"Realm rank is not touched. To do it: /8v8 ready {build.Id} confirm");
                        return;
                    }
                    try
                    {
                        if (ownEvening)
                        {
                            PvpScrim.Stop();
                            Say("Your evening is ended for the change; a new one begins when it is done.");
                        }
                        foreach (string line in PvpPlayerReady.Apply(player, build))
                            Say(line);
                        // Its new cloak and shields wear its guild's emblem.
                        PvpGuildEmblem.Wear(player);
                        // An 8v8 character now: its evening begins (PvpEveningStart), as it will each time it comes in.
                        PvpEveningStart.BeginFor(player, ownEvening);
                    }
                    catch (System.Exception exception)
                    {
                        Say("Making the character ready failed part way: " + exception.Message);
                    }
                    return;
                }

                case "roam":
                    try
                    {
                        // "/8v8 roam thid hib-2bard": in Thidranki, and with another of the realm's groups than its main one.
                        PvpRoam.StartFor(player, Say, args.Skip(2).Select(PvpBuildCatalog.FindGroup).FirstOrDefault(group => group != null),
                            args.Skip(2).Select(PvpRoam.MapFor).FirstOrDefault(map => map != null));
                    }
                    catch (System.Exception exception)
                    {
                        Say(exception.Message);
                    }
                    return;

                case "prime":
                    try
                    {
                        // "/8v8 prime thid 6": in Thidranki, six groups of each realm.
                        // "/8v8 prime emain 8 rank 8": an evening to try things, your group at realm rank 8 and earning nothing.
                        // "/8v8 prime emain 8 rank 8 hib-2bard": the same with another of your realm's line-ups.
                        if (PrimeWords(args.Skip(2).ToArray(), out string[] rest, out int trialRank) is { } notUnderstood)
                        {
                            Say(notUnderstood);
                            return;
                        }
                        int groups = rest.Select(word => int.TryParse(word, out int number) ? number : 0).FirstOrDefault(number => number > 0);
                        PvpRoam.StartFor(player, Say, rest.Select(PvpBuildCatalog.FindGroup).FirstOrDefault(group => group != null),
                            rest.Select(PvpRoam.MapFor).FirstOrDefault(map => map != null), System.Math.Clamp(groups == 0 ? 8 : groups, 1, 16),
                            rest.Any(word => word.Equals("one", System.StringComparison.OrdinalIgnoreCase)),
                            rest.Any(word => word.Equals("all", System.StringComparison.OrdinalIgnoreCase)), trialRank);
                    }
                    catch (System.Exception exception)
                    {
                        Say(exception.Message);
                    }
                    return;

                case "go":
                    try
                    {
                        PvpRoam.GoTo(player, args.Skip(2).Select(PvpRoam.MapFor).FirstOrDefault(map => map != null), Say);
                    }
                    catch (System.Exception exception)
                    {
                        Say(exception.Message);
                    }
                    return;

                case "rivals":
                {
                    System.Collections.Generic.List<string> rivals = PvpRoam.RivalLines(player);
                    if (rivals.Count == 0)
                        Say("You have no rivals yet: nobody you have wiped, and nobody who has wiped you.");
                    foreach (string line in rivals)
                        Say(line);
                    return;
                }

                case "who":
                {
                    System.Collections.Generic.List<string> who = PvpRoam.WhoLines(player);
                    if (who.Count == 0)
                        Say("Nobody is out.");
                    foreach (string line in who)
                        Say(line);
                    return;
                }

                case "status":
                case "roam-status":
                case "teams":
                {
                    // What each team of the roam is doing: the owner, 2026-11-22, "it's hard to know if both opposite
                    // teams are at keep buffing, fighting or roaming".
                    System.Collections.Generic.List<string> lines = PvpRoam.StatusLines(player);
                    if (lines.Count == 0)
                        Say("No roam is running. /8v8 roam starts one.");
                    foreach (string line in lines)
                        Say(line);
                    return;
                }

                case "ranks":
                {
                    static string Rank(long points) => $"{PvpRealmPoints.RankName(PvpRealmPoints.LevelFor(points))} ({points:N0})";
                    System.Collections.Generic.List<PvpRosterEntry> mine = PvpRoster.Load(System.AppContext.BaseDirectory, player.InternalID);
                    Say($"In real fights you are at {Rank(PvpPlayerReady.RealPointsOf(player))}; your character stands at {PvpRealmPoints.RankName(player.RealmLevel)} now.");
                    mine = mine.Where(entry => entry.Build != PvpRealmPoints.PlayerBuild).ToList();
                    Say(mine.Count == 0 ? "You have no companions yet: they come with your first fight."
                        : "Your companions: " + string.Join(", ", mine.Select(entry => $"{entry.Name} ({entry.Build}) {Rank(entry.RealmPoints)}")));
                    // Every team this character has met: the one of a session, and the two of each line-up in the roam.
                    foreach (string team in PvpBuildCatalog.Groups.Concat(PvpBuildCatalog.Variants)
                                 .SelectMany(group => new[] { PvpRoam.TeamName(group.Id, 1), PvpRoam.TeamName(group.Id, 2) }))
                    {
                        System.Collections.Generic.List<PvpRosterEntry> theirs = PvpRoster.Load(System.AppContext.BaseDirectory,
                            PvpRealmPoints.EnemyRoster(player.InternalID, team));
                        if (theirs.Count > 0)
                            Say($"{team}: " + string.Join(", ", theirs.Select(entry => $"{entry.Name} ({entry.Build}) {Rank(entry.RealmPoints)}")));
                    }
                    return;
                }

                case "corpse":
                {
                    PvpCorpseShow? show = (args.Length > 2 ? args[2].ToLowerInvariant() : string.Empty) switch
                    {
                        "player" => PvpCorpseShow.Player,
                        "body" => PvpCorpseShow.Body,
                        "fall" => PvpCorpseShow.Fall,
                        "ghost" => PvpCorpseShow.Ghost,
                        "off" => PvpCorpseShow.Off,
                        _ => null
                    };
                    if (show == null)
                    {
                        Say($"A dead 8v8 bot is shown as: {PvpCorpse.Show}. /8v8 corpse player, body, fall, ghost or off changes it.");
                        return;
                    }
                    int lying = PvpCorpse.Switch(show.Value, PvpArena.Members);
                    Say($"A dead 8v8 bot is now shown as: {PvpCorpse.Show} ({lying} dead shown again). It lasts until the server restarts.");
                    return;
                }

                case "stop":
                    PvpScrim.Stop();
                    Say("It is ended and the 8v8 bots are gone; what was earned is kept. /8v8 evening begins an evening.");
                    return;

                case "clear":
                    Say($"Removed {PvpArena.Clear()} 8v8 bots.");
                    return;

                case "lanes":
                case "watch":
                case "feed":
                    if (client.Account.PrivLevel < (uint)ePrivLevel.GM)
                    {
                        Say("Only a GM can watch scrims. Tick \"Make me GM\" in the launcher with the server stopped.");
                        return;
                    }
                    if (args[1].ToLowerInvariant() == "feed")
                    {
                        bool on = !(args.Length > 2 && args[2].ToLowerInvariant() == "off");
                        PvpObserver.Set(player, on);
                        Say(on ? "The feed is on: you read the calls of the lane you are in." : "The feed is off.");
                        return;
                    }
                    System.Collections.Generic.List<string> lanes = PvpScrim.LaneLines();
                    if (args[1].ToLowerInvariant() == "lanes")
                    {
                        Say(lanes.Count == 0 ? "No scrim is running. Start one with source/tools/pvp_watch.py." : "Fighting now:");
                        foreach (string line in lanes)
                            Say("  " + line);
                        return;
                    }
                    int lane = args.Length > 2 && int.TryParse(args[2], out int asked) ? asked : 0;
                    if (PvpScrim.LaneMiddle(lane) is not { } middle)
                    {
                        Say("There are six lanes, 0 to 5.");
                        return;
                    }
                    PvpObserver.Set(player, true, lane);
                    player.MoveTo(middle.Region, (int)middle.Point.X, (int)middle.Point.Y, (int)middle.Point.Z + 300, player.Heading);
                    Say($"Lane {lane}. The feed is on for this lane only; /fly to hover, /8v8 feed off to silence it.");
                    foreach (string line in lanes.Where(line => line.StartsWith($"lane {lane}:")))
                        Say("  " + line);
                    return;

                default:
                    DisplaySyntax(client);
                    return;
            }
        }
    }
}
