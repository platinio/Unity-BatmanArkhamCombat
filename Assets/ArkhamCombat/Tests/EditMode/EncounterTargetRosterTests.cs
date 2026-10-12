using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterTargetRosterTests
    {
        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private readonly List<GameObject> agentObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings());
            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject agentObject in agentObjects)
            {
                Object.DestroyImmediate(agentObject);
            }

            agentObjects.Clear();
            Object.DestroyImmediate(encounterObject);
        }

        private EncounterAgent ThugInTheEncounter(string name, out EnemyStatus status)
        {
            GameObject thug = HiddenObject(name);
            agentObjects.Add(thug);
            status = thug.AddComponent<EnemyStatus>();
            return JoinTheEncounter(thug);
        }

        private EncounterAgent BystanderInTheEncounter()
        {
            GameObject bystander = HiddenObject("Bystander");
            agentObjects.Add(bystander);
            return JoinTheEncounter(bystander);
        }

        private EncounterAgent JoinTheEncounter(GameObject agentObject)
        {
            EncounterAgent agent = agentObject.AddComponent<EncounterAgent>();
            agent.Construct(encounter);
            agent.OnEnable();
            return agent;
        }

        [Test]
        public void ListsWhoeverWasAlreadyInTheEncounter()
        {
            ThugInTheEncounter("Early thug", out EnemyStatus status);

            EncounterTargetRoster roster = new EncounterTargetRoster(director);

            CollectionAssert.AreEqual(new ICombatTarget[] { status }, roster.Targets);
        }

        [Test]
        public void ListsAThugThatJoinsLater()
        {
            EncounterTargetRoster roster = new EncounterTargetRoster(director);
            Assert.IsEmpty(roster.Targets, "sanity: nobody has joined yet");

            ThugInTheEncounter("Late thug", out EnemyStatus status);

            CollectionAssert.AreEqual(new ICombatTarget[] { status }, roster.Targets);
        }

        [Test]
        public void AThugThatLeavesTheEncounter_IsNoLongerATarget()
        {
            EncounterAgent firstAgent = ThugInTheEncounter("First thug", out EnemyStatus _);
            ThugInTheEncounter("Second thug", out EnemyStatus secondStatus);
            EncounterTargetRoster roster = new EncounterTargetRoster(director);

            firstAgent.OnDisable();

            CollectionAssert.AreEqual(new ICombatTarget[] { secondStatus }, roster.Targets);
        }

        [Test]
        public void AnAgentLivingOnNoObject_IsNotATarget()
        {
            EncounterTargetRoster roster = new EncounterTargetRoster(director);

            director.Register(new StandingAgent(), AttackTokenType.Melee);

            Assert.IsEmpty(roster.Targets);
        }

        [Test]
        public void AnAgentWithNothingOnItToHit_IsNotATarget()
        {
            EncounterTargetRoster roster = new EncounterTargetRoster(director);

            EncounterAgent bystander = BystanderInTheEncounter();

            Assert.IsEmpty(roster.Targets);
            Assert.DoesNotThrow(() => bystander.OnDisable(), "leaving must not trip over an agent the roster never listed");
        }
    }
}
