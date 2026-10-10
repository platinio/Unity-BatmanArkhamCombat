#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class ActionStartedGameEventTrigger : EventTriggerBase
    {
        public GameObject Character;
public ActionDefinition Action;
public bool IsInterrupt;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.ActionStartedGameEvent.Raise(Character, Action, IsInterrupt);
        }
    }
}
#endif