# Changelog

All notable changes to this package are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-09-19

### Added
- Core: `CharacterContext`, `CharacterProfile`, `MotorBase` + `CharacterController` / `Rigidbody` motors, priority-based motor request bus, ground probe with slope / step / moving-platform support, external impulse handling.
- Input: device-agnostic `IInputSource` abstraction, legacy Input Manager source, optional new Input System source (auto-enabled through an assembly definition `versionDefines` gate), action consume semantics, jump buffering.
- Abilities: locomotion, sprint, crouch, jump (coyote time, air jumps, variable height), dash, fly / noclip, swim, ladder climb, glide, interaction, aim.
- Camera: first person, third person orbit (with obstruction handling, zoom, shoulder offset), top-down / isometric, 2.5D follow, fixed, plus rig switching with blending, shake and cursor control.
- Presentation: animator driver (parameter-name mapped), audio driver (footsteps by surface, jump / land / dash), event hooks.
- Editor: character rig wizard for first person / third person / top-down / 2.5D, profile inspector with tuning validation, runtime gizmos.
- Tests: runtime edit-mode test assembly covering math helpers, resources, ability state machine and request resolution.