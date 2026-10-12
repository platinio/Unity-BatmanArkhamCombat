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

        [Tooltip("How much standing in front of the target counts when several agents ask for the same token.")]
        [SerializeField, Min(0f)] private float frontWeight = 1.0f;

        [Tooltip("How much a long wait since its last token counts when several agents ask for the same token.")]
        [SerializeField, Min(0f)] private float fairnessWeight = 0.6f;

        [Tooltip("Seconds without a token after which waiting longer no longer counts for more. 0 switches the fairness weight off.")]
        [SerializeField, Min(0f)] private float fairnessHorizonSeconds = 6.0f;

        [Tooltip("How much being far from the target counts against an agent asking for a token. A full ring radius away costs all of it.")]
        [SerializeField, Min(0f)] private float distancePenalty = 0.3f;

        [Tooltip("Seconds an agent has to ask again, with Try Acquire Attack Token, for a token it was granted before the token is taken back. 0 disables this.")]
        [SerializeField, Min(0f)] private float grantGraceSeconds = 0.5f;

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
        public float FrontWeight => Mathf.Max(0.0f, frontWeight);
        public float FairnessWeight => Mathf.Max(0.0f, fairnessWeight);
        public float FairnessHorizonSeconds => Mathf.Max(0.0f, fairnessHorizonSeconds);
        public float DistancePenalty => Mathf.Max(0.0f, distancePenalty);
        public float GrantGraceSeconds => Mathf.Max(0.0f, grantGraceSeconds);

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
            int defaultAvoidancePriority = 50,
            float frontWeight = 1.0f,
            float fairnessWeight = 0.6f,
            float fairnessHorizonSeconds = 6.0f,
            float distancePenalty = 0.3f,
            float grantGraceSeconds = 0.5f)
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
            this.frontWeight = frontWeight;
            this.fairnessWeight = fairnessWeight;
            this.fairnessHorizonSeconds = fairnessHorizonSeconds;
            this.distancePenalty = distancePenalty;
            this.grantGraceSeconds = grantGraceSeconds;
        }
    }
}
