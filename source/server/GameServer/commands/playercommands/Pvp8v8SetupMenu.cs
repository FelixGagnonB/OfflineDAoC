using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>What a player has picked in the 8v8 setup window.</summary>
    /// <param name="Own">The player's group by its name when it is not the realm's main one (PvpBuildCatalog.Variants).</param>
    /// <param name="Real">A real fight (PvpRealmPoints): ranks as earned, realm points kept, no timer reset. Off: practice, as it was.</param>
    public sealed record Pvp8v8Setup(eRealm Enemy, string Start, int Rank, bool BotDrives = false, string Own = null, bool Real = false)
    {
        public static readonly int[] Ranks = [1, 3, 5, 7, 10];

        /// <summary>The first pick: an enemy realm that is not the player's, face to face, rank 1.</summary>
        public static Pvp8v8Setup DefaultFor(eRealm own) =>
            new(own == eRealm.Midgard ? eRealm.Hibernia : eRealm.Midgard, "lane", 5);

        /// <summary>
        /// The setup after a click on one of the window's words; unchanged for a word that is not
        /// a choice (or is the player's own realm).
        /// </summary>
        public Pvp8v8Setup After(string word, eRealm own)
        {
            switch ((word ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "albion": return own == eRealm.Albion ? this : this with { Enemy = eRealm.Albion };
                case "midgard": return own == eRealm.Midgard ? this : this with { Enemy = eRealm.Midgard };
                case "hibernia": return own == eRealm.Hibernia ? this : this with { Enemy = eRealm.Hibernia };
                case "lead": return this with { BotDrives = false };
                case "follow": return this with { BotDrives = true };
                case "lane": return this with { Start = "lane" };
                case "basin": return this with { Start = "basin" };
                case "practice": return this with { Real = false };
                case "real": return this with { Real = true };
                case string id when PvpBuildCatalog.FindGroup(id) is { } group && group.Realm == own:
                    return this with { Own = PvpBuildCatalog.Groups.Contains(group) ? null : group.Id };
                case string rank when rank.StartsWith("rank") && int.TryParse(rank[4..], out int asked) && Ranks.Contains(asked):
                    return this with { Rank = asked };
                default: return this;
            }
        }

        /// <param name="realRank">For the Real rules: the rank the character has earned in real fights and the one it stands at, when they differ.</param>
        public string Text(eRealm own, string realRank = null)
        {
            string Pick(string word, bool chosen) => chosen ? $"<{word}>" : $"[{word}]";
            string realms = string.Join("   ", new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }.Where(realm => realm != own)
                .Select(realm => Pick(GlobalConstants.RealmToName(realm), realm == Enemy)));
            // The owner, 2026-11-20, having asked for Hibernia with two Bards and started from this window:
            // "Group setup is hero druid druid bard bm bm eld enchanter". Only a realm with a second group has the line.
            PvpGroupTemplate main = PvpBuildCatalog.Groups.First(group => group.Realm == own);
            string groups = string.Concat(PvpBuildCatalog.Variants.Where(group => group.Realm == own).Prepend(main)
                .Select(group => $"{Pick(group.Id, group.Id == (Own ?? main.Id))}: {string.Join(", ", group.BuildIds)}\n"));
            string ownLine = PvpBuildCatalog.Variants.Any(group => group.Realm == own) ? $"Your group (your class takes its own place):\n{groups}\n" : string.Empty;
            return "8v8: you and seven companions against a group of eight, in Emain Macha.\n\n" +
                   ownLine +
                   $"Enemy:   {realms}\n\n" +
                   $"Start:   {Pick("Lane", Start == "lane")}   {Pick("Basin", Start == "basin")}\n" +
                   $"On the road:   {Pick("Lead", !BotDrives)}   {Pick("Follow", BotDrives)}\n" +
                   "Lead: your group follows you. Follow: your Bard, Skald or Minstrel drives and you stick to it.\n" +
                   "Lane: face to face, 6,000 units apart. Basin: opposite ends, out of sight; the enemy roams and you find it.\n\n" +
                   $"Rules:   {Pick("Practice", !Real)}   {Pick("Real", Real)}\n" +
                   "Practice: everybody at the rank picked below, your timers reset, nothing earned. " +
                   "Real: everybody at the rank it has earned, from rank 1, kills earn realm points that are kept (/8v8 ranks), no timer is reset.\n" +
                   (Real && realRank != null ? realRank + "\n" : string.Empty) + "\n" +
                   $"Realm rank of every bot (practice):   {string.Join("   ", Ranks.Select(rank => Pick("Rank" + rank, rank == Rank)))}\n\n" +
                   "What is in <> is chosen. Click another to change it, then:\n\n[Start]\n\n" +
                   "Picking a rank above your own gives you that rank and its realm ability points (/train to spend them). " +
                   "/8v8 ready makes your character level 50 with a build's spec and gear.";
        }
    }

    /// <summary>
    /// The 8v8 setup window. The 1.65 client only makes bracketed words clickable when an NPC owns
    /// the conversation, so this is an invisible NPC that exists for one player for a few minutes
    /// (the same way as the companion class menu of /spawn).
    /// </summary>
    public sealed class Pvp8v8SetupMenu : GameNPC
    {
        private const string ActiveMenuProperty = "OfflineDAoC.Active8v8SetupMenu";
        private const int MenuLifetimeMilliseconds = 600_000;
        private const ushort InvisibleModel = 150;

        private readonly GamePlayer _owner;
        private Pvp8v8Setup _setup;
        private ECSGameTimer _expirationTimer;
        private bool _closed;

        private Pvp8v8SetupMenu(GamePlayer owner)
        {
            _owner = owner;
            _setup = Pvp8v8Setup.DefaultFor(owner.Realm);
            Name = "8v8 Setup";
            Realm = owner.Realm;
            Level = 1;
            Model = InvisibleModel;
            Size = 1;
            Flags = eFlags.PEACE | eFlags.DONTSHOWNAME;
            X = owner.X;
            Y = owner.Y;
            Z = owner.Z;
            Heading = owner.Heading;
            CurrentRegionID = owner.CurrentRegionID;
        }

        internal static bool Open(GamePlayer player)
        {
            if (player?.CurrentRegion == null)
                return false;
            player.TempProperties.GetProperty<Pvp8v8SetupMenu>(ActiveMenuProperty)?.Close();
            var menu = new Pvp8v8SetupMenu(player);
            if (!menu.AddToWorld())
                return false;
            player.TempProperties.SetProperty(ActiveMenuProperty, menu);
            menu._expirationTimer = new ECSGameTimer(menu, _ =>
            {
                menu.Close();
                return 0;
            }, MenuLifetimeMilliseconds);
            menu.Show();
            return true;
        }

        private void Show()
        {
            _owner.TargetObject = this;
            _owner.Out.SendChangeTarget(this);
            int earned = PvpRealmPoints.LevelFor(PvpPlayerReady.RealPointsOf(_owner));
            _owner.Out.SendMessage(_setup.Text(_owner.Realm, earned == _owner.RealmLevel ? null
                    : $"Start puts your character at rank {PvpRealmPoints.RankName(earned)}, what it has earned in real fights (it stands at " +
                      $"{PvpRealmPoints.RankName(_owner.RealmLevel)} now){(earned < _owner.RealmLevel ? ", and hands its realm abilities back to be bought again" : string.Empty)}."),
                eChatType.CT_Say, eChatLoc.CL_PopupWindow);
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!ReferenceEquals(source, _owner) || string.IsNullOrWhiteSpace(text) || _closed)
                return false;
            if (text.Trim().ToLowerInvariant() == "start")
            {
                PvpGroupTemplate enemy = PvpBuildCatalog.Groups.First(group => group.Realm == _setup.Enemy);
                Pvp8v8Setup setup = _setup;
                GamePlayer owner = _owner;
                Close();
                if (PvpScrim.Running)
                {
                    owner.Out.SendMessage("A fight or a scrim is already running. /8v8 stop ends it.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return true;
                }
                PvpScrim.TravelAndStartPlay(owner, enemy, (setup.Rank - 1) * 10, setup.Start,
                    said => owner.Out.SendMessage(said, eChatType.CT_System, eChatLoc.CL_SystemWindow), setup.BotDrives,
                    PvpBuildCatalog.FindGroup(setup.Own), setup.Real);
                return true;
            }
            int rankBefore = _setup.Rank;
            _setup = _setup.After(text, _owner.Realm);
            // A higher rank picked: the player has its points at once, to train before clicking Start.
            if (!_setup.Real && _setup.Rank != rankBefore && PvpPlayerReady.RaiseRealmRank(_owner, (_setup.Rank - 1) * 10) is { } raised)
                _owner.Out.SendMessage(raised, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            // The client closes an NPC's choices after a click: show the window again with the new pick.
            if (_owner.ObjectState is eObjectState.Active && _owner.CurrentRegion == CurrentRegion)
                Show();
            return true;
        }

        private void Close()
        {
            if (_closed)
                return;
            _closed = true;
            _expirationTimer?.Stop();
            _expirationTimer = null;
            if (ReferenceEquals(_owner?.TempProperties.GetProperty<Pvp8v8SetupMenu>(ActiveMenuProperty), this))
                _owner.TempProperties.RemoveProperty(ActiveMenuProperty);
            if (_owner?.ObjectState is eObjectState.Active && ReferenceEquals(_owner.TargetObject, this))
            {
                _owner.TargetObject = null;
                _owner.Out.SendChangeTarget(null);
            }
            Delete();
        }
    }
}
