using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>What the resolver is looking at: the chain position and the attack playing there, if any.</summary>
    public readonly struct ResolveInput
    {
        public readonly ChainNode Node;

        /// <summary>Null while idle at a node.</summary>
        public readonly AttackDefinition Attack;

        public readonly float NormalizedTime;

        /// <summary>Whether the node's own edges may be taken: the cancel window is open, or nothing is playing.</summary>
        public readonly bool FollowUpsOpen;

        /// <summary>True while any action plays, attack or not. A plain reaction has no evade window and is not idle.</summary>
        public readonly bool IsPlaying;

        public ResolveInput(ChainNode node, AttackDefinition attack, float normalizedTime, bool followUpsOpen, bool isPlaying)
        {
            Node = node;
            Attack = attack;
            NormalizedTime = normalizedTime;
            FollowUpsOpen = followUpsOpen;
            IsPlaying = isPlaying;
        }

        public static ResolveInput Idle(ChainNode node) => new ResolveInput(node, null, 0f, true, false);
    }

    /// <summary>What the resolver chose, and which press it spent on it.</summary>
    public readonly struct Resolution
    {
        public static readonly Resolution None = default;

        public readonly ChainNode Destination;
        public readonly Edge Edge;
        public readonly Intent Consumed;

        /// <summary>True when a global edge matched: the destination replaces the current attack rather than following it.</summary>
        public readonly bool IsInterrupt;

        public Resolution(ChainNode destination, Edge edge, Intent consumed, bool isInterrupt)
        {
            Destination = destination;
            Edge = edge;
            Consumed = consumed;
            IsInterrupt = isInterrupt;
        }

        public bool Matched => Destination != null;
    }

    /// <summary>
    /// The chain walk. Global interrupt edges first, in priority order, whenever their own gate
    /// allows; then the node's edges, only while follow-ups are open. Exactly one intent is consumed
    /// per resolve, and an unmatched intent stays queued, which is what lets a press during recovery
    /// start the next chain from the root. Reads facts, never writes them.
    /// </summary>
    public sealed class ComboResolver
    {
        private readonly IConditionEvaluator conditions;

        public ComboResolver(IConditionEvaluator conditions)
        {
            this.conditions = conditions ?? new AlwaysConditionEvaluator();
        }

        public Resolution Resolve(Stance stance, in ResolveInput input, IntentBuffer intents, CombatContext context)
        {
            if (stance == null || intents == null)
            {
                return Resolution.None;
            }

            IReadOnlyList<Edge> globals = stance.GlobalEdges;
            for (int i = 0; i < globals.Count; i++)
            {
                Edge edge = globals[i];
                if (edge == null || !InterruptAllowed(edge, input, context))
                {
                    continue;
                }

                if (TryTake(stance, edge, intents, context, out Resolution resolution, isInterrupt: true))
                {
                    return resolution;
                }
            }

            if (!input.FollowUpsOpen || input.Node == null)
            {
                return Resolution.None;
            }

            IReadOnlyList<Edge> edges = input.Node.Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge != null && TryTake(stance, edge, intents, context, out Resolution resolution, isInterrupt: false))
                {
                    return resolution;
                }
            }

            return Resolution.None;
        }

        private bool TryTake(Stance stance, Edge edge, IntentBuffer intents, CombatContext context, out Resolution resolution, bool isInterrupt)
        {
            resolution = Resolution.None;

            Intent intent = intents.PeekNewest(edge.Intent);
            if (intent == null)
            {
                return false;
            }

            if (!stance.TryGetNode(edge.Destination, out ChainNode destination))
            {
                return false;
            }

            if (!conditions.Evaluate(edge, context, intent))
            {
                return false;
            }

            intents.Consume(intent);
            resolution = new Resolution(destination, edge, intent, isInterrupt);
            return true;
        }

        /// <summary>
        /// The gate a global edge passes before its condition is even asked. Evade needs the current
        /// attack's evade window, or an idle character; a plain action playing cannot be evaded out of.
        /// Counter needs a counterable attack incoming;
        /// Stun and a global Strike have no gate of their own.
        /// </summary>
        public static bool InterruptAllowed(Edge edge, in ResolveInput input, CombatContext context)
        {
            switch (edge.Intent)
            {
                case IntentKind.Evade:
                    return !input.IsPlaying
                           || (input.Attack != null && input.Attack.CancelEvade.Contains(input.NormalizedTime));
                case IntentKind.Counter:
                    return context != null && context.IncomingAttackCounterable;
                default:
                    return true;
            }
        }
    }
}
