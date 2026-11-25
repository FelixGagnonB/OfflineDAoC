using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// Every guild of an 8v8 evening wears an emblem (docs/PVP-8V8-ROADMAP.md, M13.12). The
    /// owner, 2026-11-25, after the first evening on the play install: "Can I still visite the
    /// capital city to change the guild embleem? We all wear the default all green, red, blue. It
    /// would be nice if each guild has it's own embleem and using it".
    ///
    /// The game's own way is the emblem keeper in the capital: the guild's first emblem is free
    /// there, a second costs 200 gold, and each cloak or shield costs 5 gold to have it put on;
    /// an 8v8 character has no coin, and its seven are not players that could hand over a cloak.
    /// So: <c>/8v8 guild emblem</c> opens the game's emblem window wherever the player stands,
    /// at no cost and as often as it likes; the player's cloak and shields wear what it picks;
    /// its companions wear it; and a guild of bots wears one made from its name, the same every
    /// evening.
    /// </summary>
    public static class PvpGuildEmblem
    {
        /// <summary>Set while the emblem window opened by <c>/8v8 guild emblem</c> is open: its answer is taken here and not by the game's own rule.</summary>
        public const string AskedProperty = "PVP_8V8_EMBLEM_ASKED";

        /// <summary>An emblem as the game keeps it: the logo, the pattern (0 to 3), the first colour (0 to 15) and the second (0 to 7).</summary>
        public static int Pack(int logo, int pattern, int primaryColor, int secondaryColor) =>
            (logo << 9) | (pattern << 7) | (primaryColor << 3) | secondaryColor;

        /// <summary>
        /// The emblem of a guild of bots: made from its name, so that it is its own and the same
        /// every evening. The logo is kept under 91 (a number the client has a picture for in
        /// every realm, as far as is known; one it has none for shows the pattern and colours
        /// alone) and the two colours are never the same.
        /// </summary>
        public static int Made(string guild)
        {
            if (string.IsNullOrWhiteSpace(guild))
                return 0;
            uint hash = 2166136261;
            foreach (char letter in guild.Trim().ToLowerInvariant())
                hash = unchecked((hash ^ letter) * 16777619);
            int primary = (int)(hash % 16), secondary = (int)((hash >> 4) % 8);
            if (secondary == primary % 8)
                secondary = (secondary + 3) % 8;
            return Pack(1 + (int)((hash >> 12) % 90), (int)((hash >> 8) % 4), primary, secondary);
        }

        /// <summary>Which emblem a member of that guild wears: the one a player's guild has picked, else the one made from the name.</summary>
        public static int Worn(string guild, int pickedByItsPlayer) => string.IsNullOrWhiteSpace(guild) ? 0 : pickedByItsPlayer != 0 ? pickedByItsPlayer : Made(guild);

        /// <summary>What an 8v8 bot shows on its cloak and its shield (AutonomousBotGuilds.Texture); 0 for a bot with no guild.</summary>
        public static int Of(GameBot bot) =>
            bot?.PvpGuild is { Length: > 0 } guild ? Worn(guild, GuildMgr.GetGuildByName(guild)?.Emblem ?? 0) : 0;

        /// <summary>What the player is told when it asks for the emblem window; null when the window opens.</summary>
        public static string CannotAsk(bool hasAGuild, bool isItsGuildmaster) =>
            !hasAGuild ? "You have no guild: /8v8 guild <name> forms one."
            : !isItsGuildmaster ? "Only the guildmaster picks the guild's emblem."
            : null;

        /// <summary><c>/8v8 guild emblem</c>: opens the game's emblem window. What the player is told.</summary>
        public static string Ask(GamePlayer player)
        {
            if (CannotAsk(player.Guild != null, player.Guild?.HasRank(player, Guild.eRank.Leader) == true) is { } why)
                return why;
            player.TempProperties.SetProperty(AskedProperty, true);
            player.Out.SendEmblemDialogue();
            return "Pick your guild's emblem in the window. It costs nothing, and you may pick again whenever you like.";
        }

        /// <summary>The emblem window was closed with an emblem. True when it was the one this command opened: the game's own rule is then not applied.</summary>
        public static bool Chosen(GamePlayer player, int emblem)
        {
            if (player?.TempProperties.GetProperty<bool>(AskedProperty) != true)
                return false;
            player.TempProperties.RemoveProperty(AskedProperty);
            void Say(string text) => player.Out.SendMessage("[8v8] " + text, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            if (player.Guild is not { } guild || !guild.HasRank(player, Guild.eRank.Leader))
                return true;
            if (emblem == 0)
            {
                Say("No emblem was picked.");
                return true;
            }
            guild.Emblem = emblem;
            guild.SaveIntoDatabase();
            int items = Wear(player);
            // Its seven wear it at once.
            foreach (GameBot bot in player.Group?.GetMembersInTheGroup().OfType<GameBot>().Where(bot => bot.IsPvpBot) ?? [])
                bot.BroadcastLivingEquipmentUpdate();
            Say($"{guild.Name} has its emblem. It is on {items} of your cloaks and shields, and your companions wear it.");
            return true;
        }

        /// <summary>The player's cloaks and shields, worn or carried, take its guild's emblem; how many were changed.</summary>
        public static int Wear(GamePlayer player)
        {
            int emblem = player?.Guild?.Emblem ?? 0;
            if (emblem == 0)
                return 0;
            List<DbInventoryItem> changed = player.Inventory.AllItems
                .Where(item => item != null && item.SlotPosition <= (int)eInventorySlot.LastBackpack &&
                               (item.Object_Type == (int)eObjectType.Shield || item.Item_Type == Slot.CLOAK) && item.Emblem != emblem)
                .ToList();
            foreach (DbInventoryItem item in changed)
                item.Emblem = emblem;
            if (changed.Count > 0)
            {
                player.Out.SendInventoryItemsUpdate(changed);
                player.UpdateEquipmentAppearance();
            }
            return changed.Count;
        }
    }
}
