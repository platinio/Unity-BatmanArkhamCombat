# 08 — Presentation driver: procedural bodies, no authored animation

**Status:** task document for implementation. Replaces the "animation driver" decision left open in
spec 01 (task T6) and amends `ActionDefinition`. Also carries the paired-action amendment to specs 01
and 05.

## Goal

Make every action, reaction and movement in the demo readable on screen without a single authored
animation clip. The gameplay clock is a duration. The body is a child mesh that tweens bend. The root
transform, owned by the motor or the NavMeshAgent, is never touched by presentation.

## Context

- Spec 01's `ActionRunner` advances normalized time and fires window events. It asks a driver for time;
  it does not care where time comes from.
- Spec 01's driver interface is `Play`, `NormalizedTime`, `Speed`. It was written so the implementation
  could be swapped. This spec is the swap.
- Spec 03's motor and spec 02's NavMeshAgent own position and facing. Spec 05's hit-stop pauses the
  driver's `Speed`, never global time.
- There is no animator on the team. Mixamo-style clips remain a documented upgrade path, not a plan.

## Principles

1. **Duration is the clock.** An action has a `duration` in seconds. Normalized time is elapsed over
   duration, scaled by `Speed`. Windows are normalized, so nothing downstream changes if a clip is ever
   added later.
2. **Tweens touch the body child only.** Position, rotation, scale and colour of `Body` and its
   children. Never the root. Never the camera pivot. A test asserts the root does not move when a cue
   plays.
3. **Cues are data on the action.** A cue is a normalized time, a kind, and a few numbers. Designers tune
   the look next to the windows in the same asset, and the overlay from spec 01 draws cue marks on the
   same bar as the windows.
4. **Readability beats fidelity.** Telegraph is a colour pulse and a lean back. A counter is both bodies
   snapping toward each other. If it cannot be read on a capsule it is not a good cue.

## Amendment to `ActionDefinition` (spec 01)

| Field | Change |
|---|---|
| `clip` | Becomes optional. When present, `duration` is read from it and the clip driver may be used |
| `duration` | New, seconds, required when `clip` is null |
| `cues` | New, list of `PresentationCue` |
| `pairedReaction` | New, optional `ActionDefinition` the receiver plays instead of the reaction table's choice (counters, takedowns) |
| `pairedOffset` | New, local offset from attacker to receiver that both warp toward over the warp window when `pairedReaction` is set |

### PresentationCue

| Field | Meaning |
|---|---|
| `at` | Normalized time the cue fires |
| `kind` | A `[SerializeReference, SubclassSelector] ICueKind`, picked from a dropdown; one of the kinds below |
| `duration` | Seconds the tween runs |
| `strength` | Scalar; metres for leans, factor for scales, 0..1 for colour |
| `ease` | DOTween ease |
| `colour` | For colour cues |

The `cues` list is therefore polymorphic: each kind is its own class and may carry extra fields of its
own (a `Spin` has a turn count, a `Punch` names its child). SerializeReferenceExtensions provides the
dropdown; no enum, no switch.

### Cue kinds, first pass

| Kind | What it does to `Body` |
|---|---|
| `Lean` | Tilts toward the facing (or away for negative strength) and back |
| `Squash` | Scales Y down and XZ up by `strength`, then back |
| `Stretch` | The inverse |
| `Punch` | Pushes a `Fist` child forward and back, or `Body` itself if none |
| `Flash` | Lerps the body material's emission to `colour` and back |
| `Pulse` | Repeating flash for `duration`, used for telegraphs |
| `Crumple` | Scales Y down and tilts, holds until the action ends |
| `Fall` | Rotates the body to horizontal over `duration`, holds |
| `Rise` | The inverse of `Fall` |
| `Spin` | Yaws the body a full turn, for the roundhouse |

A cue kind is a small class implementing `ICueKind` with `Play(Body, cue)` returning the tween. Adding a
kind is one class, and it appears in the dropdown on every action asset without any registration.

## The driver

```text
IPresentationDriver
  Play(ActionDefinition action)      resets time, kills running tweens, schedules the cues
  Stop()                             kills tweens, returns Body to rest over a short blend
  NormalizedTime                     elapsed / duration, read by the runner
  Speed                              1 normally; the hit-stop listener sets it to 0 briefly
  Tick(deltaTime)                    advances elapsed by deltaTime * Speed; fires cues whose `at` is crossed
```

Two implementations:

| Driver | When |
|---|---|
| `ProceduralPresentationDriver` | Always, for this demo. Owns `Body`, the cue scheduler and the locomotion visuals below |
| `ClipPresentationDriver` | Documented, not built. Reads time from an Animator or a Playable, still plays cues. The upgrade path if clips ever exist |

Cues fire from `Tick`, not from DOTween's own clock, so hit-stop and interrupts stop them exactly where
gameplay stopped. A cue's tween, once started, runs on its own; `Stop` kills it.

### Rest pose and blends

`Body` has a rest local pose. `Stop` and the end of every action tween back to it over `restBlendSeconds`
(default 0.12). Reactions that hold (`Crumple`, `Fall`) are released by the next `Play` or `Stop`.

## Locomotion visuals

Procedural, inside the driver, active when no action is playing:

