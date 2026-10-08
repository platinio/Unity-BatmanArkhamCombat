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
    /// Presentation events raised by the core: what the HUD, audio and camera listen to. Gameplay
    /// never listens here; it is called directly. The one real implementation is the Hermes adapter
    /// in the shell; tests use a fake, and the installer can pick a logging stub from a dropdown.
    /// </summary>
    public interface ICombatEvents
    {
        void ComboChanged(int count, int tier);

        void ComboReset(ComboResetReason reason);

        void ActionStarted(ActionDefinition action, bool isInterrupt);

        void ActionEnded(ActionDefinition action, bool wasInterrupted);
    }

    /// <summary>Swallows everything. For agents with no presentation and for tests that do not care.</summary>
    [Serializable]
    public sealed class NullCombatEvents : ICombatEvents
    {
        public void ComboChanged(int count, int tier) { }

        public void ComboReset(ComboResetReason reason) { }

        public void ActionStarted(ActionDefinition action, bool isInterrupt) { }

        public void ActionEnded(ActionDefinition action, bool wasInterrupted) { }
    }

    /// <summary>Writes every event to the console. The stand-in until the Hermes adapter exists.</summary>
    [Serializable]
    public sealed class LoggingCombatEvents : ICombatEvents
    {
        public void ComboChanged(int count, int tier) => Debug.Log($"[Combat] combo {count} (tier {tier})");

        public void ComboReset(ComboResetReason reason) => Debug.Log($"[Combat] combo reset: {reason}");

        public void ActionStarted(ActionDefinition action, bool isInterrupt) =>
            Debug.Log($"[Combat] {(isInterrupt ? "interrupt" : "action")} {action.name}");

        public void ActionEnded(ActionDefinition action, bool wasInterrupted) =>
            Debug.Log($"[Combat] {action.name} {(wasInterrupted ? "interrupted" : "ended")}");
    }
}
