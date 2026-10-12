using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class CombatFactsUpdaterTests
    {
        private const float DeadAheadAngle = 6f;

        private static readonly Vector3 Facing = Vector3.forward;

        private static Vector3 TargetAtDegreesToTheRight(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Facing * 3f;

        [Test]
        public void ATargetWithinTheDeadAheadAngle_IsDeadAhead()
        {
            Assert.AreEqual(TargetSide.DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(3f), DeadAheadAngle));
            Assert.AreEqual(TargetSide.DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(-3f), DeadAheadAngle));
        }

        [Test]
        public void ATargetBeyondTheDeadAheadAngle_IsOnItsSide()
        {
            Assert.AreEqual(TargetSide.Right, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(10f), DeadAheadAngle));
            Assert.AreEqual(TargetSide.Left, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(-10f), DeadAheadAngle));
        }

        [Test]
        public void ACounterableAttackTelegraphedInTheEncounter_IsIncomingForTheCharacter()
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings());
            StandingAgent attacker = new StandingAgent();
            director.Register(attacker, AttackTokenType.Melee);
            GameObject character = HiddenObject("Character");
            CombatFactsUpdater updater = character.AddComponent<CombatFactsUpdater>();
            updater.Construct(runner: null, settings: null, frame: null, targetPicker: null, director);

            try
            {
                updater.MeasureIncomingAttack();
                Assert.IsFalse(updater.Facts.IsIncomingAttackCounterable, "before anyone telegraphs");

                director.TelegraphAnAttack(attacker, isCounterable: true);
                updater.MeasureIncomingAttack();
                Assert.IsTrue(updater.Facts.IsIncomingAttackCounterable, "while the attack is telegraphed");
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void ACharacterOutsideAnyEncounter_HasNoIncomingAttack()
        {
            GameObject character = HiddenObject("Character");
            CombatFactsUpdater updater = character.AddComponent<CombatFactsUpdater>();
            updater.Construct(runner: null, settings: null, frame: null, targetPicker: null);

            try
            {
                updater.MeasureIncomingAttack();

                Assert.IsFalse(updater.Facts.IsIncomingAttackCounterable);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void AWiderDeadAheadAngle_TakesInATargetFartherToTheSide()
        {
            Assert.AreEqual(TargetSide.DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(10f), deadAheadAngle: 20f));
        }
    }
}
