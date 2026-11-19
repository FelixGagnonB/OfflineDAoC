namespace DOL.GS
{
    /// <summary>
    /// What a member of an 8v8 group is, whether a bot or a player (docs/PVP-8V8-ROADMAP.md, M9).
    /// The squad brain was written for eight bots and read each one's build; the owner's group
    /// has a player in it, and the enemy has to see that player as a healer, a caster or a
    /// fighter like anybody else. A bot answers from its build, a player from its own class.
    /// </summary>
    public static class PvpMember
    {
        public static eCharacterClass ClassOf(GameLiving living) => living switch
        {
            GameBot { PvpBuild: { } build } => build.Class,
            IGamePlayer { CharacterClass: { } own } => (eCharacterClass)own.ID,
            _ => eCharacterClass.Unknown,
        };

        public static PvpBuffArchetype ArchetypeOf(GameLiving living) => PvpBuffPlan.ArchetypeOf(ClassOf(living));

        public static PvpRole RoleOf(GameLiving living) => PvpRoles.Of(ClassOf(living));

        public static bool IsFighter(GameLiving living) => ArchetypeOf(living) == PvpBuffArchetype.Melee;

        /// <summary>The build's name for the recordings: a bot's build, "player-" and the class for a player.</summary>
        public static string BuildOf(GameLiving living) =>
            living is GameBot { PvpBuild: { } build } ? build.Id : "player-" + ClassOf(living).ToString().ToLowerInvariant();
    }
}
