using UnityEngine;

namespace ArkhamCombat.Combat
{
    public sealed class StillLocomotionSignal : ILocomotionSignal
    {
        public Vector3 PlanarVelocity => Vector3.zero;
    }
}
