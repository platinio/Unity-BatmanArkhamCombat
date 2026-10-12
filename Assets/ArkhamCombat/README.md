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
| Per enemy | `EnemyInstaller`, on the enemy | Its archetype, also bound as the enemy's `IEncounterAgentSettings`, and the archetype's `HitReceiverProfile`, an `ActionRunner` with no stance and no presses, empty `CombatFacts`, the always-true condition evaluator, the presentation driver under it, the warp mover, a hit window listener that does nothing, and the target picker and start gate found on the enemy or the ones that do nothing. Its tree plays its actions |
| Shared | `CombatStaticInstaller`, scene-wide | `IComboMeterSettings`, `ITargetingSettings`, `IFacingSettings`, `ICombatFactsSettings`, `IHitCheckSettings`, `InterruptKinds`, `EncounterDirectorSettings`, `ITargetRoster`, `ITargetScorer`; the `CombatConfig` asset itself stays on the installer |
| Shared | `EncounterStaticInstaller`, scene-wide | The scene's one `EncounterDirector`, the `ISlotValidator` it checks slots with, and the `EncounterDirectorBehaviour` found in the hierarchy |
| Shared for now | `PlayerStaticInstaller`, scene-wide | The character controller: profile, input reader, context, state machine and states; the player's `CharacterBrain` and `CharacterMotor`, found in the hierarchy, and the `CombatActions` beside that brain: every fighting character has combat actions, so the first in the hierarchy may be an enemy's |

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
| `ArkhamCombat.Characters` | `Runtime/Characters` | What every fighting character has whoever drives it: `CombatActions`, which owns and ticks the character's runner, and `MotorWarpMover` | Combat, the character controller, Zenject, Hermes |
| `ArkhamCombat.Player` | `Runtime/Player` | The scene installer and the character installer, the player's character components (facts updater, combo tracker, attacking state switch), the `Attacking` state, the Function evaluator, the roster of who the player can strike, and the stand-ins behind the core's interfaces | Combat, Characters, Encounter, Presentation, BH3 (fact writer), Zenject, Hermes (runtime and the two generated assemblies) |
| `ArkhamCombat.Encounter` | `Runtime/Encounter` | The group fight: the director that hands out attack tokens and ring slots, the agent and host components, the slot validators, the scene installer, and the five behaviour tree nodes that ask the director | BH3, GraphCore, UnityExtensions, Zenject. Neither Player nor Enemies: a test asserts it |
| `ArkhamCombat.Enemies` | `Runtime/Enemies` | The enemy archetype and its database, the enemy installer, the components that publish an enemy's facts, and two C# behaviour tree nodes, `Play Action` and `Move To Strike Distance` | Combat, Characters, Encounter, BH3, GraphCore, the database module, Zenject. Never Player: a test asserts it |
| `ArkhamCombat.Shell` | `Runtime/Shell` | The frame-data overlay and the camera demo | Combat, Camera, Characters, Player (the overlay reads the `CombatActions` and the `ComboTracker`) |
| `ArkhamCombat.Camera` | `Runtime/Camera` | Group framing math and the combat framing source | Character controller |
| `ArkhamCombat.Editor` | `Editor` | The node id dropdown, and the database windows and inspectors for hit receiver profiles and enemy archetypes | |
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
| `ICombatTarget` | A target that can also be read (`State`) and hit: `Receive(HitInfo)` is where the target decides what the hit does to it, and its `HitResult` says whether the hit landed at all. An enemy's status component implements it |
| `ITargetRoster` | Who can be struck. The player's is `EncounterTargetRoster`: whoever is in the encounter |
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
| `HitReceiverProfile`, `HitReceiverProfileDatabase` | What a character that can be hit is made of: `maxHealth`, `isArmored`, `canBeKnockedDown`. A database item, picked through the database dropdown; the database asset is `HitProfiles/HitReceiverProfiles.asset`, edited in **Window → Arcane Onyx → Hit Receiver Profiles** |
| `IHitCheckSettings` | The two values the land check is tuned by: `HitRangeMargin` and `HitAngle`. The core only declares it; `CombatConfig` provides it |

### Presentation (`Runtime/Presentation`)

