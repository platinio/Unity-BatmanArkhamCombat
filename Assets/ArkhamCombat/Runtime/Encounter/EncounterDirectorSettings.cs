using System;
using UnityEngine;

namespace ArkhamCombat.Encounter
{
    [Serializable]
    public class EncounterDirectorSettings
    {
        [Tooltip("How many agents may be attacking in melee at the same time.")]
        [SerializeField] private int maxMeleeTokens = 1;

        [Tooltip("How many agents may be attacking at range at the same time.")]
        [SerializeField] private int maxRangedTokens = 1;

        [Tooltip("Seconds an agent may hold a token before it is taken back. 0 disables the timeout.")]
        [SerializeField] private float maxTokenHoldSeconds = 3.0f;

        [Tooltip("Seconds an agent must wait after losing a token before it can ask for another one.")]
        [SerializeField] private float reacquireCooldownSeconds = 1.5f;

        [Tooltip("Number of standing positions in the ring around the target.")]
        [SerializeField] private int slotCount = 8;

        [Tooltip("Distance from the target to the ring of standing positions.")]
        [SerializeField] private float slotRadius = 4.0f;

        [Tooltip("Seconds between re-spreading agents across the ring.")]
        [SerializeField] private float slotReassignIntervalSeconds = 1.0f;

        [Tooltip("How far from its place on the ring a slot may be moved to reach walkable ground. A slot with none that close is left empty.")]
        [SerializeField] private float slotSampleRadius = 1.0f;

        [Tooltip("NavMeshAgent avoidance priority of an agent holding a token. Lower numbers are steered around by higher ones.")]
        [SerializeField, Range(0, 99)] private int attackerAvoidancePriority = 20;

        [Tooltip("NavMeshAgent avoidance priority of an agent without a token.")]
        [SerializeField, Range(0, 99)] private int defaultAvoidancePriority = 50;

        public int MaxMeleeTokens => Mathf.Max(0, maxMeleeTokens);
        public int MaxRangedTokens => Mathf.Max(0, maxRangedTokens);
        public float MaxTokenHoldSeconds => Mathf.Max(0.0f, maxTokenHoldSeconds);
        public float ReacquireCooldownSeconds => Mathf.Max(0.0f, reacquireCooldownSeconds);
        public int SlotCount => Mathf.Max(1, slotCount);
        public float SlotRadius => Mathf.Max(0.0f, slotRadius);
        public float SlotReassignIntervalSeconds => Mathf.Max(0.0f, slotReassignIntervalSeconds);
        public float SlotSampleRadius => Mathf.Max(0.0f, slotSampleRadius);
        public int AttackerAvoidancePriority => attackerAvoidancePriority;
        public int DefaultAvoidancePriority => defaultAvoidancePriority;

        public EncounterDirectorSettings()
        {
        }

        public EncounterDirectorSettings(
            int maxMeleeTokens = 1,
            int maxRangedTokens = 1,
            float maxTokenHoldSeconds = 3.0f,
            float reacquireCooldownSeconds = 1.5f,
            int slotCount = 8,
            float slotRadius = 4.0f,
            float slotReassignIntervalSeconds = 1.0f,
            float slotSampleRadius = 1.0f,
            int attackerAvoidancePriority = 20,
            int defaultAvoidancePriority = 50)
        {
            this.maxMeleeTokens = maxMeleeTokens;
            this.maxRangedTokens = maxRangedTokens;
            this.maxTokenHoldSeconds = maxTokenHoldSeconds;
            this.reacquireCooldownSeconds = reacquireCooldownSeconds;
            this.slotCount = slotCount;
            this.slotRadius = slotRadius;
            this.slotReassignIntervalSeconds = slotReassignIntervalSeconds;
            this.slotSampleRadius = slotSampleRadius;
            this.attackerAvoidancePriority = attackerAvoidancePriority;
            this.defaultAvoidancePriority = defaultAvoidancePriority;
        }
    }
}
