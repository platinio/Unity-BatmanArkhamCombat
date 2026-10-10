namespace ArkhamCombat.Combat
{
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
