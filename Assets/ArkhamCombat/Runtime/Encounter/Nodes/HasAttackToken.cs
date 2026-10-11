using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Has Attack Token")]
    public sealed class HasAttackToken : EncounterNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override string NodeName => "Has Attack Token";

        public override string Description => "Outputs true while this agent holds an attack token.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Output = ValueOutput<bool>(nameof(Output), () => IsInAnEncounter && EncounterAgent.HasToken);
        }
    }
}
