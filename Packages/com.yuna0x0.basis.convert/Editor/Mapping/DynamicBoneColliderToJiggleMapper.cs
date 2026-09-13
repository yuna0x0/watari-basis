using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a Dynamic Bone collider into a jiggle collider.
    /// <para>
    /// Dynamic Bone picks the shape from its height rather than from a shape field: a capsule no
    /// taller than its diameter is a sphere, anything taller is a capsule along the chosen axis
    /// (`DynamicBoneCollider.Prepare`: half length is `height / 2 - radius`). That height runs
    /// end to end, caps included, while jiggle measures between the two cap centres, so it is
    /// shortened by a diameter on the way across.
    /// </para>
    /// <para>
    /// Plane colliders are their own component type and face the chosen axis. A jiggle plane
    /// faces its transform's Y only, so a plane along X or Z is written facing Y and reported.
    /// </para>
    /// </summary>
    public static class DynamicBoneColliderToJiggleMapper
    {
        public static JiggleColliderPlan Map(DynamicBoneColliderData source)
        {
            JiggleColliderPlan plan = new JiggleColliderPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                TransformFileId = source.OwnerGameObjectFileId,
                Radius = Mathf.Max(0f, source.Radius),
                Height = Mathf.Max(0f, source.Height),
                LocalOffset = source.Center,
                CapsuleAxis = (JiggleCapsuleAxis)source.Direction,
            };

            // Dynamic Bone tapers only when the second radius is set and differs by at least
            // 0.01, and then tests the sphere case against the larger radius.
            bool tapered = !source.IsPlane
                && source.Radius2 > 0f
                && Mathf.Abs(source.Radius - source.Radius2) >= 0.01f;
            float shapeRadius = tapered
                ? Mathf.Max(plan.Radius, Mathf.Max(0f, source.Radius2))
                : plan.Radius;

            if (source.IsPlane)
            {
                plan.Shape = JiggleColliderShape.Plane;
                plan.Height = 0f;

                if (source.Direction != DynamicBoneColliderDirection.Y)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "collider.planeAxis.dropped",
                        $"The plane faced its transform's {source.Direction} axis. A jiggle plane "
                        + "always faces the Y axis, so it was written facing Y. Turn the "
                        + "transform, or parent the collider to one that faces the right way.");
                }
            }
            else if (plan.Height > shapeRadius * 2f)
            {
                plan.Shape = JiggleColliderShape.Capsule;
                plan.Height -= plan.Radius * 2f;
            }
            else
            {
                plan.Shape = JiggleColliderShape.Sphere;
                plan.Height = 0f;
            }

            if (source.Bound == DynamicBoneColliderBound.Inside)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "collider.insideBounds.dropped",
                    "This collider kept bones inside it rather than outside. Jiggle colliders "
                    + "only push bones out.");
            }

            if (tapered)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "collider.taper.dropped",
                    $"This capsule tapered from {source.Radius} to {source.Radius2}. Jiggle "
                    + $"capsules have one radius, so {plan.Radius} was used for the whole length.");
            }

            return plan;
        }
    }
}
