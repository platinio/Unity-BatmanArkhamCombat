using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Request Attack Token")]
    public sealed class RequestAttackToken : EncounterNode
    {
        public override string NodeName => "Request Attack Token";

        public override string Description =>
            "Asks the encounter director for permission to attack.\nReturns SUCCESS while this agent holds a token, FAILURE otherwise.";

        public override ExecutionStatus OnUpdate()
        {
            bool canAttack = HasEncounterAgent && EncounterAgent.TryAcquireToken();
            return canAttack ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}
