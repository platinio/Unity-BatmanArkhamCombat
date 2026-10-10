using System;
using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Each action asks the target picker once, when it starts, and keeps that target until it
    /// ends. A character without a stance, like an enemy, drives it through
    /// <see cref="PlayAction"/>.
    /// </summary>
    public sealed class ActionRunner
    {
        private readonly Stance stance;
        private readonly IntentBuffer intents;
        private readonly CombatFacts facts;
        private readonly ComboResolver resolver;
        private readonly IPresentationDriver driver;
        private readonly IWarpMover warpMover;
        private readonly IHitWindowListener hitWindowListener;
        private readonly IActionTargetPicker targetPicker;
        private readonly System.Random random;
        private readonly Dictionary<ChainNode, AttackDefinition> lastAttackPickedAtNode = new Dictionary<ChainNode, AttackDefinition>();

        private bool isPlaying;
        private bool isHitWindowOpen;
        private bool isComboWindowOpen;
        private bool isWarpWindowOpen;
        private bool isWarping;
        private float idleSeconds;

        // Code that starts an action has no press, so its target is picked straight ahead.
        private static readonly Vector2 StraightAhead = Vector2.zero;

        public ActionRunner(
            Stance stance,
            IntentBuffer intents,
            CombatFacts facts,
            IConditionEvaluator conditions,
            InterruptKinds interruptKinds,
            IPresentationDriver driver,
            IWarpMover warpMover,
            IHitWindowListener hitWindowListener,
            IActionTargetPicker targetPicker = null,
            System.Random random = null)
        {
            this.stance = stance;
            this.intents = intents;
            this.facts = facts ?? new CombatFacts();
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.warpMover = warpMover ?? new NullWarpMover();
            this.hitWindowListener = hitWindowListener ?? new NullHitWindowListener();
            this.targetPicker = targetPicker ?? new NullActionTargetPicker();
            this.random = random ?? new System.Random();

            resolver = new ComboResolver(conditions, interruptKinds);
            Trace = new ActionTrace(intents);

            if (stance != null)
            {
                stance.Prepare();
                CurrentNode = stance.Root;
            }
        }

        /// <summary>The action that started, and whether an interrupt started it rather than the combo.</summary>
        public event Action<ActionDefinition, bool> ActionStarted;

        /// <summary>The action that ended, and whether it was interrupted before its end.</summary>
        public event Action<ActionDefinition, bool> ActionEnded;

        public ActionTrace Trace { get; }

        public ChainNode CurrentNode { get; private set; }

        /// <summary>Null while idle.</summary>
        public ActionDefinition CurrentAction { get; private set; }

        /// <summary>Null while idle or while a plain action plays.</summary>
        public AttackDefinition CurrentAttack => CurrentAction as AttackDefinition;

        /// <summary>
        /// Who the current action is aimed at, picked when it started and kept until it ends, so the
        /// warp and the hit go to the same target. Null while idle; may be null or invalid while
        /// playing, and the warp then does nothing.
        /// </summary>
        public IActionTarget CurrentActionTarget { get; private set; }

        public bool IsPlaying => isPlaying;
        public float NormalizedTime => isPlaying ? driver.NormalizedTime : 0f;
        public bool IsHitWindowOpen => isHitWindowOpen;
        public bool IsComboWindowOpen => isComboWindowOpen;
        public bool IsWarpWindowOpen => isWarpWindowOpen;

        /// <summary>True when the current attack did not warp because the target was beyond its lunge limit.</summary>
        public bool WasWarpRefused { get; private set; }

        public float IdleSeconds => idleSeconds;

        private bool HasValidTarget => CurrentActionTarget != null && CurrentActionTarget.IsValid;

        /// <summary>
        /// <paramref name="canStartFromIdle"/> is false while the character is airborne or in a hit
        /// reaction; a press then waits instead of starting an action.
        /// </summary>
        public void Tick(float deltaTime, Vector3 position, bool canStartFromIdle)
        {
            deltaTime = Mathf.Max(0f, deltaTime);

            if (isPlaying)
            {
                AdvanceCurrentAction(deltaTime, position);
            }
            else
            {
                AdvanceIdleTime(deltaTime);
                RestartComboAfterLongIdle();
            }

            if (TryStartActionFromPress(position, canStartFromIdle))
            {
                return;
            }

            if (isPlaying && HasCurrentActionEnded())
            {
                EndCurrentActionAndGoIdle(wasInterrupted: false);
            }
        }

        /// <summary>For code that interrupts the combo, not for presses.</summary>
        public void Interrupt(ChainNode node, Vector3 position)
        {
            if (node == null)
            {
                return;
            }

            StartAction(node, PickAttack(node), position, isInterrupt: true, StraightAhead);
        }

        /// <summary>Plays an action outside the combo: reactions, and every enemy action.</summary>
        public void PlayAction(ActionDefinition action, Vector3 position) =>
            StartAction(null, action, position, isInterrupt: isPlaying, StraightAhead);

        /// <summary>For an external interrupt that has nothing to play yet.</summary>
        public void Cancel()
        {
            if (!isPlaying)
            {
                return;
            }

            EndCurrentActionAndGoIdle(wasInterrupted: true);
        }

        private void AdvanceCurrentAction(float deltaTime, Vector3 position)
        {
            float previousTime = driver.NormalizedTime;
            driver.Tick(deltaTime);
            float currentTime = driver.NormalizedTime;
            Trace.CurrentTime = currentTime;

            if (CurrentAttack != null)
            {
                HandleAttackWindows(CurrentAttack, previousTime, currentTime, position);
            }
        }

        private void AdvanceIdleTime(float deltaTime)
        {
            idleSeconds += deltaTime;
            Trace.CurrentTime += deltaTime;
        }

        /// <summary>A short pause keeps the combo where it was, so the player can resume it.</summary>
        private void RestartComboAfterLongIdle()
        {
            if (stance != null && idleSeconds >= stance.ChainResetSeconds)
            {
                CurrentNode = stance.Root;
            }
        }

        private bool HasCurrentActionEnded() => driver.NormalizedTime >= 1f;

        private bool TryStartActionFromPress(Vector3 position, bool canStartFromIdle)
        {
            if (stance == null || CurrentNode == null)
            {
                return false;
            }

            if (!isPlaying && !canStartFromIdle)
            {
                return false;
            }

            Resolution resolution = resolver.Resolve(stance, DescribeCurrentSituation(), intents, facts);
            if (!resolution.HasMatch)
            {
                return false;
            }

            ChainNode nextNode = resolution.Destination;
            Vector2 directionAtPress = resolution.ConsumedIntent.MoveAtPress;
            StartAction(nextNode, PickAttack(nextNode), position, resolution.IsInterrupt, directionAtPress);
            return true;
        }

        private ComboSituation DescribeCurrentSituation()
        {
            if (!isPlaying)
            {
                return ComboSituation.Idle(CurrentNode);
            }

            return new ComboSituation(CurrentNode, CurrentAttack, driver.NormalizedTime, isComboWindowOpen, isPlaying: true);
        }

        /// <summary>Remembers the pick so a pool can avoid playing the same attack twice in a row.</summary>
        private AttackDefinition PickAttack(ChainNode node)
        {
            lastAttackPickedAtNode.TryGetValue(node, out AttackDefinition lastPick);
            AttackDefinition pick = node.PickAttack(new VariantPickContext(lastPick, facts.TargetSide, random));
            if (pick != null)
            {
                lastAttackPickedAtNode[node] = pick;
            }

            return pick;
        }

        /// <summary>
        /// A null node plays the action outside the combo. A running action is ended first, so its
        /// hit window never stays open behind it. That action counts as interrupted only when an
        /// interrupt replaced it; the next attack of the combo is something the action allowed.
        /// </summary>
        private void StartAction(ChainNode node, ActionDefinition action, Vector3 position, bool isInterrupt, Vector2 targetDirection)
        {
            if (action == null)
            {
                return;
            }

            if (isPlaying)
            {
                EndCurrentAction(wasInterrupted: isInterrupt);
            }

            if (node != null)
            {
                CurrentNode = node;
            }

            CurrentAction = action;
            CurrentActionTarget = targetPicker.PickTarget(targetDirection);
            isPlaying = true;
            idleSeconds = 0f;
            WasWarpRefused = false;

            driver.Play(action);
            Trace.BeginAction(node, action);
            ActionStarted?.Invoke(action, isInterrupt);

            if (action is AttackDefinition attack)
            {
                HandleWindowsStartingAtZero(attack, position);
            }
        }

        private void EndCurrentAction(bool wasInterrupted)
        {
            if (isHitWindowOpen)
            {
                hitWindowListener.HitWindowClosed();
            }

            ActionDefinition endedAction = CurrentAction;
            float endedAtTime = driver.NormalizedTime;

            CloseAllWindows();
            isPlaying = false;
            CurrentAction = null;
            CurrentActionTarget = null;
            idleSeconds = 0f;

            Trace.EndAction(endedAtTime, wasInterrupted);
            ActionEnded?.Invoke(endedAction, wasInterrupted);
        }

        private void EndCurrentActionAndGoIdle(bool wasInterrupted)
        {
            EndCurrentAction(wasInterrupted);
            driver.Stop();
            Trace.BeginIdle();
        }

        private void CloseAllWindows()
        {
            isHitWindowOpen = false;
            isComboWindowOpen = false;
            isWarpWindowOpen = false;
            isWarping = false;
        }

        /// <summary>
        /// Windows that start at zero would otherwise wait for the first tick and be a frame late.
        /// Pretending the clock came from just before zero makes them open now.
        /// </summary>
        private void HandleWindowsStartingAtZero(AttackDefinition attack, Vector3 position)
        {
            HandleAttackWindows(attack, -1f, 0f, position);
        }

        private void HandleAttackWindows(AttackDefinition attack, float previousTime, float currentTime, Vector3 position)
        {
            HandleWarp(attack, previousTime, currentTime, position);
            HandleHitWindow(attack, previousTime, currentTime);
            HandleComboWindow(attack, previousTime, currentTime);
        }

        private void HandleWarp(AttackDefinition attack, float previousTime, float currentTime, Vector3 position)
        {
            Window warpWindow = attack.WarpWindow;
            if (warpWindow.IsOpen(previousTime, currentTime))
            {
                StartWarp(attack, position);
            }

            if (isWarping)
            {
                MoveTowardTarget(attack, previousTime, currentTime, position);
            }

            if (warpWindow.IsClosed(previousTime, currentTime))
            {
                StopWarp();
            }
        }

        private void HandleHitWindow(AttackDefinition attack, float previousTime, float currentTime)
        {
            if (attack.HitWindow.IsOpen(previousTime, currentTime))
            {
                isHitWindowOpen = true;
                hitWindowListener.HitWindowOpened(attack, CurrentActionTarget);
            }

            if (attack.HitWindow.IsClosed(previousTime, currentTime))
            {
                isHitWindowOpen = false;
                hitWindowListener.HitWindowClosed();
            }
        }

        private void HandleComboWindow(AttackDefinition attack, float previousTime, float currentTime)
        {
            if (attack.ComboWindow.IsOpen(previousTime, currentTime))
            {
                isComboWindowOpen = true;
            }

            if (attack.ComboWindow.IsClosed(previousTime, currentTime))
            {
                isComboWindowOpen = false;
            }
        }

        /// <summary>A target beyond the attack's lunge limit refuses the warp, and the attack plays in place.</summary>
        private void StartWarp(AttackDefinition attack, Vector3 position)
        {
            isWarpWindowOpen = true;
            isWarping = false;

            if (!HasValidTarget)
            {
                return;
            }

            if (attack.IsBeyondLunge(position, CurrentActionTarget.Position))
            {
                WasWarpRefused = true;
                Trace.MarkWarpRefused();
                return;
            }

            isWarping = true;
        }

        private void StopWarp()
        {
            isWarpWindowOpen = false;
            isWarping = false;
        }

        /// <summary>
        /// Moves a share of the distance still left to the target: the share of the warp window's
        /// remaining time that this frame covers. The character arrives exactly as the window closes
        /// whatever the frame rate, and follows the target if it moves.
        /// </summary>
        private void MoveTowardTarget(AttackDefinition attack, float previousTime, float currentTime, Vector3 position)
        {
            if (!HasValidTarget)
            {
                isWarping = false;
                return;
            }

            Window warpWindow = attack.WarpWindow;
            float frameStart = Mathf.Max(previousTime, warpWindow.Start);
            float frameEnd = Mathf.Min(currentTime, warpWindow.End);
            float timeLeftInWindow = warpWindow.End - frameStart;
            float shareOfDistance = timeLeftInWindow <= 1e-5f ? 1f : Mathf.Clamp01((frameEnd - frameStart) / timeLeftInWindow);

            Vector3 distanceLeft = attack.WarpDestination(position, CurrentActionTarget.Position) - position;
            distanceLeft.y = 0f;
            warpMover.MoveBy(distanceLeft * shareOfDistance);
        }
    }
}
