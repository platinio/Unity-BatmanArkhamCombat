using UnityEngine;

namespace ArkhamCombat.Combat
{
    public interface ILocomotionSignal
    {
        Vector3 PlanarVelocity { get; }
    }
}
