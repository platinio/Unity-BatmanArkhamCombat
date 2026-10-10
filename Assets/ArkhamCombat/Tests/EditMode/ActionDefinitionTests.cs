using System.Collections.Generic;
using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ActionDefinitionTests
    {
        [Test]
        public void AWellFormedAttack_Validates()
        {
            List<string> errors = new List<string>();

            Assert.IsTrue(Attack("Jab").Validate(errors), string.Join("\n", errors));
            Assert.IsEmpty(errors);
        }

        [Test]
        public void ACueWithoutAKind_IsReported()
        {
            ActionDefinition action = Action("Bad", 0.5f, new PresentationCue(firesAt: 0.5f, kind: null));
            List<string> errors = new List<string>();

            Assert.IsFalse(action.Validate(errors));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("has no kind", errors[0]);
        }

        [Test]
        public void AReversedWindow_IsReportedByName()
        {
            AttackDefinition attack = Attack("Bad", hitWindow: new Window(0.6f, 0.4f));
            List<string> errors = new List<string>();

            Assert.IsFalse(attack.Validate(errors));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("hitWindow", errors[0]);
        }

        [Test]
        public void AMaxLungeShorterThanTheStrikeDistance_IsReported()
        {
            AttackDefinition attack = Attack("Bad", strikeDistance: 2f, maxLunge: 1f);
            List<string> errors = new List<string>();

            Assert.IsFalse(attack.Validate(errors));
            StringAssert.Contains("maxLunge", errors[0]);
        }
    }
}
