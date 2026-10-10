namespace ArkhamCombat.Combat
{
    public interface IHitCheckSettings
    {
        /// <summary>Metres past the attack's strike distance a target may stand and still be hit.</summary>
        float HitRangeMargin { get; }

        /// <summary>Degrees either side of the attacker's facing a target may stand and still be hit.</summary>
        float HitAngle { get; }
    }
}
