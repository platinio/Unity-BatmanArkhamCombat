using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class NavMeshWarpMoverTests
    {
        private const float AnyDeltaTime = 0.02f;

        private GameObject thug;
        private PendingWarpMovement pendingWarpMovement;
        private NavMeshWarpMover warpMover;

        [SetUp]
        public void SetUp()
        {
            thug = HiddenObject("Thug");
            pendingWarpMovement = new PendingWarpMovement();
            ActionRunner runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), pendingWarpMovement, new NullHitWindowListener());
            thug.AddComponent<CombatActions>().Construct(runner, pendingWarpMovement, new AlwaysOpenActionStartGate(), null);
            warpMover = thug.AddComponent<NavMeshWarpMover>();
            warpMover.Awake();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(thug);

        [Test]
        public void TheWarpMovementOfTheFrame_IsTaken_EvenWhereItCannotBeWalked()
        {
            pendingWarpMovement.MoveBy(Vector3.forward);

            warpMover.Tick(AnyDeltaTime);

            Assert.AreEqual(Vector3.zero, pendingWarpMovement.Movement);
        }

        [Test]
        public void OffTheNavMesh_TheAgentIsNotAskedToMove()
        {
            pendingWarpMovement.MoveBy(Vector3.forward);

            warpMover.Tick(AnyDeltaTime);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
