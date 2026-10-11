using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;
using UnityEngine.AI;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

namespace ArkhamCombat.Enemies
{
    [GraphCreateMenu("ArkhamCombat/Move To Strike Distance")]
    public sealed class MoveToStrikeDistanceNode : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Target { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput StrikeDistance { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Timeout { get; private set; }

        public override string NodeName => "Move To Strike Distance";

        public override string Description =>
            "Walks the agent toward Target until it is within StrikeDistance.\nReturns FAILURE when Timeout seconds pass first.";

        private sealed class Memory
        {
            public NavMeshAgent NavMeshAgent;
            public Transform Target;
            public float StrikeDistance;
            public float SecondsLeft;
            public string LastReasonReported;
        }

        protected override void Definition()
        {
            base.Definition();

            Target = ValueInput<Transform>(nameof(Target));
            StrikeDistance = ValueInput<float>(nameof(StrikeDistance));
            Timeout = ValueInput<float>(nameof(Timeout));
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.NavMeshAgent = ctx.GetComponent<NavMeshAgent>();
            memory.Target = ctx.GetValue<Transform>(Target);
            memory.StrikeDistance = ctx.GetValue<float>(StrikeDistance);
            memory.SecondsLeft = ctx.GetValue<float>(Timeout);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();

            if (memory.Target == null)
            {
                ReportOnceWhyItCannotMove("has no target to move to", ctx, memory);
                return ExecutionStatus.Failure;
            }

            if (IsWithinStrikeDistance(ctx, memory))
            {
                return ExecutionStatus.Success;
            }

            memory.SecondsLeft -= Time.deltaTime;
            bool hasRunOutOfTime = memory.SecondsLeft <= 0f;
            if (hasRunOutOfTime)
            {
                return ExecutionStatus.Failure;
            }

            if (!CanWalk(memory))
            {
                ReportOnceWhyItCannotMove(WhyItCannotWalk(memory), ctx, memory);
                return ExecutionStatus.Failure;
            }

            WalkTowardTheTarget(memory);
            return ExecutionStatus.Running;
        }

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (CanWalk(memory))
            {
                memory.NavMeshAgent.isStopped = true;
                memory.NavMeshAgent.velocity = Vector3.zero;
            }
        }

        private static bool IsWithinStrikeDistance(BTContext ctx, Memory memory)
        {
            Vector3 toTarget = memory.Target.position - ctx.transform.position;
            toTarget.y = 0f;
            return toTarget.magnitude <= memory.StrikeDistance;
        }

        private static bool CanWalk(Memory memory) => memory.NavMeshAgent != null && memory.NavMeshAgent.isOnNavMesh;

        private static void WalkTowardTheTarget(Memory memory)
        {
            memory.NavMeshAgent.isStopped = false;
            memory.NavMeshAgent.SetDestination(memory.Target.position);
        }

        private static string WhyItCannotWalk(Memory memory) =>
            memory.NavMeshAgent == null
                ? $"has no {nameof(NavMeshAgent)}, so it cannot walk to its target"
                : "is not standing on a NavMesh, so it cannot walk to its target";

        private void ReportOnceWhyItCannotMove(string reason, BTContext ctx, Memory memory)
        {
            if (memory.LastReasonReported == reason)
            {
                return;
            }

            memory.LastReasonReported = reason;
            Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' {reason}.", ctx.gameObject);
        }
    }
}
