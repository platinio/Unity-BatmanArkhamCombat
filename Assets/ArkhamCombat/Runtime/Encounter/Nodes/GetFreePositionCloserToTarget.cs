using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Get Free Position Closer To Target")]
    public sealed class GetFreePositionCloserToTarget : EncounterNode
    {

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput DistanceFromTarget { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput RoomEachAgentKeeps { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override string NodeName => "Get Free Position Closer To Target";

        public override string Description =>
            "Outputs the spot DistanceFromTarget metres from the encounter target, straight in from where this agent is.\n" +
            "When another agent stands within RoomEachAgentKeeps metres of that spot, the spot is not walkable ground,\n" +
            "or this agent is already that close, it outputs where the agent stands while it waits instead.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            DistanceFromTarget = ValueInput<float>(nameof(DistanceFromTarget));
            RoomEachAgentKeeps = ValueInput<float>(nameof(RoomEachAgentKeeps));

            Output = ValueOutput<Vector3>(nameof(Output), () => FreePositionCloserToTarget());
        }

        private Vector3 FreePositionCloserToTarget()
        {
            if (!HasEncounterAgent)
            {
                return transform.position;
            }

            return EncounterAgent.FreePositionCloserToTarget(DistanceFromTarget.GetValue<float>(), RoomEachAgentKeeps.GetValue<float>());
        }
    }
}