| Type | Responsibility |
|---|---|
| `ProceduralPresentationDriver` | The demo's driver: the `ActionClock` plus DOTween cues on a body child. Every tween a cue starts carries the driver as its id, so `Play` and `Stop` kill them as a group and `Speed` zero pauses them. `Stop` blends the body back to its rest pose, which is the standing pose it remembered at `Awake` (a `BodyPose`: position, rotation and scale) until a cue moves it with `RestAt`. Never moves the root |
| `LeanCue`, `PunchCue`, `SquashCue`, `SpinCue`, `FlashCue` | The cue kinds. One class each; a new kind is one file and appears in the dropdown on every action |
| `FallCue`, `RiseCue`, `CrumpleCue` | The reactions' cues. Fall lays the body down by its strength in degrees, backward when positive, and makes lying the rest pose, so a knocked-down or dead body stays down when its action ends. The body is lowered by what it is taller than deep, scaled by how far it turned, measured on the standing body's mesh and never on the pose it was hit in. Rise stands it up and makes standing the rest pose again. A held pose carries the standing scale, so a body that fell while crumpled is not left squashed. Crumple squashes and tilts and is released by `Stop` like any other cue |

### Characters (`Runtime/Characters`)

| Type | Responsibility |
|---|---|
| `CombatActions` | A character component that plays the character's actions: its tick runs the character's `ActionRunner` from the character's position, with the start gate's answer. It is also how code outside the character's context reaches the runner (`Runner`) and the frame's warp movement (`TakePendingWarpMovement`). On the player it goes after the facts updater and the combo tracker in the `CharacterBrain`'s list, so the runner resolves against this frame's facts; on an enemy, which has no brain, the `CharacterComponentUpdater` on the prefab ticks it from its own list. Listens to its runner from `Start` to `OnDestroy` and raises `ActionStarted` and `ActionEnded` with its own object whenever an action starts or ends. Without generated Hermes events the actions still play and nothing is announced; without the Hermes scene object it logs an error |
| `MotorWarpMover` | Collects the warp movement for the frame; the attacking state takes it once, through `CombatActions` |

### Player (`Runtime/Player`)

