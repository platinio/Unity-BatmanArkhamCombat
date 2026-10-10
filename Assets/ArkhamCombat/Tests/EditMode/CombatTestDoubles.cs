using System;
using System.Collections.Generic;
#if HERMES_EVENTS_GENERATED
using ArcaneOnyx.GameEventGenerator;
#endif
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public static class CombatTestDoubles
    {
        // Hidden and never saved, so the open scene is left untouched.
        public static GameObject HiddenObject(string name) => new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };

        public static AttackDefinition Attack(
            string name,
            float duration = 1f,
            Window? hitWindow = null,
            Window? comboWindow = null,
            Window? evadeWindow = null,
            Window? warpWindow = null,
            float strikeDistance = 1f,
            float maxLunge = 4f,
            float damage = 10f,
            HitReaction reaction = HitReaction.Flinch,
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
                maxLunge,
                damage,
                reaction);
            return attack;
        }

        public static HitReceiverProfile ReceiverProfile(float maxHealth = 100f, bool isArmored = false, bool canBeKnockedDown = true)
        {
            HitReceiverProfile profile = ScriptableObject.CreateInstance<HitReceiverProfile>();
            profile.name = "TestReceiverProfile";
            profile.Configure(maxHealth, isArmored, canBeKnockedDown);
            return profile;
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

            public bool IsConditionMet(Edge edge, CombatFacts facts, Intent intent)
            {
                AskedEdges.Add(edge);
                return !deniedDestinations.Contains(edge.DestinationId);
            }
        }

        /// <summary>
        /// Writes each action a runner starts or ends as a short line, so a test can assert on what
        /// happened in order.
        /// </summary>
        public sealed class RecordingActionListener
        {
            public readonly List<string> Log = new List<string>();

            public void ListenTo(ActionRunner runner)
            {
                runner.ActionStarted += RecordActionStarted;
                runner.ActionEnded += RecordActionEnded;
            }

            private void RecordActionStarted(ActionDefinition action, bool isInterrupt) =>
                Log.Add(isInterrupt ? $"interrupt with {action.name}" : $"start {action.name}");

            private void RecordActionEnded(ActionDefinition action, bool wasInterrupted) =>
                Log.Add(wasInterrupted ? $"interrupted {action.name}" : $"end {action.name}");
        }

        public sealed class RecordingHitWindowListener : IHitWindowListener
        {
            public int OpenedCount;
            public int ClosedCount;
            public bool IsOpen;
            public AttackDefinition LastOpenedFor;
            public IActionTarget LastOpenedAgainst;

            public void HitWindowOpened(AttackDefinition attack, IActionTarget target)
            {
                OpenedCount++;
                IsOpen = true;
                LastOpenedFor = attack;
                LastOpenedAgainst = target;
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

        /// <summary>Gives whichever target it was last told to, and records each direction it is asked with.</summary>
        public sealed class RecordingActionTargetPicker : IActionTargetPicker
        {
            public readonly List<Vector2> AskedDirections = new List<Vector2>();
            public IActionTarget TargetToGive;

            public IActionTarget PickTarget(Vector2 direction)
            {
                AskedDirections.Add(direction);
                return TargetToGive;
            }
        }

        public sealed class SettableActionStartGate : IActionStartGate
        {
            public bool CanStartFromIdle { get; set; }
        }

        /// <summary>A character input that holds presses and never moves the stick.</summary>
        public sealed class StillCharacterInput : ICharacterInput
        {
            public Vector2 Move => Vector2.zero;
            public Vector2 LookDelta => Vector2.zero;
            public bool SprintHeld => false;
            public IntentBuffer Intents { get; } = new IntentBuffer();

            public void ExpireQueuedIntents() => Intents.ExpireAll();
        }

#if HERMES_EVENTS_GENERATED
        /// <summary>The scene's Hermes object, reduced to the dispatcher a test built. Null for a scene without one.</summary>
        public sealed class SceneGameEventsWith : ISceneGameEvents
        {
            public SceneGameEventsWith(GameEventDispatcher dispatcher) => GameEventDispatcher = dispatcher;

            public GameEventDispatcher GameEventDispatcher { get; }
        }
#endif

        public sealed class FixedComboMeterSettings : IComboMeterSettings
        {
            public FixedComboMeterSettings(float meterTimeoutSeconds, params int[] tierThresholds)
            {
                MeterTimeoutSeconds = meterTimeoutSeconds;
                TierThresholds = tierThresholds;
            }

            public IReadOnlyList<int> TierThresholds { get; }
            public float MeterTimeoutSeconds { get; }
        }

        public sealed class HitCheckSettings : IHitCheckSettings
        {
            public float HitRangeMargin { get; set; } = 0.5f;
            public float HitAngle { get; set; } = 60f;
        }

        public sealed class PointTarget : IActionTarget
        {
            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; }

            public PointTarget(Vector3 position) => Position = position;
        }

        public sealed class TargetingSettings : ITargetingSettings
        {
            public float MaxTargetDistance { get; set; } = 8f;
            public float MaxTargetAngle { get; set; } = 110f;
            public float AngleCountingAsDoubleDistance { get; set; } = 90f;
            public float StickPushedMagnitude { get; set; } = 0.1f;
        }

        public sealed class PointCombatTarget : ICombatTarget
        {
            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; }
            public string State => "Idle";
            public HitResult ResultToGive = HitResult.Landed(AppliedReaction.Flinch);
            public HitInfo LastReceived;
            public int HitsTaken;

            public PointCombatTarget(Vector3 position) => Position = position;

            public HitResult Receive(HitInfo hit)
            {
                LastReceived = hit;
                HitsTaken++;
                return ResultToGive;
            }
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
