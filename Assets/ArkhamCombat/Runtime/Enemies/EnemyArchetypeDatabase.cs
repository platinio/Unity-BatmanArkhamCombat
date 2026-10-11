using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    [CreateAssetMenu(menuName = "ArkhamCombat/Databases/Enemy Archetypes", fileName = "EnemyArchetypes")]
    public sealed class EnemyArchetypeDatabase : ScriptableDatabase<EnemyArchetype>
    {
    }
}
