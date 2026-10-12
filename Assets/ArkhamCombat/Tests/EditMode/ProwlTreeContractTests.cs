using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using static ArkhamCombat.Tests.TreeAssets;

namespace ArkhamCombat.Tests
{
    public class ProwlTreeContractTests
    {
        private const string RootPath = "Assets/ArkhamCombat/Trees/Roots/ThugRoot.asset";
        private const string ProwlSubTree = "Prowl";

        private static BehaviorTreeGraph Prowl => SubTree(ProwlSubTree).graph;

        private static List<BehaviorTreeNode> ChildrenOf(BehaviorTreeNode parent) => Prowl.ChildrenInPriorityOrder(parent);

        private static BehaviorTreeNode OnlyChildOf(BehaviorTreeNode parent) => ChildrenOf(parent).Single();

        private static List<BehaviorTreeNode> WhatRunsSideBySide() => ChildrenOf(OnlyChildOf(Prowl.EntryNode));

        private static List<BehaviorTreeNode> StepsOfTheMove() =>
            ChildrenOf(OnlyChildOf(WhatRunsSideBySide().OfType<ReturnSuccess>().Single()));

        private static List<BehaviorTreeNode> WaysToMove() => ChildrenOf(StepsOfTheMove().OfType<Selector>().Single());

        [Test]
        public void TheProwl_EndsAsSoonAsOneOfItsSidesSucceeds()
        {
            Assert.IsInstanceOf<ParallelSelector>(OnlyChildOf(Prowl.EntryNode));
        }

        [Test]
        public void AProwlingEnemy_KeepsAskingForItsTurn_AndTheProwlEndsWhenItIsGranted()
        {
            BehaviorTreeNode keepAsking = WhatRunsSideBySide().OfType<UntilSuccess>().Single();

            Assert.IsInstanceOf<TryAcquireAttackToken>(OnlyChildOf(keepAsking));
        }

        [Test]
        public void AProwlingEnemy_KeepsFacingItsTarget()
        {
            BehaviorTreeNode keepFacing = WhatRunsSideBySide().OfType<Repeater>().Single();

            Assert.AreEqual("FaceTarget", SubTreeRunBy(OnlyChildOf(keepFacing)));
        }

        [Test]
        public void AProwl_IsOneMoveAndARest_SoTheRootLooksAtItsOtherBranchesBetweenTwoMoves()
        {
            List<BehaviorTreeNode> steps = StepsOfTheMove();

            Assert.AreEqual(4, steps.Count);
            Assert.IsInstanceOf<SetNavAgentSpeedNode>(steps[0], "it sets the speed it prowls at before it walks");
            Assert.IsInstanceOf<Selector>(steps[1], "where to");
            Assert.IsInstanceOf<ParallelSelector>(steps[2], "the walk");
            Assert.IsInstanceOf<WaitTimeRandomRange>(steps[3], "the rest");
        }

        [Test]
        public void TheMove_IsAStepInOnlySomeOfTheTime_AndASidestepOtherwise()
        {
            List<BehaviorTreeNode> waysToMove = WaysToMove();

            Assert.AreEqual(2, waysToMove.Count);
            Assert.IsInstanceOf<RandomChance>(waysToMove[0]);
            Assert.IsInstanceOf<SetNavAgentPosition>(OnlyChildOf(waysToMove[0]));
            Assert.IsInstanceOf<SetNavAgentPosition>(waysToMove[1]);
        }

        [Test]
        public void AStepIn_GoesWhereTheDirectorSaysThereIsRoomCloserIn()
        {
            BehaviorTreeNode stepIn = OnlyChildOf(WaysToMove()[0]);

            Assert.IsInstanceOf<GetFreePositionCloserToTarget>(WhatFeeds(stepIn, nameof(SetNavAgentPosition.NavPosition)));
        }

        [Test]
        public void ASidestep_GoesAroundTheEncounterTarget_FromWhereTheEnemyStands()
        {
            BehaviorTreeNode sidestep = WaysToMove()[1];
            VisualScriptGraphVariable spotToTheSide = (VisualScriptGraphVariable)WhatFeeds(sidestep, nameof(SetNavAgentPosition.NavPosition));
            VisualScriptGraphVariable whereTheTargetIs = (VisualScriptGraphVariable)WhatFeeds(spotToTheSide, "around");

            Assert.AreEqual("SidestepSpot", spotToTheSide.Function.name);
            Assert.IsInstanceOf<GetStandingPosition>(WhatFeeds(spotToTheSide, "from"));
            Assert.AreEqual("PositionOf", whereTheTargetIs.Function.name);
            Assert.IsInstanceOf<GetEncounterTarget>(WhatFeeds(whereTheTargetIs, "of"));
        }

        [Test]
        public void AWalkThatCannotArrive_IsGivenUpAfterAWhile()
        {
            List<BehaviorTreeNode> waysTheWalkEnds = ChildrenOf(StepsOfTheMove().OfType<ParallelSelector>().Single());

            Assert.AreEqual(2, waysTheWalkEnds.Count);
            Assert.IsTrue(waysTheWalkEnds.Any(way => way is WaitUntilReachNavTargetPosition), "arriving ends it");
            Assert.IsTrue(waysTheWalkEnds.Any(way => way is WaitTime), "so does walking for too long");
        }

        [TestCase("prowlSpeed")]
        [TestCase("sidestepDistance")]
        [TestCase("stepInChance")]
        [TestCase("stepInDistance")]
        [TestCase("personalSpace")]
        [TestCase("restMin")]
        [TestCase("restMax")]
        public void EveryNumberOfTheProwl_IsAParameterARootMayOverride(string parameter)
        {
            Assert.IsTrue(SubTree(ProwlSubTree).optionalDeclarations.IsDefined(parameter));
        }

        [Test]
        public void Approach_SetsTheSpeedItWalksAt_BeforeItWalks()
        {
            BehaviorTreeGraph approach = SubTree("Approach").graph;
            List<BehaviorTreeNode> steps = approach.ChildrenInPriorityOrder(approach.Nodes.OfType<Sequence>().Single());

            Assert.IsInstanceOf<SetNavAgentSpeedNode>(steps[0]);
            Assert.IsInstanceOf<SetNavAgentPosition>(steps[1]);
        }

        [TestCase("Attack")]
        [TestCase("Approach")]
        public void TheRoot_HandsABranchThatWalksFast_TheMoveSpeedTheArchetypeWriterWrites(string subTree)
        {
            BehaviorTreeGraph root = Tree(RootPath).graph;
            BehaviorTreeNode branch = root.Nodes.OfType<RunBehaviorTreeGraphNode>().Single(call => SubTreeRunBy(call) == subTree);

            VisualScriptGraphVariable read = (VisualScriptGraphVariable)WhatFeeds(branch, EnemyVariableNames.MoveSpeed);

            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.MoveSpeed }, read.Function.DeriveReadKeys());
        }
    }
}
