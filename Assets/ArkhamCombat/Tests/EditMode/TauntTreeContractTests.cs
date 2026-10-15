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
        private const string RootPath = "Assets/ArkhamCombat/Trees/Roots/ThugRoot.asset";
        private const string TauntSubTree = "Taunt";
        private const string ActionItPlays = "tauntAction";

        private static BehaviorTreeGraph Taunt => SubTree(TauntSubTree).graph;

        private static BehaviorTreeNode OnlyChildOf(BehaviorTreeNode parent) => Taunt.ChildrenInPriorityOrder(parent).Single();

        private static BehaviorTreeNode TheBranchOnTheRoot() =>
            Tree(RootPath).graph.Nodes.OfType<RunBehaviorTreeGraphNode>().Single(call => SubTreeRunBy(call) == TauntSubTree);

        [Test]
        public void ATaunt_IsRolledForOnlyOnceInAWhile_AndOnlySomeRollsPass()
        {
            BehaviorTreeNode onceInAWhile = OnlyChildOf(Taunt.EntryNode);
            BehaviorTreeNode sometimes = OnlyChildOf(onceInAWhile);

            Assert.IsInstanceOf<Cooldown>(onceInAWhile, "a roll that fails must start the wait too, or the next loop rolls again");
            Assert.IsInstanceOf<RandomChance>(sometimes);
        }

        [Test]
        public void ATauntingEnemy_StopsWalking_FacesItsTarget_ThenPlaysItsTaunt()
        {
            BehaviorTreeNode sometimes = OnlyChildOf(OnlyChildOf(Taunt.EntryNode));
            List<BehaviorTreeNode> steps = Taunt.ChildrenInPriorityOrder(OnlyChildOf(sometimes));

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
            ConditionalExecution guard = Tree(RootPath).graph.Nodes.OfType<ConditionalExecution>().Single(candidate => candidate.Owner == TheBranchOnTheRoot());

            Assert.IsNotInstanceOf<ReactiveGuard>(guard);
        }
    }
}
