---
sidebar_position: 4
---

# Conversion options

A conversion covers the object you pick and its children. What it writes can be narrowed, either
by kind or one item at a time.

## Basic

Under **Targets**, each row is a kind of thing a conversion produces, with a count beside
it:

| Option | What it writes |
|---|---|
| Physics | Jiggle physics rigs, from PhysBones, VRM spring bones and Dynamic Bone |
| Constraints | Basis constraints, from VRChat and VRM constraints |
| Avatar descriptor | The `BasisAvatar` component: view position, visemes, blink. Head chops go with it |
| Menu toggles | HVR Vixxy controls and their menu items, from menu toggles and VRM expressions |
| Authored motion | `BasisAuthoredMotion`, with a clip baked from each always-on animator layer |
| Armature links | Clothing bones moved under the avatar's, from VRCFury Armature Links. No component; the hierarchy changes |

A row is greyed out when the avatar has nothing of that kind.

Authored motion is the one row that writes an asset into the project, the baked clip, which stays
there if you undo the conversion. See [Authored motion](what-converts/authored-motion.md).

{/*
  IMAGE PLACEHOLDER: the Targets section with its checkboxes and counts.
  Save as docs/static/img/options-basic.webp, then replace this comment with:
  ![The basic options](/img/options-basic.webp)
*/}

## Advanced

**Advanced** adds:

- **Colliders**, under Physics. Rigs are still written without them, and their bones pass through
  the body instead of resting on it.
- **A checkbox per prefab**, so an accessory parented onto an avatar can be left out. Prefabs
  that hold nothing convertible are summarised rather than listed.
- **A checkbox per rig, constraint, toggle and motion**, each with what it affects.
- **The tuning weights** described in [Physics](what-converts/physics.md).

{/*
  IMAGE PLACEHOLDER: the advanced view with the prefab list and per-item checkboxes expanded.
  Save as docs/static/img/options-advanced.webp, then replace this comment with:
  ![The advanced options](/img/options-advanced.webp)
*/}

## What narrowing does not change

The scan always reads the whole avatar, so the counts and the detected source kind do not move
with what is ticked. What changes is what gets written, and what is reported: diagnostics follow
the selection, and whatever you left out is named as left out in the report.

Choices about kinds are remembered between sessions. Choices about individual items reset when
the avatar is rescanned.
