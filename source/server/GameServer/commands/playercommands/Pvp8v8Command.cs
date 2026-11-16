using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>The 8v8 mode from inside the game (docs/PVP-8V8-ROADMAP.md, M1.6).</summary>
    [CmdAttribute("&8v8", ePrivLevel.Player, "8v8 group: seven level-50 companions built around your class",
        "/8v8 group [realm rank 1-13]   your realm's main group without your own class, fully buffed",
        "/8v8 clear                     removes every 8v8 bot")]
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

                case "clear":
                    Say($"Removed {PvpArena.Clear()} 8v8 bots.");
                    return;

                default:
                    DisplaySyntax(client);
                    return;
            }
        }
    }
}
