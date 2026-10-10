#if HERMES_EVENTS_GENERATED
namespace ArcaneOnyx.GameEventGenerator
{
    // {0} game event initialization example: OnGameEntityDamage = new OnGameEntityDamageEvent(this);

    public partial class GameEventDispatcher
    {       
        private void Awake()
        {
            OnApplicationQuitGameEvent = new OnApplicationQuitEvent(this);
StrikeLandedGameEvent = new StrikeLandedEvent(this);
StrikeWhiffedGameEvent = new StrikeWhiffedEvent(this);
ComboChangedGameEvent = new ComboChangedEvent(this);
ComboResetGameEvent = new ComboResetEvent(this);
ActionStartedGameEvent = new ActionStartedEvent(this);
ActionEndedGameEvent = new ActionEndedEvent(this);

        }

    }  
}
#endif