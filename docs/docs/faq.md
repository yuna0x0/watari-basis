---
sidebar_position: 8
---

# Questions

### Do I need the VRChat SDK installed?

No, and you should not install it into a Basis project. VRChat components arrive as missing
scripts, and their data is read from the prefab file instead.

It also changes layers and the collision matrix, which are project-wide, so it breaks Basis
itself. See [Installing](installation.md).

### My avatar shows missing scripts everywhere. Is that a problem?

That is the normal state of an imported VRChat avatar in a Basis project, and it is what gets
read. Do not remove them before converting.

### Nothing was found in my avatar.

The Warnings section names the cause:

- `avatar.noPrefab`, `avatar.rootNotPrefab`: the object is not linked to a prefab.
- `source.sceneOnly`: components added in the scene and never applied to the prefab.
- `source.modelInstance`: the avatar was placed from its FBX and never saved as a prefab.
- `source.notText`: the prefab file is binary.

Fix them in the VRChat project. Unity will not save a prefab whose scripts are missing.

### I built a copy with VRCFury and the toggles are missing.

Convert the original avatar, not the copy. The copy's menus and controllers cannot be read
here. See [VRCFury](what-converts/vrcfury.md).

### The jiggle physics does not move in Play mode.

Jiggle physics runs on a calibrated avatar. Press **Test In Editor** on the `BasisAvatar`
component instead.

### Can I convert clothing separately from the avatar?

Yes. Select the clothing object and convert that. A conversion replaces only what it wrote
itself, so anything already converted stays as it is.

### An undo left a file behind.

Baked motion clips are project assets, not components, so an undo does not remove them. Anything
else a conversion writes disappears. The clip sits in a `Watari Motion` folder beside the
animation it was baked from, and converting again writes over it rather than adding another.

### I converted twice by accident.

Converting again offers to replace what the previous conversion wrote, naming what will go, and
one undo reverts the removal and the rewrite together. Components somewhere else on the avatar,
and Vixxy controls you made yourself, are left alone.

### My avatar's idle animation does not play.

Authored motion runs on a calibrated avatar, the same as jiggle physics: press **Test In Editor**
on the `BasisAvatar` component. If nothing was converted at all, check the report: only layers
with no parameter steering them are read as motion, and only the rotation in them is baked.

### The physics feels wrong compared to the original.

Two settings are fits rather than conversions: stiffness and drag. Both are adjustable under
**Advanced** in the window. See [Physics](what-converts/physics.md).

### Do Poiyomi or lilToon materials need converting?

lilToon, Silent Cel Shading and VRM 1.0 MToon handle URP themselves. Poiyomi needs its URP
package and a shader switch per material. See [Limitations](limitations.md#shaders).

### Can an OSC app drive the converted toggles?

Not by parameter name: converted controls carry no address. See
[Limitations](limitations.md#osc). Face tracking over OSC is Basis's own and is not affected.

### A component was reported as an unknown script.

Please [open an issue](https://github.com/yuna0x0/watari-basis/issues) with the code from the
report. Components are identified by their script reference, and unrecognised ones are added
when they are reported.

### Does this work for props and worlds?

"Prop" means two different things here, so both answers:

- **An object worn on an avatar**, a piece of clothing, an accessory, a gimmick: yes. It is a
  prefab with physics and constraints like any other, and it converts with the avatar it is
  attached to, or on its own if you select it.
- **A Basis prop**, meaning the `BasisProp` content type that players spawn and that is
  networked: no. Nothing here writes one, and no `BasisProp` component is created.

`BasisAvatar` is the only Basis content type written today. Props and worlds are in scope, and
each is blocked on something different.

A `BasisProp` has nothing to read from: VRChat has no prop content type, and setting one up is
authoring rather than conversion, which Basis covers with its own validator.

A `BasisScene`'s geometry, lighting and colliders are plain Unity and need no conversion. Its
behaviour is Udon, written against VRChat's own runtime API, which Basis does not have.

Physics and constraints convert on any object you select, avatar or not.
