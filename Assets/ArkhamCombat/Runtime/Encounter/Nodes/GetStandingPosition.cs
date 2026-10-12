using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Get Standing Position")]
    public sealed class GetStandingPosition : EncounterNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override string NodeName => "Get Standing Position";

        public override string Description =>
            "Outputs where this agent should stand while it waits: its slot on the ring around the encounter target,\n" +
            "or where it already is when it is nearer the target than the ring.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Output = ValueOutput<Vector3>(nameof(Output), () => HasEncounterAgent ? EncounterAgent.StandingPosition : transform.position);
        }
    }
}
