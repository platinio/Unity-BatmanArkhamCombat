using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class EvadeDirection : MonoBehaviour, ITravelDirectionPicker
    {
        private const float NegligibleSqrDistance = 1e-4f;

        private ITargetingSettings settings;
        private IMovementFrame frame;
        private EncounterDirector encounterDirector;

        [Inject]
        internal void Construct(ITargetingSettings settings, IMovementFrame frame, EncounterDirector encounterDirector)
        {
            this.settings = settings;
            this.frame = frame;
            this.encounterDirector = encounterDirector;
        }

        public Vector3 PickTravelDirection(Vector2 direction)
        {
            if (StickDirection.IsPushed(direction, settings))
            {
                return StickDirection.InTheWorld(direction, frame);
            }

            bool isSomeoneWindingUp = encounterDirector.TryGetNearestTelegraphingAgent(transform.position, out IEncounterAgent attacker);
            return isSomeoneWindingUp ? AwayFrom(attacker.Position) : StraightBack;
        }

        private Vector3 StraightBack => -transform.forward;

        private Vector3 AwayFrom(Vector3 position)
        {
            Vector3 away = transform.position - position;
            away.y = 0f;

            bool isStandingOnIt = away.sqrMagnitude < NegligibleSqrDistance;
            return isStandingOnIt ? StraightBack : away.normalized;
        }
    }
}
