using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// Leaving early, as a guard abort does, cancels the action so nothing of it stays open behind
    /// the tree.
    /// </summary>
    [GraphCreateMenu("ArkhamCombat/Play Action")]
    public sealed class PlayActionNode : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Action { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Direction { get; private set; }

        public override string NodeName => "Play Action";

        public override string Description =>
            "Plays the action on the agent's runner and waits for it to end.\n" +
            "An action that travels, such as a dodge, goes along Direction when one is fed.";

        private sealed class Memory
        {
            public ActionRunner Runner;
            public ActionDefinition ActionToPlay;
            public bool HasReportedWhyItCannotPlay;
        }

        protected override void Definition()
        {
            base.Definition();

            Action = ValueInput<ActionDefinition>(nameof(Action));
            Direction = ValueInput<Vector3>(nameof(Direction), Vector3.zero);
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.ActionToPlay = ctx.GetValue<ActionDefinition>(Action);
            memory.Runner = RunnerOf(ctx);

            if (CanPlay(memory))
            {
                Play(memory, ctx.transform.position, ctx.GetValue<Vector3>(Direction));
            }
            else
            {
                ReportWhyItCannotPlayOnce(ctx, memory);
            }
        }

        private static void Play(Memory memory, Vector3 position, Vector3 directionToTravelIn)
        {
            bool wasFedADirection = directionToTravelIn != Vector3.zero;
            if (wasFedADirection)
            {
                memory.Runner.PlayAction(memory.ActionToPlay, position, directionToTravelIn);
            }
            else
            {
                memory.Runner.PlayAction(memory.ActionToPlay, position);
            }
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (!CanPlay(memory))
            {
                return ExecutionStatus.Failure;
            }

            return IsStillPlaying(memory) ? ExecutionStatus.Running : ExecutionStatus.Success;
        }

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (CanPlay(memory) && IsStillPlaying(memory))
            {
                memory.Runner.Cancel();
            }
        }

        private static bool CanPlay(Memory memory) => memory.Runner != null && memory.ActionToPlay != null;

        // The runner may by now be playing an action something else started. That one is not this node's to wait for or to cancel.
        private static bool IsStillPlaying(Memory memory) => memory.Runner.IsStillPlaying(memory.ActionToPlay);

        private static ActionRunner RunnerOf(BTContext ctx)
        {
            CombatActions combatActions = ctx.GetComponent<CombatActions>();
            return combatActions != null ? combatActions.Runner : null;
        }

        // A branch that fails is entered again on the next tick, so the reason is logged once per agent.
        private void ReportWhyItCannotPlayOnce(BTContext ctx, Memory memory)
        {
            if (memory.HasReportedWhyItCannotPlay)
            {
                return;
            }

            memory.HasReportedWhyItCannotPlay = true;

            string reason = memory.Runner == null
                ? $"has no {nameof(CombatActions)}, so it cannot play an action"
                : "was handed no action to play";
            Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' {reason}.", ctx.gameObject);
        }
    }
}
