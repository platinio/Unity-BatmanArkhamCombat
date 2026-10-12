using System;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class StaggerState : ReactionState
    {
        public StaggerState(CharacterContext context, CombatActions combatActions, ActionDefinition[] actionsInOrder)
            : base(context, combatActions, actionsInOrder)
        {
        }
    }

    [Serializable]
    public sealed class StaggerStateBinding : ICharacterStateBinding
    {
        [Tooltip("What the character plays when a heavy hit lands.")]
        [SerializeField] private ActionDefinition staggerAction;

        public void Install(DiContainer container)
        {
            ReactionState.ReportWhenMissing(staggerAction, nameof(StaggerStateBinding), nameof(staggerAction));

            container.Bind<ICharacterState>().To<StaggerState>().AsSingle().WithArguments(new[] { staggerAction });
        }
    }
}
