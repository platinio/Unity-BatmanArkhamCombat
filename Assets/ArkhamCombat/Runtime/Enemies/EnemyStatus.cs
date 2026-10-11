using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// Publishes what each hit did as agent variables and never plays anything: the tree's reaction
    /// branches do that.
    /// </summary>
    public sealed class EnemyStatus : MonoBehaviour, ICombatTarget
    {
        private const string Source = nameof(EnemyStatus);
        private const string Dead = "Dead";

        private HitReceiverProfile profile;
        private HitReceiver receiver;
        private Variables variables;

        public string State => IsAlive ? ReactionBeingPlayed : Dead;
        public bool IsValid => this != null && isActiveAndEnabled && IsAlive;
        public Vector3 Position => transform.position;

        public bool IsAlive => receiver != null && receiver.IsAlive;

        private float ShareOfHealthLeft => receiver != null ? receiver.Health / profile.MaxHealth : 0f;

        private string ReactionBeingPlayed
        {
            get
            {
                bool hasReaction = variables != null && variables.declarations.IsDefined(EnemyVariableNames.Reaction);
                return hasReaction ? variables.declarations.Get<string>(EnemyVariableNames.Reaction) : AppliedReaction.None.ToString();
            }
        }

        [Inject]
        internal void Construct(HitReceiverProfile profile) => this.profile = profile;

        internal void Awake()
        {
            TryGetComponent(out variables);
            CreateReceiver();
            PublishStartingFacts();
        }

        private void CreateReceiver()
        {
            if (profile == null)
            {
                Debug.LogError($"[{nameof(EnemyStatus)}] No hit receiver profile was injected on '{name}', so it cannot be hit.", this);
                return;
            }

            receiver = new HitReceiver(profile);
        }

        private void PublishStartingFacts()
        {
            Write(EnemyVariableNames.IsAlive, IsAlive);
            Write(EnemyVariableNames.Reaction, AppliedReaction.None.ToString());
            Write(EnemyVariableNames.Health01, ShareOfHealthLeft);
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
                PublishHit(result.AppliedReaction);
            }

            return result;
        }

        private void PublishHit(AppliedReaction appliedReaction)
        {
            Write(EnemyVariableNames.Health01, ShareOfHealthLeft);

            switch (appliedReaction)
            {
                case AppliedReaction.None:
                    return;
                case AppliedReaction.Death:
                    Write(EnemyVariableNames.IsAlive, false);
                    return;
                default:
                    Write(EnemyVariableNames.Reaction, appliedReaction.ToString());
                    return;
            }
        }

        private void Write(string key, object value) => AgentVariableWriter.SetOn(gameObject, key, value, Source);
    }
}
