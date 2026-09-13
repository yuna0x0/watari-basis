using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRM node constraint into the Basis constraint that corresponds to it.
    /// <para>
    /// A VRM constraint drives the object it sits on and follows one source, so there is no
    /// target to relocate and no source list to flatten. What differs is the arithmetic: VRM
    /// copies a source's rotation as a delta from its rest pose, while Basis constraints follow
    /// Unity's, which take the source's rotation itself. The two agree while both objects sit at
    /// the pose they were authored in and drift apart as the source's rest changes, so a
    /// rotation constraint is reported as a fit.
    /// </para>
    /// </summary>
    public static class VrmConstraintToBasisMapper
    {
        public static BasisConstraintPlan Map(VrmConstraintData source)
        {
            BasisConstraintPlan plan = new BasisConstraintPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                HostFileId = source.OwnerGameObjectFileId,
                Kind = source.Kind == VrmConstraintKind.Aim
                    ? BasisConstraintKind.Aim
                    : BasisConstraintKind.Rotation,
                Weight = Mathf.Clamp01(source.Weight),
            };

            if (source.SourceTransformFileId == 0L)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.constraint.noSource",
                    "A VRM constraint names no source, so it does nothing. It was still created, "
                    + "to keep the avatar's structure recognisable.");
            }
            else
            {
                plan.Sources.Add(new BasisConstraintSourcePlan
                {
                    TransformFileId = source.SourceTransformFileId,
                    Weight = 1f,
                });
            }

            switch (source.Kind)
            {
                case VrmConstraintKind.Aim:
                    plan.AimVector = source.AimVector;
                    plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.constraint.aim",
                        $"An aim constraint points this object's {source.AimAxis} axis at its "
                        + "source. Basis aims the same way but also holds an up direction, which "
                        + "VRM does not state, so the scene's up is used and the roll around the "
                        + "aim may differ.");
                    break;

                case VrmConstraintKind.Roll:
                    // Nothing in Basis, or in Unity's own set, copies rotation about one axis.
                    plan.RotationAxis = AxisOf(source.RollAxis);
                    plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.constraint.roll",
                        $"A roll constraint copies the source's rotation about its "
                        + $"{AxisName(source.RollAxis)} axis alone. Basis has no constraint that "
                        + "does that, so this became a rotation constraint limited to that axis, "
                        + "which follows the source's rotation rather than its roll.");
                    break;

                default:
                    plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.constraint.rotation",
                        "A rotation constraint copies how far the source has turned from its "
                        + "rest pose. A Basis constraint takes the source's rotation itself, "
                        + "offset so the authored pose holds at rest. The two differ if the "
                        + "source's rest pose changes.");
                    break;
            }

            return plan;
        }

        private static ConstraintAxes AxisOf(int rollAxis)
        {
            return rollAxis switch
            {
                0 => ConstraintAxes.X,
                1 => ConstraintAxes.Y,
                _ => ConstraintAxes.Z,
            };
        }

        private static string AxisName(int rollAxis)
        {
            return rollAxis switch
            {
                0 => "X",
                1 => "Y",
                _ => "Z",
            };
        }
    }
}
