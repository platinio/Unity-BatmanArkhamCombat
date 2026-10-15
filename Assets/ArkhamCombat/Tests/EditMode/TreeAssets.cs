using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    internal static class TreeAssets
    {
        public const string TreesFolder = "Assets/ArkhamCombat/Trees";
        public const string SubTreesFolder = TreesFolder + "/SubTrees/";
        public const string ThugRootPath = TreesFolder + "/Roots/ThugRoot.asset";

        public static BehaviorTreeGraphAsset Tree(string path)
        {
            BehaviorTreeGraphAsset tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
            Assert.IsNotNull(tree, $"no behaviour tree at {path}");
            return tree;
        }

        public static BehaviorTreeGraphAsset SubTree(string name) => Tree(SubTreesFolder + name + ".asset");

        public static string SubTreeRunBy(BehaviorTreeNode branch) => ((RunBehaviorTreeGraphNode)branch).BehaviorTreeGraphAsset.name;

        public static BehaviorTreeNode WhatFeeds(BehaviorTreeNode node, string port) =>
            (BehaviorTreeNode)node.valueInputs[port].connection.source.behaviorTreeNode;

        public static string ParameterThatFeeds(BehaviorTreeNode node, string port)
        {
            GetVariable read = WhatFeeds(node, port) as GetVariable;
            Assert.IsNotNull(read, $"{node.GetType().Name}.{port} is not fed by a parameter of its tree");
            return (string)read.defaultValues[nameof(GetVariable.Key)];
        }

        public static FunctionGraphAsset FunctionAsking(ConditionalExecution guard) =>
            ((VisualScriptGraphVariable)WhatFeeds(guard, "Value")).Function;
    }
}
