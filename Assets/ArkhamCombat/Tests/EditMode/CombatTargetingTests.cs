using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using Zenject;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class CombatTargetingTests
    {
        private sealed class RecordingTargetScorer : ITargetScorer
        {
            public Vector3 LastDirection;

            public IActionTarget BestTarget(Vector3 position, Vector3 direction)
            {
                LastDirection = direction;
                return null;
            }
        }

        private const float Tolerance = 1e-4f;

        private GameObject character;
        private RecordingTargetScorer scorer;
        private CombatTargeting targeting;

        [SetUp]
        public void SetUp()
        {
            character = HiddenObject("Character");
            character.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            targeting = character.AddComponent<CombatTargeting>();
            scorer = new RecordingTargetScorer();

            DiContainer container = new DiContainer();
            container.Bind<ITargetScorer>().FromInstance(scorer);
            container.Bind<ITargetingSettings>().FromInstance(new TargetingSettings());
            container.Bind<IMovementFrame>().FromInstance(new WorldMovementFrame());
            container.Inject(targeting);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(character);

        [Test]
        public void AStickAtRest_PicksAlongTheFacing()
        {
            targeting.PickTarget(new Vector2(0.05f, 0f));

            Assert.AreEqual(1f, Vector3.Dot(scorer.LastDirection.normalized, Vector3.right), Tolerance, "the character faces +X");
        }

        [Test]
        public void APushedStick_PicksAlongTheStickInTheMovementFrame()
        {
            targeting.PickTarget(new Vector2(0f, 1f));

            Assert.AreEqual(1f, Vector3.Dot(scorer.LastDirection.normalized, Vector3.forward), Tolerance, "a world frame keeps stick-up as +Z");
        }
    }
}
