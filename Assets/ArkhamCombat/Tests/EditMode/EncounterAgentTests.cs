using System.Text.RegularExpressions;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode never calls OnEnable or OnDisable on these components, so each test calls them by hand.
    public class EncounterAgentTests
    {
        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject target;
        private GameObject thug;
        private EncounterAgent agent;
        private GameObject otherThug;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 1));

            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);

            target = HiddenObject("Target");
            encounter.SetTarget(target.transform);

            thug = HiddenObject("Thug");
            agent = thug.AddComponent<EncounterAgent>();

            otherThug = HiddenObject("Other thug");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(otherThug);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(encounterObject);
        }

        private void JoinTheEncounter()
        {
            agent.Construct(encounter);
            agent.OnEnable();
        }

        private EncounterAgent AnotherAgentInTheEncounter()
        {
            EncounterAgent otherAgent = otherThug.AddComponent<EncounterAgent>();
            otherAgent.Construct(encounter);
            otherAgent.OnEnable();
            return otherAgent;
        }

        private void WakeInASceneWithNoEncounter()
        {
            LogAssert.Expect(LogType.Warning, new Regex("found no encounter director"));
            agent.Construct(null);
            agent.OnEnable();
        }

        [Test]
        public void EnablingIt_JoinsTheEncounter()
        {
            JoinTheEncounter();

            Assert.IsTrue(director.IsRegistered(agent));
        }

        [Test]
        public void DisablingIt_LeavesTheEncounter()
        {
            JoinTheEncounter();

            agent.OnDisable();

            Assert.IsFalse(director.IsRegistered(agent));
        }

        [Test]
        public void DisablingItWhileItHoldsTheToken_FreesTheTurnForSomeoneElse()
        {
            JoinTheEncounter();
            EncounterAgent waitingAgent = AnotherAgentInTheEncounter();
            Assert.IsTrue(agent.TryAcquireToken(), "sanity: the first to ask gets the turn");
            Assert.IsFalse(waitingAgent.TryAcquireToken(), "sanity: one token, already taken");

            agent.OnDisable();

            Assert.IsTrue(waitingAgent.TryAcquireToken());
        }

        [Test]
        public void ReleasingItsToken_GivesTheTurnBack()
        {
            JoinTheEncounter();
            agent.TryAcquireToken();

            agent.ReleaseToken();

            Assert.IsFalse(agent.HasToken);
            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Melee));
        }

        [Test]
        public void ItsTarget_IsWhatTheEncounterRevolvesAround()
        {
            JoinTheEncounter();

            Assert.AreSame(target.transform, agent.Target);
        }

        [Test]
        public void OnceItHasLeft_ItHasNoTarget()
        {
            JoinTheEncounter();

            agent.OnDisable();

            Assert.IsNull(agent.Target);
        }

        [Test]
        public void ItsSlot_IsTheOneTheDirectorAssigned()
        {
            thug.transform.position = new Vector3(0f, 0f, 9f);
            JoinTheEncounter();
            director.SetTargetPosition(Vector3.zero);
            director.Tick(0.1f);

            Assert.AreEqual(director.GetSlotPosition(agent), agent.SlotPosition);
            Assert.AreNotEqual(thug.transform.position, agent.SlotPosition, "sanity: the slot is on the ring, not where the thug stands");
        }

        [Test]
        public void InASceneWithNoEncounter_ItIsNeverGrantedAToken()
        {
            WakeInASceneWithNoEncounter();

            Assert.IsFalse(agent.TryAcquireToken());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void InASceneWithNoEncounter_ItsSlotIsWhereItStands()
        {
            thug.transform.position = new Vector3(3f, 0f, -2f);

            WakeInASceneWithNoEncounter();

            Assert.AreEqual(thug.transform.position, agent.SlotPosition);
            Assert.IsNull(agent.Target);
        }

        [Test]
        public void InASceneWithNoEncounter_LeavingAndReleasingDoNothing()
        {
            WakeInASceneWithNoEncounter();

            Assert.DoesNotThrow(() => agent.ReleaseToken());
            Assert.DoesNotThrow(() => agent.OnDisable());
        }
    }
}
