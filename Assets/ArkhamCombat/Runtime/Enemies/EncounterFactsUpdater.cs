using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Encounter;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    [RequireComponent(typeof(EncounterAgent), typeof(AgentVariableWriter))]
    public sealed class EncounterFactsUpdater : CharacterComponent
    {
        private EnemyArchetype archetype;
        private EncounterAgent encounterAgent;
        private AgentVariableWriter agentVariables;

        private float CombatRange => archetype != null ? archetype.CombatRange : 0f;

        [Inject]
        internal void Construct([InjectOptional] EnemyArchetype archetype) => this.archetype = archetype;

        internal void Awake()
        {
            encounterAgent = GetComponent<EncounterAgent>();
            agentVariables = GetComponent<AgentVariableWriter>();
        }

        public override void Tick(float deltaTime)
        {
            Transform target = encounterAgent.Target;
            bool hasTarget = target != null;
            float targetDistance = hasTarget ? DistanceOnTheGroundTo(target.position) : float.PositiveInfinity;

            Write(EnemyVariableNames.HasTarget, hasTarget);
            Write(EnemyVariableNames.TargetDistance, targetDistance);
            Write(EnemyVariableNames.InCombatRange, hasTarget && targetDistance <= CombatRange);
            Write(EnemyVariableNames.HasToken, encounterAgent.HasToken);
        }

        private float DistanceOnTheGroundTo(Vector3 position)
        {
            Vector3 toPosition = position - transform.position;
            toPosition.y = 0f;
            return toPosition.magnitude;
        }

        private void Write(string key, object value) => agentVariables.Write(this, key, value);
    }
}
