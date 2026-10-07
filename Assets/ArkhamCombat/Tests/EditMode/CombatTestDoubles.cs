using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    /// <summary>Builders and fakes shared by the combat tests.</summary>
    public static class CombatTestDoubles
    {
        public static AttackDefinition Attack(
            string name,
            float duration = 1f,
            Window? active = null,
            Window? cancelAttack = null,
            Window? cancelEvade = null,
            Window? warp = null,
            float strikeDistance = 1f,
            float maxLunge = 4f,
            params PresentationCue[] cues)
        {
            AttackDefinition attack = ScriptableObject.CreateInstance<AttackDefinition>();
            attack.name = name;
            attack.Configure(duration, cues);
            attack.ConfigureAttack(
                active ?? new Window(0.3f, 0.5f),
                cancelAttack ?? new Window(0.5f, 0.9f),
                cancelEvade ?? new Window(0f, 0.3f),
                warp ?? new Window(0f, 0.3f),
                strikeDistance,
                maxLunge);
            return attack;
        }

        public static ActionDefinition Action(string name, float duration = 1f, params PresentationCue[] cues)
        {
            ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
            action.name = name;
            action.Configure(duration, cues);
            return action;
        }

        public static Stance Stance(string root, IEnumerable<ChainNode> nodes, IEnumerable<Edge> globalEdges = null, float chainResetSeconds = 0.6f)
        {
            Stance stance = ScriptableObject.CreateInstance<Stance>();
            stance.name = "TestStance";
            stance.Configure(root, nodes, globalEdges, chainResetSeconds);
            return stance;
        }

        public static IntentBuffer Buffer(float lifetime = 0.25f) => new IntentBuffer(IntentLifetimes.Uniform(lifetime));

        /// <summary>Answers each edge by destination; edges it has no answer for pass.</summary>
        public sealed class FakeConditions : IConditionEvaluator
        {
            public readonly Dictionary<string, bool> ByDestination = new Dictionary<string, bool>();
            public readonly List<Edge> Asked = new List<Edge>();

            public FakeConditions Deny(string destination)
            {
                ByDestination[destination] = false;
                return this;
            }

            public bool Evaluate(Edge edge, CombatContext context, Intent intent)
            {
                Asked.Add(edge);
                return !ByDestination.TryGetValue(edge.Destination, out bool allowed) || allowed;
            }
        }

        public sealed class RecordingEvents : ICombatEvents
        {
            public readonly List<string> Log = new List<string>();

            public void ComboChanged(int count, int tier) => Log.Add($"changed {count} {tier}");

            public void ComboReset(ComboResetReason reason) => Log.Add($"reset {reason}");

            public void ActionStarted(ActionDefinition action, bool interrupt) => Log.Add($"{(interrupt ? "interrupt" : "start")} {action.name}");

            public void ActionEnded(ActionDefinition action, bool interrupted) => Log.Add($"{(interrupted ? "cut" : "end")} {action.name}");
        }

        public sealed class RecordingHits : IHitWindowSink
        {
            public int Armed;
            public int Disarmed;
            public bool IsArmed;
            public AttackDefinition LastAttack;

            public void Arm(AttackDefinition attack, IActionTarget target)
            {
                Armed++;
                IsArmed = true;
                LastAttack = attack;
            }

            public void Disarm()
            {
                Disarmed++;
                IsArmed = false;
            }
        }

        public sealed class RecordingSink : IDisplacementSink
        {
            public Vector3 Total;
            public int Calls;

            public void Displace(Vector3 planarDelta)
            {
                Total += planarDelta;
                Calls++;
            }
        }

        public sealed class PointTarget : IActionTarget
        {
            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; }

            public PointTarget(Vector3 position) => Position = position;
        }

        public sealed class RecordingCue : ICueKind
        {
            public readonly List<float> FiredAt = new List<float>();
            private readonly Func<float> clock;

            public RecordingCue(Func<float> clock) => this.clock = clock;

            public void Play(ICueTarget target, PresentationCue cue) => FiredAt.Add(clock());
        }
    }
}
