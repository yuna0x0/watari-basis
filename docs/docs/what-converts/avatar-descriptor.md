---
sidebar_position: 4
---

# Avatar descriptor

The [VRChat avatar descriptor](https://creators.vrchat.com/avatars/creating-your-first-avatar/)
becomes a `BasisAvatar`, the component Basis loads an avatar through.

## What carries across

- **View position**, as the avatar's eye position.
- **Visemes**, all fifteen. Both platforms order them the same way, so they map by position
  rather than by name.
- **Blink**, taken from the eyelid blendshape the descriptor names.

A VRM has no descriptor, and its own expressions fill what they can: the five vowels and blink.
See [VRM](vrm.md).

## Head chop

A [VRC Head Chop](https://creators.vrchat.com/avatars/avatar-components/vrc-headchop/) becomes a
Basis Head Chop on the same object, naming the same bones with the same scale factors: 0 scales
a bone away while the wearer is in first person, 1 leaves it. VRChat's global factor is
multiplied into each bone.

A bone VRChat scaled away only in VR, or only on desktop, is scaled away in both. Basis has no
such condition: `headChop.condition.dropped`.

## What Basis fills in itself

Opening the `BasisAvatar` inspector for the first time makes Basis populate the animator, the
human scale, the renderer list and the mouth position. Those are left to it rather than guessed,
and a re-conversion updates the component in place instead of replacing it, so nothing Basis
filled in is thrown away.

## What does not carry across

Eye look settings, lip sync mode, the collider layout VRChat uses for its own contacts, and the
expression menu and parameters, which are a separate subject: see
[Menu toggles](menu-toggles.md).

VRC Raycast components fire a ray and set animator parameters from what it hits. Basis has nothing
that does this, so they are reported as `raycast.dropped`. Per-platform overrides and impostor
settings are instructions to VRChat's uploader with no behaviour of their own, reported as
`vrchat.buildSettings`.
