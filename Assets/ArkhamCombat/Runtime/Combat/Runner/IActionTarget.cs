using UnityEngine;

namespace ArkhamCombat.Combat
{
    public interface IActionTarget
    {
        bool IsValid { get; }

        Vector3 Position { get; }
    }
}
