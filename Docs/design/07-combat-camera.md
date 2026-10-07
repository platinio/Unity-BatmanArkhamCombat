# 07 — Combat camera

**Status:** task document for implementation. **Amended 2026-10-06:** the demo is combat only, so there is no exploration mode and no mode blend. The camera is always the combat framing: high, far, pitched down at the character, and not steered by look input. The rig keeps an `ICameraFramingSource` seam (the exploration source survives only for the controller module's own test scene); the director's roster plugs into `GroupFramingSource`. Read "Mode switching" and "Look input always wins" below as superseded. Extends `PlayerCameraRig` and `CameraRigConfig` from the
copied controller (spec 03). Supersedes the "two Cinemachine cameras blended by priority" sketch in
spec 03's Camera section: one virtual camera, one rig, two framing modes.

## Goal

Keep the whole fight on screen without taking the camera away from the player. Pull back and centre when
enemies are present, drift the look point toward the group, follow the spread with distance, and give
look input absolute priority. Ease back to the over-the-shoulder exploration camera when the encounter
ends.

## Context

- `PlayerCameraRig` already owns yaw, pitch, distance and FOV, positions a root-level pivot every
  `LateUpdate`, and pushes values onto a `CinemachineThirdPersonFollow`. Speed framing (sprint pulls
  back and widens FOV) is a parameter blend inside the rig. The combat mode is the same mechanism with a
  second parameter set.
- `CameraRigConfig` holds every tunable; the rig reads it and writes nothing back to the vcam asset.
- The director roster (spec 06, `Agents` and `IsActive`) is the complete set of enemies. There is no
  perception, so "enemies in the fight" is exactly that list.
- Every combat intent is stamped with the move vector at press time (spec 03), so strike direction is
  immune to camera motion. Continuous movement is not, which is why the latched movement frame below
  exists.
- Spec 04 (stick direction) and spec 06 (token grant front term) both read the camera's flattened
  forward. The rig publishes it.

## Principles

1. **Look input always wins.** Any look input this frame cancels auto-yaw, and auto-yaw stays off for a
   configured idle time afterwards.
2. **Batman is biased, never centred, never lost.** The pivot moves toward the group, capped by a maximum
   offset from the character, so the character cannot be pushed off screen by a wide ring.
3. **One vector for three systems.** The rig's flattened forward is what the picker, the director and
   the movement frame all read. They never compute their own.
4. **Blend parameters, not cameras.** Mode switches smooth the rig's parameters over a blend time. No
   second virtual camera for the base combat camera.

## Framing modes

| Parameter | Exploration (today) | Combat |
|---|---|---|
| Pivot position | character + `pivotHeight` | character + `combatPivotHeight` + clamp(centroid offset * `centroidWeight`, `maxPivotOffset`) |
| Shoulder offset | `shoulderOffset` | zero |
| Distance | `distance`, `sprintDistance` by speed | `Lerp(combatDistanceMin, combatDistanceMax, groupRadius / groupRadiusForMaxDistance)` |
| Pitch range | `pitchMin` .. `pitchMax` | `combatPitchMin` .. `pitchMax` |
| Auto-yaw | none | toward the centroid bearing at `autoYawDegreesPerSecond` while look is idle |
| FOV | `idleFov` .. `sprintFov` by speed | `combatFov`, fixed |
| Obstacle avoidance | on | on, unchanged |

Group centroid and radius are computed each frame from the roster positions: centroid is the mean of
enemy positions, radius is the largest planar distance from the character to an enemy. A roster of one
gives a centroid at that enemy and a radius equal to its distance, which frames a duel correctly.

### Mode switching

`mode = director.IsActive ? Combat : Exploration`. Each parameter smooth-damps toward its mode's target
with `modeBlendSeconds` (default 0.6 in, 1.2 out), so the camera eases out when the last enemy drops
instead of snapping to the shoulder. Speed framing is disabled in combat.

### Auto-yaw rules

- Target yaw is the bearing from the character to the centroid.
- Rotation speed is `autoYawDegreesPerSecond` (default 25), scaled down to zero inside
  `autoYawDeadzoneDegrees` (default 20) of the target so the camera settles rather than hunts.
- Suppressed while `LookDelta` is non-zero and for `autoYawResumeSeconds` (default 2.0) afterwards.
- Suppressed entirely when `autoYaw` is off in the config.

## Camera-relative input under a moving camera

The movement frame spec 03 kept is implemented as a `LatchedMovementFrame` wrapping the camera frame:

| Condition | Behaviour |
|---|---|
| Move stick released (magnitude below the deadzone) | Frame re-samples the camera forward every frame |
| Move stick held | Frame holds the forward it had when the hold began |
| Held stick direction changes by more than `relatchDegrees` (default 35) | Frame re-samples once and latches again |
| Mode is Exploration | Latching is off; behaves as the plain camera frame does today |

This means auto-yaw and centroid drift never bend the character's path mid-hold, while a deliberate
stick change still reads against the camera the player is looking through. The frame is owned by the
rig and handed to `CharacterContext.MovementFrame` when combat starts; the plain frame is restored on
exit.

## Published forward

`PlayerCameraRig.CombatForward`: the pivot's forward with Y removed, normalised, updated at the end of
`LateUpdate`. Also published as the agent fact `cameraForward` so Functions can read it if a guard ever
needs it. Readers:

- spec 04's picker rotates `moveAtPress` by it.
- spec 06's host passes it to `SetTarget(position, forward)` for the token front term.
- the latched movement frame samples it.

## Impact feedback

A `CinemachineImpulseSource` on the rig's pivot, fired by the hit pipeline's events (spec 05):

| Event | Impulse |
|---|---|
| `HitLanded` with reaction Flinch | none |
| `HitLanded` with Stagger or Knockdown | small, `impulseHeavy` |
| `HitCountered` | medium, `impulseCounter` |
| Player receives Knockdown | medium, `impulsePlayerHit` |

Impulses are presentation and read nothing gameplay-side. A `screenShake` toggle in the config disables
them all.

## Config additions (`CameraRigConfig`)

| Field | Default |
|---|---|
| `combatPivotHeight` | 1.3 m |
| `centroidWeight` | 0.3 |
| `maxPivotOffset` | 2.0 m |
| `combatDistanceMin`, `combatDistanceMax` | 4.0 m, 7.0 m |
| `groupRadiusForMaxDistance` | 8.0 m |
| `combatPitchMin` | -10° |
| `combatFov` | 60° |
| `autoYaw`, `autoYawDegreesPerSecond`, `autoYawDeadzoneDegrees`, `autoYawResumeSeconds` | on, 25, 20, 2.0 |
| `modeBlendInSeconds`, `modeBlendOutSeconds` | 0.6, 1.2 |
| `relatchDegrees` | 35° |
| `screenShake`, `impulseHeavy`, `impulseCounter`, `impulsePlayerHit` | on, 0.3, 0.5, 0.6 |

## Tasks

| # | Task | Done when |
|---|---|---|
| T1 | Config additions | Fields and accessors as tabled; existing exploration values untouched |
| T2 | Group framing math | Pure static `GroupFraming.Compute(characterPos, enemyPositions, settings)` returning pivot offset, distance and centroid bearing. EditMode tests: one enemy, ring of eight, enemy far beyond max radius clamps offset and distance, empty roster returns exploration values |
| T3 | Mode blend | Rig reads `director.IsActive`, smooth-damps every parameter with the in and out times, disables speed framing in combat. PlayMode test toggles a fake roster and asserts distance and shoulder offset converge within the blend time |
| T4 | Auto-yaw | Rules as specified. PlayMode test: yaw converges toward the centroid bearing with no look input, stops inside the deadzone, halts the frame look input arrives and stays halted for the resume time |
| T5 | `CombatForward` and the `cameraForward` fact | Published at end of `LateUpdate`; the picker and the director host read it. Test asserts the fact matches the pivot's flattened forward |
| T6 | `LatchedMovementFrame` | Behaviour table above. EditMode tests with a scripted camera forward and scripted stick: held stick keeps the frame while the camera rotates; release re-samples; a 40° stick change re-latches once; exploration mode never latches |
| T7 | Frame handoff | Rig swaps `CharacterContext.MovementFrame` on mode change and restores on exit. PlayMode test: holding forward while a fake auto-yaw turns the camera 60° keeps the character's heading within 2° |
| T8 | Impulse | Impulse source and the event subscriptions; `screenShake` off disables them. Manual check in the review |
| T9 | Arena check | Three enemies on the ring, player circling: no enemy leaves the frame for more than a second, character never leaves the central 60% of the screen horizontally. Recorded as a capture; the second condition is also asserted by a PlayMode test projecting the character through the camera |

T2 and T6 are pure math with EditMode tests. T3 and T4 depend on spec 06's `IsActive` and roster. T7
depends on spec 03's context. T8 depends on spec 05's events.

## Open decisions

1. **Auto-yaw** ships on at a low speed with a config toggle. Off if testers dislike it; no code change.
2. **Finisher camera** for the 8x takedown: a second virtual camera with a priority blend, triggered by
   the Takedown action's start and released at its end. Stretch; not part of the tasks above.
3. **Centroid weight** at 0.3 is a guess at Arkham's bias. Expect to tune it with the arena check.

## Non-goals

- No second virtual camera for the base combat camera.
- No lock-on camera. If lock-on returns, it is a third parameter set on the same rig.
- No cinematic cuts, slow motion or letterboxing.
- No per-enemy framing weights. All roster members count equally in the centroid.
