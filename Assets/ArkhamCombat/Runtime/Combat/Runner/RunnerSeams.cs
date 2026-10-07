using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Who an action is aimed at. The runner reads a position for the warp and hands the target to
    /// the hit sink at the active window; target selection decides who it is.
    /// </summary>
    public interface IActionTarget
    {
        bool IsValid { get; }

        Vector3 Position { get; }
    }

    /// <summary>
    /// Where the warp's displacement goes. The runner never touches a transform: the player's sink
    /// turns the delta into the frame's motion intent, an enemy's into a NavMeshAgent move.
    /// </summary>
    public interface IDisplacementSink
    {
        void Displace(Vector3 planarDelta);
    }

    /// <summary>
    /// The hit pipeline's edge. Armed at the active window's start with the attack and its target,
    /// disarmed at its end and on any interrupt, so a hitbox is never left armed behind a cancelled
    /// move.
    /// </summary>
    public interface IHitWindowSink
    {
        void Arm(AttackDefinition attack, IActionTarget target);

        void Disarm();
    }

    /// <summary>Does nothing. For tests and bodies that cannot hit.</summary>
    public sealed class NullHitWindowSink : IHitWindowSink
    {
        public void Arm(AttackDefinition attack, IActionTarget target) { }

        public void Disarm() { }
    }

    /// <summary>Discards displacement. For tests of everything but the warp.</summary>
    public sealed class NullDisplacementSink : IDisplacementSink
    {
        public void Displace(Vector3 planarDelta) { }
    }
}
