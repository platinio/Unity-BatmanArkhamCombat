using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class StrikeAnnouncementTests
    {
        private const float JabSeconds = 1f;
        private const string Coming = "coming";
        private const string Over = "over";
        private const float BeforeTheHit = 0.1f;
        private const float WhenTheHitWindowOpens = 0.3f;

        private static readonly Window HitsAfterAWhile = new Window(WhenTheHitWindowOpens, 0.5f);

        private sealed class TargetThatRecordsWhatItIsTold : IActionTarget
        {
            public readonly List<string> WhatItWasTold = new List<string>();

            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; } = Vector3.forward;

            public void StrikeIsComing() => WhatItWasTold.Add(Coming);

            public void StrikeIsOver() => WhatItWasTold.Add(Over);
        }

        private AttackDefinition jab;
        private ActionDefinition taunt;
        private TargetThatRecordsWhatItIsTold target;
        private RecordingActionTargetPicker targetPicker;
        private ActionRunner runner;
        private HitReceiverProfile profile;
        private GameObject enemy;
        private EnemyStatus status;
        private VariableDeclarations facts;

        [SetUp]
        public void SetUp()
        {
            jab = Attack("Jab", JabSeconds, hitWindow: HitsAfterAWhile);
            taunt = Action("Taunt", JabSeconds);
            target = new TargetThatRecordsWhatItIsTold();
            targetPicker = new RecordingActionTargetPicker { TargetToGive = target };
            runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), new NullWarpMover(), new NullHitWindowListener(), targetPicker);

            profile = ReceiverProfile();
            enemy = HiddenObject("Enemy");
            status = enemy.AddComponent<EnemyStatus>();
            status.Construct(profile);
            status.Awake();
            facts = Variables.Object(enemy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(taunt);
        }

        private void TickUntil(float normalizedTime) => runner.Tick(normalizedTime * JabSeconds, Vector3.zero, canStartFromIdle: true);

        private bool IsAStrikeComingAtTheEnemy => (bool)facts.Get(EnemyVariableNames.PlayerStrikeIncoming);

        private float RollOfTheStrike => (float)facts.Get(EnemyVariableNames.IncomingStrikeRoll);

        [Test]
        public void AnAttack_TellsItsTargetAStrikeIsComing_TheMomentItStarts()
        {
            runner.PlayAction(jab, Vector3.zero);

            CollectionAssert.AreEqual(new[] { Coming }, target.WhatItWasTold);
        }

        [Test]
        public void AnAttackAtATargetItCannotReach_AnnouncesNothing()
        {
            target.Position = Vector3.forward * 100f;

            runner.PlayAction(jab, Vector3.zero);
            runner.Cancel();

            CollectionAssert.IsEmpty(target.WhatItWasTold);
        }

        [Test]
        public void AnAttackReplacedBeforeItStruck_TellsItsTargetItIsOver_AndTheNextTargetThatAStrikeIsComing()
        {
            TargetThatRecordsWhatItIsTold nextTarget = new TargetThatRecordsWhatItIsTold();
            runner.PlayAction(jab, Vector3.zero);
            TickUntil(BeforeTheHit);

            targetPicker.TargetToGive = nextTarget;
            runner.PlayAction(jab, Vector3.zero);

            CollectionAssert.AreEqual(new[] { Coming, Over }, target.WhatItWasTold);
            CollectionAssert.AreEqual(new[] { Coming }, nextTarget.WhatItWasTold);
        }

        [Test]
        public void AnEnemyDestroyedWhileAStrikeWasComing_DoesNotBreakTheAttackersAction()
        {
            targetPicker.TargetToGive = status;
            runner.PlayAction(jab, Vector3.zero);
            Assert.IsTrue(IsAStrikeComingAtTheEnemy, "the enemy was told");

            Object.DestroyImmediate(enemy);

            Assert.DoesNotThrow(() => runner.Cancel());
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void BeforeTheHitWindowOpens_TheTargetStillExpectsTheStrike()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(BeforeTheHit);

            CollectionAssert.AreEqual(new[] { Coming }, target.WhatItWasTold);
        }

        [Test]
        public void OnceTheAttackHasStruck_TheTargetIsToldItIsOver()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(WhenTheHitWindowOpens);

            CollectionAssert.AreEqual(new[] { Coming, Over }, target.WhatItWasTold);
        }

        [Test]
        public void AnAttackCutShortBeforeItStruck_StillTellsItsTargetItIsOver()
        {
            runner.PlayAction(jab, Vector3.zero);
            TickUntil(BeforeTheHit);

            runner.Cancel();

            CollectionAssert.AreEqual(new[] { Coming, Over }, target.WhatItWasTold);
        }

        [Test]
        public void TheTargetIsToldItIsOver_OnlyOnce()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(JabSeconds);

            CollectionAssert.AreEqual(new[] { Coming, Over }, target.WhatItWasTold);
        }

        [Test]
        public void AnActionThatIsNotAnAttack_AnnouncesNoStrike()
        {
            runner.PlayAction(taunt, Vector3.zero);
            TickUntil(JabSeconds);

            Assert.IsEmpty(target.WhatItWasTold);
        }

        [Test]
        public void AnAttackAtATargetThatIsGone_AnnouncesNothing()
        {
            target.IsValid = false;

            runner.PlayAction(jab, Vector3.zero);
            TickUntil(JabSeconds);

            Assert.IsEmpty(target.WhatItWasTold);
        }

        [Test]
        public void AnEnemy_StartsWithNoStrikeComing_AndARollNoChanceIsAbove()
        {
            Assert.IsFalse(IsAStrikeComingAtTheEnemy);
            Assert.GreaterOrEqual(RollOfTheStrike, 1f);
        }

        [Test]
        public void AnEnemyToldAStrikeIsComing_PublishesIt_WithARollBelowOne()
        {
            status.StrikeIsComing();

            Assert.IsTrue(IsAStrikeComingAtTheEnemy);
            Assert.GreaterOrEqual(RollOfTheStrike, 0f);
            Assert.Less(RollOfTheStrike, 1f, "a chance of one has to mean every time");
        }

        [Test]
        public void AnEnemyToldTheStrikeIsOver_NoLongerExpectsIt()
        {
            status.StrikeIsComing();

            status.StrikeIsOver();

            Assert.IsFalse(IsAStrikeComingAtTheEnemy);
        }

        [Test]
        public void EachStrike_GetsItsOwnRoll()
        {
            UnityEngine.Random.InitState(11);
            HashSet<float> rolls = new HashSet<float>();

            for (int strike = 0; strike < 5; strike++)
            {
                status.StrikeIsComing();
                rolls.Add(RollOfTheStrike);
                status.StrikeIsOver();
            }

            Assert.Greater(rolls.Count, 1);
        }
    }
}
