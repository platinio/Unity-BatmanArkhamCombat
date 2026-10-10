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
        public StrikeWhiffedEvent StrikeWhiffedGameEvent;

        public class StrikeWhiffedEvent
        {
            private event Action<StrikeWhiffedEventArgs> StrikeWhiffed;
            private GameEventDispatcher Dispatcher;

            public StrikeWhiffedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject attacker, AttackDefinition attack)
            {
               var args = new StrikeWhiffedEventArgs(attacker, attack);
                               
               StrikeWhiffed?.Invoke(args);
               Dispatcher.OnGameEventRaised("StrikeWhiffedGameEvent", args);
            }
            
             public void Raise(StrikeWhiffedEventArgs eventArgs)
             {                                   
                 StrikeWhiffed?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("StrikeWhiffedGameEvent", eventArgs);
             }

            public void AddListener(Action<StrikeWhiffedEventArgs> listener)
            {
                StrikeWhiffed += listener;
            }
            
             public void RemoveListener(Action<StrikeWhiffedEventArgs> listener)
             {
                StrikeWhiffed -= listener;
             }
        }
    }
}
#endif