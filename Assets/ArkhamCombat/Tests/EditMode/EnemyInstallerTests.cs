using System.Text.RegularExpressions;
using ArcaneOnyx.GameEventGenerator;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
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
        private const float IdleMin = 0.3f;
        private const float IdleMax = 0.9f;
        private const float Tolerance = 1e-5f;

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
            archetype.name = "TestArchetype";
            archetype.Configure(profile, IdleMin, IdleMax);

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

        private void InstallTheEnemyWithItsArchetype()
        {
            GiveTheEnemyItsArchetype();
            InstallTheEnemy();
        }

        private T AddToTheEnemyAndInject<T>() where T : Component
        {
            T component = enemy.AddComponent<T>();
            enemyContainer.Inject(component);
            return component;
        }

        [Test]
        public void BindsARunnerWithoutAStance()
        {
            InstallTheEnemyWithItsArchetype();

            ActionRunner runner = enemyContainer.Resolve<ActionRunner>();

            Assert.IsNull(runner.CurrentNode, "an enemy has no combo to be in");
            Assert.IsFalse(runner.IsPlaying);
            Assert.AreSame(runner, enemyContainer.Resolve<ActionRunner>(), "one runner per enemy");
        }

        [Test]
        public void AnEnemyWithNothingOptional_GetsTheVersionsThatDoNothing()
        {
            InstallTheEnemyWithItsArchetype();

            Assert.IsTrue(enemyContainer.Resolve<IActionStartGate>().CanStartFromIdle);
            Assert.IsInstanceOf<NullPresentationDriver>(enemyContainer.Resolve<IPresentationDriver>());
            Assert.IsInstanceOf<NullActionTargetPicker>(enemyContainer.Resolve<IActionTargetPicker>());
            Assert.IsInstanceOf<NullHitWindowListener>(enemyContainer.Resolve<IHitWindowListener>());
        }

        [Test]
        public void BindsTheArchetypeAndItsProfile()
        {
            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(archetype, enemyContainer.Resolve<EnemyArchetype>());
            Assert.AreSame(profile, enemyContainer.Resolve<HitReceiverProfile>());
        }

        [Test]
        public void TheWarpMoverIsOneObjectUnderBothNames()
        {
            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(enemyContainer.Resolve<MotorWarpMover>(), enemyContainer.Resolve<IWarpMover>());
        }

        [Test]
        public void TheEnemysCombatActions_GetTheEnemysRunner()
        {
            InstallTheEnemyWithItsArchetype();

            CombatActions combatActions = AddToTheEnemyAndInject<CombatActions>();

            Assert.AreSame(enemyContainer.Resolve<ActionRunner>(), combatActions.Runner);
        }

        [Test]
        public void TheEnemysStatus_GetsTheArchetypesProfile_AndCanBeHit()
        {
            InstallTheEnemyWithItsArchetype();

            EnemyStatus status = AddToTheEnemyAndInject<EnemyStatus>();
            status.Awake();

            Assert.IsTrue(status.IsValid);
        }

        [Test]
        public void TheEnemysArchetypeWriter_WritesTheArchetypesIdleRange()
        {
            InstallTheEnemyWithItsArchetype();
            enemy.AddComponent<Variables>();

            AddToTheEnemyAndInject<EnemyArchetypeWriter>().WriteTunables();

            Assert.AreEqual(IdleMin, (float)Variables.Object(enemy).Get(EnemyVariableNames.IdleMin), Tolerance);
        }

        [Test]
        public void WithoutAnArchetype_ItSaysSo_AndTheEnemyIsStillBuilt()
        {
            LogAssert.Expect(LogType.Error, new Regex("No archetype assigned"));

            InstallTheEnemy();

            Assert.IsNotNull(enemyContainer.Resolve<ActionRunner>());
            Assert.DoesNotThrow(() => AddToTheEnemyAndInject<EnemyArchetypeWriter>(), "what needs the archetype goes without it");
        }

        [Test]
        public void AnArchetypeWithoutAProfile_IsReported()
        {
            archetype.Configure(null, IdleMin, IdleMax);
            LogAssert.Expect(LogType.Error, new Regex("names no hit receiver profile"));

            InstallTheEnemyWithItsArchetype();

            Assert.IsFalse(enemyContainer.HasBinding<HitReceiverProfile>());
        }

        [Test]
        public void AnIdleRangeTheWrongWayRound_IsReported()
        {
            archetype.Configure(profile, idleMin: IdleMax, idleMax: IdleMin);
            LogAssert.Expect(LogType.Error, new Regex("idleMax .* is below idleMin"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void ACombatRangeThatDoesNotReachPastTheRing_IsReported()
        {
            EncounterDirectorSettings ringOutToFive = new EncounterDirectorSettings(slotRadius: 4f, slotSampleRadius: 1f);
            sceneContainer.Bind<EncounterDirectorSettings>().FromInstance(ringOutToFive);
            archetype.ConfigureMovement(combatRange: 5f, moveSpeed: 3f);
            LogAssert.Expect(LogType.Error, new Regex("does not reach past the ring"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void ACombatRangeWiderThanTheRing_IsNotReported()
        {
            EncounterDirectorSettings ringOutToFour = new EncounterDirectorSettings(slotRadius: 3f, slotSampleRadius: 1f);
            sceneContainer.Bind<EncounterDirectorSettings>().FromInstance(ringOutToFour);
            archetype.ConfigureMovement(combatRange: 5f, moveSpeed: 3f);

            InstallTheEnemyWithItsArchetype();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnArchetypeThatCannotWalk_IsReported()
        {
            archetype.ConfigureMovement(combatRange: 5f, moveSpeed: 0f);
            LogAssert.Expect(LogType.Error, new Regex("has no move speed"));

            InstallTheEnemyWithItsArchetype();
        }
    }
}
