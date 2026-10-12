using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode has no NavMesh, so the walk itself is checked in play mode. What is pinned here is
    // that the movement never piles up, which would throw off where the next strike is measured from.
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
        public void OffTheNavMesh_TheEnemyStaysWhereItIs()
        {
            Vector3 whereItStood = thug.transform.position;
            pendingWarpMovement.MoveBy(Vector3.forward);

            warpMover.Tick(AnyDeltaTime);

            Assert.AreEqual(whereItStood, thug.transform.position);
        }
    }
}
