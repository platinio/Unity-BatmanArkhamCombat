using System;
using System.Linq;
using System.Reflection;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Presentation;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    /// <summary>
    /// The module boundary from spec 00 and spec 08 T8: DOTween stays in the presentation
    /// assembly, and the combat core is container-agnostic. A referenced assembly only appears in
    /// the compiled metadata when code actually uses it, so these fail the day someone reaches
    /// across the line.
    /// </summary>
    public class AssemblyBoundaryTests
    {
        private static readonly Assembly CombatCore = typeof(ActionRunner).Assembly;
        private static readonly Assembly PresentationAssembly = typeof(ProceduralPresentationDriver).Assembly;
        private static readonly Assembly CharactersAssembly = typeof(CombatActions).Assembly;
        private static readonly Assembly EnemiesAssembly = typeof(EnemyStatus).Assembly;
        private static readonly Assembly EncounterAssembly = typeof(EncounterDirector).Assembly;

        private static readonly string[] GameplayAssemblies =
        {
            "ArkhamCombat.Combat", "ArkhamCombat.Characters", "ArkhamCombat.Player", "ArkhamCombat.Enemies", "ArkhamCombat.Encounter", "ArkhamCombat.Camera", "ArkhamCombat.Shell"
        };

        private static bool HasReferenceTo(Assembly assembly, string referencedName) =>
            assembly.GetReferencedAssemblies().Any(reference => reference.Name.Equals(referencedName, StringComparison.OrdinalIgnoreCase));

        [Test]
        public void TheCore_DoesNotReferenceDOTween()
        {
            Assert.IsFalse(HasReferenceTo(CombatCore, "DOTween"));
        }

        [Test]
        public void TheCore_DoesNotReferenceZenject()
        {
            Assert.IsFalse(HasReferenceTo(CombatCore, "Zenject"));
        }

        [Test]
        public void TheCore_CarriesNoInjectAttribute()
        {
            Type[] types;
            try
            {
                types = CombatCore.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
            }

            const BindingFlags everyDeclaredMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type type in types)
            {
                foreach (MemberInfo member in type.GetMembers(everyDeclaredMember))
                {
                    bool isInjected = member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == "InjectAttribute");
                    Assert.IsFalse(isInjected, $"{type.Name}.{member.Name} carries [Inject]; the core takes collaborators through constructors.");
                }
            }
        }

        [Test]
        public void WhatEveryCharacterShares_DoesNotReferenceThePlayer()
        {
            Assert.IsFalse(HasReferenceTo(CharactersAssembly, "ArkhamCombat.Player"), "an enemy would then drag the player's assembly in");
        }

        [Test]
        public void TheEnemies_DoNotReferenceThePlayer()
        {
            Assert.IsFalse(HasReferenceTo(EnemiesAssembly, "ArkhamCombat.Player"), "what both sides share lives in ArkhamCombat.Characters");
        }

        [Test]
        public void TheEncounter_ReferencesNeitherSideOfTheFight()
        {
            Assert.IsFalse(HasReferenceTo(EncounterAssembly, "ArkhamCombat.Player"), "the director knows agents and a target position, not the player");
            Assert.IsFalse(HasReferenceTo(EncounterAssembly, "ArkhamCombat.Enemies"), "the enemies reference the encounter, never the other way round");
        }

        [Test]
        public void NoGameplayAssembly_ReferencesDOTween()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (GameplayAssemblies.Contains(name))
                {
                    Assert.IsFalse(HasReferenceTo(assembly, "DOTween"), $"{name} references DOTween; only the presentation assembly may.");
                }
            }
        }

        [Test]
        public void NoGameplayAssembly_DrawsWithUnitysGizmos()
        {
            const BindingFlags everyDeclaredMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (GameplayAssemblies.Contains(assembly.GetName().Name))
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        bool drawsWithUnitysGizmos = type.GetMethods(everyDeclaredMethod).Any(method => method.Name.StartsWith("OnDrawGizmos"));
                        Assert.IsFalse(drawsWithUnitysGizmos, $"{type.FullName} draws in OnDrawGizmos, which only the Scene view shows; debug drawing goes through MGizmos.");
                    }
                }
            }
        }

        [Test]
        public void ThePresentationAssembly_IsTheOneThatReferencesDOTween()
        {
            Assert.IsTrue(HasReferenceTo(PresentationAssembly, "DOTween"), "sanity: the reference check would otherwise pass vacuously");
        }
    }
}
