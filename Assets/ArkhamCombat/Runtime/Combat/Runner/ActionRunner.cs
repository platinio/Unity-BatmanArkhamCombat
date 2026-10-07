using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Owns the current action and its normalized time, read from the presentation driver. Each tick
    /// it fires every window crossing once, arms and disarms the hit sink, feeds the warp to the
    /// displacement sink, and asks the resolver whether a queued press takes the chain somewhere:
    /// global interrupts at any time, follow-ups while the cancel window is open or nothing plays.
    /// After an action ends the chain position is kept for the stance's reset time of idle seconds,
    /// then collapses to the root. An agent without a stance drives it through <see cref="PlayAction"/>.
    /// </summary>
    public sealed class ActionRunner
    {
        private readonly Stance stance;
        private readonly IntentBuffer intents;
        private readonly CombatContext context;
        private readonly ComboResolver resolver;
        private readonly IPresentationDriver driver;
        private readonly IDisplacementSink displacement;
        private readonly IHitWindowSink hits;
        private readonly ICombatEvents events;
        private readonly System.Random random;
        private readonly Dictionary<ChainNode, AttackDefinition> lastPicked = new Dictionary<ChainNode, AttackDefinition>();

        private bool playing;
        private bool armed;
        private bool cancelOpen;
        private bool warpOpen;
        private bool warpActive;
        private float idleSeconds;

        public ActionRunner(
            Stance stance,
            IntentBuffer intents,
            CombatContext context,
            IConditionEvaluator conditions,
            IPresentationDriver driver,
            IDisplacementSink displacement,
            IHitWindowSink hits,
            ICombatEvents events,
            System.Random random = null)
        {
            this.stance = stance;
            this.intents = intents;
            this.context = context ?? new CombatContext();
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.displacement = displacement ?? new NullDisplacementSink();
            this.hits = hits ?? new NullHitWindowSink();
            this.events = events ?? new NullCombatEvents();
            this.random = random ?? new System.Random();

            resolver = new ComboResolver(conditions);
            Trace = new ActionTrace(intents);

            if (stance != null)
            {
                stance.Prepare();
                CurrentNode = stance.Root;
            }
        }

        public ActionTrace Trace { get; }

        /// <summary>Set by target selection before each tick. May be null or invalid; the warp then does nothing.</summary>
        public IActionTarget Target { get; set; }

        public ChainNode CurrentNode { get; private set; }

        /// <summary>Null while idle.</summary>
        public ActionDefinition CurrentAction { get; private set; }

        /// <summary>Null while idle or while a plain action plays.</summary>
        public AttackDefinition CurrentAttack => CurrentAction as AttackDefinition;

        public bool IsPlaying => playing;
        public float NormalizedTime => playing ? driver.NormalizedTime : 0f;
        public bool HitArmed => armed;
        public bool CancelWindowOpen => cancelOpen;
        public bool WarpOpen => warpOpen;

        /// <summary>True when the current attack's warp was skipped because the target was beyond the lunge limit.</summary>
        public bool WarpRefused { get; private set; }

        /// <summary>Seconds spent idle since the last action ended or the last press was spent. Drives the chain reset.</summary>
        public float IdleSeconds => idleSeconds;

        /// <summary>
        /// One frame. <paramref name="position"/> is the character's current position, for the warp;
        /// <paramref name="canStartChain"/> says whether an idle character may begin an action now
        /// (grounded, not in a reaction).
        /// </summary>
        public void Tick(float deltaTime, Vector3 position, bool canStartChain)
        {
            deltaTime = Mathf.Max(0f, deltaTime);

            if (playing)
            {
                TickPlaying(deltaTime, position);
            }
            else
            {
                TickIdle(deltaTime, position, canStartChain);
            }
        }

        /// <summary>Plays a node now, replacing whatever runs. The code-driven interrupt path.</summary>
        public void Interrupt(ChainNode node, Vector3 position)
        {
            if (node == null)
            {
                return;
            }

            Start(node, Pick(node), position, interrupt: true);
        }

        /// <summary>Plays an action outside the chain: reactions, and every enemy action.</summary>
        public void PlayAction(ActionDefinition action, Vector3 position) => Start(null, action, position, interrupt: playing);

        /// <summary>Drops the current action, disarming if needed. For an external interrupt with nothing to play yet.</summary>
        public void Cancel()
        {
            if (!playing)
            {
                return;
            }

            Finish(interrupted: true, stopDriver: true);
            Trace.BeginIdle();
        }

        private void TickPlaying(float deltaTime, Vector3 position)
        {
            float previous = driver.NormalizedTime;
            driver.Tick(deltaTime);
            float t = driver.NormalizedTime;
            Trace.CurrentTime = t;

            AttackDefinition attack = CurrentAttack;
            if (attack != null)
            {
                ProcessCrossings(attack, previous, t, position);
            }

            if (stance != null && CurrentNode != null)
            {
                Resolution resolution = resolver.Resolve(
                    stance, new ResolveInput(CurrentNode, attack, t, cancelOpen), intents, context);

                if (resolution.Matched)
                {
                    idleSeconds = 0f;
                    Start(resolution.Destination, Pick(resolution.Destination), position, resolution.IsInterrupt);
                    return;
                }
            }

            if (t >= 1f)
            {
                Finish(interrupted: false, stopDriver: true);
                Trace.BeginIdle();
            }
        }

        private void TickIdle(float deltaTime, Vector3 position, bool canStartChain)
        {
            idleSeconds += deltaTime;
            Trace.CurrentTime += deltaTime;

            if (stance == null)
            {
                return;
            }

            ChainNode root = stance.Root;
            if (CurrentNode != root && idleSeconds >= stance.ChainResetSeconds)
            {
                CurrentNode = root;
            }

            if (!canStartChain || CurrentNode == null)
            {
                return;
            }

            Resolution resolution = resolver.Resolve(stance, ResolveInput.Idle(CurrentNode), intents, context);
            if (resolution.Matched)
            {
                idleSeconds = 0f;
                Start(resolution.Destination, Pick(resolution.Destination), position, resolution.IsInterrupt);
            }
        }

        private AttackDefinition Pick(ChainNode node)
        {
            lastPicked.TryGetValue(node, out AttackDefinition last);
            AttackDefinition pick = node.Pick(new VariantPickContext(last, context.TargetSide, random));
            if (pick != null)
            {
                lastPicked[node] = pick;
            }

            return pick;
        }

        /// <summary>
        /// Begins an action at this node. A running action is finished first with its close events,
        /// so a hitbox never survives the move that armed it; it counts as interrupted only when a
        /// global edge replaced it, a follow-up is a cancel the move allowed. Windows that start at zero fire here,
        /// so the warp or the hit frames of a fast move are not a frame late.
        /// </summary>
        private void Start(ChainNode node, ActionDefinition action, Vector3 position, bool interrupt)
        {
            if (action == null)
            {
                return;
            }

            if (playing)
            {
                Finish(interrupted: interrupt, stopDriver: false);
            }

            if (node != null)
            {
                CurrentNode = node;
            }

            CurrentAction = action;
            playing = true;
            armed = false;
            cancelOpen = false;
            warpOpen = false;
            warpActive = false;
            WarpRefused = false;

            driver.Play(action);
            Trace.BeginAction(node, action);
            events.ActionStarted(action, interrupt);

            if (action is AttackDefinition attack)
            {
                ProcessCrossings(attack, -1f, 0f, position);
            }
        }

        private void Finish(bool interrupted, bool stopDriver)
        {
            if (armed)
            {
                armed = false;
                hits.Disarm();
            }

            ActionDefinition ended = CurrentAction;
            float t = driver.NormalizedTime;

            playing = false;
            cancelOpen = false;
            warpOpen = false;
            warpActive = false;
            CurrentAction = null;
            idleSeconds = 0f;

            if (stopDriver)
            {
                driver.Stop();
            }

            Trace.EndAction(t, interrupted);
            events.ActionEnded(ended, interrupted);
        }

        private void ProcessCrossings(AttackDefinition attack, float previous, float t, Vector3 position)
        {
            Window warp = attack.Warp;
            if (warp.Opened(previous, t))
            {
                warpOpen = true;
                BeginWarp(attack, position);
            }

            if (warpOpen && warpActive)
            {
                ApplyWarp(attack, previous, t, position);
            }

            if (warp.Closed(previous, t))
            {
                warpOpen = false;
                warpActive = false;
            }

            if (attack.Active.Opened(previous, t))
            {
                armed = true;
                hits.Arm(attack, Target);
            }

            if (attack.Active.Closed(previous, t))
            {
                armed = false;
                hits.Disarm();
            }

            if (attack.CancelAttack.Opened(previous, t))
            {
                cancelOpen = true;
            }

            if (attack.CancelAttack.Closed(previous, t))
            {
                cancelOpen = false;
            }
        }

        private void BeginWarp(AttackDefinition attack, Vector3 position)
        {
            warpActive = false;
            if (Target == null || !Target.IsValid)
            {
                return;
            }

            Vector3 lunge = WarpDestination(attack, position) - position;
            lunge.y = 0f;

            if (lunge.magnitude > attack.MaxLunge)
            {
                WarpRefused = true;
                Trace.MarkWarpRefused();
                return;
            }

            warpActive = true;
        }

        /// <summary>
        /// Moves the remaining distance in proportion to the slice of the warp window this tick
        /// covers, so the character arrives exactly as the window closes however the frames fall,
        /// and re-aims every tick if the target moves.
        /// </summary>
        private void ApplyWarp(AttackDefinition attack, float previous, float t, Vector3 position)
        {
            if (Target == null || !Target.IsValid)
            {
                warpActive = false;
                return;
            }

            Window warp = attack.Warp;
            float from = Mathf.Max(previous, warp.Start);
            float to = Mathf.Min(t, warp.End);
            float span = warp.End - from;
            float fraction = span <= 1e-5f ? 1f : Mathf.Clamp01((to - from) / span);

            Vector3 remaining = WarpDestination(attack, position) - position;
            remaining.y = 0f;
            displacement.Displace(remaining * fraction);
        }

        private Vector3 WarpDestination(AttackDefinition attack, Vector3 position)
        {
            Vector3 targetPosition = Target.Position;
            Vector3 away = position - targetPosition;
            away.y = 0f;

            if (away.sqrMagnitude < 1e-6f)
            {
                away = Vector3.back;
            }

            return targetPosition + away.normalized * attack.StrikeDistance;
        }
    }
}
