using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class StanceTests
    {
        private TestIntentKinds kinds;
        private AttackDefinition jab;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds();
            jab = Attack("Jab");
        }

        [TearDown]
        public void TearDown() => kinds.Destroy();

        [Test]
        public void AComboThatLoopsBetweenTwoAttacks_Validates()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                new ChainNode("S1", jab, new Edge(kinds.Strike, "S2")),
                new ChainNode("S2", jab, new Edge(kinds.Strike, "S1"))
            });

            Assert.IsEmpty(ValidationErrors(stance));
        }

        [Test]
        public void TheRootMayPlayNothing_ButAnyOtherNodeMustHaveAnAttackOrAPoolEntry()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "Empty")),
                new ChainNode("Empty", new VariantPool(new RandomPolicy()))
            });

            List<string> errors = ValidationErrors(stance);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(error => error.Contains("'Empty' has neither")), "the node itself is reported");
            Assert.IsTrue(errors.Exists(error => error.Contains("points at 'Empty', which has nothing to play")), "and so is the edge into it");
        }

        [Test]
        public void AnEdgeToANodeWithNothingToPlay_IsReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                new ChainNode("S1", jab, new Edge(kinds.Strike, "Neutral"))
            });

            List<string> errors = ValidationErrors(stance);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("points at 'Neutral', which has nothing to play", errors[0]);
        }

        [Test]
        public void Prepare_LeavesTheAuthoredEdgeOrderAlone()
        {
            Edge runsFirst = new Edge(kinds.Strike, "A", priority: 1);
            Edge runsSecond = new Edge(kinds.Strike, "B", priority: 2);
            ChainNode node = new ChainNode("N", jab, runsSecond, runsFirst);
            Stance stance = Stance("N", new[] { node, new ChainNode("A", jab), new ChainNode("B", jab) });

            Assert.AreSame(runsFirst, node.Edges[0], "the sorted edges lead with the lower priority number");

            UnityEditor.SerializedProperty authoredEdges = new UnityEditor.SerializedObject(stance)
                .FindProperty("nodes").GetArrayElementAtIndex(0).FindPropertyRelative("edges");
            Assert.AreEqual("B", authoredEdges.GetArrayElementAtIndex(0).FindPropertyRelative("destinationId").stringValue,
                "the serialized list keeps the designer's order; only the sorted copy is reordered");
        }

        [Test]
        public void ANodeNothingReaches_IsReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                new ChainNode("S1", jab),
                new ChainNode("Orphan", jab)
            });

            List<string> errors = ValidationErrors(stance);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("'Orphan' is not reachable", errors[0]);
        }

        [Test]
        public void AGlobalEdgeDestination_CountsAsReachable()
        {
            Stance stance = Stance("Neutral", new[]
                {
                    NodeWithNothingToPlay("Neutral"),
                    new ChainNode("Evade", jab)
                },
                new[] { new Edge(kinds.Evade, "Evade") });

            Assert.IsEmpty(ValidationErrors(stance));
        }

        [Test]
        public void AnEdgeWithNoDestination_AndOneToAnUnknownNode_AreBothReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, null), new Edge(kinds.Strike, "Nowhere"))
            });

            List<string> errors = ValidationErrors(stance);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("has no destination", errors[0]);
            StringAssert.Contains("unknown node 'Nowhere'", errors[1]);
        }

        [Test]
        public void AnEdgeWithNoIntentKind_IsReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                NodeWithNothingToPlay("Neutral", new Edge(null, "S1")),
                new ChainNode("S1", jab)
            });

            List<string> errors = ValidationErrors(stance);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("has no intent kind", errors[0]);
        }

        [Test]
        public void DuplicateIds_AndAMissingRoot_AreReported()
        {
            Stance stance = Stance("Missing", new[]
            {
                new ChainNode("A", jab),
                new ChainNode("A", jab)
            });

            List<string> errors = ValidationErrors(stance);
            Assert.IsTrue(errors.Exists(error => error.Contains("used more than once")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(error => error.Contains("root 'Missing' names no node")), string.Join("\n", errors));
        }

        [Test]
        public void Prepare_SortsEdgesByPriority_KeepingAuthoredOrderForTies()
        {
            ChainNode node = new ChainNode("N", jab,
                new Edge(kinds.Strike, "C", priority: 2),
                new Edge(kinds.Strike, "A", priority: 1),
                new Edge(kinds.Strike, "B", priority: 1));
            Stance stance = Stance("N", new[] { node, new ChainNode("A", jab), new ChainNode("B", jab), new ChainNode("C", jab) });

            Assert.AreEqual("A", node.Edges[0].DestinationId);
            Assert.AreEqual("B", node.Edges[1].DestinationId);
            Assert.AreEqual("C", node.Edges[2].DestinationId);
            Assert.AreSame(node, stance.Root);
        }

        [Test]
        public void TryGetNode_FindsById_AndRejectsNull()
        {
            Stance stance = Stance("N", new[] { new ChainNode("N", jab) });

            Assert.IsTrue(stance.TryGetNode("N", out ChainNode node));
            Assert.AreEqual("N", node.Id);
            Assert.IsFalse(stance.TryGetNode(null, out _));
        }

        private static List<string> ValidationErrors(Stance stance)
        {
            List<string> errors = new List<string>();
            stance.Validate(errors);
            return errors;
        }
    }

    public class VariantPolicyTests
    {
        private const int TargetOnTheLeft = -1;
        private const int TargetOnTheRight = 1;
        private const int TargetSideUnknown = 0;

        private AttackDefinition leftJab;
        private AttackDefinition rightJab;

        [SetUp]
        public void SetUp()
        {
            leftJab = Attack("Jab_L");
            rightJab = Attack("Jab_R");
        }

        private static VariantPickContext PickContext(AttackDefinition lastPicked, int targetSide, int randomSeed = 1) =>
            new VariantPickContext(lastPicked, targetSide, new Random(randomSeed));

        [Test]
        public void NoRepeat_NeverPicksTheLastAttack_WhenThereIsAChoice()
        {
            VariantPool pool = new VariantPool(new NoRepeatPolicy(), leftJab, rightJab);
            AttackDefinition lastPicked = leftJab;

            for (int seed = 0; seed < 50; seed++)
            {
                AttackDefinition picked = pool.Pick(PickContext(lastPicked, TargetSideUnknown, seed));
                Assert.AreNotSame(lastPicked, picked, $"pick {seed} repeated the previous attack");
                lastPicked = picked;
            }
        }

        [Test]
        public void NoRepeat_WithASingleEntry_ReturnsItEveryTime()
        {
            VariantPool pool = new VariantPool(new NoRepeatPolicy(), leftJab);

            Assert.AreSame(leftJab, pool.Pick(PickContext(leftJab, TargetSideUnknown)));
        }

        [Test]
        public void TargetSide_LeadsWithTheHandNearerTheTarget()
        {
            VariantPool pool = new VariantPool(new TargetSidePolicy(), leftJab, rightJab);

            Assert.AreSame(leftJab, pool.Pick(PickContext(null, TargetOnTheLeft)));
            Assert.AreSame(rightJab, pool.Pick(PickContext(null, TargetOnTheRight)));
        }

        [Test]
        public void TargetSide_WithNoSideKnown_FallsBackToNoRepeat()
        {
            VariantPool pool = new VariantPool(new TargetSidePolicy(), leftJab, rightJab);

            Assert.AreSame(rightJab, pool.Pick(PickContext(leftJab, TargetSideUnknown)));
            Assert.AreSame(leftJab, pool.Pick(PickContext(rightJab, TargetSideUnknown)));
        }

        [Test]
        public void AnEmptyPool_PicksNull()
        {
            Assert.IsNull(new VariantPool(new RandomPolicy()).Pick(PickContext(null, TargetSideUnknown)));
        }
    }
}
