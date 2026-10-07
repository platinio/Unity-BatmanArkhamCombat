using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A thing to hit until enemies exist. Publishes a state string the context reads as
    /// targetState, counts hits, and flashes so a landed strike is visible on a capsule.
    /// </summary>
    public sealed class CombatDummy : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");

        [Tooltip("Published as the targetState fact: Idle, Staggered, and so on.")]
        [SerializeField] private string state = "Idle";

        [SerializeField, Min(0f)] private float flashSeconds = 0.15f;
        [SerializeField] private Color hitColour = Color.white;

        private Renderer bodyRenderer;
        private MaterialPropertyBlock block;
        private int colourProperty;
        private float flashUntil;

        public string State => state;
        public int HitsTaken { get; private set; }
        public AttackDefinition LastHitBy { get; private set; }

        private void Awake()
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            Material material = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            colourProperty = material != null && material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
        }

        public void ReceiveHit(AttackDefinition attack)
        {
            HitsTaken++;
            LastHitBy = attack;
            flashUntil = Time.time + flashSeconds;

            if (bodyRenderer != null)
            {
                block.SetColor(colourProperty, hitColour);
                bodyRenderer.SetPropertyBlock(block);
            }
        }

        private void Update()
        {
            if (flashUntil > 0f && Time.time >= flashUntil)
            {
                flashUntil = 0f;
                if (bodyRenderer != null)
                {
                    bodyRenderer.SetPropertyBlock(null);
                }
            }
        }
    }
}
