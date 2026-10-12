using System.Text.RegularExpressions;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;

namespace ArkhamCombat.Tests
{
    // Installed beside the encounter's own installer, as a scene does it: neither is complete without the other.
    public class CombatStaticInstallerTests
    {
        private CombatConfig config;
        private CombatStaticInstaller combatInstaller;
        private EncounterStaticInstaller encounterInstaller;
        private DiContainer container;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CombatConfig>();
            combatInstaller = ScriptableObject.CreateInstance<CombatStaticInstaller>();
            encounterInstaller = ScriptableObject.CreateInstance<EncounterStaticInstaller>();

            SerializedObject serializedInstaller = new SerializedObject(combatInstaller);
            serializedInstaller.FindProperty("config").objectReferenceValue = config;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();

            container = new DiContainer();
            container.Inject(combatInstaller);
            container.Inject(encounterInstaller);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(combatInstaller);
            Object.DestroyImmediate(encounterInstaller);
            Object.DestroyImmediate(config);
        }

        private void InstallBoth()
        {
            LogAssert.Expect(LogType.Error, new Regex("has no evade kind"));
            LogAssert.Expect(LogType.Error, new Regex("has no counter kind"));

            combatInstaller.InstallBindings();
            encounterInstaller.InstallBindings();
        }

        [Test]
        public void TheDirector_IsTunedByTheEncounterSectionOfTheConfig()
        {
            InstallBoth();

            Assert.AreSame(config.Encounter, container.Resolve<EncounterDirector>().Settings);
        }

        [Test]
        public void TheEncountersFront_IsWhereTheMovementFrameLooks()
        {
            container.Bind<ArcaneOnyx.TPCharacterController.Movement.IMovementFrame>()
                .FromInstance(new ArcaneOnyx.TPCharacterController.Movement.WorldMovementFrame());
            InstallBoth();

            Assert.IsInstanceOf<MovementFrameTargetForward>(container.Resolve<ITargetForwardSource>());
        }

        [Test]
        public void ThePlayerStrikesWhoeverIsInTheEncounter()
        {
            InstallBoth();
            GameObject encounterObject = CombatTestDoubles.HiddenObject("Encounter");
            GameObject thug = CombatTestDoubles.HiddenObject("Thug");

            try
            {
                EncounterDirectorBehaviour encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
                encounter.Construct(container.Resolve<EncounterDirector>());
                EnemyStatus status = thug.AddComponent<EnemyStatus>();
                EncounterAgent agent = thug.AddComponent<EncounterAgent>();
                agent.Construct(encounter);
                ITargetRoster roster = container.Resolve<ITargetRoster>();

                agent.OnEnable();

                CollectionAssert.AreEqual(new ICombatTarget[] { status }, roster.Targets);
            }
            finally
            {
                Object.DestroyImmediate(thug);
                Object.DestroyImmediate(encounterObject);
            }
        }

        [Test]
        public void WithoutACombatConfig_TheDirectorIsTunedByTheDefaults()
        {
            Assert.IsNotNull(CombatStaticInstaller.EncounterSettingsOf(null));
        }
    }
}
