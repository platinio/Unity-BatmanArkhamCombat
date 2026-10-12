using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ReactionPlaybackTests
    {
        private const float ActionSeconds = 0.5f;
        private const float TickSeconds = 0.1f;

        private ActionDefinition knockdown;
        private ActionDefinition getUp;
        private ActionRunner runner;

        [SetUp]
        public void SetUp()
        {
            knockdown = Action("Knockdown", ActionSeconds);
            getUp = Action("GetUp", ActionSeconds);
            runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), new NullWarpMover(), new NullHitWindowListener());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(knockdown);
            Object.DestroyImmediate(getUp);
        }

        private ReactionPlayback StartedPlaybackOf(params ActionDefinition[] actionsInOrder)
        {
            ReactionPlayback playback = new ReactionPlayback(runner, actionsInOrder);
            playback.Start(Vector3.zero);
            return playback;
        }

        private void PlayTheRunnersActionToItsEnd()
        {
            int ticksPastTheEnd = Mathf.RoundToInt(ActionSeconds / TickSeconds) + 1;
            for (int i = 0; i < ticksPastTheEnd; i++)
            {
                runner.Tick(TickSeconds, Vector3.zero, canStartFromIdle: false);
            }
        }

        [Test]
        public void Starting_PlaysTheFirstAction()
        {
            ReactionPlayback playback = StartedPlaybackOf(knockdown, getUp);

            Assert.AreSame(knockdown, runner.CurrentAction);
            Assert.IsFalse(playback.HasEnded);
        }

        [Test]
        public void WhileAnActionStillPlays_TheNextOneWaits()
        {
            ReactionPlayback playback = StartedPlaybackOf(knockdown, getUp);
            runner.Tick(TickSeconds, Vector3.zero, canStartFromIdle: false);

            playback.PlayTheNextActionWhenTheCurrentOneEnds(Vector3.zero);

            Assert.AreSame(knockdown, runner.CurrentAction);
        }

        [Test]
        public void WhenAnActionEnds_TheNextOnePlays()
        {
            ReactionPlayback playback = StartedPlaybackOf(knockdown, getUp);
            PlayTheRunnersActionToItsEnd();

            playback.PlayTheNextActionWhenTheCurrentOneEnds(Vector3.zero);

            Assert.AreSame(getUp, runner.CurrentAction);
            Assert.IsFalse(playback.HasEnded);
        }

        [Test]
        public void WhenTheLastActionEnds_TheReactionHasEnded()
        {
            ReactionPlayback playback = StartedPlaybackOf(knockdown);
            PlayTheRunnersActionToItsEnd();

            playback.PlayTheNextActionWhenTheCurrentOneEnds(Vector3.zero);

            Assert.IsTrue(playback.HasEnded);
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void AMissingAction_IsSkipped()
        {
            ReactionPlayback playback = StartedPlaybackOf(null, getUp);

            playback.PlayTheNextActionWhenTheCurrentOneEnds(Vector3.zero);

            Assert.AreSame(getUp, runner.CurrentAction);
        }

        [Test]
        public void AReactionWithNoActions_EndsAsItStarts()
        {
            ReactionPlayback playback = StartedPlaybackOf();

            Assert.IsTrue(playback.HasEnded);
        }

        [Test]
        public void StartingAgain_PlaysFromTheFirstAction()
        {
            ReactionPlayback playback = StartedPlaybackOf(knockdown, getUp);
            PlayTheRunnersActionToItsEnd();
            playback.PlayTheNextActionWhenTheCurrentOneEnds(Vector3.zero);

            playback.Start(Vector3.zero);

            Assert.AreSame(knockdown, runner.CurrentAction);
        }
    }
}
