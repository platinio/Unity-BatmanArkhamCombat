using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>A struct so a pick allocates nothing.</summary>
    public readonly struct VariantPickContext
    {
        public readonly AttackDefinition LastPicked;

        public readonly TargetSide TargetSide;

        public readonly System.Random Random;

        public VariantPickContext(AttackDefinition lastPicked, TargetSide targetSide, System.Random random)
        {
            LastPicked = lastPicked;
            TargetSide = targetSide;
            Random = random;
        }

        public bool IsTargetToOneSide => TargetSide != TargetSide.DeadAhead;
        public bool IsTargetOnTheLeft => TargetSide == TargetSide.Left;
    }

    public interface IVariantPolicy
    {
        AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context);
    }

    [Serializable]
    public sealed class NoRepeatPolicy : IVariantPolicy
    {
        public AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context)
        {
            if (attacks.Count == 1)
            {
                return attacks[0];
            }

            int index = context.Random.Next(attacks.Count);
            if (attacks[index] == context.LastPicked)
            {
                index = AnyOtherIndex(index, attacks.Count, context.Random);
            }

            return attacks[index];
        }

        /// <summary>Steps forward by 1 to count - 1 places, wrapping, so every other index is equally likely.</summary>
        private static int AnyOtherIndex(int index, int count, System.Random random)
        {
            int steps = 1 + random.Next(count - 1);
            return (index + steps) % count;
        }
    }

    [Serializable]
    public sealed class RandomPolicy : IVariantPolicy
    {
        public AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context) =>
            attacks[context.Random.Next(attacks.Count)];
    }

    /// <summary>
    /// The first entry when the target is on the left, the second when on the right, so a pool of
    /// Jab_L and Jab_R leads with the hand nearer the target. With the target dead ahead, or no
    /// target, it falls back to no-repeat, which keeps the pool alternating.
    /// </summary>
    [Serializable]
    public sealed class TargetSidePolicy : IVariantPolicy
    {
        private const int LeftAttackIndex = 0;
        private const int RightAttackIndex = 1;

        private static readonly NoRepeatPolicy Fallback = new NoRepeatPolicy();

        public AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context)
        {
            bool hasLeftAndRightAttack = attacks.Count >= 2;
            if (!context.IsTargetToOneSide || !hasLeftAndRightAttack)
            {
                return Fallback.Pick(attacks, context);
            }

            return context.IsTargetOnTheLeft ? attacks[LeftAttackIndex] : attacks[RightAttackIndex];
        }
    }

    /// <summary>The policy is a dropdown so adding a rule is one class, not a switch.</summary>
    [Serializable]
    public sealed class VariantPool
    {
        [SerializeField] private List<AttackDefinition> attacks = new List<AttackDefinition>();

        [SerializeReference, SubclassSelector] private IVariantPolicy policy = new NoRepeatPolicy();

        public VariantPool() { }

        public VariantPool(IVariantPolicy policy, params AttackDefinition[] attacks)
        {
            this.policy = policy;
            this.attacks = new List<AttackDefinition>(attacks);
        }

        public IReadOnlyList<AttackDefinition> Attacks => attacks;
        public IVariantPolicy Policy => policy;
        public int Count => attacks.Count;
        public bool IsEmpty => attacks.Count == 0;

        public AttackDefinition Pick(in VariantPickContext context)
        {
            if (IsEmpty)
            {
                return null;
            }

            return policy != null ? policy.Pick(attacks, context) : attacks[0];
        }
    }
}
