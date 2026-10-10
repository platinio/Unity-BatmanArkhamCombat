#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    //{0} Game event name example: OnGameEntityDamage
    //{1} event args signature example: GameEntity Target, float Damage   
    //{2} event args name signature example: target, value   
    //{3} namespaces example: using System;
    public partial class GameEventDispatcher
    {
        public ComboResetEvent ComboResetGameEvent;

        public class ComboResetEvent
        {
            private event Action<ComboResetEventArgs> ComboReset;
            private GameEventDispatcher Dispatcher;

            public ComboResetEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject character, ComboResetReason reason)
            {
               var args = new ComboResetEventArgs(character, reason);
                               
               ComboReset?.Invoke(args);
               Dispatcher.OnGameEventRaised("ComboResetGameEvent", args);
            }
            
             public void Raise(ComboResetEventArgs eventArgs)
             {                                   
                 ComboReset?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("ComboResetGameEvent", eventArgs);
             }

            public void AddListener(Action<ComboResetEventArgs> listener)
            {
                ComboReset += listener;
            }
            
             public void RemoveListener(Action<ComboResetEventArgs> listener)
             {
                ComboReset -= listener;
             }
        }
    }
}
#endif