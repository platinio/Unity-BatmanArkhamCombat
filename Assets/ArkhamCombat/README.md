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
CharacterBrain.Update  (controller submodule)
        │  PlayerInputReader.Tick       samples Move, expires old intents
        │
        │  each CharacterComponent in the CharacterBrain's list, top to bottom:
        │       CombatFactsUpdater.Tick  ──► CombatFacts + agent variables, measured against
        │                                    IActionTargetPicker.PickTarget(the stick now): the next press's target
        │       ComboTracker.Tick        ──► ComboMeter.Tick: the combo is lost after the timeout
        │       CombatActions.Tick       ──► ActionRunner.Tick(deltaTime, position, IActionStartGate.CanStartFromIdle)
        │            playing: IPresentationDriver.Tick  ──► ActionClock advances, cues fire
        │                     windows open and close      ──► IHitWindowListener.HitWindowOpened/Closed, warp ──► IWarpMover.MoveBy
        │                     ComboResolver.Resolve       ──► follow-up or interrupt ──► the next action starts
        │                     clock reached 1             ──► the action ends, back to idle
        │            idle:    idle seconds ──► the combo goes back to the root after chainResetSeconds
        │                     ComboResolver.Resolve from the kept node ──► the next action starts
        │            an action starts: IActionTargetPicker.PickTarget(the press's MoveAtPress, zero from code)
        │                     ──► CombatTargeting ──► ITargetScorer.BestTarget(position, world direction)
        │                     ──► ActionRunner.CurrentActionTarget, kept until that action ends
        │       AttackingStateSwitch.Tick ──► an action plays ──► CharacterStateMachine.Change<AttackingState>()
        │
        │  CharacterStateMachine.Tick   ──► AttackingState.Tick
        │       CombatActions.TakePendingWarpMovement() / dt  ──► planar velocity
        │       face the target, SuppressGravity while the warp window is open
        │       CharacterMotor.Tick(MotionIntent)
        │       runner idle ──► Change<LocomotionState>()
        │
Hit     TargetedHitWindowListener.HitWindowOpened: HitResolver.TryLand (in range and in front)
        ──► ICombatTarget.Receive(HitInfo) ──► HitReceiver: damage, ReactionRules ──► HitResult
        landed ──► raise StrikeLanded; out of reach, or a target that ignores the hit ──► raise StrikeWhiffed
Combo   ComboTracker hears every strike and keeps its own character's: ComboMeter.Increment or
        ComboMeter.Reset(Whiff), then raises ComboChanged / ComboReset with its character
Action  CombatActions hears its runner start or end an action and raises ActionStarted / ActionEnded
        with its character
Events  Hermes for actions, strikes and combos, each naming its character (see Events)
Debug   FrameDataOverlay draws the Trace of CombatActions.Runner: windows, cue marks, playhead, every press and its fate
```

Two rules make the order safe: the character components tick before the state machine, so the state
machine sees the frame's warp movement and the character is in Attacking in the same frame its action
starts, and the start gate (`AttackingStateSwitch`) lets the runner resolve from idle only while the
character is on the ground in Locomotion, or still in Attacking after an action ended (recovery), never
from Airborne or a reaction.

## One context per character

Each fighting character carries a Zenject `GameObjectContext` with a `CombatCharacterInstaller`, so its
combat is bound for that character alone and two characters never share a runner.

| Bound | Where | What |
|---|---|---|
| Per character | `CombatCharacterInstaller`, on the character | Its stance, its presses (`IntentBuffer`), its `ActionRunner`, its `CombatFacts`, condition evaluator, presentation driver, warp mover, hit window listener, target picker, start gate and `CharacterMotor` |
| Shared | `CombatStaticInstaller`, scene-wide | `IComboMeterSettings`, `ITargetingSettings`, `IFacingSettings`, `ICombatFactsSettings`, `IHitCheckSettings`, `InterruptKinds`, `ITargetRoster`, `ITargetScorer`; the `CombatConfig` asset itself stays on the installer |
| Shared for now | `PlayerStaticInstaller`, scene-wide | The character controller: profile, input reader, context, state machine and states; the player's `CharacterBrain`, `CharacterMotor` and `CombatActions`, found in the hierarchy |

The character's context sees everything the scene binds; the scene sees nothing the character binds.
So code built by the scene reaches a character's pieces **through that character's components**: the
player installer binds the player's `CombatActions` the way it binds the brain and the motor, found
once in the hierarchy, and `AttackingState` and the overlay take it by injection for the runner and
the warp movement; the overlay takes the optional `ComboTracker` from it. Nothing else searches the
scene for a character's pieces. Every component on the character and under it is injected by the
character's context, before any `Awake`.

## Assemblies

| Assembly | Folder | Holds | References |
|---|---|---|---|
| `ArkhamCombat.Combat` | `Runtime/Combat` | The core: actions and windows, the chain, the resolver, the meter, the runner, the clock, the hit rules, the interfaces the runner talks through | The character controller (intent types), VisualScriptingExtension (`FunctionCall<bool>` on edges). Never DOTween, never Zenject (a test asserts both), never Hermes: the generated event assemblies reference this one |
| `ArkhamCombat.Presentation` | `Runtime/Presentation` | The DOTween driver and the cue kinds | Combat, DOTween |
| `ArkhamCombat.Player` | `Runtime/Player` | The scene installer and the character installer, the character components that run combat each frame (facts updater, combo tracker, combat actions, attacking state switch), the `Attacking` state, the Function evaluator, and the stand-ins behind the core's interfaces | Combat, Presentation, BH3 (fact writer), Zenject, Hermes (runtime and the two generated assemblies) |
| `ArkhamCombat.Shell` | `Runtime/Shell` | The frame-data overlay and the camera demo | Combat, Camera, Player (the overlay reads the `CombatActions` and the `ComboTracker`) |
| `ArkhamCombat.Camera` | `Runtime/Camera` | Group framing math and the combat framing source | Character controller |
| `ArkhamCombat.Editor` | `Editor` | The node id dropdown and the fixture builder | |
| `ArkhamCombat.Tests` | `Tests/EditMode` | EditMode tests for the core and the stand-ins | |

The intent buffer lives in the character controller submodule (`ArcaneOnyx.TPCharacterController`,
`Runtime/Inputs`) because `ICharacterInput` exposes it and the submodule cannot reference this project.
The ordered list of per-frame jobs is the controller's too: `CharacterComponent` and the list on
`CharacterBrain` know nothing about combat.

## Who does what

### Character (controller submodule, `Runtime`)

| Type | Responsibility |
|---|---|
| `CharacterBrain` | Runs the character's frame: samples input, ticks its character components in the order of its list, then ticks the state machine. Adding it lists the character components already on the object. An empty entry is skipped; an empty entry or a component on another object is reported once at start |
| `CharacterComponent` | One per-frame job on a character: a `MonoBehaviour` with `Tick(deltaTime)`. The `CharacterBrain` ticks the ones in its list, in that order, so a character has exactly the jobs it was given. A job sees this frame's input and what the jobs above it wrote this frame |

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
| `VariantPool`, `IVariantPolicy` | A list of attacks and the rule to pick one: `NoRepeatPolicy`, `RandomPolicy`, `TargetSidePolicy` (left hand for a target on the left, falls back to no-repeat with the target dead ahead) |
| `CombatFacts` | The facts a condition may read, filled each frame: combo count and tier, target distance, side, state, beyond-lunge, incoming counterable, stick angle. `Keys` names them for the agent variables |
| `InterruptKinds` | Which kinds are the evade and the counter, handed to the resolver from `CombatConfig` by the scene installer, because the core cannot reference the player assembly |
| `IConditionEvaluator` | Answers an edge's condition with `IsConditionMet`. Tests use a recording stand-in; the real one runs the Function; `AlwaysConditionEvaluator` agrees to every edge and is what a stance without conditions gets |
| `ComboResolver` | Picks the edge a queued press takes, without side effects beyond consuming that press. Global edges first, each only when allowed (the evade kind needs the attack's evade window or an idle character; the counter kind needs an incoming attack that can be countered; any other kind has no gate), then the node's edges while the combo can continue. Consumes exactly one intent per match; an unmatched press stays queued. Reads a `ComboSituation` and returns a `Resolution`: destination, edge, consumed intent, whether it was an interrupt |

### Meter (`Runtime/Combat/Meter`)

| Type | Responsibility |
|---|---|
| `ComboMeter` | One character's count and tier. Increments on a landed strike, counter or evade; resets to zero on a hit taken, a whiff or `MeterTimeoutSeconds` without an increment. Silent at zero. Separate from the chain on purpose. Tells its owner through two plain C# events, `ComboChanged(count, tier)` and `ComboReset(reason)`, and knows nothing about Hermes |
| `IComboMeterSettings` | The two values a meter is tuned by: `TierThresholds` and `MeterTimeoutSeconds`. The core only declares it; `CombatConfig` provides it |

### Presentation clock (`Runtime/Combat/Presentation`)

| Type | Responsibility |
|---|---|
| `IPresentationDriver` | Where the runner reads time: `Play`, `Stop`, `IsPlaying`, `NormalizedTime`, `Speed`, `Tick`. A driver that shows nothing and one on DOTween exist; one on animation clips is designed in spec 10 |
| `ActionClock` | The duration clock with the cue schedule on it. Advances elapsed time by `deltaTime * Speed`, fires each cue once when its time is crossed, fires cues at zero on `Play` so a frozen clock fires nothing. Pure; drivers compose it |
| `NullPresentationDriver` | The clock alone. Tests and bodies without a picture |

### Runner (`Runtime/Combat/Runner`)

| Type | Responsibility |
|---|---|
| `ActionRunner` | Plays the actions. Owns the current node, action and `CurrentActionTarget`. Each action asks the `IActionTargetPicker` once as it starts, with the stick at the press (zero for `Interrupt` and `PlayAction`), and keeps that target until it ends, so every attack of a combo picks again but none switches target halfway. Without a picker an action has no target and plays in place. Each tick advances the driver, opens and closes the windows (the hit window, the combo window, the warp), asks the resolver, starts follow-ups and interrupts, ends the action at its end, and keeps the combo's place for `chainResetSeconds` of idle time before going back to the root. A follow-up ends the current attack normally; only a global edge interrupts it. `Interrupt`, `PlayAction` and `Cancel` are the paths code uses: reactions and enemies. The warp moves the share of the remaining distance that matches the share of the warp window's remaining time the tick covers, re-aimed each tick, and is refused beyond `MaxLunge` (`WasWarpRefused`). Tells its owner through two plain C# events, `ActionStarted(action, isInterrupt)` and `ActionEnded(action, wasInterrupted)`, and knows nothing about Hermes |
| `ActionTrace` | What the overlay draws: the last eight actions and idle stretches with every press that arrived and whether it was consumed, expired or is still live |
| `IActionTarget` | Position and validity. All the runner needs to warp and to hit |
| `ICombatTarget` | A target that can also be read (`State`) and hit: `Receive(HitInfo)` is where the target decides what the hit does to it, and its `HitResult` says whether the hit landed at all. Dummies now, enemy status components later |
| `ITargetRoster` | Who is in the fight. A scene scan now, the encounter director later |
| `IActionTargetPicker` | `PickTarget(direction)`: hands a target to whoever asks. The direction is in the character's movement frame (the stick, or wherever an agent aims); zero means straight ahead. Knows nothing about combos, presses or callers. `NullActionTargetPicker` never finds one |
| `ITargetScorer` | `BestTarget(position, direction)` in world space: the best target in the roster for a character there, facing that way. A stand-in now, spec 04's scoring later |
| `IActionStartGate` | `CanStartFromIdle`: whether a character playing nothing may start an action from a press right now. The runner does not ask it; whoever ticks the runner passes the answer in. `AlwaysOpenActionStartGate` never holds a press back |
| `IWarpMover` | Moves the character during the warp. The runner never touches a transform |
| `IHitWindowListener` | The hit code, told when the hit window opens, with the attack and target, and when it closes, at its end or on any interrupt, so a hit window never stays open behind an interrupted attack |

### Hits (`Runtime/Combat/Hits`)

| Type | Responsibility |
|---|---|
| `HitResolver` | The land check. Hits are targeted: a strike lands on the one target its action picked when that target stands within the attack's `StrikeDistance` plus `HitRangeMargin`, and within `HitAngle` degrees either side of the attacker's facing, both measured on the ground. Nothing is swept and no collider is asked. `TryLand` answers and hands back the `HitInfo`. An attacker standing on its target hits whichever way it faces |
| `StrikeOrigin` | Where a strike comes from and which way its attacker faces. The caller supplies both, so the check never reads a transform |
| `HitInfo` | A strike that passed the land check, as its target receives it: the attack, and the flat direction from the attacker to the target |
| `HitReceiver` | One character's health and what each hit does to it: takes the attack's damage, asks `ReactionRules` and returns the `HitResult`. The dead take nothing |
| `ReactionRules` | The attacker asks for a reaction and the receiver's profile decides the one that plays. No health left is death, whatever was asked. Armor turns a flinch into nothing and a stagger into a flinch, and damage still applies. A knockdown knocks down, through armor too, unless the profile cannot be knocked down, and then it staggers |
| `HitResult`, `AppliedReaction` | Whether the hit landed and what the target does: nothing, flinch, stagger, knockdown or death. `HitReaction` on the attack is what was asked for; this is what happened |
| `HitReceiverProfile` | The asset a character that can be hit points at: `maxHealth`, `isArmored`, `canBeKnockedDown` |
| `IHitCheckSettings` | The two values the land check is tuned by: `HitRangeMargin` and `HitAngle`. The core only declares it; `CombatConfig` provides it |

### Presentation (`Runtime/Presentation`)

| Type | Responsibility |
|---|---|
| `ProceduralPresentationDriver` | The demo's driver: the `ActionClock` plus DOTween cues on a body child. Every tween a cue starts carries the driver as its id, so `Play` and `Stop` kill them as a group and `Speed` zero pauses them. `Stop` blends the body back to its rest pose. Never moves the root |
| `LeanCue`, `PunchCue`, `SquashCue`, `SpinCue`, `FlashCue` | The cue kinds. One class each; a new kind is one file and appears in the dropdown on every action |

### Player (`Runtime/Player`)

| Type | Responsibility |
|---|---|
| `CombatConfig` | The asset every character shares: which kinds are the evade and the counter (`evadeKind`, `counterKind`), the combo meter's tier thresholds and timeout (`comboTierThresholds`, `comboTimeoutSeconds`, handed out as `IComboMeterSettings`), the facing turn time (`faceTargetSmoothTime`, as `IFacingSettings`), the angle that still counts as dead ahead for the `targetSide` fact (`deadAheadAngle`, with `maxLungeWhileIdle` as `ICombatFactsSettings`), how far past its strike distance and how far off the attacker's facing a strike still lands (`hitRangeMargin`, `hitAngle`, as `IHitCheckSettings`), and the tunables the stand-ins use, among them how much aim outweighs distance when a target is picked (`angleCountingAsDoubleDistance`). The stance is not here but on each character's `CombatCharacterInstaller` |
| `CombatStaticInstaller` | Binds scene-wide what every character shares: the config, the config again as `IComboMeterSettings`, the `InterruptKinds` from the config, the roster and the target scorer. Validates the config's evade and counter kinds at scene load and logs each problem |
| `CombatCharacterInstaller` | A `MonoInstaller` on the character, listed in its `GameObjectContext`, that binds one character's combat: its stance (set on the installer), its presses (the intent buffer of its input), its own `CharacterMotor`, the warp mover and the hit window listener, and its `ActionRunner`. The optional pieces are bound when their component is on the character and replaced by one that does nothing when it is not: the target picker (`CombatTargeting`, otherwise the runner and the facts have no target), the `CombatFactsUpdater` and its facts (otherwise an empty set, so the counter rule sees false and the target-side pool falls back), the presentation driver on the character or under it (otherwise the clock alone), the start gate (`AttackingStateSwitch`, otherwise a press may always start an action). The `FunctionConditionEvaluator` is bound only when the stance has an edge with a condition, so a character without conditions never touches Visual Scripting. Validates the stance and every attack as the character's context is built and logs each problem |
| `CombatActions` | A character component that plays the character's actions: its tick runs the character's `ActionRunner` from the character's position, with the start gate's answer. It is also how code outside the character's context reaches the runner (`Runner`) and the frame's warp movement (`TakePendingWarpMovement`). Goes after the facts updater and the combo tracker in the `CharacterBrain`'s list, so the runner resolves against this frame's facts. Listens to its runner from `Start` to `OnDestroy` and raises `ActionStarted` and `ActionEnded` with its own object whenever an action starts or ends. Without generated Hermes events the actions still play and nothing is announced; without the Hermes scene object it logs an error |
| `AttackingStateSwitch` | A character component that keeps the state machine and the actions in step, and the character's `IActionStartGate`. Its tick puts the state machine into `Attacking` while an action plays, so it goes after `CombatActions` in the `CharacterBrain`'s list and the state machine ticks `Attacking` in the frame the action starts. `CanStartFromIdle` is true on the ground in Locomotion, or in Attacking (recovery, so a queued press continues the combo without a frame in Locomotion). A character without it never enters `Attacking` |
| `AttackingState` | The character while an action plays: the pending warp movement becomes the frame's planar velocity, the character turns toward the runner's `CurrentActionTarget`, gravity is held during the warp. Hands back to Locomotion when the runner goes idle. A follow-up is not a state change. Built by the scene with the player's `CombatActions` injected, which hands it the runner and the warp movement |
| `MotorWarpMover` | Collects the warp movement for the frame; the attacking state takes it once, through `CombatActions` |
| `CombatFactsUpdater` | A character component that owns its character's `CombatFacts` (`Facts`). Each tick measures the combo (read from the `ComboTracker` on the same object, zero without one), the target the next press would get (asks the target picker with the stick as it is now), its side and state, beyond-lunge and incoming counterable, and mirrors them onto the agent's variables through BH3's writer, writing only what changed, so Functions and Variable Watch see the same facts. Updates the per-press stick angle right before a condition runs. A character that needs no facts leaves it off |
| `ComboTracker` | A character component that owns its character's `ComboMeter`, built from the injected `IComboMeterSettings`, so every character's meter is tuned in `CombatConfig`. Listens to the Hermes `StrikeLanded` and `StrikeWhiffed` events from `Start` to `OnDestroy` and ignores every attacker but its own object: a landed strike increments, a whiff resets. Its tick runs the meter's timeout, so it goes after the facts updater in the `CharacterBrain`'s list. Raises `ComboChanged` and `ComboReset` with its own object whenever the meter changes. `Count` and `Tier` are what the facts updater and the overlay read. Without generated Hermes events it warns once and stays at zero; without the Hermes scene object it logs an error |
| `FunctionConditionEvaluator` | Runs an edge's Function against the character's agent. Empty condition is true; a Function that cannot run is reported once and treated as false. Checks every authored condition returns a bool when it is built. Without a facts updater on the character the stick angle is not measured. Bound only for a stance that has a condition |
| `CombatTargeting` | The character's `IActionTargetPicker`, a plain component (not a character component: it stores nothing and has nothing to tick). Turns the direction into world space with the injected movement frame, the same one the facts use, falls back to the facing when the stick is pushed less than `stickPushedMagnitude`, and asks the `ITargetScorer` |
| `StandInTargetScorer` | The `ITargetScorer` stand-in: the nearest roster target roughly along the direction, distance alone with no direction. Tuned through `ITargetingSettings`, which `CombatConfig` implements. Replaced by spec 04 |
| `SceneTargetRoster` | Every `ICombatTarget` component in the scene, read on first use. Replaced by the encounter director |
| `CombatDummy` | A thing to hit: a state string, a hit counter, a flash, and a `HitReceiver` built from the `HitReceiverProfile` set on it, so it loses health and dies. Dead, it lies down along the hit, switches its collider off and stops being a valid target, so it is never picked again; it stays dead until the scene reloads. Without a profile it logs an error and can be neither targeted nor hit. Replaced by enemy status components |
| `TargetedHitWindowListener` | The character's `IHitWindowListener`. As the hit window opens it asks the `HitResolver` whether the strike lands on the action's target, measured from where this tick's pending warp lands rather than from the transform, which the attacking state moves only after the components tick. A strike that lands is handed to the target, and `StrikeLanded` is raised when the target took it; a strike out of reach, at an invalid target, or at a target that ignores it (the dead) raises `StrikeWhiffed`. The attacker is the character's object, the target the target's. It does not know who counts combos. Tuned through `IHitCheckSettings` |

### Shell, Camera, Editor

| Type | Responsibility |
|---|---|
| `FrameDataOverlay` | The spec 01 tuning strip, IMGUI: one bar per recent action with the warp (yellow), hit (red), combo (green) and evade (blue) bands, magenta cue marks, a white playhead, and a tick per press coloured by its fate (green spent, red expired, yellow queued). Everything comes from the runner of the scene's `CombatActions`; without one it draws nothing. The header's combo is the scene's `ComboTracker`, zero without one |
| `CombatCameraDemo`, `GroupFramingSource`, `GroupFraming` | The combat framing on the camera rig: pivot drifts toward the enemy centroid, distance follows the spread. Pure math in `GroupFraming` |
| `ChainNodeIdDrawer` | The dropdown of node ids on edge destinations and the stance root |
| `GroundStanceFixtureBuilder` | **ArkhamCombat → Build Ground Stance Fixture**: the four intent kinds (existing ones keep their seconds queued), four attacks with windows and cues, the Ground stance, and the evade and counter kinds on the combat config, rebuilt in place. The stance keeps its GUID, so the character's installer in the scene keeps pointing at it |

## Events

Actions, strikes and combo changes travel as Hermes events, so any number of characters can fight in
one scene and anything can react without a reference to the code that raised them. Each of the six
events names the character it is about, and **a listener filters by character**.

| Event | Carries | Raised by | Listened to by |
|---|---|---|---|
| `ActionStarted` | `GameObject Character`, `ActionDefinition Action`, `bool IsInterrupt` | `CombatActions`, when its runner starts an action | Nothing yet |
| `ActionEnded` | `GameObject Character`, `ActionDefinition Action`, `bool WasInterrupted` | `CombatActions`, when its runner ends an action. The next attack of a combo ends the one before it without interrupting it, and that `ActionEnded` comes before the follow-up's `ActionStarted` | Nothing yet |
| `StrikeLanded` | `GameObject Attacker`, `AttackDefinition Attack`, `GameObject Target` | `TargetedHitWindowListener` | Each `ComboTracker`, for its own attacker |
| `StrikeWhiffed` | `GameObject Attacker`, `AttackDefinition Attack` | `TargetedHitWindowListener` | Each `ComboTracker`, for its own attacker |
| `ComboChanged` | `GameObject Character`, `int Count`, `int Tier` | `ComboTracker`, after every increment and after a reset | Nothing yet (the HUD later) |
| `ComboReset` | `GameObject Character`, `ComboResetReason Reason` | `ComboTracker`, just before the change to zero | Nothing yet |

The definitions are in `Assets/ArkhamCombat/Events/CombatGameEvents.asset`, edited in **Window → Arcane
Onyx → Hermes**; **Regenerate Events** there writes the code to `Assets/Hermes.Generated` and sets the
`HERMES_EVENTS_GENERATED` define. Every raise and listen in this project sits inside
`#if HERMES_EVENTS_GENERATED`, so the project still compiles without generated events; actions are
then not announced, and the combo does not count and each `ComboTracker` says so once. The scene needs
one object with `SceneGameEvents` and `GameEventDispatcher`; Hermes's installer binds it as
`ISceneGameEvents`.

The core never raises a Hermes event. `ActionRunner` and `ComboMeter` raise plain C# events, and the
component that owns them on the character (`CombatActions`, `ComboTracker`) forwards each one with its
own object.

The events carry `ActionDefinition`, `AttackDefinition` and `ComboResetReason`, so `ArkhamCombat.Combat` is listed under
**Extra Assembly References** in the Hermes settings (`Assets/Editor/HermesSettings.asset`), which makes
both generated assemblies reference it.

## Authoring

- **An attack**: Create → ArkhamCombat → Attack. Set the duration, drag the four windows, the strike
  distance and the lunge limit, and add cues from the dropdown.
- **A stance**: Create → ArkhamCombat → Stance. Add nodes with ids, give each an attack or a pool with a
  policy, add edges with an intent kind, a priority and a destination from the dropdown. Global edges are the
  interrupts. A condition is a Function returning bool; empty means always.
- **A kind of press**: Create → ArcaneOnyx → TP Character Controller → Intent Kind, set its seconds
  queued, add an action for it to `Settings/ArkhamControls.inputactions` and a press binding to
  `Settings/ArkhamInputConfig.asset` that references the action and the kind.
- **Wiring**: `CombatConfig` names the evade and counter kinds; the `CombatStaticInstaller` asset in
  `Assets/Installers/Static` points at the config. The stance is set on the
  character's `CombatCharacterInstaller` and the combo meter in `CombatConfig`. The player
  installer points at `Settings/ArkhamCharacterProfile.asset`, the game's own profile: the controller's
  motor, locomotion and camera configs with the game's `ArkhamInputConfig`. Its state list must include
  `AttackingStateBinding`.
- **A prefab**: next to `CharacterBrain`, a `GameObjectContext` whose Mono Installers list holds the
  `CombatCharacterInstaller` on the same object, with the stance set on the installer. Then the
  `CharacterBrain`'s list of character components reads, top to bottom: `CombatFactsUpdater`,
  `ComboTracker`, `CombatActions`, `AttackingStateSwitch`. `CombatTargeting` gives it targets, and a
  `ProceduralPresentationDriver` with the body child, an optional fist and the body renderer gives it a
  body. Leaving out the facts updater, the combo tracker, the targeting, the driver or the state switch
  gives a character without that piece; `CombatActions` is what plays actions at all. Dummies are any
  object with `CombatDummy` and a `HitReceiverProfile` set on it (`HitProfiles/Dummy.asset`). The scene also needs the Hermes object described under Events.

## Extending

| Want | Do |
|---|---|
| A new cue kind | One `[Serializable]` class implementing `ICueKind` in Presentation; it appears in the dropdown |
| A new pool policy | One class implementing `IVariantPolicy`; same dropdown rule |
| Real target selection | Implement `ITargetScorer`, bind it in `CombatStaticInstaller`; `CombatTargeting` keeps turning the stick into a world direction. A character that aims another way (an enemy agent) gets its own `IActionTargetPicker` component, which the character installer binds in place of `CombatTargeting` |
| The encounter roster | Implement `ITargetRoster`, bind it in `CombatStaticInstaller` |
| Enemies as targets | Their status component implements `ICombatTarget`, with a `HitReceiver` built from the enemy's own `HitReceiverProfile` |
| A tougher or armored target | A new `HitReceiverProfile` asset (**Create → ArkhamCombat → Hit Receiver Profile**), set on the target |
| Another way of landing hits (a swept volume) | Implement `IHitWindowListener`, bind it in `CombatCharacterInstaller` in place of `TargetedHitWindowListener` |
| Real animation | A second `IPresentationDriver` component that sets clip time from the clock; on the character or under it, the character installer binds it. See spec 10 |
| Another fighting character | Give it its own `GameObjectContext` and `CombatCharacterInstaller` with its stance, and only the character components it needs, in its `CharacterBrain`'s list |
| Another rule for when an action may start | A component on the character implementing `IActionStartGate`, in place of `AttackingStateSwitch` |
| Something new every character needs one of | Bind it in `CombatCharacterInstaller`; something they all share goes in `CombatStaticInstaller` |
| Scene-level code that needs a character's runner | Find the character's `CombatActions` and read `Runner`; the scene container cannot hand it over |
| A new combat state | A state class and a three-line `ICharacterStateBinding`, picked in the player installer |
| Reacting to an action starting or ending, a strike or a combo change | Listen to the Hermes event (`ActionStarted`, `ActionEnded`, `StrikeLanded`, `StrikeWhiffed`, `ComboChanged`, `ComboReset`) on the injected `ISceneGameEvents` from `Start` to `OnDestroy`, inside `#if HERMES_EVENTS_GENERATED`, and filter by character: ignore the ones you do not follow |
| A new per-frame job | A `CharacterComponent` on the character, added to the `CharacterBrain`'s list where it should tick |

## Running it

Open `Assets/CombatArena.unity` and press Play. Left mouse or the gamepad's west button strikes; the
chain is Jab (left or right by target side), Cross, Roundhouse, then back to the jab. Stand near a
dummy: beyond the lunge limit the warp refuses and the strike whiffs. Each strike takes its damage from the
dummy's health (ten hits with the fixture's attacks and `HitProfiles/Dummy.asset`); the hit that empties it
lays the dummy down, and the next strike picks another one. The overlay in the top left shows
why anything happened. If the Editor window loses focus play mode stops advancing unless Run In
Background is on.

