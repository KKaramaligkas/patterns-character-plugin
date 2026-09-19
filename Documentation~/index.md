# Patterns — Character Abilities

Drop-in movement, camera and character glue for Unity. Add abilities like lego, switch camera modes at
runtime, and tune everything from a data asset.

## Contents

1. [Installation](#installation)
2. [Getting started](#getting-started)
3. [Architecture](#architecture)
4. [Abilities](#abilities)
5. [Cameras](#cameras)
6. [Input](#input)
7. [Resources, health and damage](#resources-health-and-damage)
8. [Presentation](#presentation)
9. [Extending the package](#extending-the-package)
10. [Troubleshooting](#troubleshooting)

## Installation

**Package Manager → + → Install package from git URL**

```
https://github.com/KKaramaligkas/patterns-character-plugin.git
```

Alternatives: add by path (`+ → Add package from disk… → package.json`), copy the folder into `Packages/`,
or import the released `.unitypackage`/tarball.

Requirements: Unity 2021.3 LTS or newer. No third-party dependencies. The Input System adapter is
optional and activates itself when `com.unity.inputsystem` is present.

## Getting started

1. **GameObject ▸ Patterns Character ▸ Create Player — Third Person** (or any other preset).
2. Press Play.

Everything you need is created for you: capsule, motor, abilities, camera rigs, input source, camera target.

To set up an existing rigged model instead, select it and use
**GameObject ▸ Patterns Character ▸ Add Character Components To Selection**. The wizard keeps your
existing components (animator, renderer, scripts) and only adds what is missing.

**Tools ▸ Patterns Character ▸ Setup Scene ▸ Add Test Obstacle Course** creates ramps, steps, platforms,
a water volume and a ladder for testing.

### Where to tune

| What | Where |
|---|---|
| Speeds, jump, gravity, camera sensitivity, resources | `CharacterProfile` asset (**Tools ▸ Patterns Character ▸ Create Profile Asset**), then assign it on `CharacterContext` |
| Per ability behaviour | The ability component itself |
| Camera framing | The rig component (`ThirdPersonCameraRig`, `TopDownCameraRig`, …) |

Nothing is required to be assigned: without a profile the package uses built-in runtime defaults.

## Architecture

```
CharacterContext                (hub: input frame, ability ticking, references)
├── MotorBase                   (CharacterMotor or RigidbodyCharacterMotor)
├── CharacterLook               (camera yaw / pitch state)
├── CharacterResources          (health, stamina, energy)
├── CharacterCameraRigSwitcher  (camera modes)
│   ├── FirstPersonCameraRig
│   ├── ThirdPersonCameraRig
│   ├── TopDownCameraRig
│   └── FollowCameraRig
└── Abilities                   (LocomotionAbility, JumpAbility, DashAbility, …)
```

Four rules make the whole thing predictable:

**1. Abilities submit requests, the motor resolves them.**
```csharp
SubmitMotion(desiredVelocity, useGravity: true, drivesVertical: false, skipGroundSnap: false);
```
The highest priority request wins each frame. Locomotion is 0, jump 100, modes (fly / swim / ladder) 500,
dash 900. Abilities never talk to each other, so any of them can be added, removed or disabled at runtime.

**2. Abilities request tiers, not speeds.**
```csharp
locomotion.RequestTier(this, priority: 200, LocomotionTier.Crouch, speedMultiplier: 0.9f);
```
Requests expire after 0.25 s, so a destroyed or disabled ability can never leave the character stuck.
Locomotion resolves the winning tier and reads the actual speed from the profile.

**3. Input actions are claimed once.**
```csharp
if (Input.ConsumePressed(CharacterAction.Jump)) { … }
```
Jump, dash and interact can all look at the same frame without double firing.

**4. Execution order is explicit.** Abilities declare an `executionOrder` and the context ticks them in that
order (locomotion 0 → orientation 50 → sprint 100 → crouch 110 → glide 120 → aim 150 → jump 200 →
dash 300 → interact 350 → fly 400). The motor applies motion in `LateUpdate`, camera rigs after that.

## Abilities

| Ability | Notes |
|---|---|
| `LocomotionAbility` | Movement spaces: `World`, `CameraYaw`, `CameraBasis` (top-down), `CharacterYaw` (tank / FPS strafe), `WorldXY` (2D). Air control, jump momentum, slope following, local-space speeds for blend trees. |
| `OrientationAbility` | `FaceMovement`, `FaceInput`, `FaceCamera`, `Manual`. Constant angular speed or snappy exponential rotation, slope alignment, temporary overrides from other abilities. |
| `SprintAbility` | Forward-only gate, start delay, stamina drain, exhaustion state. |
| `CrouchAbility` | Capsule resize with transition speed, ceiling check that ignores your own colliders, camera target lowering, hold or toggle. |
| `JumpAbility` | Coyote time, jump buffer, `airJumps`, variable height with cut multiplier, ceiling check, stamina cost, `Jumped` / `JumpCut` events. |
| `DashAbility` | Input or facing direction, air dash budget, gravity suspension, physics push, exit momentum hand-back, i-frames flag. |
| `GlideAbility` | Reduced fall speed with blend, minimum clearance, stamina drain, `GlideRatio` for VFX. |
| `SwimAbility` | Water volumes with buoyancy, dive / surface, drag, currents, volume damage and exit impulse. Works when spawning inside water. |
| `LadderAbility` | Snap on with a dot-product test, position lock on the ladder face, top hop-off, orientation lock. |
| `FlyAbility` | `Fly` (collides) and `NoClip` (passes through) modes, camera-basis flight, hover, take-off / landing impulses. |
| `InteractionAbility` | Sphere cast detection from camera or body, prompt text, hold-to-interact progress, focus / blur events. |
| `AimAbility` | Strafe facing, slower tier, camera FOV / distance / shoulder blend, sensitivity reduction. |

Adding an ability is a file:

```csharp
public class WallRunAbility : ContinuousAbility
{
    protected override void OnTick(float deltaTime)
    {
        if (!DetectWall()) return;
        Motor.SetGravityScale(0.2f);                 // frame scoped
        SubmitMotion(transform.forward * 8f, false, true);
    }
}
```

Derive from `ContinuousAbility`, `InputTriggeredAbility` (press, cost, duration, cooldown) or `ToggleAbility`
(modes). The context finds it automatically, including when it is added at runtime.

## Cameras

`CharacterCameraRigSwitcher` owns the modes. Switch at runtime:

```csharp
context.CameraRig.SetRigById("Top Down");
context.CameraRig.NextRig();
context.CameraRig.RigChanged += (previous, current) => Debug.Log(current.rigId);
```

| Rig id | Behaviour |
|---|---|
| `First Person` | Eye camera, head bob by distance travelled, sprint FOV kick, strafe lean, occlusion push-out. |
| `Third Person` | Orbit with zoom, shoulder offset (static or aim only), obstruction handling that snaps in instantly and eases back out. |
| `Top Down` | Fixed / input / character yaw, perspective or orthographic, look-ahead, dead zone, edge scrolling, `Rotate90()`. |
| `Follow (2.5D)` | Axis mask, dead zone, look-ahead, fixed or look-at rotation, height lock. |

Look input is fed into `CharacterLook`, which clamps pitch (each rig can override the limits while active),
applies smoothing and writes the yaw / pitch pivots. Body rotation is a separate concern: use
`OrientationAbility` with `FaceCamera` for shooters and `FaceMovement` for third person adventures.

Aiming goes through `SetAimState(source, ratio, fovDelta, distanceScale, shoulderOffset)` and sensitivity
through `SetSensitivityScale(source, scale)`; both use the same expire-after-0.5 s rule so releasing the
button restores the previous state automatically.

Shake is trauma based:

```csharp
GetComponent<CameraShaker>().AddTrauma(0.6f);
CameraShaker.ShakeAt(explosionPosition, 25f, 0.8f);
```

`CameraShaker` can subscribe to the character's own events (hard landings, dashes, death).

## Input

Everything reads one `CharacterInputFrame` per frame, filled by an `ICharacterInputSource`.

- `LegacyInputSource` — keyboard and mouse through the classic Input Manager. Works out of the box.
- `InputSystemSource` (optional assembly) — direct device polling, or your own `InputActionAsset`.
- Write your own for touch, AI, networking or replays:

```csharp
public class TouchSource : CharacterInputSourceBehaviour
{
    public override void Poll(CharacterInputFrame frame, float deltaTime)
    {
        frame.move = stick.Value;
        frame.jumpPressed = jumpButton.PressedThisFrame;
        MarkActive();
    }
}
```

With `Auto` source selection the highest priority source wins, and a source that produced input in the last
3 seconds always beats one that did not, so keyboard and gamepad can be swapped mid-game.

### Default bindings

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | South button |
| Sprint | Shift | Left stick click |
| Crouch | C | East button |
| Dash | Ctrl | West button |
| Interact | E | North button |
| Fly toggle | F | — |
| Glide | Space (while falling) | South button |
| Camera mode | Tab | Select |
| Zoom | Wheel | — |
| Release cursor | Esc | — |

## Resources, health and damage

`CharacterResources` holds health, stamina and energy, each with its own regen rate and regen delay.

```csharp
resources.TrySpend(ResourceType.Stamina, 20f);   // all or nothing, used by jump / dash
resources.Drain(ResourceType.Stamina, 10f * dt); // continuous, used by sprint / glide
resources.TakeDamage(25f, attacker);
resources.Heal(10f);
resources.Died += r => { … };
```

Abilities use `activationCost` / `drainPerSecond` / `costResource` fields, so costs are configured in the
inspector rather than in code. Damage respects an invulnerability window and a global damage multiplier.

## Presentation

`CharacterAnimatorDriver` writes speed, local forward / right speeds, grounded, crouch ratio, aim ratio,
climbing, swimming, flying, dead and jump / land / dash triggers. Parameter names are editable, and
**Validate Parameters** in the context menu lists anything missing from your controller.

`CharacterAudioDriver` plays footsteps spaced by distance travelled (not by a timer, so cadence stays right
at every speed), plus jump, land, dash and swim sounds. Put a `SurfaceAudioSet` on your ground colliders to
get per-surface clips.

## Extending the package

| Goal | Do this |
|---|---|
| New mechanic | New `CharacterAbility` subclass |
| New input backend | New `CharacterInputSourceBehaviour` subclass |
| New camera style | New `CharacterCameraRig` subclass, implementing `ComputeTarget` |
| New movement model (physics, root motion, network prediction) | New `MotorBase` subclass, implementing `ProbeGround`, `CommitMotion`, `Teleport`, `Resize`, `SetCollisionEnabled` |
| Trigger zones | Add a `CharacterVolume` to a trigger collider (water, ladder, wind, lava, custom) |

## Troubleshooting

**The character does not move.** Check `Motor.BlockReason` and `Motor.HasRequest` in the motor inspector
while playing. `Locked` means something called `SetMovementLocked(true)`, `NoRequest` means no ability is
submitting motion (usually a disabled `LocomotionAbility`).

**The camera does not rotate.** The cursor must be locked (or the mode must not manage the cursor). Press
Play and click in the game view, or check `CharacterCameraRigSwitcher.manageCursor`.

**Crouch does not stand up.** Something is above the character; `CrouchAbility.BlockedFromStanding` reports it.

**The Input System source is missing from the Add Component menu.** The optional assembly only compiles when
`com.unity.inputsystem` is installed.

**Jump feels floaty.** Lower `gravity` magnitude or reduce `jumpHeight`; the profile inspector prints the
resulting launch velocity and air time.
