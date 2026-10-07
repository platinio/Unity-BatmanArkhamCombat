# 00 — Arkham combat demo: overview

**Status:** the index and the decision record for the design documents in this folder. Read this first.
Specs 01 to 08 each stand alone for their system; this document holds what cuts across them.

## What the demo is

A Batman Arkham style free-flow brawl in a flat arena. One player character, three enemy archetypes
driven by BH3 behaviour trees, a handful of enemies at a time. The point is the AI and the feel of the
flow: the token choreography that keeps one attacker coming at a time, the committed swing, the counter
window, the warp into every strike, and the combo meter that gates finishers. It is built with capsules
and tweens, no authored animation, and every system is tuned from numbers visible in an overlay.

Everything in it is designer-modifiable in the BH3 sense: an enemy's behaviour is a root tree listing
sub-trees, guards and arguments, and a variant is a prefab variant, a root edit, or a Function swap.

## Locked decisions

Collected from the specs' open-decision sections, so nobody reopens one by accident. The spec that
argued each is in brackets.

| Decision | Choice |
|---|---|
| Project layout | Standalone repository. BH3 and its dependencies as git submodules [01, 02] |
| Dependency injection | Zenject (platinio fork) with ScriptableObject static installers for scene-wide services and GameObjectContext on the player and enemy prefabs. The core assembly stays container-agnostic: constructor-injected plain classes, no Zenject reference, so EditMode tests never see a container [00] |
| Awareness | None. Every unit knows every other from spawn; the director roster is the world [04] |
| Tactical positions | Not used. The ring and the strike distance are the only positional decisions [02, 06] |
| Target selection module | Not used. A fifty-line scored pick over the roster [04] |
| Tree authoring | In the Editor through the `bt_*` tools; assets are the source of truth. No generator [02] |
| Hits | Targeted, not volumetric. A strike lands on its chosen target by range and angle at window open [05] |
| Animation | None authored. Duration is the clock; tweened cues on a body child are the picture; clips are an optional upgrade path [08] |
| DOTween | Allowed for the body child and the HUD only. A boundary test keeps it out of gameplay assemblies [08] |
| Presentation events | Hermes, behind an `ICombatEvents` interface in the core with a fake for tests and the adapter in Shell. Gameplay calls stay direct [00] |
| World-space debug | MGizmos. The frame-data overlay stays screen-space [00, 04, 06] |
| Camera | One virtual camera on the existing rig, always in the combat framing: high, pulled back, pitched down at the character, not steered by look input. The demo has no exploration, so no mode blend is needed; the rig keeps a framing-source seam and the exploration source only for the controller module's own test scene. No target group, no second vcam [07] |
| Auto-yaw | On, slow, with a config toggle [07] |
| Evade input | Single press; double tap available per control scheme as a binding interaction [03] |
| Lock-on | Removed. The movement frame hook stays [03] |
| Sprint in combat | Ignored by combat states [03] |
| Encounter code | Extracted from bh3-development into its own module repo, added as a submodule to both [06] |
| Token grant | Request-and-grant at tick end, scored by front, fairness and distance [06] |
| Target forward | The combat camera's flattened forward, read by the picker, the token grant and the movement frame [07] |
| `ActionDefinition` | Base asset with duration, windows and cues; `AttackDefinition` adds hit fields [02, 08] |
| Paired actions | Start together on `BeginPair`, resolve at the attacker's active window, break on either interrupt [05] |
| Knife enemy | Keeps Flinch. Remove the branch from its root if it stun-locks too easily [02] |
| Ranged token pool | Kept, unused [06] |

## The documents

