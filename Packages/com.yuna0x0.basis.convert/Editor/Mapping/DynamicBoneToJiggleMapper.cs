using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a Dynamic Bone into jiggle rigs.
    /// <para>
    /// Dynamic Bone's damping and jiggle's drag are the same per-tick damping on the same 0 to
    /// 1 scale. Elasticity moves a bone a fraction of the way back to its pose each tick, which
    /// jiggle does with the square of its stiffness. Dynamic Bone's stiffness is a cap on how
    /// far a bone may leave its pose, which is jiggle's angle limit. Gravity is added per tick
    /// without a time step and cancelled at the rest pose, so it has no jiggle counterpart.
    /// </para>
    /// <para>
    /// One component can drive several roots, and each becomes its own rig, since they are
    /// separate chains rather than branches of one. A component with no root simulates nothing
    /// and produces no rig.
    /// </para>
    /// </summary>
    public static class DynamicBoneToJiggleMapper
    {
        /// <summary>
        /// Written when the source radius is 0 and colliders are listed. Dynamic Bone collides
        /// such bones as points; jiggle skips collision entirely at radius 0.
        /// </summary>
        public const float PointCollisionRadius = 0.01f;

        /// <summary>Ticks per second the fit is normalised to; Dynamic Bone's default rate.</summary>
        private const float ReferenceUpdateRate = 60f;

        public static List<JiggleRigPlan> Map(
            DynamicBoneData source, JiggleMappingProfile profile = null)
        {
            profile ??= JiggleMappingProfile.Default;

            List<long> roots = ResolveRoots(source);
            List<JiggleRigPlan> plans = new List<JiggleRigPlan>();

            if (Mathf.Approximately(source.BlendWeight, 0f))
            {
                return plans;
            }

            foreach (long root in roots)
            {
                plans.Add(MapOne(source, root, profile, roots.Count));
            }

            return plans;
        }

        /// <summary>
        /// The roots this component drives: the single Root, then any extra Roots. Dynamic Bone
        /// builds no chain when both are empty, so neither does this.
        /// </summary>
        private static List<long> ResolveRoots(DynamicBoneData source)
        {
            List<long> roots = new List<long>();

            if (source.RootFileId != 0L)
            {
                roots.Add(source.RootFileId);
            }

            foreach (long extra in source.RootFileIds)
            {
                if (extra != 0L && !roots.Contains(extra))
                {
                    roots.Add(extra);
                }
            }

            return roots;
        }

        private static JiggleRigPlan MapOne(
            DynamicBoneData source, long rootFileId, JiggleMappingProfile profile, int rootCount)
        {
            JiggleRigPlan plan = new JiggleRigPlan
            {
                SourcePhysBoneDocumentFileId = source.DocumentFileId,
                RootBoneFileId = rootFileId,
                ExcludedTransformFileIds = new List<long>(source.ExclusionFileIds),
                ColliderSourceFileIds = new List<long>(source.ColliderFileIds),
            };

            List<ConversionDiagnostic> log = plan.Diagnostics;
            JiggleParameterPlan parameters = plan.Parameters;

            if (rootCount > 1)
            {
                log.Add(DiagnosticSeverity.Mapped, "dynamicbone.multipleRoots",
                    $"This Dynamic Bone drives {rootCount} separate chains. Each became its own "
                    + "jiggle rig, sharing these settings.");
            }

            float rateScale = source.UpdateRate > 0f
                ? source.UpdateRate / ReferenceUpdateRate
                : 1f;

            MapElasticity(source, rateScale, parameters, log);
            MapStiffness(source, profile, parameters, log);
            MapDamping(source, parameters, log);
            MapInert(source, parameters, log);
            MapRadius(source, parameters, log);
            MapGravity(source, parameters, log);
            ReportUnmappable(source, rateScale, log);

            return plan;
        }

        /// <summary>
        /// Dynamic Bone moves each bone <c>elasticity</c> of the way back to its pose per tick.
        /// Jiggle moves it <c>stiffness²</c> of the way per step, so the square root gives the
        /// same fraction. The rate scale folds in Update Rate, which multiplies the fraction.
        /// </summary>
        private static void MapElasticity(DynamicBoneData source, float rateScale,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            float fraction = Mathf.Clamp01(source.Elasticity.Value * rateScale);
            float stiffness = Mathf.Sqrt(fraction);

            parameters.Stiffness = new JiggleCurvedFloatPlan(stiffness, source.Elasticity.Curve);
            log.Add(DiagnosticSeverity.Approximated, "dynamicbone.elasticity.stiffness",
                $"elasticity {source.Elasticity.Value} became jiggle stiffness {stiffness:0.###}. "
                + "Both return a bone to its pose by a fraction per tick; jiggle squares its "
                + "stiffness, so the square root was taken. Its curve was kept.");
        }

        /// <summary>
        /// Dynamic Bone stiffness caps how far a bone may stray from its pose: displacement up to
        /// <c>2·length·(1−s)</c>, an angle of <c>2·asin(1−s)</c>. Blend Weight below 1 pulls the
        /// cap toward rigid. Jiggle expresses that as its angle limit.
        /// </summary>
        private static void MapStiffness(DynamicBoneData source, JiggleMappingProfile profile,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            float stiffness = Mathf.Clamp01(
                Mathf.Lerp(1f, source.Stiffness.Value, Mathf.Clamp01(source.BlendWeight)));

            if (!Mathf.Approximately(source.BlendWeight, 1f))
            {
                log.Add(DiagnosticSeverity.Approximated, "dynamicbone.blendWeight",
                    $"Blend Weight {source.BlendWeight} stiffens the chain toward rigid. It was "
                    + $"folded into the angle limit as stiffness {stiffness:0.###}.");
            }

            if (source.Stiffness.Curve != null && source.Stiffness.Curve.length > 0)
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.stiffnessCurve.dropped",
                    "The stiffness distribution curve was dropped. Stiffness became an angle "
                    + "limit, which does not follow the curve.");
            }

            if (stiffness <= 0f)
            {
                parameters.AngleLimitToggle = false;
                log.Add(DiagnosticSeverity.Mapped, "dynamicbone.stiffness.tooWide",
                    "stiffness 0 caps nothing, so the jiggle rig has no angle limit.");
                return;
            }

            float degrees = 2f * Mathf.Asin(1f - stiffness) * Mathf.Rad2Deg;
            if (degrees >= profile.AngleLimitDegreesAtOne)
            {
                parameters.AngleLimitToggle = false;
                log.Add(DiagnosticSeverity.Approximated, "dynamicbone.stiffness.tooWide",
                    $"stiffness {source.Stiffness.Value} allows {degrees:0} degrees of deviation, "
                    + $"wider than jiggle's angle limit of {profile.AngleLimitDegreesAtOne}. "
                    + "No limit was written.");
                return;
            }

            parameters.AngleLimitToggle = true;
            parameters.AngleLimit = new JiggleCurvedFloatPlan(
                degrees / profile.AngleLimitDegreesAtOne);
            log.Add(DiagnosticSeverity.Approximated, "dynamicbone.stiffness.angleLimit",
                $"stiffness {source.Stiffness.Value} allows {degrees:0} degrees of deviation and "
                + "became a jiggle angle limit of that angle.");
        }

        /// <summary>
        /// Dynamic Bone damps the whole velocity. Jiggle damps motion relative to the parent
        /// with drag and the rest with air drag, so both take the value.
        /// </summary>
        private static void MapDamping(DynamicBoneData source, JiggleParameterPlan parameters,
            List<ConversionDiagnostic> log)
        {
            JiggleCurvedFloatPlan damping = new JiggleCurvedFloatPlan(
                Mathf.Clamp01(source.Damping.Value), source.Damping.Curve);
            parameters.Drag = damping;
            parameters.AirDrag = damping;
            log.Add(DiagnosticSeverity.Mapped, "dynamicbone.damping.drag",
                $"damping {source.Damping.Value} became jiggle drag and air drag, with its curve "
                + "if it had one. Dynamic Bone damps the whole velocity; jiggle splits it in two.");
        }

        private static void MapInert(DynamicBoneData source, JiggleParameterPlan parameters,
            List<ConversionDiagnostic> log)
        {
            parameters.IgnoreRootMotion = Mathf.Clamp01(source.Inert.Value);
            log.Add(DiagnosticSeverity.Mapped, "dynamicbone.inert.ignoreRootMotion",
                $"inert {source.Inert.Value} became ignoreRootMotion. Dynamic Bone measures the "
                + "inherited motion at the component's object, jiggle at the rig's root bone.");

            if (source.Inert.Curve != null && source.Inert.Curve.length > 0)
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.inertCurve.dropped",
                    "The inert distribution curve was dropped. Jiggle's ignoreRootMotion is one "
                    + "value for the rig.");
            }
        }

        private static void MapRadius(
            DynamicBoneData source, JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            float radius = Mathf.Max(0f, source.Radius.Value);

            if (radius <= 0f && source.ColliderFileIds.Count > 0)
            {
                parameters.CollisionRadius = new JiggleCurvedFloatPlan(PointCollisionRadius);
                parameters.CollisionToggle = true;
                log.Add(DiagnosticSeverity.Approximated, "dynamicbone.radius.zero",
                    "radius 0 with colliders listed. Dynamic Bone collides such bones as points; "
                    + $"jiggle needs a radius, so {PointCollisionRadius} was written.");
                return;
            }

            bool collides = radius > 0f;
            parameters.CollisionRadius = new JiggleCurvedFloatPlan(radius, source.Radius.Curve);
            parameters.CollisionToggle = collides;

            if (collides)
            {
                log.Add(DiagnosticSeverity.Mapped, "dynamicbone.radius.collisionRadius",
                    $"radius {radius} became collisionRadius, with its curve if it had one.");
            }
        }

        /// <summary>
        /// Dynamic Bone adds its gravity vector to each bone once per tick with no time step, and
        /// cancels the part along the chain's rest direction, so a chain at rest feels none of
        /// it. Jiggle scales world gravity by a multiplier and applies it always. The two are
        /// not comparable, so a non-zero gravity leaves the preset's value in place.
        /// </summary>
        private static void MapGravity(
            DynamicBoneData source, JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            if (source.Gravity == Vector3.zero)
            {
                // A constant force straight down is gravity without the rest-pose cancelling:
                // added per tick, so F per tick at 60 ticks a second is F·3600 m/s².
                if (ForceIsStraightDown(source))
                {
                    float multiplier = -source.Force.y * ReferenceUpdateRate * ReferenceUpdateRate / 9.81f;
                    parameters.Gravity = new JiggleCurvedFloatPlan(multiplier);
                    log.Add(DiagnosticSeverity.Approximated, "dynamicbone.force.gravity",
                        $"force {source.Force} points straight down and became a jiggle gravity "
                        + $"multiplier of {multiplier:0.###}, from {ReferenceUpdateRate} ticks a "
                        + "second.");
                    return;
                }

                parameters.Gravity = new JiggleCurvedFloatPlan(0f);
                return;
            }

            log.Add(DiagnosticSeverity.Approximated, "dynamicbone.gravity",
                $"gravity {source.Gravity} was not carried over. Dynamic Bone adds it per tick "
                + "without a time step and cancels it at the rest pose; jiggle scales world "
                + "gravity. The preset's gravity was kept.");
        }

        private static bool ForceIsStraightDown(DynamicBoneData source)
        {
            return source.Force.y < 0f
                && Mathf.Approximately(source.Force.x, 0f)
                && Mathf.Approximately(source.Force.z, 0f);
        }

        private static void ReportUnmappable(
            DynamicBoneData source, float rateScale, List<ConversionDiagnostic> log)
        {
            if (!Mathf.Approximately(rateScale, 1f))
            {
                log.Add(DiagnosticSeverity.Approximated, "dynamicbone.updateRate",
                    $"Update Rate {source.UpdateRate} scales elasticity by {rateScale:0.##}. That "
                    + "was folded into stiffness; the rig runs at jiggle's own rate.");
            }

            if (source.Force != Vector3.zero
                && !(source.Gravity == Vector3.zero && ForceIsStraightDown(source)))
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.force.dropped",
                    $"The constant force {source.Force} was dropped. Jiggle has gravity but no "
                    + "arbitrary force.");
            }

            if (source.FreezeAxis != DynamicBoneFreezeAxis.None)
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.freezeAxis.dropped",
                    $"Freeze Axis was {source.FreezeAxis}, which flattens the chain's motion onto "
                    + "a plane. Jiggle has no equivalent, so the bones move freely.");
            }

            if (!Mathf.Approximately(source.Friction.Value, 0f))
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.friction.dropped",
                    $"Friction {source.Friction.Value} was dropped. It slows bones after they "
                    + "touch a collider, which jiggle does not model separately from drag.");
            }

            // With End Length 1 and no offset the end particle sits where jiggle puts its own
            // virtual tip, one bone length past the last bone. Anything else is dropped.
            bool endLengthSet = !Mathf.Approximately(source.EndLength, 0f);
            bool exactTip = endLengthSet && Mathf.Approximately(source.EndLength, 1f);
            if ((endLengthSet && !exactTip) || (!endLengthSet && source.EndOffset != Vector3.zero))
            {
                log.Add(DiagnosticSeverity.Dropped, "dynamicbone.endpoint.dropped",
                    "The chain's end offset or length was dropped. Jiggle places its own tip one "
                    + "bone length past the last bone, which matches End Length 1 only.");
            }
        }
    }
}
