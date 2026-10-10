using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Player;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    // The rule alone: the states are only looked at for their type, so they are built without a
    // character. Entering the Attacking state needs the whole controller and is checked in play mode.
    public class AttackingStateSwitchTests
    {
        private const bool Grounded = true;
        private const bool InTheAir = false;

        private static readonly ICharacterState Locomotion = new LocomotionState(null);
        private static readonly ICharacterState Airborne = new AirborneState(null);
        private static readonly ICharacterState Attacking = new AttackingState(null, null);

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
