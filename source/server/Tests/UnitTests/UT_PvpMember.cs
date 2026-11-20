using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.Database;
using DOL.GS;
using DOL.GS.PlayerClass;
using NUnit.Framework;

namespace DOL.Tests.Unit.Gameserver;

/// <summary>A member of an 8v8 group is a bot or a player (bots/pvp/PvpMember.cs).</summary>
[TestFixture, NonParallelizable]
public class UT_PvpMember
{
    // GameObject's static start reads the database: an empty one, as UT_AnimistRewardParity uses.
    private static readonly IObjectDatabase EmptyDatabase =
        DispatchProxy.Create<IObjectDatabase, DOL.UnitTests.UT_UnobservedConcentration.EmptyReads>();

    private sealed class InertServer : GameServer
    {
        protected override IObjectDatabase DataBaseImpl => EmptyDatabase;
    }

    private GameServer _previousServer;

    [SetUp]
    public void SetUp()
    {
        _previousServer = GameServer.Instance;
        GameServer.LoadTestDouble((InertServer)RuntimeHelpers.GetUninitializedObject(typeof(InertServer)));
    }

    [TearDown]
    public void TearDown() => GameServer.LoadTestDouble(_previousServer);

    private sealed class DruidPlayer : GamePlayer
    {
        private DruidPlayer() : base(null, null) { }
        public override ICharacterClass CharacterClass => new ClassDruid();
    }

    private sealed class HeroPlayer : GamePlayer
    {
        private HeroPlayer() : base(null, null) { }
        public override ICharacterClass CharacterClass => new ClassHero();
    }

    private static T Player<T>() where T : GamePlayer => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Test]
    public void APlayersClassIsRead()
    {
        // A real player is not an IGamePlayer: before this was read it counted as a fighter of no class.
        GamePlayer druid = Player<DruidPlayer>();
        Assert.That(PvpMember.ClassOf(druid), Is.EqualTo(eCharacterClass.Druid));
        Assert.That(PvpMember.RoleOf(druid), Is.EqualTo(PvpRole.Heal));
        Assert.That(PvpMember.IsFighter(druid), Is.False);
        Assert.That(PvpMember.BuildOf(druid), Is.EqualTo("player-druid"));
    }

    [Test]
    public void APlayerWhoFightsIsAFighter()
    {
        GamePlayer hero = Player<HeroPlayer>();
        Assert.That(PvpMember.ClassOf(hero), Is.EqualTo(eCharacterClass.Hero));
        Assert.That(PvpMember.IsFighter(hero), Is.True);
    }

    [Test]
    public void AnythingElseHasNoClass()
    {
        Assert.That(PvpMember.ClassOf(null), Is.EqualTo(eCharacterClass.Unknown));
    }
}
