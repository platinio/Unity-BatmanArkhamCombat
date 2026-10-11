using System.Collections.Generic;
using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
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

        public void Configure(HitReceiverProfile hitReceiverProfile, float idleMin, float idleMax)
        {
            this.hitReceiverProfile = hitReceiverProfile;
            this.idleMin = idleMin;
            this.idleMax = idleMax;
        }

        public bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;

            if (hitReceiverProfile == null)
            {
                errors.Add($"Archetype '{name}' names no hit receiver profile, so its enemies cannot be hit.");
            }

            if (idleMax < idleMin)
            {
                errors.Add($"Archetype '{name}': idleMax ({idleMax:0.00}) is below idleMin ({idleMin:0.00}).");
            }

            return errors.Count == errorCountBefore;
        }
    }
}
