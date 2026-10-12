using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Encounter
{
    public class EncounterDirector
    {
        public const int UnassignedSlot = -1;

        private const float NegligibleSqrMagnitude = 0.001f;

        private sealed class AgentState
        {
            public IEncounterAgent Agent;
            public AttackTokenType TokenType;
            public bool HasToken;
            public float TokenHeldFor;
            public float ReacquireCooldown;
            public int Slot = UnassignedSlot;
        }

        private readonly EncounterDirectorSettings settings;
        private readonly ISlotValidator slotValidator;
        private readonly List<AgentState> agentStates = new List<AgentState>();
        private readonly List<IEncounterAgent> registeredAgents = new List<IEncounterAgent>();
        private readonly Dictionary<IEncounterAgent, AgentState> stateByAgent = new Dictionary<IEncounterAgent, AgentState>();
        private readonly List<AgentState> assignmentOrder = new List<AgentState>();
        private readonly bool[] isSlotTaken;
        private readonly bool[] isSlotUsable;
        private readonly Vector3[] slotOffsetFromTarget;

        private Vector3 targetPosition;
        private float secondsUntilSlotsAreReassigned;

        public EncounterDirector(EncounterDirectorSettings settings, ISlotValidator slotValidator = null)
        {
            this.settings = settings ?? new EncounterDirectorSettings();
            this.slotValidator = slotValidator ?? new EverySlotIsValid();

            int slotCount = this.settings.SlotCount;
            isSlotTaken = new bool[slotCount];
            isSlotUsable = new bool[slotCount];
            slotOffsetFromTarget = new Vector3[slotCount];

            for (int slot = 0; slot < slotCount; slot++)
            {
                isSlotUsable[slot] = true;
                slotOffsetFromTarget[slot] = OffsetOnTheRing(slot);
            }
        }

        public event Action<IEncounterAgent> AgentRegistered;
        public event Action<IEncounterAgent> AgentUnregistered;
        public event Action<IEncounterAgent> TokenGranted;
        public event Action<IEncounterAgent> TokenReleased;

        public EncounterDirectorSettings Settings => settings;
        public Vector3 TargetPosition => targetPosition;
        public IReadOnlyList<IEncounterAgent> Agents => registeredAgents;
        public bool IsActive => agentStates.Count > 0;

        public void SetTargetPosition(Vector3 position)
        {
            targetPosition = position;
        }

        public void Register(IEncounterAgent agent, AttackTokenType tokenType)
        {
            if (agent == null || stateByAgent.ContainsKey(agent))
            {
                return;
            }

            AgentState state = new AgentState { Agent = agent, TokenType = tokenType };
            agentStates.Add(state);
            registeredAgents.Add(agent);
            stateByAgent.Add(agent, state);

            ReassignSlotsOnTheNextTick();
            AgentRegistered?.Invoke(agent);
        }

        public void Unregister(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return;
            }

            Release(state);

            agentStates.Remove(state);
            registeredAgents.Remove(agent);
            stateByAgent.Remove(agent);

            ReassignSlotsOnTheNextTick();
            AgentUnregistered?.Invoke(agent);
        }

        public bool IsRegistered(IEncounterAgent agent)
        {
            return agent != null && stateByAgent.ContainsKey(agent);
        }

        private void ReassignSlotsOnTheNextTick()
        {
            secondsUntilSlotsAreReassigned = 0.0f;
        }

        public bool HasToken(IEncounterAgent agent)
        {
            return agent != null && stateByAgent.TryGetValue(agent, out AgentState state) && state.HasToken;
        }

        public bool TryAcquireToken(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return false;
            }

            if (state.HasToken)
            {
                return true;
            }

            bool isCoolingDown = state.ReacquireCooldown > 0.0f;
            bool isPoolEmpty = HeldTokenCount(state.TokenType) >= MaxTokens(state.TokenType);
            if (isCoolingDown || isPoolEmpty)
            {
                return false;
            }

            state.HasToken = true;
            state.TokenHeldFor = 0.0f;
            TokenGranted?.Invoke(agent);

            return true;
        }

        public void ReleaseToken(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return;
            }

            Release(state);
        }

        public int HeldTokenCount(AttackTokenType tokenType)
        {
            int count = 0;

            for (int i = 0; i < agentStates.Count; i++)
            {
                if (agentStates[i].HasToken && agentStates[i].TokenType == tokenType)
                {
                    count++;
                }
            }

            return count;
        }

        private int MaxTokens(AttackTokenType tokenType)
        {
            return tokenType == AttackTokenType.Ranged ? settings.MaxRangedTokens : settings.MaxMeleeTokens;
        }

        private void Release(AgentState state)
        {
            if (!state.HasToken)
            {
                return;
            }

            state.HasToken = false;
            state.TokenHeldFor = 0.0f;
            state.ReacquireCooldown = settings.ReacquireCooldownSeconds;

            TokenReleased?.Invoke(state.Agent);
        }

        public Vector3 GetSlotPosition(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return targetPosition;
            }

            return state.Slot == UnassignedSlot ? BearingPosition(agent) : SlotWorldPosition(state.Slot);
        }

        public int GetSlot(IEncounterAgent agent)
        {
            return agent != null && stateByAgent.TryGetValue(agent, out AgentState state) ? state.Slot : UnassignedSlot;
        }

        public Vector3 SlotWorldPosition(int slot)
        {
            return targetPosition + slotOffsetFromTarget[slot];
        }

        private Vector3 OffsetOnTheRing(int slot)
        {
            float radians = slot * ((Mathf.PI * 2.0f) / settings.SlotCount);
            Vector3 directionFromTarget = new Vector3(Mathf.Sin(radians), 0.0f, Mathf.Cos(radians));

            return directionFromTarget * settings.SlotRadius;
        }

        // An agent the ring has no room for holds the line here, instead of piling onto a slot someone else has.
        private Vector3 BearingPosition(IEncounterAgent agent)
        {
            Vector3 bearing = agent.Position - targetPosition;
            bearing.y = 0.0f;

            if (bearing.sqrMagnitude < NegligibleSqrMagnitude)
            {
                bearing = Vector3.forward;
            }

            return targetPosition + (bearing.normalized * settings.SlotRadius);
        }

        public void Tick(float deltaTime)
        {
            AgeReacquireCooldowns(deltaTime);
            TakeBackTokensHeldTooLong(deltaTime);
            AssignSlotsWhenDue(deltaTime);
        }

        private void AgeReacquireCooldowns(float deltaTime)
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                if (agentStates[i].ReacquireCooldown > 0.0f)
                {
                    agentStates[i].ReacquireCooldown -= deltaTime;
                }
            }
        }

        private void TakeBackTokensHeldTooLong(float deltaTime)
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                AgentState state = agentStates[i];
                if (!state.HasToken)
                {
                    continue;
                }

                state.TokenHeldFor += deltaTime;

                bool hasHeldItTooLong = settings.MaxTokenHoldSeconds > 0.0f && state.TokenHeldFor >= settings.MaxTokenHoldSeconds;
                if (hasHeldItTooLong)
                {
                    Release(state);
                }
            }
        }

        private void AssignSlotsWhenDue(float deltaTime)
        {
            secondsUntilSlotsAreReassigned -= deltaTime;
            if (secondsUntilSlotsAreReassigned > 0.0f)
            {
                return;
            }

            ValidateSlots();
            AssignSlots();
            secondsUntilSlotsAreReassigned = settings.SlotReassignIntervalSeconds;
        }

        // A slot keeps the place it was given here until the next assignment, carried along with the target.
        private void ValidateSlots()
        {
            for (int slot = 0; slot < isSlotUsable.Length; slot++)
            {
                Vector3 offsetOnTheRing = OffsetOnTheRing(slot);
                isSlotUsable[slot] = slotValidator.TryValidate(targetPosition + offsetOnTheRing, out Vector3 validPosition);
                slotOffsetFromTarget[slot] = isSlotUsable[slot] ? validPosition - targetPosition : offsetOnTheRing;
            }
        }

        /// <summary>
        /// The agents closest to the target choose first. That keeps the front rank still and pushes
        /// the shuffling out to the back, instead of everyone finding a new path at once.
        /// </summary>
        private void AssignSlots()
        {
            for (int i = 0; i < isSlotTaken.Length; i++)
            {
                isSlotTaken[i] = false;
            }

            assignmentOrder.Clear();
            assignmentOrder.AddRange(agentStates);
            assignmentOrder.Sort(CompareByDistanceToTarget);

            for (int i = 0; i < assignmentOrder.Count; i++)
            {
                AgentState state = assignmentOrder[i];
                state.Slot = NearestFreeSlot(state.Agent.Position);

                if (state.Slot != UnassignedSlot)
                {
                    isSlotTaken[state.Slot] = true;
                }
            }
        }

        private int CompareByDistanceToTarget(AgentState a, AgentState b)
        {
            float distanceA = (a.Agent.Position - targetPosition).sqrMagnitude;
            float distanceB = (b.Agent.Position - targetPosition).sqrMagnitude;

            return distanceA.CompareTo(distanceB);
        }

        private int NearestFreeSlot(Vector3 position)
        {
            int nearestSlot = UnassignedSlot;
            float nearestSqrDistance = float.MaxValue;

            for (int slot = 0; slot < isSlotTaken.Length; slot++)
            {
                if (isSlotTaken[slot] || !isSlotUsable[slot])
                {
                    continue;
                }

                float sqrDistance = (SlotWorldPosition(slot) - position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestSlot = slot;
                }
            }

            return nearestSlot;
        }
    }
}
