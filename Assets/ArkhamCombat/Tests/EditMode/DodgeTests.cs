using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;
using static ArkhamCombat.Tests.TreeAssets;
using Variables = Unity.VisualScripting.Variables;

namespace ArkhamCombat.Tests
{
    public class DodgeTests
    {
        private const string CanDodgePath = "Assets/ArkhamCombat/Trees/Functions/CanDodge.asset";
        private const string SidestepDirectionPath = "Assets/ArkhamCombat/Trees/Functions/SidestepDirection.asset";
        private const string KnifeRootPath = "Assets/ArkhamCombat/Trees/Roots/KnifeRoot.asset";
        private const float HalfTheTime = 0.5f;
        private const float RollUnderTheChance = 0.2f;
        private const float RollOverTheChance = 0.8f;
        private const float Tolerance = 1e-4f;
        private const int EnoughDodgesToSeeBothSides = 40;
        private const int TimesAGuardMayAskAboutOneStrike = 30;
        private const float Always = 1f;

        private static readonly Vector3 WhereTheEnemyStands = new Vector3(2f, 0f, 3f);
        private static readonly Vector3 WhereItsTargetStands = new Vector3(2f, 0f, -1f);

        private GameObject agent;
        private FunctionGraphAsset canDodge;
        private FunctionGraphAsset sidestepDirection;

        [SetUp]
        public void SetUp()
        {
            canDodge = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(CanDodgePath);
            sidestepDirection = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(SidestepDirectionPath);
            Assert.IsNotNull(canDodge, $"no Function at {CanDodgePath}");
            Assert.IsNotNull(sidestepDirection, $"no Function at {SidestepDirectionPath}");

            agent = HiddenObject("Agent");
            agent.AddComponent<BehaviorTreeMachine>();
            UnityEngine.Random.InitState(3);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(agent);

        private VisualScriptGraphVariable ACallOf(FunctionGraphAsset function, params (string input, object value)[] arguments)
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            VisualScriptGraphVariable call = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(graph, 0f, 0f);
            call.SetFunction(function);
            foreach ((string input, object value) in arguments)
            {
                call.valueInputs[input].SetDefaultValue(value);
            }

            BehaviorTreeMachine machine = agent.GetComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            call.SetMachine(machine);
            graph.OnAwake();
            return call;
        }

        private bool CanDodgeWith(float chance) => (bool)ACallOf(canDodge, ("chance", chance)).Output.GetPortValue();

        private Vector3 ASidestepDirection() =>
            (Vector3)ACallOf(sidestepDirection, ("from", WhereTheEnemyStands), ("around", WhereItsTargetStands)).Output.GetPortValue();

        private void AStrikeIsComingWithTheRoll(float roll)
        {
            Variables.Object(agent).Set(EnemyVariableNames.PlayerStrikeIncoming, true);
            Variables.Object(agent).Set(EnemyVariableNames.IncomingStrikeRoll, roll);
        }

        [Test]
        public void AStrikeWhoseRollIsUnderTheChance_CanBeDodged()
        {
            AStrikeIsComingWithTheRoll(RollUnderTheChance);

            Assert.IsTrue(CanDodgeWith(HalfTheTime));
        }

        [Test]
        public void AStrikeWhoseRollIsOverTheChance_CannotBeDodged()
        {
            AStrikeIsComingWithTheRoll(RollOverTheChance);

            Assert.IsFalse(CanDodgeWith(HalfTheTime));
        }

        [Test]
        public void AskedManyTimesAboutTheSameStrike_TheAnswerNeverChanges()
        {
            AStrikeIsComingWithTheRoll(RollUnderTheChance);

            for (int i = 0; i < TimesAGuardMayAskAboutOneStrike; i++)
            {
                Assert.IsTrue(CanDodgeWith(HalfTheTime), $"asked for the {i + 1}th time");
            }
        }

