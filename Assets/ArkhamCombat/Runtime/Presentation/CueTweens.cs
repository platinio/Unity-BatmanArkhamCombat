using ArkhamCombat.Combat;
using DG.Tweening;

namespace ArkhamCombat.Presentation
{
    internal static class CueTweens
    {
        public static void Kill(ICueTarget target) => DOTween.Kill(target.TweenId);

        public static void FollowSpeed(ICueTarget target, float speed)
        {
            bool isFrozen = speed <= 0f;
            if (isFrozen)
            {
                DOTween.Pause(target.TweenId);
            }
            else
            {
                DOTween.Play(target.TweenId);
            }
        }
    }
}
