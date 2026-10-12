using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GetVariable = ArcaneOnyx.BehaviorTree.GetVariable;
using Variables = Unity.VisualScripting.Variables;
using static ArkhamCombat.Tests.CombatTestDoubles;
using static ArkhamCombat.Tests.TreeAssets;

namespace ArkhamCombat.Tests
{
    public class ReactionOrDefaultTests
    {
        private const string FunctionPath = "Assets/ArkhamCombat/Trees/Functions/ReactionOrDefault.asset";
        private const string WhatTheBranchPlaysOtherwise = "default";

        private GameObject agent;
        private FunctionGraphAsset reactionOrDefault;
        private ActionDefinition stagger;
        private ActionDefinition countered;

        [SetUp]
        public void SetUp()
        {
            reactionOrDefault = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(FunctionPath);
            Assert.IsNotNull(reactionOrDefault, $"no Function at {FunctionPath}");

            agent = HiddenObject("Agent");
            agent.AddComponent<BehaviorTreeMachine>();
            stagger = Action("Stagger");
            countered = Action("Countered");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(stagger);
            Object.DestroyImmediate(countered);
        }

        private ActionDefinition WhatABranchWouldPlay(ActionDefinition itsOwnAction)
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            VisualScriptGraphVariable call = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(graph, 0f, 0f);
            call.SetFunction(reactionOrDefault);
            call.valueInputs[WhatTheBranchPlaysOtherwise].SetDefaultValue(itsOwnAction);

            BehaviorTreeMachine machine = agent.GetComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            call.SetMachine(machine);
            graph.OnAwake();

            return (ActionDefinition)call.Output.GetPortValue();
        }

        [Test]
        public void WhenAPairedAttackBroughtAnAction_TheBranchPlaysThat()
        {
            Variables.Object(agent).Set(EnemyVariableNames.ReactionOverride, countered);

            Assert.AreSame(countered, WhatABranchWouldPlay(stagger));
        }

        [Test]
        public void WhenNoneWasBrought_TheBranchPlaysItsOwn()
        {
            Variables.Object(agent).Set(EnemyVariableNames.ReactionOverride, null);

            Assert.AreSame(stagger, WhatABranchWouldPlay(stagger));
        }

        [Test]
        public void AnAgentThatHasNeverHeardOfPairedAttacks_PlaysItsOwn()
        {
            Assert.AreSame(stagger, WhatABranchWouldPlay(stagger));
        }

        [TestCase("Stagger", "action")]
        [TestCase("KnockedDown", "knockdownAction")]
        [TestCase("Death", "action")]
        public void ABranchAPairMayBorrow_PlaysWhatThePairBrought_OrItsOwnParameter(string subTree, string ownParameter)
        {
            BehaviorTreeGraph graph = SubTree(subTree).graph;
            VisualScriptGraphVariable call = graph.Nodes.OfType<VisualScriptGraphVariable>().Single(candidate => candidate.Function == reactionOrDefault);
            PlayActionNode play = graph.Nodes.OfType<PlayActionNode>().SingleOrDefault(candidate => WhatFeeds(candidate, nameof(PlayActionNode.Action)) == call);
            GetVariable ownAction = (GetVariable)WhatFeeds(call, WhatTheBranchPlaysOtherwise);

            Assert.IsNotNull(play, $"no Play Action of {subTree} is fed by ReactionOrDefault");
            Assert.AreEqual(ownParameter, ownAction.defaultValues[nameof(GetVariable.Key)]);
        }

        [Test]
        public void AFlinch_IsNeverBorrowed()
        {
            Assert.IsFalse(SubTree("Flinch").graph.Nodes.OfType<VisualScriptGraphVariable>().Any(call => call.Function == reactionOrDefault));
        }

        [Test]
        public void TheFunction_ReadsTheFactTheStatusWrites()
        {
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.ReactionOverride }, reactionOrDefault.DeriveReadKeys());
        }
    }
}
