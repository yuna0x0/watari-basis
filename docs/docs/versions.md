---
sidebar_position: 9
---

# Versions checked

The readers were checked against these releases of each source. A component or field a later
release adds is not read until the converter is checked against it, and shows up as
`source.unknownScript` in the report. The report's first line and the bottom of the window state
the same versions.

| Source | Checked against |
|---|---|
| VRChat SDK | 3.10.5 |
| UniVRM | 0.131.2 |
| Dynamic Bone | 1.3.4 |
| Modular Avatar | 1.18.7, with NDMF 1.14.8 |
| Basis | `developer` at [748fd4d3b](https://github.com/BasisVR/Basis/commit/748fd4d3b2259110c4224cab3391a1723b473f58), 2026-09-12 |

## VRChat SDK

Avatar components and what becomes of each on Basis. The SDK release that added a component is
given where the release notes state it.

| Component | On Basis |
|---|---|
| Avatar descriptor | Converts to a Basis Avatar. See [Avatar descriptor](what-converts/avatar-descriptor.md) |
| PhysBones and colliders | Convert to jiggle physics. See [Physics](what-converts/physics.md) |
| Expression menu and parameters | Toggles, selectors and radials are rebuilt as Vixxy controls. See [Menu toggles](what-converts/menu-toggles.md) |
| Contacts, box-shaped ones included (3.10.4) | Reported as `contacts.dropped` |
| VRC Head Chop | Converts to a Basis Head Chop |
| Constraints, all six types (3.7.0) | Convert to Basis constraints |
| Per-platform overrides (3.8.1) and impostor settings | Reported as `vrchat.buildSettings` |
| VRC Raycast (3.10.3) | Reported as `raycast.dropped` |
| Global PhysBone colliders (3.10.4) | Reported as `collider.global.dropped` |

## VRM

Checked against the specification text for VRMC_vrm 1.0 (expressions, lookAt, firstPerson, meta)
and VRMC_springBone 1.0, and against the consortium's sample models: Seed-san, the constraint and
twist sample, the two isBinary conformance models, the MToon UV animation test, and Alicia 0.51.
All six read and plan without an unrecognised component.

## Modular Avatar

Every component 1.18.7 ships is recognised. See [Modular Avatar](what-converts/modular-avatar.md)
for which are rebuilt, which are left to Modular Avatar and which are reported.
