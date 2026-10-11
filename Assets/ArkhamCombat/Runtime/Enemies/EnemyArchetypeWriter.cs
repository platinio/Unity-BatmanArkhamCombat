using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// The tree reads the tunables by key from the agent's variables and never sees the asset.
    /// </summary>
    public sealed class EnemyArchetypeWriter : MonoBehaviour
    {
        private const string Source = nameof(EnemyArchetypeWriter);

        private EnemyArchetype archetype;

        [Inject]
        internal void Construct([InjectOptional] EnemyArchetype archetype) => this.archetype = archetype;

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
