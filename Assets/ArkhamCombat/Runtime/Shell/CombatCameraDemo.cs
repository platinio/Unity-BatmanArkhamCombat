using ArcaneOnyx.TPCharacterController.CameraRig;
using ArkhamCombat.Cameras;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Shell
{
    /// <summary>
    /// The demo is combat only, so there is no other mode to switch from. Stand-in for the
    /// encounter director until spec 06 lands: the hand-placed enemies here are what the director's
    /// roster will replace.
    /// </summary>
    public sealed class CombatCameraDemo : MonoBehaviour
    {
        private const float ReadoutWidth = 900f;
        private const float ReadoutHeight = 60f;

        [Tooltip("Capsules standing in for the roster. Disabled ones are ignored.")]
        [SerializeField] private Transform[] enemies;

        [Tooltip("Top-left corner of the readout.")]
        [SerializeField] private Vector2 origin = new Vector2(12f, 12f);

        private PlayerCameraRig rig;
        private GroupFramingSource groupSource;

        [Inject]
        private void Construct(PlayerCameraRig rig) => this.rig = rig;

        private void Start()
        {
            if (rig == null)
            {
                Debug.LogError($"[{nameof(CombatCameraDemo)}] No PlayerCameraRig was injected; is there a SceneContext?", this);
                enabled = false;
                return;
            }

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
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            CameraFraming framing = rig.CurrentFraming;
            GroupFraming.Result group = groupSource.LastResult;
            string bearing = group.HasYawTarget ? $"{group.CentroidYaw:0}" : "held";

            GUI.Label(
                new Rect(origin.x, origin.y, ReadoutWidth, ReadoutHeight),
                $"Combat camera   enemies {(group.HasGroup ? "yes" : "none")}   target yaw {bearing}   yaw {rig.Yaw:0}   pitch {rig.Pitch:0}\n" +
                $"distance {framing.Distance:0.00}   fov {framing.Fov:0}   pivot offset {framing.PivotOffset.x:0.00},{framing.PivotOffset.z:0.00}");
        }
    }
}
