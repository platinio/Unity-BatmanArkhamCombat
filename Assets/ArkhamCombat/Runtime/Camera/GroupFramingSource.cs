using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.CameraRig;
using ArcaneOnyx.TPCharacterController.Configuration;
using UnityEngine;

namespace ArkhamCombat.Cameras
{
    /// <summary>
    /// Takes any list of transforms as the group. The encounter director's roster plugs in here
    /// once it exists; until then the demo hands it a few capsules.
    /// </summary>
    public sealed class GroupFramingSource : ICameraFramingSource
    {
        private readonly CameraRigConfig config;
        private readonly IReadOnlyList<Transform> members;
        private readonly List<Vector3> positions = new List<Vector3>();

        public GroupFramingSource(CameraRigConfig config, IReadOnlyList<Transform> members)
        {
            this.config = config;
            this.members = members;
        }

        public GroupFraming.Result LastResult { get; private set; }

        public bool AcceptsLookInput => config.Combat.LookControl;

        public CameraFraming GetFraming(in FramingContext context)
        {
            CameraFraming framing = config.Combat.ToFraming();
            GroupFraming.Result result = Evaluate(context.CharacterPosition, framing.Distance);

            framing.PivotOffset += result.PivotOffset;
            framing.Distance = result.Distance;
            return framing;
        }

        public bool TryGetYawTarget(in FramingContext context, out float yawDegrees)
        {
            GroupFraming.Result result = Evaluate(context.CharacterPosition, config.Combat.Distance);
            yawDegrees = result.CentroidYaw;
            return result.HasYawTarget;
        }

        private GroupFraming.Result Evaluate(Vector3 characterPosition, float baseDistance)
        {
            positions.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                Transform member = members[i];
                if (member != null && member.gameObject.activeInHierarchy)
                {
                    positions.Add(member.position);
                }
            }

            GroupFraming.Result result = GroupFraming.Compute(
                characterPosition, positions, config.CombatGroup, baseDistance);
            LastResult = result;
            return result;
        }
    }
}
