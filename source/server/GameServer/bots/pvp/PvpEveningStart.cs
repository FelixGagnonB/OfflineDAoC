using System;
using System.Linq;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// The evening begins when the player comes in (docs/PVP-8V8-ROADMAP.md, M13.2). Until
    /// 2026-11-24 a prime-time evening was begun with <c>/8v8 prime</c>, zone, number of groups
    /// and line-up typed each time. The owner, of the game as it is to be played on the play
    /// install: "I consider the /8v8 as testing commands"; asked whether the evening should begin
    /// at login or when the player steps out of its keep: "Yes, I would do it as I log in".
    ///
    /// A player with an 8v8 character (level 50, a class its realm has a build for) that enters
    /// the game while nothing runs has its evening begin where it stands: in the frontier zone it
    /// is in when it is in one, and else in the zone its last evening began in, with its seven
    /// waiting off the field until it is in the frontier (PvpRoam.PresenceOf). The line-up and the
    /// number of groups are those of its last evening (the realm's main one and
    /// PvpRealWeights.EveningGroups the first time). It is a real evening: what everybody earns
    /// is kept. It goes on wherever the player goes and ends when the player leaves the game.
    /// </summary>
    public static class PvpEveningStart
    {
        private static readonly Logger Log = LoggerManager.Create(typeof(PvpEveningStart));

        /// <summary>What a player that has just come in is told and whether its evening begins; null and true when it does, with nothing to say.</summary>
        public static (bool Begins, string Told) AtLogin(bool beginsAtLogin, bool hasAn8v8Character, bool somethingRuns, bool itIsThePlayersOwn)
        {
            if (!beginsAtLogin || itIsThePlayersOwn)
                return (false, null);
            if (!hasAn8v8Character)
                return (false, "This character is not an 8v8 character yet. /8v8 ready lists the builds of your realm; your evening begins once it is one.");
            if (somethingRuns)
                return (false, "A fight or an evening is running on this server already. Yours begins when it is over and you come in again.");
            return (true, null);
        }

        /// <summary>Whether a character is one the 8v8 is played with: level 50, of a class its realm has a build for.</summary>
        public static bool Is8v8Character(int level, bool itsClassHasABuild) => level >= 50 && itsClassHasABuild;

        [GameServerStartedEvent]
        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, new DOLEventHandler(OnEntered));
        }

        private const string EnteredProperty = "PVP_8V8_ENTERED_THE_GAME", BeginsProperty = "PVP_8V8_EVENING_BEGINS";

        /// <summary>
        /// Whether this coming in is the player's login. The game tells of a player "entering"
        /// each time its client has loaded a region, so also after every change of region: the
        /// port to the frontier that begins the evening, <c>/8v8 go</c>, and a walk through a
        /// portal to the capital. Without this a player that left the frontier for its trainer
        /// (the evening ends when its player is no longer in it) was taken back to Emain Macha
        /// four seconds after it arrived. Found reading the code on 2026-11-25, before the
        /// owner's first evening on the play install.
        /// </summary>
        public static bool IsLogin(bool enteredBeforeThisSession) => !enteredBeforeThisSession;

        /// <summary>A second start asked for while the first is still taking the player to the frontier is not made.</summary>
        public static bool IsUnderWay(long now, long begunAt) => begunAt > 0 && now - begunAt < 30_000;

        private static void OnEntered(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player || !PvpWeights.Current.Real.EveningAtLogin)
                return;
            if (!IsLogin(player.TempProperties.GetProperty<bool>(EnteredProperty)))
                return;
            player.TempProperties.SetProperty(EnteredProperty, true);
            // A moment for the client to have the world: the evening takes the player to its realm's start.
            new ECSGameTimer(player, _ =>
            {
                BeginFor(player);
                return 0;
            }, 4_000);
        }

        /// <summary>
        /// Begins the player's evening when it has none: on coming in, once <c>/8v8 ready</c> has
        /// made its character, and when the player asks (<c>/8v8 evening</c>, after it has been
        /// away from the frontier, which ends an evening).
        /// </summary>
        /// <param name="asked">The player typed it: it begins whether or not evenings begin at login on this server.</param>
        public static void BeginFor(GamePlayer player, bool asked = false)
        {
            try
            {
                if (player?.Client?.IsPlaying != true || player.ObjectState != GameObject.eObjectState.Active)
                    return;
                void Say(string text) => player.Out.SendMessage("[8v8] " + text, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                PvpRealWeights real = PvpWeights.Current.Real;
                // (Not when the player asks: it has just ended its evening itself.)
                if (!asked && IsUnderWay(GameLoop.GameLoopTime, player.TempProperties.GetProperty<long>(BeginsProperty)) && !PvpScrim.Running)
                    return;
                (bool begins, string told) = AtLogin(real.EveningAtLogin || asked,
                    Is8v8Character(player.Level, PvpPlayerReady.BuildsFor(player.Realm).Any(build => build.Class == (eCharacterClass)player.CharacterClass.ID)),
                    PvpScrim.Running, PvpScrim.IsPlaying(player) || PvpRoam.Leads(player));
                if (told != null)
                    Say(told);
                if (!begins)
                    return;
                player.TempProperties.SetProperty(BeginsProperty, GameLoop.GameLoopTime);
                // (Its cloak and shields wear its guild's emblem, whatever it has put on since.)
                PvpGuildEmblem.Wear(player);
                PvpPlayerSetting kept = PvpPlayerSettings.Get(AppContext.BaseDirectory, player.InternalID);
                // Where the player stands when that is a frontier zone; else where its last evening began. The player is not moved.
                PvpRoamMap zone = PvpRoam.FrontierUnder(player) ?? (kept.Zone == null ? null : PvpRoam.MapFor(kept.Zone));
                PvpRoam.StartFor(player, Say, kept.Lineup == null ? null : PvpBuildCatalog.FindGroup(kept.Lineup), zone ?? PvpRoam.Emain,
                    // (As many groups as the settings say: what was kept from the last evening was only ever that number,
                    // and held it fast when the setting was changed.)
                    Math.Clamp(real.EveningGroups, 1, PvpTeamPool.MostGroups), stay: true);
            }
            catch (Exception exception)
            {
                Log.Error("8v8: the evening of " + player?.Name + " could not begin", exception);
            }
        }
    }
}