| Signal | Effect on `Body` |
|---|---|
| Planar speed | Forward lean up to `maxLean` degrees at sprint; vertical bob at a frequency scaled by speed, amplitude a few centimetres |
| Turn rate | Roll into the turn, a few degrees |
| Airborne | Slight stretch |
| Idle | Slow breathing scale, barely visible |

The player's signals come from the motor; an enemy's from its NavMeshAgent velocity. Same code, two
signal sources, one small interface.

## Bodies

| Who | Shape | Look |
|---|---|---|
| Player | Capsule, dark grey, a short `Cape` child quad that leans opposite to motion | A `Fist` child sphere for `Punch` cues |
| Thug | Capsule, red | |
| Knife | Thinner capsule, orange, a `Knife` child box | `Pulse` colour differs for unblockable telegraphs |
| Armored | Wider capsule, steel blue, a `Shield` child | `Flash` on ignored flinches so the player learns armor |
| Elite variants | Same shape, brighter material | Data variant only |

A `FaceMarker` child on every body shows facing, since a capsule has none.

## Telegraphs and prompts

The attack's telegraph window (spec 01) is where the `Pulse` cue sits, coloured by `counterable`
(white) or `unblockable` (red). The HUD prompt from the shell reads the same facts. Enemies therefore
read the same way on the body and on the HUD.

## Paired actions (amendment to specs 01 and 05)

One move, two bodies. The rules live in spec 05's Paired actions section; what the driver and runner
owe them:

- **Same start.** The receiver's `pairedReaction` is played on the frame the attacker's action starts,
  via `BeginPair`, not when the hit lands. Both drivers therefore begin their cue schedules together,
  and a cue at the same normalized time fires on both bodies in the same frame.
- **Same place.** Both runners warp toward `pairedOffset`: the attacker toward `receiver - offset`, the
  receiver toward `attacker + offset`, each through its own displacement sink over its own warp window.
  Both also rotate to face each other over the same window, since an offset without facing leaves the
  enemy countered from behind. On capsules that is the two bodies snapping together and squaring up,
  which reads as a grapple.
- **Same clock.** Hit-stop from the pair's hit sets `Speed` on both drivers, so both freeze mid-cue.
- **Breaking.** When spec 05 breaks the pair, the receiver's driver gets a `Stop` followed by the plain
  stagger's `Play`; the attacker's driver gets whatever its interrupt plays. Neither body is left holding
  a `Crumple` or `Fall` for a move that no longer exists.
- The counter action and the takedown action carry a paired reaction. Ordinary strikes do not.
- The finisher camera from spec 07 triggers on the takedown's `Play`, and releases on its end or break.

## Dependencies

DOTween, used only by cue kinds, locomotion visuals and the HUD. A test assembly rule, in the shape of
the module boundary tests, asserts that no gameplay assembly references it.

## Debug

The spec 01 overlay gains cue marks on each action bar, coloured by kind, so a cue that fires late or
on the wrong frame is visible next to the window it should align with.

## Tasks

| # | Task | Done when |
|---|---|---|
| T1 | `ActionDefinition` amendment | `duration`, optional `clip`, `cues`, `pairedReaction`, `pairedOffset`; validation requires one of clip or duration |
| T2 | `IPresentationDriver` and `ProceduralPresentationDriver` core | Time, speed, cue scheduling from `Tick`, `Stop`, rest blend. EditMode tests with a fake tween factory: cues fire once each at the right normalized time, none fire while `Speed` is 0, `Stop` kills pending cues |
| T3 | Cue kinds | The ten listed. PlayMode test plays each on a body and asserts the root transform did not move |
| T4 | Locomotion visuals | Lean, bob, roll, airborne stretch, idle breathing from a speed signal interface; motor and agent sources. Manual check |
| T5 | Bodies | Five prefabs as tabled with `Body`, `FaceMarker` and props |
| T6 | Paired actions | Dual warp and mutual facing in both runners; both drivers start together on `BeginPair`. PlayMode test: a counter brings both bodies within the offset tolerance and within a few degrees of facing each other by the end of the warp window, and a cue at the same normalized time fires on both in the same frame; breaking the pair returns the receiver's body to rest then into the stagger |
| T7 | Telegraph pulse | `Pulse` colour chosen from the attack flags. Test asserts the colour |
| T8 | Boundary test | No gameplay assembly references DOTween, and `ArkhamCombat.Combat` references neither DOTween nor Zenject |
| T9 | Overlay cue marks | Marks drawn and labelled |
| T10 | Starter action set | Durations, windows and cues authored for: Jab L/R, Cross, Roundhouse, GlideKick, Counter, Evade, Stun, Takedown, Haymaker, KnifeSlash, HeavySwing, Flinch, Stagger, Knockdown, GetUp, Death, Taunt, Dodge, StunnedLoop. Each plays end to end in the test scene |

T1 and T2 unblock every other spec's PlayMode test, since they all need an action that advances time.

## Open decisions

1. **Cue timing source.** From the driver's `Tick`, as specified, so hit-stop freezes cues. The
   alternative, letting DOTween run cues on its own clock, is simpler and wrong.
2. **Clip upgrade path.** Documented only. If clips appear, `ClipPresentationDriver` reads time from them
   and the cues keep working as accents on top.

## Non-goals

- No authored animation clips, skeletons, Animator controllers or retargeting.
- No ragdolls, IK or cloth. The cape is a quad with a lean.
- No per-frame procedural animation beyond the signals listed. If it needs a skeleton, it is out.
