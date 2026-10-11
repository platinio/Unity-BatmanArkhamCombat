using ArcaneOnyx.UnityExtensions;
using Zenject;

namespace ArkhamCombat.Encounter
{
    [AutoAssetGeneration("Installers/Static", "EncounterStaticInstaller")]
    [StaticInstaller(StaticInstallerExecutionOrder.Normal)]
    public sealed class EncounterStaticInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<ISlotValidator>().To<NavMeshSlotValidator>().AsSingle();
            Container.Bind<EncounterDirector>().AsSingle();
            Container.Bind<EncounterDirectorBehaviour>().FromComponentInHierarchy().AsSingle();
        }
    }
}
