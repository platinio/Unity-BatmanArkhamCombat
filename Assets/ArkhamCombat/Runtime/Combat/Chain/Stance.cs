using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A whole chain: its nodes, the root the position collapses to, and the interrupt edges that
    /// are reachable from anywhere. One asset, authored as lists; nodes are found by id. Prepared
    /// on load so lookups and edge order cost nothing at resolve time.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Stance", fileName = "Stance")]
    public sealed class Stance : ScriptableObject
    {
        [Tooltip("The node the chain starts from and returns to.")]
        [SerializeField, ChainNodeId] private string root = "Neutral";

        [SerializeField] private List<ChainNode> nodes = new List<ChainNode>();

        [Tooltip("Interrupt edges (Counter, Evade, Stun) reachable from any node, tried before the node's own edges.")]
        [SerializeField] private List<Edge> globalEdges = new List<Edge>();

        [Tooltip("Idle seconds after the last consumed intent before the chain position collapses to the root. Tuned separately from the combo meter timeout.")]
        [SerializeField, Min(0f)] private float chainResetSeconds = 0.6f;

        private readonly Dictionary<string, ChainNode> nodesById = new Dictionary<string, ChainNode>();
        [NonSerialized] private List<Edge> globalEdgesByPriority;
        private bool isPrepared;

        public string RootId => root;
        public IReadOnlyList<ChainNode> Nodes => nodes;

        /// <summary>In priority order once prepared; the authored list is never reordered.</summary>
        public IReadOnlyList<Edge> GlobalEdges => globalEdgesByPriority ?? globalEdges;

        public float ChainResetSeconds => chainResetSeconds;

        /// <summary>Null when the root id names no node; <see cref="Validate"/> reports that.</summary>
        public ChainNode Root
        {
            get
            {
                EnsurePrepared();
                return nodesById.TryGetValue(root ?? string.Empty, out ChainNode node) ? node : null;
            }
        }

        public bool TryGetNode(string id, out ChainNode node)
        {
            EnsurePrepared();
            if (id == null)
            {
                node = null;
                return false;
            }

            return nodesById.TryGetValue(id, out node);
        }

        /// <summary>Sets every field from code. For tests and the fixture builder.</summary>
        public void Configure(string root, IEnumerable<ChainNode> nodes, IEnumerable<Edge> globalEdges = null, float chainResetSeconds = 0.6f)
        {
            this.root = root;
            this.nodes = new List<ChainNode>(nodes);
            this.globalEdges = globalEdges != null ? new List<Edge>(globalEdges) : new List<Edge>();
            this.chainResetSeconds = chainResetSeconds;
            Prepare();
        }

        /// <summary>Idempotent; runs again after inspector edits.</summary>
        public void Prepare()
        {
            IndexNodesById();
            SortEdgesByPriority();
            isPrepared = true;
        }

        private void EnsurePrepared()
        {
            if (!isPrepared)
            {
                Prepare();
            }
        }

        /// <summary>The first node with an id wins; a duplicate is left out of the index and reported by <see cref="Validate"/>.</summary>
        private void IndexNodesById()
        {
            nodesById.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id) || nodesById.ContainsKey(node.Id))
                {
                    continue;
                }

                nodesById.Add(node.Id, node);
            }
        }

        private void SortEdgesByPriority()
        {
            foreach (ChainNode node in nodesById.Values)
            {
                node.SortEdgesByPriority();
            }

            globalEdgesByPriority = EdgeOrder.SortByPriority(globalEdges, globalEdgesByPriority);
        }

        private void OnEnable() => Prepare();

        private void OnValidate() => Prepare();

        /// <summary>
        /// Appends every structural problem to <paramref name="errors"/>: a missing root, duplicate
        /// ids, a node with nothing to play, an edge to nowhere, and a node nothing reaches.
        /// Returns true when there were none.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;
            Prepare();

            ValidateNodes(errors);
            ValidateEdges(globalEdges, "global edges", errors);

            if (Root == null)
            {
                errors.Add($"'{name}': root '{root}' names no node.");
            }
            else
            {
                ReportUnreachableNodes(errors);
            }

            return errors.Count == errorCountBefore;
        }

        private void ValidateNodes(List<string> errors)
        {
            HashSet<string> seenIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    errors.Add($"'{name}': node {i} has no id.");
                    continue;
                }

                if (!seenIds.Add(node.Id))
                {
                    errors.Add($"'{name}': node id '{node.Id}' is used more than once.");
                }

                if (node.Id != root && node.HasNothingToPlay)
                {
                    errors.Add($"'{name}': node '{node.Id}' has neither an attack nor a pool entry.");
                }

                ValidateEdges(node.Edges, $"node '{node.Id}'", errors);
            }
        }

        private void ValidateEdges(IReadOnlyList<Edge> edges, string owner, List<string> errors)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge == null)
                {
                    errors.Add($"'{name}': {owner} edge {i} is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.DestinationId))
                {
                    errors.Add($"'{name}': {owner} edge {i} ({edge.Intent}) has no destination.");
                }
                else if (!nodesById.TryGetValue(edge.DestinationId, out ChainNode destination))
                {
                    errors.Add($"'{name}': {owner} edge {i} ({edge.Intent}) points at unknown node '{edge.DestinationId}'.");
                }
                else if (destination.HasNothingToPlay)
                {
                    errors.Add($"'{name}': {owner} edge {i} ({edge.Intent}) points at '{edge.DestinationId}', which has nothing to play.");
                }
            }
        }

        /// <summary>The search starts from every global edge as well as the root, since interrupt nodes are entered only that way.</summary>
        private void ReportUnreachableNodes(List<string> errors)
        {
            HashSet<string> reachedIds = FindReachableNodeIds();

            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.Id) && !reachedIds.Contains(node.Id))
                {
                    errors.Add($"'{name}': node '{node.Id}' is not reachable from the root or any global edge.");
                }
            }
        }

        private HashSet<string> FindReachableNodeIds()
        {
            HashSet<string> reachedIds = new HashSet<string>();
            Stack<ChainNode> nodesToVisit = new Stack<ChainNode>();

            void Reach(string id)
            {
                if (id != null && nodesById.TryGetValue(id, out ChainNode node) && reachedIds.Add(id))
                {
                    nodesToVisit.Push(node);
                }
            }

            Reach(root);
            for (int i = 0; i < globalEdges.Count; i++)
            {
                Reach(globalEdges[i]?.DestinationId);
            }

            while (nodesToVisit.Count > 0)
            {
                ChainNode node = nodesToVisit.Pop();
                for (int i = 0; i < node.Edges.Count; i++)
                {
                    Reach(node.Edges[i]?.DestinationId);
                }
            }

            return reachedIds;
        }
    }
}
