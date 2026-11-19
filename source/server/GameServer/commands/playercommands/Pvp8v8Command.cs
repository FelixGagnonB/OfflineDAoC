using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>The 8v8 mode from inside the game (docs/PVP-8V8-ROADMAP.md, M1.6).</summary>
    [CmdAttribute("&8v8", ePrivLevel.Player, "8v8 group: seven level-50 companions built around your class",
        "/8v8 group [realm rank 1-13]   your realm's main group without your own class, fully buffed",
        "/8v8 play <alb|mid|hib> [rank] a fight: you and seven companions against that realm's group of eight, in the basin of Emain Macha",
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
                    PvpGroupTemplate enemy = args.Length > 2 ? PvpBuildCatalog.Groups.FirstOrDefault(group =>
                        group.Id.StartsWith(args[2].ToLowerInvariant().Substring(0, System.Math.Min(3, args[2].Length)))) : null;
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
                    // The fight is in the open basin of Emain Macha. Getting there changes region, which the
                    // client needs a moment for: the first /8v8 play takes you there, the second starts.
                    (ushort region, System.Numerics.Vector3 start) = PvpScrim.PlayStart();
                    if (player.CurrentRegionID != region || !player.IsWithinRadius(new Point3D((int)start.X, (int)start.Y, (int)start.Z), 2_500))
                    {
                        player.MoveTo(region, (int)start.X, (int)start.Y, (int)start.Z, player.Heading);
                        Say("You are at the start, in Emain Macha. Type the same /8v8 play again to bring your companions and the enemy.");
                        return;
                    }
                    int playRank = args.Length > 3 && int.TryParse(args[3], out int wanted) ? System.Math.Clamp(wanted, 1, 13) : 5;
                    try
                    {
                        PvpScrim.StartPlay(player, enemy, (playRank - 1) * 10);
                    }
                    catch (System.Exception exception)
                    {
                        Say("The fight could not start: " + exception.Message);
                        return;
                    }
                    Say($"Seven companions are with you at realm rank {playRank}, and {enemy.Id} is at the far end of the basin, to the east. " +
                        "Both groups buff for about a minute; then the enemy roams. Lead your group where you like.");
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
