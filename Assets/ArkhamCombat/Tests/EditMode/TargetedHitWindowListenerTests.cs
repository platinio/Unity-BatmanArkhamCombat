using System.Collections.Generic;
#if HERMES_EVENTS_GENERATED
using ArcaneOnyx.GameEventGenerator;
#endif
using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class TargetedHitWindowListenerTests
    {
        private const float StrikeDistance = 1f;

        private GameObject character;
        private MotorWarpMover warpMover;
        private TargetedHitWindowListener listener;
        private AttackDefinition jab;

        [SetUp]
        public void SetUp()
        {
            character = HiddenObject("Character");
            CharacterMotor motor = character.AddComponent<CharacterMotor>();
            warpMover = new MotorWarpMover();
#if HERMES_EVENTS_GENERATED
            listener = new TargetedHitWindowListener(new HitCheckSettings(), motor, warpMover, BuildSceneGameEvents());
#else
            listener = new TargetedHitWindowListener(new HitCheckSettings(), motor, warpMover, null);
#endif
            jab = Attack("Jab", strikeDistance: StrikeDistance);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(jab);
#if HERMES_EVENTS_GENERATED
            Object.DestroyImmediate(hermesObject);
#endif
        }

        private static PointCombatTarget TargetAhead(float distance) => new PointCombatTarget(new Vector3(0f, 0f, distance));

        [Test]
        public void ATargetWithinStrikeDistancePlusTheMargin_IsHit()
        {
            PointCombatTarget target = TargetAhead(1.4f);

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(1, target.HitsTaken);
            Assert.AreSame(jab, target.LastReceived.Attack);
        }

        [Test]
        public void ATargetBeyondTheMargin_IsAWhiff()
        {
            PointCombatTarget target = TargetAhead(1.6f);

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(0, target.HitsTaken);
        }

        [Test]
        public void ATargetBehindTheCharacter_IsAWhiff()
        {
            PointCombatTarget target = new PointCombatTarget(Vector3.back * StrikeDistance);

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(0, target.HitsTaken);
        }

        [Test]
        public void TheWarpStillPendingThisTick_CountsTowardTheRange()
        {
            PointCombatTarget target = TargetAhead(3f);
            warpMover.MoveBy(new Vector3(0f, 0f, 2f));

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(1, target.HitsTaken, "the character lands 1 m from the target this tick");
        }

        [Test]
        public void AnInvalidTarget_IsAWhiff()
        {
            PointCombatTarget target = TargetAhead(1f);
            target.IsValid = false;

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(0, target.HitsTaken);
        }

#if HERMES_EVENTS_GENERATED
        private GameObject hermesObject;
        private List<StrikeLandedEventArgs> landedStrikes;
        private List<StrikeWhiffedEventArgs> whiffedStrikes;

        // Edit mode runs no Awake, so the dispatcher's events are built by hand.
        private ISceneGameEvents BuildSceneGameEvents()
        {
            hermesObject = HiddenObject("Hermes");
            GameEventDispatcher dispatcher = hermesObject.AddComponent<GameEventDispatcher>();
            dispatcher.StrikeLandedGameEvent = new GameEventDispatcher.StrikeLandedEvent(dispatcher);
            dispatcher.StrikeWhiffedGameEvent = new GameEventDispatcher.StrikeWhiffedEvent(dispatcher);

            landedStrikes = new List<StrikeLandedEventArgs>();
            whiffedStrikes = new List<StrikeWhiffedEventArgs>();
            dispatcher.StrikeLandedGameEvent.AddListener(landedStrikes.Add);
            dispatcher.StrikeWhiffedGameEvent.AddListener(whiffedStrikes.Add);

            return new SceneGameEventsWith(dispatcher);
        }

        [Test]
        public void AStrikeThatLands_IsAnnouncedWithItsAttacker_AndNotAsAWhiff()
        {
            listener.HitWindowOpened(jab, TargetAhead(1f));

            Assert.AreEqual(1, landedStrikes.Count);
            Assert.AreSame(character, landedStrikes[0].Attacker);
            Assert.AreSame(jab, landedStrikes[0].Attack);
            Assert.IsEmpty(whiffedStrikes);
        }

        [Test]
        public void AStrikeOutOfRange_IsAnnouncedAsAWhiff()
        {
            listener.HitWindowOpened(jab, TargetAhead(1.6f));

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.AreSame(character, whiffedStrikes[0].Attacker);
            Assert.IsEmpty(landedStrikes);
        }

        [Test]
        public void AStrikeTheTargetIgnores_IsAnnouncedAsAWhiff()
        {
            PointCombatTarget deadTarget = TargetAhead(1f);
            deadTarget.ResultToGive = HitResult.Ignored;

            listener.HitWindowOpened(jab, deadTarget);

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.IsEmpty(landedStrikes);
        }

        [Test]
        public void ATargetThatCannotBeHit_IsAWhiff()
        {
            listener.HitWindowOpened(jab, new PointTarget(new Vector3(0f, 0f, 1f)));

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.IsEmpty(landedStrikes);
        }
#endif
    }
}
