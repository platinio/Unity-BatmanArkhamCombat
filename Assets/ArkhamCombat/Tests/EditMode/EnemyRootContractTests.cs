using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.TreeAssets;

namespace ArkhamCombat.Tests
{
    public class EnemyRootContractTests
    {
        private const string RootsFolder = TreesFolder + "/Roots";
        private const string PrefabsFolder = "Assets/ArkhamCombat/Prefabs/";
        private const string ArchetypesPath = "Assets/ArkhamCombat/Archetypes/EnemyArchetypes.asset";
        private const string KindOfReactionAskedAbout = "kind";
        private const int JobsEveryEnemyTicks = 4;

        private static readonly string[] ReactionsAStatusCanPublish =
        {
            nameof(AppliedReaction.Flinch), nameof(AppliedReaction.Stagger), nameof(AppliedReaction.Knockdown), nameof(AppliedReaction.Stun)
        };

        private static string[] EveryRoot() =>
            AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset", new[] { RootsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .ToArray();

        private static BehaviorTreeGraph Root(string rootName) => Tree($"{RootsFolder}/{rootName}.asset").graph;

        private static List<string> BranchesInPriorityOrder(string rootName)
        {
            BehaviorTreeGraph root = Root(rootName);
            return root.ChildrenInPriorityOrder(root.Nodes.OfType<Selector>().Single()).Select(SubTreeRunBy).ToList();
        }

        private static ConditionalExecution GuardOf(string rootName, string subTree) =>
            Root(rootName).Nodes.OfType<ConditionalExecution>().Single(guard => SubTreeRunBy(guard.Owner) == subTree);

        private static List<string> ReactionsWithABranchOn(string rootName)
        {
            List<string> kinds = new List<string>();
            foreach (VisualScriptGraphVariable functionCall in Root(rootName).Nodes.OfType<VisualScriptGraphVariable>())
            {
                ValueInput kind = functionCall.valueInputs.FirstOrDefault(input => input.key == KindOfReactionAskedAbout);
                if (kind != null)
                {
                    kinds.Add(kind.GetValue<string>());
                }
            }

            return kinds;
        }

        private static GameObject Prefab(string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + prefabName + ".prefab");
            Assert.IsNotNull(prefab, $"no prefab named {prefabName}");
            return prefab;
        }

        private static EnemyArchetype[] EveryArchetype() => AssetDatabase.LoadAllAssetsAtPath(ArchetypesPath).OfType<EnemyArchetype>().ToArray();

        private static EnemyArchetype ArchetypeNamed(string archetypeName)
        {
            EnemyArchetype archetype = EveryArchetype().SingleOrDefault(candidate => candidate.Name == archetypeName);
            Assert.IsNotNull(archetype, $"no archetype named {archetypeName} in {ArchetypesPath}");
            return archetype;
        }

        private static EnemyArchetype ArchetypeOf(GameObject prefab) =>
            (EnemyArchetype)new SerializedObject(prefab.GetComponent<EnemyInstaller>()).FindProperty("archetype").objectReferenceValue;

        [Test]
        public void ThereIsMoreThanOneKindOfEnemy()
        {
            CollectionAssert.IsSupersetOf(EveryRoot(), new[] { "ThugRoot", "ArmoredRoot", "KnifeRoot" });
        }

        [TestCaseSource(nameof(EveryRoot))]
        public void EveryRoot_HasABranchForEveryReactionAStatusCanPublish(string rootName)
        {
            CollectionAssert.AreEquivalent(
                ReactionsAStatusCanPublish, ReactionsWithABranchOn(rootName),
                "a reaction with no branch is a fact nothing plays and nothing clears, so the enemy stays in it for ever");
        }

