using UnityEngine;
using UnityEngine.AI;

namespace ArkhamCombat.Encounter
{
    public sealed class NavMeshSlotValidator : ISlotValidator
    {
        private readonly EncounterDirectorSettings settings;

        public NavMeshSlotValidator(EncounterDirectorSettings settings)
        {
            this.settings = settings;
        }

        public bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition)
        {
            bool isOnTheNavMesh = NavMesh.SamplePosition(desiredPosition, out NavMeshHit nearestPoint, settings.SlotSampleRadius, NavMesh.AllAreas);
            validPosition = isOnTheNavMesh ? nearestPoint.position : desiredPosition;
            return isOnTheNavMesh;
        }
    }
}
