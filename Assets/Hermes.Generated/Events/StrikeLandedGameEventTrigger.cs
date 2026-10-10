#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class StrikeLandedGameEventTrigger : EventTriggerBase
    {
        public GameObject Attacker;
public AttackDefinition Attack;
public GameObject Target;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.StrikeLandedGameEvent.Raise(Attacker, Attack, Target);
        }
    }
}
#endif