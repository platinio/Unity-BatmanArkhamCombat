# 05 — Hit pipeline: landing, receiving, reacting

**Status:** task document for implementation. Shared by the player and every enemy archetype.

## Goal

Turn an action's active window into damage, a reaction on the receiver, and the facts and events the
rest of the demo reads: the combo meter, the enemy trees, the HUD. One pipeline, both directions.

## Context

- Every attack is an `AttackDefinition` (spec 01, amended by spec 02 into an `ActionDefinition`
  subclass) with an `active` window, `damage`, a requested `reaction`, and the `counterable` and
  `unblockable` flags.
- Every attack already has a target when it starts. The player's comes from the picker (spec 04); an
  enemy's is the player, from the director. Nobody swings at empty space hoping to connect.
- The `ActionRunner` (spec 01) fires window open and close events and is the only thing that knows where
  an action is in time.
- Enemy state facts (`isAlive`, `isFlinching`, `isStaggered`, `isKnockedDown`, `isStunned`, `health01`)
  are published by `EnemyStatus` (spec 02). The player's reactions arrive as external interrupts on the
  character context (spec 03).

## Principles

1. **Hits are targeted, not volumetric.** An attack lands on its target if, when the active window opens,
   the target is within the attack's hit range and hit angle. No hitbox colliders, no hurtbox layers, no
   per-frame overlap queries, no dedupe. This is how the Arkham games behave: a strike that resolved to a
   target connects unless the target left. A volume mode can be added later behind the same interface
   if a sweeping attack ever needs it.
2. **The attacker proposes, the receiver decides.** The attack carries a requested reaction. The
   receiver's profile turns that into the reaction actually played: armor ignores light hits, a stunned
   enemy takes everything as heavy, the dead take nothing.
3. **The pipeline writes facts and raises events. It never plays animations or moves anything.** Enemy
   trees react to the facts; the player's state machine reacts to the interrupt; presentation listens
   to the events.
4. **Deterministic and testable.** Every rule below is a pure function over `HitInfo` and two profiles,
   with EditMode tests. Unity enters only at the component boundary.

## Flow

```text
ActionRunner: active window opens
  -> HitResolver.TryLand(attacker, attack, target)        range and angle check
     -> miss: attacker.OnWhiff(attack)                     player: combo meter resets
     -> hit:  receiver.Receive(HitInfo) -> HitResult
          ReactionRules.Resolve(requested, receiverProfile, receiverState) -> reaction
          apply damage, update health, pick Death if health <= 0
          enemy receiver: EnemyStatus writes the reaction fact and health01
          player receiver: context.RequestInterrupt(reaction state)
          raise HitLanded(HitInfo, HitResult) for feedback
          attacker.OnHitLanded(result)                     player: combo meter increments
```

The check runs once, at window open. A target that steps out during the active frames is still hit;
the warp has already carried the attacker to it, and re-checking would make hits feel random.

### Invulnerability and counters

Two cases short-circuit `Receive` before damage:

| Case | Rule | Outcome |
|---|---|---|
| **Evade i-frames** | The receiver is mid-action and that action's `invulnerable` window contains the current time | No damage, no reaction. Raise `HitEvaded`. Player: combo meter increments as a successful evade |
| **Counter** | The receiver is the player in `Countering`, the attacker is the enemy whose telegraph opened that counter, and the attack is `counterable` | The enemy's attack never lands. Its action was already cancelled when the counter **began** (see Paired actions below); this row only guarantees that a late-arriving active window from it is ignored. Raise `HitCountered`. Combo meter increments |

`incomingAttackCounterable` (spec 01) is therefore `telegraphing AND attack.counterable`. The director
relays both. An unblockable knife slash never opens the counter edge, so the only answer to it is Evade,
which is the design intent.

### Paired actions

A paired action is one move performed by two bodies: the counter and the "got countered" reaction, the
takedown and the death it ends in. Four properties make it read as one move, and each has a rule:

