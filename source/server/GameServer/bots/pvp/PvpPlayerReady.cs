using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// Makes a player's own character ready for 8v8 (docs/PVP-8V8-ROADMAP.md, M1.4): the class of
    /// a catalogue build, level 50, the build's specialization lines, and the same gear a bot of
    /// that build wears. It changes the character for good, so the command asks for it twice.
    /// Realm rank and realm abilities are left as they are.
    /// </summary>
    public static class PvpPlayerReady
    {
        /// <summary>The build a player was last made ready as, so the companions leave out that build and not only that class.</summary>
        public const string BuildProperty = "PVP_8V8_READY_BUILD";

        /// <summary>The builds a player of this realm may take: those of the realm's classes, the buffbot's aside.</summary>
        public static IEnumerable<PvpBuild> BuildsFor(eRealm realm) =>
            PvpBuildCatalog.Builds.Where(build => PvpBuildCatalog.RealmOf(build.Class) == realm && !build.Id.StartsWith("buffbot"));

        /// <summary>
        /// The place in a group that the player takes, so its companions are the other seven: the
        /// build the player was made ready as, else the first build of the player's class, else
        /// the last place.
        /// </summary>
        public static int SlotOf(IReadOnlyList<string> builds, eCharacterClass playerClass, string readyBuild)
        {
            int own = readyBuild == null ? -1 : builds.ToList().FindIndex(id => string.Equals(id, readyBuild, StringComparison.OrdinalIgnoreCase));
            if (own < 0)
                own = builds.ToList().FindIndex(id => PvpBuildCatalog.Find(id)?.Class == playerClass);
            return own >= 0 ? own : builds.Count - 1;
        }

        /// <summary>A build by its name, or the first build of a class by the class's name ("cleric").</summary>
        public static PvpBuild Find(eRealm realm, string asked) =>
            BuildsFor(realm).FirstOrDefault(build => string.Equals(build.Id, asked, StringComparison.OrdinalIgnoreCase)) ??
            BuildsFor(realm).FirstOrDefault(build => string.Equals(build.Class.ToString(), asked, StringComparison.OrdinalIgnoreCase));

        /// <summary>The realm points a realm level stands for (rank 5 is level 40), and 0 below rank 1 level 1.</summary>
        public static long RealmPointsFor(int realmLevel) =>
            realmLevel <= 0 ? 0 : GamePlayer.REALMPOINTS_FOR_LEVEL[Math.Min(realmLevel, GamePlayer.REALMPOINTS_FOR_LEVEL.Length - 1)];

        /// <summary>
        /// Gives the player the realm rank a session is played at, when it is higher than the
        /// player's own, so there are realm ability points to spend as the bots of that rank have
        /// (owner, 2026-11-19: "even if I pick realm rank 5, I have no realm points and can't train
        /// purge"). Never lowers a rank: abilities already bought stay bought. Returns what to tell
        /// the player, or null when nothing changed.
        /// </summary>
        public static string RaiseRealmRank(GamePlayer player, int realmLevel)
        {
            if (player == null || realmLevel <= player.RealmLevel)
                return null;
            player.RealmPoints = Math.Max(player.RealmPoints, RealmPointsFor(realmLevel));
            player.RealmLevel = realmLevel;
            player.Out.SendUpdatePlayer();
            player.Out.SendUpdatePoints();
            player.Out.SendUpdatePlayerSkills(true);
            player.SaveIntoDatabase();
            return $"You are now realm rank {realmLevel / 10 + 1} with {player.RealmSpecialtyPoints} realm ability points to spend: /train opens the trainer window.";
        }

        /// <summary>
        /// Puts the character at the rank it has earned in real sessions (PvpRealmPoints), up or
        /// down: a practice session may have raised it. Going down, its realm abilities are handed
        /// back, since the points that bought them are gone. Returns what to tell the player, or
        /// null when nothing changed.
        /// </summary>
        public static string SetRealRank(GamePlayer player, long earned)
        {
            if (player == null)
                return null;
            earned = Math.Max(0, earned);
            int level = PvpRealmPoints.LevelFor(earned);
            if (player.RealmPoints == earned && player.RealmLevel == level)
                return null;
            bool lowered = level < player.RealmLevel;
            string was = PvpRealmPoints.RankName(player.RealmLevel);
            player.RealmPoints = earned;
            player.RealmLevel = level;
            if (lowered)
                player.RespecRealm(false);
            player.Out.SendUpdatePlayer();
            player.Out.SendUpdatePoints();
            player.Out.SendUpdatePlayerSkills(true);
            player.SaveIntoDatabase();
            return $"A real fight: you are at realm rank {PvpRealmPoints.RankName(level)}, what you have earned in real fights (you were {was})." +
                   (lowered ? $" Your realm abilities are handed back; you have {player.RealmSpecialtyPoints} points to spend with /train." : string.Empty);
        }

        /// <summary>
        /// The start of a real session or a roam for a character: it is in the roster beside its
        /// companions, and at the rank it has earned in real sessions. Returns what to tell the player, or null.
        /// </summary>
        public static string EnterReal(GamePlayer player)
        {
            List<PvpRosterEntry> roster = PvpRoster.Load(AppContext.BaseDirectory, player.InternalID);
            if (PvpRoster.Find(roster, PvpRealmPoints.PlayerBuild, 0) == null)
            {
                roster.Add(new PvpRosterEntry(PvpRealmPoints.PlayerBuild, 0, player.Name, (int)player.Gender));
                PvpRoster.Save(AppContext.BaseDirectory, player.InternalID, roster);
            }
            return SetRealRank(player, RealPointsOf(player));
        }

        /// <summary>What a character has earned in real sessions; 0 for one that has been in none.</summary>
        public static long RealPointsOf(GamePlayer player) =>
            PvpRoster.Find(PvpRoster.Load(AppContext.BaseDirectory, player.InternalID), PvpRealmPoints.PlayerBuild, 0)?.RealmPoints ?? 0;

        /// <summary>Null when the player may take the build, otherwise why not.</summary>
        public static string Refusal(eRealm realm, bool inCombat, bool alive, PvpBuild build)
        {
            if (build == null)
                return "There is no such build.";
            if (PvpBuildCatalog.RealmOf(build.Class) != realm)
                return $"{build.Id} is a build of another realm.";
            if (!alive)
                return "You are dead.";
            return inCombat ? "You are in combat." : null;
        }

        /// <summary>What was done, one line for each part, for the player's chat window.</summary>
        public static List<string> Apply(GamePlayer player, PvpBuild build)
        {
            var said = new List<string>();

            // Class and level. Reset takes the character back to level 1 with nothing trained; the
            // level set after it goes through every level-up again, as the GM's /player level does.
            bool newClass = player.CharacterClass.ID != (int)build.Class;
            if (newClass)
            {
                player.RemoveAllSpecs();
                player.RemoveAllSpellLines();
                player.styleComponent.RemoveAllStyles();
            }
            player.Reset();
            if (newClass)
            {
                player.SetCharacterClass((int)build.Class);
                player.Reset();
                player.OnLevelUp(0);
            }
            player.Level = PvpBuildBudget.MaxLevel;
            player.RefreshSpecDependantSkills(false);
            said.Add($"You are a level {player.Level} {player.CharacterClass.Name}.");

            // The build's lines. Every build is checked against the points a level-50 character has (UT_PvpBuildCatalog).
            foreach (PvpSpecLine line in build.Lines)
            {
                Specialization spec = player.GetSpecializationByName(line.Spec);
                if (spec == null)
                {
                    said.Add($"The line {line.Spec} was not found on your class and is left untrained.");
                    continue;
                }
                spec.Level = line.Level;
            }
            player.RefreshSpecDependantSkills(false);
            said.Add("Trained: " + string.Join(", ", build.Lines.Select(line => $"{line.Spec} {line.Level}")) +
                     $" ({player.SkillSpecialtyPoints} points left).");

            int pieces = Equip(player, build);
            said.Add($"{pieces} pieces of level-51 gear with the build's bonuses are on you; what you wore is in your backpack where there was room.");

            player.Health = player.MaxHealth;
            player.Mana = player.MaxMana;
            player.Endurance = player.MaxEndurance;
            player.TempProperties.SetProperty(BuildProperty, build.Id);
            // The skill list is rebuilt from nothing: lines the old class shared with the new one stayed in it at their old level.
            player.ForgetUsableSkills();
            player.Out.SendUpdatePlayer();
            player.Out.SendUpdatePlayerSkills(true);
            player.Out.SendUpdatePoints();
            player.Out.SendCharStatsUpdate();
            player.Out.SendCharResistsUpdate();
            player.UpdatePlayerStatus();
            player.SaveIntoDatabase();
            return said;
        }

        /// <summary>
        /// The gear of a bot of the build, copied piece by piece. Each copy is the player's own
        /// item (a unique template of its own, saved with the character), not the bot's.
        /// </summary>
        private static int Equip(GamePlayer player, PvpBuild build)
        {
            // A bot of the build that never enters the world: its constructor picks the gear and applies the template.
            var model = new GameBot(build, "model", (byte)PvpBuildCatalog.RaceOf(build.Class), (byte)player.Gender);
            int pieces = 0;
            try
            {
                foreach (PvpTemplateItem plan in model.PvpTemplate.Items)
                {
                    DbInventoryItem worn = model.Inventory.GetItem(plan.Slot);
                    if (worn?.Template == null)
                        continue;
                    DbInventoryItem old = player.Inventory.GetItem(plan.Slot);
                    if (old != null)
                    {
                        eInventorySlot free = player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        if (free == eInventorySlot.Invalid || !player.Inventory.MoveItem(plan.Slot, free, old.Count))
                            player.Inventory.RemoveItem(old);
                    }
                    GameInventoryItem item = GameInventoryItem.Create(new DbItemUnique(worn.Template));
                    item.Color = worn.Color;
                    item.SlotPosition = (int)plan.Slot;
                    PvpTemplateOutfitter.Apply(item, plan);
                    if (player.Inventory.AddItem(plan.Slot, item))
                        pieces++;
                }
            }
            finally
            {
                model.Delete();
            }
            player.SwitchWeapon(build.TwoHanded || build.WeaponOne == eObjectType.Staff ? eActiveWeaponSlot.TwoHanded : eActiveWeaponSlot.Standard);
            player.Out.SendInventoryItemsUpdate(player.Inventory.EquippedItems.ToList());
            player.BroadcastLivingEquipmentUpdate();
            return pieces;
        }
    }
}
