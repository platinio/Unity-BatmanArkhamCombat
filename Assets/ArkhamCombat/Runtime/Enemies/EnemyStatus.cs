using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// It never plays anything: the tree's reaction branches do that, and clear the reaction when
    /// they are done.
    /// </summary>
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class EnemyStatus : MonoBehaviour, ICombatTarget
    {
        private const string Source = nameof(EnemyStatus);
        private const string Dead = "Dead";
        private const string NoReaction = nameof(AppliedReaction.None);
        private const string Stunned = nameof(AppliedReaction.Stun);
        private const ActionDefinition NoOverride = null;

        private HitReceiverProfile profile;
        private HitReceiver receiver;
        private ActionRunner runner;
        private AgentVariableWriter agentVariables;

        public string State => IsAlive ? ReactionAsTheTreeLeftIt : Dead;
        public bool IsValid => this != null && isActiveAndEnabled && IsAlive;
        public Vector3 Position => transform.position;
        public GameObject Owner => gameObject;

        public bool IsAlive => receiver != null && receiver.IsAlive;

        private float ShareOfHealthLeft => receiver != null ? receiver.Health / profile.MaxHealth : 0f;

        private string ReactionAsTheTreeLeftIt => agentVariables.Read(EnemyVariableNames.Reaction) as string ?? NoReaction;

        [Inject]
        internal void Construct([InjectOptional] HitReceiverProfile profile) => this.profile = profile;

        internal void Awake()
        {
            agentVariables = GetComponent<AgentVariableWriter>();
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

            runner = CombatActions.RunnerOn(gameObject);
            receiver = new HitReceiver(profile, runner);
        }

        private void PublishStartingFacts()
        {
            Write(EnemyVariableNames.IsAlive, IsAlive);
            Write(EnemyVariableNames.Reaction, NoReaction);
            Write(EnemyVariableNames.ReactionOverride, NoOverride);
            Write(EnemyVariableNames.Health01, ShareOfHealthLeft);
        }

        /// <summary>
        /// Whatever it was playing is cut short at once, its own swing included, so a countered
        /// attacker cannot land the hit it had started.
        /// </summary>
        public void BeginPairedReaction(AttackDefinition attack)
        {
            if (!IsAlive)
            {
                return;
            }

            runner?.Cancel();
            Write(EnemyVariableNames.ReactionOverride, attack.PairedReaction);
            Write(EnemyVariableNames.Reaction, receiver.ReactionToAPairedAttack(attack).ToString());
        }

        public HitResult Receive(HitInfo hit)
        {
            if (receiver == null)
            {
                return HitResult.Ignored;
            }

            HitResult result = IsStunned ? receiver.ReceiveWhileStunned(hit) : receiver.Receive(hit);
            if (result.HasLanded)
            {
                PublishHit(result.AppliedReaction);
            }

            return result;
        }

        // The tree clears the reaction when the stun has played out, which is what ends it.
        private bool IsStunned => ReactionAsTheTreeLeftIt == Stunned;

        private void PublishHit(AppliedReaction appliedReaction)
        {
            Write(EnemyVariableNames.Health01, ShareOfHealthLeft);

            // The branch a paired attack started is already playing its action; whatever this hit starts plays its own.
            Write(EnemyVariableNames.ReactionOverride, NoOverride);

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