| Spec | System | One line |
|---|---|---|
| [01](01-combo-system.md) | Combo system | Intent buffer, attack chain as nodes and edges resolved at cancel windows, combo meter, action runner, frame-data overlay |
| [02](02-enemy-behavior-trees.md) | Enemy behaviour trees | Eleven shared sub-trees, three roots, the prefab fact contract, variants in three layers |
| [03](03-player-controller.md) | Player controller | The copied controller plus the intent buffer, combat states, external interrupts, displacement sinks |
| [04](04-free-flow-target-selection.md) | Target selection | One pure pick over the roster: angle, distance, telegraph bonus, sticky bonus |
| [05](05-hit-pipeline.md) | Hit pipeline | Targeted land check, reaction rules, i-frames and counters, paired actions, events |
| [06](06-encounter-director.md) | Encounter director | Facts, telegraph relay, validated slots, scored token grant, roster, module extraction |
| [07](07-combat-camera.md) | Combat camera | Group framing math, mode blend, auto-yaw rules, latched movement frame, impulse |
| [08](08-presentation-driver.md) | Presentation driver | Duration clock, cue kinds, procedural locomotion visuals, bodies, paired-action rules |
| 09 | Demo shell | Not yet written: arena, spawner, HUD, reset loop, feedback listener |

## How the systems depend on each other

```text
08 Presentation driver ──────────────┐
                                      ▼
01 Combo system (ActionRunner, ActionDefinition, IntentBuffer, ComboMeter)
      │                 │                       │
      ▼                 ▼                       ▼
03 Player controller   05 Hit pipeline         02 Enemy trees (Play Action node)
      │                 │        ▲               │
      │                 │        │               ▼
      │                 └────────┼──────► 06 Encounter director ◄── 02 facts
      ▼                          │               │
07 Combat camera ◄───────────────┼───────────────┘ (roster, IsActive)
      │                          │
      └──► 04 Target selection ──┘ (camera forward in; strike announcement out to 02)
```

Read it as: the runner and its action asset sit under everything; the hit pipeline connects the two
sides; the director is the enemies' shared brain and the camera's and picker's source of truth.

## Assemblies and folders

```text
Assets/
  ArcaneOnyx/
    BH3/                              submodule  platinio/Unity-BH3
    GraphCore/                        submodule  platinio/graph-core-library, BH3 dependency
    VisualScriptingExtension/         submodule  platinio/visual-scripting-extension, BH3 dependency
    BlockVariables/                   submodule  platinio/Unity-BlockVariables, BH3 dependency
    MGizmos/                          submodule  platinio/Unity-MGizmos, world-space debug marks (spec 04, 06)
    HermesEventGenerator/             submodule  platinio/Unity-HermesEventGenerator, presentation events
    ScriptableObjectDatabase/         submodule  platinio/Unity-ScriptableObjectDatabase, Hermes dependency
    AdvancedDropdown/                 submodule  platinio/Unity-AdvancedDropdown, ScriptableObjectDatabase editor dependency
    Zenject/                          submodule  platinio/Unity-Zenject, dependency injection with static installers
    SerializeReferenceExtensions/     vendored   MackySoft 1.6.1, MIT, copied from bh3-development; SubclassSelector dropdown for interface fields
    UnityExtensions/                  submodule  platinio/Unity-EditorExtension, Zenject fork dependency (asset generation, project reload hooks)
    Encounter/                        submodule  new, extracted per spec 06
    CharacterController/              submodule  platinio/Unity-CharacterController (spec 03)
  ArkhamCombat/
    Runtime/
      Combat/        ArkhamCombat.Combat         01, 05: actions, runner, resolver, meter, pipeline
      Player/        ArkhamCombat.Player         03, 04: combat states, intents, picker
      Enemies/       ArkhamCombat.Enemies        02: sensors, Play Action, Move To Strike Distance
      Camera/        ArkhamCombat.Camera         07
      Presentation/  ArkhamCombat.Presentation   08: driver, cues, bodies  (the only DOTween reference)
      Shell/         ArkhamCombat.Shell          09: spawner, HUD, reset, Hermes adapter (the only dispatcher reference)
    Editor/          ArkhamCombat.Editor
    Tests/
      EditMode/      ArkhamCombat.Tests
      PlayMode/      ArkhamCombat.PlayTests
    Trees/           sub-trees, roots, Functions
    Actions/         ActionDefinition and AttackDefinition assets
    Prefabs/
    Scenes/
Docs/design/         these documents
```

