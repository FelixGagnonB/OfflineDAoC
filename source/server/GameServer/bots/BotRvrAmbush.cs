namespace DOL.GS
{
    public static class BotRvrAmbush
    {
        public static bool IsStealthClass(eCharacterClass characterClass) => BotPoisonSupply.IsAssassin(characterClass) ||
            characterClass is eCharacterClass.Hunter or eCharacterClass.Scout or eCharacterClass.Ranger or eCharacterClass.Minstrel;

        public static bool IsEnemyCombatant(GameBot bot, GameLiving target) =>
            target is GamePlayer or GameBot && target.IsAlive && target.Realm != eRealm.None &&
            target.Realm != bot.Realm && target.CurrentRegionID == bot.CurrentRegionID;

        // Never in an 8v8 group (owner, 2026-11-19, watching the Minstrel): it fights in the open with its group.
        public static bool CanApproach(GameBot bot, GameLiving target) => bot != null && !bot.IsPvpBot &&
            IsStealthClass((eCharacterClass)bot.CharacterClass.ID) &&
            (bot.GetSpecializationByName(Specs.Stealth)?.Level ?? 0) > 0 &&
            bot.IsAlive && !bot.InCombat && !bot.IsAttacking && !bot.IsCasting && !bot.IsCrowdControlled &&
            !bot.IsOnHorse && bot.castingComponent?.HasPendingSkillRequests != true &&
            IsEnemyCombatant(bot, target) && bot.IsWithinRadius(target, 2_000) &&
            GameServer.ServerRules.IsAllowedToAttack(bot, target, true);
    }
}
