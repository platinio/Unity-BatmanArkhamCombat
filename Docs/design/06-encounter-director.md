# 06 — Encounter director: extensions for the arena

**Status:** task document for implementation. Extends the `EncounterDirector` from bh3-development
(`GOWDraugr/BH3/Encounter/`) rather than replacing it. Verify member names against that code.

## Goal

The director already makes a group read as choreographed: a limited number of attack tokens circulate,
and everyone without one stands on a ring. This spec adds what the other five specs ask of it: the facts
enemies' trees read, the telegraph relay that opens the player's counter, slots that respect the arena,
a token grant that keeps attacks on screen, and the registered list the camera frames.

## What exists

| Piece | What it does |
|---|---|
| `EncounterDirector` (plain class) | Registration, token pools per `AttackTokenType`, hold timeout, reacquire cooldown, greedy nearest-slot assignment on an interval. Time arrives through `Tick(deltaTime)`, so it is test-driven without a scene |
| `EncounterDirectorSettings` | Token counts, timeouts, slot count and radius, reassign interval |
| `IEncounterAgent` | `Position` only. Deliberately narrow |
| `EncounterAgent` (component) | Registers on enable, unregisters on disable (which releases the token), exposes `Target`, `HasToken`, `TryAcquireToken`, `ReleaseToken`, `SlotPosition` |
| `EncounterDirectorBehaviour` | Hosts the director, follows the target, ticks it, draws the ring. A static `Active` fallback |
| Nodes | `RequestAttackToken`, `ReleaseAttackToken`, `HasAttackToken`, `GetSlotPosition`, `GetEncounterTarget` |
| `EncounterDebugView` | Runtime view of tokens and slots |

There are no EditMode tests for the director today. This spec adds them.

## Principles

1. **Trees ask, the director decides.** Unchanged. No node commands the director.
2. **The plain class stays plain.** Everything new that needs Unity (NavMesh, Variables, NavMeshAgent)
   enters through an interface injected into the director or lives on the component. The director
   remains drivable from a test with fake agents and a fake clock.
3. **Facts go through the writer.** Everything a tree or a guard reads is written with
   `AgentVariableWriter`, so `On Key Changed` triggers wake and a value republished unchanged costs
   nothing.
4. **One encounter per scene.** The `Active` fallback stays as a safety net, but the normal path is
   injection: the Encounter module ships a static installer (spec 00) that binds the scene's
   `EncounterDirectorBehaviour` and its `EncounterDirector`, so `EncounterAgent`, the picker, the camera
   rig and the spawner receive the director rather than searching for it. Multiple simultaneous
   encounters are out of scope.

## Extensions

### 1. Per-agent facts

`EncounterAgent` publishes, every frame, onto its own `Variables`:

| Key | Value |
|---|---|
| `hasTarget` | director has a target position |
| `targetDistance` | planar distance to the target |
| `inCombatRange` | `targetDistance <= combatRange`, where `combatRange` is read from the agent's `Variables` (a spec 02 tunable) |
| `hasToken` | mirrors the director |

The writer's dedupe is what makes an every-frame publish free for the booleans. `targetDistance` changes
every frame by nature; the guards that depend on it use `inCombatRange` instead, which is why that
derived fact exists.

### 2. Telegraph relay

The enemy's `ActionRunner` tells its `EncounterAgent` when the attack's telegraph window opens and
closes: `NotifyTelegraph(AttackDefinition attack, bool open)`. The agent forwards to the director, which
keeps the set of currently telegraphing agents with their attack.

The director host publishes onto the **player's** `Variables`:

| Key | Value |
|---|---|
| `incomingAttackCounterable` | any telegraphing agent whose attack is `counterable` |
| `incomingAttackUnblockable` | any telegraphing agent whose attack is `unblockable`, for the HUD's "evade" prompt |

Spec 04's Counter gate picks the actual attacker among the telegraphing agents, so the director exposes
`IsTelegraphing(agent)` and `TelegraphingAttack(agent)` for the picker. The relay clears on window close,
on token release, and on unregister, so an interrupted attacker never leaves a stale prompt.

### 3. Slot validation

`AssignSlots` currently places slots on a perfect circle. Add an injected `ISlotValidator` with
`bool TryValidate(Vector3 desired, out Vector3 valid)`. The Unity implementation samples the NavMesh
within `settings.SlotSampleRadius` (default 1.0 m). A slot that cannot be validated is marked unusable for
that assignment, and agents that would have taken it fall back to the bearing position, which already
exists. Tests inject a validator that rejects a chosen set of slots.

### 4. Token grant by priority

Today `TryAcquireToken` grants on first ask. Replace with a request-and-grant cycle:

- `TryAcquireToken(agent)` returns true only if the agent already holds a token. Otherwise it records a
  request for this tick and returns false. The tree's `RequestAttackToken` fails, the selector falls
  through to HoldSlot, and the branch asks again on the next loop, exactly as it does today when the
  pool is full.
- At the end of `Tick`, for each pool with free tokens, requests are scored and the best are granted:

```text
score = frontWeight    * (1 - angle(targetForward, agent - target) / 180)
      + fairnessWeight * clamp01(secondsSinceLastToken / fairnessHorizon)
      - distancePenalty * clamp01(distance / slotRadius)
```

Defaults: `frontWeight` 1.0, `fairnessWeight` 0.6, `fairnessHorizon` 6 s, `distancePenalty` 0.3. The
front term keeps attacks on screen; fairness stops the same two enemies owning the fight; the distance
penalty prefers an attacker who is already close.

