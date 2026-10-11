using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// Running until the action ends, however it ends. Leaving early, as a guard abort does, cancels
    /// the action so nothing of it stays open behind the tree.
    /// </summary>
    [GraphCreateMenu("ArkhamCombat/Play Action")]
    public sealed class PlayActionNode : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Action { get; private set; }

        public override string NodeName => "Play Action";
        public override string Description => "Plays the action on the agent's runner and waits for it to end.";

        private sealed class Memory
        {
            public ActionRunner Runner;
            public ActionDefinition Action;
        }

        protected override void Definition()
        {
            base.Definition();

            Action = ValueInput<ActionDefinition>(nameof(Action));
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Action = ctx.GetValue<ActionDefinition>(Action);
            memory.Runner = RunnerOf(ctx);

            if (memory.Action == null)
            {
                Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' was handed no action to play.", ctx.gameObject);
                return;
            }

            memory.Runner?.PlayAction(memory.Action, ctx.transform.position);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (memory.Runner == null || memory.Action == null)
            {
                return ExecutionStatus.Failure;
            }

            return IsStillPlaying(memory) ? ExecutionStatus.Running : ExecutionStatus.Success;
        }

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (memory.Runner != null && IsStillPlaying(memory))
            {
                memory.Runner.Cancel();
            }
        }

        private static bool IsStillPlaying(Memory memory) =>
            memory.Runner.IsPlaying && memory.Runner.CurrentAction == memory.Action;

        private ActionRunner RunnerOf(BTContext ctx)
        {
            CombatActions combatActions = ctx.GetComponent<CombatActions>();
            if (combatActions == null)
            {
                Debug.LogError($"[{NodeName}] '{ctx.gameObject.name}' has no {nameof(CombatActions)}, so it cannot play an action.", ctx.gameObject);
                return null;
            }

            return combatActions.Runner;
        }
    }
}
