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
    /// An action that can hit. Adds the four windows the runner fires events on, the travel numbers
    /// the warp uses, and the fields the hit pipeline and the enemies read. Windows are normalized
    /// so nothing downstream changes if a clip ever replaces the duration.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Attack", fileName = "Attack")]
    public sealed class AttackDefinition : ActionDefinition
    {
        [Header("Windows (normalized time)")]
        [Tooltip("Hit frames. The hit pipeline arms at the start and disarms at the end.")]
        [SerializeField] private Window active = new Window(0.3f, 0.5f);

        [Tooltip("While open, a queued Strike may chain into a follow-up.")]
        [SerializeField] private Window cancelAttack = new Window(0.5f, 0.9f);

        [Tooltip("While open, a queued Evade may cancel this attack.")]
        [SerializeField] private Window cancelEvade = new Window(0f, 0.3f);

        [Tooltip("The character is displaced toward the target over this span.")]
        [SerializeField] private Window warp = new Window(0f, 0.3f);

        [Header("Travel")]
        [Tooltip("Where the warp wants to end: this far from the target, on the character's side.")]
        [SerializeField, Min(0f)] private float strikeDistance = 1.2f;

        [Tooltip("Beyond this the warp refuses and the strike plays in place. The resolver should have picked a travelling variant.")]
        [SerializeField, Min(0f)] private float maxLunge = 4f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField] private HitReaction reaction = HitReaction.Flinch;

        [Tooltip("Read by enemies and the counter prompt: the receiver may counter this.")]
        [SerializeField] private bool counterable = true;

        [Tooltip("Read by enemies and the telegraph colour: this cannot be countered, only evaded.")]
        [SerializeField] private bool unblockable;

        public Window Active => active;
        public Window CancelAttack => cancelAttack;
        public Window CancelEvade => cancelEvade;
        public Window Warp => warp;
        public float StrikeDistance => strikeDistance;
        public float MaxLunge => maxLunge;
        public float Damage => damage;
        public HitReaction Reaction => reaction;
        public bool Counterable => counterable;
        public bool Unblockable => unblockable;

        /// <summary>Sets the attack fields from code. For tests and the fixture builder.</summary>
        public void ConfigureAttack(
            Window active,
            Window cancelAttack,
            Window cancelEvade,
            Window warp,
            float strikeDistance,
            float maxLunge,
            float damage = 10f,
            HitReaction reaction = HitReaction.Flinch,
            bool counterable = true,
            bool unblockable = false)
        {
            this.active = active;
            this.cancelAttack = cancelAttack;
            this.cancelEvade = cancelEvade;
            this.warp = warp;
            this.strikeDistance = strikeDistance;
            this.maxLunge = maxLunge;
            this.damage = damage;
            this.reaction = reaction;
            this.counterable = counterable;
            this.unblockable = unblockable;
        }

        /// <summary>
        /// Where the warp wants to end: strikeDistance from the target, on the character's side. A
        /// character standing on the target backs off along -Z so the result is still defined.
        /// </summary>
        public Vector3 WarpDestination(Vector3 position, Vector3 targetPosition)
        {
            Vector3 away = position - targetPosition;
            away.y = 0f;

            if (away.sqrMagnitude < 1e-6f)
            {
                away = Vector3.back;
            }

            return targetPosition + away.normalized * strikeDistance;
        }

        /// <summary>Planar distance the warp would travel from here to its end point.</summary>
        public float LungeDistance(Vector3 position, Vector3 targetPosition)
        {
            Vector3 lunge = WarpDestination(position, targetPosition) - position;
            lunge.y = 0f;
            return lunge.magnitude;
        }

        /// <summary>
        /// The one lunge rule: the warp refuses, and the targetBeyondLunge fact is true, when the
        /// travel to the end point exceeds maxLunge. Both sides read this so they can never disagree.
        /// </summary>
        public bool IsBeyondLunge(Vector3 position, Vector3 targetPosition) =>
            LungeDistance(position, targetPosition) > maxLunge;

        public override bool Validate(List<string> errors)
        {
            bool ok = base.Validate(errors);

            ok &= ValidateWindow(nameof(active), active, errors);
            ok &= ValidateWindow(nameof(cancelAttack), cancelAttack, errors);
            ok &= ValidateWindow(nameof(cancelEvade), cancelEvade, errors);
            ok &= ValidateWindow(nameof(warp), warp, errors);

            if (maxLunge < strikeDistance)
            {
                errors.Add($"'{name}': maxLunge ({maxLunge:0.00}) is shorter than strikeDistance ({strikeDistance:0.00}); the warp could never reach its own end point.");
                ok = false;
            }

            return ok;
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
    }
}
