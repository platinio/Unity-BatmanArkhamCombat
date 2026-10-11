using System.Text.RegularExpressions;
using ArcaneOnyx.GameEventGenerator;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EnemyInstallerTests
    {
        private const float PressLifetimeSeconds = 10f;

        private TestIntentKinds kinds;
        private HitReceiverProfile profile;
        private EnemyArchetype archetype;
        private GameObject enemy;
        private EnemyInstaller installer;
        private DiContainer sceneContainer;
        private DiContainer enemyContainer;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            profile = ReceiverProfile();
            archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.Configure(profile);

            enemy = HiddenObject("Enemy");
            installer = enemy.AddComponent<EnemyInstaller>();

            sceneContainer = new DiContainer();
            sceneContainer.Bind<InterruptKinds>().FromInstance(kinds.InterruptKinds);
#if HERMES_EVENTS_GENERATED
            sceneContainer.Bind<ISceneGameEvents>().FromInstance(new SceneGameEventsWith(null));
#endif
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(profile);
            kinds.Destroy();
        }

        private void GiveTheEnemyItsArchetype()
        {
            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("archetype").objectReferenceValue = archetype;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        private void InstallTheEnemy()
        {
            enemyContainer = sceneContainer.CreateSubContainer();
            enemyContainer.Inject(installer);
            installer.InstallBindings();
        }

        [Test]
        public void BindsARunnerWithoutAStance()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();

            ActionRunner runner = enemyContainer.Resolve<ActionRunner>();

            Assert.IsNull(runner.CurrentNode, "an enemy has no combo to be in");
            Assert.IsFalse(runner.IsPlaying);
            Assert.AreSame(runner, enemyContainer.Resolve<ActionRunner>(), "one runner per enemy");
        }

        [Test]
        public void TheStartGateIsAlwaysOpen()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();

            Assert.IsTrue(enemyContainer.Resolve<IActionStartGate>().CanStartFromIdle);
        }

        [Test]
        public void BindsTheArchetypeAndItsProfile()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();

            Assert.AreSame(archetype, enemyContainer.Resolve<EnemyArchetype>());
            Assert.AreSame(profile, enemyContainer.Resolve<HitReceiverProfile>());
        }

        [Test]
        public void AnEnemyWithoutABody_GetsTheDriverThatDoesNothing()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();

            Assert.IsInstanceOf<NullPresentationDriver>(enemyContainer.Resolve<IPresentationDriver>());
        }

        [Test]
        public void TheWarpMoverIsOneObjectUnderBothNames()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();

            Assert.AreSame(enemyContainer.Resolve<MotorWarpMover>(), enemyContainer.Resolve<IWarpMover>());
        }

        [Test]
        public void WithoutAnArchetype_LogsAnError_AndBindsNothing()
        {
            LogAssert.Expect(LogType.Error, new Regex("No archetype assigned"));

            InstallTheEnemy();

            Assert.IsFalse(enemyContainer.HasBinding<ActionRunner>());
        }
    }
}
