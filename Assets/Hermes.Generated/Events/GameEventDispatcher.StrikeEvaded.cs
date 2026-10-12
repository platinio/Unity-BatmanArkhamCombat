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
        public StrikeEvadedEvent StrikeEvadedGameEvent;

        public class StrikeEvadedEvent
        {
            private event Action<StrikeEvadedEventArgs> StrikeEvaded;
            private GameEventDispatcher Dispatcher;

            public StrikeEvadedEvent(GameEventDispatcher dispatcher)
            {
                Dispatcher = dispatcher;
            }

            public void Raise(GameObject attacker, AttackDefinition attack, GameObject target)
            {
               var args = new StrikeEvadedEventArgs(attacker, attack, target);
                               
               StrikeEvaded?.Invoke(args);
               Dispatcher.OnGameEventRaised("StrikeEvadedGameEvent", args);
            }
            
             public void Raise(StrikeEvadedEventArgs eventArgs)
             {                                   
                 StrikeEvaded?.Invoke(eventArgs);
                 Dispatcher.OnGameEventRaised("StrikeEvadedGameEvent", eventArgs);
             }

            public void AddListener(Action<StrikeEvadedEventArgs> listener)
            {
                StrikeEvaded += listener;
            }
            
             public void RemoveListener(Action<StrikeEvadedEventArgs> listener)
             {
                StrikeEvaded -= listener;
             }
        }
    }
}
#endif