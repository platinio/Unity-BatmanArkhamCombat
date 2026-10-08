using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A range check at the hit window's start, standing in for spec 05's targeted land check:
    /// within strike distance plus a margin lands and feeds the meter; anything else is a whiff and
    /// resets it. Closing has nothing to do because the check is instantaneous.
    /// </summary>
    public sealed class DemoHitWindowListener : IHitWindowListener
    {
        private readonly ComboMeter meter;
        private readonly CombatConfig config;
        private readonly Transform character;

        public DemoHitWindowListener(ComboMeter meter, CombatConfig config, CharacterMotor motor)
        {
            this.meter = meter;
            this.config = config;
            character = motor.transform;
        }

        public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
        {
            if (target is ICombatTarget victim && victim.IsValid && IsWithinStrikeRange(attack, victim.Position))
            {
                victim.Receive(attack);
                meter.Increment(ComboIncrementReason.StrikeLanded);
            }
            else
            {
                meter.Reset(ComboResetReason.Whiff);
            }
        }

        public void HitWindowClosed() { }

        private bool IsWithinStrikeRange(AttackDefinition attack, Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - character.position;
            toTarget.y = 0f;
            return toTarget.magnitude <= attack.StrikeDistance + config.HitRangeMargin;
        }
    }
}
