---
sidebar_position: 2
---

# VRM

VRM avatars, including everything VRoid Studio exports, carry their physics as spring bones and
their faces as expressions. Both formats are read: spring bones become Basis jiggle physics, and
expressions become HVR Vixxy controls.

UniVRM is needed to import a `.vrm` file, not to convert one. A VRM avatar handed over as a
prefab arrives with its components as missing scripts, and the data is read from the file either
way.

## Bringing in a `.vrm` file

Install [UniVRM](https://github.com/vrm-c/UniVRM) and drag the file into the project. Use 0.131.2
or newer: 0.131.0 does not compile on the Unity version Basis targets.

Drag the imported avatar into a scene and convert it. It does not need unpacking, and its assets
do not need extracting. A prefab saved from it without unpacking converts too, and the report
names the file it was read from as `source.modelRead`. A `.vrm` is binary, so there is no text
to read; its spring bones, constraints, expressions, licence and eye offset are read from the
components instead.

UniVRM's data components stay on the converted avatar. They are not on the Basis allow-list, so
the Basis build strips them.

## The two formats

**VRM 0.x** puts one component on the avatar carrying a group of chains and a single set of
parameters for all of them. Each root bone it names becomes a jiggle rig.

**VRM 1.0** puts a joint on each bone and lists which joints make up which chain on the avatar's
own component. Each chain becomes a jiggle rig rooted at its first joint.

UniVRM 1.0 converts a 0.x file to 1.0 components as it imports, so a `.vrm` brought in that way
reads through the 1.0 path whichever version it was written as. UniVRM 0.x instead writes a
prefab beside the `.vrm` as it imports; convert that prefab. The 0.x path also covers prefabs
made with an older UniVRM.

## UniVRM's runtime is removed

A converted VRM keeps UniVRM's data components and loses its runtime drivers: `Vrm10Instance` for
1.0; `VRMSpringBone`, `VRMBlendShapeProxy` and the look-at components for 0.x. They would rewrite
every blendshape and run their own physics each frame over the conversion. Basis strips them at
build anyway. Undo restores them: `vrm.runtimeRemoved`.

## What carries across

| VRM | Basis jiggle |
|---|---|
| Drag force | Drag, on the same 0 to 1 scale |
| Joint radius / hit radius | Collision radius, both in metres |
| Gravity power and direction | Gravity multiplier, the vertical part, as a fit: VRM adds a per-step force |
| Collider groups | The rig's colliders |
| Stiffness force | Stiffness, as a fit rather than a conversion |
| Centre transform | Ignore root motion, fully, measured at the rig's root bone |
| Cone angle limit (1.0) | Angle limit, capped at 90 degrees. Hinge and spherical limits are dropped |

**Parameters that differ along a chain become a curve.** VRM 1.0 carries a value per joint, and
Basis evaluates its parameters over distance from the chain root, which is the same axis. A chain
that stiffens toward its tip keeps that shape instead of being averaged.

**Bones the chain does not name are excluded.** A VRM spring names the bones it moves; a jiggle
rig simulates everything under its root. An accessory hanging off a hair bone would start
swinging, so it is excluded to leave it as still as VRM left it.

## Expressions

A VRM expression is a set of blendshape weights. The avatar's expressions become one Vixxy
selector named Expression: Neutral first, then one choice per expression. Every shape any
expression touches is set at every choice, at the expression's weight or zero, as the VRM
specification applies them. Neutral is every shape at zero; the avatar's own `neutral` preset is
not applied, since VRM applications do not apply it either.

Material colour and texture offset changes carry over as Vixxy material properties under MToon's
names, on every renderer that uses the material alone. A renderer that also carries other
materials is left alone: `vrm.expression.materialShared`. A material no renderer uses is left
out: `vrm.expression.materials`.

Two things the spec allows are reported rather than converted. An expression that is not
`isBinary` can be worn at any strength; a choice is all or nothing: `vrm.expression.continuous`.
An expression's overrides, which block or attenuate blink, gaze or lip sync while it is worn, have
no counterpart, since Basis keeps those running: `vrm.expression.override`.

The lip sync shapes, blinking and looking around do not become controls. Basis drives those
itself, and a menu item would fight it.

The five vowels and blink go on the `BasisAvatar`, so a converted VRM talks and blinks. Blink
keeps every shape the expression moves, on one mesh. Basis has fifteen visemes and VRM five:
`aa`, `E`, `ih`, `oh` and `ou` are filled, the consonants stay unset. A viseme holds one
blendshape, so an expression that moves several is reported.

VRM refers to a blendshape by its position in the mesh rather than by name, so each one is looked
up on the renderer it names. A shape that is no longer there, usually because the mesh changed
after the expression was authored, is reported rather than guessed at.


## The avatar's licence

Every VRM states who may wear it and what may be done with it. The window shows all of it before
you convert: the title, the author, who may wear it, whether it may be changed or passed on,
whether commercial, violent, sexual, political or hateful use is allowed, whether credit is
required, and the licence it names.

A permission the format has no field for is left out rather than guessed at. VRM 0.x has no
political or antisocial fields, and no redistribution or credit fields; VRM 1.0 has all of them.

The licence is a warning when it forbids changing the avatar or limits who may wear it. Nothing
is blocked. Converting changes an avatar, and using it on Basis is a use; check the licence
allows both.

## Constraints

VRM 1.0 has three node constraints, and each becomes a Basis constraint on the object it sits on.

- **Rotation** becomes a Basis rotation constraint. VRM copies how far the source has turned from
  its rest pose; a Basis constraint takes the source's rotation itself, with a rotation offset so
  the authored pose holds while the source rests. The two differ if the source's rest pose
  changes.
- **Aim** becomes a Basis aim constraint, pointing the same local axis at the same source. Basis
  also holds an up direction, which VRM does not state, so the scene's up is used and the roll
  around the aim may differ.
- **Roll** copies the source's rotation about one axis alone, which nothing in Basis or in
  Unity's own set does. It becomes a rotation constraint limited to that axis, which follows the
  source's rotation rather than its roll.

All three are reported, since none of them is exact.

## Where the eyes sit

Both formats record the point the camera sits at as an offset from the head bone. Basis stores
the same point as the avatar's eye position, so it carries across: a converted VRM avatar has a
correct eye height instead of one Basis has to guess.

A VRM also marks renderers to hide from the wearer or show only to them. Basis hides the head
bone and everything under it in first person, which covers a face skinned to the head and hats
parented to it. The flags are reported rather than converted. If something still blocks the
camera, add a Basis Head Chop naming it.

## What does not

- **The centre transform.** VRM simulates relative to it so hair does not lag behind a moving
  avatar. Basis has no equivalent, though its own root motion handling covers some of the same
  ground.
- **A plane collider's normal**, when it is not its transform's Y axis. Basis planes face that
  axis, so the plane is written facing Y and reported as `vrm.collider.planeNormal`.
- **Inside colliders**, which hold bones within a shape. Basis only pushes out, so they are
  written as ordinary colliders and reported. Remove them if the result looks wrong.
- **Where the avatar looks.** VRM aims the eyes with curves or an expression; Basis drives gaze
  from the eye bones. Only the eye offset carries over. VRM 0.x look-at components:
  `vrm.lookAt`. An expression-type look-at has no eye bones, so the eyes stay still:
  `vrm.lookAt.expression`.
- **How far the eyes may turn.** VRM 1.0 states a limit, 10 degrees on a VRoid export. Basis
  turns eyes up to 25 degrees with no setting on the avatar, so a large eye shows white past the
  model's limit: `vrm.lookAt.range`.
- **The avatar's metadata**: its title, author and permissions.
