# 0018: Vixxy targets live in the scanned hierarchy's space

Date: 2026-09-21. Status: accepted.

## Decision

Every object, renderer and rest value a Vixxy control is planned against is a transform of the
hierarchy that was scanned, the scene instance or the asset when planning from a path, whichever
prefab the toggle was read from. `AvatarConversionPlan.HierarchyRoot` names that object and
`HierarchyLocator` maps a source asset's transform to it through the source's placement path.
At write time these translate from the hierarchy root, not from a source.

Rigs, constraints, head chops and the descriptor keep their per-source transforms: they sit on
the prefab that declares them, and the sibling-index path from that prefab is what a repeated
conversion recognises them by.

## Why

A VRCFury toggle's actions name objects anywhere on the avatar. VRCFury resolves an `allRenderers`
blendshape action and an `affectAllMeshes` material action against the avatar object. Resolved
against the toggle's own prefab, a toggle kept in a prefab of its own, which holds no renderer,
lost every blendshape and material action, and a renderer it named in another prefab resolved to
that prefab's asset, whose path meant nothing on the avatar.

The rest state of what a toggle does not set was read from the prefab asset's renderer and
material. An instance whose blendshape weights or materials are overridden in an outer prefab
or in the scene carries different values, and a control filled from the asset switched between
two equal values and did nothing. The instance is what the control will drive, so its values
are the ones to fill from.

## Rejected

A source per subject on the planned control, keeping asset transforms and translating each
from its own prefab. It carries the same information one step removed and reads defaults from
the wrong object unless every default lookup also goes through the hierarchy.
