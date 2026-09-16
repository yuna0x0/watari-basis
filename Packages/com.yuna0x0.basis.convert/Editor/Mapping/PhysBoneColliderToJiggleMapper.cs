using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRCPhysBoneCollider into a jiggle collider.
    /// <para>
    /// The shapes line up: both systems offer sphere, capsule and plane. Two things differ.
    /// </para>
    /// <para>
    /// Orientation. VRChat places a collider with an arbitrary rotation quaternion and runs both
    /// a capsule and a plane's normal along the rotated Y (`VRCPhysBoneColliderBase.axis`).
    /// Jiggle orients a capsule along one of the three local axes and a plane along local Y
    /// only, so a rotated capsule is snapped to the nearest axis and a rotated plane keeps its
    /// transform's Y. Both are reported.
    /// </para>
    /// <para>
    /// Capsule height. VRChat measures it end to end, caps included, and treats a capsule no
    /// taller than its diameter as a sphere (`CollisionScene`: half length is
    /// `height / 2 - radius`). Jiggle measures the distance between the two cap centres, so the
    /// height is shortened by a diameter on the way across.
    /// </para>
    /// </summary>
    public static class PhysBoneColliderToJiggleMapper
    {
        /// <summary>Angle past which snapping a capsule to an axis is worth reporting.</summary>
        private const float AxisSnapToleranceDegrees = 5f;

        public static JiggleColliderPlan Map(PhysBoneColliderData source)
        {
            JiggleColliderPlan plan = new JiggleColliderPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                TransformFileId = source.RootTransformFileId,
                Radius = Mathf.Max(0f, source.Radius),
                Height = Mathf.Max(0f, source.Height),
                LocalOffset = source.Position,
            };

            if (source.Radius < 0f)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "collider.radius.negative",
                    $"Collider radius was {source.Radius}. Clamped to 0.");
            }

            switch (source.ShapeType)
            {
                case PhysBoneColliderShape.Sphere:
                    plan.Shape = JiggleColliderShape.Sphere;
                    break;

                case PhysBoneColliderShape.Capsule:
                    if (plan.Height <= plan.Radius * 2f)
                    {
                        plan.Shape = JiggleColliderShape.Sphere;
                        plan.Height = 0f;
                        break;
                    }

                    plan.Shape = JiggleColliderShape.Capsule;
                    plan.Height -= plan.Radius * 2f;
                    plan.CapsuleAxis = NearestAxis(source.Rotation, plan.Diagnostics);
                    break;

                case PhysBoneColliderShape.Plane:
                    plan.Shape = JiggleColliderShape.Plane;
                    ReportPlaneRotation(source.Rotation, plan.Diagnostics);
                    break;
            }

            if (source.InsideBounds)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "collider.insideBounds.dropped",
                    "Inside Bounds was set, which keeps bones within the collider rather than "
                    + "outside it. Jiggle colliders only push bones out.");
            }

            if (source.BonesAsSpheres)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "collider.bonesAsSpheres.dropped",
                    "Bones As Spheres was set and has no jiggle equivalent.");
            }

            if (source.GlobalCollision)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "collider.global.dropped",
                    "Global collision was dropped. Basis exposes only hands, arms and feet to other avatars, so this collider acts on its own avatar only.");
            }

            return plan;
        }

        /// <summary>
        /// VRChat capsules run along their local Y before rotation. Jiggle picks an axis instead
        /// of a rotation, so take the axis the rotated Y lands closest to.
        /// </summary>
        private static JiggleCapsuleAxis NearestAxis(
            Quaternion rotation, System.Collections.Generic.List<ConversionDiagnostic> log)
        {
            Vector3 direction = rotation * Vector3.up;

            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);
            float z = Mathf.Abs(direction.z);

            JiggleCapsuleAxis axis;
            Vector3 snapped;
            if (x >= y && x >= z)
            {
                axis = JiggleCapsuleAxis.X;
                snapped = Vector3.right;
            }
            else if (y >= z)
            {
                axis = JiggleCapsuleAxis.Y;
                snapped = Vector3.up;
            }
            else
            {
                axis = JiggleCapsuleAxis.Z;
                snapped = Vector3.forward;
            }

            float residual = Vector3.Angle(direction, direction.x * snapped.x
                + direction.y * snapped.y + direction.z * snapped.z >= 0f ? snapped : -snapped);

            if (residual > AxisSnapToleranceDegrees)
            {
                log.Add(DiagnosticSeverity.Approximated, "collider.capsuleRotation.snapped",
                    $"The capsule was rotated {residual:0.#} degrees away from the "
                    + $"{axis} axis. Jiggle orients capsules along an axis rather than by "
                    + "rotation, so it was snapped to that axis.");
            }

            return axis;
        }

        /// <summary>
        /// A jiggle plane faces its transform's Y and has no rotation of its own, so a VRChat
        /// plane turned away from that axis cannot be written as it was.
        /// </summary>
        private static void ReportPlaneRotation(
            Quaternion rotation, System.Collections.Generic.List<ConversionDiagnostic> log)
        {
            float turned = Vector3.Angle(rotation * Vector3.up, Vector3.up);
            if (turned > AxisSnapToleranceDegrees)
            {
                log.Add(DiagnosticSeverity.Dropped, "collider.planeRotation.dropped",
                    $"The plane is rotated {turned:0.#} degrees off its transform's Y axis, which a jiggle plane always faces. Turn the transform to fix the facing.");
            }
        }
    }
}
