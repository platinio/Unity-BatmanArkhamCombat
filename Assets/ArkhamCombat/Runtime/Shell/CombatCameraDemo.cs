using ArcaneOnyx.TPCharacterController.CameraRig;
using ArkhamCombat.Cameras;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Shell
{
    /// <summary>
    /// Binds the combat framing to the rig from the first frame. The demo is combat only, so there
    /// is no other mode to switch from. Stand-in for the encounter director until spec 06 lands: the
    /// hand-placed enemies here are what the director's roster will replace.
    /// </summary>
    public sealed class CombatCameraDemo : MonoBehaviour
    {
        [Tooltip("Capsules standing in for the roster. Disabled ones are ignored.")]
        [SerializeField] private Transform[] enemies;

        [Inject] private PlayerCameraRig rig;

        private GroupFramingSource groupSource;

        private void Start()
        {
            if (rig == null)
            {
                Debug.LogError($"[{nameof(CombatCameraDemo)}] No PlayerCameraRig was injected; is there a SceneContext?", this);
                enabled = false;
                return;
            }

            // The rig runs its Start first (DefaultExecutionOrder -100), so its config is resolved.
            if (rig.Config == null)
            {
                Debug.LogError($"[{nameof(CombatCameraDemo)}] The rig has no config; did it fail to start?", this);
                enabled = false;
                return;
            }

            groupSource = new GroupFramingSource(rig.Config, enemies);
            rig.SetFramingSource(groupSource, rig.Config.FramingSmoothTime, immediate: true);
        }

        private void OnGUI()
        {
            CameraFraming framing = rig.CurrentFraming;
            GroupFraming.Result group = groupSource.LastResult;
            string bearing = group.HasYawTarget ? $"{group.CentroidYaw:0}" : "held";

            GUI.Label(
                new Rect(12, 12, 900, 60),
                $"Combat camera   enemies {(group.HasGroup ? "yes" : "none")}   target yaw {bearing}   yaw {rig.Yaw:0}   pitch {rig.Pitch:0}\n" +
                $"distance {framing.Distance:0.00}   fov {framing.Fov:0}   pivot offset {framing.PivotOffset.x:0.00},{framing.PivotOffset.z:0.00}");
        }
    }
}
