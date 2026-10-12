using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterTargetPickerTests
    {
        private static readonly Vector2 AnyDirection = Vector2.right;

        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject target;
        private GameObject thug;
        private EncounterAgent agent;
        private EncounterTargetPicker picker;
        private HitReceiverProfile profile;

        [SetUp]
        public void SetUp()
        {
            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(new EncounterDirector(new EncounterDirectorSettings()));
            target = HiddenObject("Target");
            profile = ReceiverProfile();

            thug = HiddenObject("Thug");
            agent = thug.AddComponent<EncounterAgent>();
            picker = thug.AddComponent<EncounterTargetPicker>();
            picker.Awake();
            agent.Construct(encounter);
            agent.OnEnable();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(encounterObject);
            Object.DestroyImmediate(profile);
        }

        private EnemyStatus MakeTheTargetAFighter()
        {
            EnemyStatus targetsStatus = target.AddComponent<EnemyStatus>();
            targetsStatus.Construct(profile);
            targetsStatus.Awake();
            return targetsStatus;
        }

        [Test]
        public void ItPicksWhoeverTheEncounterRevolvesAround()
        {
            EnemyStatus targetsStatus = MakeTheTargetAFighter();
            encounter.SetTarget(target.transform);

            IActionTarget picked = picker.PickTarget(AnyDirection);

            Assert.AreSame(targetsStatus, picked);
        }

        [Test]
        public void ATargetNothingCanStrike_IsNotPicked()
        {
            encounter.SetTarget(target.transform);

            Assert.IsNull(picker.PickTarget(AnyDirection));
        }

        [Test]
        public void WhileTheEncounterHasNoTarget_NothingIsPicked()
        {
            Assert.IsNull(picker.PickTarget(AnyDirection));
        }
    }
}