| Property | Rule |
|---|---|
| **Same start** | The receiver's `pairedReaction` starts when the attacker's action **starts**, not when its hit lands. `ActionRunner.Play` on an action with a `pairedReaction` calls `HitPipeline.BeginPair(attacker, receiver, attack)` before the first tick |
| **Same clock** | Both drivers share hit-stop (below). The pair holds both runners so one `Speed` change reaches both |
| **Same place** | Both runners warp toward `pairedOffset` and rotate to face each other over their own warp windows (spec 08) |
| **Same end** | Damage, death and facts resolve at the attacker's active window as for any hit, through `Receive`, with `HitInfo.isPaired` true so the reaction table is bypassed. If the pair is broken first, nothing resolves |

`BeginPair`:

1. Interrupts whatever the receiver is doing. An enemy's current action is aborted through its runner;
   the player's through `RequestInterrupt`.
2. Queues the reaction. Enemy: `EnemyStatus.SetReactionOverride(pairedReaction)` and the matching
   trigger fact, `isStaggered` for a counter, `isAlive = false` for a takedown, so the right branch in
   spec 02 takes over and plays the override. Player: the interrupt carries the action.
3. Releases the receiver's attack token, if any, through the director.
4. Registers a `Pair { attacker, receiver, attack, startedAt }` with the pipeline.

**Breaking a pair.** If either participant's runner is interrupted before the pair's active window (a
third enemy knocks the player down mid-takedown, or the receiver dies to something else), the pipeline
`Break`s the pair: the receiver's override is cleared and replaced by a plain Stagger through the normal
table, the attacker's interrupt proceeds as usual, and any pending kill is cancelled. A takedown that is
broken does not kill. The `isAlive = false` written at a takedown's start is therefore provisional, and
`Break` restores it; the Death branch must tolerate being aborted by that flip, which a reactive guard
with Stops Its Own Branch on already does.

Vulnerability during a pair is the existing `invulnerable` window on each action. The takedown has one
covering its whole duration; the counter has none, so a second attacker can still land a hit and break
the pair, which is what makes the multi-counter in the games a skill.

Only the counter action and the takedown action carry a `pairedReaction`. Ordinary strikes use the
reaction table.

### Reaction rules

| Requested | Receiver | Result |
|---|---|---|
| Flinch | normal | Flinch |
| Flinch | armored | none, damage still applies |
| Flinch or Stagger | stunned | Stagger |
| Stagger | armored | Flinch |
| Knockdown | any | Knockdown, unless `profile.canBeKnockedDown` is false, then Stagger |
| any | health <= 0 after damage | Death |
| any | already dead | ignored, `HitResult.landed = false` |
| Takedown | stunned or knocked down | Death, bypasses health |

Player as receiver uses the same table with a profile that has `canBeKnockedDown` true and no armor.

## Data

### HitInfo

| Field | Meaning |
|---|---|
| `attacker`, `receiver` | `IHitReceiver` references |
| `attack` | The `AttackDefinition` |
| `point`, `direction` | Receiver position and attacker-to-receiver direction, for feedback and knockback |
| `requestedReaction` | From the attack |
| `isCounter` | True when produced by the counter rule |
| `isPaired` | True when the hit resolves a registered pair; the reaction table is bypassed |

### HitResult

`landed`, `reactionApplied`, `damageDealt`, `killed`, `evaded`, `countered`.

### HitReceiverProfile (ScriptableObject)

| Field | Default |
|---|---|
| `maxHealth` | 100 |
| `armored` | false |
| `canBeKnockedDown` | true |
| `knockbackDistance` per reaction | Flinch 0.3 m, Stagger 0.8 m, Knockdown 1.5 m |
| `stunSeconds` | 3.0, used when this receiver is hit by a Stun action |

### IHitReceiver

| Member | Meaning |
|---|---|
| `Position`, `Forward` | For the land check and `HitInfo` |
| `IsAlive`, `CurrentState` | Read by the rules |
| `Receive(HitInfo) : HitResult` | The entry point |
| `OnHitLanded(HitResult)`, `OnWhiff(AttackDefinition)` | Attacker-side callbacks |
| `BeginPairedReaction(ActionDefinition, IHitReceiver attacker)` | Receiver-side: interrupt, queue the override, release the token |
| `ClearPairedReaction()` | Receiver-side: called by `Break`; falls back to a plain Stagger |
| `Runner` | The participant's `ActionRunner`, so the pair can observe interrupts and share hit-stop |

Two implementations: `EnemyHitReceiver` (writes through `EnemyStatus`) and `PlayerHitReceiver` (requests
the interrupt and drives the combo meter).

