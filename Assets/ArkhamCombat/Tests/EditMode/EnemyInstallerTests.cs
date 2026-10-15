using System.Text.RegularExpressions;
using ArcaneOnyx.GameEventGenerator;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Presentation;
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

        private static readonly Vector3 FarFromTheOrigin = new Vector3(20f, 0f, 0f);
        private static readonly Window AWindowThatEndsBeforeItStarts = new Window(0.6f, 0.4f);

        private TestIntentKinds kinds;
        private HitReceiverProfile profile;
        private EnemyArchetype archetype;
        private AttackDefinition haymaker;
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
            haymaker = Attack("Haymaker");
            haymaker.ConfigureTelegraphWindow(new Window(0f, 0.2f));
            archetype.ConfigureAttack(haymaker);

            enemy = HiddenObject("Enemy");
            installer = enemy.AddComponent<EnemyInstaller>();

            sceneContainer = new DiContainer();
            sceneContainer.Bind<InterruptKinds>().FromInstance(kinds.InterruptKinds);
            sceneContainer.Bind<IHitCheckSettings>().FromInstance(new HitCheckSettings());
#if HERMES_EVENTS_GENERATED
            sceneContainer.Bind<ISceneGameEvents>().FromInstance(new SceneGameEventsWith(null));
#endif
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(haymaker);
            Object.DestroyImmediate(profile);
            kinds.Destroy();
        }

        private void GiveTheEnemyItsArchetype()
        {
            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("archetype").objectReferenceValue = archetype;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        // The way a context does it, so anything Zenject warns about during an install is warned about here too.
        private void InstallTheEnemy()
        {
            enemyContainer = sceneContainer.CreateSubContainer();
            enemyContainer.IsInstalling = true;
            enemyContainer.Inject(installer);
            installer.InstallBindings();
            enemyContainer.IsInstalling = false;
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
            Assert.IsInstanceOf<NullTravelDirectionPicker>(enemyContainer.Resolve<ITravelDirectionPicker>());
        }

        [Test]
        public void AnEnemyThatWalks_HasItsAgentAsLocomotionSignal_AndOneThatDoesNotStandsStill()
        {
            InstallTheEnemyWithItsArchetype();
            Assert.IsInstanceOf<StillLocomotionSignal>(enemyContainer.Resolve<ILocomotionSignal>());

            enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
            InstallTheEnemyWithItsArchetype();

            Assert.IsInstanceOf<NavMeshAgentLocomotionSignal>(enemyContainer.Resolve<ILocomotionSignal>());
        }

        [Test]
        public void ADriverOnADeactivatedBody_IsNotTheOneBound_TheActiveBodysIs()
        {
            ProceduralPresentationDriver driverOnTheCapsule = DriverOnAChildCalled("Capsule");
            driverOnTheCapsule.gameObject.SetActive(false);
            ProceduralPresentationDriver driverOnTheModel = DriverOnAChildCalled("Model");

            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(driverOnTheModel, enemyContainer.Resolve<IPresentationDriver>(), "a variant swaps the picture by deactivating the capsule child");
        }

        private ProceduralPresentationDriver DriverOnAChildCalled(string childName)
        {
            GameObject body = HiddenObject(childName);
            body.transform.SetParent(enemy.transform);
            return body.AddComponent<ProceduralPresentationDriver>();
        }

        [Test]
        public void AnEnemysStrike_IsMeasuredFromThatEnemy()
        {
            enemy.transform.position = FarFromTheOrigin;
            InstallTheEnemyWithItsArchetype();
            PointCombatTarget targetInFrontOfTheEnemy = new PointCombatTarget(FarFromTheOrigin + Vector3.forward);

            enemyContainer.Resolve<IHitWindowListener>().HitWindowOpened(haymaker, targetInFrontOfTheEnemy);

            Assert.AreEqual(1, targetInFrontOfTheEnemy.HitsTaken);
        }

        [Test]
        public void AnEnemyWithATargetPickerOfItsOwn_AimsItsActionsWithIt()
        {
            enemy.AddComponent<EncounterAgent>();
            EncounterTargetPicker pickerOnTheEnemy = enemy.AddComponent<EncounterTargetPicker>();
            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(pickerOnTheEnemy, enemyContainer.Resolve<IActionTargetPicker>());
        }

        [Test]
        public void AnArchetypeThatNamesNoAttack_IsReported()
        {
            archetype.ConfigureAttack(null);
            LogAssert.Expect(LogType.Error, new Regex("names no attack"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void AnArchetypeWhoseAttackIsBroken_IsReported()
        {
            haymaker.ConfigureTelegraphWindow(AWindowThatEndsBeforeItStarts);
            LogAssert.Expect(LogType.Error, new Regex("telegraphWindow"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void AnArchetypeWhoseAttackGivesNoWarning_IsReported()
        {
            haymaker.ConfigureTelegraphWindow(new Window(0f, 0f));
            LogAssert.Expect(LogType.Error, new Regex("has no telegraph window"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void AnArchetypeWhoseAttackStillTelegraphsAsItsHitWindowOpens_IsReported()
        {
            haymaker.ConfigureTelegraphWindow(new Window(0f, haymaker.HitWindow.Start + 0.1f));
            LogAssert.Expect(LogType.Error, new Regex("The telegraph has to end by then"));

            InstallTheEnemyWithItsArchetype();
        }

        [Test]
        public void AnAttackThatTelegraphsRightUpToItsHit_IsFine()
        {
            haymaker.ConfigureTelegraphWindow(new Window(0f, haymaker.HitWindow.Start));

            InstallTheEnemyWithItsArchetype();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BindsTheArchetypeAndItsProfile()
        {
            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(archetype, enemyContainer.Resolve<EnemyArchetype>());
            Assert.AreSame(profile, enemyContainer.Resolve<HitReceiverProfile>());
        }

        [Test]
        public void TheEnemysEncounterAgent_WaitsForTheKindOfTokenItsArchetypeNames()
        {
            archetype.ConfigureTokenType(AttackTokenType.Ranged);
            InstallTheEnemyWithItsArchetype();

            IEncounterAgentSettings agentSettings = enemyContainer.Resolve<IEncounterAgentSettings>();

            Assert.AreSame(archetype, agentSettings);
            Assert.AreEqual(AttackTokenType.Ranged, agentSettings.TokenType);
        }

        [Test]
        public void ThePendingWarpMovementIsOneObjectUnderBothNames()
        {
            InstallTheEnemyWithItsArchetype();

            Assert.AreSame(enemyContainer.Resolve<PendingWarpMovement>(), enemyContainer.Resolve<IWarpMover>());
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
            Assert.DoesNotThrow(() => AddToTheEnemyAndInject<EncounterAgent>(), "and so does its encounter agent");
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
        public void InstallingAnEnemyInAnEncounter_AsksTheContainerForNothing()
        {
            EncounterDirectorSettings ringOutToFour = new EncounterDirectorSettings(slotRadius: 3f, slotSampleRadius: 1f);
            sceneContainer.Bind<EncounterDirectorSettings>().FromInstance(ringOutToFour);
            archetype.ConfigureMovement(combatRange: 5f, moveSpeed: 3f);
            string zenjectWarning = null;
            Application.LogCallback rememberAZenjectWarning = (message, stackTrace, type) =>
            {
                if (type == LogType.Warning && message.Contains("Zenject Warning"))
                {
                    zenjectWarning = message;
                }
            };

            Application.logMessageReceived += rememberAZenjectWarning;
            try
            {
                InstallTheEnemyWithItsArchetype();
            }
            finally
            {
                Application.logMessageReceived -= rememberAZenjectWarning;
            }

            Assert.IsNull(zenjectWarning, "resolving while the bindings are still being made is what Zenject warns about");
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
