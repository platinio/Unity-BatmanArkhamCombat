using ArcaneOnyx.TPCharacterController.CameraRig;
using ArcaneOnyx.TPCharacterController.Movement;
using ArcaneOnyx.UnityExtensions;
using Zenject;

namespace ArkhamCombat.Cameras
{
    /// <summary>
    /// The movement frame is built on the rig's pivot so the character's "forward" is whatever the
    /// camera is looking along.
    /// </summary>
    [AutoAssetGeneration("Installers/Static", "CameraStaticInstaller")]
    [StaticInstaller(StaticInstallerExecutionOrder.Normal)]
    public sealed class CameraStaticInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<PlayerCameraRig>().FromComponentInHierarchy().AsSingle();

            Container.Bind<IMovementFrame>()
                .FromMethod(context => new TransformMovementFrame(
                    context.Container.Resolve<PlayerCameraRig>().Pivot))
                .AsSingle();
        }
    }
}
