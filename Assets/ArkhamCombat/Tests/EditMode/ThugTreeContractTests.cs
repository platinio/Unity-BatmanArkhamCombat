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
using static ArkhamCombat.Tests.TreeAssets;

namespace ArkhamCombat.Tests
{
    // The code and the canvases agree on names, on order and on which step runs what, and nothing
    // compiles the two together. So these read the authored assets.
    public class ThugTreeContractTests
    {
        private const string RootPath = ThugRootPath;
        private const string HasTargetInCombatRangePath = "Assets/ArkhamCombat/Trees/Functions/HasTargetInCombatRange.asset";
        private const string SwitchedOnOrOff = "enabled";
        private const string AttackSubTree = "Attack";
        private const string ProwlSubTree = "Prowl";
        private const string TauntSubTree = "Taunt";

        [TestCase("Flinch")]
        [TestCase("Stagger")]
        [TestCase("KnockedDown")]
        [TestCase("Stunned")]
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

            CollectionAssert.AreEqual(
                new[] { "Death", "KnockedDown", "Stunned", "Stagger", "Flinch", "Attack", TauntSubTree, ProwlSubTree, "Approach", "Idle" }, subTreesInPriorityOrder);
        }


        [Test]
        [TestCase(AttackSubTree)]
        [TestCase(TauntSubTree)]
        [TestCase(ProwlSubTree)]
        public void EveryBranchThatNeedsTheTargetInRange_AsksTheSameFunction(string subTree)
        {
            FunctionGraphAsset hasTargetInCombatRange = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(HasTargetInCombatRangePath);
            ConditionalExecution guard = Tree(RootPath).graph.Nodes.OfType<ConditionalExecution>().Single(candidate => SubTreeRunBy(candidate.Owner) == subTree);

            Assert.AreSame(hasTargetInCombatRange, FunctionAsking(guard));
        }

        [Test]
        public void TheRoot_HandsTheAttackBranchTheAttackTheArchetypeWriterWrites()
        {
            BehaviorTreeNode attackBranch = BranchesOfTheRootInPriorityOrder().Single(branch => SubTreeRunBy(branch) == AttackSubTree);

            GetVariable read = (GetVariable)WhatFeeds(attackBranch, EnemyVariableNames.Attack);

            Assert.AreEqual(EnemyVariableNames.Attack, read.defaultValues[nameof(GetVariable.Key)]);
            Assert.IsTrue(AttackTree().requiredDeclarations.IsDefined(EnemyVariableNames.Attack));
        }

        [Test]
        public void AnAttack_AsksForTheTurnFirst_AndGivesItBackLast_EvenWhenTheSwingFails()
        {
            List<BehaviorTreeNode> steps = StepsOfTheAttack();

            Assert.AreEqual(3, steps.Count);
            Assert.IsInstanceOf<TryAcquireAttackToken>(steps[0]);
            Assert.IsInstanceOf<ReturnSuccess>(steps[1], "a walk-in that times out must not skip giving the turn back");
            Assert.IsInstanceOf<ReleaseAttackToken>(steps[2]);
        }

        [Test]
        public void TheSwing_SetsItsSpeed_WalksIn_SquaresUp_AndAsksWhetherItIsStillItsTurn_BeforeItStrikes()
        {
            List<BehaviorTreeNode> stepsOfTheSwing = StepsOfTheSwing();

            Assert.AreEqual(6, stepsOfTheSwing.Count);
            Assert.IsInstanceOf<SetNavAgentSpeedNode>(stepsOfTheSwing[0], "a thug that was prowling must not walk in at its prowling speed");
            Assert.IsInstanceOf<MoveToStrikeDistanceNode>(stepsOfTheSwing[1]);
            Assert.AreEqual("FaceTarget", SubTreeRunBy(stepsOfTheSwing[2]));
            Assert.IsInstanceOf<TryAcquireAttackToken>(stepsOfTheSwing[3], "a thug that lost the turn while it walked in must not swing");
            Assert.IsInstanceOf<ParallelSelector>(stepsOfTheSwing[4]);
        }

        [Test]
        public void EachSwing_SpendsOneAttackOfTheTurn_OnceItsStrikeIsOver()
        {
            Assert.IsInstanceOf<SpendAttackOfTheTurn>(StepsOfTheSwing().Last());
        }

