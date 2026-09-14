# Changelog

Notable changes to this package. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- A menu toggle whose clip also enabled or disabled a PhysBone or a renderer was dropped whole,
  its object switches included. The control now drives the jiggle rig or renderer:
  `vixxy.componentSwitch`. A switch on any other component is left out and reported:
  `vixxy.componentSwitch.dropped`.

## [0.8.0] - 2026-09-14

### Removed

- The application of Modular Avatar Shape Changers without a menu item, added in 0.7.0. Modular
  Avatar bakes those itself at Basis build time when installed with the Basis NDMF platform.
- The unused max grab stretch on rig plans, and the unused PhysBone grab movement and reset when
  disabled fields.

## [0.7.0] - 2026-09-14

### Added

- The FX layers nothing read are listed by name: `fx.layersUnread`. States scrubbed by a
  parameter are reported: `motion.motionTime`.
- Two controls setting the same object, blendshape or property are reported: `vixxy.overlap`.
  A control switching the avatar root is reported: `vixxy.rootActivation`.
- PhysBone permissions decided per player are reported: `physbone.allowGrabbing.filtered`,
  `physbone.allowCollision.filtered`.
- A Dynamic Bone force straight down, with no gravity, becomes the gravity multiplier:
  `dynamicbone.force.gravity`.
- A Merge Animator's relative path root is honoured.
- Property overrides from prefab variants and nested prefab instances are applied to the
  prefabs they modify before reading: `source.overridesApplied`. A variant that retunes a
  PhysBone or renames a shape converts with its own values.

## [0.6.0] - 2026-09-14

### Changed

- Dynamic Bone elasticity becomes jiggle stiffness by its square root, scaled by Update Rate,
  and Dynamic Bone stiffness becomes an angle limit of `2·asin(1−s)` degrees. The PhysBone fit
  weights no longer apply to Dynamic Bone. `dynamicbone.stiffness.angleLimit`,
  `dynamicbone.stiffness.tooWide`, `dynamicbone.updateRate`.
- Dynamic Bone damping becomes air drag as well as drag.
- Dynamic Bone gravity is reported and left to the preset. `dynamicbone.gravity` is Approximated;
  `dynamicbone.gravity.direction` is gone.
- Blend Weight folds into the angle limit (`dynamicbone.blendWeight`); Blend Weight 0 writes no
  rig.
- A PhysBone Hinge limit is reported as Approximated (`physbone.limitType.hinge`), gravity as
  Approximated, and falloff curves as Approximated (`physbone.curves.domain`): VRChat samples
  them by bone index, jiggle by distance from the root. `physbone.isAnimated` is Mapped.
- A PhysBone with radius 0 is locked from grabbing, as VRChat never grabs it.
- Max Stretch is dropped (`physbone.maxStretch.dropped`); it bounded bone length, not grab reach.
- A PhysBone whose Root Transform cannot be resolved is skipped instead of rooted on its object.
- Disabled PhysBones, Dynamic Bones, colliders, VRChat constraints and head chops write nothing,
  as they did nothing on the source: `physbone.disabled`, `dynamicbone.disabled`,
  `collider.disabled`, `constraint.disabled`, `headChop.disabled`.
- Basis constraints are always locked; VRChat forces Locked on in play. `constraint.locked`.
- A lone constraint source below full weight and an Aim with World Up Type None are reported:
  `constraint.source.weight.normalized`, `constraint.worldUp.none`.
- The rig check requires Chest and Neck, as the Basis validator does, and warns about unmapped
  shoulders: `rig.recommendedBones`.
- A VRM 1.0 cone angle limit becomes a jiggle angle limit; hinge and spherical limits are
  reported: `vrm.angleLimit.cone`, `vrm.angleLimit.dropped`. A chain's centre transform becomes
  full ignore root motion: `vrm.center`. VRM gravity is reported as a fit: `vrm.gravity`.
- VRM rigs no longer inherit a preset's angle limit, stretch, soften, air drag or root stretch.
- Vixxy controls carry the parameter's `saved` and `networkSynced` flags as remember and
  networked. A two-state control with a value other than 1 is written as 0 and 1:
  `vixxy.values.normalized`. An avatar without HVR Avatar Comms is warned about:
  `vixxy.commsMissing`.
- A menu toggle keeps its other targets when one object is missing, and an activation that
  changes nothing is left out.
- A clip that plays on its own and does not loop now plays once and holds, as in the animator.
  Layers animating something other than rotation are reported: `motion.notRotation`. State speed
  carries over.
- Modular Avatar menu items without a parameter take the one Modular Avatar assigns; the menu
  label, default, saved and synced flags are read; an inverted Object Toggle acts while the item
  is off; targets resolve by object reference before path. `Menu Install Target` is recognised.

### Fixed

- A VRM 1.0 chain's tail joint fed its parameters into the curves; UniVRM never reads them.
- A VRM rotation or roll constraint snapped the bone to the source's orientation at rest.
- VRM 0.x visemes and blink are matched by preset, not by the clip's free-text name.
- Upward VRM gravity was zeroed. The obsolete `VRMLookAt` driver is removed with the others.
- A menu toggle that swapped materials was rebuilt without the swap and without a diagnostic.
- A VRC Look At constraint rolled the wrong way on Basis. Roll is negated.
- The at-rest pose read from a VRC constraint was overwritten by the transform's current pose.
- A constraint whose Target Transform was its own transform was reported as retargeted.
- Blink was written from stale eyelid settings when Eye Look was disabled:
  `descriptor.eyeLook.disabled`. An unset blink slot no longer becomes shape -1.
