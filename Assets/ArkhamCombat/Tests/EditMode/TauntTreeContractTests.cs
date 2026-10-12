using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using static ArkhamCombat.Tests.TreeAssets;

namespace ArkhamCombat.Tests
{
    public class TauntTreeContractTests
    {
        private const string TauntSubTree = "Taunt";
        private const string ActionItPlays = "tauntAction";

        private static BehaviorTreeGraph Taunt => SubTree(TauntSubTree).graph;

        private static BehaviorTreeNode OnlyChildOf(BehaviorTreeNode parent) => Taunt.ChildrenInPriorityOrder(parent).Single();

        private static BehaviorTreeNode TheBranchOnTheRoot() =>
            Tree(ThugRootPath).graph.Nodes.OfType<RunBehaviorTreeGraphNode>().Single(call => SubTreeRunBy(call) == TauntSubTree);

        private static BehaviorTreeNode TheWait() => OnlyChildOf(Taunt.EntryNode);

        private static BehaviorTreeNode TheRoll() => OnlyChildOf(TheWait());

        private static List<BehaviorTreeNode> StepsOfATaunt() => Taunt.ChildrenInPriorityOrder(OnlyChildOf(TheRoll()));

        [Test]
        public void ATaunt_IsRolledForOnlyOnceInAWhile_AndOnlySomeRollsPass()
        {
            Assert.IsInstanceOf<Cooldown>(TheWait(), "a roll that fails must start the wait too, or the next loop rolls again");
            Assert.IsInstanceOf<RandomChance>(TheRoll());
        }

        [TestCase(typeof(Cooldown), nameof(Cooldown.Duration), "secondsBetweenTaunts")]
        [TestCase(typeof(RandomChance), nameof(RandomChance.Chance), "tauntChance")]
        [TestCase(typeof(PlayActionNode), nameof(PlayActionNode.Action), ActionItPlays)]
        public void EachStepOfATaunt_ReadsItsOwnParameter(Type step, string port, string parameter)
        {
            BehaviorTreeNode stepOfTheTaunt = Taunt.Nodes.Single(node => node.GetType() == step);

            Assert.AreEqual(parameter, ParameterThatFeeds(stepOfTheTaunt, port));
        }

        [Test]
        public void ATauntingEnemy_StopsWalking_FacesItsTarget_ThenPlaysItsTaunt()
        {
            List<BehaviorTreeNode> steps = StepsOfATaunt();

            Assert.AreEqual(3, steps.Count);
            Assert.IsInstanceOf<StopNavAgent>(steps[0]);
            Assert.AreEqual("FaceTarget", SubTreeRunBy(steps[1]));
            Assert.IsInstanceOf<PlayActionNode>(steps[2]);
        }

        [TestCase("tauntChance")]
        [TestCase("secondsBetweenTaunts")]
        [TestCase(ActionItPlays)]
        public void EverythingAboutATaunt_IsAParameterARootMayOverride(string parameter)
        {
            Assert.IsTrue(SubTree(TauntSubTree).optionalDeclarations.IsDefined(parameter));
        }

        [Test]
        public void TheTauntItPlaysUnlessTold_IsAnActionThatCanPlay()
        {
            ActionDefinition taunt = SubTree(TauntSubTree).optionalDeclarations.Get<ActionDefinition>(ActionItPlays);
            List<string> errors = new List<string>();

            Assert.IsNotNull(taunt, "the Taunt sub-tree carries no action of its own");
            Assert.IsTrue(taunt.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void TheRoot_HandsTheTauntTheChanceTheArchetypeWriterWrites()
        {
            VisualScriptGraphVariable read = (VisualScriptGraphVariable)WhatFeeds(TheBranchOnTheRoot(), EnemyVariableNames.TauntChance);

            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.TauntChance }, read.Function.DeriveReadKeys());
        }

        [Test]
        public void TheTauntsGuard_ChecksOnlyAtTheDoor_SoATauntThatStartedIsNotCutShortByIt()
        {
            ConditionalExecution guard = Tree(ThugRootPath).graph.Nodes.OfType<ConditionalExecution>().Single(candidate => candidate.Owner == TheBranchOnTheRoot());

            Assert.IsNotInstanceOf<ReactiveGuard>(guard);
        }
    }
}
