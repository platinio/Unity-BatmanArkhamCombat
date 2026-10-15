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
        // The jab lasts one second, so seconds played and normalized time are the same number.
        private const float JabSeconds = 1f;
        private const float BeforeTheHit = 0.1f;
        private const float WhenTheHitWindowOpens = 0.3f;

        private static readonly Window HitsAfterAWhile = new Window(WhenTheHitWindowOpens, 0.5f);

        private sealed class TargetThatRecordsWhatItIsTold : IActionTarget
        {
            public readonly List<string> WhatItWasTold = new List<string>();

            public bool IsValid { get; set; } = true;
            public Vector3 Position => Vector3.forward;

            public void StrikeIsComing() => WhatItWasTold.Add("coming");

            public void StrikeIsOver() => WhatItWasTold.Add("over");
        }

        private AttackDefinition jab;
        private ActionDefinition taunt;
        private TargetThatRecordsWhatItIsTold target;
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
            runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), new NullWarpMover(), new NullHitWindowListener(), new RecordingActionTargetPicker { TargetToGive = target });

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

            CollectionAssert.AreEqual(new[] { "coming" }, target.WhatItWasTold);
        }

        [Test]
        public void BeforeTheHitWindowOpens_TheTargetStillExpectsTheStrike()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(BeforeTheHit);

            CollectionAssert.AreEqual(new[] { "coming" }, target.WhatItWasTold);
        }

        [Test]
        public void OnceTheAttackHasStruck_TheTargetIsToldItIsOver()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(WhenTheHitWindowOpens);

            CollectionAssert.AreEqual(new[] { "coming", "over" }, target.WhatItWasTold);
        }

        [Test]
        public void AnAttackCutShortBeforeItStruck_StillTellsItsTargetItIsOver()
        {
            runner.PlayAction(jab, Vector3.zero);
            TickUntil(BeforeTheHit);

            runner.Cancel();

            CollectionAssert.AreEqual(new[] { "coming", "over" }, target.WhatItWasTold);
        }

        [Test]
        public void TheTargetIsToldItIsOver_OnlyOnce()
        {
            runner.PlayAction(jab, Vector3.zero);

            TickUntil(JabSeconds);

            CollectionAssert.AreEqual(new[] { "coming", "over" }, target.WhatItWasTold);
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
