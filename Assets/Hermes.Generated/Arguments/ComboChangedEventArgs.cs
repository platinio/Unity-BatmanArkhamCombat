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
        public class ComboChangedEventArgs : GameEventArgsBase
        {        
            public GameObject Character;
public int Count;
public int Tier;

    
            public ComboChangedEventArgs(GameObject character, int count, int tier)
            {
                this.Character = character;
this.Count = count;
this.Tier = tier;

            }
        }
}