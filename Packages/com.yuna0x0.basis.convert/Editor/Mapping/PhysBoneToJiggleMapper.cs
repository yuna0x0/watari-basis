using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns one VRCPhysBone into the jiggle parameters that best correspond to it.
    /// <para>
    /// Pure: no scene access, no Unity object lookups, no hierarchy. Chain topology, and
    /// therefore splitting one PhysBone into several rigs, is handled separately, so everything
    /// here can be unit tested directly.
    /// </para>
    /// <para>
    /// Both systems evaluate a falloff curve as <c>value * curve(t)</c>, but PhysBone samples
    /// <c>t</c> by bone index and jiggle by distance from the root, so curves carry across as an
    /// approximation.
    /// </para>
    /// </summary>
    public static class PhysBoneToJiggleMapper
    {
        /// <summary>
        /// Passed as <c>rootChildCount</c> when the caller has no hierarchy. Treated as a root
        /// with several children, the case where Multi Child Type applies.
        /// </summary>
        public const int UnknownChildCount = -1;

        /// <param name="rootChildCount">
        /// Children of the root bone after ignored transforms are removed. PhysBone simulates a
        /// root with exactly one child whatever Multi Child Type says.
        /// </param>
        public static JiggleRigPlan Map(PhysBoneData source, JiggleMappingProfile profile = null,
            int rootChildCount = UnknownChildCount)
        {
            profile ??= JiggleMappingProfile.Default;

            JiggleRigPlan plan = new JiggleRigPlan
            {
                SourcePhysBoneDocumentFileId = source.DocumentFileId,
                RootBoneFileId = source.RootTransformFileId,
                ExcludedTransformFileIds = new List<long>(source.IgnoreTransformFileIds),
                ColliderSourceFileIds = new List<long>(source.ColliderFileIds),
            };

            List<ConversionDiagnostic> log = plan.Diagnostics;
            JiggleParameterPlan parameters = plan.Parameters;

            MapStiffness(source, profile, parameters, log);
            MapDrag(source, profile, parameters, log);
            MapImmobile(source, parameters, log);
            MapGravity(source, parameters, log);
            MapRadius(source, parameters, log);
            MapAngleLimit(source, profile, parameters, log);
            MapStretch(source, plan, parameters, log);
            MapGrab(source, plan, log);
            MapMultiChild(source, plan, rootChildCount, log);
            ReportUnmappable(source, log);

            // PhysBone never lets the root particle leave its animated position. Presets do.
            parameters.RootStretch = 0f;

            return plan;
        }

        private static void MapStiffness(PhysBoneData source, JiggleMappingProfile profile,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            bool advanced = source.IntegrationType == PhysBoneIntegrationType.Advanced;
            float stiffnessWeight = advanced ? profile.StiffnessToStiffness : 0f;

            float value = source.Pull.Value * profile.PullToStiffness
                + source.Stiffness.Value * stiffnessWeight;

            // Pull carries the falloff shape. Combining two curves would need resampling and
            // would misrepresent both, so the pull curve is used as-is and the stiffness curve,
            // if there is one, is reported instead of being quietly discarded.
            parameters.Stiffness = new JiggleCurvedFloatPlan(
                Clamp01(value, "jiggle.stiffness", log), source.Pull.Curve);

            log.Add(DiagnosticSeverity.Approximated, "physbone.pull.stiffness",
                advanced
                    ? $"pull {source.Pull.Value} and stiffness {source.Stiffness.Value} became "
                        + $"jiggle stiffness {parameters.Stiffness.Value.Value}, as a fit."
                    : $"pull {source.Pull.Value} became jiggle stiffness "
                        + $"{parameters.Stiffness.Value.Value}, as a fit.");

            if (advanced && source.Stiffness.HasCurve)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.stiffnessCurve.dropped",
                    "The stiffness falloff curve was dropped. Jiggle has a single stiffness "
                    + "parameter, and it took the pull curve.");
            }
        }

        private static void MapDrag(PhysBoneData source, JiggleMappingProfile profile,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            // PhysBone spring is how much a bone wobbles on its way back to rest, so it is the
            // inverse of damping. Jiggle drag is local-space friction, which is damping. The
            // relationship is inverse but the scales do not line up, so map onto a band rather
            // than using 1 - spring directly, which would produce implausibly heavy damping for
            // ordinary values.
            float spring = Mathf.Clamp01(source.Spring.Value);
            float drag = Mathf.Lerp(profile.DragAtNoSpring, profile.DragAtFullSpring, spring);

            // The curve cannot follow: it scales spring, and drag runs the other way, so a curve
            // fading spring out toward the tip would fade drag out instead of raising it.
            parameters.Drag = new JiggleCurvedFloatPlan(drag);

            log.Add(DiagnosticSeverity.Approximated, "physbone.spring.drag",
                $"spring {source.Spring.Value} became jiggle drag {drag}. "
                + "Spring and drag are inverses but their scales differ, so this is a fit, not "
                + "a conversion.");

            if (source.Spring.HasCurve)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.springCurve.dropped",
                    "The spring falloff curve was dropped. Drag runs opposite to spring, so the "
                    + "curve would have inverted the falloff.");
            }
        }

        private static void MapImmobile(PhysBoneData source, JiggleParameterPlan parameters,
            List<ConversionDiagnostic> log)
        {
            parameters.IgnoreRootMotion =
                Clamp01(source.Immobile.Value, "jiggle.ignoreRootMotion", log);

            log.Add(DiagnosticSeverity.Mapped, "physbone.immobile.ignoreRootMotion",
                $"immobile {source.Immobile.Value} became ignoreRootMotion "
                + $"{parameters.IgnoreRootMotion}.");

            if (source.Immobile.HasCurve)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.immobileCurve.dropped",
                    "The immobile falloff curve was dropped. Jiggle's ignoreRootMotion is a "
                    + "single value with no per-bone curve.");
            }

            if (source.ImmobileType == PhysBoneImmobileType.WorldMotion)
            {
                log.Add(DiagnosticSeverity.Approximated, "physbone.immobileType.world",
                    "Immobile Type was World, which damps only scene-space movement and leaves "
                    + "animation alone. Jiggle's ignoreRootMotion does not make that "
                    + "distinction, so the result will also damp animated motion.");
            }
        }

        private static void MapGravity(PhysBoneData source, JiggleParameterPlan parameters,
            List<ConversionDiagnostic> log)
        {
            parameters.Gravity = new JiggleCurvedFloatPlan(
                source.Gravity.Value, source.Gravity.Curve);

            log.Add(DiagnosticSeverity.Approximated, "physbone.gravity",
                $"gravity {source.Gravity.Value} became the jiggle gravity multiplier, with its "
                + "curve if it had one. PhysBone gravity blends the rest direction toward down; "
                + "jiggle scales world gravity.");

            if (!Mathf.Approximately(source.GravityFalloff.Value, 0f))
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.gravityFalloff.dropped",
                    $"Gravity Falloff {source.GravityFalloff.Value} was dropped. Jiggle has no "
                    + "equivalent, so gravity applies evenly rather than easing off near the "
                    + "rest pose.");
            }
        }

        private static void MapRadius(PhysBoneData source, JiggleParameterPlan parameters,
            List<ConversionDiagnostic> log)
        {
            float radius = source.Radius.Value;
            if (radius < 0f)
            {
                log.Add(DiagnosticSeverity.Warning, "physbone.radius.negative",
                    $"Collision radius was {radius}, which is not a valid radius. Clamped to 0 "
                    + "and collision left off.");
                radius = 0f;
            }

            // Allow Collision governs collision with colliders not listed on the component, which
            // VRChat documents as other players' hands. The listed colliders apply regardless, so
            // the toggle depends on the radius alone.
            bool collides = radius > 0f;
            parameters.CollisionRadius = new JiggleCurvedFloatPlan(radius, source.Radius.Curve);
            parameters.CollisionToggle = collides;

            if (collides)
            {
                log.Add(DiagnosticSeverity.Mapped, "physbone.radius.collisionRadius",
                    $"radius {radius} became collisionRadius, with its curve if it had one.");
            }

            if (!source.AllowCollision && collides)
            {
                log.Add(DiagnosticSeverity.Approximated, "physbone.allowCollision.off",
                    "Allow Collision was off, which excluded other players' hands. Basis "
                    + "registers every avatar's hands, arms and feet as global jiggle colliders "
                    + "and a rig cannot opt out, so this rig collides with them.");
            }
        }

        private static void MapAngleLimit(PhysBoneData source, JiggleMappingProfile profile,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            if (source.LimitType == PhysBoneLimitType.None)
            {
                parameters.AngleLimitToggle = false;
                log.Add(DiagnosticSeverity.Mapped, "physbone.limitType.none",
                    "No angle limit was set, so the jiggle rig has none either.");
                return;
            }

            float degrees = source.MaxAngleX.Value;
            parameters.AngleLimitToggle = true;

            switch (source.LimitType)
            {
                case PhysBoneLimitType.Angle:
                    log.Add(DiagnosticSeverity.Mapped, "physbone.limitType.angle",
                        $"Angle limit of {degrees} degrees became a jiggle angle limit.");
                    break;

                case PhysBoneLimitType.Hinge:
                    log.Add(DiagnosticSeverity.Approximated, "physbone.limitType.hinge",
                        $"Hinge limit of {degrees} degrees became a jiggle cone of that angle. "
                        + "A hinge also keeps the bone on one plane; the cone does not.");
                    break;

                case PhysBoneLimitType.Polar:
                    degrees = Mathf.Max(source.MaxAngleX.Value, source.MaxAngleZ.Value);
                    log.Add(DiagnosticSeverity.Approximated, "physbone.limitType.polar",
                        $"Polar limit had separate pitch {source.MaxAngleX.Value} and yaw "
                        + $"{source.MaxAngleZ.Value} angles. Jiggle has one cone limit, so the "
                        + $"wider of the two, {degrees} degrees, was used.");
                    break;
            }

            // Jiggle's angle limit tops out at AngleLimitDegreesAtOne, so a wider PhysBone limit
            // cannot be expressed. Clamping would make the result tighter than the source, which
            // is worse than having no limit, so drop the limit instead and say so.
            if (degrees >= profile.AngleLimitDegreesAtOne)
            {
                parameters.AngleLimitToggle = false;
                log.Add(DiagnosticSeverity.Approximated, "physbone.limitType.tooWide",
                    $"The limit of {degrees} degrees is wider than jiggle's angle limit can "
                    + $"express, which stops at {profile.AngleLimitDegreesAtOne}. Keeping it "
                    + "would have constrained the bones more than the original did, so the "
                    + "limit was left off.");
                return;
            }

            parameters.AngleLimit = new JiggleCurvedFloatPlan(
                degrees / profile.AngleLimitDegreesAtOne, source.MaxAngleX.Curve);

            if (source.LimitRotation != Vector3.zero)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.limitRotation.dropped",
                    $"Limit Rotation {source.LimitRotation} was dropped. Jiggle's angle limit is "
                    + "always centred on the rest pose and cannot be re-aimed.");
            }
        }

        private static void MapStretch(PhysBoneData source, JiggleRigPlan plan,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            parameters.Stretch = new JiggleCurvedFloatPlan(
                Clamp01(source.StretchMotion.Value, "jiggle.stretch", log),
                source.StretchMotion.Curve);

            if (source.StretchMotion.Value > 0f)
            {
                log.Add(DiagnosticSeverity.Mapped, "physbone.stretchMotion.stretch",
                    $"stretchMotion {source.StretchMotion.Value} became jiggle stretch, with its "
                    + "curve if it had one.");
            }

            if (source.MaxStretch.Value > 0f)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.maxStretch.dropped",
                    $"Max Stretch {source.MaxStretch.Value} was dropped. It bounds how far a bone "
                    + "may lengthen; jiggle has no such bound.");
            }

            if (source.MaxSquish.Value > 0f)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.maxSquish.dropped",
                    $"Max Squish {source.MaxSquish.Value} was dropped. It bounds how far a bone "
                    + "may shorten; jiggle has no such bound.");
            }
        }

        private static void MapGrab(PhysBoneData source, JiggleRigPlan plan,
            List<ConversionDiagnostic> log)
        {
            // VRChat skips grabbing when the radius is 0, whatever Allow Grabbing says.
            bool grabbable = source.AllowGrabbing && source.Radius.Value > 0f;
            plan.LockFromGrabbing = !grabbable;
            log.Add(DiagnosticSeverity.Mapped, "physbone.allowGrabbing",
                grabbable
                    ? "Grabbing stays enabled."
                    : source.AllowGrabbing
                        ? "Radius was 0, which VRChat treats as not grabbable, so the jiggle "
                            + "rig is locked from grabbing."
                        : "Grabbing was disabled, so the jiggle rig is locked from grabbing.");

            if (source.AllowPosing)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.allowPosing.dropped",
                    "Allow Posing was on. Jiggle bones spring back when released and cannot be "
                    + "left posed.");
            }

            if (source.SnapToHand)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.snapToHand.dropped",
                    "Snap To Hand was on and has no jiggle equivalent.");
            }
        }

        private static void MapMultiChild(PhysBoneData source, JiggleRigPlan plan,
            int rootChildCount, List<ConversionDiagnostic> log)
        {
            // Multi Child Type only applies to a root with several children. With one child the
            // root is simulated toward it, which is what a jiggle root does on its own.
            if (rootChildCount == 1)
            {
                plan.ExcludeRoot = false;
                log.Add(DiagnosticSeverity.Mapped, "physbone.multiChildType.oneChild",
                    "The root has one child, so PhysBone simulates it. The jiggle root moves too.");
                return;
            }

            switch (source.MultiChildType)
            {
                case PhysBoneMultiChildType.Ignore:
                    plan.ExcludeRoot = true;
                    log.Add(DiagnosticSeverity.Mapped, "physbone.multiChildType.ignore",
                        "Multi Child Type was Ignore, so the root bone itself stays still. "
                        + "That is jiggle's Motionless Root.");
                    break;

                case PhysBoneMultiChildType.First:
                case PhysBoneMultiChildType.Average:
                    plan.ExcludeRoot = true;
                    log.Add(DiagnosticSeverity.Approximated, "physbone.multiChildType.blended",
                        $"Multi Child Type was {source.MultiChildType}, which moves the shared "
                        + "root from its child chains. Jiggle cannot blend a shared root, so the "
                        + "root was left motionless instead.");
                    break;
            }
        }

        private static void ReportUnmappable(PhysBoneData source, List<ConversionDiagnostic> log)
        {
            if (!string.IsNullOrEmpty(source.Parameter))
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.parameter.dropped",
                    $"The animator parameter prefix '{source.Parameter}' was dropped. Basis has "
                    + "no playable layers; rebuild this with HVR Vixxy if you need it.");
            }

            if (source.IsAnimated)
            {
                log.Add(DiagnosticSeverity.Mapped, "physbone.isAnimated",
                    "Is Animated was on, so the rest pose followed animation each frame. Jiggle "
                    + "always does.");
            }

            if (source.Pull.HasCurve || source.Gravity.HasCurve || source.Radius.HasCurve
                || source.MaxAngleX.HasCurve || source.StretchMotion.HasCurve)
            {
                log.Add(DiagnosticSeverity.Approximated, "physbone.curves.domain",
                    "Falloff curves carried over. PhysBone samples them by bone index and jiggle "
                    + "by distance from the root, so a bone reads a different point on the curve.");
            }

            if (source.EndpointPosition != Vector3.zero)
            {
                log.Add(DiagnosticSeverity.Dropped, "physbone.endpointPosition.dropped",
                    $"Endpoint Position {source.EndpointPosition} was dropped. Jiggle derives "
                    + "its own chain endpoint, so the last bone may behave differently.");
            }
        }

        private static float Clamp01(float value, string target, List<ConversionDiagnostic> log)
        {
            if (value >= 0f && value <= 1f)
            {
                return value;
            }

            float clamped = Mathf.Clamp01(value);
            log.Add(DiagnosticSeverity.Warning, "mapping.clamped",
                $"{target} would have been {value}, which is out of range. Clamped to {clamped}.");
            return clamped;
        }
    }
}
