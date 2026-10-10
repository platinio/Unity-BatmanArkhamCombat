using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A range check at the hit window's start, standing in for spec 05's targeted land check:
    /// within strike distance plus a margin the strike lands, anything else is a whiff. Closing has
    /// nothing to do because the check is instantaneous.
    /// </summary>
    public sealed class DemoHitWindowListener : IHitWindowListener
    {
        private readonly IHitRangeSettings settings;
        private readonly Transform character;
        private readonly MotorWarpMover warpMover;
        private readonly ISceneGameEvents sceneGameEvents;

        public DemoHitWindowListener(
            IHitRangeSettings settings,
            CharacterMotor motor,
            MotorWarpMover warpMover,
            ISceneGameEvents sceneGameEvents)
        {
            this.settings = settings;
            this.warpMover = warpMover;
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

        // The hit window opens on the tick the warp closes, and that tick's warp share has not
        // reached the transform yet, so the check measures from where the character lands.
        private bool IsWithinStrikeRange(AttackDefinition attack, Vector3 targetPosition)
        {
            Vector3 whereTheWarpLands = character.position + warpMover.PendingMovement;
            Vector3 toTarget = targetPosition - whereTheWarpLands;
            toTarget.y = 0f;
            return toTarget.magnitude <= attack.StrikeDistance + settings.HitRangeMargin;
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
