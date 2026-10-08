using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Which of the game's press kinds are the evade and the counter. The resolver gates those two
    /// global edges by the situation; every other kind takes a global edge whenever it matches.
    /// </summary>
    public sealed class InterruptKinds
    {
        public InterruptKinds(IntentKind evade, IntentKind counter)
        {
            Evade = evade;
            Counter = counter;
        }

        public IntentKind Evade { get; }
        public IntentKind Counter { get; }
    }
}
