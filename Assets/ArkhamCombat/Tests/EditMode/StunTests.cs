using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class StunTests
    {
        private const float FullHealth = 100f;
        private const float SomeHealthLeft = 40f;
        private const float NoHealthLeft = 0f;
        private const float JabDamage = 10f;

        private static readonly Vector3 AnyDirection = Vector3.forward;

        private HitReceiverProfile unarmored;
        private HitReceiverProfile armored;
        private HitReceiverProfile neverKnockedDown;
        private AttackDefinition capeStun;
        private AttackDefinition jab;
        private GameObject enemy;
        private EnemyStatus status;
        private VariableDeclarations facts;

        [SetUp]
        public void SetUp()
        {
            unarmored = ReceiverProfile(FullHealth);
            armored = ReceiverProfile(FullHealth, isArmored: true);
            neverKnockedDown = ReceiverProfile(FullHealth, canBeKnockedDown: false);
            capeStun = Attack("CapeStun", damage: 0f, reaction: HitReaction.Stun);
            jab = Attack("Jab", damage: JabDamage, reaction: HitReaction.Flinch);

            enemy = HiddenObject("Enemy");
            status = enemy.AddComponent<EnemyStatus>();
            facts = Variables.Object(enemy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(unarmored);
            Object.DestroyImmediate(armored);
            Object.DestroyImmediate(neverKnockedDown);
            Object.DestroyImmediate(capeStun);
            Object.DestroyImmediate(jab);
        }

        private void WakeTheEnemyWith(HitReceiverProfile profile)
        {
            status.Construct(profile);
            status.Awake();
        }

        private HitResult Hit(AttackDefinition attack) => status.Receive(new HitInfo(attack, AnyDirection));

        private string ReactionTheEnemyPublished => (string)facts.Get(EnemyVariableNames.Reaction);

        [Test]
        public void AStun_Stuns()
        {
            Assert.AreEqual(AppliedReaction.Stun, ReactionRules.Resolve(HitReaction.Stun, unarmored, SomeHealthLeft));
        }

        [Test]
        public void AStun_GoesThroughArmor()
        {
            Assert.AreEqual(AppliedReaction.Stun, ReactionRules.Resolve(HitReaction.Stun, armored, SomeHealthLeft));
        }

        [TestCase(HitReaction.Flinch)]
        [TestCase(HitReaction.Stagger)]
        public void AStunnedReceiver_IsStaggeredByEvenALightHit(HitReaction requested)
        {
            Assert.AreEqual(AppliedReaction.Stagger, ReactionRules.ResolveForAStunnedReceiver(requested, unarmored, SomeHealthLeft));
        }

        [TestCase(HitReaction.Flinch)]
        [TestCase(HitReaction.Stagger)]
        public void ArmorDoesNotHelpAStunnedReceiver(HitReaction requested)
        {
            Assert.AreEqual(AppliedReaction.Stagger, ReactionRules.ResolveForAStunnedReceiver(requested, armored, SomeHealthLeft));
        }

        [Test]
        public void AStunnedReceiver_IsKnockedDownByAKnockdown()
        {
            Assert.AreEqual(AppliedReaction.Knockdown, ReactionRules.ResolveForAStunnedReceiver(HitReaction.Knockdown, unarmored, SomeHealthLeft));
        }

        [Test]
        public void AStunnedReceiverThatCannotBeKnockedDown_StillOnlyStaggers()
        {
            Assert.AreEqual(AppliedReaction.Stagger, ReactionRules.ResolveForAStunnedReceiver(HitReaction.Knockdown, neverKnockedDown, SomeHealthLeft));
        }

        [Test]
        public void AStunnedReceiverStunnedAgain_StaysStunned()
        {
            Assert.AreEqual(AppliedReaction.Stun, ReactionRules.ResolveForAStunnedReceiver(HitReaction.Stun, unarmored, SomeHealthLeft));
        }

        [TestCase(HitReaction.Flinch)]
        [TestCase(HitReaction.Stun)]
        public void AStunnedReceiverWithNoHealthLeft_Dies(HitReaction requested)
        {
            Assert.AreEqual(AppliedReaction.Death, ReactionRules.ResolveForAStunnedReceiver(requested, armored, NoHealthLeft));
        }

        [Test]
        public void AStunWithNoHealthLeft_IsDeath()
        {
            Assert.AreEqual(AppliedReaction.Death, ReactionRules.Resolve(HitReaction.Stun, unarmored, NoHealthLeft));
        }

        [Test]
        public void AStun_IsHeavierThanAFlinch_AndLighterThanAStagger()
        {
            Assert.IsTrue(AppliedReaction.Stun.IsHeavierThan(AppliedReaction.Flinch));
            Assert.IsTrue(AppliedReaction.Stagger.IsHeavierThan(AppliedReaction.Stun));
        }

        [Test]
        public void AStunnedEnemy_PublishesThatItIsStunned_AndLosesNoHealthToAStunThatDealsNone()
        {
            WakeTheEnemyWith(unarmored);

            HitResult result = Hit(capeStun);

            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(nameof(AppliedReaction.Stun), ReactionTheEnemyPublished);
            Assert.AreEqual(1f, (float)facts.Get(EnemyVariableNames.Health01), 1e-5f);
        }

        [Test]
        public void AJabOnAStunnedEnemy_StaggersIt()
        {
            WakeTheEnemyWith(unarmored);
            Hit(capeStun);

            Hit(jab);

            Assert.AreEqual(nameof(AppliedReaction.Stagger), ReactionTheEnemyPublished);
        }

        [Test]
        public void AnArmoredEnemy_ShrugsOffAJab_UntilItIsStunned()
        {
            WakeTheEnemyWith(armored);

            Hit(jab);
            Assert.AreEqual(nameof(AppliedReaction.None), ReactionTheEnemyPublished, "armor shrugs off a jab");

            Hit(capeStun);
            Hit(jab);
            Assert.AreEqual(nameof(AppliedReaction.Stagger), ReactionTheEnemyPublished, "stunned, the same jab staggers it");
        }

        [Test]
        public void OnceTheTreeHasClearedTheStun_AJabIsOnlyAFlinchAgain()
        {
            WakeTheEnemyWith(unarmored);
            Hit(capeStun);
            facts.Set(EnemyVariableNames.Reaction, nameof(AppliedReaction.None));

            Hit(jab);

            Assert.AreEqual(nameof(AppliedReaction.Flinch), ReactionTheEnemyPublished);
        }
    }
}