## Tests

`ArkhamCombat.Tests` runs in EditMode and needs no scene: windows, actions, stance validation, pool
policies, the reaction table row by row, a receiver's health down to death and past it, the land check by range and by angle, the group framing math on plain vectors, the hit window listener against a target in and out of reach, behind, with a pending warp, and one that ignores the hit, with its announcements on a dispatcher built by the test, the dummy taking hits, dying, lying down and refusing hits without a profile, the targeting component's stick threshold and frame, the trace marking presses until it stops listening, one test that fails loudly when the Hermes events are not generated, the meter, the combo tracker on a dispatcher built by the test, the resolver, the clock, the runner with the null driver and recording test
doubles, its started and ended events, the stand-in picker on test targets, the character brain listing the character components already on a character, the combat actions with a start gate that
allows and refuses and their announcements on a dispatcher built by the test, the attacking state switch's rule, the character installer on containers built by the test (which
condition evaluator a stance gets, the optional pieces and their do-nothing versions, a runner for a character with
nothing optional), the player installer reporting an empty state list and an empty state slot, and the assembly boundary (no DOTween or Zenject in the core). The intent buffer's tests live in
the controller submodule, and so do the character brain skipping and reporting an empty entry of its list. Entering the `Attacking` state needs the whole controller and is checked in play mode.
