using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>What an attack asks of its target.</summary>
    public enum HitReaction
    {
        Flinch,
        Stagger,
        Knockdown,
        Stun
    }

    /// <summary>
    /// Windows are normalized so nothing downstream changes if a clip ever replaces the duration.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Attack", fileName = "Attack")]
    public sealed class AttackDefinition : ActionDefinition
    {
        internal const float StandingOnTargetSqrDistance = 1e-6f;

        [Header("Windows (normalized time)")]
        [Tooltip("While open, this attack can hit.")]
        [SerializeField] private Window hitWindow = new Window(0.3f, 0.5f);

        [Tooltip("While open, a queued Strike starts the next attack in the chain.")]
        [SerializeField] private Window comboWindow = new Window(0.5f, 0.9f);

        [Tooltip("While open, a queued Evade may interrupt this attack.")]
        [SerializeField] private Window evadeWindow = new Window(0f, 0.3f);

        [Tooltip("The character is moved toward the target over this span.")]
        [SerializeField] private Window warpWindow = new Window(0f, 0.3f);

        [Tooltip("While open, the attack is announced before it can hit, so its target can answer it. Leave start and end equal for an attack that gives no warning.")]
        [SerializeField] private Window telegraphWindow = new Window(0f, 0f);

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

        [Tooltip("Aimed at whoever is winding up an attack against this character, wherever the stick points: a counter. Off, it is aimed where the stick points.")]
        [SerializeField] private bool isAimedAtTheAttacker;

        [Header("Pair")]
        [Tooltip("What the target plays from the moment this attack starts, in place of the action its reaction would play: the other half of a counter. Leave empty for an ordinary strike, whose target reacts when the hit lands.")]
        [SerializeField] private ActionDefinition pairedReaction;

        public Window HitWindow => hitWindow;
        public Window ComboWindow => comboWindow;
        public Window EvadeWindow => evadeWindow;
        public Window WarpWindow => warpWindow;
        public Window TelegraphWindow => telegraphWindow;
        public float StrikeDistance => strikeDistance;
        public float MaxLunge => maxLunge;
        public float Damage => damage;
        public HitReaction Reaction => reaction;
        public bool IsCounterable => isCounterable;
        public bool IsUnblockable => isUnblockable;
        public bool IsAimedAtTheAttacker => isAimedAtTheAttacker;
        public ActionDefinition PairedReaction => pairedReaction;
        public bool IsPaired => pairedReaction != null;

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

        public void ConfigureTelegraphWindow(Window telegraphWindow) => this.telegraphWindow = telegraphWindow;

        public void ConfigurePairedReaction(ActionDefinition pairedReaction) => this.pairedReaction = pairedReaction;

        public void ConfigureAsAimedAtTheAttacker() => isAimedAtTheAttacker = true;

        internal override void GiveItsWindowsTo(ActionRunner runner) => runner.TakeTheWindowsOf(this);

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
            int errorCountBefore = errors.Count;

            base.Validate(errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(hitWindow), hitWindow, errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(comboWindow), comboWindow, errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(evadeWindow), evadeWindow, errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(warpWindow), warpWindow, errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(telegraphWindow), telegraphWindow, errors);
            ReportWhenLungeCannotReachStrikeDistance(errors);
            ReportWhenThePairedReactionEndsBeforeTheHit(errors);
            ReportWhenThePairedReactionWouldNeverPlay(errors);

            bool isWellFormed = errors.Count == errorCountBefore;
            return isWellFormed;
        }

        private void ReportWhenThePairedReactionWouldNeverPlay(List<string> errors)
        {
            bool asksForAReactionThatAlwaysPlaysItsOwnAction = reaction == HitReaction.Flinch || reaction == HitReaction.Stun;
            if (IsPaired && asksForAReactionThatAlwaysPlaysItsOwnAction)
            {
                errors.Add(
                    $"'{name}': it asks for a {reaction}, which always plays its own action, so its paired reaction " +
                    $"'{pairedReaction.name}' would never play. A paired attack asks for a Stagger or a Knockdown.");
            }
        }

        private void ReportWhenThePairedReactionEndsBeforeTheHit(List<string> errors)
        {
            if (!IsPaired)
            {
                return;
            }

            float secondsUntilTheHit = hitWindow.Start * Duration;
            if (pairedReaction.Duration < secondsUntilTheHit)
            {
                errors.Add(
                    $"'{name}': its paired reaction '{pairedReaction.name}' lasts {pairedReaction.Duration:0.00} s and the hit window opens " +
                    $"{secondsUntilTheHit:0.00} s in, so the target would have stopped reacting before the hit lands.");
            }
        }


        private void ReportWhenLungeCannotReachStrikeDistance(List<string> errors)
        {
            bool canLungeReachStrikeDistance = maxLunge >= strikeDistance;
            if (!canLungeReachStrikeDistance)
            {
                errors.Add($"'{name}': maxLunge ({maxLunge:0.00}) is shorter than strikeDistance ({strikeDistance:0.00}); the warp could never reach its own end point.");
            }
        }
    }
}
