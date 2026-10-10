#if HERMES_EVENTS_GENERATED
using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    public class ComboChangedGameEventTrigger : EventTriggerBase
    {
        public GameObject Character;
public int Count;
public int Tier;

    
        public override void Trigger()
        {
            var gameEventDispatcher = GetComponent<GameEventDispatcher>();
            if (gameEventDispatcher == null) return;
            
            gameEventDispatcher.ComboChangedGameEvent.Raise(Character, Count, Tier);
        }
    }
}
#endif