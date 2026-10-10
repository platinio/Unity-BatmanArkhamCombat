//{0} Game event name example: OnGameEntityDamage
//{1} event args signature example: GameEntity Target, float Damage
//{2} event args declaration example: public readonly GameEntity Target;
//{3} event args assignment  example: Target = target;
//{4} namespaces example: using System;

using System;
using UnityEngine;
using ArkhamCombat.Combat;


namespace ArcaneOnyx.GameEventGenerator
{
    [System.Serializable]
        public class ActionStartedEventArgs : GameEventArgsBase
        {        
            public GameObject Character;
public ActionDefinition Action;
public bool IsInterrupt;

    
            public ActionStartedEventArgs(GameObject character, ActionDefinition action, bool isInterrupt)
            {
                this.Character = character;
this.Action = action;
this.IsInterrupt = isInterrupt;

            }
        }
}