Submodules sit directly under `Assets/ArcaneOnyx/`, flatter than bh3-development's `Modules/` layer.
This project only consumes modules, so the extra level bought nothing; path examples copied from
bh3-development's skills or notes need the `Modules/` segment removed. Each submodule is pinned to the
commit bh3-development records, so the set is known to compile together.

Rules that follow from the specs:

- `ArkhamCombat.Combat` references the character controller (for the intent types), VisualScriptingExtension
  (for `FunctionCall<bool>` on an edge) and `UnityEngine` basics, and never DOTween, Zenject or Cinemachine.
  It is the EditMode-tested core. BH3 and Function graphs may be referenced by any assembly that needs
  them; `ArkhamCombat.Player` references BH3 for the fact writer.
- `ArkhamCombat.Presentation` is the only assembly that may reference DOTween (spec 08 T8 asserts it).
- `ArcaneOnyx.TPCharacterController` is the `platinio/Unity-CharacterController` submodule, not a copy; a
  change it needs is a commit there followed by a pointer bump here.
- **Presentation events go through Hermes; gameplay calls do not.** The "events for presentation"
  in specs 01 (meter), 03 (active device), 05 (hits), 06 (roster, token, telegraph) are raised through
  an `ICombatEvents` interface declared in `ArkhamCombat.Combat`. Tests inject a fake. The one real
  implementation is a Hermes adapter in `ArkhamCombat.Shell`, so the camera, HUD, audio and rumble
  listen to generated typed events without referencing a gameplay assembly, and the
  `HERMES_EVENTS_GENERATED` define never appears outside Shell. `BeginPair`, `Receive`,
  `RequestInterrupt` and the token request stay direct calls: they need determinism and EditMode tests,
  not a scene-scoped dispatcher.
- World-space debug marks (picker lines, ring slots, warp destinations, telegraph rings, hit range at
  window open) go through MGizmos. The spec 01 frame-data overlay is screen-space and stays IMGUI/uGUI.
- **Zenject wires the edges; the core never sees it.** Scene-wide services are bound by
  ScriptableObject static installers, one per assembly that owns services: the hit pipeline and the
  resolver and picker settings (Combat), the encounter director host (Encounter), the cue registry
  (Presentation), and the `ICombatEvents` Hermes adapter (Shell). Hermes's own static installer binds
  the scene events. Per-instance graphs, the action runner with its driver and sink, the player's
  states, an enemy's sensors, live on a `GameObjectContext` on the player and enemy prefabs, and the
  spawner instantiates enemies through the container so those contexts resolve. `ArkhamCombat.Combat`
  classes take collaborators through constructors and carry no `[Inject]` and no Zenject reference; the
  spec 08 boundary test asserts that alongside the DOTween rule. `[Inject]` is allowed only on
  MonoBehaviours in Player, Enemies, Presentation and Shell.
- **Implementations are chosen in the inspector, not in code.** Where a static installer binds an
  interface with more than one plausible implementation, the installer asset holds a
  `[SerializeReference, SubclassSelector]` field of that interface type and binds whatever the designer
  picked from the dropdown: the picker's scoring, the token grant policy, the variant pool policy, the
  combat events sink (Hermes adapter or a logging stub). Swapping an implementation is an inspector
  change on the installer asset, no recompile. SerializeReferenceExtensions provides the dropdown; the
  same attribute pair is what makes spec 08's cue kinds and spec 01's pool policies polymorphic lists.

### Packages to add

| Package | Why |
|---|---|
| `com.unity.cinemachine` | Camera rig (bh3-development uses 3.1.7) |
| `com.unity.pipeline` | Required by BH3's editor commands behind the `bt_*` tools; same source as bh3-development, to be confirmed |
| DOTween | Body-child cues and HUD only (spec 08) |

Already present: Input System, AI Navigation, Visual Scripting, Test Framework, URP.

Scripting defines, set in Player Settings for every target, the way bh3-development does it:
`MODULE_BH3_EXIST`, `MODULE_SCRIPTABLE_OBJECT_DATABASE_EXIST`, `MODULE_ZENJECT_EXIST`. The last one
switches on Hermes's Zenject installer and every other module's optional Zenject assembly. The module
tooling does not write these; they are part of the project settings commit.

