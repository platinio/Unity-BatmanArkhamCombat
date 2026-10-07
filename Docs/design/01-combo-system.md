# 01 — Combo System: intent buffer, attack chain resolver, combo meter

**Status:** task document for implementation. Written before any combat code exists in this project.
Verify type and member names against bh3-development before reusing anything from it.

## Goal

A free-flow combat chain in the Batman Arkham style, built on an engine general enough to also drive a
multi-input chain (Devil May Cry, Bayonetta). One button means "strike"; context picks the move, the
target, and the travel. A counter and an evade interrupt from anywhere. A combo meter sits beside the
chain and gates finishers.

The deliverable of this document is the **player side** of combat: input to action to animation. Enemy
behaviour, the hit pipeline, and the encounter director are covered by later specs and are referenced
here only where the chain reads from them.

## Context you need

- `TPCharacterController` (bh3-development, `GOWDraugr/CharacterController/Runtime/`) already owns the
  motor, the locomotion state machine, a `PlayerInputReader`, and a `BufferedButton`. Combat is a new set
  of states on that machine, not a second controller.
- BH3 **Functions** are Visual Scripting graphs saved as assets with declared inputs, a declared result
  type, and the agent facts they read (`docs/2-building-trees/08-functions.md`). They are the condition
  language for this system.
- `EncounterDirector` (bh3-development, `GOWDraugr/BH3/Encounter/`) decides which enemy may attack. A
  later spec makes it publish a telegraph; the counter edge in this spec reads that telegraph as a fact.
- Facts are `Object` variables on the agent's `Variables` component, written through
  `AgentVariableWriter` so change detection works (`docs/2-building-trees/06-guards.md`).

## The problem this design solves

A previous attempt wired an Animator state graph with transitions driven by inputs. It failed for
reasons that are structural, not tuning:

1. **The input was the clock.** Transitions were evaluated continuously, so a press during recovery was
   either dropped or fired late through a stale trigger.
2. **Chain position and move were the same thing.** Reusing a clip in two chains meant duplicating the
   state and all of its edges.
3. **Interrupts shared the graph with follow-ups.** Hit reactions and counters multiplied the edges until
   the graph was unreadable.

## Principles (hold these during implementation)

1. **The animation is the clock.** Follow-up decisions happen only when the current attack opens its
   cancel window. Nothing else in the chain reads input directly.
2. **Intents, not buttons.** Input is mapped to intents (`Strike`, `Counter`, `Evade`, `Stun`) with a
   timestamp and the stick direction at press time, and stored in a buffer with a lifetime.
3. **A chain node is not an attack.** The attack definition is a duration with windows. The chain node is a
   position in the chain with edges. The same attack definition may back many nodes.
4. **Interrupts are a separate channel** evaluated before follow-ups and mostly independent of windows.
5. **Conditions are pure.** An edge condition reads facts and returns a bool. It never writes.
6. **Time is passed in.** Every runtime class takes `deltaTime` through a `Tick` call so EditMode tests
   can drive it, mirroring `EncounterDirector`.
7. **The combo meter is not the chain.** It has its own increment and reset rules and its own timeout.

## Data model

### AttackDefinition (ScriptableObject)

Knows nothing about chains. Windows are normalized action time `[0, 1]`.

| Field | Type | Meaning |
|---|---|---|
| `duration` | float | The clock. Normalized time is elapsed over duration. An optional `clip` may supply it instead (spec 08) |
| `active` | window | Hit frames; the hit pipeline is told to arm and disarm hitboxes at these edges |
| `cancelAttack` | window | While open, the resolver may consume a follow-up intent |
| `cancelEvade` | window | While open, the Evade interrupt may cancel this attack |
| `warp` | window | Character is displaced toward the target over this range |
| `strikeDistance` | float | Where the warp wants to end relative to the target |
| `maxLunge` | float | Beyond this the warp refuses; the resolver should have picked a travelling variant |
| `damage`, `reaction` | | Handed to the hit pipeline (spec 05) |
| `counterable`, `unblockable` | bool | Read by enemies; present here so one asset type serves both sides |
| `tags` | string set | Free-form, read by Functions through the context |