| Type | Responsibility |
|---|---|
| `CombatConfig` | The asset every character shares: which kinds are the evade and the counter (`evadeKind`, `counterKind`), the combo meter's tier thresholds and timeout (`comboTierThresholds`, `comboTimeoutSeconds`, handed out as `IComboMeterSettings`), the facing turn time (`faceTargetSmoothTime`, as `IFacingSettings`), the angle that still counts as dead ahead for the `targetSide` fact (`deadAheadAngle`, with `maxLungeWhileIdle` as `ICombatFactsSettings`), how far past its strike distance and how far off the attacker's facing a strike still lands (`hitRangeMargin`, `hitAngle`, as `IHitCheckSettings`), the tunables the stand-ins use, among them how much aim outweighs distance when a target is picked (`angleCountingAsDoubleDistance`), and the encounter director's settings (`encounter`, an `EncounterDirectorSettings`: tokens, ring and avoidance priorities). The stance is not here but on each character's `CombatCharacterInstaller` |
| `CombatStaticInstaller` | Binds scene-wide what every character shares: the config, the config again as `IComboMeterSettings`, the `InterruptKinds` from the config, the encounter director's settings from the config, the roster and the target scorer. Validates the config's evade and counter kinds at scene load and logs each problem |
| `CombatCharacterInstaller` | A `MonoInstaller` on the character, listed in its `GameObjectContext`, that binds one character's combat: its stance (set on the installer), its presses (the intent buffer of its input), its own `CharacterMotor`, the warp mover and the hit window listener, and its `ActionRunner`. The optional pieces are bound when their component is on the character and replaced by one that does nothing when it is not: the target picker (`CombatTargeting`, otherwise the runner and the facts have no target), the `CombatFactsUpdater` and its facts (otherwise an empty set, so the counter rule sees false and the target-side pool falls back), the presentation driver on the character or under it (otherwise the clock alone), the start gate (`AttackingStateSwitch`, otherwise a press may always start an action). The `FunctionConditionEvaluator` is bound only when the stance has an edge with a condition, so a character without conditions never touches Visual Scripting. Validates the stance and every attack as the character's context is built and logs each problem |
| `AttackingStateSwitch` | A character component that keeps the state machine and the actions in step, and the character's `IActionStartGate`. Its tick puts the state machine into `Attacking` while an action plays, so it goes after `CombatActions` in the `CharacterBrain`'s list and the state machine ticks `Attacking` in the frame the action starts. `CanStartFromIdle` is true on the ground in Locomotion, or in Attacking (recovery, so a queued press continues the combo without a frame in Locomotion). A character without it never enters `Attacking` |
| `AttackingState` | The character while an action plays: the pending warp movement becomes the frame's planar velocity, the character turns toward the runner's `CurrentActionTarget`, gravity is held during the warp. Hands back to Locomotion when the runner goes idle. A follow-up is not a state change. Built by the scene with the player's `CombatActions` injected, which hands it the runner and the warp movement |
| `CombatFactsUpdater` | A character component that owns its character's `CombatFacts` (`Facts`). Each tick measures the combo (read from the `ComboTracker` on the same object, zero without one), the target the next press would get (asks the target picker with the stick as it is now), its side and state, beyond-lunge and incoming counterable, and mirrors them onto the agent's variables through BH3's writer, writing only what changed, so Functions and Variable Watch see the same facts. Updates the per-press stick angle right before a condition runs. A character that needs no facts leaves it off |
| `ComboTracker` | A character component that owns its character's `ComboMeter`, built from the injected `IComboMeterSettings`, so every character's meter is tuned in `CombatConfig`. Listens to the Hermes `StrikeLanded` and `StrikeWhiffed` events from `Start` to `OnDestroy` and ignores every attacker but its own object: a landed strike increments, a whiff resets. Its tick runs the meter's timeout, so it goes after the facts updater in the `CharacterBrain`'s list. Raises `ComboChanged` and `ComboReset` with its own object whenever the meter changes. `Count` and `Tier` are what the facts updater and the overlay read. Without generated Hermes events it warns once and stays at zero; without the Hermes scene object it logs an error |
| `FunctionConditionEvaluator` | Runs an edge's Function against the character's agent. Empty condition is true; a Function that cannot run is reported once and treated as false. Checks every authored condition returns a bool when it is built. Without a facts updater on the character the stick angle is not measured. Bound only for a stance that has a condition |
| `CombatTargeting` | The character's `IActionTargetPicker`, a plain component (not a character component: it stores nothing and has nothing to tick). Turns the direction into world space with the injected movement frame, the same one the facts use, falls back to the facing when the stick is pushed less than `stickPushedMagnitude`, and asks the `ITargetScorer` |
| `StandInTargetScorer` | The `ITargetScorer` stand-in: the nearest roster target roughly along the direction, distance alone with no direction. Tuned through `ITargetingSettings`, which `CombatConfig` implements. Replaced by spec 04 |
| `EncounterTargetRoster` | The player's `ITargetRoster`: the `ICombatTarget` on each agent in the encounter, in the order they joined, kept current from the director's `AgentRegistered` and `AgentUnregistered`. An agent with nothing on it to hit is not listed. A thug that has died leaves the encounter from its Death branch, and in the frame before that the scorer already skips it as no longer valid |
| `TargetedHitWindowListener` | The character's `IHitWindowListener`. As the hit window opens it asks the `HitResolver` whether the strike lands on the action's target, measured from where this tick's pending warp lands rather than from the transform, which the attacking state moves only after the components tick. A strike that lands is handed to the target, and `StrikeLanded` is raised when the target took it; a strike out of reach, at an invalid target, or at a target that ignores it (the dead) raises `StrikeWhiffed`. The attacker is the character's object, the target the target's. It does not know who counts combos. Tuned through `IHitCheckSettings` |

### Encounter (`Runtime/Encounter`)

Trees ask the director and never command it: a node requests a token or reads a slot, and what the
answer is stays the director's decision.

