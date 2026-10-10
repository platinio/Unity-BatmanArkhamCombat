using UnityEngine;

namespace ArkhamCombat.Combat
{
    [CreateAssetMenu(menuName = "ArkhamCombat/Hit Receiver Profile", fileName = "HitReceiverProfile")]
    public sealed class HitReceiverProfile : ScriptableObject
    {
        [Tooltip("Health the character starts with. It dies when this reaches zero.")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Tooltip("Armor shrugs off light hits: a Flinch does nothing and a Stagger only flinches. Damage still applies.")]
        [SerializeField] private bool isArmored;

        [Tooltip("When off, a Knockdown only staggers.")]
        [SerializeField] private bool canBeKnockedDown = true;

        public float MaxHealth => maxHealth;
        public bool IsArmored => isArmored;
        public bool CanBeKnockedDown => canBeKnockedDown;

        public void Configure(float maxHealth, bool isArmored = false, bool canBeKnockedDown = true)
        {
            this.maxHealth = maxHealth;
            this.isArmored = isArmored;
            this.canBeKnockedDown = canBeKnockedDown;
        }
    }
}
