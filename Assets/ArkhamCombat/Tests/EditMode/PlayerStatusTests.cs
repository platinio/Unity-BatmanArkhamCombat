using System.Text.RegularExpressions;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class PlayerStatusTests
    {
        private const float MaxHealth = 100f;
        private const float Damage = 10f;

        private GameObject player;
        private PlayerStatus status;
        private HitReceiverProfile profile;
        private AttackDefinition jab;
        private AttackDefinition cross;
        private AttackDefinition kick;

        [SetUp]
        public void SetUp()
        {
            profile = ReceiverProfile(MaxHealth);
            jab = Attack("Jab", damage: Damage, reaction: HitReaction.Flinch);
            cross = Attack("Cross", damage: Damage, reaction: HitReaction.Stagger);
            kick = Attack("Kick", damage: Damage, reaction: HitReaction.Knockdown);

            player = HiddenObject("Player");
            status = player.AddComponent<PlayerStatus>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(cross);
            Object.DestroyImmediate(kick);
        }

        private void WakeWith(HitReceiverProfile profileToUse)
        {
            status.Construct(profileToUse);
            status.Awake();
        }

        private void WakeWithHealthForOneHit()
        {
            profile.Configure(Damage);
            WakeWith(profile);
        }

        private HitResult Hit(AttackDefinition attack) => status.Receive(new HitInfo(attack, Vector3.forward));

        private AppliedReaction TakePendingReaction()
        {
            status.TryTakePendingReaction(out AppliedReaction reaction);
            return reaction;
        }

        [Test]
        public void BeforeAnyHit_ThereIsNoReactionToTake()
        {
            WakeWith(profile);

            Assert.IsFalse(status.TryTakePendingReaction(out _));
        }

        [Test]
        public void ThePlayer_IsTheObjectAnEventAboutItNames()
        {
            IActionTarget target = status;

            Assert.AreSame(player, target.Owner);
        }

        [Test]
        public void AHitHandedToThePlayerAsAnActionTarget_StillReachesIt()
        {
            WakeWith(profile);
            IActionTarget target = status;

            HitResult result = target.Receive(new HitInfo(jab, Vector3.forward));

            Assert.IsTrue(result.HasLanded);
        }

        [Test]
        public void ALivingPlayer_IsAValidTarget()
        {
            WakeWith(profile);

            Assert.IsTrue(status.IsValid);
        }

        [Test]
        public void AHitThatLands_LeavesItsReactionToBeTaken()
        {
            WakeWith(profile);

            HitResult result = Hit(cross);

            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(AppliedReaction.Stagger, TakePendingReaction());
        }

        [Test]
        public void AReaction_IsTakenOnlyOnce()
        {
            WakeWith(profile);
            Hit(jab);
            TakePendingReaction();

            Assert.IsFalse(status.TryTakePendingReaction(out _));
        }

        [Test]
        public void OfTwoHitsBeforeTheReactionIsTaken_TheHeavierReactionIsKept_WhenItLandsFirst()
        {
            WakeWith(profile);

            Hit(cross);
            Hit(jab);

            Assert.AreEqual(AppliedReaction.Stagger, TakePendingReaction());
        }

        [Test]
        public void OfTwoHitsBeforeTheReactionIsTaken_TheHeavierReactionIsKept_WhenItLandsLast()
        {
            WakeWith(profile);

            Hit(jab);
            Hit(kick);

            Assert.AreEqual(AppliedReaction.Knockdown, TakePendingReaction());
        }

        [Test]
        public void AKnockdown_IsHeavierThanAStagger()
        {
            WakeWith(profile);

            Hit(kick);
            Hit(cross);

            Assert.AreEqual(AppliedReaction.Knockdown, TakePendingReaction());
        }

        [Test]
        public void TheHitThatKills_LeavesDeathToBeTaken()
        {
            WakeWithHealthForOneHit();

            HitResult result = Hit(jab);

            Assert.IsTrue(result.WasKilled);
            Assert.AreEqual(AppliedReaction.Death, TakePendingReaction());
        }

        [Test]
        public void ADeadPlayer_IsNoLongerAValidTarget()
        {
            WakeWithHealthForOneHit();

            Hit(jab);

            Assert.IsFalse(status.IsValid);
            Assert.AreEqual("Dead", status.State);
        }

        [Test]
        public void AKnockdownStillPending_GivesWayToTheHitThatKills()
        {
            profile.Configure(2f * Damage);
            WakeWith(profile);

            Hit(kick);
            Hit(jab);

            Assert.AreEqual(AppliedReaction.Death, TakePendingReaction());
        }

        [Test]
        public void TheDead_IgnoreHits()
        {
            WakeWithHealthForOneHit();
            Hit(jab);
            TakePendingReaction();

            HitResult result = Hit(kick);

            Assert.IsFalse(result.HasLanded);
            Assert.IsFalse(status.TryTakePendingReaction(out _));
        }

        [Test]
        public void WithoutAProfile_ItSaysSo_AndIgnoresHits()
        {
            LogAssert.Expect(LogType.Error, new Regex("No hit receiver profile"));
            WakeWith(null);

            HitResult result = Hit(jab);

            Assert.IsFalse(result.HasLanded);
            Assert.IsFalse(status.IsValid);
        }
    }
}
