# 02 — Enemy behaviour trees and unit variants

**Status:** task document for implementation. Trees are authored directly in the Unity Editor through the
BH3 `bt_*` tools and reviewed on the canvas. There is no code generator. Verify type and member names
against bh3-development before reusing anything from it.

## Goal

Three enemy archetypes for an Arkham-style melee arena, every one of them running the same handful of
sub-trees. A designer changes what an enemy does by editing its root tree: which sub-trees it has, in
which priority, under which guard, with which arguments. Nothing inside a sub-tree knows which enemy it
is running on.

Companion documents: [01 — Combo System](01-combo-system.md) owns the player's actions and the
`ActionDefinition` assets this spec reuses. The hit pipeline (05) and the encounter director extensions
(06) are referenced where this spec reads their facts.

## Context you need

- BH3 sub-trees (`Run Behavior Tree Graph`) declare Required and Optional parameters that become input
  ports on the calling node. Guards go on the call site. A reactive guard on the call site aborts the
  whole sub-tree instance (`docs/2-building-trees/07-sub-trees.md`).
- Reactive guards with **Takes Over Lower Priority** preempt a running lower-priority sibling on the same
  frame. **Stops Its Own Branch** off is the committed swing (`docs/2-building-trees/06-guards.md`).
- `EncounterDirector` and its nodes (`RequestAttackToken`, `ReleaseAttackToken`, `HasAttackToken`,
  `GetSlotPosition`, `GetEncounterTarget`) live in bh3-development under `GOWDraugr/BH3/Encounter/`. That
  is superproject code, not a module, so it must be copied or extracted into a module before this project
  can reference it (see Open decisions).
- The Draugr root in bh3-development is the closest prior art. Its sub-trees Death, Stagger, Flinch,
  MeleeAttack, HoldSlotPosition, ApproachTarget and IdleWait are the starting point for the ones below.
  Its inner guards on the swing sequence are replaced here by take-over guards at the root.

## Design laws

