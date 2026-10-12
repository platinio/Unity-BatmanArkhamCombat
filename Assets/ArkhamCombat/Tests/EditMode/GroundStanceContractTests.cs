using System.Collections.Generic;
using System.Linq;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    public class GroundStanceContractTests
    {
        private const string StancePath = "Assets/ArkhamCombat/Stances/Ground.asset";
        private const string ConfigPath = "Assets/ArkhamCombat/Settings/CombatConfig.asset";
        private const string PlayerInstallerPath = "Assets/Installers/Static/PlayerStaticInstaller.asset";

        private static Stance Ground()
        {
            Stance stance = AssetDatabase.LoadAssetAtPath<Stance>(StancePath);
            Assert.IsNotNull(stance, $"no stance at {StancePath}");
            stance.Prepare();
            return stance;
        }

        private static CombatConfig Config()
        {
            CombatConfig config = AssetDatabase.LoadAssetAtPath<CombatConfig>(ConfigPath);
            Assert.IsNotNull(config, $"no combat config at {ConfigPath}");
            return config;
        }

        private static ActionDefinition WhatThePressPlaysFromAnywhere(Stance stance, UnityEngine.Object kindOfPress)
        {
            Edge edge = stance.GlobalEdges.SingleOrDefault(candidate => candidate.IntentKind == kindOfPress);
            Assert.IsNotNull(edge, $"no global edge of {StancePath} takes the {kindOfPress.name} press");
            Assert.IsTrue(stance.TryGetNode(edge.DestinationId, out ChainNode node), $"the edge leads to '{edge.DestinationId}', which is no node");
            return node.Action;
        }

        private static List<string> StatesThePlayerCanBeIn()
        {
            SerializedProperty states = new SerializedObject(AssetDatabase.LoadAssetAtPath<PlayerStaticInstaller>(PlayerInstallerPath)).FindProperty("states");
            List<string> bindings = new List<string>();
            for (int i = 0; i < states.arraySize; i++)
            {
                bindings.Add(states.GetArrayElementAtIndex(i).managedReferenceValue.GetType().Name);
            }

            return bindings;
        }

        [Test]
        public void TheStance_IsWellFormed()
        {
            List<string> errors = new List<string>();

            Assert.IsTrue(Ground().Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void TheEvadePress_PlaysADash_FromAnywhere()
        {
            ActionDefinition evade = WhatThePressPlaysFromAnywhere(Ground(), Config().EvadeKind);

            Assert.IsInstanceOf<DashDefinition>(evade);
        }

        [Test]
        public void TheEvade_CannotBeHit_FromItsFirstMoment()
        {
            ActionDefinition evade = WhatThePressPlaysFromAnywhere(Ground(), Config().EvadeKind);

            Assert.IsTrue(evade.IsInvulnerableAt(0f), "a swing that lands on the frame of the press must already miss");
        }

        [Test]
        public void TheEvade_IsAnActionThatCanPlay()
        {
            ActionDefinition evade = WhatThePressPlaysFromAnywhere(Ground(), Config().EvadeKind);
            List<string> errors = new List<string>();

            Assert.IsTrue(evade.Validate(errors), string.Join("\n", errors));
        }

        private static AttackDefinition TheCounter()
        {
            ActionDefinition counter = WhatThePressPlaysFromAnywhere(Ground(), Config().CounterKind);
            Assert.IsInstanceOf<AttackDefinition>(counter, "a counter strikes, so it has to be an attack");
            return (AttackDefinition)counter;
        }

        [Test]
        public void TheCounterPress_PlaysAnAttackAimedAtWhoeverIsAttacking()
        {
            Assert.IsTrue(TheCounter().IsAimedAtTheAttacker);
        }

        [Test]
        public void TheCounter_BringsTheReactionItsTargetPlays_FromItsFirstFrame()
        {
            Assert.IsTrue(TheCounter().IsPaired);
        }

        [Test]
        public void TheCounter_AndTheReactionItBrings_AreActionsThatCanPlay()
        {
            List<string> errors = new List<string>();

            Assert.IsTrue(TheCounter().Validate(errors), string.Join("\n", errors));
            Assert.IsTrue(TheCounter().PairedReaction.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void TheCounter_GivesNoWarning_SoNoEnemyTreatsItAsATelegraph()
        {
            Assert.IsTrue(TheCounter().TelegraphWindow.IsZeroLength);
        }

        [Test]
        public void TheStunPress_PlaysAnAttackThatAsksForAStun_FromAnywhere()
        {
            UnityEngine.Object stunKind = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/ArkhamCombat/Intents/Stun.asset");
            ActionDefinition stun = WhatThePressPlaysFromAnywhere(Ground(), stunKind);
            List<string> errors = new List<string>();

            Assert.IsInstanceOf<AttackDefinition>(stun);
            Assert.AreEqual(HitReaction.Stun, ((AttackDefinition)stun).Reaction);
            Assert.IsTrue(stun.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void ThePlayer_HasAStateToEvadeIn()
        {
            CollectionAssert.Contains(StatesThePlayerCanBeIn(), nameof(EvadingStateBinding));
        }
    }
}
