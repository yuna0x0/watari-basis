---
sidebar_position: 8
---

# VRCFury

VRCFury applies its components only when VRChat builds an avatar. On Basis they do nothing, so
Watari reads them from the original avatar and rebuilds what they describe. VRCFury does not
need to be installed.

## Bringing the avatar over

1. In the VRChat project, select the avatar prefab that still carries the VRCFury components.
   Assets > Export Assets, enable **Include dependencies**, Export.
2. In the Basis project, Assets > Import Package > Custom Package.
3. Select the avatar in the Watari window and convert.

Do not export a copy made with Build an Editor Test Copy. Its components are already applied
and removed, its menus are stored in binary form, and its controllers live under `Packages/`,
which the export leaves out. The report names such a copy as `source.builtCopy`.

## Toggle

A menu item, a parameter and a list of actions. Rebuilt as a Vixxy control.

| Action | On Basis |
|---|---|
| Object Toggle | Object on or off in each state |
| BlendShape | Shape value on every skinned mesh that has it, or the one named |
| Material Property | Float, colour or vector on every renderer, or the one named |
| Material swap, animation clip, scale, flipbook, FX float, SPS, others | Not rebuilt: `vrcfury.action.dropped` |

- A slider toggle becomes a Vixxy slider. The saved flag and default carry over.
- The menu item is the last segment of the toggle's menu path. Folders are not rebuilt.
- A hold button, or a toggle with no menu item, is not rebuilt: `vrcfury.toggle.dropped`.
- Transitions, separate local states, the security lock and driven parameters are ignored:
  `vrcfury.toggle.animatorOnly`.
- Toggles sharing an exclusive tag do not turn each other off: `vrcfury.exclusiveTag`.

## Full Controller

A controller, menus and parameters merged from assets. Read like the avatar's own menu and FX
controller; see [Menu toggles](menu-toggles.md). Only FX-type controllers are read.

## Armature Link

Clothing bones are parented under the avatar's bones. The target is **Armature links** in the
window.

- Bones are matched by name from the link's target down, as VRCFury matches them.
- A matched bone is aligned to its avatar bone as the link asks and renamed
  `[VF] <bone> from <clothing>`.
- A bone with no match stays under its clothing parent.
- A bone inside a PhysBone chain stays with the chain: `vrcfury.armatureLink.physBone`.
- The clothing's prefab instance is unpacked first, because Unity does not allow a bone to
  leave one: `apply.unpacked`. Undo restores it. Converting the avatar again no longer reads that
  clothing.
- Bone count is not reduced. VRCFury also merges the clothing's skins onto the avatar's bones;
  that is not done here.

## Not read

Every other VRCFury feature is listed by name: `vrcfury.feature.unread`.

## Versions

Read against VRCFury 1.1429.0. Older files are read the way VRCFury upgrades them.