        [TestCaseSource(nameof(EveryRoot))]
        public void EveryRoot_TriesDeathFirst_AndEveryReactionBeforeAnythingItChoosesToDo(string rootName)
        {
            List<string> branches = BranchesInPriorityOrder(rootName);
            int lastReaction = new[] { "KnockedDown", "Stunned", "Stagger", "Flinch" }.Max(branches.IndexOf);

            Assert.AreEqual("Death", branches[0]);
            Assert.Less(lastReaction, branches.IndexOf("Attack"));
            Assert.AreEqual("Idle", branches.Last());
        }

        [TestCaseSource(nameof(EveryRoot))]
        public void OnEveryRoot_AKnockedDownEnemyGetsUpBeforeAnythingLighterIsHeard(string rootName)
        {
            ReactiveGuard guard = (ReactiveGuard)GuardOf(rootName, "KnockedDown");

            Assert.IsTrue(guard.TakesOverLowerPriority);
            Assert.IsFalse(guard.StopsItsOwnBranch);
        }

        [TestCaseSource(nameof(EveryRoot))]
        public void OnEveryRoot_ASwingThatStartedIsNotCutShortByItsOwnGuard(string rootName)
        {
            Assert.IsNotInstanceOf<ReactiveGuard>(GuardOf(rootName, "Attack"));
        }

        [TestCaseSource(nameof(EveryRoot))]
        public void EveryRoot_HandsItsAttackBranchTheAttackAndTheSpeedTheArchetypeWriterWrites(string rootName)
        {
            BehaviorTreeNode attackBranch = Root(rootName).Nodes.OfType<RunBehaviorTreeGraphNode>().Single(call => SubTreeRunBy(call) == "Attack");

            GetVariable attackRead = (GetVariable)WhatFeeds(attackBranch, EnemyVariableNames.Attack);
            VisualScriptGraphVariable speedRead = (VisualScriptGraphVariable)WhatFeeds(attackBranch, EnemyVariableNames.MoveSpeed);

            Assert.AreEqual(EnemyVariableNames.Attack, attackRead.defaultValues[nameof(GetVariable.Key)]);
            CollectionAssert.AreEquivalent(new[] { EnemyVariableNames.MoveSpeed }, speedRead.Function.DeriveReadKeys());
        }

        [Test]
        public void TheArmoredEnemy_HoldsItsPlaceWhereAThugProwls_AndNeverTaunts()
        {
            CollectionAssert.AreEqual(
                new[] { "Death", "KnockedDown", "Stunned", "Stagger", "Flinch", "Attack", "HoldSlot", "Approach", "Idle" },
                BranchesInPriorityOrder("ArmoredRoot"));
        }

        [Test]
        public void TheKnifeEnemy_CanDodge_AndNeverTaunts()
        {
            CollectionAssert.AreEqual(
                new[] { "Death", "KnockedDown", "Stunned", "Stagger", "Flinch", "Dodge", "Attack", "Prowl", "Approach", "Idle" },
                BranchesInPriorityOrder("KnifeRoot"));
        }

        [TestCase("prowlSpeed")]
        [TestCase("sidestepDistance")]
        public void TheKnifeEnemy_ProwlsWiderAndQuickerThanAThug_ByWhatItsRootPassesTheBranch(string parameter)
        {
            float OnTheRoot(string rootName) =>
                Root(rootName).Nodes.OfType<RunBehaviorTreeGraphNode>().Single(call => SubTreeRunBy(call) == "Prowl").valueInputs[parameter].GetValue<float>();

            Assert.Greater(OnTheRoot("KnifeRoot"), OnTheRoot("ThugRoot"));
        }

        [Test]
        public void TheKnifeEnemy_DodgesSomeStrikes_AndItsSlashCannotBeCountered()
        {
            EnemyArchetype knife = ArchetypeOf(Prefab("Knife"));

            Assert.Greater(knife.DodgeChance, 0f);
            Assert.IsFalse(knife.Attack.IsCounterable, "the only answer to it is to evade");
            Assert.IsTrue(knife.Attack.IsUnblockable);
            Assert.IsFalse(knife.HitReceiverProfile.IsArmored);
        }

