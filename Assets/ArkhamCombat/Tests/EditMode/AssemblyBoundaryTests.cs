using System;
using System.Linq;
using System.Reflection;
using ArkhamCombat.Combat;
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

        private static readonly string[] GameplayAssemblies =
        {
            "ArkhamCombat.Combat", "ArkhamCombat.Player", "ArkhamCombat.Enemies", "ArkhamCombat.Camera", "ArkhamCombat.Shell"
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
        public void ThePresentationAssembly_IsTheOneThatReferencesDOTween()
        {
            Assert.IsTrue(HasReferenceTo(PresentationAssembly, "DOTween"), "sanity: the reference check would otherwise pass vacuously");
        }
    }
}
