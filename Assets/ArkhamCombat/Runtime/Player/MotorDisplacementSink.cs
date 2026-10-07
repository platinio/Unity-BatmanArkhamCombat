using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Collects the runner's warp displacement for the frame so the attacking state can hand it to
    /// the motor as that frame's planar velocity. The runner never touches the motor; this is the
    /// only bridge.
    /// </summary>
    public sealed class MotorDisplacementSink : IDisplacementSink
    {
        private Vector3 pending;

        public void Displace(Vector3 planarDelta)
        {
            planarDelta.y = 0f;
            pending += planarDelta;
        }

        /// <summary>The displacement accumulated since the last take, then nothing.</summary>
        public Vector3 Take()
        {
            Vector3 taken = pending;
            pending = Vector3.zero;
            return taken;
        }
    }
}
