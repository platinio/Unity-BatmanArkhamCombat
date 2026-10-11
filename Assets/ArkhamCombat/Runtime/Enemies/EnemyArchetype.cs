using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// One kind of enemy. A prefab points at an archetype; an Elite is another archetype, not
    /// another prefab.
    /// </summary>
    public sealed class EnemyArchetype : ScriptableItem
    {
        [Tooltip("How much health this enemy has and which hits it shrugs off.")]
        [SerializeField, ScriptableItemDatabaseSelector(typeof(HitReceiverProfileDatabase))]
        private HitReceiverProfile hitReceiverProfile;

        [Tooltip("Seconds an idle loop lasts, at least.")]
        [SerializeField, Min(0f)] private float idleMin = 0.2f;

        [Tooltip("Seconds an idle loop lasts, at most.")]
        [SerializeField, Min(0f)] private float idleMax = 0.5f;

        public HitReceiverProfile HitReceiverProfile => hitReceiverProfile;
        public float IdleMin => idleMin;
        public float IdleMax => idleMax;

        public void Configure(HitReceiverProfile hitReceiverProfile, float idleMin = 0.2f, float idleMax = 0.5f)
        {
            this.hitReceiverProfile = hitReceiverProfile;
            this.idleMin = idleMin;
            this.idleMax = idleMax;
        }
    }
}
