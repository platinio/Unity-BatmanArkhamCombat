using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// It only starts actions and watches them end; the runner's owner keeps ticking the runner.
    /// </summary>
    public sealed class ReactionPlayback
    {
        private readonly ActionRunner runner;
        private readonly IReadOnlyList<ActionDefinition> actionsInOrder;
        private int currentActionIndex;

        public ReactionPlayback(ActionRunner runner, IReadOnlyList<ActionDefinition> actionsInOrder)
        {
            this.runner = runner;
            this.actionsInOrder = actionsInOrder;
        }

        public bool HasEnded { get; private set; }

        private ActionDefinition CurrentAction => actionsInOrder[currentActionIndex];

        public void Start(Vector3 position)
        {
            currentActionIndex = 0;
            HasEnded = false;
            PlayTheCurrentActionOrEnd(position);
        }

        public void PlayTheNextActionWhenTheCurrentOneEnds(Vector3 position)
        {
            if (HasEnded || runner.IsStillPlaying(CurrentAction))
            {
                return;
            }

            currentActionIndex++;
            PlayTheCurrentActionOrEnd(position);
        }

        private void PlayTheCurrentActionOrEnd(Vector3 position)
        {
            bool isThereAnActionLeft = currentActionIndex < actionsInOrder.Count;
            if (!isThereAnActionLeft)
            {
                HasEnded = true;
                return;
            }

            if (CurrentAction != null)
            {
                runner.PlayAction(CurrentAction, position);
            }
            else
            {
                // A missing action must still end what was playing: the reaction would otherwise sit on
                // top of an attack that goes on hitting, and the attacking state would take the character back.
                runner.Cancel();
            }
        }
    }
}
