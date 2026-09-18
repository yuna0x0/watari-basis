# Changelog

Notable changes to this package. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Parameters that drive more than one control are named in the report: `vixxy.sharedParameter`.

## [0.11.0] - 2026-09-18

### Added

- Components that exist only in the scene are read from the saved scene file: `source.scene`,
  `scene.unsaved`, `scene.dirty`, `scene.notText`.
- A converted control's Vixxy address is its parameter name, so an OSC float sent to
  `/avatar/parameters/<name>` drives it: `vixxy.address`.

### Changed

- `source.sceneOnly`, `source.modelInstance`, `avatar.noPrefab` and `avatar.rootNotPrefab` now
  say to save the scene; they appear only when the scene could not be read.

### Fixed

- An avatar placed from its model file, with components added in the scene, is reported as
  `source.modelInstance` instead of being told to apply them to a prefab it does not have.

## [0.10.0] - 2026-09-18

### Added

- The eye rotation limit of a VRChat descriptor or a VRM 1.0 look-at is written as the Basis
  Avatar's eye look angle: `descriptor.eyeLook.range`, `vrm.lookAt.range`.

### Changed

- `vrm.lookAt.range` is Mapped rather than Dropped; Basis now has a per-avatar eye angle.

### Fixed

- Vector and rotation components written with a negative exponent, such as `-4e-08`, are read.

## [0.9.2] - 2026-09-17

### Changed

- Report messages cut to one or two sentences: what happened and what to do.

## [0.9.1] - 2026-09-17

### Fixed

- PhysBones kept on a prefab of their own now find their root bone and colliders on the avatar.
- A descriptor kept on a nested prefab now keeps its meshes.
- Colliders assigned to a nested PhysBone by override are read.

## [0.9.0] - 2026-09-17

### Added

- VRCFury toggles become Vixxy controls. Checked against VRCFury 1.1429.0.
- VRCFury Full Controllers are traced like the avatar's own menu and FX controller.
- VRCFury Armature Links parent clothing bones under the avatar's, new **Armature links** target.
- A menu, parameter or FX controller the descriptor names but cannot be read is a warning:
  `expressions.assetMissing`, `expressions.assetNotText`, `fx.controllerMissing`.
- A copy built by VRCFury is named as such: `source.builtCopy`.

## [0.8.5] - 2026-09-17

### Added

- Three warnings for an avatar that reads as empty: `source.notText`, `source.sceneOnly`,
  `avatar.rootNotPrefab`.

## [0.8.4] - 2026-09-16

### Fixed

- The Jiggle Physics dependency is declared as 16.0.1, the version every Basis checkout ships. VPM
  clients matched the embedded package against 16.0.0 exactly and warned of a conflict on install.

## [0.8.3] - 2026-09-14

### Changed

- `physbone.immobile.ignoreRootMotion` is Approximated, not Mapped: ignore root motion cancels the
  root's translation only, and a turning parent still swings the chain.
- New `physbone.immobile.drift` warning for an immobile bone whose stiffness came out under 0.1:
  on Basis it wanders when its parent turns. Values are unchanged; the fix is the rig's stiffness.

## [0.8.2] - 2026-09-14

### Changed

- Internal cleanup only: an unused helper removed. Nothing converts differently.

## [0.8.1] - 2026-09-14

### Fixed

- Menu toggles that also switch a PhysBone or a renderer now convert; the whole toggle used to be dropped.
  `vixxy.componentSwitch`, `vixxy.componentSwitch.dropped`.

## [0.8.0] - 2026-09-14

### Removed

- Applying Modular Avatar Shape Changers that have no menu item, added in 0.7.0. Modular Avatar
  does this itself at Basis build time when it is installed with the Basis NDMF platform.

## [0.7.0] - 2026-09-14

### Added

- Overrides from prefab variants and nested prefabs are applied before reading: `source.overridesApplied`.
- The report names unread FX layers, motion-time radials, overlapping controls and per-player PhysBone permissions:
  `fx.layersUnread`, `motion.motionTime`, `vixxy.overlap`.
- A Dynamic Bone constant force pointing straight down becomes gravity.
- A Modular Avatar Merge Animator's relative path root is honoured.

## [0.6.0] - 2026-09-14

Every reader was audited against VRChat SDK 3.10.5, UniVRM 0.131.2, Dynamic Bone 1.3.4 and
Modular Avatar 1.18.7.

