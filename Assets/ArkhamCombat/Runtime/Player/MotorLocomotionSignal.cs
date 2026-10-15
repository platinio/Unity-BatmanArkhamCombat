using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    public sealed class MotorLocomotionSignal : ILocomotionSignal
    {
        private readonly CharacterMotor motor;

        public MotorLocomotionSignal(CharacterMotor motor) => this.motor = motor;

        public Vector3 PlanarVelocity => motor.PlanarVelocity;
    }
}
