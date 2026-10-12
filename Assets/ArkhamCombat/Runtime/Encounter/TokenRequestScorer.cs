using UnityEngine;

namespace ArkhamCombat.Encounter
{
    /// <summary>
    /// The front term keeps attacks where the target is looking, the fairness term stops the same
    /// few agents owning the fight, and the distance term prefers an attacker who is already close.
    /// </summary>
    public sealed class TokenRequestScorer
    {
        private const float HalfTurnDegrees = 180f;
        private const float NegligibleSqrMagnitude = 0.001f;

        private readonly EncounterDirectorSettings settings;

        public TokenRequestScorer(EncounterDirectorSettings settings) => this.settings = settings;

        public float Score(Vector3 agentPosition, float secondsWithoutAToken, Vector3 targetPosition, Vector3 targetForward)
        {
            Vector3 fromTargetToAgent = agentPosition - targetPosition;
            fromTargetToAgent.y = 0f;

            return settings.FrontWeight * ShareInFrontOfTheTarget(fromTargetToAgent, targetForward)
                   + settings.FairnessWeight * ShareOfTheFairnessHorizonWaited(secondsWithoutAToken)
                   - settings.DistancePenalty * ShareOfTheRingRadiusAway(fromTargetToAgent);
        }

        private static float ShareInFrontOfTheTarget(Vector3 fromTargetToAgent, Vector3 targetForward)
        {
            targetForward.y = 0f;

            bool hasNoDirectionToCompare =
                targetForward.sqrMagnitude < NegligibleSqrMagnitude || fromTargetToAgent.sqrMagnitude < NegligibleSqrMagnitude;
            if (hasNoDirectionToCompare)
            {
                return 0f;
            }

            return 1f - Vector3.Angle(targetForward, fromTargetToAgent) / HalfTurnDegrees;
        }

        private float ShareOfTheFairnessHorizonWaited(float secondsWithoutAToken)
        {
            bool isEveryWaitTheSame = settings.FairnessHorizonSeconds <= 0f;
            return isEveryWaitTheSame ? 0f : Mathf.Clamp01(secondsWithoutAToken / settings.FairnessHorizonSeconds);
        }

        private float ShareOfTheRingRadiusAway(Vector3 fromTargetToAgent)
        {
            bool isThereNoRing = settings.SlotRadius <= 0f;
            return isThereNoRing ? 0f : Mathf.Clamp01(fromTargetToAgent.magnitude / settings.SlotRadius);
        }
    }
}
