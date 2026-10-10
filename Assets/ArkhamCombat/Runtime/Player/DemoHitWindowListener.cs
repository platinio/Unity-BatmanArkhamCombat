using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A range check at the hit window's start, standing in for spec 05's targeted land check:
    /// within strike distance plus a margin the strike lands, anything else is a whiff. Either way it
    /// tells the scene through a Hermes event that names the attacker; whoever keeps that character's
    /// combo does the counting. Closing has nothing to do because the check is instantaneous.
    /// </summary>
    public sealed class DemoHitWindowListener : IHitWindowListener
    {
        private readonly CombatConfig config;
        private readonly Transform character;
        private readonly ISceneGameEvents sceneGameEvents;

        public DemoHitWindowListener(CombatConfig config, CharacterMotor motor, ISceneGameEvents sceneGameEvents)
        {
            this.config = config;
            this.sceneGameEvents = sceneGameEvents;
            character = motor.transform;
        }

        public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
        {
            if (target is ICombatTarget victim && victim.IsValid && IsWithinStrikeRange(attack, victim.Position))
            {
                victim.Receive(attack);
                AnnounceStrikeLanded(attack, victim);
            }
            else
            {
                AnnounceStrikeWhiffed(attack);
            }
        }

        public void HitWindowClosed() { }

        private bool IsWithinStrikeRange(AttackDefinition attack, Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - character.position;
            toTarget.y = 0f;
            return toTarget.magnitude <= attack.StrikeDistance + config.HitRangeMargin;
        }

        private void AnnounceStrikeLanded(AttackDefinition attack, ICombatTarget victim)
        {
#if HERMES_EVENTS_GENERATED
            sceneGameEvents?.GameEventDispatcher.StrikeLandedGameEvent.Raise(character.gameObject, attack, GameObjectOf(victim));
#endif
        }

        private void AnnounceStrikeWhiffed(AttackDefinition attack)
        {
#if HERMES_EVENTS_GENERATED
            sceneGameEvents?.GameEventDispatcher.StrikeWhiffedGameEvent.Raise(character.gameObject, attack);
#endif
        }

        // A target that is not a component has no object to name, and the event carries null for it.
        private static GameObject GameObjectOf(ICombatTarget victim) =>
            victim is Component component ? component.gameObject : null;
    }
}
