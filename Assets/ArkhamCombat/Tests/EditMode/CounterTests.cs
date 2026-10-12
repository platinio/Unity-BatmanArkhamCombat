using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using Zenject;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class CounterTests
    {
        private static readonly Vector3 Near = Vector3.forward * 2f;
        private static readonly Vector3 Far = Vector3.forward * 6f;

        private sealed class PickerThatKnowsWhoAttacks : IActionTargetPicker
        {
            public readonly List<Vector2> DirectionsAskedWith = new List<Vector2>();
            public int TimesAskedForTheAttacker;
            public IActionTarget WhereTheStickPoints = new PointTarget(Vector3.right);
            public IActionTarget Attacker = new PointTarget(Vector3.left);

            public IActionTarget PickTarget(Vector2 direction)
            {
                DirectionsAskedWith.Add(direction);
                return WhereTheStickPoints;
            }

            public IActionTarget PickAttackerToCounter()
            {
                TimesAskedForTheAttacker++;
                return Attacker;
            }
        }

        private sealed class OneAttacker : IIncomingAttacks
        {
            public ICombatTarget Attacker;

            public bool TryGetNearestAttackerThatCanBeCountered(Vector3 position, out ICombatTarget attacker)
            {
                attacker = Attacker;
                return attacker != null;
            }
        }

        private TestIntentKinds kinds;
        private AttackDefinition counter;
        private AttackDefinition jab;
        private PickerThatKnowsWhoAttacks picker;
        private EncounterDirector director;
        private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds();
            counter = Attack("Counter");
            counter.ConfigureAsAimedAtTheAttacker();
            jab = Attack("Jab");
            picker = new PickerThatKnowsWhoAttacks();
            director = new EncounterDirector(new EncounterDirectorSettings());
        }

        [TearDown]
        public void TearDown()
        {
            kinds.Destroy();
            Object.DestroyImmediate(counter);
            Object.DestroyImmediate(jab);
            foreach (GameObject created in objectsToDestroy)
            {
                Object.DestroyImmediate(created);
            }

            objectsToDestroy.Clear();
        }

        private ActionRunner ARunner() =>
            new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), kinds.InterruptKinds,
                new NullPresentationDriver(), new NullWarpMover(), new NullHitWindowListener(), picker);

        private StandingAgent AnAgentWindingUp(Vector3 position, bool isCounterable)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, AttackTokenType.Melee);
            director.TelegraphAnAttack(agent, isCounterable);
            return agent;
        }

        private EncounterAgent AThugInTheEncounterAt(Vector3 position, out EnemyStatus status)
        {
            GameObject encounterObject = HiddenObject("Encounter");
            objectsToDestroy.Add(encounterObject);
            EncounterDirectorBehaviour encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);

            GameObject thug = HiddenObject("Thug");
            objectsToDestroy.Add(thug);
            thug.transform.position = position;
            status = thug.AddComponent<EnemyStatus>();
            EncounterAgent agent = thug.AddComponent<EncounterAgent>();
            agent.Construct(encounter);
            agent.OnEnable();
            return agent;
        }

        [Test]
        public void AnAttackAimedAtTheAttacker_IsAimedAtWhoeverThePickerSaysAttacks()
        {
            ActionRunner runner = ARunner();

            runner.PlayAction(counter, Vector3.zero);

            Assert.AreSame(picker.Attacker, runner.CurrentActionTarget);
            Assert.IsEmpty(picker.DirectionsAskedWith, "the stick has no say in who a counter is aimed at");
        }

        [Test]
        public void AnOrdinaryAttack_IsAimedWhereTheStickPoints()
        {
            ActionRunner runner = ARunner();

            runner.PlayAction(jab, Vector3.zero);

            Assert.AreSame(picker.WhereTheStickPoints, runner.CurrentActionTarget);
            Assert.AreEqual(0, picker.TimesAskedForTheAttacker);
        }

        [Test]
        public void ACounterWithNobodyAttacking_PlaysAtNoTarget()
        {
            picker.Attacker = null;
            ActionRunner runner = ARunner();

            runner.PlayAction(counter, Vector3.zero);

            Assert.IsTrue(runner.IsPlaying);
            Assert.IsNull(runner.CurrentActionTarget);
        }

        [Test]
        public void APickerThatDoesNotKnowWhoAttacks_NamesNobody()
        {
            IActionTargetPicker pickerOfAnEnemy = new RecordingActionTargetPicker { TargetToGive = new PointTarget(Vector3.one) };

            Assert.IsNull(pickerOfAnEnemy.PickAttackerToCounter());
        }

        [Test]
        public void TheDirector_NamesTheNearestAgentWindingUpAnAttackThatCanBeCountered()
        {
            AnAgentWindingUp(Far, isCounterable: true);
            StandingAgent nearer = AnAgentWindingUp(Near, isCounterable: true);

            Assert.IsTrue(director.TryGetNearestAgentTelegraphingACounterableAttack(Vector3.zero, out IEncounterAgent nearest));
            Assert.AreSame(nearer, nearest);
        }

        [Test]
        public void AnAttackThatCannotBeCountered_IsNotNamed_HoweverNear()
        {
            AnAgentWindingUp(Near, isCounterable: false);
            StandingAgent counterable = AnAgentWindingUp(Far, isCounterable: true);

            Assert.IsTrue(director.TryGetNearestAgentTelegraphingACounterableAttack(Vector3.zero, out IEncounterAgent nearest));
            Assert.AreSame(counterable, nearest);
        }

        [Test]
        public void WithOnlyAnUnblockableAttackComing_ThereIsNobodyToCounter()
        {
            AnAgentWindingUp(Near, isCounterable: false);

            Assert.IsFalse(director.TryGetNearestAgentTelegraphingACounterableAttack(Vector3.zero, out IEncounterAgent nobody));
            Assert.IsNull(nobody);
        }

        [Test]
        public void TheRoster_HandsOverTheEnemyThatIsWindingUp_AsSomethingThatCanBeHit()
        {
            EncounterAgent agent = AThugInTheEncounterAt(Near, out EnemyStatus status);
            EncounterTargetRoster roster = new EncounterTargetRoster(director);
            agent.TelegraphAnAttack(isCounterable: true);

            Assert.IsTrue(roster.TryGetNearestAttackerThatCanBeCountered(Vector3.zero, out ICombatTarget attacker));
            Assert.AreSame(status, attacker);
        }

        [Test]
        public void TheRoster_NamesNobody_WhileNoEnemyWindsUp()
        {
            AThugInTheEncounterAt(Near, out EnemyStatus _);
            EncounterTargetRoster roster = new EncounterTargetRoster(director);

            Assert.IsFalse(roster.TryGetNearestAttackerThatCanBeCountered(Vector3.zero, out ICombatTarget attacker));
            Assert.IsNull(attacker);
        }

        [Test]
        public void ThePlayersTargeting_AimsACounterAtWhoeverIsAttacking()
        {
            GameObject player = HiddenObject("Player");
            objectsToDestroy.Add(player);
            CombatTargeting targeting = player.AddComponent<CombatTargeting>();
            PointCombatTarget attacker = new PointCombatTarget(Near);
            DiContainer container = new DiContainer();
            container.Bind<ITargetScorer>().FromInstance(new StandInTargetScorer(new TargetingSettings(), new EncounterTargetRoster(director)));
            container.Bind<ITargetingSettings>().FromInstance(new TargetingSettings());
            container.Bind<IMovementFrame>().FromInstance(new WorldMovementFrame());
            container.Bind<IIncomingAttacks>().FromInstance(new OneAttacker { Attacker = attacker });
            container.Inject(targeting);

            Assert.AreSame(attacker, targeting.PickAttackerToCounter());
        }
    }
}
