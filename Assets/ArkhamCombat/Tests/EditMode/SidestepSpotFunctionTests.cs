using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class SidestepSpotFunctionTests
    {
        private const string FunctionPath = "Assets/ArkhamCombat/Trees/Functions/SidestepSpot.asset";
        private const float SidestepDistance = 1.2f;
        private const float Tolerance = 1e-3f;
        private const int AnySeed = 7;
        private const int EnoughSidestepsToSeeBothSides = 40;

        private static readonly Vector3 Centre = new Vector3(10f, 0f, -4f);
        private static readonly Vector3 ThreeMetresNorthOfTheCentre = Centre + Vector3.forward * 3f;

        private GameObject agent;
        private FunctionGraphAsset sidestepSpot;
        private Random.State randomStateBeforeTheTest;

        [SetUp]
        public void SetUp()
        {
            sidestepSpot = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(FunctionPath);
            Assert.IsNotNull(sidestepSpot, $"no Function at {FunctionPath}");

            agent = HiddenObject("Agent");
            agent.AddComponent<BehaviorTreeMachine>();
            randomStateBeforeTheTest = Random.state;
            Random.InitState(AnySeed);
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = randomStateBeforeTheTest;
            Object.DestroyImmediate(agent);
        }

        private Vector3 SidestepSpot(Vector3 from, Vector3 around, float distance)
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            VisualScriptGraphVariable call = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(graph, 0f, 0f);
            call.SetFunction(sidestepSpot);
            call.valueInputs["from"].SetDefaultValue(from);
            call.valueInputs["around"].SetDefaultValue(around);
            call.valueInputs["distance"].SetDefaultValue(distance);

            BehaviorTreeMachine machine = agent.GetComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            call.SetMachine(machine);
            graph.OnAwake();

            return (Vector3)call.Output.GetPortValue();
        }

        [Test]
        public void TheSpot_IsAsFarFromTheCentre_AsWhereTheStepStarted()
        {
            Vector3 spot = SidestepSpot(ThreeMetresNorthOfTheCentre, Centre, SidestepDistance);

            Assert.AreEqual(Vector3.Distance(ThreeMetresNorthOfTheCentre, Centre), Vector3.Distance(spot, Centre), Tolerance);
        }

        [Test]
        public void TheSpot_IsTheSidestepDistanceAway_MeasuredAlongTheCircle()
        {
            Vector3 spot = SidestepSpot(ThreeMetresNorthOfTheCentre, Centre, SidestepDistance);

            float radius = Vector3.Distance(ThreeMetresNorthOfTheCentre, Centre);
            float arcInRadians = Vector3.Angle(ThreeMetresNorthOfTheCentre - Centre, spot - Centre) * Mathf.Deg2Rad;
            Assert.AreEqual(SidestepDistance, arcInRadians * radius, Tolerance);
        }

        [Test]
        public void TheSpot_IsAtTheHeightTheStepStartedFrom()
        {
            Vector3 spot = SidestepSpot(ThreeMetresNorthOfTheCentre, Centre, SidestepDistance);

            Assert.AreEqual(ThreeMetresNorthOfTheCentre.y, spot.y, Tolerance);
        }

        [Test]
        public void OverManySidesteps_ItGoesToBothSides()
        {
            bool wentEast = false;
            bool wentWest = false;

            for (int i = 0; i < EnoughSidestepsToSeeBothSides; i++)
            {
                Vector3 spot = SidestepSpot(ThreeMetresNorthOfTheCentre, Centre, SidestepDistance);
                wentEast |= spot.x > Centre.x;
                wentWest |= spot.x < Centre.x;
            }

            Assert.IsTrue(wentEast, "never stepped east");
            Assert.IsTrue(wentWest, "never stepped west");
        }

        [Test]
        public void AnAgentStandingOnTheCentre_IsNotSentToASpotThatIsNotANumber()
        {
            Vector3 spot = SidestepSpot(Centre, Centre, SidestepDistance);

            Assert.IsFalse(float.IsNaN(spot.x) || float.IsNaN(spot.y) || float.IsNaN(spot.z), $"the spot was {spot}");
        }
    }
}
