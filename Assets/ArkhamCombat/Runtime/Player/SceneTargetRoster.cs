using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The roster until the encounter director exists: every <see cref="ICombatTarget"/> component in
    /// the scene, read on first use. <see cref="Refresh"/> re-reads after spawning.
    /// </summary>
    public sealed class SceneTargetRoster : ITargetRoster
    {
        private readonly List<ICombatTarget> targets = new List<ICombatTarget>();
        private bool scanned;

        public IReadOnlyList<ICombatTarget> Targets
        {
            get
            {
                if (!scanned)
                {
                    Refresh();
                }

                return targets;
            }
        }

        public void Refresh()
        {
            targets.Clear();
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is ICombatTarget target)
                {
                    targets.Add(target);
                }
            }

            scanned = true;
        }
    }
}
