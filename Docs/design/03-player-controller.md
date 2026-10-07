# 03 — Player controller: input, combat states, camera

**Status:** task document for implementation. Starts from a copy of `TPCharacterController`
(bh3-development, `GOWDraugr/CharacterController/`). Verify member names against that copy before
building; this document describes intent, not the final API.

## Goal

A third-person controller that plays identically on keyboard and mouse and on a gamepad, with combat
states that host the action system from [01 — Combo System](01-combo-system.md). Locomotion is already
done. This spec adds the intent buffer to the input layer, the combat states, external interrupts, a
displacement sink shared with enemies, and a combat camera.

## Context you need

The copied controller has this shape, and the design below leans on each part as it is:

| Piece | What it does today | Role here |
|---|---|---|
| `CharacterMotor` + `MotionIntent` | States hand the motor one intent per frame: planar velocity, rotation, gravity suppression. No root motion | The attack warp is a planar velocity. Unchanged |
| `CharacterStateMachine`, `CharacterContext`, `ICharacterState` | States request their own transitions and hold no references of their own | Combat states are new siblings of Locomotion |
| `LocomotionState`, `AirborneState` | Grounded and airborne movement from the `Move` vector through a movement frame | Unchanged |
| `ICharacterInput`, `PlayerInputReader`, `BufferedButton` | Semantic actions from an Input System asset; mouse and stick look treated as different quantities; one consume-only buffered press per action | Grows an intent buffer, loses lock-on |
| `CharacterProfile` and the config assets | One asset per character, everything tunable | Gains a `CombatConfig` |
| `PlayerCameraRig` | Cinemachine third-person follow with yaw and pitch | Becomes the exploration mode of the rig; spec 07 adds the combat mode |
| `TPCharacterControls.inputactions` | Gameplay map with Move, Look, Sprint, Dodge, LockOn | Reworked with two control schemes |

Cinemachine is not in this project's manifest and must be added. The Input System already is.

## Principles

1. **Devices differ in the asset, never in code.** Actions are semantic. Bindings, control schemes,
   deadzone processors and gesture interactions live in the `.inputactions` asset. The one existing
   exception, mouse versus stick look scaling, stays because the two really are different quantities.
2. **Direction is the Move vector at press time.** Stick on gamepad, WASD on keyboard, both arrive as
   `Move`. Every intent is stamped with it. The resolver reads it camera-relative.
3. **One intent per frame, still.** Combat states produce a `MotionIntent` like any other state. The
   `ActionRunner` computes displacement; the state turns it into an intent.
4. **States own transitions, but interrupts come from outside.** The hit pipeline and the encounter
   request an interrupt on the context; the active state honours it at the top of its tick.
5. **Device awareness is presentation only.** Glyphs and rumble read the last active device. Gameplay
   never does.
6. **The brain is injected, not self-built.** Today `CharacterBrain` constructs the motor, reader,
   context and states by hand in `Awake`. Here the player prefab carries a `GameObjectContext` whose
   installer binds the profile, the input reader, the intent buffer, the `ActionRunner` with its
   presentation driver and motor sink, the picker, the combo meter and the states; the brain receives
   them. Scene-wide services (hit pipeline, director, combat events) come from the static installers in
   spec 00. The classes themselves are unchanged plain constructor-injected types, so the EditMode tests
   build them directly without a container.

## Input layer

### Action asset

One `Gameplay` map, two control schemes, every action bound in both:

| Action | Type | Keyboard and mouse | Gamepad | Notes |
|---|---|---|---|---|
| `Move` | Vector2 | WASD | Left stick, stick deadzone processor | Direction source for strikes |
| `Look` | Vector2 | Mouse delta | Right stick | Scaled per device in the reader, as today |
| `Sprint` | Button | Left Shift | Left stick press | Ignored by combat states |
| `Strike` | Button | Left mouse | West button | |
| `Counter` | Button | Right mouse | North button | |
| `Evade` | Button | Space | South button | Default single press. A `MultiTap` interaction on the binding turns it into a double tap per scheme without touching code |
| `Stun` | Button | Middle mouse or Q | East button | Cape stun |

