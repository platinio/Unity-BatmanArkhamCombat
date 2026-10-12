using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// Listed after <see cref="CombatActions"/>, so what it says is this frame's. It says where the
    /// runner stands every tick rather than when the window opens and closes, so an attack cut
    /// short can never leave a telegraph behind.
    /// </summary>
    [RequireComponent(typeof(CombatActions), typeof(EncounterAgent), typeof(AgentVariableWriter))]
    public sealed class TelegraphAnnouncer : CharacterComponent
    {
        private CombatActions combatActions;
        private EncounterAgent encounterAgent;
        private AgentVariableWriter agentVariables;

        internal void Awake()
        {
            combatActions = GetComponent<CombatActions>();
            encounterAgent = GetComponent<EncounterAgent>();
            agentVariables = GetComponent<AgentVariableWriter>();

            // The tree may ask before the first tick: the machine enters its root in Start.
            TellTheTree(isTelegraphing: false);
        }

        public override void Tick(float deltaTime)
        {
            ActionRunner runner = combatActions.Runner;
            bool isTelegraphing = runner.IsTelegraphWindowOpen;

            TellTheTree(isTelegraphing);

            if (isTelegraphing)
            {
                encounterAgent.TelegraphAnAttack(runner.CurrentAttack.IsCounterable);
            }
            else
            {
                encounterAgent.StopTelegraphing();
            }
        }

        private void TellTheTree(bool isTelegraphing) =>
            agentVariables.Write(this, EnemyVariableNames.Telegraphing, isTelegraphing);
    }
}
