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
    // The states are real and play on a real runner; the brain is left out, so the test ticks the
    // runner and the state by hand, in the order the brain would.
    public class HitReactionSwitchTests
    {
        private const float ReactionSeconds = 0.5f;
        private const float TickSeconds = 0.1f;

        private GameObject character;
        private MotorConfig motorConfig;
        private ActionRunner runner;
        private CharacterStateMachine stateMachine;
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
            CharacterMotor motor = character.AddComponent<CharacterMotor>();
            motor.Initialize(motorConfig);

            PendingWarpMovement pendingWarpMovement = new PendingWarpMovement();
            runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), pendingWarpMovement, new NullHitWindowListener());
            combatActions = character.AddComponent<CombatActions>();
            combatActions.Construct(runner, pendingWarpMovement, new AlwaysOpenActionStartGate(), null);

            stateMachine = new CharacterStateMachine();
            context = new CharacterContext(motor, input: null, profile: null, movementFrame: null) { StateMachine = stateMachine };
            stateMachine.Register(new LocomotionState(context));
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
            Object.DestroyImmediate(flinch);
            Object.DestroyImmediate(stagger);
            Object.DestroyImmediate(knockdown);
            Object.DestroyImmediate(getUp);
            Object.DestroyImmediate(death);
        }

        private void ReactTo(AppliedReaction reaction) => HitReactionSwitch.EnterTheStateFor(reaction, stateMachine);

        // Stops once the character is back in locomotion: that state needs a floor and an input, and the test has neither.
        private void TickTheCharacterFor(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / TickSeconds);
            for (int i = 0; i < ticks && !(stateMachine.Current is LocomotionState); i++)
            {
                combatActions.Tick(TickSeconds);
                stateMachine.Tick(TickSeconds);
            }
        }

        private void TickTheCharacterPastTheEndOfOneAction() => TickTheCharacterFor(ReactionSeconds + TickSeconds);

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
            TickTheCharacterFor(2 * TickSeconds);
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
            TickTheCharacterFor(2 * TickSeconds);
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

            TickTheCharacterFor(4 * ReactionSeconds);

            Assert.IsInstanceOf<DeadState>(stateMachine.Current);
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void AStateWithAMissingAction_SaysSoWhenItIsBuilt()
        {
            LogAssert.Expect(LogType.Error, new Regex("FlinchState.*Action 0 of this reaction is not assigned"));

            new FlinchState(context, combatActions, new ActionDefinition[] { null });
        }
    }
}