A window is a pair of floats with `Contains(t)` and `Crossed(previousT, t)` helpers so the runner can
fire open and close events once each.

### ChainNode

| Field | Meaning |
|---|---|
| `id` | Stable name used in the debug overlay and tests |
| `attack` **or** `pool` | One attack definition, or a variant pool |
| `edges` | Prioritized list of `Edge` |

### VariantPool

A list of attack definitions plus a selection policy, held as a `[SerializeReference, SubclassSelector]
IVariantPolicy` so the policy is picked from a dropdown on the node. Built-in policies: `NoRepeat`,
`TargetSide` (left or right of the player), `Random`. A `FunctionIndex` policy that asks a Function for
an index is a stretch goal, not a first-pass requirement.

### Edge

| Field | Meaning |
|---|---|
| `intent` | Which intent matches |
| `condition` | Optional Function asset with a `bool` result; `null` means always |
| `priority` | Lower runs first within a node |
| `destination` | ChainNode |

### Stance (ScriptableObject)

| Field | Meaning |
|---|---|
| `root` | The Neutral node |
| `nodes` | All nodes in the stance |
| `globalEdges` | Interrupt edges reachable from any node (Counter, Evade, Stun) |
| `chainResetSeconds` | Idle time after the last consumed intent before the position collapses to `root` |

First stance to author, as a worked example and the acceptance fixture:

```text
Stance: Ground
  global edges
    Counter -> CounterNode     cond: IncomingAttackCounterable
    Evade   -> EvadeNode
    Stun    -> CapeStunNode

  Neutral (root)
    Strike  -> GlideKick       cond: TargetBeyondLunge      priority 1
    Strike  -> S1                                            priority 2

  S1   pool: Jab_L / Jab_R     select: TargetSide
    Strike  -> Takedown        cond: ComboAtLeast(8) AND TargetStaggered
    Strike  -> S2

  S2   attack: Cross
    Strike  -> Takedown        cond: ComboAtLeast(8) AND TargetStaggered
    Strike  -> S3

  S3   attack: RoundhouseKick
    Strike  -> S1
```

### IntentBuffer

Holds `Intent { kind, timestamp, stickDirection }` entries. Configurable lifetime per intent kind
(default 0.25 s). Operations: `Push`, `PeekNewest(kind)`, `Consume(entry)`, `Tick(deltaTime)` which
expires dead entries. The buffer never decides anything.

### CombatContext (facts)

Published through `AgentVariableWriter` before the resolver runs, so Functions can read them and so the
same keys show in BH3's Variable Watch:

| Key | Type | Source |
|---|---|---|
| `comboCount` | int | ComboMeter |
| `comboTier` | int | ComboMeter |
| `targetDistance` | float | Target selection (spec 04) |
| `targetSide` | int (-1, 0, 1) | Target selection |
| `targetState` | string | Target's status component |
| `targetBeyondLunge` | bool | `targetDistance > current attack's maxLunge` |
| `incomingAttackCounterable` | bool | Encounter telegraph (spec 06) |
| `stickAngleToTarget` | float | Intent buffer entry being resolved |

### ComboMeter

| Rule | Behaviour |
|---|---|
| Increment | Landed strike, successful counter, successful evade |
| Reset to 0 | Player takes a hit; a strike whiffs (no valid target); `meterTimeoutSeconds` elapses with no increment |
| Tiers | Thresholds list, e.g. `[3, 5, 8]`; `comboTier` is the highest threshold reached |
| Events | `Changed(count, tier)`, `Reset(reason)` for HUD and audio |

## Runtime

### ActionRunner

Owns the current attack and its normalized time. Each `Tick`:

1. Advance `t` from the presentation driver (not from deltaTime alone, so hit-stop and speed changes stay
   honest).
