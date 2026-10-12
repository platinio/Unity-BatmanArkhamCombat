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
            public bool HasComeBackForItsToken;
            public bool IsAskingForAToken;
            public bool IsTelegraphing;
            public bool IsTelegraphingACounterableAttack;
            public float TokenHeldFor;
            public float SecondsWithoutAToken;
            public float ReacquireCooldown;
            public int AttacksLeftInItsTurn;
            public int Slot = UnassignedSlot;
        }

        private readonly EncounterDirectorSettings settings;
        private readonly ISlotValidator slotValidator;
        private readonly TokenRequestScorer tokenRequestScorer;
        private readonly System.Random random;
        private readonly int slotCount;
        private readonly List<AgentState> agentStates = new List<AgentState>();
        private readonly List<IEncounterAgent> registeredAgents = new List<IEncounterAgent>();
        private readonly Dictionary<IEncounterAgent, AgentState> stateByAgent = new Dictionary<IEncounterAgent, AgentState>();
        private readonly List<AgentState> assignmentOrder = new List<AgentState>();
        private readonly bool[] isSlotTaken;
        private readonly bool[] isSlotUsable;
        private readonly Vector3[] slotOffsetFromTarget;

        private readonly Dictionary<AttackTokenType, float> secondsUntilTheNextTurnByTokenType = new Dictionary<AttackTokenType, float>();

        private Vector3 targetPosition;
        private Vector3 targetForward;
        private float secondsUntilSlotsAreReassigned;

        public EncounterDirector(EncounterDirectorSettings settings, ISlotValidator slotValidator = null, System.Random random = null)
        {
            this.settings = settings ?? new EncounterDirectorSettings();
            this.slotValidator = slotValidator ?? new EverySlotIsValid();
            this.random = random ?? new System.Random();
            tokenRequestScorer = new TokenRequestScorer(this.settings);

            slotCount = this.settings.SlotCount;
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
        public Vector3 TargetForward => targetForward;
        public IReadOnlyList<IEncounterAgent> Agents => registeredAgents;
        public bool IsActive => agentStates.Count > 0;

        public bool IsACounterableAttackIncoming
        {
            get
            {
                for (int i = 0; i < agentStates.Count; i++)
                {
                    if (agentStates[i].IsTelegraphingACounterableAttack)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void SetTarget(Vector3 position, Vector3 forward)
        {
            targetPosition = position;
            targetForward = forward;
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

            bool wasTakenOutByAListenerToTheRelease = !stateByAgent.ContainsKey(agent);
            if (wasTakenOutByAListenerToTheRelease)
            {
                return;
            }

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

        /// <summary>
        /// True only while the agent holds a token, and asking while holding is what keeps a granted
        /// token past the grace time. From anyone else it is an ask, remembered until the tick ends,
        /// when the free tokens go to the best of those who asked: an agent that still wants the turn
        /// asks again.
        /// </summary>
        public bool TryAcquireToken(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return false;
            }

            if (state.HasToken)
            {
                state.HasComeBackForItsToken = true;
                return true;
            }

            state.IsAskingForAToken = true;
            return false;
        }

        public void ReleaseToken(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state))
            {
                return;
            }

            Release(state);
        }

        public int AttacksLeftInTheTurnOf(IEncounterAgent agent)
        {
            return agent != null && stateByAgent.TryGetValue(agent, out AgentState state) ? state.AttacksLeftInItsTurn : 0;
        }

        /// <summary>
        /// A turn ends with its last attack: the token then goes back without the agent giving it.
        /// </summary>
        public void SpendAnAttackOfTheTurn(IEncounterAgent agent)
        {
            if (agent == null || !stateByAgent.TryGetValue(agent, out AgentState state) || !state.HasToken)
            {
                return;
            }

            state.AttacksLeftInItsTurn--;

            bool wasTheLastAttackOfTheTurn = state.AttacksLeftInItsTurn <= 0;
            if (wasTheLastAttackOfTheTurn)
            {
                Release(state);
            }
        }

        public void TelegraphAnAttack(IEncounterAgent agent, bool isCounterable)
        {
            if (agent != null && stateByAgent.TryGetValue(agent, out AgentState state))
            {
                state.IsTelegraphing = true;
                state.IsTelegraphingACounterableAttack = isCounterable;
            }
        }

        public void StopTelegraphing(IEncounterAgent agent)
        {
            if (agent != null && stateByAgent.TryGetValue(agent, out AgentState state))
            {
                state.IsTelegraphing = false;
                state.IsTelegraphingACounterableAttack = false;
            }
        }

        public bool IsTelegraphing(IEncounterAgent agent)
        {
            return agent != null && stateByAgent.TryGetValue(agent, out AgentState state) && state.IsTelegraphing;
        }

        public bool TryGetNearestTelegraphingAgent(Vector3 position, out IEncounterAgent nearestAgent) =>
            TryGetNearestAgent(position, state => state.IsTelegraphing, out nearestAgent);

        // A telegraph ends before the swing lands, so once nobody winds up the attacker is whoever has the turn.
        public bool TryGetNearestAttacker(Vector3 position, out IEncounterAgent nearestAttacker) =>
            TryGetNearestTelegraphingAgent(position, out nearestAttacker)
            || TryGetNearestAgent(position, state => state.HasToken, out nearestAttacker);

        public bool TryGetNearestAgentTelegraphingACounterableAttack(Vector3 position, out IEncounterAgent nearestAgent) =>
            TryGetNearestAgent(position, state => state.IsTelegraphingACounterableAttack, out nearestAgent);

        private bool TryGetNearestAgent(Vector3 position, Func<AgentState, bool> isWhoIsAskedFor, out IEncounterAgent nearestAgent)
        {
            nearestAgent = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < agentStates.Count; i++)
            {
                AgentState state = agentStates[i];
                if (!isWhoIsAskedFor(state))
                {
                    continue;
                }

                float distance = DistanceOnTheGround(state.Agent.Position, position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestAgent = state.Agent;
                }
            }

            return nearestAgent != null;
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
            state.AttacksLeftInItsTurn = 0;
            state.TokenHeldFor = 0.0f;
            state.SecondsWithoutAToken = 0.0f;
            state.ReacquireCooldown = settings.ReacquireCooldownSeconds;
            secondsUntilTheNextTurnByTokenType[state.TokenType] = settings.SecondsBetweenTurns;

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

        /// <summary>
        /// An agent nearer the target than the ring holds its ground: one that has walked in to
        /// attack does not walk back out to wait for its next turn.
        /// </summary>
        public Vector3 GetStandingPosition(IEncounterAgent agent)
        {
            bool isInTheEncounter = agent != null && stateByAgent.ContainsKey(agent);
            return isInTheEncounter && IsInsideTheRing(agent) ? agent.Position : GetSlotPosition(agent);
        }

        private bool IsInsideTheRing(IEncounterAgent agent) =>
            DistanceOnTheGround(agent.Position, targetPosition) < settings.SlotRadius;

        public Vector3 GetFreePositionCloserToTarget(IEncounterAgent agent, float distanceFromTarget, float roomEachAgentKeeps)
        {
            if (!IsRegistered(agent) || IsAlreadyThisCloseToTheTarget(agent, distanceFromTarget))
            {
                return GetStandingPosition(agent);
            }

            Vector3 positionCloserToTarget = PositionOnTheBearingOf(agent, distanceFromTarget);
            bool canStandThere = slotValidator.TryValidate(positionCloserToTarget, out Vector3 walkablePosition)
                                 && !IsAnotherAgentStandingWithin(roomEachAgentKeeps, walkablePosition, agent);

            return canStandThere ? walkablePosition : GetStandingPosition(agent);
        }

        private bool IsAlreadyThisCloseToTheTarget(IEncounterAgent agent, float distanceFromTarget) =>
            DistanceOnTheGround(agent.Position, targetPosition) <= distanceFromTarget;

        private bool IsAnotherAgentStandingWithin(float metres, Vector3 position, IEncounterAgent agentThatWouldStandThere)
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                IEncounterAgent otherAgent = agentStates[i].Agent;
                bool isAnotherAgent = !ReferenceEquals(otherAgent, agentThatWouldStandThere);

                if (isAnotherAgent && DistanceOnTheGround(otherAgent.Position, position) < metres)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistanceOnTheGround(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0.0f;

            return offset.magnitude;
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
            float radians = slot * ((Mathf.PI * 2.0f) / slotCount);
            Vector3 directionFromTarget = new Vector3(Mathf.Sin(radians), 0.0f, Mathf.Cos(radians));

            return directionFromTarget * settings.SlotRadius;
        }

        private Vector3 BearingPosition(IEncounterAgent agent) => PositionOnTheBearingOf(agent, settings.SlotRadius);

        private Vector3 PositionOnTheBearingOf(IEncounterAgent agent, float distanceFromTarget)
        {
            Vector3 bearing = agent.Position - targetPosition;
            bearing.y = 0.0f;

            if (bearing.sqrMagnitude < NegligibleSqrMagnitude)
            {
                bearing = Vector3.forward;
            }

            return targetPosition + (bearing.normalized * distanceFromTarget);
        }

        public void Tick(float deltaTime)
        {
            AgeTheBreathBetweenTurns(deltaTime);
            AgeReacquireCooldowns(deltaTime);
            AgeHeldTokens(deltaTime);
            AgeTheWaitsForAToken(deltaTime);
            TakeBackTokensHeldTooLong();
            TakeBackTokensNobodyCameBackFor();
            GrantFreeTokensToTheBestRequests();
            ForgetEveryRequest();
            AssignSlotsWhenDue(deltaTime);
        }

        private void AgeTheBreathBetweenTurns(float deltaTime)
        {
            AgeTheBreathBefore(AttackTokenType.Melee, deltaTime);
            AgeTheBreathBefore(AttackTokenType.Ranged, deltaTime);
        }

        private void AgeTheBreathBefore(AttackTokenType tokenType, float deltaTime)
        {
            if (IsTakingABreathBefore(tokenType))
            {
                secondsUntilTheNextTurnByTokenType[tokenType] -= deltaTime;
            }
        }

        private bool IsTakingABreathBefore(AttackTokenType tokenType) =>
            secondsUntilTheNextTurnByTokenType.TryGetValue(tokenType, out float secondsLeft) && secondsLeft > 0.0f;

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

        private void AgeHeldTokens(float deltaTime)
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                if (agentStates[i].HasToken)
                {
                    agentStates[i].TokenHeldFor += deltaTime;
                }
            }
        }

        private void AgeTheWaitsForAToken(float deltaTime)
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                if (!agentStates[i].HasToken)
                {
                    agentStates[i].SecondsWithoutAToken += deltaTime;
                }
            }
        }

        private void TakeBackTokensHeldTooLong()
        {
            bool canATokenBeHeldTooLong = settings.MaxTokenHoldSeconds > 0.0f;

            for (int i = 0; i < agentStates.Count; i++)
            {
                AgentState state = agentStates[i];

                bool hasHeldItTooLong = canATokenBeHeldTooLong && state.HasToken && state.TokenHeldFor >= settings.MaxTokenHoldSeconds;
                if (hasHeldItTooLong)
                {
                    Release(state);
                }
            }
        }

        private int AttacksATurnIsWorth()
        {
            int oneMoreThanTheMost = settings.MaxAttacksPerTurn + 1;
            return random.Next(settings.MinAttacksPerTurn, oneMoreThanTheMost);
        }

        // An agent that asked and then turned to something else would otherwise hold a turn it never took until the hold timeout.
        private void TakeBackTokensNobodyCameBackFor()
        {
            bool isThereAGraceTime = settings.GrantGraceSeconds > 0.0f;

            for (int i = 0; i < agentStates.Count; i++)
            {
                AgentState state = agentStates[i];

                bool hasNotComeBackInTime = isThereAGraceTime
                                            && state.HasToken
                                            && !state.HasComeBackForItsToken
                                            && state.TokenHeldFor >= settings.GrantGraceSeconds;
                if (hasNotComeBackInTime)
                {
                    Release(state);
                }
            }
        }

        private void GrantFreeTokensToTheBestRequests()
        {
            GrantFreeTokensOf(AttackTokenType.Melee);
            GrantFreeTokensOf(AttackTokenType.Ranged);
        }

        private void GrantFreeTokensOf(AttackTokenType tokenType)
        {
            if (IsTakingABreathBefore(tokenType))
            {
                return;
            }

            int freeTokens = MaxTokens(tokenType) - HeldTokenCount(tokenType);

            for (int i = 0; i < freeTokens; i++)
            {
                AgentState bestRequest = BestRequestFor(tokenType);
                if (bestRequest == null)
                {
                    return;
                }

                Grant(bestRequest);
            }
        }

        private AgentState BestRequestFor(AttackTokenType tokenType)
        {
            AgentState bestRequest = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < agentStates.Count; i++)
            {
                AgentState state = agentStates[i];
                if (!CanBeGrantedAToken(state, tokenType))
                {
                    continue;
                }

                float score = tokenRequestScorer.Score(state.Agent.Position, state.SecondsWithoutAToken, targetPosition, targetForward);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRequest = state;
                }
            }

            return bestRequest;
        }

        private static bool CanBeGrantedAToken(AgentState state, AttackTokenType tokenType)
        {
            bool isCoolingDown = state.ReacquireCooldown > 0.0f;
            return state.IsAskingForAToken && !state.HasToken && !isCoolingDown && state.TokenType == tokenType;
        }

        private void Grant(AgentState state)
        {
            state.HasToken = true;
            state.HasComeBackForItsToken = false;
            state.IsAskingForAToken = false;
            state.TokenHeldFor = 0.0f;
            state.AttacksLeftInItsTurn = AttacksATurnIsWorth();

            TokenGranted?.Invoke(state.Agent);
        }

        private void ForgetEveryRequest()
        {
            for (int i = 0; i < agentStates.Count; i++)
            {
                agentStates[i].IsAskingForAToken = false;
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

        private void ValidateSlots()
        {
            for (int slot = 0; slot < isSlotUsable.Length; slot++)
            {
                Vector3 offsetOnTheRing = OffsetOnTheRing(slot);
                isSlotUsable[slot] = slotValidator.TryValidate(targetPosition + offsetOnTheRing, out Vector3 validPosition);
                slotOffsetFromTarget[slot] = isSlotUsable[slot] ? validPosition - targetPosition : offsetOnTheRing;
            }
        }

        // Closest first keeps the front rank still and pushes the shuffling out to the back, instead of everyone finding a new path at once.
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
            float sqrDistanceA = (a.Agent.Position - targetPosition).sqrMagnitude;
            float sqrDistanceB = (b.Agent.Position - targetPosition).sqrMagnitude;

            return sqrDistanceA.CompareTo(sqrDistanceB);
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
