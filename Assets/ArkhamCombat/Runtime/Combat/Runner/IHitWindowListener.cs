namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The hit code, told when an attack's hit window opens and closes. Closing is also reported when
    /// the attack is interrupted, so a hit window never stays open behind it.
    /// </summary>
    public interface IHitWindowListener
    {
        void HitWindowOpened(AttackDefinition attack, IActionTarget target);

        void HitWindowClosed();
    }

    /// <summary>Does nothing. For tests and bodies that cannot hit.</summary>
    public sealed class NullHitWindowListener : IHitWindowListener
    {
        public void HitWindowOpened(AttackDefinition attack, IActionTarget target) { }

        public void HitWindowClosed() { }
    }
}
