using System;

namespace DOL.GS
{
    /// <summary>
    /// What the bots of a group say in group chat to the player they fight beside
    /// (docs/PVP-8V8-ROADMAP.md, M9.2): the calls a group of players makes aloud. The squad
    /// already decides all of them; this is only their wording and how often they are said.
    /// </summary>
    public static class PvpCalls
    {
        /// <summary>Set on a player who turned the calls off (/8v8 calls off).</summary>
        public const string OffProperty = "PVP_8V8_CALLS_OFF";

        /// <summary>An order is said once it has stood this long; "incoming" no sooner than its own time after the last.</summary>
        public const int OrderSettlesMilliseconds = 3_000, IncomingEveryMilliseconds = 8_000;

        public static bool OrderIsSettled(long now, long since) => now - since >= OrderSettlesMilliseconds;

        public static string Order(PvpOrder order) => order switch
        {
            PvpOrder.Engage => "Engage.",
            PvpOrder.Push => "Push!",
            PvpOrder.Regroup => "Regroup on the healers.",
            PvpOrder.Extend => "They are backing off: extend on them.",
            PvpOrder.Hold => "Hold here and let them come.",
            _ => order + ".",
        };

        public static string Assist(string name, eCharacterClass characterClass, int healthPercent) =>
            $"Assist {name} ({characterClass})" + (healthPercent < 100 ? $", at {healthPercent}%." : ".");

        /// <summary>Said by the member itself, or by another for the player it is said to.</summary>
        public static string Incoming(string victim, int attackers, bool saidToTheVictim) =>
            saidToTheVictim ? $"{attackers} on you, {victim}!" : $"Incoming on me: {attackers}.";

        public const string OutOfPower = "I am out of power.";

        public static string Gather(int pets) => $"Stack on me: {pets} pets to clear.";

        /// <summary>
        /// Said to a player who heals when the group has moved out of that player's reach: in the
        /// owner's five sessions as a Cleric two or more of the group were beyond 2,000 units of
        /// the owner a quarter of the time (9 to 46%), against 5% for the bot healer beside, and
        /// the owner could not tell ("out of range of many casters, or they pushed too much").
        /// </summary>
        public static string OutOfYourReach(string healer, int members) => $"{healer}, {members} of us are out of your reach.";

        /// <summary>That call is for two or more members, and no more often than this.</summary>
        public const int OutOfReachEveryMilliseconds = 10_000;

        public static bool WorthOutOfReach(int members) => members >= 2;

        /// <summary>How long a call by the player stands before the squad decides for itself again, in seconds.</summary>
        public const int PlayerCallSeconds = 12;

        /// <summary>
        /// The order a player's call stands for (/8v8 call regroup): push, regroup, hold, engage,
        /// or extend. Null for anything else, "assist" among them, which names a target and no order.
        /// </summary>
        public static PvpOrder? OrderFor(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "push" => PvpOrder.Push,
            "regroup" or "back" or "retreat" => PvpOrder.Regroup,
            "hold" or "wait" => PvpOrder.Hold,
            "engage" or "go" => PvpOrder.Engage,
            "extend" or "chase" => PvpOrder.Extend,
            _ => null,
        };

        /// <summary>What a bot answers to the player's call.</summary>
        public static string Answer(PvpOrder order, string caller) => order switch
        {
            PvpOrder.Push => "Pushing.",
            PvpOrder.Regroup => $"Regrouping on {caller}.",
            PvpOrder.Hold => "Holding.",
            PvpOrder.Extend => "Extending on them.",
            _ => "Engaging.",
        };

        /// <summary>The order in force: the player's call while it stands, else the squad's own choice.</summary>
        public static PvpOrder InForce(PvpOrder chosen, PvpOrder? called, long now, long calledUntil) =>
            called is { } order && now < calledUntil ? order : chosen;

        /// <summary>Whether a call of a kind may be said now, given when the last of that kind was.</summary>
        public static bool Due(long now, long last, int every) => every <= 0 || last <= 0 || now - last >= every;

        /// <summary>Only an attack by two or more is worth a call.</summary>
        public static bool WorthIncoming(int attackers) => attackers >= 2;
    }
}
