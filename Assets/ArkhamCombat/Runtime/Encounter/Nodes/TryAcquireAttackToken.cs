using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Try Acquire Attack Token")]
    public sealed class TryAcquireAttackToken : EncounterNode
    {
        public override string NodeName => "Try Acquire Attack Token";

        public override string Description =>
            "Asks the encounter director for an attack token and answers at once, without waiting for one.\nReturns SUCCESS while this agent holds a token, FAILURE otherwise.";

        public override ExecutionStatus OnUpdate()
        {
            bool canAttack = HasEncounterAgent && EncounterAgent.TryAcquireToken();
            return canAttack ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}
