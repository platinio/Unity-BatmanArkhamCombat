using System.Collections.Generic;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterAttacksPerTurnTests
    {
        private const float NoTime = 0f;
        private const int SameDrawsEveryRun = 12345;
        private const int ManyTurns = 200;

        private static EncounterDirector DirectorWhoseTurnsAreWorth(int fewestAttacks, int mostAttacks, System.Random random = null) =>
            new EncounterDirector(
                new EncounterDirectorSettings(
                    reacquireCooldownSeconds: 0f,
                    minAttacksPerTurn: fewestAttacks,
                    maxAttacksPerTurn: mostAttacks),
                slotValidator: null,
                random);

        private static StandingAgent Join(EncounterDirector director)
        {
            StandingAgent agent = new StandingAgent(Vector3.forward);
            director.Register(agent, AttackTokenType.Melee);
            return agent;
        }

        [Test]
        public void ATurnWorthOneAttack_EndsWithItsFirstAttack()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 1, mostAttacks: 1);
            StandingAgent agent = Join(director);
            TakeAToken(director, agent);

            director.SpendAnAttackOfTheTurn(agent);

            Assert.IsFalse(director.HasToken(agent));
        }

        [Test]
        public void ATurnWorthTwoAttacks_KeepsTheTokenAfterTheFirst()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 2, mostAttacks: 2);
            StandingAgent agent = Join(director);
            TakeAToken(director, agent);

            director.SpendAnAttackOfTheTurn(agent);

            Assert.IsTrue(director.HasToken(agent));
            Assert.AreEqual(1, director.AttacksLeftInTheTurnOf(agent));
        }

        [Test]
        public void ATurnWorthTwoAttacks_EndsWithTheSecond()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 2, mostAttacks: 2);
            StandingAgent agent = Join(director);
            TakeAToken(director, agent);

            director.SpendAnAttackOfTheTurn(agent);
            director.SpendAnAttackOfTheTurn(agent);

            Assert.IsFalse(director.HasToken(agent));
        }

        [Test]
        public void ATurnThatEndsWithItsLastAttack_IsAnnouncedAsATokenGivenBack()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 1, mostAttacks: 1);
            StandingAgent agent = Join(director);
            TakeAToken(director, agent);
            List<IEncounterAgent> released = new List<IEncounterAgent>();
            director.TokenReleased += released.Add;

            director.SpendAnAttackOfTheTurn(agent);

            CollectionAssert.AreEqual(new[] { agent }, released);
        }

        [Test]
        public void AnAgentWithoutAToken_HasNoAttackToSpend()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 2, mostAttacks: 2);
            StandingAgent agent = Join(director);

            director.SpendAnAttackOfTheTurn(agent);
            TakeAToken(director, agent);

            Assert.AreEqual(2, director.AttacksLeftInTheTurnOf(agent), "spending without a token took nothing from the turn that followed");
        }

        [Test]
        public void ATokenGivenBackEarly_TakesItsUnspentAttacksWithIt()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 2, mostAttacks: 2);
            StandingAgent agent = Join(director);
            TakeAToken(director, agent);

            director.ReleaseToken(agent);

            Assert.AreEqual(0, director.AttacksLeftInTheTurnOf(agent));
        }

        [Test]
        public void TheAttacksATurnIsWorth_ArePickedFromTheFewestToTheMost_AndEveryNumberComesUp()
        {
            EncounterDirector director = DirectorWhoseTurnsAreWorth(fewestAttacks: 1, mostAttacks: 3, new System.Random(SameDrawsEveryRun));
            StandingAgent agent = Join(director);
            HashSet<int> attacksSeen = new HashSet<int>();

            for (int turn = 0; turn < ManyTurns; turn++)
            {
                TakeAToken(director, agent);
                attacksSeen.Add(director.AttacksLeftInTheTurnOf(agent));
                director.ReleaseToken(agent);
                director.Tick(NoTime);
            }

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, attacksSeen);
        }

        [Test]
        public void ATurn_IsWorthAtLeastOneAttack()
        {
            EncounterDirectorSettings settings = new EncounterDirectorSettings(minAttacksPerTurn: 0, maxAttacksPerTurn: 0);

            Assert.AreEqual(1, settings.MinAttacksPerTurn);
            Assert.AreEqual(1, settings.MaxAttacksPerTurn);
        }

        [Test]
        public void TheFewestAttacks_AreNeverMoreThanTheMost()
        {
            EncounterDirectorSettings settings = new EncounterDirectorSettings(minAttacksPerTurn: 3, maxAttacksPerTurn: 2);

            Assert.AreEqual(2, settings.MinAttacksPerTurn);
        }
    }
}
