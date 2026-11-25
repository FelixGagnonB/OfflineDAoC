namespace DOL.GS.Effects
{
    public class SpeedOfSoundECSEffect : ECSGameAbilityEffect
    {
        public SpeedOfSoundECSEffect(in ECSGameEffectInitParams initParams) : base(initParams)
        {
            EffectType = eEffect.SpeedOfSound;
        }

        public override ushort Icon => 4249;

        public override string Name => "Speed Of Sound";

        public override bool HasPositiveEffect => true;

        // On a player, and on a bot of its group (a companion, an 8v8 bot): a pet takes its owner's pace.
        private bool Runs => Owner is GamePlayer or GameBot;

        private void SpeedChanged()
        {
            OwnerPlayer?.Out.SendUpdateMaxSpeed();
            (Owner as GameNPC)?.OnMaxSpeedChange();
        }

        public override void OnStartEffect()
        {
            if (!Runs)
                return;

            foreach (ECSGameSpellEffect speedBuff in Owner.effectListComponent.GetSpellEffects(eEffect.MovementSpeedBuff))
            {
                if (speedBuff.GetType() != typeof(SpeedOfSoundECSEffect))
                    speedBuff.Disable();
            }

            foreach (ECSGameSpellEffect root in Owner.effectListComponent.GetSpellEffects(eEffect.MovementSpeedDebuff))
                root.Disable();

            foreach (ECSGameSpellEffect ichor in Owner.effectListComponent.GetSpellEffects(eEffect.Ichor))
                ichor.Disable();

            Owner.BuffBonusMultCategory1.Set((int) eProperty.MaxSpeed, this, PropertyCalc.MaxSpeedCalculator.SPEED4);
            SpeedChanged();
        }

        public override void OnStopEffect()
        {
            if (!Runs)
                return;

            Owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);

            foreach (ECSGameSpellEffect speedBuff in Owner.effectListComponent.GetSpellEffects(eEffect.MovementSpeedBuff))
                speedBuff.Enable();

            foreach (ECSGameSpellEffect root in Owner.effectListComponent.GetSpellEffects(eEffect.MovementSpeedDebuff))
                root.Enable();

            foreach (ECSGameSpellEffect ichor in Owner.effectListComponent.GetSpellEffects(eEffect.Ichor))
                ichor.Enable();

            SpeedChanged();
        }
    }
}