| Type | Responsibility |
|---|---|
| `EncounterDirector` | A plain class that decides who may attack and where everyone else stands, ticked with `Tick(deltaTime)` so tests step it exactly. **Tokens**: an agent registers with a token kind; `TryAcquireToken` grants one while that kind's pool has one free and the agent is not cooling down, and keeps answering true to the holder; a token held for `maxTokenHoldSeconds` is taken back, and a released one cannot be asked for again for `reacquireCooldownSeconds`. **Slots**: a ring of `slotCount` positions `slotRadius` from the target. Every `slotReassignIntervalSeconds`, and on the tick after anyone joins or leaves, each slot is validated and the agents closest to the target pick their nearest free slot first, which keeps the front rank still. An agent with no slot left holds its bearing at the ring's radius. A slot keeps the place its validation gave it until the next assignment and moves with the target. **Roster**: `Agents` in the order they joined, `IsActive` while anyone is in. Raises plain C# events, `AgentRegistered`, `AgentUnregistered`, `TokenGranted` and `TokenReleased`; a holder that leaves releases its token before it is gone |
| `EncounterDirectorSettings` | The director's tuning, a section of `CombatConfig`: tokens per kind, hold time and cooldown, slot count, radius and reassign interval, how far a slot may be moved to reach walkable ground (`slotSampleRadius`), and the two avoidance priorities |
| `AttackTokenType` | Melee or Ranged. Each kind has its own pool, so a thrown attack does not use up the turn of the enemy closing in |
| `IEncounterAgent` | `Position`: all the director knows about a participant, so tests drive it with plain objects |
| `IEncounterAgentSettings` | `TokenType`: whether an agent waits for a melee token or a ranged one. An enemy's archetype implements it |
| `ISlotValidator`, `EverySlotIsValid`, `NavMeshSlotValidator` | Whether a slot can be stood on, and exactly where. The NavMesh one moves a slot to the nearest walkable point within `slotSampleRadius` and rejects it when there is none; a director built without a validator accepts every slot as it is |
| `EncounterDirectorBehaviour` | The scene's host, one per scene: each `Update` it centres the ring on its `target` Transform and ticks the injected director. Draws the ring while selected in play mode |
| `EncounterAgent` | Enrols its object in the encounter: registers with the injected host's director when enabled and unregisters when disabled, which also gives back a token it holds. It waits for the kind of token its injected `IEncounterAgentSettings` name, and for a melee one when it was given none. `Target`, `HasToken`, `SlotPosition`, `TryAcquireToken` and `ReleaseToken` are what the nodes and the facts updater ask; `TokenGranted` and `TokenReleased` are raised for its own token only. It counts as joined before the director announces it, so a listener can already ask it for its target. In a scene with no encounter it warns once, is never granted a token, has no target, and its slot is where it stands; an agent that was never injected (instantiated without the container) says that instead |
| `AttackerLane` | Makes the ring yield to the attacker: while its agent holds a token the `NavMeshAgent`'s avoidance priority is `attackerAvoidancePriority`, otherwise `defaultAvoidancePriority`. Enabled while the token is already held it takes the lane at once, and disabled it gives the lane back |
| `EncounterDebugView` | Tints a renderer while its agent holds a token and draws a line to its slot. On no prefab yet |
| `EncounterStaticInstaller` | Binds scene-wide one `EncounterDirector`, the NavMesh slot validator, and the `EncounterDirectorBehaviour` found in the hierarchy. The director's settings are bound by `CombatStaticInstaller`, from the config |
| The nodes (`Nodes/`) | **Encounter → Try Acquire Attack Token** (answers at once: Success while the agent is granted or holds a token, Failure otherwise), **Release Attack Token** (always Success), and three value nodes: **Has Attack Token**, **Get Slot Position**, **Get Encounter Target**. On an agent that is in no encounter they answer Failure, Success, false, the agent's own position and nothing |

### Enemies (`Runtime/Enemies`)

