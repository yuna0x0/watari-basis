# 0017: Read the animator for data, never run it

Date: 2026-09-14. Status: accepted.

## Decision

The converter reads an FX controller only to find the constant values a menu entry sets:
which objects, blendshapes, material properties, PhysBones and renderers a toggle switches, and
to what. Everything that exists only while an animator runs is out of scope and reported by
name: gesture blends, sliders that scrub a clip through motion time, layers that interact, state
machines with logic, layer priority.

The line: in, data on components plus the constant values menu entries set; out, anything that
only exists while an animator is running.

## Why

Basis has no animator layer system for avatars. Its targets are Vixxy, a menu entry that sets
values, and Authored Motion, a baked rotation clip. Anything beyond constants per choice has no
runtime on Basis, so "converting" it means either inventing a runtime Basis does not have or
squashing logic into static values by guesswork. Each such step is an approximation with its own
diagnostic, and the set has no end.

## Rejected

- Sampling motion-time clips into slider ends (Shinano's Breast size and Hair length): the
  clips also flip 12 objects partway along and set material properties by hash. Half cannot
  live in a slider; the other half needs time sampling. Two sliders on one avatar, rebuildable by
  hand in Vixxy from the clips' end values.
- Merging FX layers that share a parameter: 14 controllers in the reference content, functional
  today as duplicate menu entries. Would need a "which layer wins" rule, which is animator
  semantics.
- Sub-state machines and Direct blend trees: in the reference content only gesture, face
  tracking and locomotion controllers use them.

## Consequence

New converter work starts from a wrong result on a real avatar or the user's ask, never from a
survey turning up another animator shape. Gaps found in passing get one factual line in the
worklog and nothing else.
