using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Configuration;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArcaneOnyx.UnityExtensions;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The profile and the state set are picked on this asset in the inspector, so changing the
    /// character's feel or what it can do is an asset change, not a scene or code edit.
    /// </summary>
    [AutoAssetGeneration("Installers/Static", "PlayerStaticInstaller")]
    [StaticInstaller(StaticInstallerExecutionOrder.Normal)]
    public sealed class PlayerStaticInstaller : ScriptableObjectInstaller
    {
        [Tooltip("Every tunable value for the player character. Swap the asset to swap the feel.")]
        [SerializeField] private CharacterProfile profile;

        [Tooltip("The states this character can be in. Each entry binds one state type; the " +
                 "container builds the state with the character's context.")]
        [SerializeReference, SubclassSelector]
        private List<ICharacterStateBinding> states = new List<ICharacterStateBinding>();

        public override void InstallBindings()
        {
            ReportAuthoringErrors();

            BindProfile();
            BindPlayerParts();
            BindInput();
            BindStateMachine();
            BindStates();
        }

        private void ReportAuthoringErrors()
        {
            if (profile == null)
            {
                Debug.LogError($"[{nameof(PlayerStaticInstaller)}] No CharacterProfile assigned on '{name}'.", this);
            }

            if (states.Count == 0)
            {
                Debug.LogError($"[{nameof(PlayerStaticInstaller)}] No states listed on '{name}', so the character has nothing to be in.", this);
            }

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] == null)
                {
                    Debug.LogError($"[{nameof(PlayerStaticInstaller)}] State {i} on '{name}' is empty and binds nothing.", this);
                }
            }
        }

        private void BindProfile()
        {
            Container.Bind<CharacterProfile>().FromInstance(profile);
            Container.Bind<InputConfig>().FromResolveGetter<CharacterProfile>(characterProfile => characterProfile.Input);
        }

        // Found once in the hierarchy: one player per scene.
        private void BindPlayerParts()
        {
            Container.Bind<CharacterBrain>().FromComponentInHierarchy().AsSingle();
            Container.Bind<CharacterMotor>().FromComponentInHierarchy().AsSingle();
            Container.Bind<CombatActions>().FromComponentInHierarchy().AsSingle();
        }

        private void BindInput()
        {
            Container.Bind(typeof(PlayerInputReader), typeof(ICharacterInput)).To<PlayerInputReader>().AsSingle();
        }

        private void BindStateMachine()
        {
            Container.Bind<CharacterStateMachine>().AsSingle();
            Container.Bind<CharacterContext>().AsSingle();
        }

        private void BindStates()
        {
            foreach (ICharacterStateBinding binding in states)
            {
                binding?.Install(Container);
            }
        }
    }
}
