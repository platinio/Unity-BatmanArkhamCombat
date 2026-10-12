using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The attack lasts one second and telegraphs over its first half, so seconds played and
    // normalized time are the same number.
    public class TelegraphAnnouncerTests
    {
        private const float AttackSeconds = 1f;
        private const float TickSeconds = 0.1f;

        private static readonly Window TelegraphWindow = new Window(0f, 0.5f);

        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject thug;
        private EncounterAgent agent;
        private CombatActions combatActions;
        private TelegraphAnnouncer announcer;
        private VariableDeclarations facts;
        private AttackDefinition haymaker;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings());
            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);

            haymaker = Attack("Haymaker", AttackSeconds);
            haymaker.ConfigureTelegraphWindow(TelegraphWindow);

            thug = HiddenObject("Thug");
            PendingWarpMovement pendingWarpMovement = new PendingWarpMovement();
            ActionRunner runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), pendingWarpMovement, new NullHitWindowListener());
            combatActions = thug.AddComponent<CombatActions>();
            combatActions.Construct(runner, pendingWarpMovement, new AlwaysOpenActionStartGate(), null);
            agent = thug.AddComponent<EncounterAgent>();
            announcer = thug.AddComponent<TelegraphAnnouncer>();
            facts = Variables.Object(thug);

            agent.Construct(encounter);
            agent.OnEnable();
            announcer.Awake();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(encounterObject);
            Object.DestroyImmediate(haymaker);
        }

        private bool IsTheTreeToldItTelegraphs => (bool)facts.Get(EnemyVariableNames.Telegraphing);

        private void StartTheAttack() => combatActions.Runner.PlayAction(haymaker, Vector3.zero);

        private void TickTheThugFor(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / TickSeconds);
            for (int i = 0; i < ticks; i++)
            {
                combatActions.Tick(TickSeconds);
                announcer.Tick(TickSeconds);
            }
        }

        [Test]
        public void BeforeItsFirstTick_TheTreeIsToldItDoesNotTelegraph()
        {
            Assert.IsFalse(IsTheTreeToldItTelegraphs);
        }

        [Test]
        public void WhileTheTelegraphWindowIsOpen_TheTreeAndTheEncounterAreTold()
        {
            StartTheAttack();

            TickTheThugFor(TickSeconds);

            Assert.IsTrue(IsTheTreeToldItTelegraphs);
            Assert.IsTrue(director.IsTelegraphing(agent));
        }

        [Test]
        public void OnceTheTelegraphWindowCloses_BothAreToldItStopped()
        {
            StartTheAttack();

            TickTheThugFor(TelegraphWindow.End + TickSeconds);

            Assert.IsFalse(IsTheTreeToldItTelegraphs);
            Assert.IsFalse(director.IsTelegraphing(agent));
        }

        [Test]
        public void AnAttackCutShortWhileItTelegraphs_LeavesNoTelegraphBehind()
        {
            StartTheAttack();
            TickTheThugFor(TickSeconds);

            combatActions.Runner.Cancel();
            TickTheThugFor(TickSeconds);

            Assert.IsFalse(IsTheTreeToldItTelegraphs);
            Assert.IsFalse(director.IsTelegraphing(agent));
        }

        [Test]
        public void TheEncounterIsTold_WhetherTheAttackCanBeCountered()
        {
            StartTheAttack();
            TickTheThugFor(TickSeconds);
            Assert.IsTrue(director.IsACounterableAttackIncoming, "attacks can be countered unless they say otherwise");

            MakeTheAttackImpossibleToCounter();
            TickTheThugFor(TickSeconds);
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void TheTelegraphingFact_IsWrittenOnlyWhenItChanges()
        {
            AgentVariableWriter agentVariables = thug.GetComponent<AgentVariableWriter>();
            StartTheAttack();
            TickTheThugFor(TickSeconds);
            int versionWhileTelegraphing = agentVariables.VersionOf(EnemyVariableNames.Telegraphing);

            TickTheThugFor(2 * TickSeconds);

            Assert.AreEqual(versionWhileTelegraphing, agentVariables.VersionOf(EnemyVariableNames.Telegraphing));
        }

        private void MakeTheAttackImpossibleToCounter()
        {
            haymaker.ConfigureAttack(
                haymaker.HitWindow, haymaker.ComboWindow, haymaker.EvadeWindow, haymaker.WarpWindow,
                haymaker.StrikeDistance, haymaker.MaxLunge, haymaker.Damage, haymaker.Reaction,
                isCounterable: false);
        }
    }
}
