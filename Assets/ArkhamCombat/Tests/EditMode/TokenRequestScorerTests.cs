using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // Each test switches on one term of the score.
    public class TokenRequestScorerTests
    {
        private const float FairnessHorizonSeconds = 6f;
        private const float SlotRadius = 4f;
        private const float Tolerance = 1e-4f;
        private const float NoWait = 0f;

        private static readonly Vector3 Target = Vector3.zero;
        private static readonly Vector3 North = Vector3.forward;
        private static readonly Vector3 WhereTheTargetLooks = North;

        private static TokenRequestScorer Scorer(float frontWeight = 0f, float fairnessWeight = 0f, float distancePenalty = 0f, float fairnessHorizonSeconds = FairnessHorizonSeconds)
        {
            return new TokenRequestScorer(new EncounterDirectorSettings(
                slotRadius: SlotRadius,
                frontWeight: frontWeight,
                fairnessWeight: fairnessWeight,
                fairnessHorizonSeconds: fairnessHorizonSeconds,
                distancePenalty: distancePenalty));
        }

        [TestCase(0f, 1f, TestName = "DeadAhead_ScoresTheWholeFrontWeight")]
        [TestCase(90f, 0.5f, TestName = "AtTheSide_ScoresHalfTheFrontWeight")]
        [TestCase(180f, 0f, TestName = "RightBehind_ScoresNoneOfTheFrontWeight")]
        public void TheFrontTerm_FallsOffWithTheAngleFromWhereTheTargetLooks(float degreesOffTheFront, float expectedScore)
        {
            Vector3 agentPosition = Quaternion.Euler(0f, degreesOffTheFront, 0f) * North;

            float score = Scorer(frontWeight: 1f).Score(agentPosition, NoWait, Target, North);

            Assert.AreEqual(expectedScore, score, Tolerance);
        }

        [Test]
        public void ATargetWithNoFacing_HasNoFront()
        {
            float score = Scorer(frontWeight: 1f).Score(North, NoWait, Target, Vector3.zero);

            Assert.AreEqual(0f, score, Tolerance);
        }

        [Test]
        public void AnAgentStandingOnTheTarget_IsNotInFrontOfIt()
        {
            float score = Scorer(frontWeight: 1f).Score(Target, NoWait, Target, North);

            Assert.AreEqual(0f, score, Tolerance);
        }

        [Test]
        public void ATargetLookingUpOrDown_HasItsFrontMeasuredOnTheGround()
        {
            Vector3 lookingNorthAndSteeplyDown = North + Vector3.down * 3f;

            float score = Scorer(frontWeight: 1f).Score(North, NoWait, Target, lookingNorthAndSteeplyDown);

            Assert.AreEqual(1f, score, Tolerance);
        }

        [Test]
        public void WithoutARing_DistanceCostsNothing()
        {
            TokenRequestScorer scorerWithoutARing = new TokenRequestScorer(new EncounterDirectorSettings(slotRadius: 0f, frontWeight: 0f, fairnessWeight: 0f, distancePenalty: 1f));

            float score = scorerWithoutARing.Score(Target, NoWait, Target, WhereTheTargetLooks);

            Assert.AreEqual(0f, score, Tolerance);
        }

        [Test]
        public void TheFrontTerm_IgnoresHeight()
        {
            Vector3 aheadAndUpALedge = North + Vector3.up * 3f;

            float score = Scorer(frontWeight: 1f).Score(aheadAndUpALedge, NoWait, Target, North);

            Assert.AreEqual(1f, score, Tolerance);
        }

        [TestCase(0f, 0f, TestName = "NoWait_ScoresNoneOfTheFairnessWeight")]
        [TestCase(3f, 0.5f, TestName = "HalfTheHorizon_ScoresHalfTheFairnessWeight")]
        [TestCase(6f, 1f, TestName = "TheWholeHorizon_ScoresAllOfTheFairnessWeight")]
        [TestCase(60f, 1f, TestName = "WaitingPastTheHorizon_CountsForNoMore")]
        public void TheFairnessTerm_GrowsWithTheWaitUpToTheHorizon(float secondsWithoutAToken, float expectedScore)
        {
            float score = Scorer(fairnessWeight: 1f).Score(North, secondsWithoutAToken, Target, North);

            Assert.AreEqual(expectedScore, score, Tolerance);
        }

        [Test]
        public void WithAHorizonOfZero_WaitingCountsForNothing()
        {
            float score = Scorer(fairnessWeight: 1f, fairnessHorizonSeconds: 0f).Score(North, 60f, Target, North);

            Assert.AreEqual(0f, score, Tolerance);
        }

        [TestCase(0f, 0f, TestName = "OnTheTarget_CostsNothing")]
        [TestCase(2f, -0.5f, TestName = "HalfTheRingRadiusAway_CostsHalfThePenalty")]
        [TestCase(4f, -1f, TestName = "OnTheRing_CostsTheWholePenalty")]
        [TestCase(40f, -1f, TestName = "BeyondTheRing_CostsNoMore")]
        public void TheDistanceTerm_CostsMoreTheFartherTheAgentIs_UpToTheRing(float metresAway, float expectedScore)
        {
            float score = Scorer(distancePenalty: 1f).Score(North * metresAway, NoWait, Target, North);

            Assert.AreEqual(expectedScore, score, Tolerance);
        }
    }
}
