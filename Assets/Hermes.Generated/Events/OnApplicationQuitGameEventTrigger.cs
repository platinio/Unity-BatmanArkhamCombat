#if HERMES_EVENTS_GENERATED
using System;


namespace ArcaneOnyx.GameEventGenerator
{
    public class OnApplicationQuitGameEventTrigger : EventTriggerBase
    {
        
    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.OnApplicationQuitGameEvent.Raise();
        }
    }
}
#endif