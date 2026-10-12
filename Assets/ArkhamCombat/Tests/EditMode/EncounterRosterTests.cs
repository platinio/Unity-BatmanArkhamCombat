using System.Collections.Generic;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterRosterTests
    {
        private const float MaxTokenHoldSeconds = 3f;

        private EncounterDirector director;
        private StandingAgent first;
        private StandingAgent second;
        private List<IEncounterAgent> registered;
        private List<IEncounterAgent> unregistered;
        private List<IEncounterAgent> granted;
        private List<IEncounterAgent> released;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 1, maxTokenHoldSeconds: MaxTokenHoldSeconds));
            first = new StandingAgent();
            second = new StandingAgent();

            registered = new List<IEncounterAgent>();
            unregistered = new List<IEncounterAgent>();
            granted = new List<IEncounterAgent>();
            released = new List<IEncounterAgent>();
            director.AgentRegistered += registered.Add;
            director.AgentUnregistered += unregistered.Add;
            director.TokenGranted += granted.Add;
            director.TokenReleased += released.Add;
        }

        private void RegisterBoth()
        {
            director.Register(first, AttackTokenType.Melee);
            director.Register(second, AttackTokenType.Melee);
        }

        [Test]
        public void TheRoster_ListsTheAgentsInTheOrderTheyJoined()
        {
            RegisterBoth();

            CollectionAssert.AreEqual(new IEncounterAgent[] { first, second }, director.Agents);
        }

        [Test]
        public void AnAgentThatLeaves_IsNoLongerOnTheRoster()
        {
            RegisterBoth();

            director.Unregister(first);

            CollectionAssert.AreEqual(new IEncounterAgent[] { second }, director.Agents);
        }

        [Test]
        public void TheEncounterIsActive_OnlyWhileSomeoneIsInIt()
        {
            Assert.IsFalse(director.IsActive, "before anyone joined");

            director.Register(first, AttackTokenType.Melee);
            Assert.IsTrue(director.IsActive, "with one agent");

            director.Unregister(first);
            Assert.IsFalse(director.IsActive, "after the last one left");
        }

        [Test]
        public void Joining_IsAnnouncedOncePerAgent()
        {
            director.Register(first, AttackTokenType.Melee);
            director.Register(first, AttackTokenType.Melee);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, registered);
        }

        [Test]
        public void Leaving_IsAnnounced_AndOnlyForAnAgentThatWasIn()
        {
            director.Register(first, AttackTokenType.Melee);

            director.Unregister(first);
            director.Unregister(second);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, unregistered);
        }

        [Test]
        public void AGrantedToken_IsAnnouncedOnce_HoweverOftenTheHolderAsksAgain()
        {
            RegisterBoth();

            director.TryAcquireToken(first);
            director.TryAcquireToken(first);
            director.TryAcquireToken(second);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, granted, "the second agent was refused, so nothing was granted to it");
        }

        [Test]
        public void AReleasedToken_IsAnnounced()
        {
            RegisterBoth();
            director.TryAcquireToken(first);

            director.ReleaseToken(first);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, released);
        }

        [Test]
        public void ReleasingWithoutAToken_AnnouncesNothing()
        {
            RegisterBoth();

            director.ReleaseToken(first);

            CollectionAssert.IsEmpty(released);
        }

        [Test]
        public void ATokenTakenBackForBeingHeldTooLong_IsAnnouncedAsReleased()
        {
            RegisterBoth();
            director.TryAcquireToken(first);

            director.Tick(MaxTokenHoldSeconds);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, released);
        }

        [Test]
        public void AHolderThatLeaves_ReleasesItsTokenBeforeItIsGone()
        {
            RegisterBoth();
            director.TryAcquireToken(first);
            bool wasStillInTheEncounterWhenReleased = false;
            director.TokenReleased += agent => wasStillInTheEncounterWhenReleased = director.IsRegistered(agent);

            director.Unregister(first);

            CollectionAssert.AreEqual(new IEncounterAgent[] { first }, released);
            Assert.IsTrue(wasStillInTheEncounterWhenReleased, "a listener can still ask the director about the agent");
        }

        [Test]
        public void AnAgentWithoutAToken_LeavesWithoutAnnouncingARelease()
        {
            RegisterBoth();

            director.Unregister(second);

            CollectionAssert.IsEmpty(released);
        }
    }
}
