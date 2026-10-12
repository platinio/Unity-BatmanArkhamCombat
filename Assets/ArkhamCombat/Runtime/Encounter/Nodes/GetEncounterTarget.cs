using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Get Encounter Target")]
    public sealed class GetEncounterTarget : EncounterNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override string NodeName => "Get Encounter Target";

        public override string Description => "Outputs the transform this encounter is centred on.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Output = ValueOutput<Transform>(nameof(Output), () => IsInAnEncounter ? EncounterAgent.Target : null);
        }
    }
}
