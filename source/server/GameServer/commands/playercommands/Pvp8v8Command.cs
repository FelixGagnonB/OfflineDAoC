using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>The 8v8 mode from inside the game (docs/PVP-8V8-ROADMAP.md, M1.6).</summary>
    [CmdAttribute("&8v8", ePrivLevel.Player, "8v8 group: seven level-50 companions built around your class",
        "/8v8 group [realm rank 1-13]   your realm's main group without your own class, fully buffed",
        "/8v8 setup                     a window to pick the enemy realm, the start (lane or basin) and the realm rank, then start",
        "/8v8 ready [build]             your character becomes level 50 with that build's class, spec and gear (lists the builds without one)",
        "/8v8 play <alb|mid|hib> [rank] [lane|basin] [follow]  a fight: you and seven companions against that realm's group of eight, in Emain Macha; follow: a bot drives",
        "/8v8 play hib-2bard ...  or  /8v8 play mid hib-2bard ...  another group by its name: as the enemy (first), or as your own (later)",
        "/8v8 play <alb|mid|hib> real [lane|basin] [follow]  a real fight: everybody at the rank it has earned, kills earn realm points that are kept, no timer is reset",
        "/8v8 ranks                     the realm ranks of your companions, and of the enemy groups you have fought for real",
        "/8v8 call <push|regroup|hold|engage|extend|assist>  your call to the group: an order stands twelve seconds; assist puts the damage dealers on your target while it lives (45 s at most)",
        "/8v8 calls <on|off>            your companions' calls in group chat (assist, incoming, orders, out of power)",
        "/8v8 corpse <player|body|fall|ghost|off>  how a dead 8v8 bot is shown: lying as a dead player does, to click (player); a body under its name that only the group window targets (body); the body and a ghost standing over it (fall); the ghost only (ghost); standing as if alive (off)",
        "/8v8 stop                      ends the fight and removes every 8v8 bot",
        "/8v8 clear                     removes every 8v8 bot",
        "/8v8 lanes                     (GM) what is fighting in each lane of the arena",
        "/8v8 watch <lane>              (GM) takes you to the middle of that lane and turns the feed on; then /fly",
        "/8v8 feed <on|off>             (GM) each group's calls in your chat window, for the lane you watch or stand in")]
    public sealed class Pvp8v8CommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null)
                return;
            void Say(string text) => client.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_SystemWindow);

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
                        Say("You call to a group in a fight: start one with /8v8 setup.");
                        return;
                    }
                    string word = args.Length > 2 ? args[2].ToLowerInvariant() : string.Empty;
                    if (word == "assist")
                    {
                        if (!squad.CallAssist(player, player.TargetObject as GameLiving))
                            Say("Target an enemy your group can see, then /8v8 call assist.");
                        return;
                    }
                    if (PvpCalls.OrderFor(word) is not { } called)
                    {
                        Say("/8v8 call push, regroup, hold, engage, extend or assist.");
                        return;
                    }
                    squad.CallOrder(player, called);
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
                    string refusal = PvpScrim.IsPlaying(player) ? "A fight is running. /8v8 stop first."
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
                        foreach (string line in PvpPlayerReady.Apply(player, build))
                            Say(line);
                    }
                    catch (System.Exception exception)
                    {
                        Say("Making the character ready failed part way: " + exception.Message);
                    }
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
                    foreach (PvpGroupTemplate group in PvpBuildCatalog.Groups.Concat(PvpBuildCatalog.Variants))
                    {
                        System.Collections.Generic.List<PvpRosterEntry> theirs = PvpRoster.Load(System.AppContext.BaseDirectory,
                            PvpRealmPoints.EnemyRoster(player.InternalID, group.Id));
                        if (theirs.Count > 0)
                            Say($"{group.Id}: " + string.Join(", ", theirs.Select(entry => $"{entry.Name} ({entry.Build}) {Rank(entry.RealmPoints)}")));
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
                    Say("The fight is ended and the 8v8 bots are gone.");
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
