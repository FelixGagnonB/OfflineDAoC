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
    /// the game while nothing runs is taken to its realm's start in the zone it had last, with
    /// the line-up it had last (the realm's main one the first time) and as many groups of each
    /// realm as it had last (PvpRealWeights.EveningGroups the first time). It is a real evening:
    /// what everybody earns is kept. It ends when the player leaves the game, as it did.
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

        private static void OnEntered(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player || !PvpWeights.Current.Real.EveningAtLogin)
                return;
            // A moment for the client to have the world: the evening takes the player to its realm's start.
            new ECSGameTimer(player, _ =>
            {
                BeginFor(player);
                return 0;
            }, 4_000);
        }

        /// <summary>Begins the player's evening when it has none: on coming in, and once <c>/8v8 ready</c> has made its character.</summary>
        public static void BeginFor(GamePlayer player)
        {
            try
            {
                if (player?.Client?.IsPlaying != true || player.ObjectState != GameObject.eObjectState.Active)
                    return;
                void Say(string text) => player.Out.SendMessage("[8v8] " + text, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                PvpRealWeights real = PvpWeights.Current.Real;
                (bool begins, string told) = AtLogin(real.EveningAtLogin,
                    Is8v8Character(player.Level, PvpPlayerReady.BuildsFor(player.Realm).Any(build => build.Class == (eCharacterClass)player.CharacterClass.ID)),
                    PvpScrim.Running, PvpScrim.IsPlaying(player));
                if (told != null)
                    Say(told);
                if (!begins)
                    return;
                PvpPlayerSetting kept = PvpPlayerSettings.Get(AppContext.BaseDirectory, player.InternalID);
                PvpRoamMap zone = kept.Zone == null ? null : PvpRoam.MapFor(kept.Zone);
                PvpRoam.StartFor(player, Say, kept.Lineup == null ? null : PvpBuildCatalog.FindGroup(kept.Lineup), zone ?? PvpRoam.Emain,
                    Math.Clamp(kept.Groups > 0 ? kept.Groups : real.EveningGroups, 1, 16));
            }
            catch (Exception exception)
            {
                Log.Error("8v8: the evening of " + player?.Name + " could not begin", exception);
            }
        }
    }
}
