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

        /// <summary>
        /// Told as a paired attack aimed at it starts, so it can react from that moment rather than
        /// when the hit lands.
        /// </summary>
        void BeginPairedReaction(AttackDefinition attack) { }

        /// <summary>Told as an attack aimed at it starts, when that attack can reach it.</summary>
        void StrikeIsComing() { }

        /// <summary>Told once that attack has struck, hit or miss, or was cut short before it could.</summary>
        void StrikeIsOver() { }
    }
}
