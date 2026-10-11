using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    // The status and the facts updater publish under names the trees repeat on their canvases.
    // Nothing compiles the two together, so these read the authored assets.
    public class ThugTreeContractTests
    {
        private const string RootPath = "Assets/ArkhamCombat/Trees/Roots/ThugRoot.asset";
        private const string SubTreesFolder = "Assets/ArkhamCombat/Trees/SubTrees/";
        private const string KindOfReactionAskedAbout = "kind";
        private const string HasTargetInCombatRangePath = "Assets/ArkhamCombat/Trees/Functions/HasTargetInCombatRange.asset";

        private static readonly string[] SubTreesThatReadAParameter = { "Flinch", "Stagger", "KnockedDown", "Death", "Idle", "FaceTarget", "Approach" };

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
        public void TheRoot_TriesEveryReactionBeforeTheRing_AndIdleLast()
        {
            string[] subTreesInPriorityOrder = BranchesOfTheRootInPriorityOrder().Select(SubTreeRunBy).ToArray();

            CollectionAssert.AreEqual(new[] { "Death", "KnockedDown", "Stagger", "Flinch", "HoldSlot", "Approach", "Idle" }, subTreesInPriorityOrder);
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
            int release = steps.FindIndex(step => step is ReleaseAttackToken);
            int stop = steps.FindIndex(step => step is StopNavAgent);
            int play = steps.FindIndex(step => step is PlayActionNode);

            Assert.AreEqual(0, release, "the token goes back first");
            Assert.Less(release, stop);
            Assert.Less(stop, play);
        }

        [Test]
        public void AFlinch_StopsTheWalk_AndKeepsTheToken()
        {
            List<BehaviorTreeNode> steps = StepsOf("Flinch");

            Assert.IsInstanceOf<StopNavAgent>(steps[0]);
            Assert.IsFalse(steps.Any(step => step is ReleaseAttackToken), "chip damage must not cost an enemy its turn");
        }

        [Test]
        public void EveryParameterASubTreeReads_SaysWhichVariablesItReadsFrom()
        {
            FieldInfo variableKind = typeof(GetVariable).GetField("VariableKind", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(variableKind, "BH3 renamed the field this test reads");

            foreach (string subTree in SubTreesThatReadAParameter)
            {
                foreach (GetVariable read in Tree(SubTreesFolder + subTree + ".asset").graph.Nodes.OfType<GetVariable>())
                {
                    Assert.AreNotEqual(BehaviorTreeVariableKind.None, variableKind.GetValue(read),
                        $"a Get Variable in {subTree} has no variable kind, so it throws every tick and bt_verify does not report it");
                }
            }
        }
    }
}