### Fixed

- PhysBones with Allow Collision off lost collision with their own colliders. That setting only
  governs other players' hands.
- A PhysBone root with one child stayed still after conversion. VRChat simulates it.
- The spring falloff curve was applied to drag, which runs the other way. It is dropped instead.
- A PhysBone with radius 0 became grabbable. VRChat never grabs it.
- A Dynamic Bone with no root jiggled everything under its object. It now writes nothing.
- A Dynamic Bone with radius 0 and colliders lost collision.
- Disabled PhysBones, Dynamic Bones, colliders, constraints and head chops were converted as
  enabled. They now write nothing.
- VRChat Look At constraints rolled the wrong way. A constraint's at-rest pose was overwritten by
  the transform's current pose.
- Blink was written from eyelid settings left behind when Eye Look was off.
- Head chop entries naming the humanoid Head are dropped; Basis ignores them.
- VRM: tail joint parameters, constraint rest snapping, 0.x viseme and blink matching, upward gravity and preset leaks fixed.
- A menu toggle that swaps materials is reported instead of rebuilt without the swap. A toggle
  keeps its other objects when one is missing.
- A clip that plays on its own and does not loop now plays once and holds, as in the animator.
- Modular Avatar menu items without a parameter, inverted Object Toggles, labels and defaults
  follow Modular Avatar's own rules.

### Changed

- Dynamic Bone elasticity, stiffness, damping and gravity are derived from its simulation rather
  than reusing the PhysBone fit. Gravity is left to the preset and reported.
- More of the mapping is labelled Approximated where the two systems differ: PhysBone gravity
  and falloff curves, hinge limits, VRM gravity and cone limits.
- The rig check requires Chest and Neck, as the Basis validator does.
- Vixxy controls carry the parameter's saved and synced flags. A missing HVR Avatar Comms is
  warned about.

## [0.5.8] - 2026-09-07

### Changed

- Report messages for the stiffness and drag fits, and for arm bones without a twist child,
  state the fit and stop.

## [0.5.7] - 2026-09-06

### Added

- A VRM's eye rotation limit is read and, when it is below the 25 degrees Basis turns every
  avatar's eyes to, reported: `vrm.lookAt.range`. Basis has no per-avatar limit to write it into.

## [0.5.6] - 2026-09-06

### Fixed

- The Expression selector's Neutral choice is every shape at zero, no longer the avatar's `neutral` preset.

## [0.5.5] - 2026-09-06

### Fixed

- UniVRM's runtime drivers are removed on conversion; they rewrote every blendshape each frame: `vrm.runtimeRemoved`.

## [0.5.4] - 2026-09-06

### Added

- VRM material colour and texture offset changes become Vixxy material properties:
  `vrm.expression.materialValues`, `vrm.expression.materials`, `vrm.expression.materialShared`.

## [0.5.3] - 2026-09-06

### Added

- Checked against the VRM consortium's sample models: Seed-san, the constraint and twist sample,
  the two isBinary conformance models, the MToon UV test and Alicia 0.51. All six read and plan.
- What the Expression selector cannot carry is reported:
  `vrm.expression.continuous`, `vrm.expression.override`, `vrm.expression.materials`.
- An avatar that aims its eyes with expressions rather than eye bones is reported:
  `vrm.lookAt.expression`.

### Fixed

- A blink expression that moves more than one blendshape was left unset. Basis blinks with every
  index in its blink array, so all of its shapes on one mesh are written now.

## [0.5.2] - 2026-09-05

### Fixed

- VRM expressions are one selector named Expression, not one toggle each.

## [0.5.1] - 2026-09-05

### Changed

- Window headings and messages are terser: Targets, Diagnostics, and Warnings, Dropped,
  Approximated, Mapped as the severity headings in the window and the report.

### Fixed

- The versions line at the bottom of the window was cut off instead of wrapped. The wrapping
  helper now forces word wrap on any style it is given.

## [0.5.0] - 2026-09-05

### Added

- A VRC Head Chop converts to a Basis Head Chop, naming the same bones with the same scale
  factors. A bone limited to VR or to desktop is reported: `headChop.condition.dropped`.
- VRC Raycast is reported as `raycast.dropped`; per-platform overrides and impostor settings as
  `vrchat.buildSettings`. Neither was recognised before.
