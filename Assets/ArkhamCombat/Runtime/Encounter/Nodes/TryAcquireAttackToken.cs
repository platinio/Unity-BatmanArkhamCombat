using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Try Acquire Attack Token")]
    public sealed class TryAcquireAttackToken : EncounterNode
    {
        public override string NodeName => "Try Acquire Attack Token";

        public override string Description =>
            "Asks the encounter director for an attack token and answers at once, without waiting for one.\n" +
            "Returns SUCCESS only while this agent holds a token. Otherwise the ask is remembered until the director's tick ends " +
            "and the node returns FAILURE, so run it again: the first ask always fails, and a granted token has to be asked for again within the grace time.";

        public override ExecutionStatus OnUpdate()
        {
            bool canAttack = HasEncounterAgent && EncounterAgent.TryAcquireToken();
            return canAttack ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}
