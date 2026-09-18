---
sidebar_position: 7
---

# Limitations

Everything here is also reported by the tool. This is the same information in one place.

## Not converted at all

- **VRChat contacts.** Basis has no contact system, so anything driven by touch is dropped.
- **VRChat raycasts.** Nothing in Basis fires a ray into animator parameters.
- **Where a VRM looks.** Its eye offset carries across as the Basis eye position, but the
  aiming does not: Basis drives gaze itself. The renderers a VRM hides from its wearer are
  reported rather than converted. See [VRM](what-converts/vrm.md).
- **A VRM's metadata.** Its title, author and permissions are shown before you convert, and
  Basis has nowhere to keep them.
- **Two-axis and four-axis puppets.**
- **Animation that moves or scales something over time.** Only rotation is baked, so a toggle
  or a layer that animates anything else over time is reported rather than half converted
  (`vixxy.notSimple`, `motion.notRotation`). See [Authored motion](what-converts/authored-motion.md).
- **Expression parameters as a system.** Vixxy controls hold their own state, so there is no
  parameter list to recreate.
- **Custom animation layers**, gestures, sitting and IK poses.
- **Anything that only exists while an animator runs.** Gesture blends, radials that scrub a
  clip through motion time, layers that interact. The animator is read to find the constant
  values a menu entry sets, never run. Such layers are named in the report (`fx.layersUnread`,
  `motion.motionTime`).
- **Overrides made on a scene instance.** A prefab variant's overrides and a nested prefab's
  overrides are read from their files. A value changed on the instance in the scene, on a
  component that arrives as a missing script, is not.

## Converted with a caveat

- **Two physics settings are fits**, not conversions. See [Physics](what-converts/physics.md).
- **Wide angle limits are dropped** rather than clamped to something tighter.
- **A head chop applies in VR and on desktop alike**, whichever VRChat limited it to.
- **Eyes turn up to 25 degrees on every avatar.** Basis has no per-avatar limit; a VRM built for
  10 degrees shows white when its eyes track past that.
- **Global PhysBone colliders stay local.** Basis makes hands, arms and feet global on its own.
- **Material properties are applied through a property block**, which covers every material on a
  renderer. A renderer with more than one material is reported.
- **Modular Avatar toggles** are rebuilt only when a single parameter of the avatar's own steers
  their layer.
- **A toggle that waited on a VRChat parameter no longer waits.** `IsLocal`, `InStation`,
  `Seated` and the rest have no Basis equivalent, so a control guarded by one switches whenever
  it is used. The report names the guard that was dropped.
- **Authored motion carries rotation only.** A baked Basis motion clip holds nothing else, so a
  clip that also moves or scales something keeps the turning and reports the rest.
- **A baked motion clip is a project asset**, so an undo removes the components a conversion
  wrote but leaves the clip on disk.
- **A radial puppet becomes a slider between the two ends of its blend tree.** Vixxy interpolates
  in a straight line between choices, so motions the tree held in between are approximated by
  that line.
- **A VRM fills five of the fifteen visemes.** It names the vowels and no consonants, so the
  mouth moves on `aa`, `E`, `ih`, `oh` and `ou` and holds still on the rest.
- **None of the three VRM constraints is exact.** VRM's rotation constraint copies a delta from
  the source's rest pose, VRM's aim states no up direction, and nothing in Basis copies rotation
  about a single axis the way a roll constraint does. See [VRM](what-converts/vrm.md).

## Where the data comes from

Component data is read from prefab files, because in a Basis project the VRChat components are
missing scripts and only the file still holds their values. Two things follow from that:

- **The avatar has to still be linked to its prefab.** If the prefab was unpacked, there is
  nothing left to read: `avatar.noPrefab`, or `avatar.rootNotPrefab` when prefab instances
  remain beneath the selected object.
- **A change made to a prefab instance in the scene, rather than to the prefab, is not seen.**
  A component added there is reported as `source.sceneOnly`; a collider assignment made there
  shows up as an unresolved collider reference.
- **An avatar placed from its FBX has no prefab file.** A model file holds the mesh and the
  skeleton and no components, so everything added on that instance is scene-only:
  `source.modelInstance`. Save the avatar as a prefab in the VRChat project and export that.
- **The prefab file has to be text.** A prefab stored in Unity's binary form yields nothing and
  is reported as `source.notText`. Unity will not save a prefab whose scripts are missing, so the
  switch to Force Text and the re-save have to happen in the project where the scripts are
  installed, before the avatar is exported.
- **A prefab variant is read from every prefab above it as well**, since its own file holds
  only its overrides. The report names the base as `source.prefabVariant`.
- **A prefab nested inside another is read from its own file**, with the outer prefab's
  overrides applied to it first, including references that point back into the outer prefab. An
  avatar that keeps its armature clean and puts every component on a prefab of its own reads
  the same as one that does not.

A copy built by VRCFury cannot be read. Convert the original avatar: see
[VRCFury](what-converts/vrcfury.md).

An imported `.vrm` is the exception. It is binary rather than text, and UniVRM has to be
installed for it to import at all, so its components are read directly. See
[VRM](what-converts/vrm.md).

## Things a conversion does not touch

Materials and shaders, meshes, the avatar's animator, and anything Basis fills in itself when the
`BasisAvatar` inspector is first opened.

### Shaders

Basis renders with URP. A material whose shader does not run there is swapped at load for
`Universal Render Pipeline/Lit`, keeping its base texture, colour, normal, metallic and occlusion
maps. What an avatar's materials need depends on the shader:

- **lilToon** regenerates its shaders for the project's pipeline on import. No material change.
- **Silent Cel Shading** has a URP pass in the same shader file. No material change.
- **MToon** for VRM 1.0 is chosen for URP by UniVRM when the `.vrm` is imported. VRM 0.x MToon
  has no URP shader and imports as unlit.
- **Poiyomi** 10.0.20 and later ships its URP shaders as a separate package, `Poi.Toon.URP`,
  for Unity 6. They are separate shaders, so each material has to be switched to the URP one.
  A Poiyomi material whose shader is missing offers **Switch to latest Toon** in its inspector,
  which picks the URP shader in a URP project. A locked material has to be unlocked first.

Basis keeps its own list of shaders known to work at
[docs.basisvr.org](https://docs.basisvr.org/en/docs/avatar/shaders).

### OSC

Basis listens for OSC on port 9000 and routes `/avatar/parameters/<name>` to the Vixxy control
whose address is `<name>`, on an avatar carrying HVR Basis Comms's Automatic Face Tracking or
OSC Acquisition component. Converted controls are written without an address, so Vixxy
generates one from the object path and an app cannot reach them by parameter name.

Basis reads float arguments only; bool and int messages are dropped.
