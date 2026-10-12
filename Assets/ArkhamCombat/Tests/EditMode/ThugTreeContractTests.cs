using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    // The code and the canvases agree on names, on order and on which step runs what, and nothing
    // compiles the two together. So these read the authored assets.
    public class ThugTreeContractTests
    {
        private const string RootPath = "Assets/ArkhamCombat/Trees/Roots/ThugRoot.asset";
        private const string SubTreesFolder = "Assets/ArkhamCombat/Trees/SubTrees/";
        private const string KindOfReactionAskedAbout = "kind";
        private const string HasTargetInCombatRangePath = "Assets/ArkhamCombat/Trees/Functions/HasTargetInCombatRange.asset";
        private const string TreesFolder = "Assets/ArkhamCombat/Trees";
        private const string SwitchedOnOrOff = "enabled";
        private const string AttackSubTree = "Attack";

        private static BehaviorTreeGraphAsset Tree(string path)
        {
            BehaviorTreeGraphAsset tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
            Assert.IsNotNull(tree, $"no behaviour tree at {path}");
            return tree;
        }

        private static List<string> KindsTheRootAsksAbout()
        {
            List<string> kinds = new List<string>();
            foreach (VisualScriptGraphVariable functionCall in Tree(RootPath).graph.Nodes.OfType<VisualScriptGraphVariable>())
            {
                ValueInput kind = functionCall.valueInputs.FirstOrDefault(input => input.key == KindOfReactionAskedAbout);
                if (kind != null)
                {
                    kinds.Add(kind.GetValue<string>());
                }
            }

            return kinds;
        }

        [Test]
        public void TheRootGuardsOneBranchPerReactionAnEnemyCanPlay()
        {
            string[] reactionsWithABranch = { nameof(AppliedReaction.Flinch), nameof(AppliedReaction.Stagger), nameof(AppliedReaction.Knockdown) };

            CollectionAssert.AreEquivalent(reactionsWithABranch, KindsTheRootAsksAbout());
        }

        [Test]
        public void EveryKindTheRootAsksAbout_IsAReactionTheStatusCanPublish()
        {
            foreach (string kind in KindsTheRootAsksAbout())
            {
                Assert.IsTrue(Enum.TryParse(kind, out AppliedReaction _), $"'{kind}' on the canvas is not an {nameof(AppliedReaction)}, so its guard can never be true.");
            }
        }

        [TestCase("Flinch")]
        [TestCase("Stagger")]
        [TestCase("KnockedDown")]
        public void AReactionBranch_ClearsTheReactionToWhatTheStatusCallsNone(string subTree)
        {
            SetVariable clear = Tree(SubTreesFolder + subTree + ".asset").graph.Nodes.OfType<SetVariable>().Single();

            Assert.AreEqual(EnemyVariableNames.Reaction, clear.Key.GetValue<string>());
            Assert.AreEqual(nameof(AppliedReaction.None), clear.Value.GetValue<object>());
        }

        private static List<BehaviorTreeNode> BranchesOfTheRootInPriorityOrder()
        {
            BehaviorTreeGraph root = Tree(RootPath).graph;
            return root.ChildrenInPriorityOrder(root.Nodes.OfType<Selector>().Single());
        }

        private static string SubTreeRunBy(BehaviorTreeNode branch) => ((RunBehaviorTreeGraphNode)branch).BehaviorTreeGraphAsset.name;

        private static List<BehaviorTreeNode> StepsOf(string subTree)
        {
            BehaviorTreeGraph graph = Tree(SubTreesFolder + subTree + ".asset").graph;
            return graph.ChildrenInPriorityOrder(graph.Nodes.OfType<Sequence>().Single());
        }

        private static string[] KeysThatWakeTheGuardOn(string subTree)
        {
            ReactiveGuard guard = Tree(RootPath).graph.Nodes.OfType<ReactiveGuard>().Single(candidate => SubTreeRunBy(candidate.Owner) == subTree);
            return guard.Triggers.SelectMany(trigger => trigger.Keys).ToArray();
        }

        [Test]
        public void TheRoot_TriesEveryReactionFirst_ThenTheAttack_ThenTheRing_AndIdleLast()
        {
            string[] subTreesInPriorityOrder = BranchesOfTheRootInPriorityOrder().Select(SubTreeRunBy).ToArray();

            CollectionAssert.AreEqual(new[] { "Death", "KnockedDown", "Stagger", "Flinch", "Attack", "HoldSlot", "Approach", "Idle" }, subTreesInPriorityOrder);
        }

        [Test]
        public void TheAttacksGuard_ChecksOnlyAtTheDoor_SoASwingIsNeverCutShortByIt()
        {
            ConditionalExecution guard = Tree(RootPath).graph.Nodes.OfType<ConditionalExecution>().Single(candidate => SubTreeRunBy(candidate.Owner) == AttackSubTree);

            Assert.IsNotInstanceOf<ReactiveGuard>(guard);
        }

        [Test]
        public void TheAttackAndTheRing_AskTheSameFunctionWhetherTheTargetIsInCombatRange()
        {
            FunctionGraphAsset hasTargetInCombatRange = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(HasTargetInCombatRangePath);

            int guardsAskingIt = Tree(RootPath).graph.Nodes.OfType<VisualScriptGraphVariable>().Count(read => read.Function == hasTargetInCombatRange);

            Assert.AreEqual(2, guardsAskingIt, "one for Attack, one for HoldSlot");
        }

        [Test]
        public void TheRoot_HandsTheAttackBranchTheAttackTheArchetypeWriterWrites()
        {
            GetVariable read = Tree(RootPath).graph.Nodes.OfType<GetVariable>().Single();

            Assert.AreEqual(EnemyVariableNames.Attack, read.defaultValues[nameof(GetVariable.Key)]);
            Assert.IsTrue(Tree(SubTreesFolder + AttackSubTree + ".asset").requiredDeclarations.IsDefined(EnemyVariableNames.Attack));
        }

        [Test]
        public void AnAttack_AsksForTheTurnFirst_AndGivesItBackLast_EvenWhenTheSwingFails()
        {
            BehaviorTreeGraph attack = Tree(SubTreesFolder + AttackSubTree + ".asset").graph;
            BehaviorTreeNode spine = attack.ChildrenInPriorityOrder(attack.EntryNode).Single();

            List<BehaviorTreeNode> steps = attack.ChildrenInPriorityOrder(spine);

            Assert.AreEqual(3, steps.Count);
            Assert.IsInstanceOf<TryAcquireAttackToken>(steps[0]);
            Assert.IsInstanceOf<ReturnSuccess>(steps[1], "a walk-in that times out must not skip giving the turn back");
            Assert.IsInstanceOf<ReleaseAttackToken>(steps[2]);
        }

        [Test]
        public void AnAttackingThug_TracksItsTarget_WhileTheFactTheRelayWritesIsTrue()
        {
            ReactiveGuard guard = Tree(SubTreesFolder + AttackSubTree + ".asset").graph.Nodes.OfType<ReactiveGuard>().Single();

            Assert.AreEqual("FaceTarget", SubTreeRunBy(guard.Owner));
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.Telegraphing }, guard.Triggers.SelectMany(trigger => trigger.Keys).ToArray());
            Assert.IsTrue(guard.StopsItsOwnBranch, "it stops turning the moment the telegraph closes");
        }

        [Test]
        public void TheRingsGuards_WakeOnTheFactsTheUpdaterWrites()
        {
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.HasTarget, EnemyVariableNames.InCombatRange }, KeysThatWakeTheGuardOn("HoldSlot"));
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.HasTarget }, KeysThatWakeTheGuardOn("Approach"));
        }

        [Test]
        public void HasTargetInCombatRange_ReadsExactlyTheFactsItSaysItWatches()
        {
            FunctionGraphAsset function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(HasTargetInCombatRangePath);
            string[] factsOfTheUpdater = { EnemyVariableNames.HasTarget, EnemyVariableNames.InCombatRange };

            CollectionAssert.AreEquivalent(factsOfTheUpdater, function.DeriveReadKeys());
            CollectionAssert.AreEquivalent(factsOfTheUpdater, function.WatchedKeys);
        }

        [TestCase("Death")]
        [TestCase("KnockedDown")]
        [TestCase("Stagger")]
        public void AReactionThatCanInterruptAnAttack_GivesTheTokenBack_ThenStopsTheWalk_BeforeItPlays(string subTree)
        {
            List<BehaviorTreeNode> steps = StepsOf(subTree);
            int releaseIndex = steps.FindIndex(step => step is ReleaseAttackToken);
            int stopIndex = steps.FindIndex(step => step is StopNavAgent);
            int playIndex = steps.FindIndex(step => step is PlayActionNode);

            Assert.AreEqual(0, releaseIndex, "the token goes back first");
            Assert.Less(releaseIndex, stopIndex);
            Assert.Less(stopIndex, playIndex);
        }

        [Test]
        public void AFlinch_StopsTheWalkFirst()
        {
            Assert.IsInstanceOf<StopNavAgent>(StepsOf("Flinch")[0]);
        }

        [Test]
        public void AFlinch_KeepsTheToken()
        {
            Assert.IsFalse(StepsOf("Flinch").Any(step => step is ReleaseAttackToken), "chip damage must not cost an enemy its turn");
        }

        private static FunctionGraphAsset FunctionRunBy(BehaviorTreeNode step) =>
            ((VisualScriptingNode)step).LifecycleGraphs.Select(graph => graph.Function).FirstOrDefault(function => function != null);

        private static Type[] WhatItSwitches(FunctionGraphAsset function) =>
            function.graph.units.OfType<Unity.VisualScripting.SetMember>()
                .Where(switchedMember => switchedMember.member.name == SwitchedOnOrOff)
                .Select(switchedMember => switchedMember.member.targetType)
                .ToArray();

        [Test]
        public void ADyingThug_LeavesTheEncounter_BeforeItsDeathPlays()
        {
            List<BehaviorTreeNode> steps = StepsOf("Death");
            int leaveIndex = steps.FindIndex(step => step is VisualScriptingNode && FunctionRunBy(step).name == "LeaveTheEncounter");
            int playIndex = steps.FindIndex(step => step is PlayActionNode);

            Assert.GreaterOrEqual(leaveIndex, 0, "no step of Death runs LeaveTheEncounter");
            Assert.Less(leaveIndex, playIndex);
            CollectionAssert.AreEquivalent(new[] { typeof(EncounterAgent) }, WhatItSwitches(FunctionRunBy(steps[leaveIndex])));
        }

        [Test]
        public void ADeadThug_StopsBlockingTheWay_AfterItsDeathPlayed()
        {
            List<BehaviorTreeNode> steps = StepsOf("Death");
            int stopBlockingIndex = steps.FindIndex(step => step is VisualScriptingNode && FunctionRunBy(step).name == "StopBlockingTheWay");
            int playIndex = steps.FindIndex(step => step is PlayActionNode);

            Assert.Greater(stopBlockingIndex, playIndex);
            CollectionAssert.AreEquivalent(
                new[] { typeof(UnityEngine.Collider), typeof(UnityEngine.AI.NavMeshAgent) }, WhatItSwitches(FunctionRunBy(steps[stopBlockingIndex])));
        }

        [TestCase("Death", true)]
        [TestCase("KnockedDown", false)]
        [TestCase("Stagger", true)]
        [TestCase("Flinch", true)]
        [TestCase("HoldSlot", true)]
        [TestCase("Approach", true)]
        public void EveryGuardedBranch_TakesOverWhatIsBelowIt(string subTree, bool shouldAlsoStopItself)
        {
            ReactiveGuard guard = Tree(RootPath).graph.Nodes.OfType<ReactiveGuard>().Single(candidate => SubTreeRunBy(candidate.Owner) == subTree);

            Assert.IsTrue(guard.TakesOverLowerPriority);
            Assert.AreEqual(shouldAlsoStopItself, guard.StopsItsOwnBranch);
        }

        [Test]
        public void Idle_IsTheOnlyBranchWithoutAGuard()
        {
            string[] guardedSubTrees = Tree(RootPath).graph.Nodes.OfType<ConditionalExecution>().Select(guard => SubTreeRunBy(guard.Owner)).ToArray();

            CollectionAssert.AreEquivalent(new[] { "Death", "KnockedDown", "Stagger", "Flinch", AttackSubTree, "HoldSlot", "Approach" }, guardedSubTrees);
        }

        [Test]
        public void EveryVariableReadInEveryTree_IsCompleteEnoughToRun()
        {
            int readsChecked = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset", new[] { TreesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (GetVariable read in Tree(path).graph.Nodes.OfType<GetVariable>())
                {
                    List<NodeProblem> problems = new List<NodeProblem>();
                    read.CollectProblems(problems);

                    Assert.IsEmpty(problems, $"a Get Variable in {path} would throw on every tick, and bt_verify does not report it");
                    readsChecked++;
                }
            }

            Assert.Greater(readsChecked, 0, $"found no Get Variable under {TreesFolder} to check");
        }
    }
}
