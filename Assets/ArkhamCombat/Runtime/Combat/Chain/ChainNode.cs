using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A position in a chain: what plays here and where the chain may go next. Not an attack; the
    /// attack is a separate asset, so the same jab can sit at three positions in three stances.
    /// </summary>
    [Serializable]
    public sealed class ChainNode
    {
        [Tooltip("Stable name used by edges, the overlay and tests.")]
        [SerializeField] private string id;

        [Tooltip("One attack. Leave empty to use the pool instead.")]
        [SerializeField] private AttackDefinition attack;

        [Tooltip("Used when no single attack is set.")]
        [SerializeField] private VariantPool pool = new VariantPool();

        [Tooltip("Ways out, tried lowest priority first.")]
        [SerializeField] private List<Edge> edges = new List<Edge>();

        public ChainNode() { }

        public ChainNode(string id, AttackDefinition attack, params Edge[] edges)
        {
            this.id = id;
            this.attack = attack;
            this.edges = new List<Edge>(edges);
        }

        public ChainNode(string id, VariantPool pool, params Edge[] edges)
        {
            this.id = id;
            this.pool = pool;
            this.edges = new List<Edge>(edges);
        }

        public string Id => id;
        public AttackDefinition Attack => attack;
        public VariantPool Pool => pool;

        /// <summary>In priority order once the owning stance has been prepared.</summary>
        public IReadOnlyList<Edge> Edges => edges;

        public bool UsesPool => attack == null;

        /// <summary>The attack to play from here. Null only for a node that has neither an attack nor a pool entry.</summary>
        public AttackDefinition Pick(in VariantPickContext context) =>
            attack != null ? attack : pool?.Pick(context);

        /// <summary>Stable insertion sort by priority, so equal priorities keep authored order.</summary>
        internal void SortEdges()
        {
            for (int i = 1; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                int j = i - 1;
                while (j >= 0 && edges[j].Priority > edge.Priority)
                {
                    edges[j + 1] = edges[j];
                    j--;
                }

                edges[j + 1] = edge;
            }
        }

        public override string ToString() => id;
    }
}
