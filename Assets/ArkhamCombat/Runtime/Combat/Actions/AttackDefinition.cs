using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>What the hit pipeline does to the receiver. Handed through untouched; spec 05 owns the rules.</summary>
    public enum HitReaction
    {
        Flinch,
        Stagger,
        Knockdown
    }

    /// <summary>
    /// Windows are normalized so nothing downstream changes if a clip ever replaces the duration.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Attack", fileName = "Attack")]
    public sealed class AttackDefinition : ActionDefinition
    {
        /// <summary>Closer than this the character stands on the target and has no direction to back off along.</summary>
        private const float StandingOnTargetSqrDistance = 1e-6f;

        [Header("Windows (normalized time)")]
        [Tooltip("While open, this attack can hit.")]
        [SerializeField] private Window hitWindow = new Window(0.3f, 0.5f);

        [Tooltip("While open, a queued Strike starts the next attack in the chain.")]
        [SerializeField] private Window comboWindow = new Window(0.5f, 0.9f);

        [Tooltip("While open, a queued Evade may interrupt this attack.")]
        [SerializeField] private Window evadeWindow = new Window(0f, 0.3f);

        [Tooltip("The character is moved toward the target over this span.")]
        [SerializeField] private Window warpWindow = new Window(0f, 0.3f);

        [Header("Travel")]
        [Tooltip("Where the warp wants to end: this far from the target, on the character's side.")]
        [SerializeField, Min(0f)] private float strikeDistance = 1.2f;

        [Tooltip("Beyond this the warp refuses and the strike plays in place. The resolver should have picked a travelling variant.")]
        [SerializeField, Min(0f)] private float maxLunge = 4f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField] private HitReaction reaction = HitReaction.Flinch;

        [Tooltip("Read by enemies and the counter prompt: the receiver may counter this.")]
        [SerializeField] private bool isCounterable = true;

        [Tooltip("Read by enemies and the telegraph colour: this cannot be countered, only evaded.")]
        [SerializeField] private bool isUnblockable;

        public Window HitWindow => hitWindow;
        public Window ComboWindow => comboWindow;
        public Window EvadeWindow => evadeWindow;
        public Window WarpWindow => warpWindow;
        public float StrikeDistance => strikeDistance;
        public float MaxLunge => maxLunge;
        public float Damage => damage;
        public HitReaction Reaction => reaction;
        public bool IsCounterable => isCounterable;
        public bool IsUnblockable => isUnblockable;

        public void ConfigureAttack(
            Window hitWindow,
            Window comboWindow,
            Window evadeWindow,
            Window warpWindow,
            float strikeDistance,
            float maxLunge,
            float damage = 10f,
            HitReaction reaction = HitReaction.Flinch,
            bool isCounterable = true,
            bool isUnblockable = false)
        {
            this.hitWindow = hitWindow;
            this.comboWindow = comboWindow;
            this.evadeWindow = evadeWindow;
            this.warpWindow = warpWindow;
            this.strikeDistance = strikeDistance;
            this.maxLunge = maxLunge;
            this.damage = damage;
            this.reaction = reaction;
            this.isCounterable = isCounterable;
            this.isUnblockable = isUnblockable;
        }

        /// <summary>
        /// A character standing on the target backs off along -Z so the result is still defined.
        /// </summary>
        public Vector3 WarpDestination(Vector3 position, Vector3 targetPosition)
        {
            Vector3 awayFromTarget = position - targetPosition;
            awayFromTarget.y = 0f;

            if (awayFromTarget.sqrMagnitude < StandingOnTargetSqrDistance)
            {
                awayFromTarget = Vector3.back;
            }

            return targetPosition + awayFromTarget.normalized * strikeDistance;
        }

        public float LungeDistance(Vector3 position, Vector3 targetPosition)
        {
            Vector3 lunge = WarpDestination(position, targetPosition) - position;
            lunge.y = 0f;
            return lunge.magnitude;
        }

        /// <summary>
        /// Both the warp and the targetBeyondLunge fact read this one rule, so they can never
        /// disagree.
        /// </summary>
        public bool IsBeyondLunge(Vector3 position, Vector3 targetPosition) =>
            LungeDistance(position, targetPosition) > maxLunge;

        public override bool Validate(List<string> errors)
        {
            bool isValid = base.Validate(errors);

            isValid &= ValidateWindow(nameof(hitWindow), hitWindow, errors);
            isValid &= ValidateWindow(nameof(comboWindow), comboWindow, errors);
            isValid &= ValidateWindow(nameof(evadeWindow), evadeWindow, errors);
            isValid &= ValidateWindow(nameof(warpWindow), warpWindow, errors);
            isValid &= ValidateLungeReachesStrikeDistance(errors);

            return isValid;
        }

        private bool ValidateWindow(string label, Window window, List<string> errors)
        {
            if (window.IsValid(out string error))
            {
                return true;
            }

            errors.Add($"'{name}': {label} {error}.");
            return false;
        }

        private bool ValidateLungeReachesStrikeDistance(List<string> errors)
        {
            if (maxLunge >= strikeDistance)
            {
                return true;
            }

            errors.Add($"'{name}': maxLunge ({maxLunge:0.00}) is shorter than strikeDistance ({strikeDistance:0.00}); the warp could never reach its own end point.");
            return false;
        }
    }
}
