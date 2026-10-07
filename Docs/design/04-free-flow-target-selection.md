# 04 — Free-flow target selection

**Status:** task document for implementation. Deliberately small.

## Goal

When the player presses Strike, Counter, Stun or Evade, pick the enemy the action applies to. The pick
must favour the direction the player is pushing, reward hitting the enemy who is about to attack, and not
flip between two equally placed enemies mid-chain.

## Context

There is no awareness in this demo. Every unit knows every other unit from the moment it exists, nothing
hides, nothing flees, and a fight starts the instant enemies spawn. The encounter director's registration
list is therefore the complete set of candidates, handed over by reference. No perception, no sensors,
no scoring framework. `EntityTargetSelection` and `AIPerception` are not used.

## The picker

One pure function, `FreeFlowTargetPicker.Pick(request, candidates, settings)`, with no state of its own
beyond what the request carries.

### Request

| Field | Source |
|---|---|
| `origin`, `forward` | Player transform |
| `cameraForward` | Combat camera, flattened to the ground plane |
| `moveAtPress` | The intent being resolved (spec 01). Zero when the player pushed nothing |
| `intent` | Strike, Counter, Stun or Evade |
| `currentTarget` | The enemy the previous action hit, or null |

The pushed direction is `moveAtPress` rotated into world space by `cameraForward`. When `moveAtPress` is
zero the pushed direction is the player's `forward`.

### Gates, in order, first failure skips the candidate

| Gate | Rule |
|---|---|
| Alive | `isAlive` fact true |
| Reachable | Distance at or below `settings.maxRange` (glide-kick range) |
| Intent fits state | Strike skips a knocked-down enemy unless `settings.allowGroundStrikes`; Counter accepts only an enemy whose `telegraphing` fact is true; Stun accepts any; Evade ignores candidates entirely and returns null |

### Score, higher wins

```text
score = angleWeight     * (1 - angle / 180)                angle between pushed direction and direction to enemy
      + distanceWeight  * (1 - distance / maxRange)
      + telegraphBonus  * (telegraphing ? 1 : 0)
      + stickyBonus     * (candidate == currentTarget ? 1 : 0)
```

Defaults: `angleWeight` 1.0, `distanceWeight` 0.35, `telegraphBonus` 0.5, `stickyBonus` 0.15. With a
pushed direction the angle term dominates. With none, the player's forward stands in and distance decides
among enemies in front. The telegraph bonus is large enough that an attacking enemy slightly off-axis
beats an idle one on-axis, which is the Arkham feel. The sticky bonus only breaks near-ties.

An enemy directly behind the player scores near zero on angle and is still pickable when it is the only
one telegraphing, which is the correct behaviour for Counter.

### Result

`PickResult { target, distance, side }` where `side` is -1, 0 or 1 for left, centre, right relative to the
player's forward. Spec 01's `CombatContext` publishes `targetDistance`, `targetSide` and
`targetBeyondLunge` from it before the resolver runs.

## Announcing the pick

The moment a Strike resolves to a target, the picker's caller tells that enemy it is about to be hit.
That is the `playerStrikeIncoming` fact in spec 02, written through the enemy's `ThreatSensor`, and it is
what the knife enemy's Dodge branch reads. The sensor clears it when the strike's active window closes,
whether it landed or not. Counter and Stun do not announce; their targets are already committed to their
own action.

## Settings

`FreeFlowTargetSettings`, referenced from `CombatConfig.targeting` (spec 03):

| Field | Default |
|---|---|
| `maxRange` | 7 m |
| `angleWeight`, `distanceWeight`, `telegraphBonus`, `stickyBonus` | as above |
| `allowGroundStrikes` | false |
| `sideDeadzoneDegrees` | 15, inside which `side` is 0 |

## Debug

A gizmo, on while the overlay from spec 01 is on, draws a line from the player to each candidate coloured
by score, the pushed direction as an arrow, and the chosen target with a ring. Scores are labelled. This
is a `MGizmos` draw list fed from the last `Pick`, nothing more.

## Tasks

| # | Task | Done when |
|---|---|---|
| T1 | `FreeFlowTargetSettings` and `PickResult` | Asset and struct exist; defaults as tabled |
| T2 | `FreeFlowTargetPicker.Pick` | EditMode tests with fake candidates: pushed direction wins over distance; telegraphing enemy off-axis beats idle on-axis; sticky bonus breaks a tie and loses a clear contest; Counter returns null when nobody telegraphs; Evade returns null; knocked-down skipped unless allowed |
| T3 | Context publication | `targetDistance`, `targetSide`, `targetBeyondLunge` written through `AgentVariableWriter` after each pick. Test asserts the three writes |
| T4 | Announcement | Resolving a Strike sets `playerStrikeIncoming` on the target's `ThreatSensor`; the sensor clears it when the active window closes. PlayMode test with a placeholder clip |
| T5 | Gizmo | Lines, arrow, ring, labels, gated by the overlay toggle |

## Non-goals

- No perception, memory, or line of sight. Everyone sees everyone.
- No stickiness over time, commitment windows, or trace. The sticky bonus is the entire anti-flip measure.
- No lock-on. If a lock-on mode returns, it feeds `currentTarget` and raises `stickyBonus`; the picker
  does not change.
