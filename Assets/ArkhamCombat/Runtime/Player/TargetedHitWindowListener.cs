using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Lands the attack on the one target its action picked. The check is made once, as the hit
    /// window opens, so closing has nothing to do.
    /// </summary>
    public sealed class TargetedHitWindowListener : IHitWindowListener
    {
        private readonly HitResolver hitResolver;
        private readonly Transform character;
        private readonly MotorWarpMover warpMover;
        private readonly ISceneGameEvents sceneGameEvents;

        public TargetedHitWindowListener(
            IHitCheckSettings settings,
            CharacterMotor motor,
            MotorWarpMover warpMover,
            ISceneGameEvents sceneGameEvents)
        {
            hitResolver = new HitResolver(settings);
            this.warpMover = warpMover;
            this.sceneGameEvents = sceneGameEvents;
            character = motor.transform;
        }

        public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
        {
            if (target is ICombatTarget victim && TryLandOn(victim, attack))
            {
                AnnounceStrikeLanded(attack, victim);
            }
            else
            {
                AnnounceStrikeWhiffed(attack);
            }
        }

        public void HitWindowClosed() { }

        private bool TryLandOn(ICombatTarget victim, AttackDefinition attack)
        {
            if (!victim.IsValid)
            {
                return false;
            }

            if (!hitResolver.TryLand(attack, WhereTheStrikeComesFrom(), victim.Position, out HitInfo hit))
            {
                return false;
            }

            return victim.Receive(hit).HasLanded;
        }

        // The hit window opens on the tick the warp closes, and that tick's warp share has not
        // reached the transform yet, so the strike is measured from where the character lands.
        private StrikeOrigin WhereTheStrikeComesFrom() =>
            new StrikeOrigin(character.position + warpMover.PendingMovement, character.forward);

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
