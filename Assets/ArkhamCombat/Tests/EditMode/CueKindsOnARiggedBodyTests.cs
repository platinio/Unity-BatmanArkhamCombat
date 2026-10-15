using System;
using System.Linq;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    // On a rigged body the clip is the picture: only a kind that adds to it, such as a colour, plays there.
    public class CueKindsOnARiggedBodyTests
    {
        private static readonly (Type kind, bool playsOnARiggedBody)[] EveryKind =
        {
            (typeof(FlashCue), true),
            (typeof(PulseCue), true),
            (typeof(LeanCue), false),
            (typeof(PunchCue), false),
            (typeof(SquashCue), false),
            (typeof(SpinCue), false),
            (typeof(CrumpleCue), false),
            (typeof(FallCue), false),
            (typeof(RiseCue), false),
        };

        [Test]
        public void ColourKindsPlayOnARiggedBody_AndShapingKindsStaySilent()
        {
            foreach ((Type kind, bool playsOnARiggedBody) in EveryKind)
            {
                ICueKind cue = (ICueKind)Activator.CreateInstance(kind);

                Assert.AreEqual(playsOnARiggedBody, cue.CanPlayOnARiggedBody, kind.Name);
            }
        }

        [Test]
        public void EveryKindInTheAssembly_HasAnswered()
        {
            Type[] kindsInTheAssembly = typeof(FlashCue).Assembly.GetTypes()
                .Where(type => typeof(ICueKind).IsAssignableFrom(type) && !type.IsAbstract)
                .ToArray();

            CollectionAssert.AreEquivalent(EveryKind.Select(entry => entry.kind), kindsInTheAssembly, "a new kind decides whether it plays on a rigged body");
        }
    }
}
