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
        private int currentIndex;

        public ReactionPlayback(ActionRunner runner, IReadOnlyList<ActionDefinition> actionsInOrder)
        {
            this.runner = runner;
            this.actionsInOrder = actionsInOrder;
        }

        public bool HasEnded { get; private set; }

        private ActionDefinition CurrentAction => actionsInOrder[currentIndex];

        public void Start(Vector3 position)
        {
            currentIndex = 0;
            HasEnded = false;
            PlayTheCurrentActionOrEnd(position);
        }

        public void PlayTheNextActionWhenTheCurrentOneEnds(Vector3 position)
        {
            if (HasEnded || IsTheCurrentActionStillPlaying())
            {
                return;
            }

            currentIndex++;
            PlayTheCurrentActionOrEnd(position);
        }

        private void PlayTheCurrentActionOrEnd(Vector3 position)
        {
            bool isThereAnActionLeft = currentIndex < actionsInOrder.Count;
            if (isThereAnActionLeft)
            {
                runner.PlayAction(CurrentAction, position);
            }
            else
            {
                HasEnded = true;
            }
        }

        // The runner may by now be playing an action something else started, and that one is not this reaction's.
        private bool IsTheCurrentActionStillPlaying() =>
            runner.IsPlaying && runner.CurrentAction == CurrentAction;
    }
}
