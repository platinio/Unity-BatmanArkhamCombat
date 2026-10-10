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
        private const float SomeDamage = 30f;
        private const float MostOfItsHealth = 60f;
        private const float TheRestOfItsHealth = MaxHealth - MostOfItsHealth;
        private const float MoreThanItsHealth = 250f;

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
        public void AHit_TakesTheAttacksDamage()
        {
            HitReceiver receiver = ReceiverWith();

            HitResult result = receiver.Receive(HitDealing(SomeDamage));

            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(MaxHealth - SomeDamage, receiver.Health);
        }

        [Test]
        public void AHit_PlaysTheReactionTheAttackAskedFor()
        {
            HitReceiver receiver = ReceiverWith();

            HitResult result = receiver.Receive(HitDealing(SomeDamage, HitReaction.Stagger));

            Assert.AreEqual(AppliedReaction.Stagger, result.AppliedReaction);
            Assert.IsFalse(result.WasKilled);
        }

        [Test]
        public void AHit_OnAnArmoredReceiver_StillTakesDamage_ThoughTheReactionIsShruggedOff()
        {
            HitReceiver receiver = ReceiverWith(isArmored: true);

            HitResult result = receiver.Receive(HitDealing(SomeDamage, HitReaction.Flinch));

            Assert.AreEqual(MaxHealth - SomeDamage, receiver.Health);
            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(AppliedReaction.None, result.AppliedReaction);
        }

        [Test]
        public void TheHitThatEmptiesTheHealth_Kills()
        {
            HitReceiver receiver = ReceiverWith();
            receiver.Receive(HitDealing(MostOfItsHealth));

            HitResult result = receiver.Receive(HitDealing(TheRestOfItsHealth));

            Assert.IsTrue(result.WasKilled);
            Assert.AreEqual(AppliedReaction.Death, result.AppliedReaction);
            Assert.IsFalse(receiver.IsAlive);
        }

        [Test]
        public void DamageBeyondTheHealthLeft_LeavesHealthAtZero()
        {
            HitReceiver receiver = ReceiverWith();

            receiver.Receive(HitDealing(MoreThanItsHealth));

            Assert.AreEqual(0f, receiver.Health);
        }

        [Test]
        public void AHit_OnTheDead_IsIgnored()
        {
            HitReceiver receiver = ReceiverWith();
            receiver.Receive(HitDealing(MaxHealth));

            HitResult result = receiver.Receive(HitDealing(SomeDamage));

            Assert.IsFalse(result.HasLanded);
            Assert.IsFalse(result.WasKilled, "it was killed by the hit before, not by this one");
            Assert.AreEqual(AppliedReaction.None, result.AppliedReaction);
            Assert.AreEqual(0f, receiver.Health);
        }
    }
}
