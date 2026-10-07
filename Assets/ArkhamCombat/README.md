# ArkhamCombat

The free-flow combat demo's own code: the combo system, its presentation driver, the player's combat
states, the shell. Design documents live in `Docs/design/`; this file says where things are and how to
run them.

## Assemblies

| Assembly | Folder | Holds | May reference |
|---|---|---|---|
| `ArkhamCombat.Combat` | `Runtime/Combat` | Actions and windows, the chain (stance, nodes, edges, pools), the resolver, the meter, the action runner and its trace, the presentation clock and the seams the rest plugs into | The character controller (intents), VisualScriptingExtension (Functions). Never DOTween, never Zenject: a test asserts both |
| `ArkhamCombat.Presentation` | `Runtime/Presentation` | The DOTween driver and the cue kinds | Combat, DOTween |
| `ArkhamCombat.Player` | `Runtime/Player` | The combat installer, the combat brain, the `Attacking` state, the fact publisher, the Function evaluator, and the stand-ins behind the core seams: `StandInTargetPicker` (`ITargetPicker`), `SceneTargetRoster` (`ITargetRoster`), `CombatDummy` (`ICombatTarget`), `DemoHitWindowSink` (`IHitWindowSink`) | Everything above, BH3 for the fact writer |
| `ArkhamCombat.Shell` | `Runtime/Shell` | The frame-data overlay and the camera demo | Combat, Camera |
| `ArkhamCombat.Editor` | `Editor` | The node id dropdown and the fixture builder | |
| `ArkhamCombat.Tests` | `Tests/EditMode` | EditMode tests for everything in Combat | |

## How the chain runs

1. `PlayerInputReader` (controller submodule) turns a press into an `Intent` in the `IntentBuffer`,
   stamped with the move vector at that instant. Entries expire by kind.
2. `CombatBrain` runs before `CharacterBrain` every frame: picks a target, publishes the facts, ticks the
   meter and the `ActionRunner`, and switches the state machine to `Attacking` when the runner starts.
3. `ActionRunner` reads normalized time from the `IPresentationDriver`, fires each window crossing once,
   arms and disarms the `IHitWindowSink`, feeds the warp to the `IDisplacementSink`, and asks the
   `ComboResolver` whether a queued press takes the chain somewhere. Global edges are tried at any time
   through their gates; a node's edges only while `cancelAttack` is open or nothing plays.
4. `AttackingState` turns the frame's displacement into the motor's planar velocity and faces the target.
   When the runner goes idle it hands back to `LocomotionState`.

The chain position survives an action's end for the stance's `chainResetSeconds` of idle time, then
collapses to the root.

## Authoring

- An `AttackDefinition` asset holds the duration, the four windows, the travel numbers and the cues.
  Create one with **Create → ArkhamCombat → Attack**. Cue kinds are picked from a dropdown on each cue.
- A `Stance` asset holds the nodes and the global edges. Nodes are found by id; an edge's destination is
  a dropdown of the stance's ids. An edge's condition is a `FunctionCall<bool>`; empty means always.
- `CombatConfig` names the stance, the meter thresholds and the stand-in tunables. The
  `CombatStaticInstaller` asset in `Assets/Installers/Static` points at it and picks the events sink.
- **ArkhamCombat → Build Ground Stance Fixture** rebuilds the four attacks, the Ground stance and the
  config in place, so the fixture is reproducible from code.

## Running it

Open `Assets/CombatArena.unity` and press Play. Left mouse or the gamepad's west button strikes; the chain
is Jab (left or right by target side), Cross, Roundhouse, then back to the jab. The overlay in the top left
is the spec 01 frame-data strip: one bar per recent action with the warp (yellow), active (red), cancel
(green) and evade (blue) windows, magenta cue marks, a white playhead and a tick per press showing whether
it was spent (green), expired (red) or is still queued (yellow).

## Tests

`ArkhamCombat.Tests` runs in EditMode and needs no scene. `ActionRunnerTests` drives the runner with the
null driver and fake sinks; `AssemblyBoundaryTests` keeps DOTween and Zenject out of the core.
