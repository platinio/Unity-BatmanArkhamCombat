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
        private ITargetScorer scorer;
        private ITargetingSettings settings;
        private CharacterBrain characterBrain;

        [Inject]
        private void Construct(ITargetScorer scorer, ITargetingSettings settings)
        {
            this.scorer = scorer;
            this.settings = settings;
        }

        private void Awake()
        {
            characterBrain = GetComponent<CharacterBrain>();
        }

        public IActionTarget PickTarget(Vector2 direction) =>
            scorer.BestTarget(transform.position, WorldDirection(direction));

        private Vector3 WorldDirection(Vector2 direction)
        {
            bool isStickPushed = direction.magnitude > settings.StickPushedMagnitude;
            if (isStickPushed)
            {
                return characterBrain.Context.MovementFrame.Frame * new Vector3(direction.x, 0f, direction.y);
            }

            return transform.forward;
        }
    }
}
