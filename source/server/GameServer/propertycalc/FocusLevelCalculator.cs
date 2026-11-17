using System;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// The Focus Level calculator
    ///
    /// BuffBonusCategory1 is used for buffs, uncapped
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 unused
    /// BuffBonusCategory4 unused
    /// BuffBonusMultCategory1 unused
    /// </summary>
    [PropertyCalculator(eProperty.Focus_Darkness, eProperty.Focus_Matter)]
    [PropertyCalculator(eProperty.Focus_Mind, eProperty.Focus_Arboreal)]
    [PropertyCalculator(eProperty.Focus_EtherealShriek, eProperty.Focus_Witchcraft)]
    public class FocusLevelCalculator : PropertyCalculator
    {
        public FocusLevelCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            // An 8v8 bot's focus staff counts as a player's does; without it the bot paid 120% of
            // a spell's power where the player pays 80%.
            ICharacterClass characterClass = living switch
            {
                GamePlayer gamePlayer => gamePlayer.CharacterClass,
                GameBot { IsPvpBot: true } bot => bot.CharacterClass,
                _ => null,
            };

            if (characterClass != null)
            {
                GameLiving player = living;
                int itemBonus = player.ItemBonus[property];
                int focusLevel = player.BaseBuffBonusCategory[property];

                if (SkillBase.CheckPropertyType(property, ePropertyType.Focus) && characterClass.IsFocusCaster)
                {
                    focusLevel += player.BaseBuffBonusCategory[eProperty.AllFocusLevels];
                    itemBonus = Math.Max(itemBonus, player.ItemBonus[eProperty.AllFocusLevels]);
                }

                return focusLevel + Math.Min(50, itemBonus);
            }

            return 0;
        }
    }
}