`LockOn` is removed. Free-flow target selection replaces it; the movement frame hook it used remains for a
future lock-on mode.

### Intent buffer in the contract

`ICharacterInput` keeps `Move`, `LookDelta` and `SprintHeld`, drops `TryConsumeDodge` and
`TryConsumeLockOn`, and exposes the `IntentBuffer` from spec 01:

| Member | Meaning |
|---|---|
| `Intents` | The buffer. Entries are `{ kind, timestamp, moveAtPress }`. Lifetime per kind from `CombatConfig` |
| `FlushBuffers()` | Unchanged. Called when control is taken away |

`PlayerInputReader` subscribes to the four combat actions and pushes an intent on `performed`, stamping the
current `Move`. `BufferedButton` is retired. A `ScriptedInput` implementation of the interface, driven
from code, is what every PlayMode test in specs 01 to 03 uses.

### Device watcher

A small component records the device behind the last `performed` action and publishes `activeDevice`
(`KeyboardMouse` or `Gamepad`) as an agent fact. The HUD reads it for glyphs; a rumble helper reads it to
decide whether to pulse the pad on a counter. Nothing else may read it.

## Combat states

New siblings of Locomotion, constructed in `CharacterStates` with the same context.

| State | Enters when | Each tick | Leaves when |
|---|---|---|---|
| `Attacking` | The resolver returns a chain node from `Neutral` | Asks the `ActionRunner` for this frame's warp displacement, faces the target, builds the intent with `SuppressGravity` during the warp window | Action ends with no follow-up: back to Locomotion. Follow-up resolved: stays, re-entered with the new action |
| `Countering` | Counter interrupt edge fires | Runner plays the counter action; target is the telegraphing attacker | Action end |
| `Evading` | Evade interrupt edge fires | Runner plays the evade action; displacement along `moveAtPress`, or away from the nearest attacker when Move is zero | Action end |
| `HitReaction` | External interrupt from the hit pipeline | Runner plays the reaction; small knockback displacement | Action end |
| `KnockedDown` | External interrupt, heavy hit | Down for a configured time, then get-up action | Get-up ends |
| `Dead` | External interrupt | Still intent, gravity on | Never |

A follow-up within `Attacking` is a re-entry, not a state change: the state swaps the action on the runner
and resets the warp. This keeps `StateChanged` events meaningful for animation and audio, which care
about "started fighting" rather than "third punch".

Sprint is ignored inside every combat state. Movement input during an action only feeds the next intent's
`moveAtPress`; it never moves the character.

### External interrupts

`CharacterContext` gains `RequestInterrupt(ICharacterState, payload)` and `TryTakeInterrupt(out …)`.
Every state calls `TryTakeInterrupt` first in `Tick` and, if one is pending, changes to it and returns
without producing its own intent. The new state produces that frame's intent on the same tick. Priority
when two arrive in one frame: Dead, then KnockedDown, then HitReaction. Combat interrupt edges from the
resolver (Counter, Evade, Stun) do not use this path; they are resolved inside `Attacking` or `Locomotion`
through the buffer, as spec 01 describes.

## Displacement sink

Spec 02 runs enemy actions through the same `ActionRunner`, but enemies move with a NavMeshAgent. The
runner therefore never touches the motor directly. It reports displacement through:

| Implementation | Used by | Behaviour |
|---|---|---|
| `MotorDisplacementSink` | Player states | Converts displacement into the frame's `MotionIntent.PlanarVelocity` |
| `NavAgentDisplacementSink` | Enemy `Play Action` node | Calls `NavMeshAgent.Move` with the agent's updatePosition left on, so the agent stays on the mesh |

