---
sidebar_position: 7
---

# Modular Avatar

[Modular Avatar](https://modular-avatar.nadena.dev/) applies its components at Basis build time
when it, NDMF and the Basis NDMF platform are installed in the project, and much of what it does
then needs no conversion. Its components are read here so the parts that cannot work on Basis are
handled rather than silently doing nothing.

Modular Avatar does not need to be installed for this. Its components are read from the prefab
like every other source, and all of them are named, so a component this does not handle is
reported as what it is rather than as an unknown script.

## Left to Modular Avatar

`Merge Armature`, `Bone Proxy`, `Mesh Settings` and the rest of the components that rearrange
the hierarchy or the meshes do platform-independent work. They are reported as left alone rather
than as unrecognised, and nothing is written for them. `Blendshape Sync` and `Parameters` are
listed with them but their build passes are VRChat-only in Modular Avatar 1.18.7, so they do
nothing on Basis.

## Rebuilt

`Menu Item` targets VRChat's expression menu and `Merge Animator` targets its animator layer
slots, neither of which exists on Basis, so clothing that installs a toggle this way does nothing
there.

Read together, those two describe a toggle completely: the menu item names the parameter, the
merged animator holds the layer that implements it. Those are traced and rebuilt as Vixxy
controls, on the same terms as [menu toggles](menu-toggles.md) from the avatar's own menu.

`Object Toggle` needs no animator at all. It switches objects while its own object is active, and
a menu item on that object makes it active, so the two together say what a toggle and its clips
say. Objects it does not name keep the state the avatar was authored with, which is the
same rule everywhere else. An inverted toggle acts while the menu item is off. A menu item with no
parameter gets the one Modular Avatar assigns at build, from the object's name, and its default,
saved and synced flags carry onto the control.

Paths inside a merged animator's clips are relative to the object the animator was merged at, and
are rebased before anything is resolved. An `Object Toggle` entry is resolved by its object
reference first, then by its path, which Modular Avatar records from the avatar root.

## Shape Changers with no menu item

An outfit's `Shape Changer` usually sits on an always-active part and shrinks the avatar's body
under it. Modular Avatar bakes such a rule at build as a constant, on Basis as well when it is
installed with the Basis NDMF platform. The conversion writes the same blendshape values onto the
renderers, so the result is visible in the editor and holds without Modular Avatar: a Set entry
as its value, a Delete entry as the shape at 100, since nothing here removes vertices.
`modularAvatar.shapeChanger.applied`, `modularAvatar.shapeChanger.missing`.

## Reported, not rebuilt

- `Shape Changer` under a menu item, `Material Setter` and `Material Swap` react to a menu item,
  and are listed as `modularAvatar.shapeChanger.menu` and `modularAvatar.menus`.
- Components that act on VRChat's own systems, its colliders, its head chop, its MMD layers.
  There is nothing for them to act on under Basis, and they are reported as
  `modularAvatar.vrchatOnly`.

## Not covered

- Gimmick controllers whose layers are steered by several parameters at once, which are reported
  rather than guessed at.
- Menu structure. A rebuilt control keeps its label, not its place in a menu tree.
