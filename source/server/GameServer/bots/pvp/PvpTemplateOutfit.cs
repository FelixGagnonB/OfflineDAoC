using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>What a build's template should reach, in order of need.</summary>
    public static class PvpTemplateNeeds
    {
        private static readonly eProperty[] Resists =
        [
            eProperty.Resist_Crush, eProperty.Resist_Slash, eProperty.Resist_Thrust,
            eProperty.Resist_Body, eProperty.Resist_Cold, eProperty.Resist_Heat,
            eProperty.Resist_Energy, eProperty.Resist_Matter, eProperty.Resist_Spirit,
        ];

        /// <param name="manaStat">The class's casting stat, or UNDEFINED for a pure fighter.</param>
        /// <param name="specToSkill">Maps a specialization line to its +skill property; SkillBase.SpecToSkill in the game.</param>
        public static IReadOnlyList<PvpTemplateNeed> For(PvpBuild build, eStat manaStat, Func<string, eProperty> specToSkill)
        {
            var needs = new List<PvpTemplateNeed>();
            void Stat(eProperty property, int priority)
            {
                if (property != eProperty.Undefined && needs.All(need => need.Property != property))
                    needs.Add(new(property, PvpBonusKind.Stat, PvpTemplateGenerator.StatCap, priority));
            }

            eProperty casting = manaStat == eStat.UNDEFINED ? eProperty.Undefined : (eProperty)manaStat;
            bool fighter = PvpBuffPlan.ArchetypeOf(build.Class) == PvpBuffArchetype.Melee;
            if (fighter)
            {
                Stat(eProperty.Strength, 1);
                Stat(eProperty.Constitution, 1);
                Stat(eProperty.Dexterity, 1);
            }
            else
            {
                Stat(casting, 1);
                Stat(eProperty.Dexterity, 1);
                Stat(eProperty.Constitution, 1);
            }
            needs.Add(new(eProperty.MaxHealth, PvpBonusKind.Hits, PvpTemplateGenerator.HitsCap, 1));

            // The two lines the build is made of first; lesser lines only once resists are in.
            int rank = 0;
            foreach (PvpSpecLine line in build.Lines.OrderByDescending(line => line.Level))
            {
                eProperty skill = specToSkill(line.Spec);
                if (skill == eProperty.Undefined || needs.Any(need => need.Property == skill))
                    continue;
                if (rank < 2)
                    needs.Add(new(skill, PvpBonusKind.Skill, PvpTemplateGenerator.SkillCap, 1));
                else if (line.Level >= 20)
                    needs.Add(new(skill, PvpBonusKind.Skill, PvpTemplateGenerator.SkillCap, 3));
                rank++;
            }

            foreach (eProperty resist in Resists)
                needs.Add(new(resist, PvpBonusKind.Resist, PvpTemplateGenerator.ResistCap, 2));

            if (fighter)
            {
                Stat(eProperty.Quickness, 3);
                Stat(casting, 3);
            }
            else if (build.SpecType == eSpecType.Instrument)
                Stat(eProperty.Strength, 3);
            return needs;
        }
    }

    /// <summary>Turns the gear a bot already wears into its template gear.</summary>
    public static class PvpTemplateOutfitter
    {
        /// <summary>
        /// Keeps each equipped item's kind and look, makes it a level-51, 100% quality piece and
        /// replaces its bonuses with the template's. Procs and charges are removed so every 8v8
        /// bot starts from the same plain crafted gear.
        /// </summary>
        public static PvpTemplate Outfit(GameBot bot, Func<string, eProperty> specToSkill = null)
        {
            // None of the 8v8 builds is an archer. A fighter handed a bow kept it as its active
            // weapon, which switched off every bonus on its real weapons (build audit, 2026-11-16).
            DbInventoryItem ranged = bot.Inventory?.GetItem(eInventorySlot.DistanceWeapon);
            if (ranged != null)
                bot.Inventory.RemoveItem(ranged);

            List<DbInventoryItem> worn = (bot.Inventory?.EquippedItems ?? []).ToList();
            IReadOnlyList<PvpTemplateNeed> needs = PvpTemplateNeeds.For(bot.PvpBuild,
                bot.CharacterClass?.ManaStat ?? eStat.UNDEFINED, specToSkill ?? SkillBase.SpecToSkill);
            // Casters hold their staff in the two-hand slot.
            bool twoHanded = bot.PvpBuild.TwoHanded || bot.PvpBuild.WeaponOne == eObjectType.Staff;
            PvpTemplate template = PvpTemplateGenerator.Generate(needs,
                PvpTemplateGenerator.SlotsFor(worn.Select(item => (eInventorySlot)item.SlotPosition), twoHanded));

            foreach (PvpTemplateItem plan in template.Items)
            {
                DbInventoryItem item = worn.First(candidate => candidate.SlotPosition == (int)plan.Slot);
                Apply(item, plan);
            }
            // One colour per realm on armour and cloak (owner, 2026-11-18: so that it is easier to
            // see who is from which realm when flying over a fight). Looks only: no bonus changes.
            int colour = RealmColour(bot.Realm);
            if (colour != 0)
            {
                foreach (DbInventoryItem item in worn.Where(item => (eInventorySlot)item.SlotPosition is eInventorySlot.HeadArmor or
                             eInventorySlot.HandsArmor or eInventorySlot.FeetArmor or eInventorySlot.TorsoArmor or eInventorySlot.LegsArmor or
                             eInventorySlot.ArmsArmor or eInventorySlot.Cloak))
                    item.Color = colour;
            }
            // Only the weapons in hand count, and a new bot may start with its bow out: put the
            // build's own weapons in hand (this also recounts the item bonuses).
            bot.SwitchWeapon(twoHanded ? eActiveWeaponSlot.TwoHanded : eActiveWeaponSlot.Standard);
            bot.RefreshItemBonuses();
            bot.BroadcastLivingEquipmentUpdate();
            return template;
        }

        /// <summary>Albion red, Midgard blue, Hibernia green: the old dyes, which take on cloth, leather and metal alike.</summary>
        public static int RealmColour(eRealm realm) => realm switch
        {
            eRealm.Albion => (int)eColor.Old_Red,
            eRealm.Midgard => (int)eColor.Old_Blue,
            eRealm.Hibernia => (int)eColor.Old_Green,
            _ => 0,
        };

        public static void Apply(DbInventoryItem item, PvpTemplateItem plan)
        {
            item.Level = PvpTemplateGenerator.ItemLevel;
            item.Quality = 100;
            item.Condition = item.MaxCondition;
            item.Durability = item.MaxDurability;

            var slot = (eInventorySlot)item.SlotPosition;
            bool armour = slot is eInventorySlot.HeadArmor or eInventorySlot.HandsArmor or eInventorySlot.FeetArmor or
                eInventorySlot.TorsoArmor or eInventorySlot.LegsArmor or eInventorySlot.ArmsArmor;
            bool weapon = slot is eInventorySlot.RightHandWeapon or eInventorySlot.LeftHandWeapon or
                    eInventorySlot.TwoHandWeapon or eInventorySlot.DistanceWeapon &&
                item.Object_Type is not ((int)eObjectType.Shield or (int)eObjectType.Instrument);
            // The same level formulas the item generator uses (GeneratedUniqueItem).
            if (armour)
            {
                item.DPS_AF = item.Object_Type == (int)eObjectType.Cloth ? item.Level : item.Level * 2;
                // The absorb of its armor class, which is what the hit piece gives a player.
                item.SPD_ABS = AbsorbOf((eObjectType)item.Object_Type);
            }
            else if (weapon)
                item.DPS_AF = (int)((item.Level * 0.3 + 1.2) * 10);
            // The to-hit bonus of top-quality level 51 gear, on armor and weapons alike, so the two
            // cancel in the miss chance as they do between equally equipped players.
            if (armour || weapon)
                item.Bonus = 35;

            item.ProcSpellID = item.ProcSpellID1 = 0;
            item.SpellID = item.SpellID1 = 0;
            item.Charges = item.Charges1 = 0;
            item.ExtraBonus = item.ExtraBonusType = 0;
            (item.Bonus1, item.Bonus1Type) = At(plan, 0);
            (item.Bonus2, item.Bonus2Type) = At(plan, 1);
            (item.Bonus3, item.Bonus3Type) = At(plan, 2);
            (item.Bonus4, item.Bonus4Type) = At(plan, 3);
            // A caster's staff focuses every line at 50, as a crafted focus staff does.
            bool staff = item.Object_Type == (int)eObjectType.Staff;
            (item.Bonus5, item.Bonus5Type) = staff ? (50, (int)eProperty.AllFocusLevels) : (0, 0);
            item.Bonus6 = item.Bonus6Type = 0;
            item.Bonus7 = item.Bonus7Type = 0;
            item.Bonus8 = item.Bonus8Type = 0;
            item.Bonus9 = item.Bonus9Type = 0;
            item.Bonus10 = item.Bonus10Type = 0;
        }

        public static int AbsorbOf(eObjectType armor) => armor switch
        {
            eObjectType.Leather => 10,
            eObjectType.Studded or eObjectType.Reinforced => 19,
            eObjectType.Chain or eObjectType.Scale => 27,
            eObjectType.Plate => 34,
            _ => 0,   // cloth
        };

        private static (int Value, int Type) At(PvpTemplateItem plan, int index) =>
            index < plan.Bonuses.Count ? (plan.Bonuses[index].Value, (int)plan.Bonuses[index].Property) : (0, 0);
    }
}
