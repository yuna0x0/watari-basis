---
sidebar_position: 8
---

# VRCFury

[VRCFury](https://vrcfury.com/) applies its components when VRChat builds or tests an avatar,
and nowhere else, so on a Basis build they do nothing. Its components are read here from the
original avatar, the one that still carries them, and the parts that describe data are rebuilt.
VRCFury does not need to be installed for this.

A copy VRCFury built with its own test-copy tool is not the thing to convert. Its menus and
parameters sit inside a container VRCFury saves in Unity's binary form, which cannot be read
here, and its controllers live under `Packages/`, which a unitypackage export leaves out. The
report names both: `expressions.assetNotText`, `expressions.assetMissing`, `fx.controllerMissing`.

## Rebuilt

**Toggle.** A menu item, a parameter and a list of actions. The on state is what the actions
do; the off state is the rest pose, except that an object action names both sides, since VRCFury
applies the off clip to the resting state at build: a "turn on" object is off at rest whatever the
prefab says. These actions carry across:

- **Object Toggle**, turn on, turn off, or flip the object's authored state.
- **BlendShape**, a value on every skinned renderer that has the shape, or on the one named.
- **Material Property**, a float, colour or vector on every renderer or on the one named. A
  property whose kind was left for VRCFury to detect is asked of the materials, as VRCFury does.

A slider toggle becomes a Vixxy slider between its off and on states, with the authored default.
The saved flag carries onto the control. The menu item takes the last segment of the toggle's
menu path; the folders above it are not rebuilt, the same as for the avatar's own menu.

**Full Controller.** A controller, menus and a parameter list from assets, merged into the
avatar at build. The menus are read like the avatar's own, the FX controller is traced for the
layers those menu items steer, and the clip bindings are moved under the object carrying the
component unless the controller was authored against the avatar root. Binding rewrites are
applied. The result is the same as for [menu toggles](menu-toggles.md) from the avatar's own menu,
and only FX-type controllers are read.

## Reported, not rebuilt

- A **hold button**, and a toggle with **no menu item** that an animator parameter drives:
  `vrcfury.toggle.dropped`.
- Actions a Vixxy control cannot express: a **material swap**, an **animation clip**, a **scale**,
  a flipbook frame, an FX float, SPS, and the rest: `vrcfury.action.dropped`.
- A toggle's **transition**, **separate local state**, **security lock** or **driven parameter**,
  which only exist while an animator runs: `vrcfury.toggle.animatorOnly`.
- An **exclusive tag**, which turned the other toggles in the group off. Vixxy controls are
  independent: `vrcfury.exclusiveTag`.
- **Armature Link.** It attaches clothing bones to the avatar's at build. Nothing here moves the
  bones yet, so what it attaches will not follow the body: `vrcfury.armatureLink`.
- Every other feature, by class and count: `vrcfury.feature.unread`.

## Versions

Read against VRCFury 1.1429.0. Each feature and action carries its own schema version, and the
upgrades VRCFury applies on load are applied here, so an older file reads as VRCFury reads it.
Components in the older list layout, with several features on one component, are read too.
