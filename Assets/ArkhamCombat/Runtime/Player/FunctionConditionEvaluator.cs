using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Answers edge conditions by running their Functions against the player's agent variables. A
    /// Function that cannot run is reported once and treated as false, so a broken graph fails closed.
    /// </summary>
    public sealed class FunctionConditionEvaluator : IConditionEvaluator
    {
        private readonly GameObject agent;
        private readonly CombatFactsUpdater factsUpdater;
        private readonly HashSet<Edge> edgesAlreadyReported = new HashSet<Edge>();

        public FunctionConditionEvaluator(CharacterMotor motor, CombatFactsUpdater factsUpdater, Stance stance)
        {
            agent = motor.gameObject;
            this.factsUpdater = factsUpdater;

            if (stance != null)
            {
                ReportInvalidConditions(stance);
            }
        }

        public bool IsConditionMet(Edge edge, CombatFacts facts, Intent intent)
        {
            if (!edge.HasCondition)
            {
                return true;
            }

            // Updated here rather than each frame: the stick angle belongs to the press being resolved.
            factsUpdater.UpdateStickAngle(intent.MoveAtPress);

            return RunCondition(edge);
        }

        private bool RunCondition(Edge edge)
        {
            FunctionCall<bool>.Bound boundCondition = edge.Condition.For(agent);
            if (boundCondition.TryInvoke(out bool isMet, out string error))
            {
                return isMet;
            }

            if (edgesAlreadyReported.Add(edge))
            {
                Debug.LogError($"[Combat] Edge {edge}: condition could not run: {error}");
            }

            return false;
        }

        /// <summary>The load-time check: every authored condition must be a Function whose Result is a bool.</summary>
        private static void ReportInvalidConditions(Stance stance)
        {
            foreach (ChainNode node in stance.Nodes)
            {
                ReportInvalidConditions(stance, node.Edges);
            }

            ReportInvalidConditions(stance, stance.GlobalEdges);
        }

        private static void ReportInvalidConditions(Stance stance, IReadOnlyList<Edge> edges)
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
