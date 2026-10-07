using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Answers edge conditions by invoking their Functions against the player's agent variables. An
    /// empty condition is always true. The per-intent stick angle is published right before the
    /// call, since it belongs to the press being resolved rather than to the frame. A Function that
    /// cannot run is reported once and treated as false, so a broken graph fails closed.
    /// </summary>
    public sealed class FunctionConditionEvaluator : IConditionEvaluator
    {
        private readonly GameObject agent;
        private readonly CombatContextPublisher publisher;
        private readonly HashSet<Edge> reported = new HashSet<Edge>();

        public FunctionConditionEvaluator(CharacterMotor motor, CombatContextPublisher publisher, Stance stance)
        {
            agent = motor.gameObject;
            this.publisher = publisher;

            if (stance != null)
            {
                ReportMismatches(stance);
            }
        }

        public bool Evaluate(Edge edge, CombatContext context, Intent intent)
        {
            if (!edge.HasCondition)
            {
                return true;
            }

            publisher.PublishStickAngle(intent.MoveAtPress);

            FunctionCall<bool>.Bound bound = edge.Condition.For(agent, edge.ToString());
            if (bound.TryInvoke(out bool result, out string error))
            {
                return result;
            }

            if (reported.Add(edge))
            {
                Debug.LogError($"[Combat] Edge {edge}: condition could not run: {error}");
            }

            return false;
        }

        /// <summary>The load-time check: every authored condition must be a Function whose Result is a bool.</summary>
        private static void ReportMismatches(Stance stance)
        {
            foreach (ChainNode node in stance.Nodes)
            {
                ReportMismatches(stance, node.Edges);
            }

            ReportMismatches(stance, stance.GlobalEdges);
        }

        private static void ReportMismatches(Stance stance, IReadOnlyList<Edge> edges)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge != null && edge.HasCondition && edge.Condition.TryDescribeMismatch(out string error))
                {
                    Debug.LogError($"[Combat] Stance '{stance.name}' edge {edge}: {error}", stance);
                }
            }
        }
    }
}
