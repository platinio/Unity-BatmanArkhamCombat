using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    // The status publishes a reaction by its name and the trees compare against names typed on the
    // canvas. Nothing compiles the two together, so these read the authored assets.
    public class ThugTreeContractTests
    {
        private const string RootPath = "Assets/ArkhamCombat/Trees/Roots/ThugRoot.asset";
        private const string SubTreesFolder = "Assets/ArkhamCombat/Trees/SubTrees/";
        private const string KindOfReactionAskedAbout = "kind";

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
    }
}
