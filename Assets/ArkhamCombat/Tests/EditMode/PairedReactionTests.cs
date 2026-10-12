using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class PairedReactionTests
    {
        private const float FullHealth = 100f;
        private const float CounterDamage = 10f;
        private const float CounterSeconds = 1f;
        private const float SecondsUntilTheCounterHits = 0.3f;

        private static readonly Window HitsAfterAWhile = new Window(SecondsUntilTheCounterHits, 0.5f);
        private static readonly Vector3 AnyDirection = Vector3.forward;

        private sealed class TargetThatRecordsPairedReactions : IActionTarget
        {
            public readonly List<AttackDefinition> PairedAttacksItWasToldOf = new List<AttackDefinition>();

            public bool IsValid { get; set; } = true;
            public Vector3 Position => Vector3.forward;

            public void BeginPairedReaction(AttackDefinition attack) => PairedAttacksItWasToldOf.Add(attack);
        }

        private ActionDefinition countered;
        private AttackDefinition counter;
        private AttackDefinition cross;
        private HitReceiverProfile profile;
        private HitReceiverProfile armoredProfile;
        private GameObject enemy;
        private EnemyStatus status;
        private VariableDeclarations facts;
        private CombatActions enemyActions;

        [SetUp]
        public void SetUp()
        {
            countered = Action("Countered", duration: 1f);
            counter = Attack("Counter", CounterSeconds, hitWindow: HitsAfterAWhile, damage: CounterDamage, reaction: HitReaction.Stagger);
            counter.ConfigurePairedReaction(countered);
            cross = Attack("Cross", damage: CounterDamage, reaction: HitReaction.Stagger);
            profile = ReceiverProfile(FullHealth);
            armoredProfile = ReceiverProfile(FullHealth, isArmored: true, canBeKnockedDown: false);

            enemy = HiddenObject("Enemy");
            enemyActions = enemy.AddComponent<CombatActions>();
            PendingWarpMovement pendingWarpMovement = new PendingWarpMovement();
            enemyActions.Construct(ARunner(warpMover: pendingWarpMovement), pendingWarpMovement, new AlwaysOpenActionStartGate(), null);
            status = enemy.AddComponent<EnemyStatus>();
            facts = Variables.Object(enemy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(countered);
            Object.DestroyImmediate(counter);
            Object.DestroyImmediate(cross);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(armoredProfile);
        }

        private static ActionRunner ARunner(IActionTargetPicker targetPicker = null, IWarpMover warpMover = null) =>
            new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), warpMover, new NullHitWindowListener(), targetPicker);

        private static ActionRunner ARunnerAimedAt(IActionTarget target) =>
            ARunner(new RecordingActionTargetPicker { TargetToGive = target });

        private void WakeTheEnemyWith(HitReceiverProfile profileToUse)
        {
            status.Construct(profileToUse);
            status.Awake();
        }

        private HitInfo TheHitOf(AttackDefinition attack) => new HitInfo(attack, AnyDirection);

        [Test]
        public void APairedAttack_TellsItsTargetTheMomentItStarts()
        {
            TargetThatRecordsPairedReactions target = new TargetThatRecordsPairedReactions();

            ARunnerAimedAt(target).PlayAction(counter, Vector3.zero);

            CollectionAssert.AreEqual(new[] { counter }, target.PairedAttacksItWasToldOf);
        }

        [Test]
        public void AnOrdinaryAttack_TellsItsTargetNothingBeforeItHits()
        {
            TargetThatRecordsPairedReactions target = new TargetThatRecordsPairedReactions();

            ARunnerAimedAt(target).PlayAction(cross, Vector3.zero);

            Assert.IsEmpty(target.PairedAttacksItWasToldOf);
        }

        [Test]
        public void APairedAttackAtATargetThatIsGone_TellsNobody()
        {
            TargetThatRecordsPairedReactions target = new TargetThatRecordsPairedReactions { IsValid = false };

            ARunnerAimedAt(target).PlayAction(counter, Vector3.zero);

            Assert.IsEmpty(target.PairedAttacksItWasToldOf);
        }

        [Test]
        public void APairedAttackWithNoTarget_StillPlays()
        {
            ActionRunner runner = ARunner();

            Assert.DoesNotThrow(() => runner.PlayAction(counter, Vector3.zero));
            Assert.AreSame(counter, runner.CurrentAttack);
        }

        [TestCase(HitReaction.Flinch, AppliedReaction.Flinch)]
        [TestCase(HitReaction.Stagger, AppliedReaction.Stagger)]
        [TestCase(HitReaction.Knockdown, AppliedReaction.Knockdown)]
        public void TheTargetOfAPairedAttack_PlaysTheReactionThatWasAskedFor(HitReaction asked, AppliedReaction played)
        {
            Assert.AreEqual(played, ReactionRules.ResolveForAPairedAttack(asked, FullHealth));
        }

        [Test]
        public void TheTargetOfAPairedAttack_DiesWhenItHasNoHealthLeft()
        {
            Assert.AreEqual(AppliedReaction.Death, ReactionRules.ResolveForAPairedAttack(HitReaction.Stagger, health: 0f));
        }

        [Test]
        public void ArmorDoesNotLightenThePairedHit_WhichWouldCutTheReactionItStartedShort()
        {
            HitReceiver armored = new HitReceiver(armoredProfile);

            Assert.AreEqual(AppliedReaction.Stagger, armored.Receive(TheHitOf(counter)).AppliedReaction);
            Assert.AreEqual(AppliedReaction.Flinch, new HitReceiver(armoredProfile).Receive(TheHitOf(cross)).AppliedReaction, "sanity: an ordinary stagger is lightened by armor");
        }

        [Test]
        public void APairedReactionThatEndsBeforeTheHit_IsReported()
        {
            countered.Configure(SecondsUntilTheCounterHits / 2f);
            List<string> errors = new List<string>();

            Assert.IsFalse(counter.Validate(errors));
            StringAssert.Contains(countered.name, errors[0]);
        }

        [Test]
        public void APairedReactionThatLastsUntilTheHit_IsWellFormed()
        {
            countered.Configure(SecondsUntilTheCounterHits);
            List<string> errors = new List<string>();

            Assert.IsTrue(counter.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void AnEnemy_StartsWithNoActionToPlayInPlaceOfItsOwn()
        {
            WakeTheEnemyWith(profile);

            Assert.IsTrue(facts.IsDefined(EnemyVariableNames.ReactionOverride), "the branches read it on every reaction, so it has to be there");
            Assert.IsNull(facts.Get(EnemyVariableNames.ReactionOverride));
        }

        [Test]
        public void AnEnemyToldOfAPairedAttack_ReactsAtOnce_WithTheActionTheAttackBrought()
        {
            WakeTheEnemyWith(profile);

            status.BeginPairedReaction(counter);

            Assert.AreEqual(nameof(AppliedReaction.Stagger), facts.Get(EnemyVariableNames.Reaction));
            Assert.AreSame(countered, facts.Get(EnemyVariableNames.ReactionOverride));
            Assert.AreEqual(1f, (float)facts.Get(EnemyVariableNames.Health01), 1e-5f, "nothing has hit it yet");
        }

        [Test]
        public void AnEnemyToldOfAPairedAttack_IsCutShortInWhateverItWasPlaying()
        {
            WakeTheEnemyWith(profile);
            enemyActions.Runner.PlayAction(cross, Vector3.zero);

            status.BeginPairedReaction(counter);

            Assert.IsFalse(enemyActions.Runner.IsPlaying, "a countered attacker must not land the swing it had started");
        }

        [Test]
        public void TheHitOfThePairedAttack_HurtsTheEnemy_AndLeavesItInTheReactionItIsAlreadyPlaying()
        {
            // Only an agent with a machine keeps the versions a guard wakes on.
            enemy.AddComponent<BehaviorTreeMachine>();
            WakeTheEnemyWith(profile);
            status.BeginPairedReaction(counter);
            int versionBeforeTheHit = enemy.GetComponent<AgentVariableWriter>().VersionOf(EnemyVariableNames.Reaction);

            HitResult result = status.Receive(TheHitOf(counter));

            Assert.IsTrue(result.HasLanded);
            Assert.Less((float)facts.Get(EnemyVariableNames.Health01), 1f);
            Assert.AreEqual(versionBeforeTheHit, enemy.GetComponent<AgentVariableWriter>().VersionOf(EnemyVariableNames.Reaction), "the reaction fact did not move, so its branch is not restarted");
        }

        [Test]
        public void AfterAHitLands_TheNextReactionPlaysItsOwnAction()
        {
            WakeTheEnemyWith(profile);
            status.BeginPairedReaction(counter);

            status.Receive(TheHitOf(cross));

            Assert.IsNull(facts.Get(EnemyVariableNames.ReactionOverride));
        }

        [Test]
        public void ADeadEnemy_IsNotMadeToReact()
        {
            profile.Configure(CounterDamage);
            WakeTheEnemyWith(profile);
            status.Receive(TheHitOf(cross));

            status.BeginPairedReaction(counter);

            Assert.IsNull(facts.Get(EnemyVariableNames.ReactionOverride));
            Assert.AreEqual(nameof(AppliedReaction.None), facts.Get(EnemyVariableNames.Reaction));
        }
    }
}