2. For each window, if `Crossed` fire the matching open or close event.
3. On `active` open and close, tell the hit pipeline to arm or disarm.
4. During `warp`, displace toward `targetPosition - direction * strikeDistance`, clamped by `maxLunge`.
5. On `cancelAttack` open, and every tick while it stays open, ask the resolver.
6. On clip end with no follow-up taken, return the stance position to `root` and hand control back to
   locomotion.

An interrupt (`Interrupt(ChainNode)`) replaces the current attack immediately, fires any pending close
events so hitboxes never stay armed, and starts the destination.

### ComboResolver

Pure function, no state of its own beyond the current node:

```text
Resolve(stance, currentNode, buffer, context, windowOpen):
  for edge in stance.globalEdges by priority:
    intent = buffer.PeekNewest(edge.intent)
    if intent != null and Evaluate(edge.condition, context) and InterruptAllowed(edge):
      buffer.Consume(intent); return edge.destination
  if not windowOpen: return null
  for edge in currentNode.edges by priority:
    intent = buffer.PeekNewest(edge.intent)
    if intent != null and Evaluate(edge.condition, context):
      buffer.Consume(intent); return edge.destination
  return null
```

`InterruptAllowed` is where Evade checks the current attack's `cancelEvade` window and Counter checks
the telegraph fact. A matched edge consumes exactly one intent. An unmatched intent stays in the buffer
until it expires, which is what lets a press during recovery start the next chain from `root`.

### Chain reset

`Stance.chainResetSeconds` counts from the last consumed intent. When it elapses while no attack is
playing, the current node becomes `root`. This number and `ComboMeter.meterTimeoutSeconds` are tuned
separately and must stay separate fields.

### Presentation driver

Specified in [08 — Presentation driver](08-presentation-driver.md). The runner reads `NormalizedTime`
and `Speed` from an `IPresentationDriver` and calls `Play(action)`; it never references an Animator. For
this demo the driver is procedural: `duration` on the action is the clock and tweened cues on a body
child are the picture.

## Functions as conditions

- An edge condition is a Function whose `Result` is `bool`. The resolver refuses any other result type
  at load, by name, the same way `GuardOnFunction` does.
- Functions read the context through `Get BT Variable`, so each declares its watched keys. A verification
  pass over a Stance collects every key declared by every referenced Function and compares it with the
  keys `CombatContext` publishes. Missing keys are reported as errors naming the stance, the edge, and
  the Function.
- Purity is enforced by the same `bt_verify` rule that flags writes inside guard graphs. Confirm the rule
  runs on Functions referenced from a Stance, and extend it if it does not.
- Starter Functions to author: `TargetBeyondLunge`, `ComboAtLeast` (with an `int threshold` input),
  `TargetStaggered`, `IncomingAttackCounterable`.

## Debug overlay (build this first)

A runtime overlay drawn per action showing a horizontal bar for the clip with the four windows as
coloured bands, a playhead, and a tick mark for every intent push with its kind and whether it was
consumed, expired, or is still live. The last eight actions stay on screen as a strip. In the editor the
same data is logged to the BH3 flight recorder so it appears in the Timeline alongside the enemy trees.

Without this overlay a chain that feels wrong cannot be diagnosed. It is scheduled early because every
later task is tuned with it.

## Tasks, in order

Each task lists what "done" means. Tests are EditMode unless stated; PlayMode is needed only where
animation time is involved.

