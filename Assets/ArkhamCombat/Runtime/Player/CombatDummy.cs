using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A thing to hit until enemies exist: the stand-in <see cref="ICombatTarget"/>. Publishes a
    /// state string, counts hits, and flashes so a landed strike is visible on a capsule. An enemy's
    /// status component implements the same interface and nothing player-side changes.
    /// </summary>
    public sealed class CombatDummy : MonoBehaviour, ICombatTarget
    {
        [Tooltip("Published as the targetState fact: Idle, Staggered, and so on.")]
        [SerializeField] private string state = "Idle";

        [SerializeField, Min(0f)] private float flashSeconds = 0.15f;
        [SerializeField] private Color flashColour = Color.white;

        private Renderer bodyRenderer;
        private MaterialPropertyBlock propertyBlock;
        private int colourProperty;
        private float flashEndsAt;

        public string State => state;
        public bool IsValid => this != null && isActiveAndEnabled;
        public Vector3 Position => transform.position;

        public int HitsTaken { get; private set; }
        public AttackDefinition LastHitBy { get; private set; }

        private bool IsFlashing => flashEndsAt > 0f;

        private void Awake()
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            colourProperty = MaterialColourProperty.Of(bodyRenderer != null ? bodyRenderer.sharedMaterial : null);
        }

        private void Update()
        {
            if (IsFlashing && Time.time >= flashEndsAt)
            {
                EndFlash();
            }
        }

        public void Receive(AttackDefinition attack)
        {
            HitsTaken++;
            LastHitBy = attack;
            StartFlash();
        }

        private void StartFlash()
        {
            flashEndsAt = Time.time + flashSeconds;

            if (bodyRenderer != null)
            {
                propertyBlock.SetColor(colourProperty, flashColour);
                bodyRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void EndFlash()
        {
            flashEndsAt = 0f;

            if (bodyRenderer != null)
            {
                bodyRenderer.SetPropertyBlock(null);
            }
        }
    }
}
