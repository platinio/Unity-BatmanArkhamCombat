using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A range check at the active window's start, standing in for spec 05's targeted land check:
    /// within strike distance plus a margin lands and feeds the meter; anything else is a whiff and
    /// resets it. Disarm has nothing to do because the check is instantaneous.
    /// </summary>
    public sealed class DemoHitWindowSink : IHitWindowSink
    {
        private readonly ComboMeter meter;
        private readonly CombatConfig config;
        private readonly Transform character;

        public DemoHitWindowSink(ComboMeter meter, CombatConfig config, CharacterMotor motor)
        {
            this.meter = meter;
            this.config = config;
            character = motor.transform;
        }

        public void Arm(AttackDefinition attack, IActionTarget target)
        {
            if (target is ICombatTarget victim && victim.IsValid)
            {
                Vector3 toTarget = victim.Position - character.position;
                toTarget.y = 0f;

                if (toTarget.magnitude <= attack.StrikeDistance + config.HitRangeMargin)
                {
                    victim.Receive(attack);
                    meter.Increment(ComboIncrementReason.StrikeLanded);
                    return;
                }
            }

            meter.Reset(ComboResetReason.Whiff);
        }

        public void Disarm() { }
    }
}
