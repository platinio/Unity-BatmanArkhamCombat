using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The resolver gates these two global edges by the situation; every other kind takes a global
    /// edge whenever it matches.
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
