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

        /// <summary>Kept apart from the serialized list so sorting never reorders what was authored.</summary>
        [NonSerialized] private List<Edge> edgesByPriority;

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

        /// <summary>In priority order once the owning stance has been prepared; authored order before.</summary>
        public IReadOnlyList<Edge> Edges => edgesByPriority ?? edges;

        public bool IsUsingPool => attack == null;

        /// <summary>True for a node with neither an attack nor a pool entry, such as a root that is only a position.</summary>
        public bool HasNothingToPlay => attack == null && (pool == null || pool.IsEmpty);

        /// <summary>Null only when <see cref="HasNothingToPlay"/>.</summary>
        public AttackDefinition PickAttack(in VariantPickContext context) =>
            attack != null ? attack : pool?.Pick(context);

        internal void SortEdgesByPriority()
        {
            edgesByPriority = EdgeOrder.SortByPriority(edges, edgesByPriority);
        }

        public override string ToString() => id;
    }

    /// <summary>The one place edge order is decided: lower priority first, ties in authored order.</summary>
    internal static class EdgeOrder
    {
        /// <summary>
        /// An insertion sort because it is stable, into <paramref name="reusableList"/> when there is
        /// one so preparing a stance again allocates nothing.
        /// </summary>
        public static List<Edge> SortByPriority(List<Edge> authored, List<Edge> reusableList)
        {
            List<Edge> sorted = reusableList ?? new List<Edge>(authored.Count);
            sorted.Clear();
            sorted.AddRange(authored);

            for (int i = 1; i < sorted.Count; i++)
            {
                Edge edge = sorted[i];
                int priority = PriorityOf(edge);
                int j = i - 1;
                while (j >= 0 && PriorityOf(sorted[j]) > priority)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }

                sorted[j + 1] = edge;
            }

            return sorted;
        }

        /// <summary>A missing edge sorts last.</summary>
        private static int PriorityOf(Edge edge) => edge != null ? edge.Priority : int.MaxValue;
    }
}
