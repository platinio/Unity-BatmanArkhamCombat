using UnityEngine;

namespace ArkhamCombat.Encounter
{
    [RequireComponent(typeof(EncounterAgent))]
    [AddComponentMenu("ArkhamCombat/Encounter Debug View")]
    public sealed class EncounterDebugView : MonoBehaviour
    {
        [Tooltip("Leave empty to use the first renderer found on this object or its children.")]
        [SerializeField] private Renderer tintedRenderer;

        [SerializeField] private Color attackingColor = new Color(0.85f, 0.15f, 0.15f);
        [SerializeField] private Color waitingColor = new Color(0.35f, 0.35f, 0.38f);

        [Tooltip("Draws a line to the standing position the director assigned.")]
        [SerializeField] private bool drawSlotLine = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private EncounterAgent encounterAgent;
        private MaterialPropertyBlock propertyBlock;
        private bool wasHoldingToken;

        private void Awake()
        {
            encounterAgent = GetComponent<EncounterAgent>();
            propertyBlock = new MaterialPropertyBlock();

            if (tintedRenderer == null)
            {
                tintedRenderer = GetComponentInChildren<Renderer>();
            }

            Tint(waitingColor);
        }

        private void Update()
        {
            bool isHoldingToken = encounterAgent.HasToken;
            if (isHoldingToken == wasHoldingToken)
            {
                return;
            }

            Tint(isHoldingToken ? attackingColor : waitingColor);
            wasHoldingToken = isHoldingToken;
        }

        private void Tint(Color color)
        {
            if (tintedRenderer == null)
            {
                return;
            }

            tintedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            tintedRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDrawGizmos()
        {
            if (!drawSlotLine || !Application.isPlaying || encounterAgent == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, encounterAgent.SlotPosition);
        }
    }
}
