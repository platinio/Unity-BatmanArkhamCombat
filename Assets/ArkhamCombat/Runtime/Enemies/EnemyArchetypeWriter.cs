using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// Copies the archetype's tunables onto the agent's variables as the enemy wakes, so its tree
    /// reads them by key and never sees the asset.
    /// </summary>
    public sealed class EnemyArchetypeWriter : MonoBehaviour
    {
        private const string Source = nameof(EnemyArchetypeWriter);

        private EnemyArchetype archetype;

        [Inject]
        internal void Construct(EnemyArchetype archetype) => this.archetype = archetype;

        private void Awake() => WriteTunables();

        internal void WriteTunables()
        {
            if (archetype == null)
            {
                return;
            }

            Write(EnemyVariableNames.IdleMin, archetype.IdleMin);
            Write(EnemyVariableNames.IdleMax, archetype.IdleMax);
        }

        private void Write(string key, object value) => AgentVariableWriter.SetOn(gameObject, key, value, Source);
    }
}
