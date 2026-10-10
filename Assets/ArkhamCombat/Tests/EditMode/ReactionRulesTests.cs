using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ReactionRulesTests
    {
        private const float SomeHealthLeft = 40f;
        private const float NoHealthLeft = 0f;

        private HitReceiverProfile unarmored;
        private HitReceiverProfile armored;
        private HitReceiverProfile neverKnockedDown;

        [SetUp]
        public void SetUp()
        {
            unarmored = ReceiverProfile();
            armored = ReceiverProfile(isArmored: true);
            neverKnockedDown = ReceiverProfile(canBeKnockedDown: false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(unarmored);
            Object.DestroyImmediate(armored);
            Object.DestroyImmediate(neverKnockedDown);
        }

        [Test]
        public void AFlinch_OnAnUnarmoredReceiver_Flinches()
        {
            Assert.AreEqual(AppliedReaction.Flinch, ReactionRules.Resolve(HitReaction.Flinch, unarmored, SomeHealthLeft));
        }

        [Test]
        public void AFlinch_OnAnArmoredReceiver_DoesNothing()
        {
            Assert.AreEqual(AppliedReaction.None, ReactionRules.Resolve(HitReaction.Flinch, armored, SomeHealthLeft));
        }

        [Test]
        public void AStagger_OnAnUnarmoredReceiver_Staggers()
        {
            Assert.AreEqual(AppliedReaction.Stagger, ReactionRules.Resolve(HitReaction.Stagger, unarmored, SomeHealthLeft));
        }

        [Test]
        public void AStagger_OnAnArmoredReceiver_OnlyFlinches()
        {
            Assert.AreEqual(AppliedReaction.Flinch, ReactionRules.Resolve(HitReaction.Stagger, armored, SomeHealthLeft));
        }

        [Test]
        public void AKnockdown_KnocksDown()
        {
            Assert.AreEqual(AppliedReaction.Knockdown, ReactionRules.Resolve(HitReaction.Knockdown, unarmored, SomeHealthLeft));
        }

        [Test]
        public void AKnockdown_KnocksDownThroughArmor()
        {
            Assert.AreEqual(AppliedReaction.Knockdown, ReactionRules.Resolve(HitReaction.Knockdown, armored, SomeHealthLeft));
        }

        [Test]
        public void AKnockdown_OnAReceiverThatCannotBeKnockedDown_Staggers()
        {
            Assert.AreEqual(AppliedReaction.Stagger, ReactionRules.Resolve(HitReaction.Knockdown, neverKnockedDown, SomeHealthLeft));
        }

        [TestCase(HitReaction.Flinch)]
        [TestCase(HitReaction.Stagger)]
        [TestCase(HitReaction.Knockdown)]
        public void AnyReaction_WithNoHealthLeft_IsDeath(HitReaction requested)
        {
            Assert.AreEqual(AppliedReaction.Death, ReactionRules.Resolve(requested, unarmored, NoHealthLeft));
        }

        [Test]
        public void ArmorDoesNotSaveAReceiverWithNoHealthLeft()
        {
            Assert.AreEqual(AppliedReaction.Death, ReactionRules.Resolve(HitReaction.Flinch, armored, NoHealthLeft));
        }
    }
}
