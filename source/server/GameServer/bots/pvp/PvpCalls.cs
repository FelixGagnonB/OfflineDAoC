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

        /// <summary>
        /// Set on a player who lets the group's own caller move it (/8v8 lead on): in a roam the
        /// seven then fall back as one by themselves when the group has too many on it
        /// (bots/pvp/PvpKite.cs), and say so loud. Off unless the player turns it on: a group that
        /// moves by itself leaves a player behind who has not seen the call.
        /// </summary>
        public const string LeadsProperty = "PVP_8V8_GROUP_LEADS";

        /// <summary>
        /// A call that moves the group is said this many times, in capitals. The owner,
        /// 2026-11-24: "If my group decide to call for a reaction (push, extend, retreat), then
        /// it would be to say in group chat but spam it in capital or something, 4-5 times, so I
        /// am aware. There are too much message in /group chat for me to notice otherwise".
        /// </summary>
        public const int LoudTimes = 4;

        public static string KiteCalled(int onIt, int ownSide) => $"KITE BACK! {onIt} ON OUR {ownSide}! KITE BACK!";
        public const string KiteAgain = "BACK AGAIN! KEEP MOVING!";

        /// <summary>
        /// What a group says that has too many on it and has not been given the lead: it stays
        /// where the player is, and tells the player what it counts and what can be called
        /// (/8v8 call kite, /8v8 call retreat). Said again no sooner than every 25 seconds.
        /// </summary>
        public static string TooMany(int onIt, int ownSide) => $"TOO MANY! {onIt} ON OUR {ownSide}! CALL IT: KITE OR RETREAT!";
        public const int TooManyEveryMilliseconds = 25_000;
        public const string KiteOver = "STAND AND FIGHT HERE!";

        /// <summary>An order is said once it has stood this long; "incoming" no sooner than its own time after the last.</summary>
        public const int OrderSettlesMilliseconds = 3_000, IncomingEveryMilliseconds = 8_000;

        public static bool OrderIsSettled(long now, long since) => now - since >= OrderSettlesMilliseconds;

        /// <summary>
        /// The group's own order as it is shouted when it moves the group: push, extend, regroup.
        /// The owner, 2026-11-24: "If my group decide to call for a reaction (push, extend,
        /// retreat), then it would be to say in group chat but spam it in capital or something,
        /// 4-5 times, so I am aware." Null for an order that moves nobody (engage, hold), which is
        /// said once as before.
        /// </summary>
        public static string LoudOrder(PvpOrder order) => order switch
        {
            PvpOrder.Push => "PUSH! PUSH! ALL ON THEM!",
            PvpOrder.Extend => "EXTEND! FALL BACK AND KITE!",
            PvpOrder.Regroup => "REGROUP! BACK TO THE HEALERS!",
            _ => null,
        };

        /// <summary>
        /// One shouted order in this long: in the owner's fight of 2026-11-24 against one group
        /// the order went push, extend, engage, push, extend, engage, push in 40 seconds, and
        /// four lines for each would have been what the shout is there to be seen through. The
        /// orders between are said once, in ordinary words.
        /// </summary>
        public const int LoudOrderEveryMilliseconds = 20_000;

        /// <summary>Whether an order is shouted: it moves the group, it is the group's own and not the player's call, and none was shouted just before.</summary>
        public static bool ShoutsOrder(PvpOrder order, bool thePlayerCalledIt, long now, long lastShout) =>
            LoudOrder(order) != null && !thePlayerCalledIt && Due(now, lastShout, LoudOrderEveryMilliseconds);

        public static string Order(PvpOrder order) => order switch
        {
            PvpOrder.Engage => "Engage.",
            PvpOrder.Push => "Push!",
            PvpOrder.Regroup => "Regroup on the healers.",
            // Extend is the kite (docs/PVP-8V8-PLAYBOOK.md, section 3): the group falls back together, its healers and
            // casters keep their distance and its fighters turn on whoever follows. It was worded as a chase until
            // 2026-11-21 ("They are backing off: extend on them"), which is the opposite, and the owner, having called
            // it: "I am not sure if /8v8 call extend works. I used it but I can't tell."
            PvpOrder.Extend => "Extend: fall back and kite, fighters on whoever follows.",
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

        /// <summary>
        /// Under a regroup the player called, a member farther than the first distance from the
        /// player comes back, and keeps coming until it is within the second: "regroup on me" is
        /// nearer than the 1,700 units at which the squad's own regroup lets a fighter go again
        /// (in the first test the members were back out at 1,850 before the call had run out).
        /// </summary>
        public const int CalledRegroupBeyond = 1_000, CalledRegroupUntilWithin = 600;

        public static bool ComesBack(int distance, bool onItsWayBack) =>
            distance > CalledRegroupBeyond || onItsWayBack && distance > CalledRegroupUntilWithin;

        /// <summary>How long a call by the player stands before the squad decides for itself again, in seconds.</summary>
        public const int PlayerCallSeconds = 12;

        /// <summary>
        /// How long an assist call stands at most. In the test of the call the group's damage
        /// dealers needed ten seconds to get to a Warrior and had it at half its health sixteen
        /// seconds later, when a 24-second call ran out.
        /// </summary>
        public const int PlayerAssistSeconds = 45;

        /// <summary>
        /// "Control that one" (/8v8 call control, M9.3): the player's target is the one the
        /// group's mez, stun, root and nearsight go to first, for this long or until it is held.
        /// </summary>
        public const int PlayerControlSeconds = 20;

        public static bool IsControlCall(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() is "control" or "mez" or "cc";

        public static string Control(string name, eCharacterClass characterClass) => $"Control on {name} ({characterClass}).";

        /// <summary>
        /// The kite the player calls (/8v8 call extend). The owner, 2026-11-22: "Extend should be
        /// us kiting back, in the opposite direction of the enemy team." The group's own Extend
        /// only shifts weights (a little more distance, the fighters on whoever follows), and a
        /// call for it changed nothing to be seen: in the one roam it was used in, the back line
        /// stood 500 units from the nearest enemy before and after. Called, every healer and
        /// caster that is not in the middle of a cast takes a step this long straight away from
        /// the enemy, again and again, while an enemy is nearer than <see cref="CalledKiteKeeps"/>;
        /// between two steps it has time for a cast. The fighters cover as under any Extend.
        /// </summary>
        public const int CalledKiteStep = 500, CalledKiteKeeps = 1_800, CalledKiteStepEveryMilliseconds = 2_500;

        public static bool KiteStepWanted(float nearestEnemy, long millisecondsSinceItsLastStep) =>
            nearestEnemy < CalledKiteKeeps && millisecondsSinceItsLastStep >= CalledKiteStepEveryMilliseconds;

        /// <summary>
        /// "/8v8 call rest": the owner, 2026-11-22, "to tell the bot to redo their self-timed
        /// buffs, druid resists, sit to regen endurance/mana. After a fight, I try to go hide
        /// somewhere to regain mana/endurance, but bot just stand up idle". While the call
        /// stands every bot of the group first casts what it keeps up (its own buffs, the group's
        /// resistances, its pet), then rests where it stands and regains health, power and
        /// endurance as a player sitting does (PvpRealWeights.RestRegainsPercentPerSecond); the
        /// Bard plays its endurance song. It ends when the time is up, with any other call, when
        /// somebody of the group is in combat, or when the player has walked this far from where
        /// it called.
        /// </summary>
        public const int PlayerRestSeconds = 120, RestEndsBeyond = 1_500;

        public static bool IsRestCall(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() is "rest" or "sit" or "buff";

        public const string Resting = "Resting: buffs first, then sitting down.";

        /// <summary>Whether a rest that was called still stands.</summary>
        public static bool RestStands(long now, long until, bool somebodyInCombat, int callerHasMoved) =>
            until != 0 && now < until && !somebodyInCombat && callerHasMoved <= RestEndsBeyond;

        /// <summary>
        /// "Retreat: everyone stop doing what they do and follow me" (the owner, 2026-11-21).
        /// Not an order the group fights under, like regroup: nobody attacks, casts or answers
        /// an attack; all of them run with the caller in travel order, the speed song on. It
        /// stands this long, is renewed by calling it again, and ends with any other call.
        /// </summary>
        public const int PlayerRetreatSeconds = 30;

        public static bool IsRetreatCall(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() is "retreat" or "run" or "follow";

        public static string Retreating(string caller) => $"Retreating with {caller}.";

        /// <summary>
        /// Where a caster's pet is sent: the enemy its owner interrupts, else the group's kill target.
        /// Not onto a target the player called with <c>/8v8 call assist</c>: the owner, 2026-11-22,
        /// "when I use /8v8 call assist, pets should not assist. It's for fighters / casters that are
        /// able to join". On such a call a pet goes on with the enemy it has, or with none.
        /// </summary>
        public static T PetAim<T>(T duty, T kill, T has, bool killIsThePlayersCall) where T : class =>
            duty ?? (killIsThePlayersCall ? has : kill);

        /// <summary>Said by the member that used it: a healer player need not cure what a Purge has taken off.</summary>
        public static string Purged(bool group) => group ? "Group Purge used." : "Purge used.";

        /// <summary>Said when a mez has put two or more of the enemy to sleep that were not asleep a second ago.</summary>
        public static string MezLanded(int asleep) => $"{asleep} of them are mezzed.";

        public const int MezLandedEveryMilliseconds = 6_000;

        public static bool WorthMezLanded(int asleep, int asleepBefore) => asleep >= 2 && asleep > asleepBefore;

        /// <summary>
        /// The order a player's call stands for (/8v8 call regroup): push, regroup, hold, engage,
        /// or extend. Null for anything else, "assist" among them, which names a target and no order.
        /// </summary>
        public static PvpOrder? OrderFor(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "push" => PvpOrder.Push,
            "regroup" or "back" => PvpOrder.Regroup,
            "hold" or "wait" => PvpOrder.Hold,
            "engage" or "go" => PvpOrder.Engage,
            "extend" or "kite" => PvpOrder.Extend,
            _ => null,
        };

        /// <summary>What a bot answers to the player's call.</summary>
        public static string Answer(PvpOrder order, string caller) => order switch
        {
            PvpOrder.Push => "Pushing.",
            PvpOrder.Regroup => $"Regrouping on {caller}.",
            PvpOrder.Hold => "Holding.",
            PvpOrder.Extend => "Extending: falling back, fighters on whoever follows.",
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