The pipeline itself is a single plain object bound `AsSingle` by the Combat static installer (spec 00)
together with the `ICombatEvents` it raises through. Receivers and runners receive it by injection on
their prefab contexts. In tests it is constructed directly with a fake events sink.

### Pair

`Pair { attacker, receiver, attack, startedAt }`, held by the pipeline in a small list. It subscribes to
both runners' interrupt events and calls `Break` on either. It is removed when the attacker's active
window resolves through `Receive` or when it is broken. At most one pair per participant at a time; a
`BeginPair` for a participant already in a pair breaks the old one first.

## Knockback and hit-stop

Knockback is a displacement the receiver's reaction action performs through its own displacement sink
(spec 03), using `knockbackDistance` along `HitInfo.direction` over the reaction action's `warp` window.
The pipeline only records the distance in the result; it moves nothing.

Hit-stop pauses both participants' presentation drivers for `attack.hitStopSeconds` (default 0.06) via
the driver's `Speed`, never through `Time.timeScale`, so the rest of the fight keeps moving. The
feedback listener does this; the pipeline raises the event. For a paired action the two participants are
the pair's, so a counter's hit-stop freezes both bodies in the same pose.

## Events for presentation

`HitLanded`, `HitEvaded`, `HitCountered`, `Whiffed`, `Died`, each carrying the `HitInfo` and
`HitResult`. The HUD, audio, VFX, camera shake and gamepad rumble subscribe. Nothing gameplay-side does.

## Tasks

| # | Task | Done when |
|---|---|---|
| T1 | `HitInfo`, `HitResult`, `HitReceiverProfile`, `IHitReceiver` | Types exist as tabled |
| T2 | `ReactionRules.Resolve` | Pure function. EditMode tests cover every row of the reaction table, including armored, stunned, death on zero health, dead-ignores, and takedown bypassing health |
| T3 | `HitResolver.TryLand` | Range and angle check against the target at window open. Tests: in range hits, out of range whiffs, behind the attacker whiffs, exactly at range hits |
| T4 | Invulnerability and counter rules | Tests: hit during an `invulnerable` window evades and raises the event; a late active window from a countered attacker is ignored; counter attempt on an unblockable attack is not even reachable because `incomingAttackCounterable` is false |
| T4b | `BeginPair`, `Pair`, `Break` | EditMode tests with fake receivers and runners: `BeginPair` interrupts the receiver, queues the override, releases its token and registers the pair; the active window resolves with `isPaired` and bypasses the table; interrupting either runner before that breaks the pair, clears the override to a plain Stagger and cancels a pending kill; a takedown's provisional `isAlive = false` is restored on break; a second `BeginPair` on a busy participant breaks the first |
| T5 | `EnemyHitReceiver` | Writes `health01` and exactly one reaction fact per hit through `EnemyStatus`; `isAlive` false on death and never true again. Tests drive it with a fake writer |
| T6 | `PlayerHitReceiver` | Requests the right interrupt with the spec 03 priority; combo meter increments on landed, countered and evaded; resets on taken and whiff. Tests use a fake context and meter |
| T7 | Runner integration | `ActionRunner` calls `TryLand` on `active` open and nothing on close, and `BeginPair` on `Play` of an action with a `pairedReaction`. PlayMode test with two capsules and duration-only actions: one hit per swing, none when the target is moved away before the window; a counter starts the receiver's reaction on the same frame |
| T8 | Feedback listener | Hit-stop through driver `Speed`, knockback through the reaction action, event fan-out. Manual check recorded in the review |

T1 to T6 are EditMode-only. T7 depends on spec 01's runner and spec 03's displacement sink.

## Open decisions

1. **Land check timing.** At window open, as specified. If the first cue sets make hits land visibly early,
   the check moves to a `hitFrame` normalized time inside the active window. One field, same test.
2. **Health on the player.** Present, so the HUD can show it and death can be demonstrated. Set high by
   default; the demo is about flow, not survival.

## Non-goals

- No hitbox or hurtbox colliders, layers or physics queries.
- No damage types, resistances or status effects beyond armor and stun.
- No friendly fire. Enemies never target each other, so the question does not arise.
- No ragdoll. Death is an action with a `Fall` cue.
