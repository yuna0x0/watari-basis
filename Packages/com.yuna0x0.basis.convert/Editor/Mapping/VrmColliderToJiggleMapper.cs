using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRM collider into a jiggle one.
    /// <para>
    /// VRM 0.x has spheres only. VRM 1.0 adds capsules and planes, which jiggle also has, and
    /// two inside variants that keep bones within the shape rather than outside it. Jiggle has
    /// nothing that does that, so those are reported and written as their outside equivalent,
    /// which is the closest shape in the wrong direction.
    /// </para>
    /// <para>
    /// A VRM capsule is its two cap centres, which is also how jiggle measures height, so the
    /// length carries over as it is. A VRM plane states its normal; a jiggle plane faces its
    /// transform's Y, so a normal pointing elsewhere is reported.
    /// </para>
    /// </summary>
    public static class VrmColliderToJiggleMapper
    {
        /// <summary>Angle past which a plane normal off the Y axis is worth reporting.</summary>
        private const float PlaneNormalToleranceDegrees = 5f;

        public static JiggleColliderPlan Map(VrmColliderData source)
        {
            JiggleColliderPlan plan = new JiggleColliderPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                TransformFileId = source.OwnerGameObjectFileId,
                Radius = Mathf.Max(0f, source.Radius),
                LocalOffset = source.Offset,
            };

            switch (source.Type)
            {
                case VrmColliderType.Capsule:
                case VrmColliderType.CapsuleInside:
                {
                    // VRM writes a capsule as its two ends; jiggle writes one as a height along
                    // an axis from the offset, so the tail becomes the length and the direction.
                    Vector3 axis = source.Tail - source.Offset;
                    plan.Shape = JiggleColliderShape.Capsule;
                    plan.Height = axis.magnitude;
                    plan.CapsuleAxis = LongestAxis(axis);
                    plan.LocalOffset = source.Offset + (axis * 0.5f);

                    if (plan.Height > 0f && !IsAxisAligned(axis))
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Approximated,
                            "vrm.collider.capsuleSnapped",
                            $"A capsule collider ran along {axis}, which is not one of the three "
                            + "axes a jiggle capsule can lie on. It was snapped to the nearest.");
                    }

                    break;
                }

                case VrmColliderType.Plane:
                {
                    plan.Shape = JiggleColliderShape.Plane;
                    plan.Radius = 0f;

                    float turned = Vector3.Angle(source.Normal, Vector3.up);
                    if (source.Normal != Vector3.zero && turned > PlaneNormalToleranceDegrees)
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Dropped,
                            "vrm.collider.planeNormal",
                            $"The plane's normal points {turned:0.#} degrees off its transform's Y axis, which a jiggle plane always faces. Turn the transform to fix the facing.");
                    }

                    break;
                }

                default:
                    plan.Shape = JiggleColliderShape.Sphere;
                    break;
            }

            if (source.Type == VrmColliderType.SphereInside
                || source.Type == VrmColliderType.CapsuleInside)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.collider.inside",
                    "An inside collider, which holds bones within its shape, was written as an ordinary one; jiggle only pushes out. Remove it if the result looks wrong.");
            }

            return plan;
        }

        private static bool IsAxisAligned(Vector3 axis)
        {
            int nonZero = 0;
            if (!Mathf.Approximately(axis.x, 0f)) nonZero++;
            if (!Mathf.Approximately(axis.y, 0f)) nonZero++;
            if (!Mathf.Approximately(axis.z, 0f)) nonZero++;
            return nonZero <= 1;
        }

        private static JiggleCapsuleAxis LongestAxis(Vector3 axis)
        {
            float x = Mathf.Abs(axis.x);
            float y = Mathf.Abs(axis.y);
            float z = Mathf.Abs(axis.z);

            if (x >= y && x >= z)
            {
                return JiggleCapsuleAxis.X;
            }

            return y >= z ? JiggleCapsuleAxis.Y : JiggleCapsuleAxis.Z;
        }
    }
}
