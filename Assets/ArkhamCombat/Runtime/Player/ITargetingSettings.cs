namespace ArkhamCombat.Player
{
    public interface ITargetingSettings
    {
        float MaxTargetDistance { get; }
        float MaxTargetAngle { get; }
        float AngleCountingAsDoubleDistance { get; }

        /// <summary>A stick pushed less than this is at rest, and the facing stands in for it.</summary>
        float StickPushedMagnitude { get; }
    }
}
