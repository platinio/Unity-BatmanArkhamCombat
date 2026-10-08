using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using Resolution = ArkhamCombat.Combat.Resolution;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ComboResolverTests
    {
        private const float InsideTheEvadeWindow = 0.1f;
        private const float AfterTheEvadeWindow = 0.5f;
        private const float InsideTheComboWindow = 0.6f;
        private const float AfterTheComboWindow = 0.95f;
        private const float EarlyInTheAttack = 0.1f;

        private TestIntentKinds kinds;
        private AttackDefinition jab;
        private Stance stance;
        private ChainNode neutralNode;
        private ChainNode firstStrikeNode;
        private IntentBuffer intents;
        private CombatContext context;
        private RecordingConditions conditions;
        private ComboResolver resolver;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds();
            jab = Attack("Jab", evadeWindow: new Window(0f, 0.3f));
            neutralNode = NodeWithNothingToPlay("Neutral",
                new Edge(kinds.Strike, "GlideKick", priority: 1),
                new Edge(kinds.Strike, "S1", priority: 2));
            firstStrikeNode = new ChainNode("S1", jab,
                new Edge(kinds.Strike, "Takedown", priority: 1),
                new Edge(kinds.Strike, "S2", priority: 2));

            stance = Stance("Neutral",
                new[]
                {
                    neutralNode, firstStrikeNode,
                    new ChainNode("S2", jab), new ChainNode("GlideKick", jab), new ChainNode("Takedown", jab),
                    new ChainNode("CounterNode", jab), new ChainNode("EvadeNode", jab), new ChainNode("StunNode", jab)
                },
                new[]
                {
                    new Edge(kinds.Counter, "CounterNode", priority: 0),
                    new Edge(kinds.Evade, "EvadeNode", priority: 1),
                    new Edge(kinds.Stun, "StunNode", priority: 2)
                });

            intents = new IntentBuffer();
            context = new CombatContext();
            conditions = new RecordingConditions();
            resolver = new ComboResolver(conditions, kinds.InterruptKinds);
        }

        [TearDown]
        public void TearDown() => kinds.Destroy();

        [Test]
        public void EdgesAreTriedInPriorityOrder_AndAFailedConditionMovesOnToTheNextEdge()
        {
            conditions.Deny("GlideKick");
            Press(kinds.Strike);

            Resolution result = ResolveIdleAt(neutralNode);

            Assert.AreEqual("S1", result.Destination.Id);
            Assert.IsFalse(result.IsInterrupt);
            Assert.AreEqual("GlideKick", conditions.AskedEdges[0].DestinationId, "the edge with the lower priority number is asked first");
        }

        [Test]
        public void TheFirstEdgeWhoseConditionPasses_Wins()
        {
            Press(kinds.Strike);

            Assert.AreEqual("Takedown", ResolveWhilePlaying(firstStrikeNode, jab, InsideTheComboWindow).Destination.Id);
        }

        [Test]
        public void AnInterrupt_IsTakenBeforeAnyFollowUp()
        {
            Press(kinds.Strike);
            Press(kinds.Stun);

            Resolution result = ResolveWhilePlaying(firstStrikeNode, jab, InsideTheComboWindow);

            Assert.AreEqual("StunNode", result.Destination.Id);
            Assert.IsTrue(result.IsInterrupt);
            Assert.AreSame(kinds.Stun, result.ConsumedIntent.Kind);
            Assert.IsNotNull(intents.FindNewest(kinds.Strike), "the strike is still queued");
        }

        [Test]
        public void APressAfterTheComboWindow_StaysQueued_AndMatchesOnceBackAtTheRoot()
        {
            Press(kinds.Strike);

            Resolution afterTheComboWindow = ResolveWhilePlaying(firstStrikeNode, jab, AfterTheComboWindow, canContinueCombo: false);
            Assert.IsFalse(afterTheComboWindow.HasMatch);
            Assert.AreEqual(1, intents.Queued.Count, "nothing was consumed");

            Resolution atTheRoot = ResolveIdleAt(neutralNode);
            Assert.AreEqual("GlideKick", atTheRoot.Destination.Id);
            Assert.AreEqual(0, intents.Queued.Count);
        }

        [Test]
        public void EachResolve_ConsumesExactlyOnePress()
        {
            Press(kinds.Strike);
            Press(kinds.Strike);
            Press(kinds.Strike);

            Assert.IsTrue(ResolveIdleAt(neutralNode).HasMatch);
            Assert.AreEqual(2, intents.Queued.Count);
        }

        [Test]
        public void TheNewestPressOfAKind_IsTheOneConsumed()
        {
            Intent older = intents.Push(kinds.Strike, Vector2.left);
            Intent newer = intents.Push(kinds.Strike, Vector2.right);

            Resolution result = ResolveIdleAt(neutralNode);

            Assert.AreSame(newer, result.ConsumedIntent);
            Assert.AreSame(older, intents.Queued[0]);
        }

        [Test]
        public void Evade_IsOnlyAllowedInsideTheCurrentAttacksEvadeWindow()
        {
            Press(kinds.Evade);

            Assert.IsFalse(ResolveWhilePlaying(firstStrikeNode, jab, AfterTheEvadeWindow).HasMatch, "after the evade window");
            Assert.AreEqual(1, intents.Queued.Count, "an edge that is not allowed consumes nothing");
            Assert.AreEqual("EvadeNode", ResolveWhilePlaying(firstStrikeNode, jab, InsideTheEvadeWindow).Destination.Id);
        }

        [Test]
        public void Evade_IsNotAllowed_WhileAPlainActionPlays()
        {
            Press(kinds.Evade);

            Resolution result = ResolveWhilePlaying(firstStrikeNode, attack: null, InsideTheEvadeWindow, canContinueCombo: false);

            Assert.IsFalse(result.HasMatch, "a reaction has no evade window, so it cannot be evaded out of");
            Assert.AreEqual(1, intents.Queued.Count);
        }

        [Test]
        public void Evade_IsAlwaysAllowed_WhenNothingPlays()
        {
            Press(kinds.Evade);

            Assert.AreEqual("EvadeNode", ResolveIdleAt(neutralNode).Destination.Id);
        }

        [Test]
        public void Counter_IsOnlyAllowedWhileTheIncomingAttackIsCounterable()
        {
            Press(kinds.Counter);

            Assert.IsFalse(ResolveWhilePlaying(firstStrikeNode, jab, EarlyInTheAttack).HasMatch);

            context.IsIncomingAttackCounterable = true;
            Assert.AreEqual("CounterNode", ResolveWhilePlaying(firstStrikeNode, jab, EarlyInTheAttack).Destination.Id);
        }

        [Test]
        public void AnEdgeToAnUnknownNode_IsSkippedWithoutConsuming()
        {
            ChainNode broken = new ChainNode("Broken", jab, new Edge(kinds.Strike, "Nowhere"));
            Stance brokenStance = Stance("Broken", new[] { broken });
            Press(kinds.Strike);

            Resolution result = resolver.Resolve(brokenStance, ComboSituation.Idle(broken), intents, context);

            Assert.IsFalse(result.HasMatch);
            Assert.AreEqual(1, intents.Queued.Count);
        }

        [Test]
        public void AnEdgeWithNoMatchingPress_IsNotAskedAboutItsCondition()
        {
            Press(kinds.Stun);
            conditions.Deny("StunNode");

            ResolveIdleAt(neutralNode);

            Assert.AreEqual(1, conditions.AskedEdges.Count, "only the stun edge had a press to ask about");
        }

        private void Press(IntentKind kind) => intents.Push(kind, Vector2.zero);

        private Resolution ResolveIdleAt(ChainNode node) =>
            resolver.Resolve(stance, ComboSituation.Idle(node), intents, context);

        /// <summary>A null <paramref name="attack"/> stands for a plain action, such as a reaction.</summary>
        private Resolution ResolveWhilePlaying(ChainNode node, AttackDefinition attack, float normalizedTime, bool canContinueCombo = true) =>
            resolver.Resolve(stance, new ComboSituation(node, attack, normalizedTime, canContinueCombo, isPlaying: true), intents, context);
    }
}
