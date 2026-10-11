using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Tests
{
    public class EncounterStaticInstallerTests
    {
        private EncounterStaticInstaller installer;
        private DiContainer container;
        private EncounterDirectorSettings settings;

        [SetUp]
        public void SetUp()
        {
            container = new DiContainer();
            settings = new EncounterDirectorSettings(slotCount: 5);
            container.Bind<EncounterDirectorSettings>().FromInstance(settings);

            installer = ScriptableObject.CreateInstance<EncounterStaticInstaller>();
            container.Inject(installer);
            installer.InstallBindings();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(installer);
        }

        [Test]
        public void TheDirector_IsBuiltWithTheSettingsTheGameBound()
        {
            Assert.AreSame(settings, container.Resolve<EncounterDirector>().Settings);
        }

        [Test]
        public void TheSceneHasOneDirector()
        {
            Assert.AreSame(container.Resolve<EncounterDirector>(), container.Resolve<EncounterDirector>());
        }

        [Test]
        public void SlotsAreValidatedAgainstTheNavMesh()
        {
            Assert.IsInstanceOf<NavMeshSlotValidator>(container.Resolve<ISlotValidator>());
        }
    }
}
