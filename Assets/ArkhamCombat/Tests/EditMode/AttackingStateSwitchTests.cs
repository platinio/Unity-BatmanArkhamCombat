using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    // The rule alone: the states are only asked what they allow, so they are built without a
    // character. Entering the Attacking state needs the whole controller and is checked in play mode.
    public class AttackingStateSwitchTests
    {
        private const bool Grounded = true;
        private const bool InTheAir = false;

        private static readonly ICharacterState Locomotion = new LocomotionState(null);
        private static readonly ICharacterState Airborne = new AirborneState(null);
        private static readonly ICharacterState Attacking = new AttackingState(null, null, null);
        private static readonly ICharacterState Flinching = new FlinchState(null, null, null);
        private static readonly ICharacterState OnTheFloor = new KnockedDownState(null, null, null);

        [Test]
        public void TheCharacterEntersAttacking_WhenAnAttackPlays()
        {
            AttackDefinition jab = CombatTestDoubles.Attack("Jab");

            Assert.IsTrue(AttackingStateSwitch.ShouldEnterAttackingWhen(jab, Locomotion));

            UnityEngine.Object.DestroyImmediate(jab);
        }

        [Test]
        public void TheCharacterIsLeftWhereItIs_WhenWhatPlaysIsNotAnAttack()
        {
            Assert.IsFalse(AttackingStateSwitch.ShouldEnterAttackingWhen(currentAttack: null, Locomotion));
        }

        [Test]
        public void ACharacterInAStateThatCannotBeInterrupted_IsNotPulledIntoAttacking()
        {
            AttackDefinition jab = CombatTestDoubles.Attack("Jab");

            Assert.IsFalse(AttackingStateSwitch.ShouldEnterAttackingWhen(jab, OnTheFloor));

            UnityEngine.Object.DestroyImmediate(jab);
        }

        [Test]
        public void TheCharacterAlreadyAttacking_IsNotEnteredAgain()
        {
            CharacterStateMachine stateMachine = new CharacterStateMachine();
            stateMachine.Register(Attacking);
            int timesTheStateChanged = 0;
            stateMachine.StateChanged += (previous, next) => timesTheStateChanged++;

            stateMachine.Change<AttackingState>();
            stateMachine.Change<AttackingState>();

            Assert.AreEqual(1, timesTheStateChanged);
        }

        [Test]
        public void AnActionCanStart_OnTheGroundInLocomotion()
        {
            Assert.IsTrue(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, Locomotion));
        }

        [Test]
        public void AnActionCanStart_WhileStillInAttackingAfterAnActionEnded()
        {
            Assert.IsTrue(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, Attacking));
        }

        [Test]
        public void NoActionStarts_InAStateThatCannotAttack()
        {
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, Airborne));
        }

        [Test]
        public void NoActionStarts_WhileTheCharacterReacts()
        {
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, Flinching));
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, OnTheFloor));
        }

        [Test]
        public void NoActionStarts_BeforeTheCharacterHasAState()
        {
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(Grounded, null));
        }

        [Test]
        public void NoActionStarts_InTheAir()
        {
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(InTheAir, Locomotion));
            Assert.IsFalse(AttackingStateSwitch.CanStartFromIdleWhen(InTheAir, Attacking));
        }
    }
}
