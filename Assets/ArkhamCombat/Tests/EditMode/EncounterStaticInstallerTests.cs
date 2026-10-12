using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Tests
{
    public class EncounterStaticInstallerTests
    {
        private EncounterStaticInstaller installer;
        private DiContainer container;

        [SetUp]
        public void SetUp()
        {
            container = new DiContainer();
            installer = ScriptableObject.CreateInstance<EncounterStaticInstaller>();
            container.Inject(installer);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(installer);
        }

        [Test]
        public void TheDirector_IsBuiltWithTheSettingsTheGameBound()
        {
            EncounterDirectorSettings settings = new EncounterDirectorSettings(slotCount: 5);
            container.Bind<EncounterDirectorSettings>().FromInstance(settings);

            installer.InstallBindings();

            Assert.AreSame(settings, container.Resolve<EncounterDirector>().Settings);
        }

        [Test]
        public void TheSceneHasOneDirector()
        {
            container.Bind<EncounterDirectorSettings>().FromInstance(new EncounterDirectorSettings());

            installer.InstallBindings();

            Assert.AreSame(container.Resolve<EncounterDirector>(), container.Resolve<EncounterDirector>());
        }

        [Test]
        public void TheGamesSettings_AreTheOnesOnTheCombatConfig()
        {
            CombatConfig config = ScriptableObject.CreateInstance<CombatConfig>();

            try
            {
                Assert.AreSame(config.Encounter, CombatStaticInstaller.EncounterSettingsOf(config));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void WithoutACombatConfig_TheDirectorStillGetsSettings()
        {
            Assert.IsNotNull(CombatStaticInstaller.EncounterSettingsOf(null));
        }
    }
}
