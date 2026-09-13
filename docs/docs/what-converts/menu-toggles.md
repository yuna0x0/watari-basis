---
sidebar_position: 5
---

# Menu toggles

Basis has no expression menu, and no FX layer. What it has is
[HVR Vixxy](https://docs.hai-vr.dev/docs/basis/avatar-customization/vixxy), which ships with
Basis and holds a control as a set of choices with a value per choice. Menu toggles are rebuilt
as Vixxy controls with a menu item each.

## How a toggle is found

The expression menu names a parameter. The FX controller has a layer steered by that parameter,
with a clip on each side. Both are read, and the two clips are reduced to what they actually do.

A layer counts only when a single parameter of the avatar's own steers it, which keeps gesture
layers that mention a toggle as a secondary condition from being read as that toggle's own.

VRChat's own parameters are treated differently. A gimmick that only runs for the wearer tests
`IsLocal`, one that stops in a chair tests `InStation`, and Basis drives none of them. A layer
guarded that way is still the toggle's, and what the rebuilt control no longer waits for is
reported as `vixxy.builtinGuard`. It only counts as a guard when no transition tests the built-in
on its own: a layer with a state per gesture belongs to the gesture, not to the menu.

A layer where one value of the parameter leads to two different states is left alone as well.
Something other than the parameter is choosing between them, and reading it would keep whichever
transition happened to come first.

## What can be rebuilt

- **Objects switched on and off.**
- **Blendshapes**, as a weight per choice.
- **Material properties**, including colours animated one channel at a time.

Where a clip sets something on one side only, the other side keeps the avatar's authored value,
read from the object rather than assumed to be the opposite.

## Controls with more than two states

An expression menu commonly puts several entries on one parameter, each setting a different
value, so that picking one clears the rest. Those entries are grouped by their parameter and
become one Vixxy control with a choice per value, named after the menu entry that selects it.

## Radial puppets

A radial drives a float rather than switching between states: its menu entry names its parameter
under `subParameters`, and its layer holds a blend tree rather than transitions. It becomes a
Vixxy control presented as a slider, with the lowest and highest motions in the tree as its two
choices.

Vixxy interpolates in a straight line between a control's choices, so the two ends carry across
exactly and the shape of the sweep between them does not. A tree holding motions between its ends
is reported as `vixxy.puppetEnds`.

## What cannot

- Anything that animates over time other than rotation. Vixxy holds a value per choice, not a
  curve. A toggle whose clip turns transforms is converted: the animation becomes
  [authored motion](authored-motion.md) that the control switches on.
- Two-axis and four-axis puppets.
- Expression parameters as a system. Vixxy controls hold their own state, so there is no
  parameter list to recreate, and anything driven by parameters outside a toggle has to be
  rebuilt by hand.

A toggle that cannot be rebuilt is reported and left alone rather than partly converted. The one
exception is an object the clip names that this avatar does not have: the control is written
without it and the report says so (`vixxy.targetMissing`).
