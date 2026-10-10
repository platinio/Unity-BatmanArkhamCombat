using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Something that can be picked, read and hit: a dummy today, an enemy's status component once
    /// spec 02 lands. Everything player-side talks to this, so the arrival of real enemies changes
    /// no caller. <see cref="Receive"/> is the direct gameplay call spec 05 keeps off the event bus.
    /// </summary>
    public interface ICombatTarget : IActionTarget
    {
        /// <summary>Reported as the targetState fact: Idle, Staggered, and so on.</summary>
        string State { get; }

        /// <summary>The hit pipeline decides the reaction; the target applies it.</summary>
        void Receive(AttackDefinition attack);
    }

    /// <summary>
    /// The encounter director owns this once it exists; until then a scene scan stands in. The
    /// picker reads it and never finds targets on its own.
    /// </summary>
    public interface ITargetRoster
    {
        IReadOnlyList<ICombatTarget> Targets { get; }
    }
}
