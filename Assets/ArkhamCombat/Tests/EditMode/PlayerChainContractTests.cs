using System.Collections.Generic;
using System.Linq;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;

namespace ArkhamCombat.Tests
{
    public class PlayerChainContractTests
    {
        private const string StancePath = "Assets/ArkhamCombat/Stances/Ground.asset";
        private const string ClipsPath = "Assets/ArkhamCombat/Characters/Adam.CharacterClips.asset";
        private const string FranksSecondCombo = "Frank_RPG_Assassin_Combo02_";
        private const float PaceToleranceSeconds = 0.02f;

        private static bool ThePackIsImported => AssetDatabase.IsValidFolder("Assets/Frank_Assassin");

        private Stance ground;

        [SetUp]
        public void SetUp()
        {
            ground = AssetDatabase.LoadAssetAtPath<Stance>(StancePath);
            Assert.IsNotNull(ground, $"no stance at {StancePath}");
            ground.Prepare();
        }

        private static CharacterClips AdamsClips()
        {
            Assume.That(ThePackIsImported, "needs the Frank RPG Assassin pack at its default path");
            CharacterClips clips = AssetDatabase.LoadAssetAtPath<CharacterClips>(ClipsPath);
            Assert.IsNotNull(clips, $"no clips at {ClipsPath}");
            return clips;
        }

        private static IEnumerable<AttackDefinition> AttacksOf(ChainNode step)
        {
            if (step.Action != null)
            {
                Assert.IsInstanceOf<AttackDefinition>(step.Action, $"'{step.Id}' plays {step.Action.name}, which is no attack");
                yield return (AttackDefinition)step.Action;
            }

            if (step.Pool != null)
            {
                foreach (AttackDefinition attack in step.Pool.Attacks)
                {
                    yield return attack;
                }
            }
        }

        private static Edge TheStrikeEdgeOf(ChainNode step, Edge firstStrike)
        {
            List<Edge> strikeEdges = step.Edges.Where(edge => edge.IntentKind == firstStrike.IntentKind).ToList();
            Assert.AreEqual(1, strikeEdges.Count, $"'{step.Id}' has {strikeEdges.Count} strike edges; the walk expects one");
            return strikeEdges[0];
        }

        private List<ChainNode> TheStepsOfTheChain(out string whereTheLastStepLeads)
        {
            Edge firstStrike = ground.Root.Edges.Single();
            List<ChainNode> steps = new List<ChainNode>();
            string nextId = firstStrike.DestinationId;
            while (steps.All(step => step.Id != nextId))
            {
                Assert.IsTrue(ground.TryGetNode(nextId, out ChainNode step), $"the chain leads to '{nextId}', which is no node");
                steps.Add(step);
                nextId = TheStrikeEdgeOf(step, firstStrike).DestinationId;
            }

            whereTheLastStepLeads = nextId;
            return steps;
        }

        private IEnumerable<AttackDefinition> TheAttacksOfTheChain() => TheStepsOfTheChain(out _).SelectMany(AttacksOf);

        private static ClipEntry EntryOf(CharacterClips clips, ActionDefinition attack)
        {
            Assert.IsTrue(clips.TryFindEntry(attack, out ClipEntry entry), $"{attack.name} has no clip on Adam");
            return entry;
        }

        [Test]
        public void TheChain_HasThreeSteps_ThatComeBackRound()
        {
            List<ChainNode> steps = TheStepsOfTheChain(out string whereTheLastStepLeads);

            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(steps[0].Id, whereTheLastStepLeads, "the last step's strike does not lead back to the first");
        }

        [Test]
        public void EachAttackOfTheChain_IsAnActionThatCanPlay()
        {
            foreach (AttackDefinition attack in TheAttacksOfTheChain())
            {
                List<string> errors = new List<string>();

                Assert.IsTrue(attack.Validate(errors), string.Join("\n", errors));
            }
        }

        [Test]
        public void EachAttackOfTheChain_WarpsAndCanBeEvaded_UntilItsHitOpens()
        {
            foreach (AttackDefinition attack in TheAttacksOfTheChain())
            {
                Assert.AreEqual(attack.HitWindow.Start, attack.WarpWindow.End, $"{attack.name}'s warp does not run up to its hit");
                Assert.AreEqual(attack.HitWindow.Start, attack.EvadeWindow.End, $"{attack.name} can be evaded past the moment it hits, or not up to it");
            }
        }

        [Test]
        public void EachAttackOfTheChain_TakesTheNextPress_OnlyAfterItsHit()
        {
            foreach (AttackDefinition attack in TheAttacksOfTheChain())
            {
                Assert.GreaterOrEqual(attack.ComboWindow.Start, attack.HitWindow.End, $"{attack.name} would be cut by the next press before its hit");
                Assert.LessOrEqual(attack.ComboWindow.End, 1f, $"{attack.name}'s combo window runs past its end");
            }
        }

        [Test]
        public void EachStepOfTheChain_PlaysThatPartOfFranksSecondCombo()
        {
            CharacterClips clips = AdamsClips();
            List<ChainNode> steps = TheStepsOfTheChain(out _);

            for (int i = 0; i < steps.Count; i++)
            {
                string expectedClipFile = $"{FranksSecondCombo}{i + 1}_IP";
                foreach (AttackDefinition attack in AttacksOf(steps[i]))
                {
                    string clipPath = AssetDatabase.GetAssetPath(EntryOf(clips, attack).Clip);
                    StringAssert.Contains(expectedClipFile, clipPath, $"step {i + 1} ('{steps[i].Id}') plays {attack.name} from the wrong clip");
                }
            }
        }

        [Test]
        public void EachAttackOfTheChain_RunsAtItsClipsOwnPace()
        {
            CharacterClips clips = AdamsClips();

            foreach (AttackDefinition attack in TheAttacksOfTheChain())
            {
                ClipEntry entry = EntryOf(clips, attack);
                float secondsOfClipUsed = (entry.End - entry.Start) * entry.Clip.length;

                Assert.AreEqual(secondsOfClipUsed, attack.Duration, PaceToleranceSeconds, $"{attack.name} would stretch or squeeze its clip");
            }
        }
    }
}
