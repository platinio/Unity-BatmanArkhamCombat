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
        public void EachTick_PassesItsTimeToTheDirector()
        {
            encounter.Construct(director);
            StandingAgent agent = new StandingAgent();
            director.Register(agent, AttackTokenType.Melee);
            Assert.IsTrue(director.TryAcquireToken(agent), "sanity: it holds the token before time passes");

            encounter.Tick(MaxTokenHoldSeconds);

            Assert.IsFalse(director.HasToken(agent), "the token was held for the whole of the time that passed");
        }

        [Test]
        public void WithoutATarget_TheRingStaysWhereItWas()
        {
            encounter.Construct(director);
            director.SetTargetPosition(new Vector3(2f, 0f, 2f));

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