| Type | Responsibility |
|---|---|
| `EnemyArchetype`, `EnemyArchetypeDatabase` | One kind of enemy, as a database item: which `HitReceiverProfile` it has, how long it idles (`idleMin`, `idleMax`), how near its target it stops closing in and takes its place on the ring (`combatRange`, kept wider than the ring), how fast it walks (`moveSpeed`), and whether it waits for a melee token or a ranged one (`tokenType`, which makes the archetype the enemy's `IEncounterAgentSettings`). `Validate` reports a missing profile, an idle range the wrong way round and a move speed of zero. The database asset is `Archetypes/EnemyArchetypes.asset`, edited in **Window → Arcane Onyx → Enemy Archetypes**. An Elite is another item, not another prefab |
| `EnemyInstaller` | The `MonoInstaller` on the enemy's `GameObjectContext`: holds the archetype (database dropdown) and binds everything in the "Per enemy" row above, the same shape as the player's installer. An optional piece is bound when its component is on the enemy and replaced by one that does nothing when it is not. It reports a missing archetype, each problem the archetype's own `Validate` finds (no profile, an idle range the wrong way round, a move speed of zero), and a `combatRange` that does not reach past the ring (slot radius plus sample radius), since such an enemy would walk in and out of range instead of holding a slot. It still builds the enemy: what needs the missing piece takes it as optional and goes without, so one mistake is one error in the console, not a failed injection |
| `EnemyArchetypeWriter` | Copies the archetype's tunables onto the agent's variables at `Awake`, under the keys in `EnemyVariableNames`, so the tree reads them by port and never sees the asset, and sets the `NavMeshAgent`'s speed to the archetype's `moveSpeed` when the enemy has one |
| `EncounterFactsUpdater` | A character component that publishes what the encounter means for this enemy, at `Awake` and then every tick, through BH3's writer: `hasTarget` (it is in an encounter that has a target), `targetDistance` (metres on the ground), `inCombatRange` (within the archetype's `combatRange`) and `hasToken`. The writer drops a value that did not change, so the three booleans wake a guard only when they flip. First in the enemy's `CharacterComponentUpdater` list. With no target the distance is infinity, and without an archetype the enemy is never in combat range |
| `EnemyStatus` | The enemy's `ICombatTarget`: a `HitReceiver` built from the injected profile. At `Awake` it publishes `isAlive` true, `reaction` "None" and `health01` 1; on each landed hit it publishes `health01` and one of: `reaction` as the applied reaction's name (Flinch, Stagger, Knockdown), `isAlive` false for a death, nothing for a hit armor shrugged off. Every write goes through BH3's `AgentVariableWriter` with its name as the source, so the why-inspector attributes it, and a hit of the kind already playing does not move the fact. It requires the writer on its object and reads the fact back through it. Dead, it is no longer a valid target. `State` is the `reaction` fact as the tree left it, or "Dead". Without a profile it logs an error and cannot be hit. It never plays anything |
| `EnemyVariableNames` | The keys: `isAlive`, `reaction`, `health01`, `idleMin`, `idleMax`, `combatRange`, `hasTarget`, `targetDistance`, `inCombatRange`, `hasToken` |
| `MoveToStrikeDistanceNode` | **ArkhamCombat → Move To Strike Distance**: ports `Target` (a Transform), `StrikeDistance` and `Timeout`, all three required. Success as soon as the agent is within the strike distance of the target, measured on the ground; until then it keeps the `NavMeshAgent` walking to the target and returns Running; Failure when the timeout passes first, so an attacker that cannot get there gives its branch up. On exit it stops the agent where it is. No target, no `NavMeshAgent`, or one that is not standing on a NavMesh is a Failure, and each reason is logged when it first applies or changes, never every tick. No tree uses it until the Attack sub-tree |
| `PlayActionNode` | **ArkhamCombat → Play Action**: one `Action` port. On enter it hands the action to the agent's `CombatActions` runner; it returns Running while that action plays and Success once it is no longer the runner's action, whether it ended or something else replaced it; on exit, which a guard abort reaches, it cancels its own action if that is still playing and never one something else started. No `CombatActions` on the agent or no action fed is a Failure, with the reason logged once per agent however often the branch is entered again |

### Trees (`Trees/`)

Authored in the Editor through BH3's authoring API and verified with `bt_verify`; the assets are the
source of truth and each carries sticky notes saying why it is shaped as it is.

