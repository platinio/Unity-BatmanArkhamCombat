using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterDirectorBehaviourTests
    {
        private const float MaxTokenHoldSeconds = 3f;

        private const float AnyTick = 0.1f;
        private const float Tolerance = 1e-4f;

        private sealed class FixedTargetForward : ITargetForwardSource
        {
            public FixedTargetForward(Vector3 targetForward) => TargetForward = targetForward;

            public Vector3 TargetForward { get; }
        }

        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject target;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings(maxTokenHoldSeconds: MaxTokenHoldSeconds));
            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            target = HiddenObject("Target");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(encounterObject);
        }

        [Test]
        public void EachTick_CentresTheRingOnTheTarget()
        {
            encounter.Construct(director);
            encounter.SetTarget(target.transform);
            target.transform.position = new Vector3(10f, 0f, -4f);

            encounter.Tick(AnyTick);

            Assert.AreEqual(target.transform.position, director.TargetPosition);
        }

        [Test]
        public void TheTargetsFront_IsWhereTheTargetFaces_WhenTheGameSaysNothingElse()
        {
            encounter.Construct(director);
            encounter.SetTarget(target.transform);
            target.transform.rotation = Quaternion.LookRotation(Vector3.right);

            encounter.Tick(AnyTick);

            Assert.Less((director.TargetForward - Vector3.right).magnitude, Tolerance);
        }

        [Test]
        public void TheTargetsFront_IsWhatTheGameSays_WhenItSays()
        {
            encounter.Construct(director, new FixedTargetForward(Vector3.back));
            encounter.SetTarget(target.transform);
            target.transform.rotation = Quaternion.LookRotation(Vector3.right);

            encounter.Tick(AnyTick);

            Assert.AreEqual(Vector3.back, director.TargetForward);
        }

        [Test]
        public void EachTick_PassesItsTimeToTheDirector()
        {
            encounter.Construct(director);
            StandingAgent agent = new StandingAgent();
            director.Register(agent, AttackTokenType.Melee);
            Assert.IsTrue(TakeAToken(director, agent), "sanity: it holds the token before time passes");

            encounter.Tick(MaxTokenHoldSeconds);

            Assert.IsFalse(director.HasToken(agent), "the token was held for the whole of the time that passed");
        }

        [Test]
        public void WithoutATarget_TheRingStaysWhereItWas()
        {
            encounter.Construct(director);
            director.SetTarget(new Vector3(2f, 0f, 2f), Vector3.forward);

            encounter.Tick(AnyTick);

            Assert.AreEqual(new Vector3(2f, 0f, 2f), director.TargetPosition);
        }

        [Test]
        public void WithoutADirector_ATickDoesNothing()
        {
            Assert.DoesNotThrow(() => encounter.Tick(AnyTick));
        }
    }
}
