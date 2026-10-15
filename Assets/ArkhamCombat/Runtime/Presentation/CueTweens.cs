using ArkhamCombat.Combat;
using DG.Tweening;

namespace ArkhamCombat.Presentation
{
    /// <summary>Every tween a cue starts carries its target as its id, so a driver kills or freezes them as a group.</summary>
    internal static class CueTweens
    {
        public static void Kill(ICueTarget target) => DOTween.Kill(target.TweenId);

        public static void Freeze(ICueTarget target) => DOTween.Pause(target.TweenId);

        public static void Resume(ICueTarget target) => DOTween.Play(target.TweenId);
    }
}
