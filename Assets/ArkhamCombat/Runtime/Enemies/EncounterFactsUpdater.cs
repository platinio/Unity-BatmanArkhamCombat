using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
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

        [Inject]
        internal void Construct([InjectOptional] EnemyArchetype archetype) => this.archetype = archetype;

        internal void Awake()
        {
            encounterAgent = GetComponent<EncounterAgent>();
            agentVariables = GetComponent<AgentVariableWriter>();

            // The tree may ask before the first tick: the machine enters its root in Start.
            PublishFacts();
        }

        public override void Tick(float deltaTime) => PublishFacts();

        private void PublishFacts()
        {
            Transform target = encounterAgent.Target;
            bool hasTarget = target != null && !IsOutOfTheFight(target);
            float targetDistance = hasTarget ? DistanceOnTheGroundTo(target.position) : float.PositiveInfinity;

            Write(EnemyVariableNames.HasTarget, hasTarget);
            Write(EnemyVariableNames.TargetDistance, targetDistance);
            Write(EnemyVariableNames.InCombatRange, hasTarget && IsWithinCombatRange(targetDistance));
            Write(EnemyVariableNames.HasToken, encounterAgent.HasToken);
        }

        private static bool IsOutOfTheFight(Transform target) =>
            target.TryGetComponent(out ICombatTarget combatTarget) && !combatTarget.IsValid;

        private bool IsWithinCombatRange(float distance) => archetype != null && distance <= archetype.CombatRange;

        private float DistanceOnTheGroundTo(Vector3 position)
        {
            Vector3 toPosition = position - transform.position;
            toPosition.y = 0f;
            return toPosition.magnitude;
        }

        private void Write(string key, object value) => agentVariables.Write(this, key, value);
    }
}
