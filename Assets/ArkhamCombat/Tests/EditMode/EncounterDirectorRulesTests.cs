using System;
using System.Collections.Generic;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Each test here stays red when the one rule it names is taken out of the director.
    public class EncounterDirectorRulesTests
    {
        private const int SlotCount = 4;
        private const float SlotRadius = 4f;
        private const float MaxTokenHoldSeconds = 3f;
        private const float ReacquireCooldownSeconds = 1.5f;
        private const float ReassignIntervalSeconds = 1f;
        private const float FirstTick = 0.1f;
        private const float Tolerance = 0.001f;

        private const int NorthSlot = 0;
        private const int SouthSlot = 2;

        private static readonly Vector3 North = new Vector3(0f, 0f, SlotRadius);
        private static readonly Vector3 East = new Vector3(SlotRadius, 0f, 0f);
        private static readonly Vector3 South = new Vector3(0f, 0f, -SlotRadius);

        private sealed class RecordingSlotValidator : ISlotValidator
        {
            public readonly List<Vector3> PositionsAskedAbout = new List<Vector3>();
            private readonly Vector3 correction;

            public RecordingSlotValidator(Vector3 correction) => this.correction = correction;

            public bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition)
            {
                PositionsAskedAbout.Add(desiredPosition);
                validPosition = desiredPosition + correction;
                return true;
            }
        }

        private static EncounterDirector Director(int maxMeleeTokens = 1, int maxRangedTokens = 1, float maxTokenHoldSeconds = MaxTokenHoldSeconds, ISlotValidator slotValidator = null) =>
            new EncounterDirector(
                new EncounterDirectorSettings(
                    maxMeleeTokens, maxRangedTokens, maxTokenHoldSeconds, ReacquireCooldownSeconds, SlotCount, SlotRadius, ReassignIntervalSeconds),
                slotValidator);

        private static StandingAgent Join(EncounterDirector director, Vector3 position, AttackTokenType tokenType = AttackTokenType.Melee)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, tokenType);
            return agent;
        }

        [Test]
        public void AnAgent_IsSentToTheSlotNearestToIt_NotTheFirstFreeOne()
        {
            EncounterDirector director = Director();
            StandingAgent agentInTheSouth = Join(director, South * 1.5f);

            director.Tick(FirstTick);

            Assert.AreEqual(SouthSlot, director.GetSlot(agentInTheSouth));
        }

        [Test]
        public void TheAgentClosestToTheTarget_ChoosesItsSlotFirst()
        {
            EncounterDirector director = Director();
            StandingAgent fartherNorth = Join(director, North * 2f);
            StandingAgent nearerNorth = Join(director, North * 1.2f);

            director.Tick(FirstTick);

            Assert.AreEqual(NorthSlot, director.GetSlot(nearerNorth), "the nearer agent keeps the slot they both want");
            Assert.AreNotEqual(NorthSlot, director.GetSlot(fartherNorth));
        }

        [Test]
        public void AnAgentTheRingHasNoRoomFor_HoldsItsOwnBearing()
        {
            EncounterDirector director = Director();
            Join(director, North * 1.1f);
            Join(director, East * 1.1f);
            Join(director, South * 1.1f);
            Join(director, -East * 1.1f);
            Vector3 northEast = (North + East).normalized;
            StandingAgent farthestOut = Join(director, northEast * SlotRadius * 3f);

            director.Tick(FirstTick);

            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(farthestOut));
            Assert.Less((director.GetSlotPosition(farthestOut) - northEast * SlotRadius).magnitude, Tolerance, "on the ring's radius, along its own bearing");
        }

        [Test]
        public void AnAgentThatJoinsLater_GetsItsSlotOnTheNextTick_NotAtTheNextInterval()
        {
            EncounterDirector director = Director();
            Join(director, North * 1.5f);
            director.Tick(FirstTick);

            StandingAgent latecomer = Join(director, South * 1.5f);
            director.Tick(ReassignIntervalSeconds / 10f);

            Assert.AreEqual(SouthSlot, director.GetSlot(latecomer));
        }

        [Test]
        public void BetweenAssignments_AnAgentKeepsItsSlot_WhereverItWalks()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North * 1.5f);
            director.Tick(FirstTick);

            agent.Position = South * 1.5f;
            director.Tick(ReassignIntervalSeconds / 2f);
            Assert.AreEqual(NorthSlot, director.GetSlot(agent), "half an interval later");

            director.Tick(ReassignIntervalSeconds / 2f);
            Assert.AreEqual(SouthSlot, director.GetSlot(agent), "once the interval has passed");
        }

        [Test]
        public void SlotsAreValidatedWhereTheTargetStands()
        {
            Vector3 whereTheTargetStands = new Vector3(10f, 0f, -6f);
            Vector3 upOntoALedge = new Vector3(0f, 0.5f, 0f);
            RecordingSlotValidator validator = new RecordingSlotValidator(upOntoALedge);
            EncounterDirector director = Director(slotValidator: validator);
            StandingAgent agent = Join(director, whereTheTargetStands + North * 1.5f);
            director.SetTargetPosition(whereTheTargetStands);

            director.Tick(FirstTick);

            CollectionAssert.Contains(validator.PositionsAskedAbout, whereTheTargetStands + North, "the validator is asked about places in the world");
            Assert.Less((director.GetSlotPosition(agent) - (whereTheTargetStands + North + upOntoALedge)).magnitude, Tolerance);
        }

        [Test]
        public void AToken_IsStillHeld_JustBeforeItsTimeIsUp()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);

            director.Tick(MaxTokenHoldSeconds - 0.1f);

            Assert.IsTrue(director.HasToken(agent));
        }

        [Test]
        public void TheTimeATokenIsHeld_AddsUpOverManyTicks()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);

            director.Tick(MaxTokenHoldSeconds / 3f);
            director.Tick(MaxTokenHoldSeconds / 3f);
            Assert.IsTrue(director.HasToken(agent), "two thirds of the way");

            director.Tick(MaxTokenHoldSeconds / 3f + Tolerance);
            Assert.IsFalse(director.HasToken(agent), "after the whole of it");
        }

        [Test]
        public void WithAHoldTimeOfZero_ATokenIsNeverTakenBack()
        {
            EncounterDirector director = Director(maxTokenHoldSeconds: 0f);
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);

            director.Tick(MaxTokenHoldSeconds * 100f);

            Assert.IsTrue(director.HasToken(agent));
        }

        [Test]
        public void AnAgentThatLostItsToken_IsRefusedWhileNobodyElseHoldsOne()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            director.Tick(MaxTokenHoldSeconds);
            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Melee), "sanity: the pool is free again");

            Assert.IsFalse(director.TryAcquireToken(agent), "only its cooldown can be refusing it");
        }

        [Test]
        public void TheCooldown_IsNotOver_PartOfTheWayThrough()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            director.ReleaseToken(agent);

            director.Tick(ReacquireCooldownSeconds / 2f);
            Assert.IsFalse(director.TryAcquireToken(agent), "halfway through");

            director.Tick(ReacquireCooldownSeconds / 2f + Tolerance);
            Assert.IsTrue(director.TryAcquireToken(agent), "once it has passed");
        }

        [Test]
        public void EachPool_GrantsAsManyTokensAsItsOwnLimit()
        {
            EncounterDirector director = Director(maxMeleeTokens: 2, maxRangedTokens: 0);
            StandingAgent firstBrawler = Join(director, North);
            StandingAgent secondBrawler = Join(director, East);
            StandingAgent thirdBrawler = Join(director, South);
            StandingAgent thrower = Join(director, -East, AttackTokenType.Ranged);

            Assert.IsTrue(director.TryAcquireToken(firstBrawler));
            Assert.IsTrue(director.TryAcquireToken(secondBrawler));
            Assert.IsFalse(director.TryAcquireToken(thirdBrawler), "the melee pool has two");
            Assert.IsFalse(director.TryAcquireToken(thrower), "the ranged pool has none");
        }

        [Test]
        public void AListenerThatTakesTheAgentOutWhenItsTokenIsReleased_SeesItLeaveOnce()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            int timesItLeft = 0;
            director.AgentUnregistered += _ => timesItLeft++;
            director.TokenReleased += director.Unregister;

            director.Unregister(agent);

            Assert.AreEqual(1, timesItLeft);
            Assert.IsFalse(director.IsRegistered(agent));
        }
    }
}
