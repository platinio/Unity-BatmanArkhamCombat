using System.Text.RegularExpressions;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode runs no Awake, so each test wakes the status by hand once its profile is in place.
    public class EnemyStatusTests
    {
        private const float MaxHealth = 100f;
        private const float Damage = 10f;
        private const float HealthShareAfterOneHit = 0.9f;
        private const float Tolerance = 1e-5f;

        private GameObject enemy;
        private VariableDeclarations facts;
        private EnemyStatus status;
        private HitReceiverProfile profile;
        private AttackDefinition jab;
        private AttackDefinition cross;

        [SetUp]
        public void SetUp()
        {
            profile = ReceiverProfile(MaxHealth);
            jab = Attack("Jab", damage: Damage, reaction: HitReaction.Flinch);
            cross = Attack("Cross", damage: Damage, reaction: HitReaction.Stagger);

            enemy = HiddenObject("Enemy");
            enemy.AddComponent<Variables>();
            facts = Variables.Object(enemy);
            status = enemy.AddComponent<EnemyStatus>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(cross);
        }

        private void WakeWith(HitReceiverProfile profileToUse)
        {
            status.Construct(profileToUse);
            status.Awake();
        }

        private HitResult Hit(AttackDefinition attack) => status.Receive(new HitInfo(attack, Vector3.forward));

        [Test]
        public void StartsAlive_WithNoReaction_AndFullHealth()
        {
            WakeWith(profile);

            Assert.IsTrue((bool)facts.Get(EnemyVariableNames.IsAlive));
            Assert.AreEqual("None", facts.Get(EnemyVariableNames.Reaction));
            Assert.AreEqual(1f, (float)facts.Get(EnemyVariableNames.Health01), Tolerance);
            Assert.IsTrue(status.IsValid);
        }

        [Test]
        public void ALandedFlinch_PublishesTheReaction_AndTheHealthLeft()
        {
            WakeWith(profile);

            Hit(jab);

            Assert.AreEqual("Flinch", facts.Get(EnemyVariableNames.Reaction));
            Assert.AreEqual(HealthShareAfterOneHit, (float)facts.Get(EnemyVariableNames.Health01), Tolerance);
        }

        [Test]
        public void AKillingHit_PublishesDeath_AndLeavesTheReactionAlone()
        {
            profile.Configure(Damage);
            WakeWith(profile);

            Hit(jab);

            Assert.IsFalse((bool)facts.Get(EnemyVariableNames.IsAlive));
            Assert.AreEqual("None", facts.Get(EnemyVariableNames.Reaction), "death is not a reaction the tree clears");
            Assert.AreEqual(0f, (float)facts.Get(EnemyVariableNames.Health01), Tolerance);
            Assert.AreEqual("Dead", status.State);
            Assert.IsFalse(status.IsValid, "a dead enemy is no longer a target");
        }

        [Test]
        public void AnArmoredEnemyShruggingOffAFlinch_PublishesOnlyTheHealthLeft()
        {
            profile.Configure(MaxHealth, isArmored: true);
            WakeWith(profile);

            Hit(jab);

            Assert.AreEqual("None", facts.Get(EnemyVariableNames.Reaction));
            Assert.AreEqual(HealthShareAfterOneHit, (float)facts.Get(EnemyVariableNames.Health01), Tolerance);
        }

        [Test]
        public void TheState_IsTheReactionFactAsTheTreeLeavesIt()
        {
            WakeWith(profile);

            Hit(cross);
            Assert.AreEqual("Stagger", status.State);

            facts.Set(EnemyVariableNames.Reaction, "None");
            Assert.AreEqual("None", status.State);
        }

        [Test]
        public void AHitOnADeadEnemy_IsIgnored_AndPublishesNothing()
        {
            profile.Configure(Damage);
            WakeWith(profile);
            Hit(jab);

            HitResult secondHit = Hit(cross);

            Assert.IsFalse(secondHit.HasLanded);
            Assert.AreEqual("None", facts.Get(EnemyVariableNames.Reaction));
        }

        [Test]
        public void WithoutAProfile_ItLogsAnError_AndCannotBeHit()
        {
            LogAssert.Expect(LogType.Error, new Regex("No hit receiver profile"));

            WakeWith(null);

            Assert.IsFalse(Hit(jab).HasLanded);
            Assert.IsFalse(status.IsValid);
        }
    }
}
