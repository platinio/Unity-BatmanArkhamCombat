using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// An enemy fights whoever its encounter revolves around, so the direction it is asked with
    /// changes nothing.
    /// </summary>
    [RequireComponent(typeof(EncounterAgent))]
    public sealed class EncounterTargetPicker : MonoBehaviour, IActionTargetPicker
    {
        private EncounterAgent encounterAgent;

        internal void Awake() => encounterAgent = GetComponent<EncounterAgent>();

        public IActionTarget PickTarget(Vector2 direction) => CombatTargetOf(encounterAgent.Target);

        // Null when there is no target, or it is one that nothing can strike.
        private static ICombatTarget CombatTargetOf(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            target.TryGetComponent(out ICombatTarget combatTarget);
            return combatTarget;
        }
    }
}
