using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Picks targets for this character. A direction given in the character's movement frame, like
    /// the stick, is turned into world space; with no direction the character's facing is used. The
    /// scene-wide <see cref="ITargetScorer"/> then names the best target that way. It remembers
    /// nothing, so every answer is for the moment it is asked.
    /// </summary>
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
            // The brain on this object, not whichever one the container found in the scene.
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
