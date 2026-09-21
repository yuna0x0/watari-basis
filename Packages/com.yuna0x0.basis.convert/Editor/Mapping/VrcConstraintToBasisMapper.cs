using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRChat constraint into the Basis constraint that corresponds to it.
    /// <para>
    /// Both systems follow Unity's own built-in constraints, so most fields transfer unchanged.
    /// Two things do not.
    /// </para>
    /// <para>
    /// A VRChat constraint can drive a transform other than the object it sits on. Basis
    /// constraints, like Unity's, always drive their own object, so the component is placed on
    /// the driven transform instead and the move is reported.
    /// </para>
    /// <para>
    /// <c>SolveInLocalSpace</c> and <c>FreezeToWorld</c> have no equivalent anywhere and are
    /// reported as dropped.
    /// </para>
    /// </summary>
    public static class VrcConstraintToBasisMapper
    {
        public static BasisConstraintPlan Map(VrcConstraintData source)
        {
            BasisConstraintPlan plan = new BasisConstraintPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                HostFileId = source.OwnerGameObjectFileId,
                Kind = KindOf(source.Kind),
                Active = source.IsActive,
                // VRChat forces Locked on in play mode. Basis unlocked returns the axes a
                // constraint does not drive to the captured rest pose, so only locked matches.
                Locked = true,
                Weight = Mathf.Clamp01(source.GlobalWeight),
            };

            if (!source.Locked)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "constraint.locked",
                    "Locked was off. VRChat ignores that in play mode, so the Basis constraint "
                    + "is locked.");
            }

            if (source.GlobalWeight < 0f || source.GlobalWeight > 1f)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.weight.clamped",
                    $"Weight was {source.GlobalWeight}, outside the 0 to 1 range Basis accepts. "
                    + $"Clamped to {plan.Weight}.");
            }

            MapSources(source, plan);
            MapTarget(source, plan);
            MapKindSpecific(source, plan);
            ReportUnmappable(source, plan);

            return plan;
        }

        private static BasisConstraintKind KindOf(VrcConstraintKind kind)
        {
            return kind switch
            {
                VrcConstraintKind.Position => BasisConstraintKind.Position,
                VrcConstraintKind.Rotation => BasisConstraintKind.Rotation,
                VrcConstraintKind.Scale => BasisConstraintKind.Scale,
                VrcConstraintKind.Parent => BasisConstraintKind.Parent,
                VrcConstraintKind.Aim => BasisConstraintKind.Aim,
                _ => BasisConstraintKind.LookAt,
            };
        }

        private static void MapSources(VrcConstraintData source, BasisConstraintPlan plan)
        {
            foreach (VrcConstraintSource entry in source.Sources)
            {
                if (entry.SourceTransformFileId == 0L)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.source.empty",
                        "A source slot had no transform assigned and was dropped.");
                    continue;
                }

                plan.Sources.Add(new BasisConstraintSourcePlan
                {
                    TransformFileId = entry.SourceTransformFileId,
                    Weight = Mathf.Max(0f, entry.Weight),
                    PositionOffset = entry.ParentPositionOffset,
                    RotationOffset = entry.ParentRotationOffset,
                });
            }

            // VRChat serializes the first sixteen sources as numbered slots and the rest in an
            // overflow list, which is not read. A constraint with more than sixteen is rare and
            // would otherwise lose the extras without saying so. Compared against the slots
            // read, empty ones included: an empty slot is reported above and is not overflow.
            if (source.DeclaredSourceCount > source.Sources.Count)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.source.overflow",
                    $"The constraint declares {source.DeclaredSourceCount} sources but only "
                    + $"{plan.Sources.Count} could be read. VRChat keeps anything past the "
                    + "sixteenth in an overflow list this does not read yet, so those were not "
                    + "carried over.");
            }

            // Basis divides the blend by the total weight, so a lone source counts in full.
            // VRChat blends toward the offset by the source's weight.
            if (plan.Sources.Count == 1 && !Mathf.Approximately(plan.Sources[0].Weight, 1f))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated,
                    "constraint.source.weight.normalized",
                    $"The only source had weight {plan.Sources[0].Weight}. Basis normalises "
                    + "source weights, so a lone source follows fully.");
            }

            if (plan.Sources.Count == 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.noSources",
                    "This constraint has no sources, so it does nothing. It was still created, "
                    + "to keep the avatar's structure recognisable.");
            }
        }

        private static void MapTarget(VrcConstraintData source, BasisConstraintPlan plan)
        {
            if (source.TargetTransformFileId == 0L
                || source.TargetTransformFileId == source.OwnerGameObjectFileId)
            {
                return;
            }

            plan.HostFileId = source.TargetTransformFileId;
            plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "constraint.retargeted",
                "This constraint drove a transform other than the object it sat on. Basis "
                + "constraints always drive their own object, so the converted constraint was "
                + "placed on the transform it drives instead.");
        }

        private static void MapKindSpecific(VrcConstraintData source, BasisConstraintPlan plan)
        {
            switch (source.Kind)
            {
                case VrcConstraintKind.Position:
                    plan.TranslationAtRest = source.PositionAtRest;
                    plan.TranslationOffset = source.PositionOffset;
                    plan.TranslationAxis = AxesOf(
                        source.AffectsPositionX, source.AffectsPositionY, source.AffectsPositionZ);
                    break;

                case VrcConstraintKind.Rotation:
                    plan.RotationAtRest = source.RotationAtRest;
                    plan.RotationOffset = source.RotationOffset;
                    plan.RotationAxis = AxesOf(
                        source.AffectsRotationX, source.AffectsRotationY, source.AffectsRotationZ);
                    break;

                case VrcConstraintKind.Scale:
                    plan.ScaleAtRest = source.ScaleAtRest;
                    plan.ScaleOffset = source.ScaleOffset;
                    plan.ScaleAxis = AxesOf(
                        source.AffectsScaleX, source.AffectsScaleY, source.AffectsScaleZ);
                    break;

                case VrcConstraintKind.Parent:
                    plan.TranslationAtRest = source.PositionAtRest;
                    plan.RotationAtRest = source.RotationAtRest;
                    plan.TranslationAxis = AxesOf(
                        source.AffectsPositionX, source.AffectsPositionY, source.AffectsPositionZ);
                    plan.RotationAxis = AxesOf(
                        source.AffectsRotationX, source.AffectsRotationY, source.AffectsRotationZ);
                    break;

                case VrcConstraintKind.Aim:
                    plan.AimVector = source.AimAxis;
                    plan.UpVector = source.UpAxis;
                    plan.WorldUpType = (ConstraintWorldUp)source.WorldUp;
                    if (plan.WorldUpType == ConstraintWorldUp.None)
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Approximated,
                            "constraint.worldUp.none",
                            "World Up Type was None, which leaves roll free in VRChat. Basis "
                            + "treats None as the scene's up.");
                    }

                    plan.WorldUpVector = source.WorldUpVector;
                    plan.WorldUpTransformFileId = source.WorldUpTransformFileId;
                    plan.RotationAtRest = source.RotationAtRest;
                    plan.RotationOffset = source.RotationOffset;
                    plan.RotationAxis = AxesOf(
                        source.AffectsRotationX, source.AffectsRotationY, source.AffectsRotationZ);
                    break;

                case VrcConstraintKind.LookAt:
                    // VRChat applies roll as a rotation of -Roll about the look axis; Basis
                    // applies +Roll about the same axis.
                    plan.Roll = -source.Roll;
                    plan.UseUpObject = source.UseUpTransform;
                    plan.WorldUpTransformFileId = source.WorldUpTransformFileId;
                    plan.RotationAtRest = source.RotationAtRest;
                    plan.RotationOffset = source.RotationOffset;
                    break;
            }
        }

        private static void ReportUnmappable(VrcConstraintData source, BasisConstraintPlan plan)
        {
            if (source.SolveInLocalSpace)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped,
                    "constraint.solveInLocalSpace.dropped",
                    "Solve In Local Space was on. It is specific to VRChat's constraints and has "
                    + "no equivalent in Basis, so the constraint solves in world space.");
            }

            if (source.FreezeToWorld)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped,
                    "constraint.freezeToWorld.dropped",
                    "Freeze To World was on and has no Basis equivalent.");
            }
        }

        private static ConstraintAxes AxesOf(bool x, bool y, bool z)
        {
            ConstraintAxes axes = ConstraintAxes.None;
            if (x)
            {
                axes |= ConstraintAxes.X;
            }

            if (y)
            {
                axes |= ConstraintAxes.Y;
            }

            if (z)
            {
                axes |= ConstraintAxes.Z;
            }

            return axes;
        }
    }
}