- The director needs the target's forward for the front term: `SetTarget(position, forward)` replaces
  `SetTargetPosition`. The host passes the combat camera's flattened forward when it exists, else the
  player's.
- A granted agent that does not start its attack within `settings.GrantGraceSeconds` (default 0.5)
  loses the token, so an agent that was interrupted between grant and swing cannot sit on it.

Hold timeout and reacquire cooldown are unchanged.

### 5. Attacker lane

The director raises `TokenGranted(agent)` and `TokenReleased(agent)`. `EncounterAgent` subscribes and
sets its NavMeshAgent's `avoidancePriority` to `settings.AttackerAvoidancePriority` (default 20) while
holding, and back to `settings.DefaultAvoidancePriority` (default 50) on release. The ring yields to the
attacker instead of slowing it down.

### 6. Roster for the camera and the HUD

`IReadOnlyList<IEncounterAgent> Agents`, plus `AgentRegistered` and `AgentUnregistered` events. The
combat camera (spec 03) builds its target group from these. `IsActive` is `Agents.Count > 0`, which is
what blends the combat camera in and out.

An agent leaves the roster when its `EncounterAgent` component is disabled. `EnemyStatus` disables it on
death, after the Death branch has had its tick to release the token, so the camera stops framing corpses
and no dead agent can be granted a turn.

### 7. `IEncounterAgent` grows by one member

`Position` plus `IsAttacking`, which the grant grace check reads. Nothing else; the fake agents in tests
stay two lines.

## Settings additions

| Field | Default |
|---|---|
| `slotSampleRadius` | 1.0 m |
| `frontWeight`, `fairnessWeight`, `fairnessHorizon`, `distancePenalty` | as above |
| `grantGraceSeconds` | 0.5 |
| `attackerAvoidancePriority`, `defaultAvoidancePriority` | 20, 50 |

## Debug

Extend `EncounterDebugView` and the gizmo: each agent shows its request score when it has a pending
request, token holders are highlighted as today, telegraphing agents get a second ring coloured by
counterable or unblockable, and unusable slots draw hollow. The scores are what you will stare at while
tuning the grant weights.

## Module extraction

The encounter code is superproject code in bh3-development and this project needs it as a dependency.
Default decision from spec 02: extract it into its own module repository and add it as a submodule to
both projects, following the core-and-adapter shape the other modules use:

| Assembly | Holds | Depends on |
|---|---|---|
| `ArcaneOnyx.Encounter.Runtime` | director, settings, `IEncounterAgent`, `ISlotValidator`, `EncounterAgent`, host, debug view | Unity only |
| `ArcaneOnyx.Encounter.BH3` | the five nodes plus `NotifyTelegraph` glue | the core, BH3 |
| `ArcaneOnyx.Encounter.Tests` | EditMode tests | the core |

The `AttackDefinition` reference in the telegraph relay is the one awkward dependency. The core takes an
interface, `ITelegraphedAttack { bool Counterable; bool Unblockable; }`, which `AttackDefinition`
implements, so the module never references this project's combat assembly.

## Tasks

| # | Task | Done when |
|---|---|---|
| T1 | Extract the module | New repo, two runtime assemblies and a test assembly, added as a submodule to bh3-development and here; bh3-development's Draugr scene still works |
| T2 | Baseline tests | EditMode tests for what exists today: register and unregister, token limits per pool, hold timeout, reacquire cooldown, nearest-slot assignment order, bearing fallback. These guard the refactor in T4 |
| T3 | Per-agent facts | The four keys written through the writer; test with a fake writer asserts the values and that an unchanged boolean produces no write |
| T4 | Token grant by priority | Request-and-grant cycle, scoring, grace. Tests: front agent beats side agent at equal fairness; starved agent eventually wins over a front agent; grace revokes an idle grant; a holder asking again gets true immediately |
| T5 | Telegraph relay | Open and close, clears on release and unregister; player facts correct with one counterable, one unblockable, and both. `IsTelegraphing` and `TelegraphingAttack` for the picker |
| T6 | Slot validation | `ISlotValidator` injected; rejected slots fall back to bearing. Unity implementation samples the NavMesh. Test with a validator rejecting half the ring |
| T7 | Attacker lane and roster | Events raised; avoidance priority flips; `Agents`, `IsActive` and the two roster events. Test with fakes |
| T8 | Death leaves the roster | `EnemyStatus` disables the agent component after the Death branch's release tick. PlayMode test: a dying token holder frees the token within one director tick and leaves `Agents` |
| T9 | Debug view | Scores, telegraph rings, hollow unusable slots. Manual check in the review |
| T10 | Director soak | Sixty seconds, six fake agents, scripted attacks and deaths, random forward: no token ever exceeds its pool, no agent starves beyond the fairness horizon twice, no stale telegraph after a release |

T2 comes before T4 on purpose: the grant change is the one refactor that can break behaviour the Draugr
scene already relies on.

## Open decisions

1. **Target forward source.** Combat camera forward when present, else the player's. Decides whether
   "in front" means on screen or in front of Batman. Default: camera, because the point is what the
   player sees.
2. **Ranged pool.** Kept, unused by the three archetypes. Removing it is a one-line change if it never
   gets a user.

## Non-goals

- No multiple encounters, no encounter lifecycle (waves, phases). The spawner in the demo shell registers
  and unregisters agents; the director does not know what a wave is.
- No tactical position queries. Slots are the circle, validated against the NavMesh.
- No group tactics beyond the token and the ring: no flanking orders, no coordinated attacks.
