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
  nothing left to read.
- **A change made to a prefab instance in the scene, rather than to the prefab, is not seen.**
  Collider assignments are commonly made that way, and show up as an unresolved collider
  reference in the report.
- **A prefab variant is read from every prefab above it as well**, since its own file holds
  only its overrides. The report names the base as `source.prefabVariant`.

An imported `.vrm` is the exception. It is binary rather than text, and UniVRM has to be
installed for it to import at all, so its components are read directly. See
[VRM](what-converts/vrm.md).

## Things a conversion does not touch

Materials and shaders, meshes, the avatar's animator, and anything Basis fills in itself when the
`BasisAvatar` inspector is first opened.
