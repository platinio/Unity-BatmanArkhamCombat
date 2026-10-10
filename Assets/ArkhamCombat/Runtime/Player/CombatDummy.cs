using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A thing to hit until enemies exist: the stand-in <see cref="ICombatTarget"/>. An enemy's
    /// status component implements the same interface and nothing player-side changes.
    /// </summary>
    public sealed class CombatDummy : MonoBehaviour, ICombatTarget
    {
        [Tooltip("Reported as the targetState fact: Idle, Staggered, and so on.")]
        [SerializeField] private string state = "Idle";

        [Tooltip("How much health it has and which hits it shrugs off.")]
        [SerializeField] private HitReceiverProfile profile;

        [SerializeField, Min(0f)] private float flashSeconds = 0.15f;
        [SerializeField] private Color flashColour = Color.white;

        private HitReceiver receiver;
        private Renderer bodyRenderer;
        private MaterialPropertyBlock propertyBlock;
        private int colourProperty;
        private float flashEndsAt;

        public string State => state;
        public bool IsValid => this != null && isActiveAndEnabled && IsAlive;
        public Vector3 Position => transform.position;

        public int HitsTaken { get; private set; }
        public AttackDefinition LastHitBy { get; private set; }

        private bool IsAlive => receiver != null && receiver.IsAlive;

        private bool IsFlashing => flashEndsAt > 0f;

        private void Awake()
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            colourProperty = MaterialColourProperty.Of(bodyRenderer != null ? bodyRenderer.sharedMaterial : null);

            CreateReceiver();
        }

        private void Update()
        {
            if (IsFlashing && Time.time >= flashEndsAt)
            {
                EndFlash();
            }
        }

        internal void CreateReceiver()
        {
            if (profile == null)
            {
                Debug.LogError($"[{nameof(CombatDummy)}] No hit receiver profile assigned on '{name}', so it cannot be hit.", this);
                return;
            }

            receiver = new HitReceiver(profile);
        }

        public HitResult Receive(HitInfo hit)
        {
            if (receiver == null)
            {
                return HitResult.Ignored;
            }

            HitResult result = receiver.Receive(hit);
            if (!result.HasLanded)
            {
                return result;
            }

            HitsTaken++;
            LastHitBy = hit.Attack;
            StartFlash();

            if (result.WasKilled)
            {
                FallOver(hit.Direction);
            }

            return result;
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

        private void FallOver(Vector3 hitDirection)
        {
            bool hasHitDirection = hitDirection != Vector3.zero;
            Vector3 fallDirection = hasHitDirection ? hitDirection : -transform.forward;

            LowerOntoItsSide();
            LieDownAlong(fallDirection);
            StopBlockingTheWay();
        }

        private void LowerOntoItsSide()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            Bounds standingBounds = bodyRenderer.bounds;
            float halfHeight = standingBounds.extents.y;
            float halfWidth = standingBounds.extents.x;
            transform.position += Vector3.down * (halfHeight - halfWidth);
        }

        private void LieDownAlong(Vector3 fallDirection)
        {
            transform.rotation = Quaternion.FromToRotation(transform.up, fallDirection) * transform.rotation;
        }

        private void StopBlockingTheWay()
        {
            if (TryGetComponent(out Collider bodyCollider))
            {
                bodyCollider.enabled = false;
            }
        }
    }
}
