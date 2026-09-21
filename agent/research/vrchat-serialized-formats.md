# VRChat serialized formats, as seen without the SDK

Verified 2026-08-30 by reading real prefabs written by VRChat SDK 3.10.3 on Unity 2022.3.22f1.

The VRChat SDK cannot be installed into a Basis project, so in a Basis project every VRChat
component is a missing script. Its serialized data survives intact in the `.prefab` file, which
is what makes this package possible.

## Identifying a component whose script is missing

A `MonoBehaviour` document carries `m_Script: {fileID: F, guid: G, type: T}`. Two shapes:

- **Loose `.cs` script**: `fileID` is always `11500000` and `guid` identifies the script.
  Dynamic Bone is like this.
- **Type inside a DLL**, which is how the VRChat SDK ships: `guid` identifies the *assembly*
  and `fileID` is a hash of the class name, so one guid covers many types. `type: 3`.

So the identity key is the `(guid, fileID)` pair, not the guid alone.

```
2a2c05204084d904aa4945ccff20d8e5 : 1661641543    VRCPhysBone
2a2c05204084d904aa4945ccff20d8e5 : -1631200402   VRCPhysBoneCollider
58e2f01a24261a14cb82e6d3399e8b16 : 1116338486    VRCPositionConstraint
58e2f01a24261a14cb82e6d3399e8b16 : 1788371120    VRCRotationConstraint
58e2f01a24261a14cb82e6d3399e8b16 : -926596935    VRCAimConstraint
67cc4cb7839cd3741b63733d5adf0442 : 542108242     VRCAvatarDescriptor
67cc4cb7839cd3741b63733d5adf0442 : -340790334    VRCExpressionsMenu
67cc4cb7839cd3741b63733d5adf0442 : -1506855854   VRCExpressionParameters
67cc4cb7839cd3741b63733d5adf0442 : 1610797297    VRCSpatialAudioSource (derived, seen in a report)
4ecd63eff847044b68db9453ce219299 : -1427037861   PipelineManager
f9ac8d30c6a0d9642a11e5be4c440740 : 11500000      DynamicBone
baedd976e12657241bf7ff2d1c685342 : 11500000      DynamicBoneCollider
4e535bdf3689369408cc4d078260ef6a : 11500000      DynamicBonePlaneCollider
```

`VRCParentConstraint`, `VRCScaleConstraint` and `VRCLookAtConstraint` fileIDs are still unknown,
because the reference avatars do not use them. Recover them by extracting
`VRC.SDK3.Dynamics.Constraint.dll` from a VCC package zip and running `ilspycmd`.

Guids are stable in practice but are not a contract, which is why the reader **reports**
unrecognised identities rather than skipping them.

## VRCPhysBone serialized field order

Editor foldout state serializes first and is noise: `foldout_transforms`, `foldout_forces`,
`foldout_collision`, `foldout_stretchsquish`, `foldout_limits`, `foldout_grabpose`,
`foldout_options`, `foldout_gizmos`.

Then: `version`, `integrationType`, `rootTransform`, `ignoreTransforms`, `endpointPosition`,
`multiChildType`, `pull`(+Curve), `spring`(+Curve), `stiffness`(+Curve), `gravity`(+Curve),
`gravityFalloff`(+Curve), `immobileType`, `immobile`(+Curve), `allowCollision`,
`collisionFilter`, `radius`(+Curve), `colliders`, `limitType`, `maxAngleX`(+Curve),
`maxAngleZ`(+Curve), `limitRotation`, `limitRotationX/Y/ZCurve`, `allowGrabbing`, `grabFilter`,
`allowPosing`, `poseFilter`, `snapToHand`, `grabMovement`, `maxStretch`(+Curve),
`maxSquish`(+Curve), `stretchMotion`(+Curve), `isAnimated`, `resetWhenDisabled`, `parameter`,
`showGizmos`, `boneOpacity`, `limitOpacity`.

Enums: `IntegrationType { Simplified=0, Advanced=1 }`, `MultiChildType { Ignore=0, First=1,
Average=2 }`, `LimitType { None=0, Angle=1, Hinge=2, Polar=3 }`, `ImmobileType { AllMotion=0,
WorldMotion=1 }`.

### Three traps

1. **An empty curve, `m_Curve: []`, means "no falloff", that is constant 1.0.** Reading it as
   zero would silently flatten every converted rig.
2. `integrationType` changes what `spring` and `stiffness` mean. Both values are common in the
   wild: 1328 Simplified against 2045 Advanced across the reference project.
3. `version` is `0` (PhysBone 1.0) or `1` (1.1) with different spring semantics, and both are
   common: 1716 against 917. Key the mapper off the `(version, integrationType)` pair.

## VRCPhysBoneCollider

`rootTransform`, `shapeType` (`Sphere=0, Capsule=1, Plane=2`), `insideBounds`, `radius`,
`height`, `position`, `rotation`, `bonesAsSpheres`.

`insideBounds` (keep bones *inside* the collider) and `bonesAsSpheres` have no jiggle
equivalent.

