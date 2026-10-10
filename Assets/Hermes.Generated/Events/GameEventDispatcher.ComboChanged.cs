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
        public ComboChangedEvent ComboChangedGameEvent;

        public class ComboChangedEvent
        {
            private event Action<ComboChangedEventArgs> ComboChanged;
            private GameEventDispatcher Dispatcher;

            public ComboChangedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject character, int count, int tier)
            {
               var args = new ComboChangedEventArgs(character, count, tier);
                               
               ComboChanged?.Invoke(args);
               Dispatcher.OnGameEventRaised("ComboChangedGameEvent", args);
            }
            
             public void Raise(ComboChangedEventArgs eventArgs)
             {                                   
                 ComboChanged?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("ComboChangedGameEvent", eventArgs);
             }

            public void AddListener(Action<ComboChangedEventArgs> listener)
            {
                ComboChanged += listener;
            }
            
             public void RemoveListener(Action<ComboChangedEventArgs> listener)
             {
                ComboChanged -= listener;
             }
        }
    }
}
#endif