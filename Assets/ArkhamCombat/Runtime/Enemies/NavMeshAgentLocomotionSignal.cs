using ArkhamCombat.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ArkhamCombat.Enemies
{
    public sealed class NavMeshAgentLocomotionSignal : ILocomotionSignal
    {
        private readonly NavMeshAgent agent;

        public NavMeshAgentLocomotionSignal(NavMeshAgent agent) => this.agent = agent;

        public Vector3 PlanarVelocity => Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
    }
}
