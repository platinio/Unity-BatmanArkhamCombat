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

        [Inject]
        private void Construct(ITargetScorer scorer, ITargetingSettings settings, IMovementFrame frame)
        {
            this.scorer = scorer;
            this.settings = settings;
            this.frame = frame;
        }

        public IActionTarget PickTarget(Vector2 direction) =>
            scorer.BestTarget(transform.position, WorldDirection(direction));

        private Vector3 WorldDirection(Vector2 direction)
        {
            bool isStickPushed = direction.magnitude > settings.StickPushedMagnitude;
            if (isStickPushed)
            {
                return frame.Frame * new Vector3(direction.x, 0f, direction.y);
            }

            return transform.forward;
        }
    }
}
