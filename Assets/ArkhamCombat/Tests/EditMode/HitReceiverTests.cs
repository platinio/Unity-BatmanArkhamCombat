using System.Collections.Generic;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class HitReceiverTests
    {
        private const float MaxHealth = 100f;

        private readonly List<Object> createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in createdAssets)
            {
                Object.DestroyImmediate(asset);
            }

            createdAssets.Clear();
        }

        private HitReceiver ReceiverWith(bool isArmored = false)
        {
            HitReceiverProfile profile = ReceiverProfile(MaxHealth, isArmored);
            createdAssets.Add(profile);
            return new HitReceiver(profile);
        }

        private HitInfo HitDealing(float damage, HitReaction reaction = HitReaction.Flinch)
        {
            AttackDefinition attack = Attack("Attack", damage: damage, reaction: reaction);
            createdAssets.Add(attack);
            return new HitInfo(attack, Vector3.forward);
        }

        [Test]
        public void AReceiver_StartsAliveAtItsProfilesMaxHealth()
        {
            HitReceiver receiver = ReceiverWith();

            Assert.AreEqual(MaxHealth, receiver.Health);
            Assert.IsTrue(receiver.IsAlive);
        }

        [Test]
        public void AHit_TakesTheAttacksDamage_AndPlaysItsReaction()
        {
            HitReceiver receiver = ReceiverWith();

            HitResult result = receiver.Receive(HitDealing(30f, HitReaction.Stagger));

            Assert.AreEqual(70f, receiver.Health);
            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(AppliedReaction.Stagger, result.Reaction);
            Assert.IsFalse(result.WasKilled);
        }

        [Test]
        public void AHit_OnAnArmoredReceiver_StillTakesDamage_ThoughTheReactionIsShruggedOff()
        {
            HitReceiver receiver = ReceiverWith(isArmored: true);

            HitResult result = receiver.Receive(HitDealing(30f, HitReaction.Flinch));

            Assert.AreEqual(70f, receiver.Health);
            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(AppliedReaction.None, result.Reaction);
        }

        [Test]
        public void TheHitThatEmptiesTheHealth_Kills()
        {
            HitReceiver receiver = ReceiverWith();
            receiver.Receive(HitDealing(60f));

            HitResult result = receiver.Receive(HitDealing(40f));

            Assert.IsTrue(result.WasKilled);
            Assert.AreEqual(AppliedReaction.Death, result.Reaction);
            Assert.IsFalse(receiver.IsAlive);
        }

        [Test]
        public void DamageBeyondTheHealthLeft_LeavesHealthAtZero()
        {
            HitReceiver receiver = ReceiverWith();

            receiver.Receive(HitDealing(250f));

            Assert.AreEqual(0f, receiver.Health);
        }

        [Test]
        public void AHit_OnTheDead_IsIgnored()
        {
            HitReceiver receiver = ReceiverWith();
            receiver.Receive(HitDealing(MaxHealth));

            HitResult result = receiver.Receive(HitDealing(10f));

            Assert.IsFalse(result.HasLanded);
            Assert.IsFalse(result.WasKilled, "it was killed by the hit before, not by this one");
            Assert.AreEqual(AppliedReaction.None, result.Reaction);
            Assert.AreEqual(0f, receiver.Health);
        }
    }
}