        [Test]
        public void OnlyTheKnifeEnemy_Dodges()
        {
            Assert.AreEqual(0f, ArchetypeOf(Prefab("Thug")).DodgeChance);
            Assert.AreEqual(0f, ArchetypeOf(Prefab("Armored")).DodgeChance);
            CollectionAssert.DoesNotContain(BranchesInPriorityOrder("ThugRoot"), "Dodge");
            CollectionAssert.DoesNotContain(BranchesInPriorityOrder("ArmoredRoot"), "Dodge");
        }

        [TestCase("Armored", "ArmoredRoot")]
        [TestCase("Knife", "KnifeRoot")]
        public void AnEnemyPrefab_RunsItsOwnRoot_AndKeepsTheJobsOfTheThugItIsAVariantOf(string prefabName, string rootName)
        {
            GameObject prefab = Prefab(prefabName);
            SerializedProperty jobs = new SerializedObject(prefab.GetComponent<ArcaneOnyx.TPCharacterController.CharacterComponentUpdater>()).FindProperty("characterComponents");

            Assert.AreEqual(rootName, prefab.GetComponent<BehaviorTreeMachine>().nest.macro.name);
            Assert.AreEqual("Thug", PrefabUtility.GetCorrespondingObjectFromSource(prefab).name);
            Assert.AreEqual(JobsEveryEnemyTicks, jobs.arraySize);
        }

        [TestCase("Armored")]
        [TestCase("Knife")]
        public void AnEnemyPrefab_NamesAnArchetypeOfItsOwnThatIsWellFormed(string prefabName)
        {
            EnemyArchetype archetype = ArchetypeOf(Prefab(prefabName));
            List<string> errors = new List<string>();

            Assert.IsNotNull(archetype, $"{prefabName} names no archetype");
            Assert.AreEqual(prefabName, archetype.Name);
            Assert.IsTrue(archetype.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void EveryArchetypeInTheDatabase_IsWellFormed()
        {
            foreach (EnemyArchetype archetype in EveryArchetype())
            {
                List<string> errors = new List<string>();

                Assert.IsTrue(archetype.Validate(errors), string.Join("\n", errors));
            }
        }

        [Test]
        public void TheEliteThug_IsOnlyAnotherArchetype_WithNoPrefabAndNoRootOfItsOwn()
        {
            Assert.IsNotNull(ArchetypeNamed("EliteThug"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "EliteThug.prefab"));
            CollectionAssert.DoesNotContain(EveryRoot(), "EliteThugRoot");
        }

        [Test]
        public void TheEliteThug_HasMoreHealthThanAThug_HitsHarder_AndTauntsLess()
        {
            EnemyArchetype elite = ArchetypeNamed("EliteThug");
            EnemyArchetype thug = ArchetypeNamed("Thug");

            Assert.Greater(elite.HitReceiverProfile.MaxHealth, thug.HitReceiverProfile.MaxHealth);
            Assert.Greater(elite.Attack.Damage, thug.Attack.Damage);
            Assert.Less(elite.TauntChance, thug.TauntChance);
        }

        [Test]
        public void TheArmoredEnemy_IsArmored_CannotBeKnockedDown_AndSwingsSomethingThatCanBeCountered()
        {
            EnemyArchetype armored = ArchetypeOf(Prefab("Armored"));

            Assert.IsTrue(armored.HitReceiverProfile.IsArmored);
            Assert.IsFalse(armored.HitReceiverProfile.CanBeKnockedDown);
            Assert.IsTrue(armored.Attack.IsCounterable);
            Assert.AreEqual(0f, armored.TauntChance);
        }

        [Test]
        public void TheArmoredEnemysSwing_WarnsLongerThanTheThugs()
        {
            AttackDefinition heavySwing = ArchetypeOf(Prefab("Armored")).Attack;
            AttackDefinition haymaker = ArchetypeOf(Prefab("Thug")).Attack;

            Assert.Greater(heavySwing.TelegraphWindow.Length * heavySwing.Duration, haymaker.TelegraphWindow.Length * haymaker.Duration);
        }
    }
}
