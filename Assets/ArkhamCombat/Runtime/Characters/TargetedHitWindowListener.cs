using ArcaneOnyx.GameEventGenerator;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Characters
{
    public sealed class TargetedHitWindowListener : IHitWindowListener
    {
        private readonly HitResolver hitResolver;
        private readonly Transform character;
        private readonly PendingWarpMovement pendingWarpMovement;
        private readonly ISceneGameEvents sceneGameEvents;

        public TargetedHitWindowListener(
            IHitCheckSettings settings,
            Transform character,
            PendingWarpMovement pendingWarpMovement,
            ISceneGameEvents sceneGameEvents)
        {
            hitResolver = new HitResolver(settings);
            this.character = character;
            this.pendingWarpMovement = pendingWarpMovement;
            this.sceneGameEvents = sceneGameEvents;
        }

        public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
        {
            HitResult result = StrikeAt(target, attack);
            Announce(result, attack, target);
        }

        public void HitWindowClosed() { }

        private HitResult StrikeAt(IActionTarget victim, AttackDefinition attack)
        {
            bool hasAVictim = victim != null && victim.IsValid;
            if (!hasAVictim)
            {
                return HitResult.Missed;
            }

            if (!hitResolver.TryLand(attack, WhereTheStrikeComesFrom(), victim.Position, out HitInfo hit))
            {
                return HitResult.Missed;
            }

            return victim.Receive(hit);
        }

        // The hit window opens on the tick the warp closes, and that tick's warp share has not
        // reached the transform yet, so the strike is measured from where the character lands.
        private StrikeOrigin WhereTheStrikeComesFrom() =>
            new StrikeOrigin(character.position + pendingWarpMovement.Movement, character.forward);

        private void Announce(HitResult result, AttackDefinition attack, IActionTarget victim)
        {
#if HERMES_EVENTS_GENERATED
            if (!CanAnnounceStrikes)
            {
                return;
            }

            GameObject attacker = character.gameObject;
            if (result.HasLanded)
            {
                sceneGameEvents.GameEventDispatcher.StrikeLandedGameEvent.Raise(attacker, attack, victim.Owner);
            }
            else if (result.WasEvaded)
            {
                sceneGameEvents.GameEventDispatcher.StrikeEvadedGameEvent.Raise(attacker, attack, victim.Owner);
            }
            else
            {
                sceneGameEvents.GameEventDispatcher.StrikeWhiffedGameEvent.Raise(attacker, attack);
            }
#endif
        }

#if HERMES_EVENTS_GENERATED
        // Compared with != and not skipped with ?.: a dispatcher that was destroyed or never added is not a C# null.
        private bool CanAnnounceStrikes => sceneGameEvents != null && sceneGameEvents.GameEventDispatcher != null;
#endif
    }
}
