using UnityEngine;
using Zenject;

namespace ArkhamCombat.Encounter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ArkhamCombat/Encounter Director")]
    public sealed class EncounterDirectorBehaviour : MonoBehaviour
    {
        private const float SlotGizmoRadius = 0.25f;

        [Tooltip("What the encounter revolves around. Normally the player.")]
        [SerializeField] private Transform target;

        [Tooltip("Draws the ring of standing positions while this object is selected in play mode.")]
        [SerializeField] private bool drawGizmos = true;

        public EncounterDirector Director { get; private set; }
        public Transform Target => target;

        [Inject]
        internal void Construct(EncounterDirector director) => Director = director;

        public void SetTarget(Transform target) => this.target = target;

        private void Update() => Tick(Time.deltaTime);

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

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || Director == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;

            for (int slot = 0; slot < Director.Settings.SlotCount; slot++)
            {
                Gizmos.DrawWireSphere(Director.SlotWorldPosition(slot), SlotGizmoRadius);
            }
        }
    }
}
