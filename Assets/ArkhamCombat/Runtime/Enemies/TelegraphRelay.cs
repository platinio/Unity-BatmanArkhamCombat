using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// It says where the runner stands every tick rather than when the window opens and closes,
    /// so an attack cut short can never leave a telegraph behind.
    /// </summary>
    [RequireComponent(typeof(CombatActions), typeof(EncounterAgent), typeof(AgentVariableWriter))]
    public sealed class TelegraphRelay : CharacterComponent
    {
        private CombatActions combatActions;
        private EncounterAgent encounterAgent;
        private AgentVariableWriter agentVariables;

        internal void Awake()
        {
            combatActions = GetComponent<CombatActions>();
            encounterAgent = GetComponent<EncounterAgent>();
            agentVariables = GetComponent<AgentVariableWriter>();

            TellTheTree(isTelegraphing: false);
        }

        public override void Tick(float deltaTime)
        {
            ActionRunner runner = combatActions.Runner;
            bool isTelegraphing = runner.IsTelegraphWindowOpen;

            TellTheTree(isTelegraphing);
            TellTheEncounter(isTelegraphing, runner.CurrentAttack);
        }

        private void TellTheTree(bool isTelegraphing) =>
            agentVariables.Write(this, EnemyVariableNames.Telegraphing, isTelegraphing);

        private void TellTheEncounter(bool isTelegraphing, AttackDefinition telegraphedAttack)
        {
            if (isTelegraphing)
            {
                encounterAgent.TelegraphAnAttack(telegraphedAttack.IsCounterable);
            }
            else
            {
                encounterAgent.StopTelegraphing();
            }
        }
    }
}