| # | Task | Done when |
|---|---|---|
| T1 | `IntentBuffer` | Push, peek newest of a kind, consume, expiry by lifetime per kind. Tests: expiry, newest-wins, consume removes only the one entry |
| T2 | `AttackDefinition`, `Window` | Asset type with the fields above. Tests on `Window.Crossed` for enter, exit, wrap-around on loop, and zero-length windows |
| T3 | `ChainNode`, `Edge`, `VariantPool`, `Stance` | Asset types; `Stance` validation reports a node not reachable from root, an edge with a null destination, and a pool with no entries |
| T4 | `ComboMeter` | Increment, reset reasons, tiers, timeout, events. Tests for every row in the rules table |
| T5 | `ComboResolver` | Pure resolution as specified, with fake Function evaluation injected. Tests: priority order, interrupt before follow-up, intent survives a closed window and matches at root, exactly one intent consumed per resolve |
| T6 | Presentation driver | Delivered by spec 08. Here: the runner compiles against `IPresentationDriver` and a null driver exists for tests |
| T7 | `ActionRunner` | Window events fire once each, interrupts disarm hitboxes, warp clamps to `maxLunge`, clip end returns to root. PlayMode test with two short duration-only actions |
| T8 | Debug overlay | Bands, playhead, intent marks, eight-action strip; flight recorder entries in editor |
| T9 | Function conditions | Result-type check at load; watched-key verification against `CombatContext`; the four starter Functions authored |
| T10 | Combat states on `TPCharacterController` | `Attacking`, `Countering`, `Evading` states; locomotion input filtered while an action plays; movement returns on clip end |
| T11 | Ground stance fixture | The worked example above authored as assets, with duration-only actions and spec 08 cues, playable end to end in a test scene against a static dummy that publishes `targetState` |
| T12 | PlayMode soak | Sixty seconds of scripted strike presses at random intervals against three dummies: no exception, no armed hitbox left behind |

T1 through T5 have no Unity scene dependency and can be built and tested in one pass. T6 and T7 are
where the animation decision lands. T8 should land before T11 so the fixture is tuned with it.

## Open decisions

1. **Project location.** Standalone here with BH3 and the needed modules as submodules, or a folder under
   `bh3-development/Assets/ArcaneOnyx/BH3Demos/`. Changes assembly references and which repo gets the
   commits. Default assumed by this document: standalone, modules as submodules.
2. **Animation driver.** Resolved by spec 08: a procedural presentation driver on a body child, no clips.
3. **Clip source.** Resolved by spec 08: none. `duration` is the clock; a clip is an optional upgrade.
4. **Intent mapping for direction.** Whether `Strike` carries direction only or whether a
   `StrikeForward` intent exists. Default: one `Strike` intent with direction on the entry; Functions
   read `stickAngleToTarget`.

## Non-goals

- No enemy behaviour, hit pipeline, or target selection here beyond the facts they publish (specs 02
  to 06).
- No GraphCore canvas for authoring stances in this pass. The ScriptableObject lists must prove the data
  model first.
- No gadget or aerial chains. The model supports them as further stances; none is authored.
- No attempt to make the resolver itself a BH3 tree or Function. Conditions are Functions; the walk is C#.

## Implementation status

**Updated 2026-10-07.** T1 to T8 are implemented and tested, with the spec 08 slice they need. T9, T11 and
T12 are not started. T10 has the `Attacking` state only. Nothing is committed: the change sits in the
working tree of this repository and of the `Assets/ArcaneOnyx/CharacterController` submodule, which must be
committed first and its pointer bumped here.

### What exists

