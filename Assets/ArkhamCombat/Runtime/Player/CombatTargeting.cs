using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>It remembers nothing, so every answer is for the moment it is asked.</summary>
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatTargeting : MonoBehaviour, IActionTargetPicker
    {
        // Below this the direction is a stick at rest rather than a choice.
        private const float DirectionPushedSqrMagnitude = 0.01f;

        private ITargetScorer scorer;
        private CharacterBrain characterBrain;

        [Inject]
        private void Construct(ITargetScorer scorer) => this.scorer = scorer;

        private void Awake()
        {
            characterBrain = GetComponent<CharacterBrain>();
        }

        public IActionTarget PickTarget(Vector2 direction) =>
            scorer.BestTarget(transform.position, WorldDirection(direction));

        private Vector3 WorldDirection(Vector2 direction)
        {
            bool isDirectionPushed = direction.sqrMagnitude > DirectionPushedSqrMagnitude;
            if (isDirectionPushed)
            {
                return characterBrain.Context.MovementFrame.Frame * new Vector3(direction.x, 0f, direction.y);
            }

            return transform.forward;
        }
    }
}
