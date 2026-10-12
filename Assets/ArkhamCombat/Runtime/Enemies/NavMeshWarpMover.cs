using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Characters;
using UnityEngine;
using UnityEngine.AI;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// The warp the runner asked for this frame goes through the NavMeshAgent, so a lunge never
    /// carries the enemy off walkable ground.
    /// </summary>
    [RequireComponent(typeof(CombatActions), typeof(NavMeshAgent))]
    public sealed class NavMeshWarpMover : CharacterComponent
    {
        private CombatActions combatActions;
        private NavMeshAgent navMeshAgent;

        internal void Awake()
        {
            combatActions = GetComponent<CombatActions>();
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        // Taken even when it cannot be walked, or it would pile up and throw off where the next strike is measured from.
        public override void Tick(float deltaTime)
        {
            Vector3 warpMovement = combatActions.TakePendingWarpMovement();

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Move(warpMovement);
            }
        }
    }
}
