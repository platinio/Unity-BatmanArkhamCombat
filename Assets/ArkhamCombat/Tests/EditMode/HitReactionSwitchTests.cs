using System.Text.RegularExpressions;
using ArcaneOnyx.TPCharacterController.Configuration;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The states are real and play on a real runner and motor. The brain is left out, so the test
    // ticks the runner and then the state machine by hand.
    public class HitReactionSwitchTests
    {
        private const float ReactionSeconds = 0.5f;
        private const float TickSeconds = 0.1f;
        private const float PartWayThroughTheReaction = 2 * TickSeconds;
        private const float LongAfterTheReactionEnded = 4 * ReactionSeconds;
        private const float MaxHealth = 100f;

        private GameObject character;
        private CharacterMotor motor;
        private PlayerStatus status;
        private HitReceiverProfile profile;
        private MotorConfig motorConfig;
        private ActionRunner runner;
        private CharacterStateMachine stateMachine;
        private LocomotionState locomotion;
        private CharacterContext context;
        private CombatActions combatActions;
        private ActionDefinition flinch;
        private ActionDefinition stagger;
        private ActionDefinition knockdown;
        private ActionDefinition getUp;
        private ActionDefinition death;

        [SetUp]
        public void SetUp()
        {
            flinch = Action("Flinch", ReactionSeconds);
            stagger = Action("Stagger", ReactionSeconds);
            knockdown = Action("Knockdown", ReactionSeconds);
            getUp = Action("GetUp", ReactionSeconds);
            death = Action("Death", ReactionSeconds);

            character = HiddenObject("Character");
            motorConfig = ScriptableObject.CreateInstance<MotorConfig>();
            motor = character.AddComponent<CharacterMotor>();
            motor.Initialize(motorConfig);

            profile = ReceiverProfile(MaxHealth);
            status = character.AddComponent<PlayerStatus>();
            status.Construct(profile);
            status.Awake();

            PendingWarpMovement pendingWarpMovement = new PendingWarpMovement();
            runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), pendingWarpMovement, new NullHitWindowListener());
            combatActions = character.AddComponent<CombatActions>();
            combatActions.Construct(runner, pendingWarpMovement, new AlwaysOpenActionStartGate(), null);

            stateMachine = new CharacterStateMachine();
            context = new CharacterContext(motor, input: null, profile: null, movementFrame: null) { StateMachine = stateMachine };
            locomotion = new LocomotionState(context);
            stateMachine.Register(locomotion);
            stateMachine.Register(new FlinchState(context, combatActions, new[] { flinch }));
            stateMachine.Register(new StaggerState(context, combatActions, new[] { stagger }));
            stateMachine.Register(new KnockedDownState(context, combatActions, new[] { knockdown, getUp }));
            stateMachine.Register(new DeadState(context, combatActions, new[] { death }));
            stateMachine.Change<LocomotionState>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(motorConfig);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(flinch);
            Object.DestroyImmediate(stagger);
            Object.DestroyImmediate(knockdown);
            Object.DestroyImmediate(getUp);
            Object.DestroyImmediate(death);
        }

        private void ReactTo(AppliedReaction reaction) => HitReactionSwitch.EnterTheStateFor(reaction, stateMachine);

        // Locomotion needs a floor and an input, and the test has neither, so the ticking stops there.
        private void TickTheCharacterWhileItReactsFor(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / TickSeconds);
            for (int i = 0; i < ticks && stateMachine.Current != locomotion; i++)
            {
                combatActions.Tick(TickSeconds);
                stateMachine.Tick(TickSeconds);
            }
        }

        private void TickTheCharacterPastTheEndOfOneAction() => TickTheCharacterWhileItReactsFor(ReactionSeconds + TickSeconds);

        [Test]
        public void AHitOnThePlayer_BecomesItsReactionState_WhenTheSwitchNextTicks()
        {
            AttackDefinition cross = Attack("Cross", reaction: HitReaction.Stagger);
            status.Receive(new HitInfo(cross, Vector3.forward));
            Assert.IsInstanceOf<LocomotionState>(stateMachine.Current, "the hit itself changes no state");

            HitReactionSwitch.EnterTheStateForThePendingReaction(status, stateMachine);

            Assert.IsInstanceOf<StaggerState>(stateMachine.Current);
            Object.DestroyImmediate(cross);
        }

        [Test]
        public void WithNoHitPending_TheSwitchLeavesTheStateAlone()
        {
            HitReactionSwitch.EnterTheStateForThePendingReaction(status, stateMachine);

            Assert.IsInstanceOf<LocomotionState>(stateMachine.Current);
        }

        [Test]
        public void WhileItReacts_TheCharacterStandsStill()
        {
            motor.Tick(new MotionIntent { PlanarVelocity = Vector3.forward, Rotation = Quaternion.identity }, TickSeconds);
            Assert.AreNotEqual(Vector3.zero, motor.PlanarVelocity, "sanity: it was moving before the hit");

            ReactTo(AppliedReaction.Flinch);
            TickTheCharacterWhileItReactsFor(TickSeconds);

            Assert.AreEqual(Vector3.zero, motor.PlanarVelocity);
        }

        [Test]
        public void AFlinch_EntersTheFlinchState_WhichPlaysItsAction()
        {
            ReactTo(AppliedReaction.Flinch);

            Assert.IsInstanceOf<FlinchState>(stateMachine.Current);
            Assert.AreSame(flinch, runner.CurrentAction);
        }

        [Test]
        public void AStagger_EntersTheStaggerState_WhichPlaysItsAction()
        {
            ReactTo(AppliedReaction.Stagger);

            Assert.IsInstanceOf<StaggerState>(stateMachine.Current);
            Assert.AreSame(stagger, runner.CurrentAction);
        }

        [Test]
        public void AStun_StaggersThePlayer_WhoHasNoStunnedStateOfItsOwn()
        {
            ReactTo(AppliedReaction.Stun);

            Assert.IsInstanceOf<StaggerState>(stateMachine.Current);
        }

        private static System.Array EveryReaction() => System.Enum.GetValues(typeof(AppliedReaction));

        [TestCaseSource(nameof(EveryReaction))]
        public void EveryReactionAHitCanCause_HasAnAnswerOnThePlayer(AppliedReaction reaction)
        {
            ReactTo(reaction);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AHitThatCausedNoReaction_ChangesNothing()
        {
            ReactTo(AppliedReaction.None);

            Assert.IsInstanceOf<LocomotionState>(stateMachine.Current);
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void WhenTheReactionEnds_TheCharacterReturnsToLocomotion()
        {
            ReactTo(AppliedReaction.Flinch);

            TickTheCharacterPastTheEndOfOneAction();

            Assert.IsInstanceOf<LocomotionState>(stateMachine.Current);
        }

        [Test]
        public void TheSameReactionAgain_DoesNotStartItOver()
        {
            ReactTo(AppliedReaction.Flinch);
            TickTheCharacterWhileItReactsFor(PartWayThroughTheReaction);
            float timeBeforeTheSecondHit = runner.NormalizedTime;

            ReactTo(AppliedReaction.Flinch);

            Assert.AreEqual(timeBeforeTheSecondHit, runner.NormalizedTime);
        }

        [Test]
        public void ALighterReaction_CutsAHeavierOneShort()
        {
            ReactTo(AppliedReaction.Stagger);

            ReactTo(AppliedReaction.Flinch);

            Assert.IsInstanceOf<FlinchState>(stateMachine.Current);
            Assert.AreSame(flinch, runner.CurrentAction);
        }

        [Test]
        public void AKnockdown_PlaysTheFall_ThenTheGetUp_ThenReturnsToLocomotion()
        {
            ReactTo(AppliedReaction.Knockdown);
            Assert.AreSame(knockdown, runner.CurrentAction);

            TickTheCharacterPastTheEndOfOneAction();
            Assert.IsInstanceOf<KnockedDownState>(stateMachine.Current);
            Assert.AreSame(getUp, runner.CurrentAction);

            TickTheCharacterPastTheEndOfOneAction();
            Assert.IsInstanceOf<LocomotionState>(stateMachine.Current);
        }

        [TestCase(AppliedReaction.Flinch)]
        [TestCase(AppliedReaction.Stagger)]
        [TestCase(AppliedReaction.Knockdown)]
        public void ACharacterOnTheFloor_ShrugsOffEveryReactionButDeath(AppliedReaction reaction)
        {
            ReactTo(AppliedReaction.Knockdown);
            TickTheCharacterWhileItReactsFor(PartWayThroughTheReaction);
            float timeBeforeTheSecondHit = runner.NormalizedTime;

            ReactTo(reaction);

            Assert.IsInstanceOf<KnockedDownState>(stateMachine.Current);
            Assert.AreSame(knockdown, runner.CurrentAction);
            Assert.AreEqual(timeBeforeTheSecondHit, runner.NormalizedTime);
        }

        [Test]
        public void Death_TakesACharacterOffTheFloorIntoTheDeadState()
        {
            ReactTo(AppliedReaction.Knockdown);

            ReactTo(AppliedReaction.Death);

            Assert.IsInstanceOf<DeadState>(stateMachine.Current);
            Assert.AreSame(death, runner.CurrentAction);
        }

        [Test]
        public void TheDead_NeverLeaveTheDeadState()
        {
            ReactTo(AppliedReaction.Death);

            TickTheCharacterWhileItReactsFor(LongAfterTheReactionEnded);

            Assert.IsInstanceOf<DeadState>(stateMachine.Current);
            Assert.IsFalse(runner.IsPlaying);
        }

        [TestCase(AppliedReaction.Flinch)]
        [TestCase(AppliedReaction.Stagger)]
        [TestCase(AppliedReaction.Knockdown)]
        [TestCase(AppliedReaction.Death)]
        public void TheDead_ReactToNothing(AppliedReaction reaction)
        {
            ReactTo(AppliedReaction.Death);
            TickTheCharacterWhileItReactsFor(LongAfterTheReactionEnded);

            ReactTo(reaction);

            Assert.IsInstanceOf<DeadState>(stateMachine.Current);
            Assert.IsFalse(runner.IsPlaying, "the death is not played again either");
        }

        [Test]
        public void ABindingWithAnEmptyActionSlot_SaysWhichOne()
        {
            LogAssert.Expect(LogType.Error, new Regex("KnockedDownStateBinding.*knockdownAction"));
            LogAssert.Expect(LogType.Error, new Regex("KnockedDownStateBinding.*getUpAction"));

            new KnockedDownStateBinding().Install(new Zenject.DiContainer());
        }
    }
}
