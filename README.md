# Patterns — Character Abilities

A drop-in character framework for Unity: **walk, run, sprint, crouch, jump, dash, glide, swim, climb, fly** plus a full **camera suite** (first person, third person, top-down/isometric, 2.5D follow) that all work together, in any genre, with **zero third-party dependencies** and **zero setup**.

```
GameObject ▸ Patterns Character ▸ Create Player - Third Person
```

…and press Play. That is the whole installation procedure.

---

## Why this exists

Most character controllers hard-code one genre. Movement lives in `Update()` of one big class, the camera is welded to the player, and every new mechanic (dash, swim, flight) is another `if` in the same file until nobody dares touch it.

This package is built from three patterns instead:

| Pattern | What it buys you |
|---|---|
| **Ability components** | Every mechanic is its own small MonoBehaviour. Add or remove abilities like lego. |
| **Priority request bus** | Abilities never fight. Dash (900) beats jump (100) beats locomotion (0), decided in one place. |
| **Data-driven profile** | All tuning lives in a `CharacterProfile` asset, so designers tune without touching code. |

Everything else (tiers, camera modes, input sources) is layered on those three ideas.

---

## Install

**Package Manager → + → Install package from git URL**

```
https://github.com/KKaramaligkas/patterns-character-plugin.git
```

Or clone/download and add by path (**+ → Add package from disk… → `package.json`**), or copy the folder into `Packages/`.

Works on **Unity 2021.3 LTS and newer** (tested on Unity 6). No dependencies in `package.json`: the Input System source assembly only compiles when the Input System package is present, and everything else runs on the classic input manager.

---

## Quick start

### 1. Create a character

Menu: **GameObject ▸ Patterns Character**

| Entry | Builds |
|---|---|
| Create Player — First Person | FP camera, CharacterYaw movement, crouch, jump, dash, interact |
| Create Player — Third Person | FP + TP camera rigs, camera-relative movement, crouch, jump, dash |
| Create Player — Top Down | Top-down/isometric rig, camera-basis movement, jump |
| Create Player — 2.5D Side Scroller | Follow rig with dead zone, world-plane movement |
| Create Player — Everything | All four rigs plus fly, glide, swim and ladder abilities |
| Add Character Components To Selection | Upgrades an existing rigged model |
| Setup Scene (Ground, Light, Volumes) | Ground, light, ramps, steps, water, ladder, test course |

### 2. Play

Default bindings (keyboard and mouse):

| Action | Key |
|---|---|
| Move | `WASD` / arrows / left stick |
| Look | Mouse / right stick |
| Jump (double jump, hold for higher) | `Space` |
| Sprint | `Shift` |
| Crouch (tap to toggle) | `C` |
| Dash | `Ctrl` |
| Interact | `E` |
| Fly / noclip toggle | `F` |
| Glide | `Space` while falling |
| Switch camera mode | `Tab` |
| Zoom | Mouse wheel |
| Release cursor | `Esc` |

### 3. Use your own input

Drop an `InputActionAsset` on the `InputSystemSource` component and name the actions
`Move`, `Look`, `Jump`, `Sprint`, `Crouch`, `Dash`, `Interact`, `Fly`, `Glide`, `Primary`, `Secondary`, `ToggleCamera`, `Zoom`, `Cancel`.
Anything not found falls back to nothing; the built-in device bindings are used when no asset is assigned.

---

## What is in the box

### Core

| Type | Role |
|---|---|
| `CharacterContext` | The hub. Owns input, ticks abilities in order, exposes motor / look / resources / camera / anchors. |
| `MotorBase` + `CharacterMotor` | CharacterController motor: gravity, slopes, steps, ground snapping, moving platforms, impulses. |
| `RigidbodyCharacterMotor` | Physics motor for pushing rigidbodies and being pushed, same API. |
| `CharacterProfile` | ScriptableObject with every tuning value, plus runtime defaults when none is assigned. |
| `CharacterResources` | Health / stamina / energy with regen, spend gating, damage, death and events. |
| `CharacterLook` | Camera yaw / pitch state with limits, smoothing and pivot support. |
| `CharacterInputFrame` | One frame of device-agnostic input, with `ConsumePressed` so only one ability claims a press. |
| `ICharacterInputSource` | Plug in touch, AI, network or replay input without touching a single ability. |
| `MovingPlatform` | Ride moving and rotating platforms, with conveyor speed multipliers. |
| `CharacterVolume` | Trigger volumes for water, ladders, wind, lava and custom zones. |

### Abilities

| Ability | Highlights |
|---|---|
| `LocomotionAbility` | Tier based speeds, camera / character / world / screen movement spaces, air control and jump momentum. |
| `OrientationAbility` | Face movement, input, camera or manual; snap or smooth; slope alignment; temporary mode overrides. |
| `SprintAbility` | Forward-only sprinting, stamina drain, start delay, tier priority below crouch. |
| `CrouchAbility` | Capsule resize, ceiling check, camera target lowering, hold or toggle. |
| `JumpAbility` | Coyote time, jump buffering, multiple air jumps, variable height, ceiling check, jump-cut events. |
| `DashAbility` | Ground and air dashes, i-frames, physics push, momentum hand-back so the exit is not a dead stop. |
| `GlideAbility` | Reduced fall speed, distance based cadence, stamina drain, blend for VFX. |
| `SwimAbility` | Water volumes, buoyancy, dive / surface, drag, currents, volume damage, exit impulse. |
| `LadderAbility` | Snap on, climb, position lock, top hop-off, orientation lock. |
| `FlyAbility` | Flight and noclip, camera-basis flight, hover, take-off and landing impulses. |
| `InteractionAbility` | Sphere cast detection, prompt text, hold-to-interact, focus events, camera origin. |
| `AimAbility` | Strafe facing, slower movement, camera FOV / distance / shoulder pull-in, sensitivity reduction. |