The runner is constructed with a sink and asks nothing else about who owns it.

## Camera

One virtual camera, one rig, two framing modes. The existing `PlayerCameraRig` gains a combat parameter
set (centred, pulled back, distance following the group spread, slow auto-yaw that look input always
overrides) blended against the exploration set when the encounter is active. Target selection and the
token grant read the rig's flattened forward so what the player sees and what the stick means stay
aligned. Specified in full in [07 — Combat camera](07-combat-camera.md).

## CombatConfig (new profile slot)

| Field | Meaning |
|---|---|
| `stance` | The Ground `Stance` asset from spec 01 |
| `intentLifetimes` | Seconds per intent kind |
| `counterAction`, `evadeAction`, `stunAction` | `ActionDefinition`s for the interrupt nodes |
| `hitReactions` | Light, heavy, knockdown, get-up, death |
| `knockdownSeconds` | Time on the ground |
| `targeting` | Reference to the target selector configuration (spec 04) |

`CharacterProfile.Validate` requires it for the player profile and tolerates its absence otherwise, the
way `CameraRigConfig` is handled today.

## Tasks, in order

| # | Task | Done when |
|---|---|---|
| T1 | Copy the controller in | Compiles in this project under its own assembly with Cinemachine added. The existing test scene moves and the camera follows on both a pad and keyboard |
| T2 | Rework the action asset | Two control schemes, every action bound in both, Evade with an optional `MultiTap` on each scheme. The reader throws a precise message when an action is missing, as today |
| T3 | Intent buffer in the contract | `ICharacterInput.Intents`; reader pushes four kinds with `moveAtPress`; `BufferedButton` and lock-on removed; `ScriptedInput` written. EditMode test: a press during a fake lock survives for its lifetime and carries the Move vector sampled at the press |
| T4 | External interrupts | `RequestInterrupt` and `TryTakeInterrupt` on the context; priority order enforced. EditMode test with fake states: an interrupt requested mid-tick is taken next tick, and exactly one intent reaches the motor that frame |
| T5 | Displacement sinks | Motor and NavAgent sinks behind one interface; the runner takes a sink. PlayMode test moves a capsule a known distance through each |
| T6 | Combat states | Six states as tabled; follow-up as re-entry; sprint ignored; movement input never displaces during an action. PlayMode test with `ScriptedInput` and two duration-only actions drives Neutral → Attacking → Attacking → Locomotion |
| T7 | Device watcher | `activeDevice` fact flips when the other device produces input. PlayMode test using the Input System's test fixture with a virtual pad and keyboard |
| T8 | Combat camera | Delivered by spec 07's tasks. Here: the rig compiles against the copied controller and the exploration camera works on both devices |
| T9 | `CombatConfig` | Profile slot, validation, the player profile populated with placeholder actions |
| T10 | Parity check | One scripted sixty-second session replayed through `ScriptedInput` once as "gamepad" and once as "keyboard" intents produces the same action sequence. This is the acceptance test for principle 1 |

T3 to T5 are independent of each other once T1 is in. T6 depends on T3, T4 and T5, and on spec 01's
`ActionRunner`. Spec 07 depends on spec 06 exposing the roster.

## Open decisions

1. **Evade gesture.** Default single press. If the double tap is wanted, it is a `MultiTap` interaction on
   the binding in each scheme, and T2 covers it.
2. **Lock-on.** Removed. The movement frame hook stays, so a lock-on mode can return as a targeting option
   without reopening this spec.
3. **Sprint in combat.** Ignored by combat states. Locomotion inside an active encounter still sprints.

## Non-goals

- No root motion. Displacement is authored as warp data and produced by the runner.
- No rebinding UI. Control schemes are authored in the asset; a rebinding screen is a later feature.
- No gadgets, grapple or traversal states. They are further siblings of Locomotion when wanted.
- No hit pipeline here. `HitReaction` consumes an interrupt that spec 05 produces.
