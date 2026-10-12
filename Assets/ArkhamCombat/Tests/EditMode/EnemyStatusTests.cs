using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
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
            status = enemy.AddComponent<EnemyStatus>();
            facts = Variables.Object(enemy);
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

        private void WakeWithHealthForOneHit()
        {
            profile.Configure(Damage);
            WakeWith(profile);
        }

        private HitResult Hit(AttackDefinition attack) => status.Receive(new HitInfo(attack, Vector3.forward));

        private int ReactionFactVersion() =>
            enemy.GetComponent<AgentVariableWriter>().VersionOf(EnemyVariableNames.Reaction);

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
            WakeWithHealthForOneHit();

            Hit(jab);

            Assert.IsFalse((bool)facts.Get(EnemyVariableNames.IsAlive));
            Assert.AreEqual("None", facts.Get(EnemyVariableNames.Reaction), "death is not a reaction the tree clears");
            Assert.AreEqual(0f, (float)facts.Get(EnemyVariableNames.Health01), Tolerance);
        }

        [Test]
        public void TheEnemy_IsTheObjectAnEventAboutItNames()
        {
            IActionTarget target = status;

            Assert.AreSame(enemy, target.Owner);
        }

        [Test]
        public void ADeadEnemyReadAsAnActionTarget_StillReportsItsOwnState()
        {
            WakeWithHealthForOneHit();
            Hit(jab);
            IActionTarget target = status;

            Assert.AreEqual("Dead", target.State);
        }

        [Test]
        public void ADeadEnemy_IsNoLongerATarget()
        {
            WakeWithHealthForOneHit();

            Hit(jab);

            Assert.AreEqual("Dead", status.State);
            Assert.IsFalse(status.IsValid);
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
            WakeWithHealthForOneHit();
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

        // A guard wakes when its fact's version moves, and only an agent with a machine keeps versions.
        [Test]
        public void OnAnAgent_ASecondHitOfTheSameKind_DoesNotMoveTheReactionFact()
        {
            enemy.AddComponent<BehaviorTreeMachine>();
            WakeWith(profile);
            Hit(jab);
            int versionAfterTheFirstFlinch = ReactionFactVersion();

            Hit(jab);

            Assert.AreEqual(versionAfterTheFirstFlinch, ReactionFactVersion());
        }

        [Test]
        public void OnAnAgent_AHitOfAnotherKind_MovesTheReactionFact()
        {
            enemy.AddComponent<BehaviorTreeMachine>();
            WakeWith(profile);
            Hit(jab);
            int versionAfterTheFlinch = ReactionFactVersion();

            Hit(cross);

            Assert.Greater(ReactionFactVersion(), versionAfterTheFlinch);
        }
    }
}