### Camera

| Rig | Use it for |
|---|---|
| `FirstPersonCameraRig` | Eye camera with head bob, speed FOV kick, strafe lean, occlusion push-out. |
| `ThirdPersonCameraRig` | Orbit with zoom, shoulder offset, aim blend, obstruction handling with instant snap-in. |
| `TopDownCameraRig` | Fixed or rotating yaw, perspective or orthographic, look-ahead, dead zone, edge scrolling, 90° rotations. |
| `FollowCameraRig` | 2.5D / side scroller: axis mask, dead zone, look-ahead, fixed or look-at rotation. |
| `CharacterCameraRigSwitcher` | Mode switching with blends, cursor control, aim requests, sensitivity scaling. |
| `CameraShaker` | Trauma-based shake wired to landings, dashes and damage; `ShakeAt` for scene-wide impact. |

Write your own rig by deriving from `CharacterCameraRig` and implementing `ComputeTarget`.

### Presentation

`CharacterAnimatorDriver` (parameter names configurable, validates against your controller) and
`CharacterAudioDriver` (distance-based footsteps, surface sets via `SurfaceAudioSet`, jump / land / dash / swim).

---

## How the patterns fit together

**1. Abilities submit, the motor decides.** Every ability that moves the character pushes a request:

```csharp
SubmitMotion(desiredVelocity, useGravity: true, drivesVertical: false);
```

The motor takes the highest priority request each frame and applies the movement. Locomotion is priority 0, jump 100, dash 900, and modes (fly / swim / ladder) sit at 500. Nothing needs to know about anything else, so abilities can be added, removed or disabled at runtime.

**2. Tiers, not speeds.** Sprint, crouch, swim, gliding and aiming do not set a speed, they request a *tier*:

```csharp
locomotion.RequestTier(this, priority: 200, LocomotionTier.Crouch, speedMultiplier: 0.9f);
```

Locomotion resolves the winner (requests expire after 0.25 s, so a destroyed ability can never leave you stuck) and reads the speed from the profile. Animator, footstep cadence and stamina all stay consistent because they read the same tier.

**3. Actions are claimed, not polled twice.** `Input.ConsumePressed(CharacterAction.Jump)` returns true once per press. Jump, dash and interact can safely all look at the same frame.

**4. Everything is data.** `CharacterProfile` holds speeds, feels, gravity, camera sensitivity, resource rules. Menu: **Tools ▸ Patterns Character ▸ Profiles ▸ Realistic / Arcade / Floaty**.

---

## Extending

### A new ability

```csharp
public class GrappleAbility : InputTriggeredAbility
{
    protected override void OnTrigger()
    {
        if (!TryPayCost()) return;                 // spends activationCost of costResource
        Motor.AddImpulse(aimDirection * 18f, true); // uses the impulse channel
        StartCooldown(1.5f);
    }
}
```

Drop it on the character. That is all — the context finds it, sorts it by `executionOrder` and ticks it.

- `ContinuousAbility` for always-on abilities (locomotion, modifiers).
- `InputTriggeredAbility` for press-to-fire with cost / duration / cooldown.
- `ToggleAbility` for modes like flight.

### A new input source

```csharp
public class TouchInputSource : CharacterInputSourceBehaviour
{
    public override void Poll(CharacterInputFrame frame, float deltaTime)
    {
        frame.move = myVirtualStick.Value;
        frame.jumpPressed = jumpButton.WasPressedThisFrame;
        frame.fromVirtualDevice = true;
        MarkActive();
    }
}
```

Higher `Priority` wins in `Auto` mode; a source that produced input in the last 3 seconds always beats one that did not.

### A custom motor

Derive from `MotorBase` and implement `ProbeGround`, `CommitMotion`, `Teleport`, `Resize` and `SetCollisionEnabled`. Every ability works unchanged, including network-predicted or root-motion driven motors.

---

## Samples

**Window ▸ Package Manager ▸ Patterns — Character Abilities ▸ Samples ▸ Import**

- `CharacterBootstrap` — builds a complete character from code.
- `SuperJumpAbility` — a custom ability with cost, cooldown and ability lookup.
- `WanderAIInputSource` — AI driving the same abilities as the player.
- `CharacterGameplayExample` — camera hotkeys, teleport, movement lock, damage and knockback.

---

## Tests

The package ships runtime tests (edit mode and play mode): maths, input claiming, profiles, resources, ability ordering.
Run them from **Window ▸ General ▸ Test Runner** (the package must be listed under *testables* in `Packages/manifest.json`:

```json
"testables": [ "com.patterns.character" ]
```

---

## Requirements & notes

- Unity 2021.3 LTS or newer. Tested with Unity 6 and the Input System 1.20.
- No dependencies. The Input System adapter is optional and gated by an assembly definition `versionDefines` + `defineConstraints` pair.
- Collision uses whatever layers you put in the masks; the wizard leaves them at `Everything` and the motor layer masks are exposed for tuning.
- Legs, arms and IK are deliberately out of scope: this package is about movement, camera and glue code, not rigs.

## License

MIT. See `LICENSE.md`.
