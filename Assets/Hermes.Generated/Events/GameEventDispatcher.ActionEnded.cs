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
        public ActionEndedEvent ActionEndedGameEvent;

        public class ActionEndedEvent
        {
            private event Action<ActionEndedEventArgs> ActionEnded;
            private GameEventDispatcher Dispatcher;

            public ActionEndedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject character, ActionDefinition action, bool wasInterrupted)
            {
               var args = new ActionEndedEventArgs(character, action, wasInterrupted);
                               
               ActionEnded?.Invoke(args);
               Dispatcher.OnGameEventRaised("ActionEndedGameEvent", args);
            }
            
             public void Raise(ActionEndedEventArgs eventArgs)
             {                                   
                 ActionEnded?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("ActionEndedGameEvent", eventArgs);
             }

            public void AddListener(Action<ActionEndedEventArgs> listener)
            {
                ActionEnded += listener;
            }
            
             public void RemoveListener(Action<ActionEndedEventArgs> listener)
             {
                ActionEnded -= listener;
             }
        }
    }
}
#endif