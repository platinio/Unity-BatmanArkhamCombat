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
        public ActionStartedEvent ActionStartedGameEvent;

        public class ActionStartedEvent
        {
            private event Action<ActionStartedEventArgs> ActionStarted;
            private GameEventDispatcher Dispatcher;

            public ActionStartedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject character, ActionDefinition action, bool isInterrupt)
            {
               var args = new ActionStartedEventArgs(character, action, isInterrupt);
                               
               ActionStarted?.Invoke(args);
               Dispatcher.OnGameEventRaised("ActionStartedGameEvent", args);
            }
            
             public void Raise(ActionStartedEventArgs eventArgs)
             {                                   
                 ActionStarted?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("ActionStartedGameEvent", eventArgs);
             }

            public void AddListener(Action<ActionStartedEventArgs> listener)
            {
                ActionStarted += listener;
            }
            
             public void RemoveListener(Action<ActionStartedEventArgs> listener)
             {
                ActionStarted -= listener;
             }
        }
    }
}
#endif