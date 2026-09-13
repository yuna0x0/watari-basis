# PhysBone to Jiggle mapping

The reference for what maps to what, and how confident each row is. Verified 2026-08-30 against
the jiggle source and real VRChat avatar data.

## Curve semantics: same formula, different domain

Both systems evaluate a falloff curve as `value * curve.Evaluate(t)`. The domain differs: VRChat's
`CalcBoneRatio`/`CalcTransformRatio` divide the bone index by the chain's bone count, jiggle's
`BuildNormalizedDistanceFromRootList` divides cumulative world distance by the longest path
(checked 2026-09-14 against SDK 3.10.5 and the Basis clone). Curves carry over and are reported
as `physbone.curves.domain`. The paragraph below predates that check.

Both systems evaluate a falloff curve as `value * curve.Evaluate(t)` over normalized distance
from the chain root. VRChat's `pullCurve`, `radiusCurve` and friends, and jiggle's
`JiggleTreeCurvedFloat`, agree on domain **and** on semantics. Confirmed in
`JigglePointParameters.cs:64`:

```csharp
public float Evaluate(float t01) {
    return curveEnabled ? value * curve.Evaluate(t01) : value;
}
```

A per-bone falloff curve therefore carries across untouched rather than being flattened to a
scalar. All 61 PhysBones in the reference avatar have curves.

## `advancedToggle` gates half the parameters

`JiggleTreeInputParameters.ToJigglePointParameters` ignores `stretch`, `collisionRadius`,
`ignoreRootMotion`, `soften` and `rootStretch` unless `advancedToggle` is on, and
`collisionRadius` additionally needs `collisionToggle`. Writing any of them with the toggle off
is a silent no-op. The emitter must always set `advancedToggle`.

## The table

| PhysBone | Jiggle | Confidence |
|---|---|---|
| `rootTransform`, else the component's own object | `rootBone` | exact |
| `ignoreTransforms` | `excludedTransforms` | exact |
| `multiChildType == Ignore`, root with 2+ children | `excludeRoot` | exact for the root; branches below stay still in VRChat only |
| any `multiChildType`, root with 1 child | root simulated | exact (SDK: `childCount == 1` is simulated before Multi Child Type is read) |
| `immobile` | `ignoreRootMotion` | exact, same 0..1 sense |
| `gravity` (+curve) | `gravity` (+curve) | approximated: PhysBone blends toward down, jiggle scales 9.81 m/s² |
| `radius` (+curve) | `collisionRadius` (+curve), `collisionToggle` | exact |
| `allowGrabbing && radius > 0` | `!lockFromGrabbing` | exact (radius 0 is never grabbable in VRChat) |
| `maxStretch` | nothing (bounds bone length; `maxGrabStretch` bounds grab reach) | dropped |
| `stretchMotion` (+curve) | `stretch` (+curve) | close |
| `limitType` Angle/Hinge + `maxAngleX` | `angleLimit = deg/90`, `angleLimitToggle` | close |
| `pull` (+`stiffness` when Advanced) | `stiffness` | **heuristic** |
| `spring` | `drag`, inversely | **heuristic** |
| `limitType` Polar | `angleLimit` from the wider of pitch/yaw | approximated |
| `multiChildType` First/Average | `excludeRoot`, root left still | approximated |
| `immobileType == World` | folded into `ignoreRootMotion` | approximated |
| `gravityFalloff`, `maxSquish`, `limitRotation`, `endpointPosition` | nothing | dropped |
| `allowPosing`, `snapToHand`, `grabMovement` | nothing | dropped |
| `parameter` | nothing, Vixxy territory | dropped |
| `isAnimated` | nothing needed: jiggle always follows the animated rest pose | mapped |
| `spring` curve | nothing (drag runs opposite to spring) | dropped |
| `stiffnessCurve` when pull also has a curve | nothing, pull's curve wins | dropped |

Colliders map shape for shape, sphere, capsule and plane, since jiggle supports all three
despite what the Basis docs say. `insideBounds` and `bonesAsSpheres` have no equivalent.

Two measurements differ, both verified 2026-09-05 by decompiling `VRC.Dynamics.dll` (SDK base
3.8.0) and reading `JiggleJobSimulate`:

- **Capsule height.** VRChat's `CollisionScene` puts the cap centres at
  `center ± axis * max(0, height / 2 - radius)`, so `height` runs end to end and a capsule no
  taller than its diameter is a sphere (`VRCPhysBoneColliderBase` says so outright). Jiggle puts
  them at `± height / 2`, so its height is centre to centre. Convert by subtracting a diameter.
  Dynamic Bone measures the same way as VRChat (`DynamicBoneCollider.Prepare`,
  `h = height / 2 - radius`). VRM states the two centres directly.
- **Plane orientation.** VRChat's `axis` is `rotation * Vector3.up` for capsules and planes
  alike. Jiggle snaps a capsule to one of three axes and a plane to local Y only
  (`localToWorldMatrix.c1`), with no axis field for planes. A rotated plane cannot be written
  and is reported. In the reference project, 8 of 23 PhysBone planes are rotated.

## The two heuristic rows

Collected in `JiggleMappingProfile` so they are data, not constants buried in code.

**stiffness.** `pull * PullToStiffness + stiffness * StiffnessToStiffness`, where the stiffness
term is zero under Simplified integration because the setting does not exist there. Pull is the
force returning bones to the rest pose, which is the PhysBone setting closest to what jiggle
calls stiffness. Defaults 1.0 and 0.5.

**drag.** `Lerp(DragAtNoSpring, DragAtFullSpring, spring)`, defaults 0.6 and 0.05. Spring is how
much a bone wobbles on its way back to rest, so it is the inverse of damping, and drag is
damping. The relationship is inverse but the scales do not line up: `1 - spring` on a default
spring of 0.2 gives drag 0.8, against a jiggle default of 0.1 and a Hair preset of 0.4, which
is far outside that range. Mapping onto a band instead puts a default PhysBone at drag 0.49,
close to the Hair preset.

Both need tuning against real avatars side by side. That is milestone 1 step 7.

## Source data can be out of range

The reference avatar contains `radius: -29.73` on two bones. VRChat evidently tolerates it.
Every value is clamped into jiggle's valid range on the way through, and a clamp emits a
`mapping.clamped` or `physbone.radius.negative` diagnostic rather than passing silently.
