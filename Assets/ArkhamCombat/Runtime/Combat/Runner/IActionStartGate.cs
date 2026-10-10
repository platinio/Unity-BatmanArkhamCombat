namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Says whether a character that is playing nothing may start an action from a press right now.
    /// The character's state decides: airborne or in a hit reaction, the press waits instead.
    /// </summary>
    public interface IActionStartGate
    {
        bool CanStartFromIdle { get; }
    }

    /// <summary>Never holds a press back. For a character with nothing that could, and for tests.</summary>
    public sealed class AlwaysOpenActionStartGate : IActionStartGate
    {
        public bool CanStartFromIdle => true;
    }
}
