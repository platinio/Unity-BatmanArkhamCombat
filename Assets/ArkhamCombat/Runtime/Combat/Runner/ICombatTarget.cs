using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A target in the fight. <see cref="Receive"/> is a direct call rather than an event: the
    /// striker needs the answer back.
    /// </summary>
    public interface ICombatTarget : IActionTarget
    {
        // Declared again so a target in the fight has to answer both; only a bare action target
        // may leave them as they are.
        new string State { get; }

        new HitResult Receive(HitInfo hit);
    }

    /// <summary>
    /// The picker reads it and never finds targets on its own.
    /// </summary>
    public interface ITargetRoster
    {
        IReadOnlyList<ICombatTarget> Targets { get; }
    }
}
