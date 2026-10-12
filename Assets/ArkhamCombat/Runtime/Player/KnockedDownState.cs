using System;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class KnockedDownState : ReactionState
    {
        public KnockedDownState(CharacterContext context, CombatActions combatActions, ActionDefinition[] actionsInOrder)
            : base(context, combatActions, actionsInOrder)
        {
        }

        public override bool CanBeInterrupted => false;
    }

    [Serializable]
    public sealed class KnockedDownStateBinding : ICharacterStateBinding
    {
        [Tooltip("What the character plays when it is knocked off its feet. Its length is the time spent on the floor.")]
        [SerializeField] private ActionDefinition knockdownAction;

        [Tooltip("What the character plays to stand up again.")]
        [SerializeField] private ActionDefinition getUpAction;

        public void Install(DiContainer container)
        {
            ReactionState.ReportWhenMissing(knockdownAction, nameof(KnockedDownStateBinding), nameof(knockdownAction));
            ReactionState.ReportWhenMissing(getUpAction, nameof(KnockedDownStateBinding), nameof(getUpAction));

            container.Bind<ICharacterState>().To<KnockedDownState>().AsSingle().WithArguments(new[] { knockdownAction, getUpAction });
        }
    }
}
