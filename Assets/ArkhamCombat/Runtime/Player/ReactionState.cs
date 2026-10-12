using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Movement input never moves it. Each reaction is its own state type, so which reactions a
    /// character has is the state list on its installer, and a hit that asks for the reaction the
    /// character is already in changes nothing.
    /// </summary>
    public abstract class ReactionState : ICharacterState
    {
        private readonly CharacterContext context;
        private readonly CombatActions combatActions;
        private readonly IReadOnlyList<ActionDefinition> actionsInOrder;
        private ReactionPlayback playback;

        protected ReactionState(CharacterContext context, CombatActions combatActions, IReadOnlyList<ActionDefinition> actionsInOrder)
        {
            this.context = context;
            this.combatActions = combatActions;
            this.actionsInOrder = actionsInOrder;

            ReportMissingActions();
        }

        protected virtual bool ReturnsToLocomotionWhenTheReactionEnds => true;

        public void Enter()
        {
            playback = new ReactionPlayback(combatActions.Runner, actionsInOrder);
            playback.Start(context.Transform.position);
        }

        public void Tick(float deltaTime)
        {
            playback.PlayTheNextActionWhenTheCurrentOneEnds(context.Transform.position);

            if (playback.HasEnded && ReturnsToLocomotionWhenTheReactionEnds)
            {
                context.StateMachine.Change<LocomotionState>();
                return;
            }

            context.Motor.Tick(MotionIntent.Still(context.Transform.rotation), deltaTime);
        }

        public void Exit() { }

        private void ReportMissingActions()
        {
            for (int i = 0; i < actionsInOrder.Count; i++)
            {
                if (actionsInOrder[i] == null)
                {
                    Debug.LogError(
                        $"[{GetType().Name}] Action {i} of this reaction is not assigned on the state's binding, so that part of the reaction will not play.");
                }
            }
        }
    }
}
