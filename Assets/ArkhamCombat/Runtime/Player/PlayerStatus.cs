using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// It never plays anything: the reaction states do.
    /// </summary>
    public sealed class PlayerStatus : MonoBehaviour, ICombatTarget
    {
        private const string Dead = "Dead";
        private const string NoReaction = nameof(AppliedReaction.None);

        private HitReceiverProfile profile;
        private HitReceiver receiver;
        private AppliedReaction pendingReaction = AppliedReaction.None;

        public string State => IsAlive ? NoReaction : Dead;
        public bool IsValid => this != null && isActiveAndEnabled && IsAlive;
        public Vector3 Position => transform.position;
        public GameObject Owner => gameObject;

        public bool IsAlive => receiver != null && receiver.IsAlive;

        [Inject]
        internal void Construct([InjectOptional] HitReceiverProfile profile) => this.profile = profile;

        internal void Awake()
        {
            if (profile == null)
            {
                Debug.LogError($"[{nameof(PlayerStatus)}] No hit receiver profile was injected on '{name}', so it cannot be hit.", this);
                return;
            }

            receiver = new HitReceiver(profile);
        }

        public HitResult Receive(HitInfo hit)
        {
            if (receiver == null)
            {
                return HitResult.Ignored;
            }

            HitResult result = receiver.Receive(hit);
            if (result.HasLanded)
            {
                KeepTheHeavierReaction(result.AppliedReaction);
            }

            return result;
        }

        public bool TryTakePendingReaction(out AppliedReaction reaction)
        {
            reaction = pendingReaction;
            pendingReaction = AppliedReaction.None;
            return reaction != AppliedReaction.None;
        }

        // Two hits can land before the character next ticks, and it can only play one reaction.
        private void KeepTheHeavierReaction(AppliedReaction appliedReaction)
        {
            if (appliedReaction.IsHeavierThan(pendingReaction))
            {
                pendingReaction = appliedReaction;
            }
        }
    }
}
