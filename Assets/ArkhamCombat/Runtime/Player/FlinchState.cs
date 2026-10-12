using System;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class FlinchState : ReactionState
    {
        public FlinchState(CharacterContext context, CombatActions combatActions, ActionDefinition[] actionsInOrder)
            : base(context, combatActions, actionsInOrder)
        {
        }
    }

    [Serializable]
    public sealed class FlinchStateBinding : ICharacterStateBinding
    {
        [Tooltip("What the character plays when a light hit lands.")]
        [SerializeField] private ActionDefinition flinchAction;

        public void Install(DiContainer container)
        {
            ReactionState.ReportWhenMissing(flinchAction, nameof(FlinchStateBinding), nameof(flinchAction));

            container.Bind<ICharacterState>().To<FlinchState>().AsSingle().WithArguments(new[] { flinchAction });
        }
    }
}
