#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class ComboResetGameEventTrigger : EventTriggerBase
    {
        public GameObject Character;
public ComboResetReason Reason;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.ComboResetGameEvent.Raise(Character, Reason);
        }
    }
}
#endif