namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A driver that keeps time and shows nothing. For EditMode tests of the runner and for an agent
    /// that has no body yet. Cues are scheduled and dropped on the floor.
    /// </summary>
    public sealed class NullPresentationDriver : IPresentationDriver
    {
        private readonly ActionClock clock = new ActionClock();

        public bool IsPlaying => clock.IsPlaying;
        public float NormalizedTime => clock.NormalizedTime;

        public float Speed
        {
            get => clock.Speed;
            set => clock.Speed = value;
        }

        public void Play(ActionDefinition action) => clock.Play(action);

        public void Stop() => clock.Stop();

        public void Tick(float deltaTime) => clock.Tick(deltaTime);
    }
}