- A head chop entry naming the humanoid Head is dropped and reported; Basis ignores it:
  `headChop.head.ignored`.
- A PhysBone root with one child stayed still after conversion whatever Multi Child Type said.
  VRChat simulates such a root; the rig now does too. `physbone.multiChildType.oneChild`,
  `physbone.multiChildType.ignoreBranches`.
- The spring falloff curve was applied to drag, which runs the other way, inverting the falloff.
  It is dropped: `physbone.springCurve.dropped`.
- The pre-rename keys `isGrabbable` and `isPoseable` are read when the current ones are absent.
- The rig's serialized version is set outright; the preset's stale version ran an upgrade on it.
- The root particle no longer inherits a preset's root stretch.
- A rig whose root bone was in its own ignore list aborted the conversion in the editor. The
  root is kept motionless instead: `physics.excludedRoot`. A rig whose preset fails to load takes
  jiggle's defaults.
- A Dynamic Bone with no root wrote a rig on its own object and jiggled everything below it.
  It now writes nothing: `dynamicbone.noRoot`. An unresolvable root skips that chain.
- A Dynamic Bone with radius 0 and colliders lost collision. It now collides at radius 0.01:
  `dynamicbone.radius.zero`.
- The stiffness and inert distribution curves were dropped without a diagnostic:
  `dynamicbone.stiffnessCurve.dropped`, `dynamicbone.inertCurve.dropped`.
- End Length 1 with no offset matches jiggle's own tip and is no longer reported as dropped.
- A second capsule radius within 0.01 of the first no longer reports a taper, matching
  Dynamic Bone.
- A PhysBone with Allow Collision off kept its listed colliders in VRChat but lost collision on
  the jiggle rig. Collision now follows the radius alone. `physbone.allowCollision.off` is
  Approximated: Basis's global hand, arm and foot colliders cannot be excluded per rig.

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

- The Expression selector's Neutral choice applied the avatar's own `neutral` expression at full
  weight, which VRM applications never do; on a VRoid avatar that reshaped the face and eyes.
  Neutral is every expression shape at zero.

## [0.5.5] - 2026-09-06

### Fixed

- A converted VRM did nothing visible: UniVRM's `Vrm10Instance` stayed on the avatar and rewrote
  every expression blendshape each frame, zeros included, and ran its own spring bones and
  look-at over the conversion. Conversion now removes UniVRM's runtime drivers, 0.x ones
  included, and says so: `vrm.runtimeRemoved`. Undo restores them.

## [0.5.4] - 2026-09-06

### Added

- A VRM expression's material colour and texture offset changes are written as Vixxy material
  properties on the renderers that use that material alone, under MToon's property names. Both
  formats: `vrm.expression.materialValues`. A material no renderer uses, or one that shares its
  renderer with others, is reported: `vrm.expression.materials`, `vrm.expression.materialShared`.

## [0.5.3] - 2026-09-06

### Added

- Checked against the VRM consortium's sample models: Seed-san, the constraint and twist sample,
  the two isBinary conformance models, the MToon UV test and Alicia 0.51. All six read and plan.
- What the selector cannot carry is reported: expressions worn at any strength as
  `vrm.expression.continuous`, blink, gaze and lip sync overrides as `vrm.expression.override`,
  an expression made only of material changes as `vrm.expression.materials`.
- An avatar that aims its eyes with expressions rather than eye bones is reported:
  `vrm.lookAt.expression`.

### Fixed

- A blink expression that moves more than one blendshape was left unset. Basis blinks with every
  index in its blink array, so all of its shapes on one mesh are written now.

## [0.5.2] - 2026-09-05

### Fixed

- A VRM avatar's expressions became one Vixxy toggle each, so two could be on at once and the
  menu held one entry per emotion. They are now one selector named Expression: Neutral, then one
  choice per expression, every shape set at every choice. See `agent/decisions/0016`.

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
- A plane collider facing anything but its transform's Y axis is reported, since a Basis plane
  always faces that axis: `collider.planeRotation.dropped`, `collider.planeAxis.dropped`,
  `vrm.collider.planeNormal`.

### Fixed

- VRChat and Dynamic Bone capsule colliders were written one diameter too long. Both give a
  capsule's height end to end; Basis measures between the cap centres.
- A VRChat or Dynamic Bone capsule no taller than its diameter is written as a sphere, which is
  how both sources collide it.

## [0.4.0] - 2026-09-04

### Added

- A `.vrm` file converts as it imports: spring bones, node constraints, expressions, licence and
  eye offset are read from its components, so it no longer has to be unpacked and saved as a
  prefab first.
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

- A prefab variant converts the physics, colliders and constraints it inherits. Its own file
  holds only its overrides, so every prefab above it is read too, reported as
  `source.prefabVariant`.
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

- Menu toggles that animate one side only came out inverted: a toggle named `Tail_OFF` showed
  the tail instead of hiding it. Which side animated an object is now recorded rather than
  inferred. Blendshapes had the same fault.

## [0.1.1] - 2026-08-31

### Changed

- Renamed to Watari. The menu is now **Tools > Watari > Convert Avatar to Basis**, and the
  repository moved to `yuna0x0/watari-basis`. The package id is unchanged, so an installed copy
  updates in place.

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

[Unreleased]: https://github.com/yuna0x0/watari-basis/compare/v0.8.0...HEAD
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
