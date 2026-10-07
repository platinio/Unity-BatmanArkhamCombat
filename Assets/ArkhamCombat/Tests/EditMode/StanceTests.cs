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
        private static AttackDefinition jab;

        [SetUp]
        public void SetUp() => jab = Attack("Jab");

        private static List<string> Errors(Stance stance)
        {
            List<string> errors = new List<string>();
            stance.Validate(errors);
            return errors;
        }

        [Test]
        public void TheWorkedExampleShape_Validates()
        {
            Stance stance = Stance("Neutral", new[]
            {
                new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "S1")),
                new ChainNode("S1", jab, new Edge(IntentKind.Strike, "S2")),
                new ChainNode("S2", jab, new Edge(IntentKind.Strike, "S1"))
            });

            Assert.IsEmpty(Errors(stance));
        }

        [Test]
        public void TheRootMayPlayNothing_ButAnyOtherNodeMustHaveAnAttackOrAPoolEntry()
        {
            Stance stance = Stance("Neutral", new[]
            {
                new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "Empty")),
                new ChainNode("Empty", new VariantPool(new RandomPolicy()))
            });

            List<string> errors = Errors(stance);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("'Empty' has neither")), "the node itself is reported");
            Assert.IsTrue(errors.Exists(e => e.Contains("points at 'Empty', which has nothing to play")), "and so is the edge into it");
        }

        [Test]
        public void AnEdgeToANodeWithNothingToPlay_IsReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "S1")),
                new ChainNode("S1", jab, new Edge(IntentKind.Strike, "Neutral"))
            });

            List<string> errors = Errors(stance);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("points at 'Neutral', which has nothing to play", errors[0]);
        }

        [Test]
        public void Prepare_LeavesTheAuthoredEdgeOrderAlone()
        {
            Edge low = new Edge(IntentKind.Strike, "A", 1);
            Edge high = new Edge(IntentKind.Strike, "B", 2);
            ChainNode node = new ChainNode("N", jab, high, low);
            Stance stance = Stance("N", new[] { node, new ChainNode("A", jab), new ChainNode("B", jab) });

            Assert.AreSame(low, node.Edges[0], "the sorted view leads with the lower priority");

            UnityEditor.SerializedProperty authored = new UnityEditor.SerializedObject(stance)
                .FindProperty("nodes").GetArrayElementAtIndex(0).FindPropertyRelative("edges");
            Assert.AreEqual("B", authored.GetArrayElementAtIndex(0).FindPropertyRelative("destination").stringValue,
                "the serialized list keeps the designer's order; it is not the cache");
        }

        [Test]
        public void ANodeNothingReaches_IsReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "S1")),
                new ChainNode("S1", jab),
                new ChainNode("Orphan", jab)
            });

            List<string> errors = Errors(stance);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("'Orphan' is not reachable", errors[0]);
        }

        [Test]
        public void AGlobalEdgeDestination_CountsAsReachable()
        {
            Stance stance = Stance("Neutral", new[]
                {
                    new ChainNode("Neutral", (AttackDefinition)null),
                    new ChainNode("Evade", jab)
                },
                new[] { new Edge(IntentKind.Evade, "Evade") });

            Assert.IsEmpty(Errors(stance));
        }

        [Test]
        public void AnEdgeWithNoDestination_AndOneToAnUnknownNode_AreBothReported()
        {
            Stance stance = Stance("Neutral", new[]
            {
                new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, null), new Edge(IntentKind.Strike, "Nowhere"))
            });

            List<string> errors = Errors(stance);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("has no destination", errors[0]);
            StringAssert.Contains("unknown node 'Nowhere'", errors[1]);
        }

        [Test]
        public void DuplicateIds_AndAMissingRoot_AreReported()
        {
            Stance stance = Stance("Missing", new[]
            {
                new ChainNode("A", jab),
                new ChainNode("A", jab)
            });

            List<string> errors = Errors(stance);
            Assert.IsTrue(errors.Exists(e => e.Contains("used more than once")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("root 'Missing' names no node")), string.Join("\n", errors));
        }

        [Test]
        public void Prepare_SortsEdgesByPriority_KeepingAuthoredOrderForTies()
        {
            ChainNode node = new ChainNode("N", jab,
                new Edge(IntentKind.Strike, "C", 2),
                new Edge(IntentKind.Strike, "A", 1),
                new Edge(IntentKind.Strike, "B", 1));
            Stance stance = Stance("N", new[] { node, new ChainNode("A", jab), new ChainNode("B", jab), new ChainNode("C", jab) });

            Assert.AreEqual("A", node.Edges[0].Destination);
            Assert.AreEqual("B", node.Edges[1].Destination);
            Assert.AreEqual("C", node.Edges[2].Destination);
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
    }

    public class VariantPolicyTests
    {
        private AttackDefinition left;
        private AttackDefinition right;

        [SetUp]
        public void SetUp()
        {
            left = Attack("Jab_L");
            right = Attack("Jab_R");
        }

        private VariantPickContext Context(AttackDefinition last, int side, int seed = 1) => new VariantPickContext(last, side, new Random(seed));

        [Test]
        public void NoRepeat_NeverPicksTheLastAttack_WhenThereIsAChoice()
        {
            VariantPool pool = new VariantPool(new NoRepeatPolicy(), left, right);
            AttackDefinition last = left;

            for (int i = 0; i < 50; i++)
            {
                AttackDefinition pick = pool.Pick(Context(last, 0, i));
                Assert.AreNotSame(last, pick, $"pick {i} repeated");
                last = pick;
            }
        }

        [Test]
        public void NoRepeat_WithASingleEntry_ReturnsItEveryTime()
        {
            VariantPool pool = new VariantPool(new NoRepeatPolicy(), left);

            Assert.AreSame(left, pool.Pick(Context(left, 0)));
        }

        [Test]
        public void TargetSide_LeadsWithTheHandNearerTheTarget()
        {
            VariantPool pool = new VariantPool(new TargetSidePolicy(), left, right);

            Assert.AreSame(left, pool.Pick(Context(null, -1)));
            Assert.AreSame(right, pool.Pick(Context(null, 1)));
        }

        [Test]
        public void TargetSide_WithNoSideKnown_FallsBackToNoRepeat()
        {
            VariantPool pool = new VariantPool(new TargetSidePolicy(), left, right);

            Assert.AreSame(right, pool.Pick(Context(left, 0)));
            Assert.AreSame(left, pool.Pick(Context(right, 0)));
        }

        [Test]
        public void AnEmptyPool_PicksNull()
        {
            Assert.IsNull(new VariantPool(new RandomPolicy()).Pick(Context(null, 0)));
        }
    }
}
