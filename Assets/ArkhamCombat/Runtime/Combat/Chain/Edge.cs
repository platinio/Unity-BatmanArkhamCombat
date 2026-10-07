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
    /// One way out of a chain position: which intent takes it, under what condition, where it goes.
    /// The condition is a Function with a bool result, so designers write the rule in a graph; null
    /// means always. Destinations are ids rather than references so a stance stays one asset.
    /// </summary>
    [Serializable]
    public sealed class Edge
    {
        [SerializeField] private IntentKind intent;

        [Tooltip("Optional. A Function returning bool, read against the combat facts. Empty means the edge always matches.")]
        [SerializeField] private FunctionCall<bool> condition = new FunctionCall<bool>();

        [Tooltip("Lower runs first within a node.")]
        [SerializeField] private int priority;

        [SerializeField, ChainNodeId] private string destination;

        public Edge() { }

        public Edge(IntentKind intent, string destination, int priority = 0)
        {
            this.intent = intent;
            this.destination = destination;
            this.priority = priority;
        }

        public IntentKind Intent => intent;
        public FunctionCall<bool> Condition => condition;
        public bool HasCondition => condition != null && condition.Function != null;
        public int Priority => priority;
        public string Destination => destination;

        public override string ToString() => $"{intent} -> {destination} (p{priority})";
    }
}
