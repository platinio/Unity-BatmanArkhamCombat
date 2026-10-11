using System.Text.RegularExpressions;
using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Configuration;
using ArkhamCombat.Characters;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class PlayerStaticInstallerTests
    {
        private PlayerStaticInstaller installer;
        private CharacterProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<CharacterProfile>();
            installer = ScriptableObject.CreateInstance<PlayerStaticInstaller>();
            new DiContainer().Inject(installer);

            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("profile").objectReferenceValue = profile;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(installer);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void NoStates_IsReported()
        {
            LogAssert.Expect(LogType.Error, new Regex("No states listed"));

            installer.InstallBindings();
        }

        [Test]
        public void AnEmptyStateSlot_IsReported()
        {
            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("states").arraySize = 1;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Error, new Regex("State 0 on .* is empty"));

            installer.InstallBindings();
        }

        [Test]
        public void ThePlayersCombatActions_AreTheOnesBesideItsBrain_NotAnEnemys()
        {
            GameObject enemy = HiddenObject("Enemy");
            enemy.AddComponent<CombatActions>();
            GameObject player = HiddenObject("Player");
            CharacterBrain playerBrain = player.AddComponent<CharacterBrain>();
            CombatActions playerCombatActions = player.AddComponent<CombatActions>();

            try
            {
                DiContainer container = new DiContainer();
                container.Bind<CharacterBrain>().FromInstance(playerBrain);

                PlayerStaticInstaller.BindCombatActionsBesideTheBrain(container);

                Assert.AreSame(playerCombatActions, container.Resolve<CombatActions>());
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(player);
            }
        }
    }
}
