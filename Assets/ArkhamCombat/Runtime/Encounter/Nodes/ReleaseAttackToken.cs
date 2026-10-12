using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Release Attack Token")]
    public sealed class ReleaseAttackToken : EncounterNode
    {
        public override string NodeName => "Release Attack Token";

        public override string Description => "Returns this agent's attack token to the encounter director.";

        public override void OnEnter()
        {
            if (HasEncounterAgent)
            {
                EncounterAgent.ReleaseToken();
            }
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
    }
}
