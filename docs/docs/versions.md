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
| [VRChat SDK](https://creators.vrchat.com/sdk/) | 3.10.5 |
| [UniVRM](https://github.com/vrm-c/UniVRM) | 0.131.2 |
| [Dynamic Bone](https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743) | 1.3.4 |
| [Modular Avatar](https://modular-avatar.nadena.dev/) | 1.18.7, with [NDMF](https://ndmf.nadena.dev/) 1.14.8 |
| [VRCFury](https://vrcfury.com/) | 1.1429.0 |
| Basis | `developer` at [33fc043c1](https://github.com/BasisVR/Basis/commit/33fc043c1149b372fa76ca9748d16cc49ba42cb1), 2026-09-18 |

The Basis row covers what the converter writes to as well as what it reads: Jiggle Physics and HVR
Basis Comms are part of the Basis repository and change with it, at version numbers that do not
move between commits, so the commit is their version.

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

## VRCFury

Toggles, Full Controllers and Armature Links as 1.1429.0 writes them. Files written by an older
VRCFury are read the way VRCFury upgrades them. See [VRCFury](what-converts/vrcfury.md).
