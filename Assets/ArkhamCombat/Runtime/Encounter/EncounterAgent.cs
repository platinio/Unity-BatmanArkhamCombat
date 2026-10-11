using UnityEngine;
using Zenject;

namespace ArkhamCombat.Encounter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ArkhamCombat/Encounter Agent")]
    public sealed class EncounterAgent : MonoBehaviour, IEncounterAgent
    {
        [Tooltip("Which token pool this agent competes in.")]
        [SerializeField] private AttackTokenType tokenType = AttackTokenType.Melee;

        private EncounterDirectorBehaviour encounter;
        private bool hasJoined;

        public Vector3 Position => transform.position;
        public Transform Target => hasJoined ? encounter.Target : null;
        public bool HasToken => hasJoined && Director.HasToken(this);
        public Vector3 SlotPosition => hasJoined ? Director.GetSlotPosition(this) : transform.position;

        private EncounterDirector Director => encounter.Director;

        [Inject]
        internal void Construct([InjectOptional] EncounterDirectorBehaviour encounter) => this.encounter = encounter;

        public bool TryAcquireToken() => hasJoined && Director.TryAcquireToken(this);

        public void ReleaseToken()
        {
            if (hasJoined)
            {
                Director.ReleaseToken(this);
            }
        }

        internal void OnEnable()
        {
            bool isThereAnEncounter = encounter != null && encounter.Director != null;
            if (!isThereAnEncounter)
            {
                Debug.LogWarning($"[{nameof(EncounterAgent)}] '{name}' found no encounter director in the scene, so it will never be granted an attack token.", this);
                return;
            }

            Director.Register(this, tokenType);
            hasJoined = true;
        }

        internal void OnDisable()
        {
            if (!hasJoined)
            {
                return;
            }

            Director.Unregister(this);
            hasJoined = false;
        }
    }
}
