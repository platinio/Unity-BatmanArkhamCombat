using System.IO;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // Both arenas place the same player prefab, so the two cannot drift apart. Nothing compiles the scene and the prefab together, so these read them.
    public class PlayerPrefabContractTests
    {
        private const string PrefabPath = "Assets/ArkhamCombat/Prefabs/Player.prefab";
        private const string CapsuleArenaPath = "Assets/CombatArena.unity";

        private GameObject player;

        [SetUp]
        public void SetUp()
        {
            player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(player, $"no prefab at {PrefabPath}");
        }

        [Test]
        public void TheCapsuleArena_PlacesThePlayerPrefab()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);

            StringAssert.Contains(prefabGuid, File.ReadAllText(CapsuleArenaPath), "the arena's player is an instance of the prefab");
        }

        [Test]
        public void ThePictureSitsOnTheBodyChild_SoAVariantSwapsItByDeactivatingThatChild()
        {
            ProceduralPresentationDriver driver = player.GetComponentInChildren<ProceduralPresentationDriver>();

            Assert.IsNotNull(driver);
            Assert.AreNotSame(player.transform, driver.transform, "a driver on the root would still be found once the capsule child is deactivated");
            Assert.AreSame(driver.transform, driver.Body);
        }
    }
}
