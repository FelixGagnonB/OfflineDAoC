using System.Collections.Generic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Effects
{
    public class AmelioratingMelodiesECSEffect : ECSGameAbilityEffect
    {
        private const int RANGE = 1500;
        private int _heal;

        public AmelioratingMelodiesECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.AmelioratingMelodies;
            PulseFreq = 1500; // 1.5s. Effect lasts 30s so that is 20 ticks.
            NextTick = GameLoop.GameLoopTime;
            _heal = (int) Effectiveness; // Effectiveness value is used as a heal value per tick.
        }

        public override ushort Icon => 4250;
        public override string Name => "Ameliorating Melodies";
        public override bool HasPositiveEffect => true;

        public override void OnEffectPulse()
        {
            // On a player or a bot (an 8v8 bot buys it); the bots of the group are healed with its players.
            // OF AM works on the caster as well, unlike NF AM.
            if (Owner is not (GamePlayer or GameBot) || !Owner.IsAlive)
                return;

            foreach (GameLiving player in RealmAbilities.AtlasRAHelpers.GroupInReach(Owner, RANGE))
            {
                if (player.Health < player.MaxHealth)
                {
                    int heal = _heal;

                    if (player.Health + heal > player.MaxHealth)
                        heal = player.MaxHealth - player.Health;

                    heal = player.ChangeHealth(Owner, eHealthChangeType.Regenerate, heal);
                    OwnerPlayer?.Out.SendMessage($"Your Ameliorating Melodies heal {player.Name} for {heal} hit points.", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    (player as GamePlayer)?.Out.SendMessage($"{Owner.Name} 's Ameliorating Melodies heals you for {heal} hit points.", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }
}
