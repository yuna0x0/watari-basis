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
                        $"Aims the {source.AimAxis} axis at the source, as VRM does, with the scene's up as the up direction VRM does not state. Roll around the aim may differ.");
                    break;

                case VrmConstraintKind.Roll:
                    // Nothing in Basis, or in Unity's own set, copies rotation about one axis.
                    plan.RotationAxis = AxisOf(source.RollAxis);
                    plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.constraint.roll",
                        $"A roll constraint about the {AxisName(source.RollAxis)} axis became a rotation constraint limited to that axis. Basis has no roll constraint; this follows rotation, not roll.");
                    break;

                default:
                    plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.constraint.rotation",
                        "VRM copies the source's turn from its rest pose; Basis takes its rotation, offset to hold the authored pose. They differ if the source's rest pose changes.");
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
