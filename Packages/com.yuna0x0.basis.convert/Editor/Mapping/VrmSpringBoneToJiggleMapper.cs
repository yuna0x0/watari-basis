using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRM spring chain into a jiggle rig.
    /// <para>
    /// VRM and jiggle describe a hanging chain the same way, so most of this is direct. The one
    /// judgement call is stiffness: VRM's is a force with no upper bound, jiggle's runs from 0
    /// to 1, so anything above 1 is as stiff as jiggle goes.
    /// </para>
    /// <para>
    /// VRM 1.0 carries parameters per joint. Jiggle evaluates its parameters over normalized
    /// distance from the chain root, which is the same axis, so a chain whose joints differ
    /// becomes a curve rather than an average.
    /// </para>
    /// </summary>
    public static class VrmSpringBoneToJiggleMapper
    {
        public static JiggleRigPlan Map(VrmSpringChainData source)
        {
            JiggleRigPlan plan = new JiggleRigPlan();
            List<ConversionDiagnostic> log = plan.Diagnostics;

            if (source == null || source.Joints.Count == 0)
            {
                log.Add(DiagnosticSeverity.Warning, "vrm.noJoints",
                    "A VRM spring chain named no joints, so there was nothing to convert.");
                return plan;
            }

            List<VrmSpringJointData> joints = WithParameters(source.Joints);
            JiggleParameterPlan parameters = plan.Parameters;

            // A VRM 1.0 chain's last joint is its tail. UniVRM never reads its parameters
            // (FastSpringBoneBuffer takes joints.Length - 1), though the importer writes defaults
            // onto it, so it is dropped here whatever it carries.
            if (source.IsVrm10 && joints.Count > 1 && joints[joints.Count - 1] == source.Joints[source.Joints.Count - 1])
            {
                joints.RemoveAt(joints.Count - 1);
            }

            if (joints.Count == 0)
            {
                joints.Add(source.Joints[0]);
            }

            // Nothing below reads these, and the preset's values would otherwise stand in.
            parameters.Stretch = new JiggleCurvedFloatPlan(0f);
            parameters.Soften = 0f;
            parameters.RootStretch = 0f;
            parameters.AirDrag = new JiggleCurvedFloatPlan(0f);

            float[] stiffness = new float[joints.Count];
            float[] drag = new float[joints.Count];
            float[] radius = new float[joints.Count];
            float[] gravity = new float[joints.Count];
            bool clamped = false;
            bool sideways = false;

            for (int i = 0; i < joints.Count; i++)
            {
                VrmSpringJointData joint = joints[i];

                clamped |= joint.Stiffness > 1f;
                stiffness[i] = Mathf.Clamp01(joint.Stiffness);
                drag[i] = Mathf.Clamp01(joint.DragForce);
                radius[i] = Mathf.Max(0f, joint.Radius);

                // VRM's gravity is a direction and a magnitude; jiggle's is a multiplier on
                // world gravity, so only the vertical part has anywhere to go. Upward stays
                // upward: jiggle's multiplier may be negative.
                Vector3 pull = joint.GravityDir.normalized * joint.GravityPower;
                gravity[i] = -pull.y;
                sideways |= joint.GravityPower > 0f
                    && !Mathf.Approximately(pull.magnitude, Mathf.Abs(pull.y));
            }

            parameters.Stiffness = Curved(stiffness);
            log.Add(DiagnosticSeverity.Approximated, "vrm.stiffness",
                $"stiffness force {joints[0].Stiffness} became jiggle stiffness "
                + $"{stiffness[0]}. VRM measures it as a force with no upper bound and jiggle "
                + "runs from 0 to 1, so this is a fit rather than a conversion.");

            if (clamped)
            {
                log.Add(DiagnosticSeverity.Approximated, "vrm.stiffness.clamped",
                    "A joint's stiffness force was above 1, which is stiffer than jiggle can "
                    + "express. It was written as fully stiff.");
            }

            parameters.Drag = Curved(drag);
            log.Add(DiagnosticSeverity.Mapped, "vrm.drag",
                $"drag force {joints[0].DragForce} became jiggle drag. Both are damping on the "
                + "same 0 to 1 scale.");

            parameters.Gravity = Curved(gravity);
            if (Mathf.Abs(gravity[0]) > 0f || sideways)
            {
                log.Add(DiagnosticSeverity.Approximated, "vrm.gravity",
                    $"gravity power {joints[0].GravityPower} became the jiggle gravity "
                    + "multiplier. VRM adds it per step as a force against stiffness; jiggle "
                    + "scales world gravity. The units differ, so this is a fit.");
            }

            if (sideways)
            {
                log.Add(DiagnosticSeverity.Approximated, "vrm.gravity.direction",
                    "Gravity did not point straight down. Jiggle scales world gravity rather "
                    + "than taking a direction, so only the vertical part carried across.");
            }

            MapAngleLimit(joints, parameters, log);

            bool collides = radius[0] > 0f;
            parameters.CollisionRadius = Curved(radius);
            parameters.CollisionToggle = collides;

            if (collides)
            {
                log.Add(DiagnosticSeverity.Mapped, "vrm.radius",
                    $"joint radius {joints[0].Radius} became jiggle collisionRadius. Both are "
                    + "metres.");
            }

            // VRM simulates a chain relative to its centre transform, so the avatar's own
            // motion never reaches it. Jiggle's ignoreRootMotion does the same from the rig's
            // root bone.
            if (source.CenterFileId != 0L)
            {
                parameters.IgnoreRootMotion = 1f;
                log.Add(DiagnosticSeverity.Approximated, "vrm.center",
                    "The chain named a centre transform, so it ignored the avatar's motion. "
                    + "The rig ignores root motion fully, measured at its root bone rather than "
                    + "the centre.");
            }
            else
            {
                parameters.IgnoreRootMotion = 0f;
            }

            return plan;
        }

        /// <summary>
        /// A VRM 1.0 cone limit is a half-angle in radians; jiggle's angle limit is 0..1 of 90
        /// degrees. Hinge and spherical limits have no jiggle shape.
        /// </summary>
        private static void MapAngleLimit(List<VrmSpringJointData> joints,
            JiggleParameterPlan parameters, List<ConversionDiagnostic> log)
        {
            const int cone = 1;
            bool anyCone = false;
            bool anyOther = false;
            bool anyUnlimited = false;
            float[] limits = new float[joints.Count];

            for (int i = 0; i < joints.Count; i++)
            {
                VrmSpringJointData joint = joints[i];
                if (joint.AngleLimitType == cone)
                {
                    anyCone = true;
                    limits[i] = Mathf.Clamp01(joint.Pitch / (Mathf.PI * 0.5f));
                }
                else
                {
                    anyOther |= joint.AngleLimitType != 0;
                    anyUnlimited |= joint.AngleLimitType == 0;
                    limits[i] = 1f;
                }
            }

            if (anyOther)
            {
                log.Add(DiagnosticSeverity.Dropped, "vrm.angleLimit.dropped",
                    "A joint had a hinge or spherical angle limit. Jiggle has a cone only, so "
                    + "that limit was dropped.");
            }

            if (!anyCone)
            {
                parameters.AngleLimitToggle = false;
                return;
            }

            parameters.AngleLimitToggle = true;
            parameters.AngleLimit = Curved(limits);
            log.Add(DiagnosticSeverity.Approximated, "vrm.angleLimit.cone",
                $"A cone limit of {joints[0].Pitch * Mathf.Rad2Deg:0} degrees became a jiggle "
                + "angle limit. Cones wider than 90 degrees stop at 90"
                + (anyUnlimited ? ", and joints without a limit take 90 too." : "."));
        }

        /// <summary>
        /// A parameter and its falloff. Jiggle evaluates <c>value * curve(t)</c> over normalized
        /// distance from the root, so a chain whose joints agree needs no curve at all, and one
        /// whose joints differ becomes the ratios between them.
        /// </summary>
        private static JiggleCurvedFloatPlan Curved(float[] values)
        {
            float first = values[0];
            bool varies = false;

            for (int i = 1; i < values.Length; i++)
            {
                if (!Mathf.Approximately(values[i], first))
                {
                    varies = true;
                    break;
                }
            }

            if (!varies || values.Length < 2)
            {
                return new JiggleCurvedFloatPlan(first);
            }

            // The curve is a ratio, so it needs something to be a ratio of. A chain starting at
            // zero is described by its largest value instead.
            float scale = first;
            if (Mathf.Approximately(scale, 0f))
            {
                foreach (float value in values)
                {
                    scale = Mathf.Max(scale, value);
                }
            }

            if (Mathf.Approximately(scale, 0f))
            {
                return new JiggleCurvedFloatPlan(0f);
            }

            Keyframe[] keys = new Keyframe[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                keys[i] = new Keyframe(i / (float)(values.Length - 1), values[i] / scale);
            }

            return new JiggleCurvedFloatPlan(scale, new AnimationCurve(keys));
        }

        private static List<VrmSpringJointData> WithParameters(List<VrmSpringJointData> joints)
        {
            List<VrmSpringJointData> kept = new List<VrmSpringJointData>();
            foreach (VrmSpringJointData joint in joints)
            {
                if (joint.HasParameters)
                {
                    kept.Add(joint);
                }
            }

            return kept;
        }
    }
}
