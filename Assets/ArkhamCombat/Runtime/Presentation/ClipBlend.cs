using UnityEngine;

namespace ArkhamCombat.Presentation
{
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

        public void CutTo(float weight)
        {
            Target = Mathf.Clamp01(weight);
            Weight = Target;
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
