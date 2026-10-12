using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;
using UnityEngine.AI;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

namespace ArkhamCombat.Enemies
{
    [GraphCreateMenu("ArkhamCombat/Set Nav Agent Speed")]
    public sealed class SetNavAgentSpeedNode : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Speed { get; private set; }

        private bool hasReportedTheMissingNavMeshAgent;

        public override string NodeName => "Set Nav Agent Speed";

        public override string Description =>
            "Makes the agent walk at Speed metres per second from now on, until something sets another speed.\n" +
            "Returns FAILURE when the agent has no NavMeshAgent, and says so once.";

        protected override void Definition()
        {
            base.Definition();

            Speed = ValueInput<float>(nameof(Speed));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            NavMeshAgent navMeshAgent = ctx.GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                ReportOnceThatThereIsNoNavMeshAgent(ctx);
                return ExecutionStatus.Failure;
            }

            navMeshAgent.speed = ctx.GetValue<float>(Speed);
            return ExecutionStatus.Success;
        }

        private void ReportOnceThatThereIsNoNavMeshAgent(BTContext ctx)
        {
            if (hasReportedTheMissingNavMeshAgent)
            {
                return;
            }

            hasReportedTheMissingNavMeshAgent = true;
            Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' has no {nameof(NavMeshAgent)}, so its speed cannot be set.", ctx.gameObject);
        }
    }
}
