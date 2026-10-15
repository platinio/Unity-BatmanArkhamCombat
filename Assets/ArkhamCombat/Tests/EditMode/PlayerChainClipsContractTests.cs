using System.Collections.Generic;
using System.Linq;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // The chain's three steps are tuned to the three parts of one of Frank's combos; these pin that pairing.
    public class PlayerChainClipsContractTests
    {
        private const string StancePath = "Assets/ArkhamCombat/Stances/Ground.asset";
        private const string ClipsPath = "Assets/ArkhamCombat/Characters/Adam.CharacterClips.asset";
        private const string FranksSecondCombo = "Frank_RPG_Assassin_Combo02_";
        private const float PaceToleranceSeconds = 0.02f;

        private static bool ThePackIsImported => AssetDatabase.IsValidFolder("Assets/Frank_Assassin");

        private Stance ground;
        private CharacterClips clips;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ThePackIsImported, "needs the Frank RPG Assassin pack at its default path");
            ground = AssetDatabase.LoadAssetAtPath<Stance>(StancePath);
            Assert.IsNotNull(ground, $"no stance at {StancePath}");
            ground.Prepare();
            clips = AssetDatabase.LoadAssetAtPath<CharacterClips>(ClipsPath);
            Assert.IsNotNull(clips, $"no clips at {ClipsPath}");
        }

        private static IEnumerable<ActionDefinition> AttacksOf(ChainNode node)
        {
            if (node.Action != null)
            {
                yield return node.Action;
            }

            if (node.Pool != null)
            {
                foreach (AttackDefinition attack in node.Pool.Attacks)
                {
                    yield return attack;
                }
            }
        }

        private List<ChainNode> TheStepsOfTheChain()
        {
            Edge firstStrike = ground.Root.Edges.Single();
            List<ChainNode> steps = new List<ChainNode>();
            string nextId = firstStrike.DestinationId;
            while (steps.All(step => step.Id != nextId))
            {
                Assert.IsTrue(ground.TryGetNode(nextId, out ChainNode step), $"the chain leads to '{nextId}', which is no node");
                steps.Add(step);
                nextId = step.Edges.Single(edge => edge.IntentKind == firstStrike.IntentKind).DestinationId;
            }

            return steps;
        }

        private ClipEntry EntryOf(ActionDefinition attack)
        {
            Assert.IsTrue(clips.TryFindEntry(attack, out ClipEntry entry), $"{attack.name} has no clip on Adam");
            return entry;
        }

        [Test]
        public void TheChain_HasThreeSteps_ThatComeBackRound()
        {
            Assert.AreEqual(3, TheStepsOfTheChain().Count);
        }

        [Test]
        public void EachStepOfTheChain_PlaysThatPartOfFranksSecondCombo()
        {
            List<ChainNode> steps = TheStepsOfTheChain();

            for (int i = 0; i < steps.Count; i++)
            {
                string expectedClipFile = $"{FranksSecondCombo}{i + 1}_IP";
                foreach (ActionDefinition attack in AttacksOf(steps[i]))
                {
                    string clipPath = AssetDatabase.GetAssetPath(EntryOf(attack).Clip);
                    StringAssert.Contains(expectedClipFile, clipPath, $"step {i + 1} ('{steps[i].Id}') plays {attack.name} from the wrong clip");
                }
            }
        }

        [Test]
        public void EachStepOfTheChain_RunsAtItsClipsOwnPace()
        {
            foreach (ActionDefinition attack in TheStepsOfTheChain().SelectMany(AttacksOf))
            {
                ClipEntry entry = EntryOf(attack);
                float secondsOfClipUsed = (entry.End - entry.Start) * entry.Clip.length;

                Assert.AreEqual(secondsOfClipUsed, attack.Duration, PaceToleranceSeconds, $"{attack.name} would stretch or squeeze its clip");
            }
        }
    }
}
