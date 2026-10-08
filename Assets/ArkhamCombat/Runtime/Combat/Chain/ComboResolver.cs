using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>What the resolver is looking at: the chain position and the attack playing there, if any.</summary>
    public readonly struct ComboSituation
    {
        public readonly ChainNode Node;

        /// <summary>Null while idle at a node.</summary>
        public readonly AttackDefinition Attack;

        public readonly float NormalizedTime;

        /// <summary>Whether the node's own edges may be taken: the combo window is open, or nothing is playing.</summary>
        public readonly bool CanContinueCombo;

        /// <summary>True while any action plays, attack or not. A plain reaction has no evade window and is not idle.</summary>
        public readonly bool IsPlaying;

        public ComboSituation(ChainNode node, AttackDefinition attack, float normalizedTime, bool canContinueCombo, bool isPlaying)
        {
            Node = node;
            Attack = attack;
            NormalizedTime = normalizedTime;
            CanContinueCombo = canContinueCombo;
            IsPlaying = isPlaying;
        }

        public static ComboSituation Idle(ChainNode node) =>
            new ComboSituation(node, null, 0f, canContinueCombo: true, isPlaying: false);

        public bool IsInEvadeWindow => Attack != null && Attack.EvadeWindow.Contains(NormalizedTime);
    }

    /// <summary>What the resolver chose, and which press it spent on it.</summary>
    public readonly struct Resolution
    {
        public static readonly Resolution None = default;

        public readonly ChainNode Destination;
        public readonly Edge Edge;
        public readonly Intent ConsumedIntent;

        /// <summary>True when a global edge matched: the destination replaces the current attack rather than following it.</summary>
        public readonly bool IsInterrupt;

        public Resolution(ChainNode destination, Edge edge, Intent consumedIntent, bool isInterrupt)
        {
            Destination = destination;
            Edge = edge;
            ConsumedIntent = consumedIntent;
            IsInterrupt = isInterrupt;
        }

        public bool HasMatch => Destination != null;
    }

    /// <summary>
    /// Walks the chain to find where a queued press takes it. Exactly one intent is consumed per
    /// resolve, and an unmatched intent stays queued, which is what lets a press during recovery
    /// start the next chain from the root. Reads facts, never writes them.
    /// </summary>
    public sealed class ComboResolver
    {
        private readonly IConditionEvaluator conditions;
        private readonly InterruptKinds interruptKinds;

        public ComboResolver(IConditionEvaluator conditions, InterruptKinds interruptKinds)
        {
            this.conditions = conditions ?? new AlwaysConditionEvaluator();
            this.interruptKinds = interruptKinds ?? throw new ArgumentNullException(nameof(interruptKinds));
        }

        /// <summary>Interrupts are tried first so an evade or a counter wins over the next attack of the combo.</summary>
        public Resolution Resolve(Stance stance, in ComboSituation situation, IntentBuffer intents, CombatFacts facts)
        {
            if (stance == null || intents == null)
            {
                return Resolution.None;
            }

            if (TryTakeInterruptEdge(stance, situation, intents, facts, out Resolution interrupt))
            {
                return interrupt;
            }

            if (TryTakeComboEdge(stance, situation, intents, facts, out Resolution followUp))
            {
                return followUp;
            }

            return Resolution.None;
        }

        /// <summary>
        /// The gate a global edge passes before its condition is even asked. Evade needs the current
        /// attack's evade window, or an idle character; a plain action playing cannot be evaded out of.
        /// Counter needs a counterable attack incoming. Any other kind has no gate of its own.
        /// </summary>
        public bool IsInterruptAllowed(Edge edge, in ComboSituation situation, CombatFacts facts)
        {
            if (edge.IntentKind == interruptKinds.Evade)
            {
                return !situation.IsPlaying || situation.IsInEvadeWindow;
            }

            if (edge.IntentKind == interruptKinds.Counter)
            {
                return facts != null && facts.IsIncomingAttackCounterable;
            }

            return true;
        }

        private bool TryTakeInterruptEdge(Stance stance, in ComboSituation situation, IntentBuffer intents, CombatFacts facts, out Resolution resolution)
        {
            IReadOnlyList<Edge> globalEdges = stance.GlobalEdges;
            for (int i = 0; i < globalEdges.Count; i++)
            {
                Edge edge = globalEdges[i];
                if (edge == null || !IsInterruptAllowed(edge, situation, facts))
                {
                    continue;
                }

                if (TryTakeEdge(stance, edge, intents, facts, isInterrupt: true, out resolution))
                {
                    return true;
                }
            }

            resolution = Resolution.None;
            return false;
        }

        private bool TryTakeComboEdge(Stance stance, in ComboSituation situation, IntentBuffer intents, CombatFacts facts, out Resolution resolution)
        {
            resolution = Resolution.None;
            if (!situation.CanContinueCombo || situation.Node == null)
            {
                return false;
            }

            IReadOnlyList<Edge> edges = situation.Node.Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge != null && TryTakeEdge(stance, edge, intents, facts, isInterrupt: false, out resolution))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Consumes the newest matching press only when the edge leads somewhere and its condition is met.</summary>
        private bool TryTakeEdge(Stance stance, Edge edge, IntentBuffer intents, CombatFacts facts, bool isInterrupt, out Resolution resolution)
        {
            resolution = Resolution.None;

            if (!edge.HasIntentKind)
            {
                return false;
            }

            Intent intent = intents.FindNewest(edge.IntentKind);
            if (intent == null)
            {
                return false;
            }

            if (!stance.TryGetNode(edge.DestinationId, out ChainNode destination))
            {
                return false;
            }

            if (!conditions.IsConditionMet(edge, facts, intent))
            {
                return false;
            }

            intents.TryConsume(intent);
            resolution = new Resolution(destination, edge, intent, isInterrupt);
            return true;
        }
    }
}
