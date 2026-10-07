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

        private readonly Dictionary<string, ChainNode> byId = new Dictionary<string, ChainNode>();
        private bool prepared;

        public string RootId => root;
        public IReadOnlyList<ChainNode> Nodes => nodes;
        public IReadOnlyList<Edge> GlobalEdges => globalEdges;
        public float ChainResetSeconds => chainResetSeconds;

        /// <summary>Null when the root id names no node; <see cref="Validate"/> reports that.</summary>
        public ChainNode Root
        {
            get
            {
                EnsurePrepared();
                return byId.TryGetValue(root ?? string.Empty, out ChainNode node) ? node : null;
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

            return byId.TryGetValue(id, out node);
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

        /// <summary>Builds the id index and sorts every edge list. Idempotent; runs again after inspector edits.</summary>
        public void Prepare()
        {
            byId.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id) || byId.ContainsKey(node.Id))
                {
                    continue;
                }

                byId.Add(node.Id, node);
                node.SortEdges();
            }

            SortGlobalEdges();
            prepared = true;
        }

        private void EnsurePrepared()
        {
            if (!prepared)
            {
                Prepare();
            }
        }

        private void SortGlobalEdges()
        {
            for (int i = 1; i < globalEdges.Count; i++)
            {
                Edge edge = globalEdges[i];
                int j = i - 1;
                while (j >= 0 && globalEdges[j].Priority > edge.Priority)
                {
                    globalEdges[j + 1] = globalEdges[j];
                    j--;
                }

                globalEdges[j + 1] = edge;
            }
        }

        private void OnEnable() => Prepare();

        private void OnValidate() => Prepare();

        /// <summary>
        /// Appends every structural problem to <paramref name="errors"/>: a missing root, duplicate
        /// ids, a node with nothing to play, an empty pool, an edge to nowhere, and a node nothing
        /// reaches. Reachability starts from the root and from every global edge, since interrupt
        /// nodes are entered only that way.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            Prepare();

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    errors.Add($"'{name}': node {i} has no id.");
                    continue;
                }

                if (!seen.Add(node.Id))
                {
                    errors.Add($"'{name}': node id '{node.Id}' is used more than once.");
                }

                if (node.Id != root && node.Attack == null && (node.Pool == null || node.Pool.IsEmpty))
                {
                    errors.Add($"'{name}': node '{node.Id}' has neither an attack nor a pool entry.");
                }

                ValidateEdges(node.Edges, $"node '{node.Id}'", errors);
            }

            ValidateEdges(globalEdges, "global edges", errors);

            if (Root == null)
            {
                errors.Add($"'{name}': root '{root}' names no node.");
            }
            else
            {
                ReportUnreachable(errors);
            }

            return errors.Count == before;
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

                if (string.IsNullOrEmpty(edge.Destination))
                {
                    errors.Add($"'{name}': {owner} edge {i} ({edge.Intent}) has no destination.");
                }
                else if (!byId.ContainsKey(edge.Destination))
                {
                    errors.Add($"'{name}': {owner} edge {i} ({edge.Intent}) points at unknown node '{edge.Destination}'.");
                }
            }
        }

        private void ReportUnreachable(List<string> errors)
        {
            HashSet<string> reached = new HashSet<string>();
            Stack<ChainNode> pending = new Stack<ChainNode>();

            void Visit(string id)
            {
                if (id != null && byId.TryGetValue(id, out ChainNode node) && reached.Add(id))
                {
                    pending.Push(node);
                }
            }

            Visit(root);
            for (int i = 0; i < globalEdges.Count; i++)
            {
                Visit(globalEdges[i]?.Destination);
            }

            while (pending.Count > 0)
            {
                ChainNode node = pending.Pop();
                for (int i = 0; i < node.Edges.Count; i++)
                {
                    Visit(node.Edges[i]?.Destination);
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                ChainNode node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.Id) && !reached.Contains(node.Id))
                {
                    errors.Add($"'{name}': node '{node.Id}' is not reachable from the root or any global edge.");
                }
            }
        }
    }
}
