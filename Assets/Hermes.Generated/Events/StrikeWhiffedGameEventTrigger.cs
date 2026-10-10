#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class StrikeWhiffedGameEventTrigger : EventTriggerBase
    {
        public GameObject Attacker;
public AttackDefinition Attack;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.StrikeWhiffedGameEvent.Raise(Attacker, Attack);
        }
    }
}
#endif