| Where | What |
|---|---|
| `CharacterController` submodule, `Runtime/Inputs/` | `IntentKind`, `Intent`, `IntentLifetimes`, `IntentBuffer` (T1). `ICharacterInput.Intents` replaces `TryConsumeDodge` and `TryConsumeLockOn`; `BufferedButton` is gone. `PlayerInputReader` pushes Strike, Counter, Evade and Stun on `performed`, stamping the move vector read at the press. `InputConfig` names the four actions and holds the lifetimes. `TPCharacterControls.inputactions` has the spec 03 action table and two control schemes. Tests in `Test/EditMode/` |
| `ArkhamCombat.Combat` | `Window`, `ActionDefinition`, `AttackDefinition` (T2); `ChainNode`, `Edge`, `VariantPool` with `NoRepeat`, `Random` and `TargetSide`, `Stance` with validation (T3); `ComboMeter` (T4); `ComboResolver` (T5); `IPresentationDriver`, `ActionClock`, `NullPresentationDriver` (T6, spec 08 T2); `ActionRunner`, `ActionTrace` (T7, T8); `CombatContext`, `IConditionEvaluator`, `ICombatEvents` with null and logging stubs, the runner's three seams `IActionTarget`, `IDisplacementSink`, `IHitWindowSink` |
| `ArkhamCombat.Presentation` | `ProceduralPresentationDriver` on DOTween, cue kinds `Lean`, `Punch`, `Squash`, `Spin`, `Flash` |
| `ArkhamCombat.Player` | `CombatConfig`, `CombatStaticInstaller`, `CombatBrain`, `AttackingState` and its binding, `MotorDisplacementSink`, `CombatContextPublisher`, `FunctionConditionEvaluator`, and three stand-ins: `StandInTargetPicker` (spec 04), `DemoHitWindowSink` (spec 05), `CombatDummy` |
| `ArkhamCombat.Shell` | `FrameDataOverlay` (T8), IMGUI |
| `ArkhamCombat.Editor` | `ChainNodeIdDrawer`, the dropdown for edge destinations; `GroundStanceFixtureBuilder`, menu `ArkhamCombat/Build Ground Stance Fixture` |
| Assets | `Actions/Jab_L, Jab_R, Cross, RoundhouseKick`, `Stances/Ground`, `Settings/CombatConfig`, `Installers/Static/CombatStaticInstaller`. `CombatArena.unity` has the driver, the brain, a `Fist` child, three dummies and the overlay |
| Tests | `Assets/ArkhamCombat/Tests/EditMode` (75) and the submodule's `Test/EditMode` (7). T7 is EditMode rather than PlayMode: time is passed in, so a real clock adds nothing |

### Decisions taken while building

1. **The intent buffer lives in the controller submodule.** `ICharacterInput` must expose it and the
   submodule cannot reference a project assembly. Lifetimes sit on `InputConfig` next to the action names,
   not on a `CombatConfig` profile slot, for the same reason: `CharacterProfile` cannot name a project type.
   `CombatConfig` is bound directly by the combat installer instead.
2. **Chain nodes are serializable classes inside the `Stance` asset, found by id.** One asset per stance, no
   sub-assets. `[ChainNodeId]` gives the designer a dropdown of the stance's ids. The root node may have
   nothing to play; every other node needs an attack or a pool entry.
3. **Clip end keeps the chain position for `chainResetSeconds` of idle time, then collapses to the root.**
   The spec says both "clip end returns to root" and "chain reset after idle time"; the grace window is the
   reading where both fields do something. With `chainResetSeconds` at zero the behaviour is the literal T7.
   The timer counts idle seconds, not seconds since the last consumed press, or any action longer than the
   grace would reset on arrival.
4. **The resolver runs every tick** while playing: global edges through their gates (Evade needs the attack's
   `cancelEvade` window, Counter needs the telegraph fact), node edges only while `cancelAttack` is open.
   While idle the node edges are always open, which is what lets a press during recovery continue the chain.
5. **A follow-up is a cancel, an interrupt is a global edge.** `ActionEnded(action, interrupted)` and the
   trace say which; the overlay shows "cancelled at" versus "interrupted at".
6. **Cue scheduling is a pure `ActionClock`** composed by the driver, so cue timing is EditMode-tested. Cues
   at time zero fire on `Play`, not on the first tick, so a frozen clock fires nothing. `ICueKind.Play`
   returns nothing: every tween carries the driver as its id and the driver kills, pauses and resumes by id.
   `Speed` zero pauses; any other value only scales the clock, not the tweens.
7. **Facts go through `AgentVariableWriter.SetOn`.** The player has no `BehaviorTreeMachine`, so writes are
   stored but neither versioned nor recorded. That is the writer's documented behaviour and changes nothing
   for Functions; it means T8's flight recorder entries have no destination until the player carries a
   machine. Reported as a gap, not faked.
