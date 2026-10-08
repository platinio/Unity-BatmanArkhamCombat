using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>What a pool policy may look at when it picks. A struct so a pick allocates nothing.</summary>
    public readonly struct VariantPickContext
    {
        /// <summary>The attack this node played last time, or null the first time.</summary>
        public readonly AttackDefinition LastPicked;

        /// <summary>-1 left of the player, 1 right, 0 unknown or dead ahead.</summary>
        public readonly int TargetSide;

        public readonly System.Random Random;

        public VariantPickContext(AttackDefinition lastPicked, int targetSide, System.Random random)
        {
            LastPicked = lastPicked;
            TargetSide = targetSide;
            Random = random;
        }

        public bool IsTargetSideKnown => TargetSide != 0;
        public bool IsTargetOnTheLeft => TargetSide < 0;
    }

    /// <summary>Picks one attack from a pool. Implementations are picked from a dropdown on the node.</summary>
    public interface IVariantPolicy
    {
        AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context);
    }

    /// <summary>Random, but never the same attack twice in a row when there is a choice.</summary>
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

    /// <summary>Uniform random, repeats allowed.</summary>
    [Serializable]
    public sealed class RandomPolicy : IVariantPolicy
    {
        public AttackDefinition Pick(IReadOnlyList<AttackDefinition> attacks, in VariantPickContext context) =>
            attacks[context.Random.Next(attacks.Count)];
    }

    /// <summary>
    /// The first entry when the target is on the left, the second when on the right, so a pool of
    /// Jab_L and Jab_R leads with the hand nearer the target. With no side known it falls back to
    /// no-repeat, which is what keeps the pool alternating until target selection exists.
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
            if (!context.IsTargetSideKnown || !hasLeftAndRightAttack)
            {
                return Fallback.Pick(attacks, context);
            }

            return context.IsTargetOnTheLeft ? attacks[LeftAttackIndex] : attacks[RightAttackIndex];
        }
    }

    /// <summary>
    /// A set of attacks a node may play, with the rule for choosing between them. The policy is a
    /// dropdown so adding a rule is one class, not a switch.
    /// </summary>
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

        /// <summary>Null when the pool is empty. A null policy degrades to the first entry rather than failing.</summary>
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
