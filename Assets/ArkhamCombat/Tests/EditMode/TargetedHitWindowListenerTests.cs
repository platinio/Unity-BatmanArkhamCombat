using System.Collections.Generic;
#if HERMES_EVENTS_GENERATED
using ArcaneOnyx.GameEventGenerator;
#endif
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class TargetedHitWindowListenerTests
    {
        private const float StrikeDistance = 1f;
        private const float WithinReach = 1f;
        private const float BeyondReach = 1.6f;

        private GameObject character;
        private PendingWarpMovement pendingWarpMovement;
        private TargetedHitWindowListener listener;
        private AttackDefinition jab;

        [SetUp]
        public void SetUp()
        {
            character = HiddenObject("Character");
            pendingWarpMovement = new PendingWarpMovement();
#if HERMES_EVENTS_GENERATED
            listener = new TargetedHitWindowListener(new HitCheckSettings(), character.transform, pendingWarpMovement, BuildSceneGameEventsRecordingStrikes());
#else
            listener = new TargetedHitWindowListener(new HitCheckSettings(), character.transform, pendingWarpMovement, null);
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
            pendingWarpMovement.MoveBy(new Vector3(0f, 0f, 2f));

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
        private List<StrikeEvadedEventArgs> evadedStrikes;

        private ISceneGameEvents BuildSceneGameEventsRecordingStrikes()
        {
            hermesObject = HiddenObject("Hermes");
            GameEventDispatcher dispatcher = hermesObject.AddComponent<GameEventDispatcher>();
            dispatcher.StrikeLandedGameEvent = new GameEventDispatcher.StrikeLandedEvent(dispatcher);
            dispatcher.StrikeWhiffedGameEvent = new GameEventDispatcher.StrikeWhiffedEvent(dispatcher);
            dispatcher.StrikeEvadedGameEvent = new GameEventDispatcher.StrikeEvadedEvent(dispatcher);

            landedStrikes = new List<StrikeLandedEventArgs>();
            whiffedStrikes = new List<StrikeWhiffedEventArgs>();
            evadedStrikes = new List<StrikeEvadedEventArgs>();
            dispatcher.StrikeLandedGameEvent.AddListener(landedStrikes.Add);
            dispatcher.StrikeWhiffedGameEvent.AddListener(whiffedStrikes.Add);
            dispatcher.StrikeEvadedGameEvent.AddListener(evadedStrikes.Add);

            return new SceneGameEventsWith(dispatcher);
        }

        [Test]
        public void AStrikeInASceneWhoseDispatcherIsGone_StillLands_AndAnnouncesNothing()
        {
            GameObject sceneEventsObject = HiddenObject("Hermes whose dispatcher is gone");
            GameEventDispatcher dispatcherThatIsGone = sceneEventsObject.AddComponent<GameEventDispatcher>();
            Object.DestroyImmediate(dispatcherThatIsGone);
            TargetedHitWindowListener listenerInThatScene = new TargetedHitWindowListener(
                new HitCheckSettings(), character.transform, pendingWarpMovement, new SceneGameEventsWith(dispatcherThatIsGone));
            PointCombatTarget target = TargetAhead(WithinReach);

            try
            {
                Assert.DoesNotThrow(() => listenerInThatScene.HitWindowOpened(jab, target));
                Assert.AreEqual(1, target.HitsTaken);
            }
            finally
            {
                Object.DestroyImmediate(sceneEventsObject);
            }
        }

        [Test]
        public void AStrikeItsTargetEvades_IsAnnouncedAsEvaded_AndNeitherLandedNorWhiffed()
        {
            GameObject victimObject = HiddenObject("Victim");
            PointCombatTarget target = TargetAhead(WithinReach);
            target.Owner = victimObject;
            target.ResultToGive = HitResult.Evaded;

            try
            {
                listener.HitWindowOpened(jab, target);

                Assert.AreEqual(1, evadedStrikes.Count);
                Assert.AreSame(character, evadedStrikes[0].Attacker);
                Assert.AreSame(jab, evadedStrikes[0].Attack);
                Assert.AreSame(victimObject, evadedStrikes[0].Target);
                Assert.IsEmpty(landedStrikes);
                Assert.IsEmpty(whiffedStrikes);
            }
            finally
            {
                Object.DestroyImmediate(victimObject);
            }
        }

        [Test]
        public void AStrikeOutOfReachOfAnInvulnerableTarget_IsAWhiff_NotAnEvade()
        {
            PointCombatTarget target = TargetAhead(BeyondReach);
            target.ResultToGive = HitResult.Evaded;

            listener.HitWindowOpened(jab, target);

            Assert.IsEmpty(evadedStrikes);
            Assert.AreEqual(1, whiffedStrikes.Count);
        }

        [Test]
        public void AStrikeThatLands_IsAnnouncedAsLanded()
        {
            listener.HitWindowOpened(jab, TargetAhead(WithinReach));

            Assert.AreEqual(1, landedStrikes.Count);
            Assert.AreSame(character, landedStrikes[0].Attacker);
            Assert.AreSame(jab, landedStrikes[0].Attack);
            Assert.IsEmpty(whiffedStrikes);
        }

        [Test]
        public void AStrikeThatLands_NamesTheObjectItsTargetLivesOn()
        {
            GameObject victimObject = HiddenObject("Victim");
            PointCombatTarget target = TargetAhead(WithinReach);
            target.Owner = victimObject;

            listener.HitWindowOpened(jab, target);

            Assert.AreSame(victimObject, landedStrikes[0].Target);
            Object.DestroyImmediate(victimObject);
        }

        [Test]
        public void AStrikeThatLandsOnATargetLivingOnNoObject_NamesNone()
        {
            listener.HitWindowOpened(jab, TargetAhead(WithinReach));

            Assert.AreEqual(1, landedStrikes.Count);
            Assert.IsNull(landedStrikes[0].Target);
        }

        [Test]
        public void AStrikeAtNoTarget_IsAWhiff()
        {
            listener.HitWindowOpened(jab, target: null);

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.IsEmpty(landedStrikes);
        }

        [Test]
        public void AStrikeOutOfRange_IsAnnouncedAsAWhiff()
        {
            listener.HitWindowOpened(jab, TargetAhead(BeyondReach));

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.AreSame(character, whiffedStrikes[0].Attacker);
            Assert.IsEmpty(landedStrikes);
        }

        [Test]
        public void AStrikeTheTargetIgnores_IsAnnouncedAsAWhiff()
        {
            PointCombatTarget deadTarget = TargetAhead(WithinReach);
            deadTarget.ResultToGive = HitResult.Ignored;

            listener.HitWindowOpened(jab, deadTarget);

            Assert.AreEqual(1, whiffedStrikes.Count);
            Assert.IsEmpty(landedStrikes);
        }

        [Test]
        public void AStrikeAtAnInvalidTarget_IsAnnouncedAsAWhiff()
        {
            PointCombatTarget goneTarget = TargetAhead(WithinReach);
            goneTarget.IsValid = false;

            listener.HitWindowOpened(jab, goneTarget);

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
