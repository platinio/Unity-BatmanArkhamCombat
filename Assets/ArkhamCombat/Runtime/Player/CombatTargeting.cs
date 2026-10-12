using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>It remembers nothing, so every answer is for the moment it is asked.</summary>
    public sealed class CombatTargeting : MonoBehaviour, IActionTargetPicker
    {
        private ITargetScorer scorer;
        private ITargetingSettings settings;
        private IMovementFrame frame;
        private IIncomingAttacks incomingAttacks;

        [Inject]
        private void Construct(ITargetScorer scorer, ITargetingSettings settings, IMovementFrame frame, IIncomingAttacks incomingAttacks)
        {
            this.scorer = scorer;
            this.settings = settings;
            this.frame = frame;
            this.incomingAttacks = incomingAttacks;
        }

        public IActionTarget PickAttacker() =>
            incomingAttacks.TryGetNearestAttackerThatCanBeCountered(transform.position, out ICombatTarget attacker) ? attacker : null;

        public IActionTarget PickTarget(Vector2 direction) =>
            scorer.BestTarget(transform.position, WorldDirection(direction));

        private Vector3 WorldDirection(Vector2 direction)
        {
            return StickDirection.IsPushed(direction, settings) ? StickDirection.InTheWorld(direction, frame) : transform.forward;
        }
    }
}
