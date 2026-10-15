using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The arena places the player prefab rather than a player of its own, so a second arena cannot drift from it.
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
        public void TheCapsuleArena_PlacesAnInstanceOfThePlayerPrefab()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            string aPrefabInstanceOfIt = $"m_SourcePrefab: {{fileID: 100100000, guid: {prefabGuid}";

            StringAssert.Contains(aPrefabInstanceOfIt, File.ReadAllText(CapsuleArenaPath));
        }

        [Test]
        public void ThePictureSitsOnTheBodyChild_SoAVariantSwapsItByDeactivatingThatChild() => AssertThePictureSitsOnTheBodyChild(player);
    }
}