8. **Target side and stick angle are character-relative**, measured against the character's forward and
   the movement frame. Spec 04 may move both to the camera forward when it lands.
9. **Zenject is scene-wide static installers**, matching the existing player installer rather than the
   overview's `GameObjectContext` on a prefab, which the project does not have yet. Classes that need the
   player's `GameObject` take `CharacterMotor`, not `CharacterBrain`, so no injection cycle runs through the
   state list.
10. **`ArkhamCombat.Combat` references the controller, VisualScriptingExtension and Visual Scripting**, for
    the intent types and `FunctionCall<bool>`. It still references neither BH3, DOTween nor Zenject, and the
    boundary tests assert the last two. `ArkhamCombat.Player` references BH3 for the writer. The user lifted
    the overview's BH3 restriction.
11. **`FunctionConditionEvaluator` does the load-time result-type check** from T9 and treats a Function that
    cannot run as false after reporting it once. Watched-key verification and the starter Functions are T9
    proper and not done. The fixture therefore has no `GlideKick` or `Takedown` edges and no global edges.

### Known gaps and what comes next

- T9: the four starter Functions, watched-key verification against `CombatContext.Keys`, and the purity
  rule. `FunctionCallVerification.FindCallSites` does not find a call nested inside `ChainNode.edges`; a
  stance-level pass must call `FunctionCallVerification.Verify` per edge.
- T10: `Countering`, `Evading`, `HitReaction`, `KnockedDown`, `Dead`, and spec 03's external interrupts.
- T11: the full Ground stance with its global edges once the interrupt nodes and Functions exist. The
  fixture builder is the place to add them.
- T12: the soak.
- Spec 08: locomotion visuals (T4), the body prefabs (T5), paired actions (T6), the telegraph pulse (T7),
  `pairedReaction` and `pairedOffset` on `ActionDefinition`, the optional clip.
- The Hermes adapter for `ICombatEvents`; the installer currently binds the logging stub.
- The player build was already failing before this change: `EditorBuildSettings` lists
  `Assets/Scenes/SampleScene.unity`, which does not exist, and `CombatArena.unity` is not in the build.
- The Editor rewrote `Assets/Settings/*.asset` and `ProjectSettings/ProjectSettings.asset` during the
  baseline build (URP prefiltering, static batching). Not part of this change; discard or keep as you see fit.

### Review outcome

An independent review of the change (2026-10-07) found no correctness defect in the core. Applied from
it: edges are sorted into runtime lists so `OnValidate` never reorders the authored data; the lunge rule
lives once on `AttackDefinition` and both the warp and the `targetBeyondLunge` fact read it; the installer
validates the stance and every attack at scene load; an edge to a node with nothing to play is a
validation error; Evade is gated on idle or the evade window, so a plain reaction cannot be evaded out of;
facts are written only on change; a queued press continues the chain during recovery without a frame in
Locomotion; `CombatBrain` takes its `CharacterBrain` from its own object; null guards in the flash and spin
cues and the hit sink; the overlay draws on Repaint only; tests for the cancel-versus-interrupt rule, a
press inside an open window, a moving warp target, the code interrupt path and the lunge parity. Left
open, by cost: a `GameObjectContext` on the player before enemies share the runner (today the runner,
sinks and driver are scene singletons); EditMode tests for the Player assembly's pure pieces (`SideOf`,
the hit sink rule); the fist as a child of the body; the dead `SampleScene` build entry was replaced by
`CombatArena`.

### Verification

Baseline before the change: EditMode 879 passed, PlayMode 111 passed, player build failed (a build
settings entry for a scene that no longer existed). After the review fixes: EditMode 968 passed (879 plus
the 89 new tests), PlayMode 111 passed, player build `Succeeded` with `CombatArena.unity`, 0 errors. The
chain was played end to end in `CombatArena` with a clean console.