SDK 3.10.4 added `globalCollision` (an `AdvancedBool`: `False=0, True=1, Other=2`),
`globalCollisionAllowSelf`, `globalCollisionAllowOthers` and `globalCollisionFlags`, replacing the
runtime-only `isGlobalCollider`. Anything but 0 is reported as `collider.global.dropped`.

## VRCHeadChop

`VRC.SDK3.Avatars.Components.VRCHeadChop`, in `VRCSDK3A.dll`, fileID `-1888410255`. Fields:
`targetBones` (a sequence of `{transform, scaleFactor, applyCondition}`, `applyCondition`
`AlwaysApply=0, VrOnly=1, NonVrOnly=2`, at most 32 entries) and `globalScaleFactor`. Maps onto
`BasisHeadChop.Targets[] {Target, Scale}`; Basis multiplies a target's local scale by `Scale`
while the wearer is in first person (`BasisLocalAvatarDriver.CollectHeadChopEntries`), which is
what VRChat's factor means too.

## Components with nothing to become

- `VRCRaycast` (3.10.3), `VRCSDK3A.dll` fileID `1472509199`: reported as `raycast.dropped`.
- `VRCImpostorSettings` (`798808286`) and `VRCImpostorEnvironment` (`306702890`) in
  `VRCSDK3A.dll`; `VRCPerPlatformOverrides` and `VRCAccessoryPerPlatformOverrides`, which are
  loose scripts in the avatars package (`Runtime/VRCSDK/SDK3A/`, guids
  `45da21a324e147228aaee066e399bff0` and `8a12ddb63afae28468db699a7e0cb228`). All reported as
  `vrchat.buildSettings`.

## Checking a new SDK release

Done for 3.10.5 on 2026-09-05, against 3.8.0. The package zips are on GitHub
(`vrchat/packages` releases) and the official VPM listing at `packages.vrchat.com/official`
names the newest; both need a User-Agent header. Unzip `Runtime/VRCSDK/Plugins/*.dll` from
`com.vrchat.base` and `com.vrchat.avatars` into a scratch folder, never into the Basis project,
decompile each with `ilspycmd -p -o`, and diff the public and `[SerializeField]` fields of the
classes the readers touch: `VRCPhysBoneBase`, `VRCPhysBoneColliderBase`, `ContactBase`,
`VRCConstraintBase` and its six subclasses, `VRCAvatarDescriptor`, `VRCExpressionsMenu`,
`VRCExpressionParameters`, `VRCHeadChop`. Then list the MonoBehaviours in
`VRC.SDK3.Avatars.Components` for new ones. A DLL type's fileID is derived as described under
KnownScriptIdentities; a loose script's guid is in its `.meta`. Raise `ProductInfo.CheckedAgainst`
and the versions page when done.

## Other formats worth knowing

- `VRCAvatarDescriptor.VisemeBlendShapes` is a `string[15]` in the same order Basis uses for
  `FaceVisemeMovement`, so viseme mapping is positional.
- `customEyeLookSettings.eyelidsBlendshapes` is an `int[]` that Unity writes as a **hex byte
  blob**, for example `1d000000ffffffffffffffff` meaning `{29, -1, -1}`, little-endian int32.
  It is not a string.
- `baseAnimationLayers` / `specialAnimationLayers` entries carry a `type` field. **The ordering
  usually quoted is wrong.** Decompiled from `VRCSDK3A.dll`
  (`VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType`) it is:

  ```
  Base = 0, Deprecated0 = 1, Additive = 2, Gesture = 3,
  Action = 4, FX = 5, Sitting = 6, TPose = 7, IKPose = 8
  ```

  The commonly cited version omits `Deprecated0` and shifts everything after it by one. Using it,
  a real avatar's layers read as Base, Action, FX, Sitting when they are actually Base, Gesture,
  Action, FX. Plausible enough to go unnoticed, and it makes the FX layer look like Sitting.
  Key off `type`, never off array position.
- VRC constraints serialize `Sources` as **16 fixed inline slots plus an `overflowList`**, where
  only the first `totalLength` entries are meaningful. Slots past that are defaults.
- VRC constraints have a `TargetTransform` that lets them drive a transform other than their
  own. Unity's and Basis's constraints always drive their own GameObject. This is the biggest
  hazard in constraint conversion.

## Prefab override paths

A PrefabInstance's `m_Modifications` name a target document, a `propertyPath` and a value.
The paths `PrefabOverrides.SetPath` follows: dotted field names, `Array.data[i]` for a list
entry, `Array.size` for its length, and `managedReferences[<id>].<field>` for a field of a
`[SerializeReference]` object, where `<id>` is the `rid` of the entry under
`references.RefIds` and the field sits under that entry's `data`. The last form was read back
from `PrefabUtility.GetPropertyModifications` in a test (`ManagedReferenceOverrideTests`),
since no local file held an example; VRCFury's actions are serialized this way, so an outer
prefab points a nested toggle at its objects through it.

