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
            director = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 1, maxRangedTokens: 1));

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

        private sealed class SettingsOfARangedAttacker : IEncounterAgentSettings
        {
            public AttackTokenType TokenType => AttackTokenType.Ranged;
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
            Assert.IsTrue(TakeAToken(director, agent), "sanity: the first to ask gets the turn");
            Assert.IsFalse(TakeAToken(director, waitingAgent), "sanity: one token, already taken");

            agent.OnDisable();

            Assert.IsTrue(TakeAToken(director, waitingAgent));
        }

        [Test]
        public void ReleasingItsToken_GivesTheTurnBack()
        {
            JoinTheEncounter();
            Assert.IsTrue(TakeAToken(director, agent), "sanity: the melee token is free");

            agent.ReleaseToken();

            Assert.IsFalse(agent.HasToken);
            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Melee));
        }

        [Test]
        public void WithNoSettings_ItWaitsForAMeleeToken()
        {
            JoinTheEncounter();

            Assert.IsTrue(TakeAToken(director, agent));

            Assert.AreEqual(1, director.HeldTokenCount(AttackTokenType.Melee));
            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Ranged));
        }

        [Test]
        public void ItWaitsForTheKindOfTokenItsSettingsName()
        {
            agent.Construct(encounter, new SettingsOfARangedAttacker());
            agent.OnEnable();

            Assert.IsTrue(TakeAToken(director, agent));

            Assert.AreEqual(1, director.HeldTokenCount(AttackTokenType.Ranged));
            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Melee));
        }

        [Test]
        public void WhoeverHearsItJoin_CanAlreadyAskItForItsTarget()
        {
            Transform targetWhenAnnounced = null;
            director.AgentRegistered += _ => targetWhenAnnounced = agent.Target;

            JoinTheEncounter();

            Assert.AreSame(target.transform, targetWhenAnnounced);
        }

        [Test]
        public void ItsTelegraph_ReachesTheDirector()
        {
            JoinTheEncounter();

            agent.TelegraphAnAttack(isCounterable: true);

            Assert.IsTrue(director.IsTelegraphing(agent));
            Assert.IsTrue(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void ItsTelegraph_IsWithdrawnFromTheDirector_WhenItStops()
        {
            JoinTheEncounter();
            agent.TelegraphAnAttack(isCounterable: true);

            agent.StopTelegraphing();

            Assert.IsFalse(director.IsTelegraphing(agent));
        }

        [Test]
        public void OutsideAnEncounter_ItsTelegraphGoesNowhere()
        {
            WakeInASceneWithNoEncounter();

            Assert.DoesNotThrow(() => agent.TelegraphAnAttack(isCounterable: true));
            Assert.DoesNotThrow(() => agent.StopTelegraphing());
        }

        [Test]
        public void ItAnnouncesItsOwnToken_AndNobodyElses()
        {
            EncounterDirector twoTokens = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 2));
            encounter.Construct(twoTokens);
            JoinTheEncounter();
            EncounterAgent otherAgent = AnotherAgentInTheEncounter();
            int granted = 0;
            int released = 0;
            agent.TokenGranted += () => granted++;
            agent.TokenReleased += () => released++;

            TakeAToken(twoTokens, otherAgent);
            otherAgent.ReleaseToken();
            Assert.AreEqual(0, granted + released, "another agent's token");

            TakeAToken(twoTokens, agent);
            agent.ReleaseToken();
            Assert.AreEqual(1, granted);
            Assert.AreEqual(1, released);
        }

        [Test]
        public void JoiningAgainAfterLeaving_ItAnnouncesItsTokenOnce()
        {
            JoinTheEncounter();
            agent.OnDisable();
            agent.OnEnable();
            int granted = 0;
            agent.TokenGranted += () => granted++;

            TakeAToken(director, agent);

            Assert.AreEqual(1, granted);
        }

        [Test]
        public void WithAHostThatWasGivenNoDirector_ItWarns_AndJoinsNothing()
        {
            GameObject hostWithoutADirector = HiddenObject("Host without a director");
            LogAssert.Expect(LogType.Warning, new Regex("found no encounter director"));

            try
            {
                agent.Construct(hostWithoutADirector.AddComponent<EncounterDirectorBehaviour>());
                agent.OnEnable();

                Assert.IsFalse(agent.TryAcquireToken());
                Assert.IsNull(agent.Target);
            }
            finally
            {
                Object.DestroyImmediate(hostWithoutADirector);
            }
        }

        [Test]
        public void NeverInjected_ItSaysThat_RatherThanBlamingTheScene()
        {
            LogAssert.Expect(LogType.Warning, new Regex("was never injected"));

            agent.OnEnable();

            Assert.IsFalse(agent.TryAcquireToken());
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
            director.SetTarget(Vector3.zero, Vector3.forward);
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