        [Test]
        public void TheSwing_IsRepeatedUntilItFails_SoATurnCanBeWorthSeveralAttacks()
        {
            BehaviorTreeGraph attack = AttackTree().graph;
            BehaviorTreeNode swingOrGiveUp = StepsOfTheAttack().OfType<ReturnSuccess>().Single();

            Assert.IsInstanceOf<UntilFailure>(attack.ChildrenInPriorityOrder(swingOrGiveUp).Single());
        }

        [Test]
        public void ASwing_OnlyStartsWhileTheThugHoldsItsToken_AskedOfTheDirectorAndNotOfAFactAFrameOld()
        {
            ConditionalExecution guard = AttackTree().graph.Nodes.OfType<ConditionalExecution>().Single(candidate => candidate.Owner == TheSwing());

            Assert.IsInstanceOf<HasAttackToken>(WhatFeeds(guard, "Value"));
            Assert.IsNotInstanceOf<ReactiveGuard>(guard, "a swing that has started is not cut short when the turn ends under it");
        }

        [Test]
        public void TheStrike_EndsWhenItsActionDoes_EvenWhenTheActionCouldNotPlay()
        {
            BehaviorTreeGraph attack = AttackTree().graph;
            BehaviorTreeNode strike = StepsOfTheSwing().OfType<ParallelSelector>().Single();

            BehaviorTreeNode sideThatEndsTheStrike = attack.ChildrenInPriorityOrder(strike).First();

            Assert.IsInstanceOf<ReturnSuccess>(sideThatEndsTheStrike, "a Play Action that fails would otherwise leave the tracking side running for ever");
            Assert.IsInstanceOf<PlayActionNode>(attack.ChildrenInPriorityOrder(sideThatEndsTheStrike).Single());
        }

        [Test]
        public void AnAttackingThug_TracksItsTarget_ExactlyWhileTheFactTheRelayWritesIsTrue()
        {
            ReactiveGuard guard = AttackTree().graph.Nodes.OfType<ReactiveGuard>().Single();

            Assert.AreEqual("FaceTarget", SubTreeRunBy(guard.Owner));
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.Telegraphing }, FunctionAsking(guard).DeriveReadKeys(), "what the guard asks");
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.Telegraphing }, guard.Triggers.SelectMany(trigger => trigger.Keys).ToArray(), "what wakes it");
        }

        [Test]
        public void TheTrackingGuard_StartsTheMomentTheTelegraphOpens_AndStopsTheMomentItCloses()
        {
            ReactiveGuard guard = AttackTree().graph.Nodes.OfType<ReactiveGuard>().Single();

            Assert.IsTrue(guard.TakesOverLowerPriority);
            Assert.IsTrue(guard.StopsItsOwnBranch);
        }

        [Test]
        public void AThugHoldingItsPlace_GoesWhereTheDirectorSaysItStands_SoOneThatWalkedInDoesNotWalkBackOut()
        {
            BehaviorTreeGraph holdSlot = Tree(SubTreesFolder + "HoldSlot.asset").graph;
            SetNavAgentPosition walk = holdSlot.Nodes.OfType<SetNavAgentPosition>().Single();

            Assert.IsInstanceOf<GetStandingPosition>(WhatFeeds(walk, nameof(SetNavAgentPosition.NavPosition)));
        }

        private static BehaviorTreeGraphAsset AttackTree() => Tree(SubTreesFolder + AttackSubTree + ".asset");

        private static List<BehaviorTreeNode> StepsOfTheAttack()
        {
            BehaviorTreeGraph attack = AttackTree().graph;
            BehaviorTreeNode spine = attack.ChildrenInPriorityOrder(attack.EntryNode).Single();
            return attack.ChildrenInPriorityOrder(spine);
        }

        private static List<BehaviorTreeNode> StepsOfTheSwing() => AttackTree().graph.ChildrenInPriorityOrder(TheSwing());

        private static BehaviorTreeNode TheSwing()
        {
            BehaviorTreeGraph attack = AttackTree().graph;
            BehaviorTreeNode swingOrGiveUp = StepsOfTheAttack().OfType<ReturnSuccess>().Single();
            BehaviorTreeNode whileTheTurnLasts = attack.ChildrenInPriorityOrder(swingOrGiveUp).Single();
            return attack.ChildrenInPriorityOrder(whileTheTurnLasts).Single();
        }

        [Test]
        public void TheRingsGuards_WakeOnTheFactsTheUpdaterWrites()
        {
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.HasTarget, EnemyVariableNames.InCombatRange }, KeysThatWakeTheGuardOn(ProwlSubTree));
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
        [TestCase("Stunned")]
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
