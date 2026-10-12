using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A target answers for itself what a strike does to it, so nothing that strikes or reads a
    /// target has to test what kind of target it holds.
    /// </summary>
    public interface IActionTarget
    {
        bool IsValid { get; }

        Vector3 Position { get; }

        /// <summary>Reported as the targetState fact: Idle, Staggered, and so on.</summary>
        string State => string.Empty;

        /// <summary>The object an event about this target names. A target that lives on no object has none.</summary>
        GameObject Owner => null;

        /// <summary>A target nothing can hurt ignores every hit.</summary>
        HitResult Receive(HitInfo hit) => HitResult.Ignored;
    }
}