        [Test]
        public void AnEnemyThatAlwaysDodges_CanDodgeEveryStrikeItIsToldOf_UntilThatStrikeIsOver()
        {
            HitReceiverProfile profile = ReceiverProfile();
            EnemyStatus status = agent.AddComponent<EnemyStatus>();
            status.Construct(profile);
            status.Awake();

            try
            {
                for (int strike = 0; strike < EnoughDodgesToSeeBothSides; strike++)
                {
                    status.StrikeIsComing();
                    Assert.IsTrue(CanDodgeWith(Always), $"strike {strike + 1} was announced");

                    status.StrikeIsOver();
                    Assert.IsFalse(CanDodgeWith(Always), $"strike {strike + 1} is over");
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void WithNoStrikeComing_ThereIsNothingToDodge_WhateverTheRoll()
        {
            Variables.Object(agent).Set(EnemyVariableNames.PlayerStrikeIncoming, false);
            Variables.Object(agent).Set(EnemyVariableNames.IncomingStrikeRoll, RollUnderTheChance);

            Assert.IsFalse(CanDodgeWith(HalfTheTime));
        }

        [Test]
        public void AnAgentThatIsNeverToldOfStrikes_NeverDodges()
        {
            Assert.IsFalse(CanDodgeWith(chance: 1f));
        }

        [Test]
        public void AChanceOfZero_NeverDodges()
        {
            AStrikeIsComingWithTheRoll(roll: 0f);

            Assert.IsFalse(CanDodgeWith(chance: 0f));
        }

        [Test]
        public void CanDodge_ReadsExactlyTheFactsItSaysItWatches()
        {
            string[] factsOfAStrike = { EnemyVariableNames.PlayerStrikeIncoming, EnemyVariableNames.IncomingStrikeRoll };

            CollectionAssert.AreEquivalent(factsOfAStrike, canDodge.DeriveReadKeys());
            CollectionAssert.AreEquivalent(factsOfAStrike, canDodge.WatchedKeys);
        }

        [Test]
        public void ASidestep_GoesAcrossTheLineToTheTarget_OnTheGround()
        {
            Vector3 direction = ASidestepDirection();
            Vector3 towardTheTarget = WhereItsTargetStands - WhereTheEnemyStands;

            Assert.Greater(direction.magnitude, 0f);
            Assert.AreEqual(0f, Vector3.Dot(direction.normalized, towardTheTarget.normalized), Tolerance);
            Assert.AreEqual(0f, direction.y, Tolerance);
        }

        [Test]
        public void OverManyDodges_ASidestepGoesToBothSides()
        {
            bool wentEast = false;
            bool wentWest = false;

            for (int i = 0; i < EnoughDodgesToSeeBothSides; i++)
            {
                Vector3 direction = ASidestepDirection();
                wentEast |= direction.x > 0f;
                wentWest |= direction.x < 0f;
            }

            Assert.IsTrue(wentEast, "never stepped east");
            Assert.IsTrue(wentWest, "never stepped west");
        }

        private static BehaviorTreeGraph Dodge => SubTree("Dodge").graph;

        private static ReactiveGuard TheGuardOnTheKnifesDodge() =>
            Tree(KnifeRootPath).graph.Nodes.OfType<ReactiveGuard>().Single(guard => SubTreeRunBy(guard.Owner) == "Dodge");

        [Test]
        public void ADodgingEnemy_GivesItsTurnBack_StopsWalking_ThenPlaysItsDodge()
        {
            List<BehaviorTreeNode> steps = Dodge.ChildrenInPriorityOrder(Dodge.Nodes.OfType<Sequence>().Single());

            Assert.AreEqual(3, steps.Count);
            Assert.IsInstanceOf<ReleaseAttackToken>(steps[0], "a dodge can take over a swing, and that swing's token must not stay held");
            Assert.IsInstanceOf<StopNavAgent>(steps[1]);
            Assert.IsInstanceOf<PlayActionNode>(steps[2]);
        }

        [Test]
        public void TheTreeSaysWhichWayADodgeGoes_ToOneSideOfTheEncounterTarget()
        {
            PlayActionNode play = Dodge.Nodes.OfType<PlayActionNode>().Single();
            VisualScriptGraphVariable whichWay = (VisualScriptGraphVariable)WhatFeeds(play, nameof(PlayActionNode.Direction));
            VisualScriptGraphVariable whereItIs = (VisualScriptGraphVariable)WhatFeeds(whichWay, "from");
            VisualScriptGraphVariable whereTheTargetIs = (VisualScriptGraphVariable)WhatFeeds(whichWay, "around");

            Assert.AreSame(sidestepDirection, whichWay.Function);
            Assert.IsInstanceOf<GetThisTransform>(WhatFeeds(whereItIs, "of"));
            Assert.IsInstanceOf<GetEncounterTarget>(WhatFeeds(whereTheTargetIs, "of"));
        }

        [Test]
        public void TheDodgeItPlaysUnlessTold_TravelsAndCannotBeHitFromItsFirstMoment()
        {
            ActionDefinition dodge = SubTree("Dodge").optionalDeclarations.Get<ActionDefinition>("dodgeAction");
            List<string> errors = new List<string>();

            Assert.IsInstanceOf<DashDefinition>(dodge);
            Assert.IsTrue(dodge.IsInvulnerableAt(0f), "the strike it answers lands within a few frames");
            Assert.IsTrue(dodge.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void OnTheKnifesRoot_ADodgeTakesOverWhateverRuns_AndIsNotCutShortByItsOwnGuard()
        {
            ReactiveGuard guard = TheGuardOnTheKnifesDodge();

            Assert.IsTrue(guard.TakesOverLowerPriority, "a strike lands within a few frames, so the dodge cannot wait for the root to come round");
            Assert.IsFalse(guard.StopsItsOwnBranch, "the strike is over long before the dodge is");
        }

        [Test]
        public void OnTheKnifesRoot_TheDodgesGuard_AsksCanDodge_WithTheChanceTheArchetypeWriterWrites()
        {
            ReactiveGuard guard = TheGuardOnTheKnifesDodge();
            VisualScriptGraphVariable ask = (VisualScriptGraphVariable)WhatFeeds(guard, "Value");
            VisualScriptGraphVariable chance = (VisualScriptGraphVariable)WhatFeeds(ask, "chance");

            Assert.AreSame(canDodge, ask.Function);
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.DodgeChance }, chance.Function.DeriveReadKeys());
        }

        [Test]
        public void OnTheKnifesRoot_TheDodgesGuard_WakesOnTheFactsOfAStrike()
        {
            string[] keysThatWakeIt = TheGuardOnTheKnifesDodge().Triggers.SelectMany(trigger => trigger.Keys).ToArray();

            CollectionAssert.IsSupersetOf(keysThatWakeIt, new[] { EnemyVariableNames.PlayerStrikeIncoming, EnemyVariableNames.IncomingStrikeRoll });
        }
    }
}
