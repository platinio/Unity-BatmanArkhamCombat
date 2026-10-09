using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// One per-frame combat job on a character. The <see cref="CombatBrain"/> ticks each one in the
    /// order of its list, so a character has exactly the jobs it was given in the inspector.
    /// </summary>
    public abstract class CombatComponent : MonoBehaviour
    {
        public abstract void Tick(float deltaTime);
    }
}
