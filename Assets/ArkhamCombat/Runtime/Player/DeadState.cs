using System;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class DeadState : ReactionState
    {
        public DeadState(CharacterContext context, CombatActions combatActions, ActionDefinition[] actionsInOrder)
            : base(context, combatActions, actionsInOrder)
        {
        }

        public override bool CanBeInterrupted => false;

        protected override bool ShouldReturnToLocomotionWhenTheReactionEnds => false;
    }

    [Serializable]
    public sealed class DeadStateBinding : ICharacterStateBinding
    {
        [Tooltip("What the character plays as it dies. It then stays where it fell.")]
        [SerializeField] private ActionDefinition deathAction;

        public void Install(DiContainer container)
        {
            ReactionState.ReportWhenMissing(deathAction, nameof(DeadStateBinding), nameof(deathAction));

            container.Bind<ICharacterState>().To<DeadState>().AsSingle().WithArguments(new[] { deathAction });
        }
    }
}