Compile notes for the first import, checked against the added submodules: BH3's TPS assemblies and
Hermes's Zenject assembly are define-constrained on their optional modules and stay out on their own.
BH3's EditMode test assembly references `ArcaneOnyx.BehaviorTree.Editor.Commands`, which only exists
once `com.unity.pipeline` is installed, so add that package before expecting BH3's own tests to
compile. Every other test assembly in the set is constrained on `UNITY_INCLUDE_TESTS` and references
only what is present.

## Build order

Each phase ends with its tests green. Phases are ordered so that every PlayMode test has the pieces it
needs when it is written.

| Phase | Work | From specs |
|---|---|---|
| 0 | Repository setup: submodules, packages, defines, copy the controller, a `SceneContext` with the `StaticInstaller` and its manifest, Hermes scene scope and the `ICombatEvents` adapter skeleton, write the boundary tests, the exploration camera moves on pad and keyboard | 00, 03 T1, 08 T8 |
| 1 | The core with no scene: `ActionDefinition` with duration and cues, `IntentBuffer`, `ComboMeter`, `ComboResolver`, `ReactionRules`, `HitResolver`, `FreeFlowTargetPicker`, `GroupFraming`, `EncounterDirector` baseline tests | 01 T1–T5, 05 T1–T4b, 04 T1–T2, 07 T2, 06 T1–T2 |
| 2 | Presentation driver and bodies, then the `ActionRunner` on top; the frame-data overlay | 08 T1–T5, T9; 01 T6–T8 |
| 3 | Player: intents in the controller, interrupts, sinks, combat states, Ground stance fixture against a dummy | 03 T2–T7, T9; 01 T9–T11 |
| 4 | Enemies: encounter module extensions, sensors, the two nodes, sub-trees, Functions, roots, prefabs, arena test scene with a stand-in player | 06 T3–T8; 02 T2–T9 |
| 5 | Integration: real player versus real enemies, paired actions, combat camera, telegraph prompts | 05 T5–T8; 08 T6–T7; 07 T3–T9; 04 T3–T5 |
| 6 | Shell and soaks: spawner, HUD, reset loop, feedback listener, every spec's soak test | 09; 01 T12, 02 T10, 03 T10, 06 T10 |

Phase 1 is deliberately large and deliberately scene-free: it is where most of the design is proven or
disproven, and it costs nothing to change there.

## Conventions

- **Tests.** EditMode for anything pure, PlayMode only where the motor, the NavMesh or real time is
  involved. Every spec's task table says which. Soak tests check correctness only: no exceptions, no
  stuck states, every branch visited. Performance is not measured; this is a small showcase.
- **Facts.** Every fact a tree or guard reads is written through `AgentVariableWriter`. Key names are
  the ones in spec 02's contract table and spec 01's context table; new keys are added to those tables
  first.
- **Trees.** Authored in the Editor through the `bt_*` tools, verified with `bt_verify` before review,
  described with `bt_describe_tree` in the review. A deliberate finding gets a sticky note on the canvas.
- **Specs.** A change in behaviour changes the spec in the same commit. A decision that changes moves
  from a spec's open decisions into the locked table above.

## Still open

1. **Commit conventions for this repository.** bh3-development forbids any machine attribution in
   commits. Nothing says so here yet; decide before the first commit.
2. **Spec 09, the demo shell.** Arena and NavMesh, spawner registering enemies with the director in
   waves, HUD with combo counter and device-aware counter and evade prompts, player health, reset loop,
   and the presentation listener for hit-stop, impulse, sounds and rumble.
3. **`com.unity.pipeline` source.** Confirm where bh3-development pulls it from so the `bt_*` tools work
   in this project.

## Non-goals for the demo

- No traversal, gadgets, stealth or predator rooms. Combat only.
- No perception, hiding or fleeing. Everyone fights from spawn.
- No ranged enemies, bosses or multi-encounter flow.
- No authored animation, skeletons or ragdolls.
- No save, settings, rebinding or menus beyond a reset.
