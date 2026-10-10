using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A plain mutable object: the factsUpdater writes it and mirrors it onto the agent's variables
    /// under the keys in <see cref="Keys"/>, so a Function and a test see the same numbers.
    /// </summary>
    public sealed class CombatFacts
    {
        /// <summary>The variable names designers read in their Functions. Renaming one breaks every graph that uses it.</summary>
        public static class Keys
        {
            public const string ComboCount = "comboCount";
            public const string ComboTier = "comboTier";
            public const string TargetDistance = "targetDistance";
            public const string TargetSide = "targetSide";
            public const string TargetState = "targetState";
            public const string TargetBeyondLunge = "targetBeyondLunge";
            public const string IncomingAttackCounterable = "incomingAttackCounterable";
            public const string StickAngleToTarget = "stickAngleToTarget";

            public static readonly string[] All =
            {
                ComboCount, ComboTier, TargetDistance, TargetSide, TargetState,
                TargetBeyondLunge, IncomingAttackCounterable, StickAngleToTarget
            };
        }

        public int ComboCount;
        public int ComboTier;
        public bool HasTarget;
        public float TargetDistance;

        public TargetSide TargetSide;

        public string TargetState = string.Empty;
        public bool IsTargetBeyondLunge;
        public bool IsIncomingAttackCounterable;

        /// <summary>Degrees between the stick at press time and the direction to the target. Set per intent being resolved.</summary>
        public float StickAngleToTarget;
    }

    /// <summary>
    /// Called for every edge, condition or not, so an implementation decides what an empty
    /// condition means (the real one: always met).
    /// </summary>
    public interface IConditionEvaluator
    {
        bool IsConditionMet(Edge edge, CombatFacts facts, Intent intent);
    }

    public sealed class AlwaysConditionEvaluator : IConditionEvaluator
    {
        public bool IsConditionMet(Edge edge, CombatFacts facts, Intent intent) => true;
    }
}
