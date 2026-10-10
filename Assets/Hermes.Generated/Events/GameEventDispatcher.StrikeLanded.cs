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
        public StrikeLandedEvent StrikeLandedGameEvent;

        public class StrikeLandedEvent
        {
            private event Action<StrikeLandedEventArgs> StrikeLanded;
            private GameEventDispatcher Dispatcher;

            public StrikeLandedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject attacker, AttackDefinition attack, GameObject target)
            {
               var args = new StrikeLandedEventArgs(attacker, attack, target);
                               
               StrikeLanded?.Invoke(args);
               Dispatcher.OnGameEventRaised("StrikeLandedGameEvent", args);
            }
            
             public void Raise(StrikeLandedEventArgs eventArgs)
             {                                   
                 StrikeLanded?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("StrikeLandedGameEvent", eventArgs);
             }

            public void AddListener(Action<StrikeLandedEventArgs> listener)
            {
                StrikeLanded += listener;
            }
            
             public void RemoveListener(Action<StrikeLandedEventArgs> listener)
             {
                StrikeLanded -= listener;
             }
        }
    }
}
#endif