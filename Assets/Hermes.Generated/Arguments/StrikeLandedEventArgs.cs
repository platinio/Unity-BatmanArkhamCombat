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
        public class StrikeLandedEventArgs : GameEventArgsBase
        {        
            public GameObject Attacker;
public AttackDefinition Attack;
public GameObject Target;

    
            public StrikeLandedEventArgs(GameObject attacker, AttackDefinition attack, GameObject target)
            {
                this.Attacker = attacker;
this.Attack = attack;
this.Target = target;

            }
        }
}