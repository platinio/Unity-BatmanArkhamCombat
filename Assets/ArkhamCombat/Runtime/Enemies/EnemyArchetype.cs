using System.Collections.Generic;
using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    public sealed class EnemyArchetype : ScriptableItem, IEncounterAgentSettings
    {
        [Tooltip("How much health this enemy has and which hits it shrugs off.")]
        [SerializeField, ScriptableItemDatabaseSelector(typeof(HitReceiverProfileDatabase))]
        private HitReceiverProfile hitReceiverProfile;

        [Tooltip("Seconds an idle loop lasts, at least.")]
        [SerializeField, Min(0f)] private float idleMin = 0.2f;

        [Tooltip("Seconds an idle loop lasts, at most.")]
        [SerializeField, Min(0f)] private float idleMax = 0.5f;

        [Tooltip("Within this many metres of its target the enemy stops closing in and takes its place on the ring. Keep it wider than the ring.")]
        [SerializeField, Min(0f)] private float combatRange = 5f;

        [Tooltip("Metres per second it walks at.")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;

        [Tooltip("Which attack turns it waits for: melee attackers and ranged attackers each have their own tokens.")]
        [SerializeField] private AttackTokenType tokenType = AttackTokenType.Melee;

        [Tooltip("What it swings when it is its turn to attack.")]
        [SerializeField] private AttackDefinition attack;

        public HitReceiverProfile HitReceiverProfile => hitReceiverProfile;
        public float IdleMin => idleMin;
        public float IdleMax => idleMax;
        public float CombatRange => combatRange;
        public float MoveSpeed => moveSpeed;
        public AttackTokenType TokenType => tokenType;
        public AttackDefinition Attack => attack;

        public void Configure(HitReceiverProfile hitReceiverProfile, float idleMin, float idleMax)
        {
            this.hitReceiverProfile = hitReceiverProfile;
            this.idleMin = idleMin;
            this.idleMax = idleMax;
        }

        public void ConfigureMovement(float combatRange, float moveSpeed)
        {
            this.combatRange = combatRange;
            this.moveSpeed = moveSpeed;
        }

        public void ConfigureTokenType(AttackTokenType tokenType) => this.tokenType = tokenType;

        public void ConfigureAttack(AttackDefinition attack) => this.attack = attack;

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

            if (moveSpeed <= 0f)
            {
                errors.Add($"Archetype '{name}' has no move speed, so its enemies cannot walk to their target.");
            }

            if (attack == null)
            {
                errors.Add($"Archetype '{name}' names no attack, so its enemies take their turn and swing nothing.");
            }
            else
            {
                bool isTheAttackWellFormed = attack.Validate(errors);
                if (isTheAttackWellFormed)
                {
                    ValidateTheAttackTelegraphsBeforeItCanHit(errors);
                }
            }

            return errors.Count == errorCountBefore;
        }

        // An enemy's tree tracks its target while the attack telegraphs, and the player's answers open on the telegraph.
        private void ValidateTheAttackTelegraphsBeforeItCanHit(List<string> errors)
        {
            if (attack.TelegraphWindow.IsZeroLength)
            {
                errors.Add($"Archetype '{name}': its attack '{attack.name}' has no telegraph window, so its enemies swing without warning and never track their target.");
                return;
            }

            bool isStillTelegraphingAsTheHitOpens = attack.TelegraphWindow.End > attack.HitWindow.Start;
            if (isStillTelegraphingAsTheHitOpens)
            {
                errors.Add(
                    $"Archetype '{name}': its attack '{attack.name}' telegraphs until {attack.TelegraphWindow.End:0.00}, " +
                    $"after its hit window opens at {attack.HitWindow.Start:0.00}. The telegraph has to end by then.");
            }
        }
    }
}
