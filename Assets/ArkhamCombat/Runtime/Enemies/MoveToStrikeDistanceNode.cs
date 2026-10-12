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
        private const float DefaultTimeoutSeconds = 3f;

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
            public bool HasReportedWhyItCannotMove;
        }

        protected override void Definition()
        {
            base.Definition();

            Target = ValueInput<Transform>(nameof(Target));
            StrikeDistance = ValueInput<float>(nameof(StrikeDistance));
            Timeout = ValueInput<float>(nameof(Timeout), DefaultTimeoutSeconds);
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
                return FailBecause("was handed no target to move to", ctx, memory);
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
                return FailBecause($"has no {nameof(NavMeshAgent)} standing on a NavMesh, so it cannot walk to its target", ctx, memory);
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

        // A branch that fails is entered again on the next tick, so the reason is logged once per agent.
        private ExecutionStatus FailBecause(string reason, BTContext ctx, Memory memory)
        {
            if (!memory.HasReportedWhyItCannotMove)
            {
                memory.HasReportedWhyItCannotMove = true;
                Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' {reason}.", ctx.gameObject);
            }

            return ExecutionStatus.Failure;
        }
    }
}
