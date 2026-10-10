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
            if (profile == null)
            {
                Debug.LogError($"[{nameof(PlayerStaticInstaller)}] No CharacterProfile assigned on '{name}'.", this);
            }

            Container.Bind<CharacterProfile>().FromInstance(profile);
            Container.Bind<InputConfig>().FromResolveGetter<CharacterProfile>(p => p.Input);

            Container.Bind<CharacterBrain>().FromComponentInHierarchy().AsSingle();
            Container.Bind<CharacterMotor>().FromComponentInHierarchy().AsSingle();
            Container.Bind<CombatActions>().FromComponentInHierarchy().AsSingle();

            Container.Bind(typeof(PlayerInputReader), typeof(ICharacterInput))
                .To<PlayerInputReader>().AsSingle();

            Container.Bind<CharacterStateMachine>().AsSingle();
            Container.Bind<CharacterContext>().AsSingle();

            foreach (ICharacterStateBinding binding in states)
            {
                binding?.Install(Container);
            }
        }
    }
}