| Asset | What it is |
|---|---|
| `Functions/IsReacting` | A Function with one input, `kind`: whether the agent's `reaction` fact equals it. Pure, watching `reaction`, so a guard on it wakes only when the fact moves. One asset, three call sites |
| `Functions/StopBlockingTheWay` | A Function with no result that switches the agent's collider and `NavMeshAgent` off, so nothing has to walk around a body; the Death branch runs it on a Script Graph node's `OnEnter` |
| `Functions/LeaveTheEncounter` | A Function with no result that switches the agent's `EncounterAgent` off: it gives back its token, leaves the ring and the roster, and has no target any more |
| `Functions/HasTargetInCombatRange` | A Function returning bool: `hasTarget` and `inCombatRange`. Pure, watching both, so a guard on it wakes only when one of them flips |
| `Functions/PositionOf` | A Function with one input, `of`, a Transform: its position. It is what lets a target feed a position port |
| `Roots/Functions/ThugRoot.*Read` | The variable reads behind the Death and Approach guards and Idle's ports, generated beside the root |
| `SubTrees/Flinch` | Optional parameter `action`, defaulting to the reaction asset. Stop Nav Agent, Play Action, then Set Variable `reaction` = None. It keeps its token, so chip damage cannot cost an enemy its turn |
| `SubTrees/Stagger` | Optional `action`. Release Attack Token, Stop Nav Agent, Play Action, Set Variable `reaction` = None |
| `SubTrees/KnockedDown` | Optional `knockdownAction` and `getUpAction`. Release Attack Token, Stop Nav Agent, Play Action, Play Action, Set Variable `reaction` = None. The time on the floor is the Knockdown action's duration |
| `SubTrees/Death` | Optional `action`. Release Attack Token, Script Graph (`LeaveTheEncounter`), Stop Nav Agent, Play Action, Script Graph (`StopBlockingTheWay`), then a Repeater over a Wait so the branch never returns and the root cannot replay the death. Leaving comes first, so the ring re-spreads while the death plays |
| `SubTrees/FaceTarget` | Optional `turnSpeed`, 540 degrees a second. One Face Target node fed by Get Encounter Target; it succeeds once the agent faces its target within about 18 degrees. The one place Face Target's four ports are wired: HoldSlot and Approach call it |
| `SubTrees/HoldSlot` | Set Nav Agent Position fed by Get Slot Position, then FaceTarget. It succeeds as soon as the agent faces, so the root asks again every loop and the slot follows the target |
| `SubTrees/Approach` | Optional `repathSeconds`, 0.25. Set Nav Agent Position fed by `PositionOf` the encounter's target, FaceTarget, then a Wait, so the path follows a target that moved |
| `SubTrees/Idle` | Optional `idleMin` and `idleMax`, read by a Wait Range |
| `Roots/ThugRoot` | Repeater, Selector, then in priority order Death (reactive guard on `isAlive` false), KnockedDown, Stagger, Flinch (reactive guards on `IsReacting` with a literal `kind` each), HoldSlot (reactive guard on `HasTargetInCombatRange`), Approach (reactive guard on `hasTarget`) and Idle (no guard, its ports fed from the agent's `idleMin` and `idleMax`). Every reactive guard takes over lower priority and wakes only when a fact it reads changes, so the ring takes over from the approach on the frame the target comes into range, and a reaction takes over either and stops the walk. KnockedDown alone has Stops Its Own Branch off: once down, the thug gets up before anything lighter is heard, and the branch's clear swallows the hit. A different reaction cuts a flinch or a stagger short because its guard goes false, a lighter one as much as a heavier one: a jab landing during a stagger ends it early with a flinch. The same reaction again changes nothing, since the fact does not move |

### Shell, Camera, Editor

| Type | Responsibility |
|---|---|
| `FrameDataOverlay` | The spec 01 tuning strip, IMGUI: one bar per recent action with the warp (yellow), hit (red), combo (green) and evade (blue) bands, magenta cue marks, a white playhead, and a tick per press coloured by its fate (green spent, red expired, yellow queued). Everything comes from the runner of the scene's `CombatActions`; without one it draws nothing. The header's combo is the scene's `ComboTracker`, zero without one |
| `CombatCameraDemo`, `GroupFramingSource`, `GroupFraming` | The combat framing on the camera rig: pivot drifts toward the enemy centroid, distance follows the spread. Pure math in `GroupFraming` |
| `ChainNodeIdDrawer` | The dropdown of node ids on edge destinations and the stance root |

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
  `Assets/Installers/Static` points at the config. The `encounter` section of the config tunes the tokens and
  the ring. The stance is set on the
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
  gives a character without that piece; `CombatActions` is what plays actions at all. The scene also needs the Hermes object described under Events.
- **A reaction action**: Create → ArkhamCombat → Action, with its duration and cues, beside the five in
  `Actions/Reactions`. A knockdown's duration is its time on the floor: the body stays down until the
  get-up stands it.
- **A hit receiver profile or an enemy archetype**: an item in `HitProfiles/HitReceiverProfiles.asset`
  or `Archetypes/EnemyArchetypes.asset`, added in **Window → Arcane Onyx → Hit Receiver Profiles** or
  **Enemy Archetypes** and picked through the dropdown on the archetype or the enemy's installer. Never a
  loose asset, and never added by hand in the YAML: the window is what gives an item its id.
- **An enemy**: `Prefabs/Thug.prefab` is the model. On the root: a `GameObjectContext` whose Mono
  Installers list holds the `EnemyInstaller` on the same object with its archetype picked, `Variables`,
  `AgentVariableWriter`, a `BehaviorTreeMachine` pointing at the root tree, `CombatActions`,
  a `CharacterComponentUpdater` whose list holds the `EncounterFactsUpdater` and then the `CombatActions`
  (an enemy has no brain, so this is what ticks its per-frame jobs, in the order of the list),
  `EnemyStatus`, `EnemyArchetypeWriter`, `EncounterAgent`, `AttackerLane`, the collider, a `NavMeshAgent`
  sized to the collider with its base offset at half the height (the prefab's origin is the capsule's
  centre) and its **Angular Speed at 0**: the tree's Face Target is the only thing that turns an enemy,
  and a NavMeshAgent that also turned it would fight it. Last, a `ProceduralPresentationDriver` whose body
  is the capsule child. Drop instances into the scene; the scene context builds them.
- **A scene enemies fight in**: one object with `EncounterDirectorBehaviour` whose target is the player,
  and a baked NavMesh. In `CombatArena` the walkable level sits under the `Arena` object, whose
  `NavMeshSurface` collects its children's colliders; after moving level geometry, bake it again from
  that component. The data is `Assets/CombatArena/NavMesh-Arena.asset`.
- **An enemy's behaviour**: edit its root tree on the canvas, or author one with
  `BehaviorTreeAuthoring` from `eval_file` as `Trees/Roots/ThugRoot` was: pick sub-trees, their order,
  their guards and the arguments on their ports. A new reaction is a new sub-tree and a guard; a different
  enemy is a different root. Run `bt_verify` on it before trusting it.

## Extending

| Want | Do |
|---|---|
| A new cue kind | One `[Serializable]` class implementing `ICueKind` in Presentation; it appears in the dropdown |
| A new pool policy | One class implementing `IVariantPolicy`; same dropdown rule |
| Real target selection | Implement `ITargetScorer`, bind it in `CombatStaticInstaller`; `CombatTargeting` keeps turning the stick into a world direction. A character that aims another way (an enemy agent) gets its own `IActionTargetPicker` component, which the character installer binds in place of `CombatTargeting` |
| Another source of targets | Implement `ITargetRoster`, bind it in `CombatStaticInstaller` in place of `EncounterTargetRoster` |
| A different token or ring feel | Edit the `encounter` section of `CombatConfig`; an enemy's `combatRange`, `moveSpeed` and `tokenType` are on its archetype |
| Slots that respect something other than the NavMesh | Implement `ISlotValidator`, bind it in `EncounterStaticInstaller` in place of `NavMeshSlotValidator` |
| A new fact about the fight for an enemy's tree | Add the key to `EnemyVariableNames` and write it from `EncounterFactsUpdater`, or from a new `CharacterComponent` in the enemy's updater list when it is a different job |
| Reacting to who joins the fight or whose turn it is | Listen to the `EncounterDirector`'s C# events from code that has the director injected. A Hermes event is added only when another system has to react |
| A tougher or armored enemy | A new item in the `HitReceiverProfiles` database, picked on a new or existing archetype |
| A new kind of enemy | A new item in the `EnemyArchetypes` database; a prefab that points at it through its `EnemyInstaller`. Different behaviour is a different root tree on its `BehaviorTreeMachine` |
| A new enemy reaction | A sub-tree with an Optional `action` parameter that starts with Stop Nav Agent, and with Release Attack Token before that when the reaction may interrupt an attack; a guard on `IsReacting` with its kind at the call site; and `EnemyStatus` publishing that kind. The reaction sub-trees need a `NavMeshAgent` on the enemy: without one Stop Nav Agent fails the branch before it plays |
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
chain is Jab (left or right by target side), Cross, Roundhouse, then back to the jab. The three thugs
walk up and stand around the player on a ring three metres out, facing in, and follow when the player
moves; near the arena's edge the ring bends to the walkable ground. They do not attack yet. Beyond the
lunge limit the warp refuses and the strike whiffs. A hit stops a thug where it is, and it rejoins the
ring afterwards. A jab makes a thug flinch, the Cross
staggers it, the Roundhouse knocks it down and it gets up; the sixth hit kills it (ten damage a strike against the Thug profile's sixty health),
it stays down, leaves the ring so the others spread out again, and the next strike picks another one. Each thug's agent variables show in BH3's Variable
Watch, and its flight recorder shows which branch ran and who wrote each fact. The overlay in the top left shows
why anything happened. If the Editor window loses focus play mode stops advancing unless Run In
Background is on.

## Tests

`ArkhamCombat.Tests` runs in EditMode and needs no scene. Edit mode runs no `Awake` and no `Start`, so a test that needs what they do calls the component's internal set-up methods by hand, in the order Unity would. It covers: windows, actions, stance validation, pool
policies, the reaction table row by row, a receiver's health down to death and past it, the land check by range and by angle, the group framing math on plain vectors, the hit window listener against a target in and out of reach, behind, with a pending warp, and one that ignores the hit, with its announcements on a dispatcher built by the test, the enemy status publishing its starting facts, a reaction, a death, an armored shrug and nothing for a dead enemy, refusing hits without a profile, and on an agent with a machine leaving the reaction fact unmoved by a second hit of the same kind, the archetype writer copying the idle range, the enemy installer on containers built by the test (a runner without a stance, the do-nothing versions of every optional piece, the archetype's profile, one warp mover under both names, the enemy's own components injected from it, and each authoring error reported with the enemy still built), the Play Action node on a hand-ticked graph (starts the action, runs until it ends, cancels on exit, stops waiting and leaves alone an action something else started, fails without an action or without `CombatActions` and says why once), the held-pose cues (a fall lowered by how far it turned, measured from the standing body even when it was crumpled, not lowered without a renderer, left lying at its end; a rise; a crumple), the reaction names typed on the tree canvases against the names the status publishes, read from the authored assets, and from the same assets the order of the root's branches, the facts that wake the ring's guards against the keys the facts updater writes, each reaction branch giving its token back and stopping the walk before it plays (a flinch only stops), and every parameter read in a sub-tree having a variable kind, which `bt_verify` does not check, the encounter director (the token limit, the two pools, hold timeout, cooldown, the ring's slots following the target, the overflow bearing; its roster and four events; slots a validator rejects or moves), the encounter agent joining, leaving and giving its token back, and standing alone in a scene with no encounter, the director's host passing target and time on, the five encounter nodes on hand-ticked graphs with and without an encounter, the encounter installer's one director built from the bound settings, the attacker lane's two priorities, the facts updater's four facts and its not rewriting one that did not change, the roster the player strikes from, the Move To Strike Distance node in every way it ends without walking (inside the distance, out of time, no target, no NavMeshAgent, no NavMesh under it, and a new reason said when the reason changes), the two scene installers together building the director from the config and the player's roster on that director, the Thug prefab's wiring (facts before actions in the updater list, the target beside the agent, a NavMeshAgent that does not turn it), the Death branch leaving the encounter before it plays and switching the collider and the NavMeshAgent off after, each guard's two switches on the root, the targeting component's stick threshold and frame, the trace marking presses until it stops listening, one test that fails loudly when the Hermes events are not generated, the meter, the combo tracker on a dispatcher built by the test, the resolver, the clock, the runner with the null driver and recording test
doubles, its started and ended events, the stand-in picker on test targets, the character brain listing the character components already on a character, the combat actions with a start gate that
allows and refuses and their announcements on a dispatcher built by the test, the attacking state switch's rule, the character installer on containers built by the test (which
condition evaluator a stance gets, the optional pieces and their do-nothing versions, a runner for a character with
nothing optional), the player installer reporting an empty state list and an empty state slot, the player installer handing out the combat actions beside the player's brain rather than an enemy's, and the assembly boundary (no DOTween or Zenject in the core, neither the enemies nor what every character shares reaching the player assembly, and the encounter reaching neither side). What the trees do when they run (a reaction taking over Idle, a stagger cutting a flinch short, a knockdown swallowing a jab, a death that is not replayed, the collider going off, the approach handing over to the ring, a hit stopping the walk, a dead thug leaving the ring) is checked in play mode by hand, from each thug's flight recorder; no automated test covers it. Nor does one cover anything that needs a NavMesh: the NavMesh slot validator, and Move To Strike Distance actually walking and stopping, were checked in play mode. The intent buffer's tests live in
the controller submodule, and so do the character brain skipping and reporting an empty entry of its list. Entering the `Attacking` state needs the whole controller and is checked in play mode.
