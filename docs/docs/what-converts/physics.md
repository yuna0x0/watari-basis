---
sidebar_position: 1
---

# Physics

VRChat PhysBones, VRM spring bones and legacy Dynamic Bone all become
[Jiggle Physics](https://github.com/naelstrof/UnityJigglePhysics) rigs, the secondary motion
system Basis ships. Dynamic Bone is an ordinary Unity asset and VRM is a format, not a platform,
so an avatar using either converts whether or not VRChat was ever involved.

This page is about PhysBones and Dynamic Bone. Spring bones have their own page, since the two
VRM formats describe a chain differently: see [VRM](vrm.md).

One rig is written per chain. A PhysBone is one chain, and jiggle physics walks into every child
of the bone it is rooted at, so a PhysBone covering a whole head of hair stays one rig rather than
becoming one per strand. A Dynamic Bone can name several root bones, and each of those becomes a
rig of its own with the component's settings.

## What carries across exactly

- The root bone, the transforms the source ignored, and grab settings. A PhysBone with radius 0
  is not grabbable in VRChat, and the rig is locked from grabbing to match.
- Radius, stretch motion, and how immobile the root is.
- Colliders: sphere, capsule and plane, with the same three shapes on both sides. A capsule's
  height is measured end to end on the source side and between the cap centres on the Basis
  side, and is converted between the two.

## What is fitted

Two PhysBone settings do not mean the same thing on both sides and are fits rather than
conversions:

- **Stiffness**, from VRChat's pull and stiffness.
- **Drag**, from VRChat's spring. Spring's curve is dropped, since drag runs the other way.

Both are exposed under **Advanced** in the window, as weights to adjust before rescanning.

Approximated, and reported as such:

- **Falloff curves** carry over as curves. VRChat samples them by bone index and jiggle physics by
  distance from the root, so a bone reads a different point: `physbone.curves.domain`.
- **Gravity.** PhysBone gravity blends the rest direction toward down; jiggle scales world
  gravity. The value is copied as the multiplier. Dynamic Bone gravity is not comparable and is
  left to the preset: `dynamicbone.gravity`.
- **Multi Child Type.** A root with one child is simulated on both sides. With several, Ignore
  becomes a motionless root, and bones further down with several children swing where VRChat
  held them still.
- **Hinge limits** become a cone of the same angle.

Dynamic Bone is derived rather than fitted. Elasticity is a per-tick fraction toward the pose,
which jiggle squares, so stiffness is its square root, scaled by Update Rate over 60. Dynamic Bone
stiffness caps how far a bone may leave its pose, and becomes jiggle's angle limit at the same
angle; caps wider than 90 degrees are left off. Damping becomes both drag and air drag. Blend
Weight tightens the cap, and 0 switches the component off, so no rig is written.

Everything else is a direct mapping.

Values the source does not determine are taken from the jiggle physics package's own presets.
The preset per rig is guessed from the bone's name and can be changed in the window.

## What does not carry across

- Angle limits wider than jiggle physics can express. No limit is written rather than a tighter
  one.
- Polar limits, which are approximated to a single angle.
- Gravity falloff, max stretch, max squish, endpoint positions, and per-axis limit rotations.
- `Is Animated`, and anything driven by a PhysBone parameter.
- A collider inverted to keep bones inside it.
- A collider marked global, for other avatars' bones. Basis offers an avatar's hands, arms and
  feet to other avatars on its own; a collider elsewhere collides with its own avatar only.
- Which way a plane collider faces, when that is not its transform's Y axis. Basis planes face
  that axis: `collider.planeRotation.dropped`, `collider.planeAxis.dropped`.

## Checking the result

Press **Test In Editor** on the `BasisAvatar` component. Jiggle physics only runs on a calibrated
avatar, so plain Play mode shows nothing moving.
