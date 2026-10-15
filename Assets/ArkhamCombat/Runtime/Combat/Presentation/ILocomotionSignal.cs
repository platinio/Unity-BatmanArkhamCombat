using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// How fast and which way the character is moving over the ground, from whatever moves it: the
    /// player's motor, an enemy's NavMeshAgent. A driver that shows locomotion reads it every frame.
    /// </summary>
    public interface ILocomotionSignal
    {
        Vector3 PlanarVelocity { get; }
    }
}
