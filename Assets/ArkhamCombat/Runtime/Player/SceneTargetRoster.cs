using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>A stand-in until the encounter director exists.</summary>
    public sealed class SceneTargetRoster : ITargetRoster
    {
        private readonly List<ICombatTarget> targets = new List<ICombatTarget>();
        private bool hasScanned;

        public IReadOnlyList<ICombatTarget> Targets
        {
            get
            {
                if (!hasScanned)
                {
                    Refresh();
                }

                return targets;
            }
        }

        private void Refresh()
        {
            targets.Clear();
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is ICombatTarget target)
                {
                    targets.Add(target);
                }
            }

            hasScanned = true;
        }
    }
}
