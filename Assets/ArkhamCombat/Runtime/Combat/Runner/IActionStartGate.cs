namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The character's state decides: airborne or in a hit reaction, the press waits instead.
    /// </summary>
    public interface IActionStartGate
    {
        bool CanStartFromIdle { get; }
    }

    public sealed class AlwaysOpenActionStartGate : IActionStartGate
    {
        public bool CanStartFromIdle => true;
    }
}
