namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Closing is also reported when the attack is interrupted, so a hit window never stays open
    /// behind it.
    /// </summary>
    public interface IHitWindowListener
    {
        void HitWindowOpened(AttackDefinition attack, IActionTarget target);

        void HitWindowClosed();
    }

    public sealed class NullHitWindowListener : IHitWindowListener
    {
        public void HitWindowOpened(AttackDefinition attack, IActionTarget target) { }

        public void HitWindowClosed() { }
    }
}
