using System;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Encounter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ArkhamCombat/Encounter Agent")]
    public sealed class EncounterAgent : MonoBehaviour, IEncounterAgent
    {
        private const AttackTokenType TokenTypeWithoutSettings = AttackTokenType.Melee;

        private EncounterDirectorBehaviour encounter;
        private IEncounterAgentSettings settings;
        private bool wasInjected;
        private bool hasJoined;

        public event Action TokenGranted;
        public event Action TokenReleased;

        public Vector3 Position => transform.position;
        public Transform Target => hasJoined ? encounter.Target : null;
        public bool HasToken => hasJoined && Director.HasToken(this);
        public Vector3 SlotPosition => hasJoined ? Director.GetSlotPosition(this) : transform.position;

        private EncounterDirector Director => encounter.Director;

        [Inject]
        internal void Construct(
            [InjectOptional] EncounterDirectorBehaviour encounter,
            [InjectOptional] IEncounterAgentSettings settings = null)
        {
            this.encounter = encounter;
            this.settings = settings;
            wasInjected = true;
        }

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
                Debug.LogWarning($"[{nameof(EncounterAgent)}] '{name}' {WhyItJoinsNoEncounter()}, so it will never be granted an attack token.", this);
                return;
            }

            // Joined before it registers: whoever hears the director announce it can already ask it for its target.
            hasJoined = true;
            Director.TokenGranted += RaiseTokenGrantedWhenItIsMine;
            Director.TokenReleased += RaiseTokenReleasedWhenItIsMine;
            Director.Register(this, TokenType);
        }

        private AttackTokenType TokenType => settings != null ? settings.TokenType : TokenTypeWithoutSettings;

        private string WhyItJoinsNoEncounter() =>
            wasInjected
                ? "found no encounter director in the scene"
                : "was never injected, so it could not look for an encounter director (was it instantiated without the container?)";

        internal void OnDisable()
        {
            if (!hasJoined)
            {
                return;
            }

            Director.Unregister(this);
            Director.TokenGranted -= RaiseTokenGrantedWhenItIsMine;
            Director.TokenReleased -= RaiseTokenReleasedWhenItIsMine;
            hasJoined = false;
        }

        private void RaiseTokenGrantedWhenItIsMine(IEncounterAgent holder)
        {
            if (ReferenceEquals(holder, this))
            {
                TokenGranted?.Invoke();
            }
        }

        private void RaiseTokenReleasedWhenItIsMine(IEncounterAgent formerHolder)
        {
            if (ReferenceEquals(formerHolder, this))
            {
                TokenReleased?.Invoke();
            }
        }
    }
}
