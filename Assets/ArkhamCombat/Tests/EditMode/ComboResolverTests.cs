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
        private AttackDefinition jab;
        private Stance stance;
        private ChainNode neutral;
        private ChainNode s1;
        private IntentBuffer intents;
        private CombatContext context;
        private FakeConditions conditions;
        private ComboResolver resolver;

        [SetUp]
        public void SetUp()
        {
            jab = Attack("Jab", cancelEvade: new Window(0f, 0.3f));
            neutral = new ChainNode("Neutral", (AttackDefinition)null,
                new Edge(IntentKind.Strike, "GlideKick", 1),
                new Edge(IntentKind.Strike, "S1", 2));
            s1 = new ChainNode("S1", jab,
                new Edge(IntentKind.Strike, "Takedown", 1),
                new Edge(IntentKind.Strike, "S2", 2));

            stance = Stance("Neutral",
                new[]
                {
                    neutral, s1,
                    new ChainNode("S2", jab), new ChainNode("GlideKick", jab), new ChainNode("Takedown", jab),
                    new ChainNode("CounterNode", jab), new ChainNode("EvadeNode", jab), new ChainNode("StunNode", jab)
                },
                new[]
                {
                    new Edge(IntentKind.Counter, "CounterNode", 0),
                    new Edge(IntentKind.Evade, "EvadeNode", 1),
                    new Edge(IntentKind.Stun, "StunNode", 2)
                });

            intents = Buffer();
            context = new CombatContext();
            conditions = new FakeConditions();
            resolver = new ComboResolver(conditions);
        }

        private Resolution Resolve(ChainNode node, AttackDefinition attack = null, float t = 0f, bool followUps = true) =>
            resolver.Resolve(stance, new ResolveInput(node, attack, t, followUps), intents, context);

        [Test]
        public void EdgesAreTriedInPriorityOrder_AndAFailedConditionFallsThrough()
        {
            conditions.Deny("GlideKick");
            intents.Push(IntentKind.Strike, Vector2.zero);

            Resolution result = Resolve(neutral);

            Assert.AreEqual("S1", result.Destination.Id);
            Assert.IsFalse(result.IsInterrupt);
            Assert.AreEqual("GlideKick", conditions.Asked[0].Destination, "the lower priority edge was asked first");
        }

        [Test]
        public void TheFirstEdgeWhoseConditionPasses_Wins()
        {
            intents.Push(IntentKind.Strike, Vector2.zero);

            Assert.AreEqual("Takedown", Resolve(s1, jab, 0.6f).Destination.Id);
        }

        [Test]
        public void AnInterrupt_IsTakenBeforeAnyFollowUp()
        {
            intents.Push(IntentKind.Strike, Vector2.zero);
            intents.Push(IntentKind.Stun, Vector2.zero);

            Resolution result = Resolve(s1, jab, 0.6f);

            Assert.AreEqual("StunNode", result.Destination.Id);
            Assert.IsTrue(result.IsInterrupt);
            Assert.AreEqual(IntentKind.Stun, result.Consumed.Kind);
            Assert.IsNotNull(intents.PeekNewest(IntentKind.Strike), "the strike is still queued");
        }

        [Test]
        public void AnIntentThatArrivesWhileTheWindowIsClosed_SurvivesAndMatchesAtTheRoot()
        {
            intents.Push(IntentKind.Strike, Vector2.zero);

            Resolution closed = Resolve(s1, jab, 0.95f, followUps: false);
            Assert.IsFalse(closed.Matched);
            Assert.AreEqual(1, intents.Entries.Count, "nothing was consumed");

            Resolution atRoot = resolver.Resolve(stance, ResolveInput.Idle(neutral), intents, context);
            Assert.AreEqual("GlideKick", atRoot.Destination.Id);
            Assert.AreEqual(0, intents.Entries.Count);
        }

        [Test]
        public void ExactlyOneIntent_IsConsumedPerResolve()
        {
            intents.Push(IntentKind.Strike, Vector2.zero);
            intents.Push(IntentKind.Strike, Vector2.zero);
            intents.Push(IntentKind.Strike, Vector2.zero);

            Assert.IsTrue(Resolve(neutral).Matched);
            Assert.AreEqual(2, intents.Entries.Count);
        }

        [Test]
        public void TheNewestIntentOfAKind_IsTheOneConsumed()
        {
            Intent older = intents.Push(IntentKind.Strike, Vector2.left);
            Intent newer = intents.Push(IntentKind.Strike, Vector2.right);

            Resolution result = Resolve(neutral);

            Assert.AreSame(newer, result.Consumed);
            Assert.AreSame(older, intents.Entries[0]);
        }

        [Test]
        public void Evade_IsGatedByTheCurrentAttacksEvadeWindow()
        {
            intents.Push(IntentKind.Evade, Vector2.zero);

            Assert.IsFalse(Resolve(s1, jab, 0.5f).Matched, "outside the evade window");
            Assert.AreEqual(1, intents.Entries.Count, "a gated edge consumes nothing");
            Assert.AreEqual("EvadeNode", Resolve(s1, jab, 0.1f).Destination.Id);
        }

        [Test]
        public void Evade_IsAlwaysAllowed_WhenNothingPlays()
        {
            intents.Push(IntentKind.Evade, Vector2.zero);

            Assert.AreEqual("EvadeNode", Resolve(neutral).Destination.Id);
        }

        [Test]
        public void Counter_IsGatedByTheTelegraphFact()
        {
            intents.Push(IntentKind.Counter, Vector2.zero);

            Assert.IsFalse(Resolve(s1, jab, 0.1f).Matched);

            context.IncomingAttackCounterable = true;
            Assert.AreEqual("CounterNode", Resolve(s1, jab, 0.1f).Destination.Id);
        }

        [Test]
        public void AnEdgeToAnUnknownNode_IsSkippedWithoutConsuming()
        {
            ChainNode broken = new ChainNode("Broken", jab, new Edge(IntentKind.Strike, "Nowhere"));
            Stance brokenStance = Stance("Broken", new[] { broken });
            intents.Push(IntentKind.Strike, Vector2.zero);

            Resolution result = resolver.Resolve(brokenStance, ResolveInput.Idle(broken), intents, context);

            Assert.IsFalse(result.Matched);
            Assert.AreEqual(1, intents.Entries.Count);
        }

        [Test]
        public void NoIntentOfTheRightKind_MeansNoMatch_AndNoConditionAsked()
        {
            intents.Push(IntentKind.Stun, Vector2.zero);
            conditions.Deny("StunNode");

            Resolve(neutral);

            Assert.AreEqual(1, conditions.Asked.Count, "only the stun edge had an intent to ask about");
        }
    }
}