1. **A branch may never depend on a sibling having run.** Facts come from sensors on the prefab, written
   through `AgentVariableWriter`. Branches consume facts and parameters. The one permitted write is a
   branch clearing the trigger fact that admitted it (the FPS demo's `justKilled` pattern).
2. **A branch never asks what kind of enemy it is on.** An "if armored" inside a sub-tree is either a
   roster difference (the root has or lacks the branch) or a parameter (the root passes a different
   argument). The check moves to the root.
3. **Reactions sit above actions, with take-over on.** Death, knockdown, stagger, stun and flinch are
   reactive guards at the top of the selector. They land the same frame their fact flips, even mid-swing,
   so no sub-tree needs to poll for them.
4. **Every reaction that can interrupt an attack releases the token first.** A guard aborting Attack
   skips its own release node. Until BH3's abort-safe resource mechanism exists, the reaction branches
   carry the release, exactly as the Draugr trees do.
5. **Quick branches return.** HoldSlot, Approach and Idle finish within a few ticks so the repeater
   restarts the selector and priorities are re-checked. A branch that returned `Running` forever would
   pin the root.

## Facts: the prefab contract

Every fact is an `Object` variable on the enemy's `Variables` component, written through
`AgentVariableWriter` so `On Key Changed` triggers wake. Three sensor components produce them.

| Key | Type | Producer | Cleared by |
|---|---|---|---|
| `isAlive` | bool | `EnemyStatus` (hit pipeline) | never |
| `isKnockedDown` | bool | `EnemyStatus` | KnockedDown branch, after get-up |
| `isStaggered` | bool | `EnemyStatus` | Stagger branch, after the action |
| `isFlinching` | bool | `EnemyStatus` | Flinch branch, after the action |
| `isStunned` | bool | `EnemyStatus`, timer from the player's stun action | `EnemyStatus` when the timer ends |
| `health01` | float | `EnemyStatus` | n/a |
| `reactionOverride` | `ActionDefinition` or null | `EnemyStatus`, set by `BeginPair` (spec 05) together with the trigger fact the paired reaction rides on | `EnemyStatus` when the reaction branch finishes or the pair is broken |
| `hasTarget` | bool | `EncounterAgent` (director) | director on unregister |
| `targetDistance` | float | `EncounterAgent`, every frame | n/a |
| `inCombatRange` | bool | `EncounterAgent`, `targetDistance <= combatRange` | n/a |
| `hasToken` | bool | `EncounterAgent`, mirrors the director | n/a |
| `telegraphing` | bool | `ActionRunner`, during the attack's telegraph window | `ActionRunner` on window close |
| `playerStrikeIncoming` | bool | `ThreatSensor`, from the player's target selection (spec 04) | `ThreatSensor` when the strike lands or misses |

Reaction triggers are cleared by the branch because their duration is the action's. `isStunned` is
cleared by the sensor because its duration is a gameplay number owned by the player's action.

`reactionOverride` is how a paired action (spec 05) borrows a reaction branch: `BeginPair` sets the
override and flips the matching trigger, `isStaggered` for a counter, `isAlive = false` for a takedown.
The branch that wakes plays the override instead of its own parameter. The Death branch must tolerate
being aborted if a broken takedown restores `isAlive`; a reactive guard with Stops Its Own Branch on
does that already.

Tunables that are arguments, not facts, also live on `Variables` so prefab variants can override them:
`attack` (an `AttackDefinition`), `combatRange`, `tauntChance`, `dodgeChance`, `idleMin`, `idleMax`.

## Sub-trees

One asset each. Parameters are listed as **R** (Required) or **O** (Optional, with default). "Exit" says
how the branch ends, which matters for law 5.

| Sub-tree | Parameters | Body | Exit |
|---|---|---|---|
| **Death** | `deathAction` O | Sequence: Release Attack Token, Stop Nav Agent, Play Action(ReactionOrDefault(`deathAction`)) | Succeeds once; the root's Death guard keeps it selected because `isAlive` stays false |
| **KnockedDown** | `knockdownAction` O, `getUpAction` O, `downSeconds` O = 1.5 | Sequence: Release Attack Token, Stop Nav Agent, Play Action(ReactionOrDefault(knockdown)), Wait Time(downSeconds), Play Action(getUp), Set Variable `isKnockedDown` = false | Succeeds after get-up |
| **Stagger** | `staggerAction` O | Sequence: Release Attack Token, Stop Nav Agent, Play Action(ReactionOrDefault(`staggerAction`)), Set Variable `isStaggered` = false | Succeeds after the action |
| **Flinch** | `flinchAction` O | Sequence: Play Action, Set Variable `isFlinching` = false. **Keeps the token** so chip damage cannot lock an enemy out of its turn | Succeeds after the action |
| **Stunned** | `stunnedLoopAction` O | Sequence: Release Attack Token, Stop Nav Agent, Play Action(loop) | Runs while `isStunned`; the root's reactive guard aborts it when the sensor clears the fact |
| **Dodge** | `dodgeAction` O | Sequence: Face Target, Play Action(dodge with lateral displacement through the sink) | Succeeds after the action. Cooldown decorator at the call site, not inside |
| **Attack** | `attack` **R**, `approachTimeout` O = 3.0 | Sequence: Request Attack Token, Move To Strike Distance(target, `attack.strikeDistance`, timeout), Face Target, Play Action(attack), Release Attack Token | Fails on the first node when it is not this agent's turn, so the selector falls through to HoldSlot. Succeeds after the swing |
| **Taunt** | `tauntAction` O | Sequence: Face Target, Play Action | Succeeds after the action |
| **HoldSlot** | none | Sequence: Set Nav Agent Position(Get Slot Position), Face Target | Succeeds as soon as facing; re-runs every loop |
| **Approach** | `repathSeconds` O = 0.25 | Sequence: Set Nav Agent Position(target position), Wait Time(repathSeconds) | Succeeds so the root re-checks |
| **Idle** | `idleMin` O = 0.2, `idleMax` O = 0.5 | Wait Time Random Range | Succeeds |

`Play Action` is one new C# node: it hands an `ActionDefinition` to the shared `ActionRunner` from
spec 01 and returns `Running` until the action ends. Windows, the land check, warp and the `telegraphing`
fact are the runner's job, so an enemy swing and a player swing behave identically. `Move To Strike
Distance` is the second new node: nav toward the target until within the given distance, failing on
timeout so a blocked attacker releases its turn rather than stalling the encounter.

`ReactionOrDefault(default)` is a Function, not a node: it returns `reactionOverride` when set, else its
`default` input. Death, KnockedDown and Stagger feed Play Action through it so a paired reaction can
borrow the branch. Flinch does not: nothing pairs with a flinch.

## Roots

Priority runs top to bottom. **RG** is a Reactive Guard with Takes Over Lower Priority on and Stops Its
Own Branch on unless stated; its trigger is `On Key Changed` for the named key. **CE** is a Conditional
Execution. Arguments in parentheses are fed from the prefab's `Variables`.

```text
ThugRoot
└─ Repeater
   └─ Selector
      ├─ Death        RG  isAlive == false
      ├─ KnockedDown  RG  isKnockedDown
      ├─ Stagger      RG  isStaggered
      ├─ Flinch       RG  isFlinching
      ├─ Attack       CE  hasTarget AND inCombatRange              attack = Haymaker
      ├─ Taunt        CE  Function ShouldTaunt(tauntChance) AND NOT hasToken,  Cooldown 6 s
      ├─ HoldSlot     CE  hasTarget AND inCombatRange
      ├─ Approach     CE  hasTarget
      └─ Idle         no guard
```

```text
KnifeRoot
└─ Repeater
   └─ Selector
      ├─ Death        RG  isAlive == false
      ├─ KnockedDown  RG  isKnockedDown
      ├─ Stagger      RG  isStaggered
      ├─ Flinch       RG  isFlinching
      ├─ Dodge        CE  playerStrikeIncoming AND Function CanDodge(dodgeChance),  Cooldown 2 s
      ├─ Attack       CE  hasTarget AND inCombatRange              attack = KnifeSlash (unblockable)
      ├─ HoldSlot     CE  hasTarget AND inCombatRange
      ├─ Approach     CE  hasTarget
      └─ Idle         no guard
```

```text
ArmoredRoot
└─ Repeater
   └─ Selector
      ├─ Death        RG  isAlive == false
      ├─ KnockedDown  RG  isKnockedDown
      ├─ Stunned      RG  isStunned
      ├─ Stagger      RG  isStaggered
      │                   (no Flinch: armor ignores light hits, so the branch is simply absent)
      ├─ Attack       CE  hasTarget AND inCombatRange              attack = HeavySwing (counterable, slow telegraph)
      ├─ HoldSlot     CE  hasTarget AND inCombatRange
      ├─ Approach     CE  hasTarget
      └─ Idle         no guard
```

Why Attack is a Conditional Execution and not a reactive guard: the attack is committed by design, and
everything that should interrupt it sits above it with take-over on. A reactive guard with Stops Its Own
Branch off would be equivalent, but the doorman says the intent more plainly and costs nothing per tick.

Why HoldSlot and Approach are Conditional Executions: both return within a few ticks, so the repeater
re-evaluates the selector often enough that a watchman buys nothing.

## Variants, three layers

Reach for the cheapest layer that expresses the difference.

| Layer | What changes | Example | Cost |
|---|---|---|---|
| **1. Data** | Values on the prefab's `Variables`, read by the call-site ports | Elite Thug: more health, Haymaker swapped for Uppercut, lower `tauntChance` | A Unity prefab variant overriding fields. No tree touched |
| **2. Roster** | Which sub-trees the root has, and their order | Knife has Dodge; Armored has Stunned and lacks Flinch | A new root asset, ten call sites |
| **3. Condition** | The Function on a guard, or its inputs | An aggressive variant's `ShouldTaunt` wired to a different chance | Swap one Function or one argument; usually collapses into layer 1 |

Each archetype is a prefab carrying the three sensors, the `Variables` contract, a `BehaviorTreeMachine`
pointing at its root, and a NavMeshAgent. Elites are prefab variants.

## Functions to author

| Function | Inputs | Reads | Result |
|---|---|---|---|
| `ShouldTaunt` | `chance : float` | `hasToken` | bool, random roll AND NOT hasToken |
| `CanDodge` | `chance : float` | `playerStrikeIncoming` | bool |
| `HasTargetInCombatRange` | none | `hasTarget`, `inCombatRange` | bool. Shared by the Attack and HoldSlot guards so the two never drift |
| `ReactionOrDefault` | `default : ActionDefinition` | `reactionOverride` | `ActionDefinition`. Feeds Play Action in Death, KnockedDown and Stagger. Not a guard, so it may return an object |

Each declares its watched keys so `bt_guard_on_function` seeds the triggers. Guards stay pure.

## What the director must provide (spec 06 dependencies)

- Publish `hasTarget`, `targetDistance`, `inCombatRange`, `hasToken` per agent through the writer.
- Relay the attacker's `telegraphing` window to the player as `incomingAttackCounterable`, with the
  attacker reference, so the player's Counter edge can fire.
- NavMesh-validate slot positions, with the bearing position as fallback.
- Prefer granting the token to agents in front of the player or on screen.
- Raise the token holder's avoidance priority so the ring yields to it.

## Authoring approach

Everything is built in the live Editor through the `bt_*` tools and reviewed on the canvas. The order
that avoids rework:

1. Write and compile the C# nodes first (`Play Action`, `Move To Strike Distance`), since a tree cannot
   reference a node type that does not exist yet.
2. Author the sub-trees, declaring parameters before adding the nodes that read them.
3. Author the three Functions and check their declared keys.
4. Author `ThugRoot`, run `bt_verify`, fix every finding, then clone the approach for Knife and Armored.
5. `bt_describe_tree` on each root goes into the review along with a canvas screenshot.

`bt_verify` is expected to report nothing on the roots. If a finding is intentional, as the FPS demo's
entry-only Taunt guard is, a sticky note on the canvas says why.

## Confirm against the code before building

1. A reactive guard on a `Run Behavior Tree Graph` node aborts an instance that is mid-`Play Action`, and
   the abort reaches the `ActionRunner` so hitboxes disarm and the warp stops. If the runner needs an
   explicit `OnExit`, add it to the node.
2. A port typed `AttackDefinition` (a `UnityEngine.Object`) can be fed from an `Object` variable. If not,
   the branch takes a string id and resolves it through the ScriptableObjectDatabase module.
3. A `Cooldown` decorator wrapping a sub-tree call site combined with a Conditional Execution on the
   same node evaluates in the order this spec assumes (cooldown first, then the guard).

## Tasks, in order

| # | Task | Done when |
|---|---|---|
| T1 | Bring in the encounter code | `EncounterDirector`, settings, `IEncounterAgent` and the five nodes compile in this project under their own assembly, as the module spec 06 extracts. Spec 06 T2 writes the EditMode baseline; none exist today |
| T2 | Sensors | `EnemyStatus`, `EncounterAgent`, `ThreatSensor` publish every fact in the contract table through `AgentVariableWriter`, including `reactionOverride` set and cleared by `BeginPair` and `Break`. EditMode tests drive each with fake inputs and assert the writes and the clears |
| T3 | `Play Action` node | Runs an `ActionDefinition` through the shared runner, returns Running until the action ends, releases cleanly on abort. PlayMode test with a short duration-only action, including an abort halfway |
| T4 | `Move To Strike Distance` node | Succeeds inside the distance, fails on timeout, stops the agent on exit. PlayMode test on a small NavMesh |
| T5 | Sub-trees | All eleven authored with the parameters listed. `bt_verify` clean on each |
| T6 | Functions | Four authored, keys declared, pure; the three guard Functions return bool. PlayMode test: a counter on a thug plays the paired reaction through Stagger, and a broken pair plays the plain stagger instead |
| T7 | Roots | Thug, Knife, Armored authored as drawn. `bt_verify` clean. `bt_describe_tree` output attached to the review |
| T8 | Prefabs | Three archetype prefabs plus one Elite Thug prefab variant that changes only `Variables` values |
| T9 | Arena test scene | Flat arena, NavMesh baked, director in the scene, three enemies of mixed archetype and a static player stand-in that publishes `playerStrikeIncoming` on a key press and deals a hit on another |
| T10 | PlayMode soak | Sixty seconds with the stand-in hitting at random: every root visits Attack, HoldSlot and at least one reaction; no agent holds a token longer than the director's timeout; no exception |

T1 to T4 are C# and testable without any tree. T5 to T7 are pure authoring in the Editor. T9 and T10
need spec 05's hit pipeline only in stub form: a component that sets the reaction facts on command.

## Open decisions

1. **Where the encounter code lives.** Copy into this project, or extract from bh3-development into a
   new `EncounterDirector` module repo and add it as a submodule. Default: extract to a module, since
   both projects will want fixes.
2. **`ActionDefinition` versus `AttackDefinition`.** Reactions, taunts and dodges need a duration and windows
   but no hit data. Default: spec 01's asset becomes `ActionDefinition` with the hit fields on an
   `AttackDefinition` subclass. This amends spec 01.
3. **Flinch on Knife.** Present as drawn. If knife enemies feel too easy to stun-lock, remove the branch
   from `KnifeRoot` rather than adding a condition inside Flinch.

## Non-goals

- No perception. Enemies know the player through the director from the moment they register.
- No tactical position queries. The ring and the strike distance are the only positional decisions.
- No ranged archetype. A gunman would add Shoot and Reposition sub-trees and is where tactical positions
  would first be needed.
- No generator. Trees are authored in the Editor and the assets are the source of truth.
