using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    /// <summary>Builders and test doubles shared by the combat tests.</summary>
    public static class CombatTestDoubles
    {
        public static AttackDefinition Attack(
            string name,
            float duration = 1f,
            Window? hitWindow = null,
            Window? comboWindow = null,
            Window? evadeWindow = null,
            Window? warpWindow = null,
            float strikeDistance = 1f,
            float maxLunge = 4f,
            params PresentationCue[] cues)
        {
            AttackDefinition attack = ScriptableObject.CreateInstance<AttackDefinition>();
            attack.name = name;
            attack.Configure(duration, cues);
            attack.ConfigureAttack(
                hitWindow ?? new Window(0.3f, 0.5f),
                comboWindow ?? new Window(0.5f, 0.9f),
                evadeWindow ?? new Window(0f, 0.3f),
                warpWindow ?? new Window(0f, 0.3f),
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

        /// <summary>A node like the root, which plays nothing and only leads somewhere.</summary>
        public static ChainNode NodeWithNothingToPlay(string id, params Edge[] edges) => new ChainNode(id, (AttackDefinition)null, edges);

        /// <summary>The game's four kinds of press as throwaway assets. Call <see cref="Destroy"/> in TearDown.</summary>
        public sealed class TestIntentKinds
        {
            public readonly IntentKind Strike;
            public readonly IntentKind Counter;
            public readonly IntentKind Evade;
            public readonly IntentKind Stun;

            public TestIntentKinds(float secondsQueued = 0.25f)
            {
                Strike = Kind("Strike", secondsQueued);
                Counter = Kind("Counter", secondsQueued);
                Evade = Kind("Evade", secondsQueued);
                Stun = Kind("Stun", secondsQueued);
            }

            public InterruptKinds InterruptKinds => new InterruptKinds(Evade, Counter);

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(Strike);
                UnityEngine.Object.DestroyImmediate(Counter);
                UnityEngine.Object.DestroyImmediate(Evade);
                UnityEngine.Object.DestroyImmediate(Stun);
            }

            private static IntentKind Kind(string name, float secondsQueued)
            {
                IntentKind kind = ScriptableObject.CreateInstance<IntentKind>();
                kind.name = name;
                kind.Configure(secondsQueued);
                return kind;
            }
        }

        /// <summary>Passes every edge except those whose destination was denied, and records each edge it is asked about.</summary>
        public sealed class RecordingConditions : IConditionEvaluator
        {
            public readonly List<Edge> AskedEdges = new List<Edge>();
            private readonly HashSet<string> deniedDestinations = new HashSet<string>();

            public RecordingConditions Deny(string destinationId)
            {
                deniedDestinations.Add(destinationId);
                return this;
            }

            public bool IsConditionMet(Edge edge, CombatContext context, Intent intent)
            {
                AskedEdges.Add(edge);
                return !deniedDestinations.Contains(edge.DestinationId);
            }
        }

        /// <summary>Writes each event as a short line, so a test can assert on what happened in order.</summary>
        public sealed class RecordingEvents : ICombatEvents
        {
            public readonly List<string> Log = new List<string>();

            public void ComboChanged(int count, int tier) => Log.Add($"combo {count} tier {tier}");

            public void ComboReset(ComboResetReason reason) => Log.Add($"reset {reason}");

            public void ActionStarted(ActionDefinition action, bool isInterrupt) =>
                Log.Add(isInterrupt ? $"interrupt with {action.name}" : $"start {action.name}");

            public void ActionEnded(ActionDefinition action, bool wasInterrupted) =>
                Log.Add(wasInterrupted ? $"interrupted {action.name}" : $"end {action.name}");
        }

        public sealed class RecordingHitWindowListener : IHitWindowListener
        {
            public int OpenedCount;
            public int ClosedCount;
            public bool IsOpen;
            public AttackDefinition LastOpenedFor;

            public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
            {
                OpenedCount++;
                IsOpen = true;
                LastOpenedFor = attack;
            }

            public void HitWindowClosed()
            {
                ClosedCount++;
                IsOpen = false;
            }
        }

        public sealed class RecordingWarpMover : IWarpMover
        {
            public Vector3 TotalMovement;
            public int MoveCount;

            public void MoveBy(Vector3 planarOffset)
            {
                TotalMovement += planarOffset;
                MoveCount++;
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
            public readonly List<float> PlayedAt = new List<float>();
            private readonly Func<float> readClockTime;

            public RecordingCue(Func<float> readClockTime) => this.readClockTime = readClockTime;

            public void Play(ICueTarget target, PresentationCue cue) => PlayedAt.Add(readClockTime());
        }
    }
}
