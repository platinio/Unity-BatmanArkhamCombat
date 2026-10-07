namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Where the runner reads time from. The runner never owns a clock of its own, so hit-stop and
    /// speed changes made on the driver stay honest in every window and warp. For this demo the
    /// driver is procedural; a clip-backed one is the upgrade path and changes nothing here.
    /// </summary>
    public interface IPresentationDriver
    {
        /// <summary>Resets time, drops whatever was playing and schedules the action's cues.</summary>
        void Play(ActionDefinition action);

        /// <summary>Drops the action and returns the body to rest.</summary>
        void Stop();

        bool IsPlaying { get; }

        /// <summary>Elapsed over duration, clamped to [0, 1]. Zero while nothing plays.</summary>
        float NormalizedTime { get; }

        /// <summary>One normally. Zero freezes time and every cue; the hit-stop listener sets it briefly.</summary>
        float Speed { get; set; }

        void Tick(float deltaTime);
    }
}
