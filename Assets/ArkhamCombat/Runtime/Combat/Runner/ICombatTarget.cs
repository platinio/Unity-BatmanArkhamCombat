using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Everything that strikes talks to this and never to the target's own component.
    /// <see cref="Receive"/> is a direct call rather than an event: the striker needs the answer back.
    /// </summary>
    public interface ICombatTarget : IActionTarget
    {
        /// <summary>Reported as the targetState fact: Idle, Staggered, and so on.</summary>
        string State { get; }

        HitResult Receive(HitInfo hit);
    }

    /// <summary>
    /// The picker reads it and never finds targets on its own.
    /// </summary>
    public interface ITargetRoster
    {
        IReadOnlyList<ICombatTarget> Targets { get; }
    }
}
