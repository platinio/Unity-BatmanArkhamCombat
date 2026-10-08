# ArkhamCombat

The free-flow combat demo's own code: a Batman Arkham style chain where one button means "strike",
context picks the move and the target, the character warps into every hit, and a combo meter counts.
Built on capsules and tweens for now, with every decision made in normalized time so real characters
and clips plug in underneath later. The design specs are the source for every rule here; this file
explains how the code carries them out and what each class is for.

## The idea in one paragraph

A press is not a command, it is an **intent** that waits in a buffer for a short lifetime. An attack is
not a chain position, it is an **action asset** with a duration and windows in normalized time. The
chain is a separate **stance asset**: nodes that point at actions, edges that say which intent moves
where. Nothing reads input directly; the **runner** asks the **resolver** whether a queued intent takes
an edge, and only when the current action's combo window is open or nothing is playing. Interrupts
(counter, evade, stun) are global edges tried before follow-ups. Time comes from a **presentation
driver**, never from `Time.deltaTime` directly, so hit-stop and tests drive the same code. The meter
counts beside the chain with its own rules. Everything hits, moves or picks through small interfaces,
and the stand-ins behind them are what the later specs replace.

## One frame, from press to hit

```text
Input System callback          PlayerInputReader pushes Intent{Kind, PressedAt, MoveAtPress}
                               into IntentBuffer (controller submodule)
        │
CombatBrain.Update  (runs before CharacterBrain)
        │  ITargetPicker.Pick(position, stick or facing)  ──► ActionRunner.Target
        │  CombatContextPublisher.Publish  ──► CombatContext + agent variables (facts)
        │  ComboMeter.Tick                 ──► timeout reset
        │  ActionRunner.Tick(deltaTime, position, CanStartActionFromIdle())
        │       playing: IPresentationDriver.Tick  ──► ActionClock advances, cues fire
        │                windows open and close      ──► IHitWindowListener.HitWindowOpened/Closed, warp ──► IWarpMover.MoveBy
        │                ComboResolver.Resolve       ──► follow-up or interrupt ──► the next action starts
        │                clock reached 1             ──► the action ends, back to idle
        │       idle:    idle seconds ──► the combo goes back to the root after chainResetSeconds
        │                ComboResolver.Resolve from the kept node ──► the next action starts
        │  runner started from idle  ──► CharacterStateMachine.Change<AttackingState>()
        │
CharacterBrain.Update
        │  PlayerInputReader.Tick       samples Move, expires old intents
        │  CharacterStateMachine.Tick   ──► AttackingState.Tick
        │       MotorWarpMover.TakePendingMovement() / dt  ──► planar velocity
        │       face the target, SuppressGravity while the warp window is open
        │       CharacterMotor.Tick(MotionIntent)
        │       runner idle ──► Change<LocomotionState>()
        │
Hit     DemoHitWindowListener.HitWindowOpened: in range ──► ICombatTarget.Receive,
        ComboMeter.Increment; otherwise ComboMeter.Reset(Whiff)
Events  ICombatEvents (logging stub today, Hermes adapter later) for the HUD, audio, camera
Debug   FrameDataOverlay draws ActionRunner.Trace: windows, cue marks, playhead, every press and its fate
```

Two rules make the order safe: the combat brain runs first so the state machine sees the frame's
warp movement, and the runner resolves from idle only while the character is in Locomotion or still in
Attacking after an action ended (recovery), never from Airborne or a reaction.

## Assemblies

| Assembly | Folder | Holds | References |
|---|---|---|---|
| `ArkhamCombat.Combat` | `Runtime/Combat` | The core: actions and windows, the chain, the resolver, the meter, the runner, the clock, the interfaces the runner talks through | The character controller (intent types), VisualScriptingExtension (`FunctionCall<bool>` on edges). Never DOTween, never Zenject; a test asserts both |
| `ArkhamCombat.Presentation` | `Runtime/Presentation` | The DOTween driver and the cue kinds | Combat, DOTween |
| `ArkhamCombat.Player` | `Runtime/Player` | The installer, the combat brain, the `Attacking` state, the fact publisher, the Function evaluator, and the stand-ins behind the core's interfaces | Combat, Presentation, BH3 (fact writer), Zenject |
| `ArkhamCombat.Shell` | `Runtime/Shell` | The frame-data overlay and the camera demo | Combat, Camera |
| `ArkhamCombat.Camera` | `Runtime/Camera` | Group framing math and the combat framing source | Character controller |
| `ArkhamCombat.Editor` | `Editor` | The node id dropdown and the fixture builder | |
| `ArkhamCombat.Tests` | `Tests/EditMode` | EditMode tests for the core and the stand-ins | |

