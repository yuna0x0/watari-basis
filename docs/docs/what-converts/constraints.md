---
sidebar_position: 3
---

# Constraints

All six [VRChat constraint](https://creators.vrchat.com/common-components/constraints/) types
become their Basis equivalents: position, rotation, aim, parent, scale and look-at. VRM has three
of its own, which are covered on the [VRM](vrm.md) page.

Basis also ships its own converter for Unity's built-in constraints and Animation Rigging, which
this does not duplicate. If your avatar uses those instead, use
`BasisConstraintConversion` from the Basis SDK.

## What carries across

Sources with their weights and offsets, the rest pose, per-axis freezing, the world up mode and
its reference object, and the active state.

A VRChat constraint can drive a transform other than the one it sits on. Basis constraints, like
Unity's, always drive their own object, so the constraint is written onto the transform it
drives.

## What does not

`Solve In Local Space`, `Freeze To World` and `Rebake Offsets When Unfrozen` have no equivalent
anywhere in Basis, and are reported when they were set.