- A PhysBone collider marked global, an SDK 3.10.4 setting, is reported:
  `collider.global.dropped`.
- The report, the window and a docs page state the source versions the readers were checked
  against: VRChat SDK 3.10.5, UniVRM 0.131.2, Dynamic Bone 1.3.4, Modular Avatar 1.18.7.
- Modular Avatar's vertex filters and Move Independently are named rather than reported as
  unknown scripts.
- A plane collider not facing its Y axis is reported:
  `collider.planeRotation.dropped`, `collider.planeAxis.dropped`, `vrm.collider.planeNormal`.

### Fixed

- VRChat and Dynamic Bone capsule colliders were written one diameter too long. Both give a
  capsule's height end to end; Basis measures between the cap centres.
- A VRChat or Dynamic Bone capsule no taller than its diameter is written as a sphere, which is
  how both sources collide it.

## [0.4.0] - 2026-09-04

### Added

- A `.vrm` file converts as imported; no need to unpack it into a prefab first.
- A VRM's vowel expressions and blink fill the Basis Avatar's visemes. Five of the fifteen, since
  VRM names no consonants: `vrm.visemes` and `vrm.blink`.
- VRM 0.x look at components are reported as `vrm.lookAt` rather than as unknown scripts, along
  with its humanoid description.
- A prefab saved from a `.vrm` without unpacking reads its components from the file:
  `source.modelRead`.
- An avatar whose visemes nothing could fill says so as `descriptor.visemesUnset`.

### Fixed

- An imported `.vrm` converted as though it were empty. It is binary, and only text was read.
- A prefab saved from a `.vrm` without extracting its assets reported `vrm.objectUnreadable`
  instead of reading the expressions and licence from the component.
- The report titled itself "VRChat avatar to Basis" whatever it had read.

## [0.3.3] - 2026-09-03

### Fixed

- Text was cut off rather than wrapped under the conversion result, in the rig and diagnostic
  lists, beside the tuning weights and under the buttons.
- The line describing Advanced left out the checkbox per motion, which it has always added.

## [0.3.2] - 2026-09-02

### Fixed

- Detected, Read from, What to convert and Prefabs cut their text off instead of wrapping when
  the window was narrow.
- Read from named every prefab, so an avatar built from dozens of them filled the window above
  the scroll view. It names six and counts the rest, as the report does.

## [0.3.1] - 2026-09-01

### Changed

- A prefab saved from an imported model without unpacking now says so, naming the model, instead
  of the generic message for finding nothing. Reported as `source.notUnpacked`.

## [0.3.0] - 2026-09-01

### Added

- A prefab variant converts what it inherits; every prefab above it is read: `source.prefabVariant`.
- Avatar Modify Support is named rather than reported as an unrecognised script. It is
  editor-only and carries nothing to convert, reported as `source.editorOnlyTool`.

### Fixed

- Components a prefab only refers to, rather than defines, were read as though they were its
  own. On a variant this counted colliders twice and reported the copies as unresolved.
- The report guide named `constraint.solveInLocalSpace`; the code is
  `constraint.solveInLocalSpace.dropped`.

## [0.2.0] - 2026-08-31

### Added

- VRM spring bones become jiggle physics, in both VRM 0.x and VRM 1.0. UniVRM is not needed.
- VRM expressions become Vixxy controls. Lip sync, blinking and gaze are left to Basis.
- VRM 1.0 rotation, aim and roll constraints become Basis constraints. None is exact, and all
  three are reported.
- A VRM's eye offset becomes the Basis eye position.
- A VRM's licence is shown before converting: title, author, who may wear it, and every
  permission the file states.
- Menu controls that share a parameter become one Vixxy control with a choice per value. This
  covers outfit and hairstyle selectors.
- Radial puppets become Vixxy sliders, taking the two ends of the blend tree. A tree with
  motions in between is reported as `vixxy.puppetEnds`.
- Toggles guarded by a VRChat parameter such as `IsLocal` are rebuilt rather than skipped. The
  dropped guard is reported as `vixxy.builtinGuard`.
- A menu toggle whose clip animates over time is rebuilt as an authored motion the control
  switches on.
- Animation that plays with nothing switching it on becomes `BasisAuthoredMotion`, baked to a
  `BasisMotionClip` asset. The asset is a project file and survives an undo.
