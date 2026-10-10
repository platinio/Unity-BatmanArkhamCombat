#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class ActionEndedGameEventTrigger : EventTriggerBase
    {
        public GameObject Character;
public ActionDefinition Action;
public bool WasInterrupted;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.ActionEndedGameEvent.Raise(Character, Action, WasInterrupted);
        }
    }
}
#endif