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

## Bringing the avatar over

1. In the VRChat project, select the avatar prefab, the one that still carries the VRCFury
   components, and go to Assets > Export Assets. Enable **Include dependencies**, so the meshes,
   materials, textures, controllers and menus it references come along, and click Export.
2. In the Basis project, go to Assets > Import Package > Custom Package and import it. The
   VRChat and VRCFury scripts arrive as missing scripts; that is expected and needed, since the
   data is read from them.
3. Open the Watari window, select the avatar, and convert. VRCFury toggles appear under Menu
   toggles and Armature Links under Armature links, alongside the avatar's own physics, constraints
   and menu.

Do not export a copy built with VRCFury's Build an Editor Test Copy. That copy has already had its
VRCFury components applied and removed, its menus and parameters sit in a container VRCFury saves
in Unity's binary form, and its controllers live under `Packages/`, which the export leaves out.
None of that can be read here, and the report says so. The original avatar has everything.

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

**Armature Link.** Clothing bones go under the avatar's, matched by name from the link's target
bone down, the way VRCFury matches them: the target by humanoid bone (falling back up the
humanoid chain), by object, or by a path; the clothing's bones child by child, with the suffix
the link names or the one its root implies removed, and VRCFury's four known mid-bone fixups. A
matched bone is aligned to its avatar bone as the link asks, position, rotation and scale, with
the same scale factor VRCFury derives, and is renamed `[VF] <bone> from <clothing>`. A bone with
no match stays under its clothing parent and moves with it. A bone inside a PhysBone chain stays
with the chain, as VRCFury leaves it. VRCFury also rewrites the clothing's skins to reuse the
avatar's bones and prunes the moved ones; that changes bone count, not where the clothing sits,
and is not done here. A link written in VRCFury's old Auto mode has the clothing's meshes decide
whether the whole tree merges, as VRCFury decides at build. Bones move before anything else is
written. Unity does not let an object leave the prefab instance it belongs to, which is why
VRCFury and Modular Avatar only do this on a build clone; there is none here, so the clothing's
prefab instance is unpacked first, outermost root only, and the report says so
(`apply.unpacked`). Everything it carried was read before that, undo restores it, and a later
conversion of the same avatar reads the clothing no more, since it is no longer linked to a
prefab. The target is **Armature links** in the window.

## Reported, not rebuilt

- A **hold button**, and a toggle with **no menu item** that an animator parameter drives:
  `vrcfury.toggle.dropped`.
- Actions a Vixxy control cannot express: a **material swap**, an **animation clip**, a **scale**,
  a flipbook frame, an FX float, SPS, and the rest: `vrcfury.action.dropped`.
- A toggle's **transition**, **separate local state**, **security lock** or **driven parameter**,
  which only exist while an animator runs: `vrcfury.toggle.animatorOnly`.
- An **exclusive tag**, which turned the other toggles in the group off. Vixxy controls are
  independent: `vrcfury.exclusiveTag`.
- Every other feature, by class and count: `vrcfury.feature.unread`.

## Versions

Read against VRCFury 1.1429.0. Each feature and action carries its own schema version, and the
upgrades VRCFury applies on load are applied here, so an older file reads as VRCFury reads it.
Components in the older list layout, with several features on one component, are read too.
