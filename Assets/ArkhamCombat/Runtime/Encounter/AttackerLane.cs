using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace ArkhamCombat.Encounter
{
    /// <summary>
    /// The others steer around whoever has the lower avoidance priority, so the ring yields to the
    /// attacker instead of slowing it down.
    /// </summary>
    [RequireComponent(typeof(EncounterAgent), typeof(NavMeshAgent))]
    [AddComponentMenu("ArkhamCombat/Attacker Lane")]
    public sealed class AttackerLane : MonoBehaviour
    {
        private EncounterDirectorSettings settings = new EncounterDirectorSettings();
        private EncounterAgent encounterAgent;
        private NavMeshAgent navMeshAgent;

        [Inject]
        internal void Construct([InjectOptional] EncounterDirectorSettings settings)
        {
            if (settings != null)
            {
                this.settings = settings;
            }
        }

        internal void Awake()
        {
            encounterAgent = GetComponent<EncounterAgent>();
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        internal void OnEnable()
        {
            encounterAgent.TokenGranted += TakeTheLane;
            encounterAgent.TokenReleased += GiveTheLaneBack;

            if (encounterAgent.HasToken)
            {
                TakeTheLane();
            }
            else
            {
                GiveTheLaneBack();
            }
        }

        internal void OnDisable()
        {
            encounterAgent.TokenGranted -= TakeTheLane;
            encounterAgent.TokenReleased -= GiveTheLaneBack;

            GiveTheLaneBack();
        }

        private void TakeTheLane() => navMeshAgent.avoidancePriority = settings.AttackerAvoidancePriority;

        private void GiveTheLaneBack() => navMeshAgent.avoidancePriority = settings.DefaultAvoidancePriority;
    }
}
