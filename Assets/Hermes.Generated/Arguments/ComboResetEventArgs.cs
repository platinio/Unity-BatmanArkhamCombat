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
        public class ComboResetEventArgs : GameEventArgsBase
        {        
            public GameObject Character;
public ComboResetReason Reason;

    
            public ComboResetEventArgs(GameObject character, ComboResetReason reason)
            {
                this.Character = character;
this.Reason = reason;

            }
        }
}