using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// Moves at a steady rate, so a blend from locomotion to a clip takes the seconds it was given at
    /// any frame rate, and a blend that starts part way takes proportionally less.
    /// </summary>
    public sealed class ClipBlend
    {
        private float secondsForAFullBlend;

        public float Weight { get; private set; }

        public float Target { get; private set; }

        public bool HasReachedItsTarget => Mathf.Approximately(Weight, Target);

        public void BlendTo(float target, float secondsForAFullBlend)
        {
            Target = Mathf.Clamp01(target);
            this.secondsForAFullBlend = Mathf.Max(0f, secondsForAFullBlend);

            bool isACut = this.secondsForAFullBlend <= 0f;
            if (isACut)
            {
                Weight = Target;
            }
        }

        public void Step(float deltaTime)
        {
            if (HasReachedItsTarget)
            {
                return;
            }

            float shareOfAFullBlend = Mathf.Max(0f, deltaTime) / secondsForAFullBlend;
            Weight = Mathf.MoveTowards(Weight, Target, shareOfAFullBlend);
        }
    }
}
