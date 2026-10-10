using System.Text.RegularExpressions;
#if HERMES_EVENTS_GENERATED
using ArcaneOnyx.GameEventGenerator;
#endif
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.Movement;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The scene container and the character's own are built by hand, the way the SceneContext and the
    // GameObjectContext build them: what is shared is bound in the scene's, and the installer binds
    // the character's into a container under it.
    public class CombatCharacterInstallerTests
    {
        private const float TickSeconds = 0.1f;
        private const float PressLifetimeSeconds = 10f;

        private TestIntentKinds kinds;
        private AttackDefinition jab;
        private Stance stance;
        private FunctionGraphAsset function;
        private CombatConfig config;
        private StillCharacterInput input;
        private GameObject character;
        private GameObject anotherCharacter;
        private CombatCharacterInstaller installer;
        private DiContainer sceneContainer;
        private DiContainer characterContainer;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            jab = Attack("Jab");
            function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            config = ScriptableObject.CreateInstance<CombatConfig>();
            input = new StillCharacterInput();

            // Adding the installer brings the character brain and its motor with it.
            character = HiddenObject("Character");
            installer = character.AddComponent<CombatCharacterInstaller>();
            anotherCharacter = HiddenObject("Another character");

            sceneContainer = new DiContainer();
            sceneContainer.Bind<CombatConfig>().FromInstance(config);
            sceneContainer.Bind<ICombatFactsSettings>().FromInstance(config);
            sceneContainer.Bind<IHitRangeSettings>().FromInstance(config);
            sceneContainer.Bind<InterruptKinds>().FromInstance(kinds.InterruptKinds);
            sceneContainer.Bind<ICharacterInput>().FromInstance(input);
            sceneContainer.Bind<IMovementFrame>().FromInstance(new WorldMovementFrame());
            sceneContainer.Bind<CharacterMotor>().FromInstance(anotherCharacter.AddComponent<CharacterMotor>());
#if HERMES_EVENTS_GENERATED
            sceneContainer.Bind<ISceneGameEvents>().FromInstance(new SceneGameEventsWith(null));
#endif
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(anotherCharacter);
            Object.DestroyImmediate(stance);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(function);
            Object.DestroyImmediate(config);
            kinds.Destroy();
        }

        private Edge StrikeEdgeWithACondition(string destinationId)
        {
            Edge edge = new Edge(kinds.Strike, destinationId);
            edge.Condition.SetFunction(function);
            return edge;
        }

        private void GiveTheCharacterAStance(Edge edgeOutOfTheRoot, params Edge[] globalEdges)
        {
            stance = Stance("Neutral",
                new[]
                {
                    NodeWithNothingToPlay("Neutral", edgeOutOfTheRoot),
                    new ChainNode("S1", jab, new Edge(kinds.Strike, "S1"))
                },
                globalEdges);

            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("stance").objectReferenceValue = stance;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        private void GiveTheCharacterAStanceWithoutConditions() => GiveTheCharacterAStance(new Edge(kinds.Strike, "S1"));

        private void InstallTheCharacter()
        {
            characterContainer = sceneContainer.CreateSubContainer();
            characterContainer.Inject(installer);
            installer.InstallBindings();
        }

        [Test]
        public void AStanceWithoutConditions_GetsTheEvaluatorThatNeverRunsAFunction()
        {
            GiveTheCharacterAStanceWithoutConditions();

            InstallTheCharacter();

            Assert.AreEqual(typeof(AlwaysConditionEvaluator), characterContainer.ResolveType<IConditionEvaluator>());
        }

        [Test]
        public void AConditionOnANodesEdge_GetsTheFunctionEvaluator()
        {
            GiveTheCharacterAStance(StrikeEdgeWithACondition("S1"));

            InstallTheCharacter();

            Assert.AreEqual(typeof(FunctionConditionEvaluator), characterContainer.ResolveType<IConditionEvaluator>());
        }

        [Test]
        public void AConditionOnAGlobalEdge_GetsTheFunctionEvaluator()
        {
            GiveTheCharacterAStance(new Edge(kinds.Strike, "S1"), StrikeEdgeWithACondition("S1"));

            InstallTheCharacter();

            Assert.AreEqual(typeof(FunctionConditionEvaluator), characterContainer.ResolveType<IConditionEvaluator>());
        }

        [Test]
        public void ACharacterWithNothingOptional_GetsTheVersionsThatDoNothing()
        {
            GiveTheCharacterAStanceWithoutConditions();

            InstallTheCharacter();

            Assert.IsInstanceOf<NullActionTargetPicker>(characterContainer.Resolve<IActionTargetPicker>());
            Assert.IsInstanceOf<NullPresentationDriver>(characterContainer.Resolve<IPresentationDriver>());
            Assert.IsInstanceOf<AlwaysOpenActionStartGate>(characterContainer.Resolve<IActionStartGate>());
            Assert.IsNotNull(characterContainer.Resolve<CombatFacts>());
            Assert.IsNull(characterContainer.TryResolve<CombatFactsUpdater>());
        }

        [Test]
        public void TheOptionalComponentsOnTheCharacter_AreTheOnesBound()
        {
            GiveTheCharacterAStanceWithoutConditions();
            CombatTargeting targeting = character.AddComponent<CombatTargeting>();
            CombatFactsUpdater factsUpdater = character.AddComponent<CombatFactsUpdater>();
            AttackingStateSwitch stateSwitch = character.AddComponent<AttackingStateSwitch>();
            ProceduralPresentationDriver driver = character.AddComponent<ProceduralPresentationDriver>();

            InstallTheCharacter();

            Assert.AreSame(targeting, characterContainer.Resolve<IActionTargetPicker>());
            Assert.AreSame(factsUpdater, characterContainer.Resolve<CombatFactsUpdater>());
            Assert.AreSame(factsUpdater.Facts, characterContainer.Resolve<CombatFacts>());
            Assert.AreSame(stateSwitch, characterContainer.Resolve<IActionStartGate>());
            Assert.AreSame(driver, characterContainer.Resolve<IPresentationDriver>());
        }

        [Test]
        public void TheCharactersOwnMotorIsBound_NotTheOneTheSceneFound()
        {
            GiveTheCharacterAStanceWithoutConditions();

            InstallTheCharacter();

            Assert.AreSame(character.GetComponent<CharacterMotor>(), characterContainer.Resolve<CharacterMotor>());
        }

#if HERMES_EVENTS_GENERATED
        [Test]
        public void ACharacterWithoutAStartGate_StartsAnActionFromAPress()
        {
            GiveTheCharacterAStanceWithoutConditions();
            CombatActions combatActions = character.AddComponent<CombatActions>();
            InstallTheCharacter();
            characterContainer.Inject(combatActions);
            input.Intents.Push(kinds.Strike, Vector2.zero);

            combatActions.Tick(TickSeconds);

            Assert.IsTrue(combatActions.Runner.IsPlaying);
        }

        [Test]
        public void TheRunnerIsBuilt_WhenItsConditionEvaluatorNeedsTheFactsUpdaterThatNeedsTheRunner()
        {
            GiveTheCharacterAStance(StrikeEdgeWithACondition("S1"));
            CombatFactsUpdater factsUpdater = character.AddComponent<CombatFactsUpdater>();
            CombatActions combatActions = character.AddComponent<CombatActions>();
            InstallTheCharacter();

            // The Function is an empty graph, which the evaluator reports when it is built.
            LogAssert.Expect(LogType.Error, new Regex("TestStance"));

            // The updater is still waiting to be injected, as it can be in the character's context,
            // when the first component to ask for the runner is injected.
            characterContainer.QueueForInject(factsUpdater);
            characterContainer.Inject(combatActions);
            characterContainer.ResolveRoots();

            Assert.IsNotNull(combatActions.Runner);
        }
#endif
    }
}
