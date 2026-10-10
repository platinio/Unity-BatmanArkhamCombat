#if HERMES_EVENTS_GENERATED
using System;


namespace ArcaneOnyx.GameEventGenerator
{
    //{0} Game event name example: OnGameEntityDamage
    //{1} event args signature example: GameEntity Target, float Damage   
    //{2} event args name signature example: target, value   
    //{3} namespaces example: using System;
    public partial class GameEventDispatcher
    {
        public OnApplicationQuitEvent OnApplicationQuitGameEvent;

        public class OnApplicationQuitEvent
        {
            private event Action<OnApplicationQuitEventArgs> OnApplicationQuit;
            private GameEventDispatcher Dispatcher;

            public OnApplicationQuitEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise()
            {
               var args = new OnApplicationQuitEventArgs();
                               
               OnApplicationQuit?.Invoke(args);
               Dispatcher.OnGameEventRaised("OnApplicationQuitGameEvent", args);
            }
            
             public void Raise(OnApplicationQuitEventArgs eventArgs)
             {                                   
                 OnApplicationQuit?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("OnApplicationQuitGameEvent", eventArgs);
             }

            public void AddListener(Action<OnApplicationQuitEventArgs> listener)
            {
                OnApplicationQuit += listener;
            }
            
             public void RemoveListener(Action<OnApplicationQuitEventArgs> listener)
             {
                OnApplicationQuit -= listener;
             }
        }
    }
}
#endif