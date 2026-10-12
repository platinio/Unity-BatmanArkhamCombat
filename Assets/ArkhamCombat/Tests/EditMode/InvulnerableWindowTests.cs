using System.Collections.Generic;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class InvulnerableWindowTests
    {
        private const float ActionSeconds = 1f;
        private const float HealthOfOneHit = 1f;
        private const float BeforeTheWindow = 0.1f;
        private const float InsideTheWindow = 0.4f;
        private const float AfterTheWindow = 0.8f;
        private const float FullHealth = 100f;

        private static readonly Window InvulnerableInTheMiddle = new Window(0.2f, 0.6f);
        private static readonly Vector3 AnyDirection = Vector3.forward;

        private ActionDefinition evade;
        private ActionDefinition taunt;
        private AttackDefinition jab;
        private HitReceiverProfile profile;
        private ActionRunner runner;

        [SetUp]
        public void SetUp()
        {
            evade = Action("Evade", ActionSeconds);
            evade.ConfigureInvulnerableWindow(InvulnerableInTheMiddle);
            taunt = Action("Taunt", ActionSeconds);
            jab = Attack("Jab");
            profile = ReceiverProfile(FullHealth);
            runner = new ActionRunner(null, null, null, null, new InterruptKinds(null, null), new NullPresentationDriver(), null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(evade);
            Object.DestroyImmediate(taunt);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(profile);
        }

        private void PlayUntil(ActionDefinition action, float normalizedTime)
        {
            runner.PlayAction(action, Vector3.zero);
            runner.Tick(normalizedTime * ActionSeconds, Vector3.zero, canStartFromIdle: true);
        }

        private HitInfo AJab() => new HitInfo(jab, AnyDirection);

        [Test]
        public void ACharacterPlayingNothing_IsNotInvulnerable()
        {
            Assert.IsFalse(runner.IsInvulnerable);
        }

        [Test]
        public void InsideTheWindow_TheCharacterIsInvulnerable()
        {
            PlayUntil(evade, InsideTheWindow);

            Assert.IsTrue(runner.IsInvulnerable);
        }

        [TestCase(BeforeTheWindow)]
        [TestCase(AfterTheWindow)]
        public void OutsideTheWindow_TheCharacterCanBeHit(float normalizedTime)
        {
            PlayUntil(evade, normalizedTime);

            Assert.IsFalse(runner.IsInvulnerable);
        }

        [Test]
        public void AnActionWithoutAWindow_NeverMakesItsCharacterInvulnerable()
        {
            PlayUntil(taunt, InsideTheWindow);

            Assert.IsFalse(runner.IsInvulnerable);
        }

        [Test]
        public void OnceTheActionIsCutShort_TheCharacterCanBeHitAgain()
        {
            PlayUntil(evade, InsideTheWindow);

            runner.Cancel();

            Assert.IsFalse(runner.IsInvulnerable);
        }

        [Test]
        public void AHitOnAnInvulnerableCharacter_IsEvaded_AndCostsItNoHealth()
        {
            HitReceiver receiver = new HitReceiver(profile, runner);
            PlayUntil(evade, InsideTheWindow);

            HitResult result = receiver.Receive(AJab());

            Assert.IsTrue(result.WasEvaded);
            Assert.IsFalse(result.HasLanded);
            Assert.AreEqual(AppliedReaction.None, result.AppliedReaction);
            Assert.AreEqual(FullHealth, receiver.Health);
        }

        [Test]
        public void AHitAfterTheWindow_LandsAsUsual()
        {
            HitReceiver receiver = new HitReceiver(profile, runner);
            PlayUntil(evade, AfterTheWindow);

            HitResult result = receiver.Receive(AJab());

            Assert.IsTrue(result.HasLanded);
            Assert.IsFalse(result.WasEvaded);
            Assert.Less(receiver.Health, FullHealth);
        }

        [Test]
        public void AReceiverThatPlaysNoActions_IsNeverInvulnerable()
        {
            HitReceiver receiver = new HitReceiver(profile);

            Assert.IsTrue(receiver.Receive(AJab()).HasLanded);
        }

        [Test]
        public void TheDead_IgnoreAHit_RatherThanEvadeIt()
        {
            profile.Configure(HealthOfOneHit);
            HitReceiver receiver = new HitReceiver(profile, runner);
            receiver.Receive(AJab());
            PlayUntil(evade, InsideTheWindow);

            HitResult result = receiver.Receive(AJab());

            Assert.IsFalse(result.WasEvaded);
            Assert.IsFalse(result.HasLanded);
        }

        [Test]
        public void AWindowThatStartsWithTheAction_IsOpenBeforeTheFirstTick()
        {
            evade.ConfigureInvulnerableWindow(new Window(0f, 0.6f));

            runner.PlayAction(evade, Vector3.zero);

            Assert.IsTrue(runner.IsInvulnerable);
        }

        [Test]
        public void AnInvulnerableWindowThatEndsBeforeItStarts_IsReported()
        {
            evade.ConfigureInvulnerableWindow(new Window(0.6f, 0.2f));
            List<string> errors = new List<string>();

            Assert.IsFalse(evade.Validate(errors));
            StringAssert.Contains("invulnerableWindow", errors[0]);
        }

        [Test]
        public void AnAttackMayBeInvulnerableToo()
        {
            jab.ConfigureInvulnerableWindow(InvulnerableInTheMiddle);

            PlayUntil(jab, InsideTheWindow);

            Assert.IsTrue(runner.IsInvulnerable);
        }
    }
}
