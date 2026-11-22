using System.Collections;
using DOL.Database;
using DOL.GS.Spells;

namespace DOL.GS.RealmAbilities;

public class AtlasOF_BrilliantAura : TimedRealmAbility, ISpellCastingAbilityHandler
{
    public AtlasOF_BrilliantAura(DbAbility dba, int level) : base(dba, level) { }

    private SpellHandler m_handler;
    
    public Spell Spell { get { return m_spell; } }
    public SpellLine SpellLine { get { return m_spellline; } }
    public Ability Ability { get { return this; } }

    public override int MaxLevel { get { return 1; } }
    public override int GetReUseDelay(int level) { return 1800; } // 30 min
    public override int CostForUpgrade(int level) { return 14; }

    int m_range = 1500;

    public override void Execute(GameLiving living)
    {
        if (CheckPreconditions(living, DEAD | SITTING)) return;

        // A player or a bot (an 8v8 bot buys it); the bots of the group are covered with its players.
        if (living is not (GamePlayer or GameBot))
            return;
        GameLiving player = living;
        if (m_handler == null) m_handler = CreateSpell(player);

        System.Collections.Generic.List<GameLiving> targets = AtlasRAHelpers.GroupInReach(player, m_range);

        SendCastMessage(player);

        bool AtLeastOneEffectRemoved = false;
        foreach (GameLiving target in targets)
        {
            ECSGameEffectFactory.Create(new(target, 30000, 1, m_handler), static (in i) => new StatBuffECSEffect(i));
            foreach (GamePlayer pl in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(target,target,4317,20000,false,1);
                pl.Out.SendSpellEffectAnimation(target,target,5208,20000,false,1);
            }
        }

        DisableSkill(living);
    }

    public virtual SpellHandler CreateSpell(GameLiving caster)
    {
        m_dbspell = new DbSpell();
        m_dbspell.Name = "Brilliant Aura of Deflection";
        m_dbspell.Icon = 7149;
        m_dbspell.ClientEffect = 7009;
        m_dbspell.Damage = 0;
        m_dbspell.DamageType = 0;
        m_dbspell.Target = eSpellTarget.REALM.ToString();
        m_dbspell.Radius = 0;
        m_dbspell.Type = eSpellType.AllSecondaryMagicResistsBuff.ToString();
        m_dbspell.Value = 36;
        m_dbspell.Duration = 30;
        m_dbspell.Pulse = 0;
        m_dbspell.PulsePower = 0;
        m_dbspell.Power = 0;
        m_dbspell.CastTime = 0;
        m_dbspell.EffectGroup = 0;
        m_dbspell.Frequency = 0;
        m_dbspell.Range = 1500;
        m_spell = new Spell(m_dbspell, 0); // make spell level 0 so it bypasses the spec level adjustment code
        m_spellline = GlobalSpellsLines.RealmSpellsSpellLine;
        return ScriptMgr.CreateSpellHandler(caster, m_spell, m_spellline) as SpellHandler;
    }

    private DbSpell m_dbspell;
    private Spell m_spell = null;
    private SpellLine m_spellline;
}