The intent buffer lives in the character controller submodule (`ArcaneOnyx.TPCharacterController`,
`Runtime/Inputs`) because `ICharacterInput` exposes it and the submodule cannot reference this project.

## Who does what

### Input (controller submodule, `Runtime/Inputs`)

| Type | Responsibility |
|---|---|
| `IntentKind` | A kind of press as an asset, holding `secondsQueued`. What a press meant, not which button it was. The controller defines none; this game's four live in `Assets/ArkhamCombat/Intents`: `Strike`, `Counter`, `Evade`, `Stun`. Compared by asset |
| `Intent` | One press: kind, `PressedAt` on the buffer's clock, the move vector read at the press, lifetime (its kind's seconds queued). A reference type so "consume exactly this press" is trivially right |
| `PressBinding` | On `InputConfig`: a reference to an input action and the kind its press queues |
| `IntentBuffer` | Holds presses until consumed or expired. `Push`, `FindNewest(kind)`, `TryConsume(intent)`, `Tick(deltaTime)`, `ExpireAll`, the `Queued` list. Raises `Pushed`, `Consumed`, `Expired` for the trace. Decides nothing |
| `PlayerInputReader` | Pushes the binding's kind on each press binding's `performed`, stamping the move vector read at that instant; samples Move, Look and Sprint once per frame; ticks the buffer |

### Actions (`Runtime/Combat/Actions`)

| Type | Responsibility |
|---|---|
| `Window` | A span of normalized time. `IsOpen(previousTime, currentTime)` and `IsClosed(previousTime, currentTime)` are true on exactly one tick each, however coarse the frame rate, including zero-length windows and a looping action going from 1 back to 0 |
| `ActionDefinition` | One move as data: `Duration` (the clock), `Cues`, `Tags`. Knows nothing about chains or hits |
| `AttackDefinition` | Adds the four windows (`HitWindow`, `ComboWindow`, `EvadeWindow`, `WarpWindow`), `StrikeDistance`, `MaxLunge`, damage, reaction, `IsCounterable`, `IsUnblockable`. Owns the one lunge rule: `WarpDestination`, `LungeDistance`, `IsBeyondLunge`, read by both the warp and the `targetBeyondLunge` fact |
| `PresentationCue`, `ICueKind`, `ICueTarget` | A cue is a normalized time, a kind picked from a dropdown, and a few numbers. A kind plays itself on a target (body, fist, renderer, rest pose, tween id). The core never learns what a tween is |

### Chain (`Runtime/Combat/Chain`)

