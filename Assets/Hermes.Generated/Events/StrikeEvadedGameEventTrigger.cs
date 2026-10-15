#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class StrikeEvadedGameEventTrigger : EventTriggerBase
    {
        public GameObject Attacker;
public AttackDefinition Attack;
public GameObject Target;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.StrikeEvadedGameEvent.Raise(Attacker, Attack, Target);
        }
    }
}
#endif