using ArcaneOnyx.TPCharacterController.Movement;
using UnityEngine;

namespace ArkhamCombat.Player
{
    internal static class StickDirection
    {
        public static bool IsPushed(Vector2 stick, ITargetingSettings settings) =>
            stick.magnitude > settings.StickPushedMagnitude;

        public static Vector3 InTheWorld(Vector2 stick, IMovementFrame frame) =>
            frame.Frame * new Vector3(stick.x, 0f, stick.y);
    }
}
