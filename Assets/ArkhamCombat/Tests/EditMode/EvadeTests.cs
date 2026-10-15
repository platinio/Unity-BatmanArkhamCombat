using ArcaneOnyx.TPCharacterController.Movement;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EvadeTests
    {
        private const float Tolerance = 1e-4f;
        private const float AnyTick = 0.1f;

        private static readonly Vector2 StickAtRest = Vector2.zero;
        private static readonly Vector2 StickPushedRight = Vector2.right;
        private static readonly Vector3 East = Vector3.right;
        private static readonly Vector3 South = Vector3.back;
        private static readonly Vector3 West = Vector3.left;

        private static readonly ICharacterState Locomotion = new LocomotionState(null);
        private static readonly ICharacterState Attacking = new AttackingState(null, null, null);
        private static readonly ICharacterState OnTheFloor = new KnockedDownState(null, null, null);

        private GameObject character;
        private EncounterDirector director;
        private EvadeDirection evadeDirection;
        private DashDefinition evade;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings());
            character = HiddenObject("Character");
            evadeDirection = character.AddComponent<EvadeDirection>();
            evadeDirection.Construct(new TargetingSettings(), new WorldMovementFrame(), director);
            evade = ScriptableObject.CreateInstance<DashDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(evade);
        }

        private StandingAgent AnAgentWindingUpAt(Vector3 position)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, AttackTokenType.Melee);
            director.TelegraphAnAttack(agent, isCounterable: true);
            return agent;
        }

        private void AssertSameDirection(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), Tolerance, $"expected {expected}, was {actual}");

        [Test]
        public void WithTheStickPushed_AnEvadeGoesAlongTheStick_InTheMovementFrame()
        {
            AnAgentWindingUpAt(East);

            AssertSameDirection(East, evadeDirection.PickTravelDirection(StickPushedRight));
        }

        [Test]
        public void WithTheStickAtRest_AnEvadeGoesAwayFromWhoeverIsWindingUp()
        {
            AnAgentWindingUpAt(East * 2f);

            AssertSameDirection(West, evadeDirection.PickTravelDirection(StickAtRest));
        }

        [Test]
        public void WithTheStickAtRest_AndNobodyWindingUp_AnEvadeGoesStraightBack()
        {
            AssertSameDirection(South, evadeDirection.PickTravelDirection(StickAtRest));
        }

        [Test]
        public void AnAttackerStandingOnTheCharacter_IsEvadedStraightBack()
        {
            AnAgentWindingUpAt(character.transform.position);

            AssertSameDirection(South, evadeDirection.PickTravelDirection(StickAtRest));
        }

        [Test]
        public void TheDirectionAwayFromAnAttacker_IsOnTheGround()
        {
            AnAgentWindingUpAt(East * 2f + Vector3.up * 5f);

            AssertSameDirection(West, evadeDirection.PickTravelDirection(StickAtRest));
        }

        [Test]
        public void OfTwoAgentsWindingUp_TheDirectorNamesTheNearerOne()
        {
            AnAgentWindingUpAt(East * 6f);
            StandingAgent nearer = AnAgentWindingUpAt(West * 2f);

            Assert.IsTrue(director.TryGetNearestTelegraphingAgent(Vector3.zero, out IEncounterAgent nearest));
            Assert.AreSame(nearer, nearest);
        }

        [Test]
        public void AnAgentThatStoppedWindingUp_IsNoLongerNamed()
        {
            StandingAgent agent = AnAgentWindingUpAt(East);

            director.StopTelegraphing(agent);

            Assert.IsFalse(director.TryGetNearestTelegraphingAgent(Vector3.zero, out IEncounterAgent nobody));
            Assert.IsNull(nobody);
        }

        [Test]
        public void AnAgentThatOnlyStandsThere_IsNotWindingUp()
        {
            director.Register(new StandingAgent(East), AttackTokenType.Melee);
            director.Tick(AnyTick);

            Assert.IsFalse(director.TryGetNearestTelegraphingAgent(Vector3.zero, out IEncounterAgent _));
        }

        [Test]
        public void TheCharacterEntersEvading_WhenADashPlays()
        {
            Assert.IsTrue(EvadingStateSwitch.ShouldEnterEvadingWhen(evade, Locomotion));
        }

        [Test]
        public void AnEvadeOutOfAnAttack_TakesTheCharacterOutOfAttacking()
        {
            Assert.IsTrue(EvadingStateSwitch.ShouldEnterEvadingWhen(evade, Attacking));
        }

        [Test]
        public void TheCharacterIsLeftWhereItIs_WhenWhatPlaysIsNotADash()
        {
            Assert.IsFalse(EvadingStateSwitch.ShouldEnterEvadingWhen(currentDash: null, Locomotion));
        }

        [Test]
        public void ACharacterInAStateThatCannotBeInterrupted_IsNotPulledIntoEvading()
        {
            Assert.IsFalse(EvadingStateSwitch.ShouldEnterEvadingWhen(evade, OnTheFloor));
        }

        [Test]
        public void AnEvadingCharacter_CanBeInterrupted_SoAHitAfterItsInvulnerableWindowStillReacts()
        {
            ICharacterState evading = new EvadingState(null, null);

            Assert.IsTrue(evading.CanBeInterrupted);
        }
    }
}