- Modular Avatar `Object Toggle` is read.

### Fixed

- Converting an avatar twice stacked a second Vixxy control and menu item instead of replacing
  the first.
- Controls started at their first choice instead of the parameter's declared default, so
  clothing authored on switched itself off at load.
- A motion a menu switches on could be written without its control, playing it permanently.
- Switching off menu toggles or authored motion left their losses in the report.
- Clip paths from a Modular Avatar merged animator did not rebase the rotations they animate.
- A second avatar descriptor, which clothing often carries, was read and reported before being
  discarded.
- The re-conversion dialog counted every non-jiggle component as a constraint, and called a
  baked motion clip undoable.
- Constraint sources past the sixteenth were dropped silently. VRChat's overflow list is still
  not read, but the difference is reported.
- `UniHumanoid.Humanoid` was reported as an unknown script.
- A VRM whose expressions and licence are still inside the `.vrm` is reported as
  `vrm.objectUnreadable`, naming the import setting that extracts them.
- A layer where one parameter value led to two states was read as if the first transition were
  the only one. The layer is left alone and reported instead.
- A Modular Avatar menu item's toggle was only built when the prefab also installed a menu.
- Any list entry in an expression menu asset began a new control, so a radial's `subParameters`
  was read as a control of its own.
- Modular Avatar object paths are resolved against the avatar root, as `AvatarObjectReference`
  does.

## [0.1.2] - 2026-08-31

### Fixed

- One-sided menu toggles came out inverted; which side animated an object is now recorded.

## [0.1.1] - 2026-08-31

### Changed

- Renamed to Watari: menu **Tools > Watari > Convert Avatar to Basis**, repository `yuna0x0/watari-basis`, package id unchanged.

## [0.1.0] - 2026-08-30

First release.

### Added

- **Physics.** VRChat PhysBones and legacy Dynamic Bone, with their colliders, become Basis
  jiggle physics. Falloff curves, collider shapes, ignored transforms and grab settings carry.
- **Constraints.** All six VRChat constraint types. One driving a transform other than its own
  is moved onto the transform it drives.
- **Avatar descriptor.** View position, the fifteen visemes and blink become a `BasisAvatar`,
  updated in place on a re-conversion.
- **Menu toggles.** Rebuilt as HVR Vixxy controls with menu items, covering object switching,
  blendshapes and material properties.
- **Modular Avatar.** Hierarchy components are left to it. Menu items and merged animators are
  read together and rebuilt as Vixxy controls.
- **Whole hierarchies.** Every prefab the chosen object is built from is read, so clothing
  converts with the avatar.
- **Conversion options.** Each kind can be switched off, and under Advanced so can any
  individual prefab, rig, constraint or toggle.
- **Rig check.** Reports the humanoid rig against what Basis's IK expects, and offers to clear
  the Jaw mapping.
- **Reporting.** Anything approximated or dropped is listed with a stable code and a reason
  before anything is written. Copyable, or saved as Markdown.
- Nothing is written until confirmed, one undo reverts a conversion, and converting again
  replaces its own output.

### Known limitations

- VRChat contacts, puppets, and animation that plays over time do not convert.
- Two physics settings are fits rather than conversions, exposed as adjustable weights.
- Component data is read from prefab files, so the avatar must still be linked to its prefab.

[Unreleased]: https://github.com/yuna0x0/watari-basis/compare/v0.11.0...HEAD
[0.11.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.11.0
[0.10.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.10.0
[0.9.2]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.9.2
[0.9.1]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.9.1
[0.9.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.9.0
[0.8.5]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.5
[0.8.4]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.4
[0.8.3]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.3
[0.8.2]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.2
[0.8.1]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.1
[0.8.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.8.0
[0.7.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.7.0
[0.6.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.6.0
[0.5.8]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.8
[0.5.7]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.7
[0.5.6]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.6
[0.5.5]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.5
[0.5.4]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.4
[0.5.3]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.3
[0.5.2]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.2
[0.5.1]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.1
[0.5.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.5.0
[0.4.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.4.0
[0.3.3]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.3.3
[0.3.2]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.3.2
[0.3.1]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.3.1
[0.3.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.3.0
[0.2.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.2.0
[0.1.2]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.1.2
[0.1.1]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.1.1
[0.1.0]: https://github.com/yuna0x0/watari-basis/releases/tag/v0.1.0
