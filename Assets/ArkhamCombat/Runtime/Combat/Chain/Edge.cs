using System;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>Marks a string field as a chain node id, so the editor offers the stance's node ids in a dropdown.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ChainNodeIdAttribute : PropertyAttribute { }

    /// <summary>
    /// One way out of a chain position: which kind of press takes it, under what condition, where it goes.
    /// The condition is a Function with a bool result, so designers write the rule in a graph; null
    /// means always. Destinations are ids rather than references so a stance stays one asset.
    /// </summary>
    [Serializable]
    public sealed class Edge
    {
        [Tooltip("The kind of press that takes this edge.")]
        [SerializeField] private IntentKind intentKind;

        [Tooltip("Optional. A Function returning bool, read against the combat facts. Empty means the edge always matches.")]
        [SerializeField] private FunctionCall<bool> condition = new FunctionCall<bool>();

        [Tooltip("Lower runs first within a node.")]
        [SerializeField] private int priority;

        [SerializeField, ChainNodeId] private string destinationId;

        public Edge() { }

        public Edge(IntentKind intentKind, string destinationId, int priority = 0)
        {
            this.intentKind = intentKind;
            this.destinationId = destinationId;
            this.priority = priority;
        }

        public IntentKind IntentKind => intentKind;
        public bool HasIntentKind => intentKind != null;

        /// <summary>Safe to print for an edge whose kind is missing.</summary>
        public string IntentKindName => HasIntentKind ? intentKind.name : "no kind";

        public FunctionCall<bool> Condition => condition;
        public bool HasCondition => condition != null && condition.Function != null;
        public int Priority => priority;
        public string DestinationId => destinationId;

        public override string ToString() => $"{IntentKindName} -> {destinationId} (p{priority})";
    }
}
