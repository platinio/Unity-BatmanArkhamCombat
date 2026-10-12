using ArcaneOnyx.MeshGizmos;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Encounter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ArkhamCombat/Encounter Director")]
    public sealed class EncounterDirectorBehaviour : MonoBehaviour
    {
        private const float SlotGizmoRadius = 0.25f;
        private static readonly Color SlotGizmoColor = Color.yellow;

        [Tooltip("What the encounter revolves around. Normally the player.")]
        [SerializeField] private Transform target;

        [Tooltip("Draws the ring of standing positions in play mode.")]
        [SerializeField] private bool drawGizmos = true;

        private MGizmoGroup ringOfSlots;

        public EncounterDirector Director { get; private set; }
        public Transform Target => target;

        [Inject]
        internal void Construct(EncounterDirector director) => Director = director;

        public void SetTarget(Transform target) => this.target = target;

        private void OnEnable() => ringOfSlots = new MGizmoGroup();

        private void OnDisable() => ringOfSlots.Dispose();

        private void Update()
        {
            Tick(Time.deltaTime);
            DrawTheRingOfSlots();
        }

        internal void Tick(float deltaTime)
        {
            if (Director == null)
            {
                return;
            }

            if (target != null)
            {
                Director.SetTargetPosition(target.position);
            }

            Director.Tick(deltaTime);
        }

        private void DrawTheRingOfSlots()
        {
            ringOfSlots.Clear();

            if (!drawGizmos || Director == null)
            {
                return;
            }

            for (int slot = 0; slot < Director.Settings.SlotCount; slot++)
            {
                ringOfSlots.Add(MGizmos.RenderSphere(Director.SlotWorldPosition(slot), SlotGizmoRadius).SetColor(SlotGizmoColor));
            }
        }
    }
}
