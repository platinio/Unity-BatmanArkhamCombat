using System;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>Why the combo meter went up.</summary>
    public enum ComboIncrementReason
    {
        StrikeLanded,
        CounterSucceeded,
        EvadeSucceeded
    }

    /// <summary>Why the combo meter went back to zero.</summary>
    public enum ComboResetReason
    {
        PlayerHit,
        Whiff,
        Timeout
    }

    /// <summary>
    /// What the runner announces scene-wide: an action started or ended. Tests use a fake, and the
    /// installer can pick a logging stub from a dropdown. Strikes and combo changes are not here;
    /// they are Hermes events that name the character they belong to.
    /// </summary>
    public interface ICombatEvents
    {
        void ActionStarted(ActionDefinition action, bool isInterrupt);

        void ActionEnded(ActionDefinition action, bool wasInterrupted);
    }

    /// <summary>Swallows everything. For agents with no presentation and for tests that do not care.</summary>
    [Serializable]
    public sealed class NullCombatEvents : ICombatEvents
    {
        public void ActionStarted(ActionDefinition action, bool isInterrupt) { }

        public void ActionEnded(ActionDefinition action, bool wasInterrupted) { }
    }

    /// <summary>Writes every event to the console. The stand-in until the Hermes adapter exists.</summary>
    [Serializable]
    public sealed class LoggingCombatEvents : ICombatEvents
    {
        public void ActionStarted(ActionDefinition action, bool isInterrupt) =>
            Debug.Log($"[Combat] {(isInterrupt ? "interrupt" : "action")} {action.name}");

        public void ActionEnded(ActionDefinition action, bool wasInterrupted) =>
            Debug.Log($"[Combat] {action.name} {(wasInterrupted ? "interrupted" : "ended")}");
    }
}