| Type | Responsibility |
|---|---|
| `Stance` | The asset: root id, nodes, global interrupt edges, `chainResetSeconds`. Indexes nodes by id and sorts edges into runtime lists on load (authored order is never rewritten). `Validate` reports a missing root, duplicate ids, a node with nothing to play, an empty pool, an edge without an intent kind, an edge to nowhere or to a node with nothing to play, and a node nothing reaches |
| `ChainNode` | A position: id, one attack or a `VariantPool`, edges in priority order. The root may play nothing |
| `Edge` | `intentKind` (an `IntentKind` asset), optional `FunctionCall<bool>` condition, priority, destination id. `[ChainNodeId]` gives the designer a dropdown |
| `VariantPool`, `IVariantPolicy` | A list of attacks and the rule to pick one: `NoRepeatPolicy`, `RandomPolicy`, `TargetSidePolicy` (left hand for a target on the left, falls back to no-repeat with no side known) |
| `CombatContext` | The facts a condition may read, filled each frame: combo count and tier, target distance, side, state, beyond-lunge, incoming counterable, stick angle. `Keys` names them for the agent variables |
| `InterruptKinds` | Which kinds are the evade and the counter, handed to the resolver from `CombatConfig` by the installer, because the core cannot reference the player assembly |
| `IConditionEvaluator` | Answers an edge's condition with `IsConditionMet`. Tests use a recording stand-in; the real one runs the Function |
| `ComboResolver` | Picks the edge a queued press takes, without side effects beyond consuming that press. Global edges first, each only when allowed (the evade kind needs the attack's evade window or an idle character; the counter kind needs an incoming attack that can be countered; any other kind has no gate), then the node's edges while the combo can continue. Consumes exactly one intent per match; an unmatched press stays queued. Reads a `ComboSituation` and returns a `Resolution`: destination, edge, consumed intent, whether it was an interrupt |

### Meter and events (`Runtime/Combat/Meter`, `Events`)

| Type | Responsibility |
|---|---|
| `ComboMeter` | Count and tier. Increments on a landed strike, counter or evade; resets to zero on a hit taken, a whiff or `meterTimeoutSeconds` without an increment. Silent at zero. Separate from the chain on purpose |
| `ICombatEvents` | Presentation events: combo changed and reset, action started and ended. Gameplay never listens here. `NullCombatEvents`, `LoggingCombatEvents`; the Hermes adapter is the real one later |

### Presentation clock (`Runtime/Combat/Presentation`)

| Type | Responsibility |
|---|---|
| `IPresentationDriver` | Where the runner reads time: `Play`, `Stop`, `IsPlaying`, `NormalizedTime`, `Speed`, `Tick`. A driver that shows nothing and one on DOTween exist; one on animation clips is designed in spec 10 |
| `ActionClock` | The duration clock with the cue schedule on it. Advances elapsed time by `deltaTime * Speed`, fires each cue once when its time is crossed, fires cues at zero on `Play` so a frozen clock fires nothing. Pure; drivers compose it |
| `NullPresentationDriver` | The clock alone. Tests and bodies without a picture |

### Runner (`Runtime/Combat/Runner`)

| Type | Responsibility |
|---|---|
| `ActionRunner` | Plays the actions. Owns the current node and action. Each tick advances the driver, opens and closes the windows (the hit window, the combo window, the warp), asks the resolver, starts follow-ups and interrupts, ends the action at its end, and keeps the combo's place for `chainResetSeconds` of idle time before going back to the root. A follow-up ends the current attack normally; only a global edge interrupts it. `Interrupt`, `PlayAction` and `Cancel` are the paths code uses: reactions and enemies. The warp moves the share of the remaining distance that matches the share of the warp window's remaining time the tick covers, re-aimed each tick, and is refused beyond `MaxLunge` (`WasWarpRefused`) |
| `ActionTrace` | What the overlay draws: the last eight actions and idle stretches with every press that arrived and whether it was consumed, expired or is still live |
| `IActionTarget` | Position and validity. All the runner needs to warp and to hit |
| `ICombatTarget` | A target that can also be read (`State`) and hit (`Receive`). Dummies now, enemy status components later |
| `ITargetRoster` | Who is in the fight. A scene scan now, the encounter director later |
| `ITargetPicker` | Picks the frame's target from the roster given the position and the preferred direction. A stand-in now, spec 04's scored pick later |
| `IWarpMover` | Moves the character during the warp. The runner never touches a transform |
| `IHitWindowListener` | The hit code, told when the hit window opens, with the attack and target, and when it closes, at its end or on any interrupt, so a hit window never stays open behind an interrupted attack |

### Presentation (`Runtime/Presentation`)

| Type | Responsibility |
|---|---|
| `ProceduralPresentationDriver` | The demo's driver: the `ActionClock` plus DOTween cues on a body child. Every tween a cue starts carries the driver as its id, so `Play` and `Stop` kill them as a group and `Speed` zero pauses them. `Stop` blends the body back to its rest pose. Never moves the root |
| `LeanCue`, `PunchCue`, `SquashCue`, `SpinCue`, `FlashCue` | The cue kinds. One class each; a new kind is one file and appears in the dropdown on every action |

### Player (`Runtime/Player`)

| Type | Responsibility |
|---|---|
| `CombatConfig` | The asset: the stance, which kinds are the evade and the counter (`evadeKind`, `counterKind`), the meter settings, the facing turn time, and the tunables the stand-ins use |
| `CombatStaticInstaller` | Binds the combat graph scene-wide: config, stance, combat events (picked on the asset), context, meter, intent buffer (from the input reader), publisher, condition evaluator, driver (found in the hierarchy), warp mover and hit window listener, roster, picker, runner. Binds the `InterruptKinds` from the config. Validates the config's evade and counter kinds, the stance and every attack at scene load and logs each problem |
| `CombatBrain` | The per-frame orchestration described above, running before the character brain. Switches the state machine into `Attacking` when the runner starts |
| `AttackingState` | The character while an action plays: the warp mover's pending movement becomes the frame's planar velocity, the character turns toward the target, gravity is held during the warp. Hands back to Locomotion when the runner goes idle. A follow-up is not a state change |
| `MotorWarpMover` | Collects the warp movement for the frame; the attacking state takes it once |
| `CombatContextPublisher` | Fills `CombatContext` each frame and mirrors it onto the agent's variables through BH3's writer, writing only what changed, so Functions and Variable Watch see the same facts. Publishes the per-press stick angle right before a condition runs |
| `FunctionConditionEvaluator` | Runs an edge's Function against the player's agent. Empty condition is true; a Function that cannot run is reported once and treated as false. Checks every authored condition returns a bool at load |
| `StandInTargetPicker` | Nearest roster target roughly along the stick, or the facing when idle. Replaced by spec 04 |
| `SceneTargetRoster` | Every `ICombatTarget` component in the scene, read on first use. Replaced by the encounter director |
| `CombatDummy` | A thing to hit: a state string, a hit counter, a flash. Replaced by enemy status components |
| `DemoHitWindowListener` | A range check at the hit window's start: in range lands and feeds the meter, otherwise a whiff. Replaced by spec 05's hit pipeline |

### Shell, Camera, Editor

| Type | Responsibility |
|---|---|
| `FrameDataOverlay` | The spec 01 tuning strip, IMGUI: one bar per recent action with the warp (yellow), hit (red), combo (green) and evade (blue) bands, magenta cue marks, a white playhead, and a tick per press coloured by its fate (green spent, red expired, yellow queued) |
| `CombatCameraDemo`, `GroupFramingSource`, `GroupFraming` | The combat framing on the camera rig: pivot drifts toward the enemy centroid, distance follows the spread. Pure math in `GroupFraming` |
| `ChainNodeIdDrawer` | The dropdown of node ids on edge destinations and the stance root |
| `GroundStanceFixtureBuilder` | **ArkhamCombat → Build Ground Stance Fixture**: the four intent kinds (existing ones keep their seconds queued), four attacks with windows and cues, the Ground stance, the combat config, rebuilt in place |

## Authoring

- **An attack**: Create → ArkhamCombat → Attack. Set the duration, drag the four windows, the strike
  distance and the lunge limit, and add cues from the dropdown.
- **A stance**: Create → ArkhamCombat → Stance. Add nodes with ids, give each an attack or a pool with a
  policy, add edges with an intent kind, a priority and a destination from the dropdown. Global edges are the
  interrupts. A condition is a Function returning bool; empty means always.
- **A kind of press**: Create → ArcaneOnyx → TP Character Controller → Intent Kind, set its seconds
  queued, add an action for it to `Settings/ArkhamControls.inputactions` and a press binding to
  `Settings/ArkhamInputConfig.asset` that references the action and the kind.
- **Wiring**: `CombatConfig` names the stance and the evade and counter kinds; the `CombatStaticInstaller` asset in
  `Assets/Installers/Static` points at the config and picks the combat events. The player installer
  points at `Settings/ArkhamCharacterProfile.asset`, the game's own profile: the controller's motor,
  locomotion and camera configs with the game's `ArkhamInputConfig`. Its state list must include
  `AttackingStateBinding`.
- **A prefab**: a `ProceduralPresentationDriver` with the body child, an optional fist and the body
  renderer, plus `CombatBrain` next to `CharacterBrain`. Dummies are any object with `CombatDummy`.

## Extending

| Want | Do |
|---|---|
| A new cue kind | One `[Serializable]` class implementing `ICueKind` in Presentation; it appears in the dropdown |
| A new pool policy | One class implementing `IVariantPolicy`; same dropdown rule |
| Real target selection | Implement `ITargetPicker`, bind it in the installer |
| The encounter roster | Implement `ITargetRoster`, bind it |
| Enemies as targets | Their status component implements `ICombatTarget` |
| The hit pipeline | Implement `IHitWindowListener`, bind it |
| Real animation | A second `IPresentationDriver` that sets clip time from the clock; see spec 10 |
| A new combat state | A state class and a three-line `ICharacterStateBinding`, picked in the player installer |

## Running it

Open `Assets/CombatArena.unity` and press Play. Left mouse or the gamepad's west button strikes; the
chain is Jab (left or right by target side), Cross, Roundhouse, then back to the jab. Stand near a
dummy: beyond the lunge limit the warp refuses and the strike whiffs. The overlay in the top left shows
why anything happened. If the Editor window loses focus play mode stops advancing unless Run In
Background is on.

## Tests

`ArkhamCombat.Tests` runs in EditMode and needs no scene: windows, actions, stance validation, pool
policies, the meter, the resolver, the clock, the runner with the null driver and recording test
doubles, the stand-in picker on test targets, and the assembly boundary (no DOTween or Zenject in the core). The
intent buffer's tests live in the controller submodule.
