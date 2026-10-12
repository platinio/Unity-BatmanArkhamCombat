using ArkhamCombat.Combat;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    public class AppliedReactionWeightTests
    {
        [TestCase(AppliedReaction.Flinch, AppliedReaction.None)]
        [TestCase(AppliedReaction.Stagger, AppliedReaction.Flinch)]
        [TestCase(AppliedReaction.Knockdown, AppliedReaction.Stagger)]
        [TestCase(AppliedReaction.Death, AppliedReaction.Knockdown)]
        public void EachReaction_IsHeavierThanTheOneBelowIt_AndNotTheOtherWayRound(AppliedReaction heavier, AppliedReaction lighter)
        {
            Assert.IsTrue(heavier.IsHeavierThan(lighter));
            Assert.IsFalse(lighter.IsHeavierThan(heavier));
        }

        [Test]
        public void AReaction_IsNotHeavierThanItself()
        {
            Assert.IsFalse(AppliedReaction.Stagger.IsHeavierThan(AppliedReaction.Stagger));
        }

        [Test]
        public void EveryReaction_HasAWeight()
        {
            foreach (AppliedReaction reaction in System.Enum.GetValues(typeof(AppliedReaction)))
            {
                Assert.DoesNotThrow(() => reaction.IsHeavierThan(AppliedReaction.None), $"{reaction} was added without a weight");
            }
        }
    }
}
