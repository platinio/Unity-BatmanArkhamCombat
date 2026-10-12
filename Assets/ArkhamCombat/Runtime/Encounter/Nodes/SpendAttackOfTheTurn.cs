using ArcaneOnyx.GraphCore;

namespace ArkhamCombat.Encounter
{
    [GraphCreateMenu("Encounter/Spend Attack Of The Turn")]
    public sealed class SpendAttackOfTheTurn : EncounterNode
    {
        public override string NodeName => "Spend Attack Of The Turn";

        public override string Description =>
            "Tells the encounter director this agent has made one of the attacks its turn is worth.\n" +
            "After the last one the director takes the token back, so the turn is over. Always returns SUCCESS.";

        public override void OnEnter()
        {
            if (HasEncounterAgent)
            {
                EncounterAgent.SpendAnAttackOfItsTurn();
            }
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
    }
}
