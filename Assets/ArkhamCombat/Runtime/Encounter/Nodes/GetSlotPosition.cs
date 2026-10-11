using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Get Slot Position")]
    public sealed class GetSlotPosition : EncounterNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override string NodeName => "Get Slot Position";

        public override string Description => "Outputs this agent's assigned standing position around the encounter target.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Output = ValueOutput<Vector3>(nameof(Output), () => HasEncounterAgent ? EncounterAgent.SlotPosition : transform.position);
        }
    }
}
