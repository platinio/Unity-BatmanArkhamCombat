using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    [CreateAssetMenu(menuName = "ArkhamCombat/Databases/Hit Receiver Profiles", fileName = "HitReceiverProfiles")]
    public sealed class HitReceiverProfileDatabase : ScriptableDatabase<HitReceiverProfile>
    {
    }
}
