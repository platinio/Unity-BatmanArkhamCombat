using UnityEngine;

namespace ArkhamCombat.Encounter
{
    public interface ISlotValidator
    {
        bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition);
    }

    public sealed class EverySlotIsValid : ISlotValidator
    {
        public bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition)
        {
            validPosition = desiredPosition;
            return true;
        }
    }
}
