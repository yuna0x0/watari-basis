using System.Collections.Generic;
using GatorDragonGames.JigglePhysics;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Writers
{
    /// <summary>A plan with its transform references resolved, ready to write.</summary>
    public sealed class ResolvedJiggleRig
    {
        public JiggleRigPlan Plan;

        /// <summary>The GameObject the JiggleRig component goes on.</summary>
        public GameObject Host;

        public Transform RootBone;
        public List<Transform> ExcludedTransforms = new List<Transform>();
        public List<ResolvedJiggleCollider> Colliders = new List<ResolvedJiggleCollider>();
    }

    public sealed class ResolvedJiggleCollider
    {
        public JiggleColliderPlan Plan;
        public Transform Transform;
    }

    /// <summary>
    /// Writes a resolved plan onto a GameObject as a <see cref="JiggleRig"/>.
    /// <para>
    /// `JiggleRig.jiggleRigData` is private with no setter, so everything goes through
    /// <see cref="SerializedObject"/>. Applying the properties runs the component's OnValidate,
    /// which handles the serialized-version upgrade and rebuilds the bone cache.
    /// </para>
    /// <para>
    /// Every mutation is registered with <see cref="Undo"/>, so one undo step reverts a whole
    /// conversion.
    /// </para>
    /// </summary>
    public static class JiggleRigWriter
    {
        private const string DataPath = "jiggleRigData.";

        public static JiggleRig Write(ResolvedJiggleRig rig, string undoName = "Convert to Jiggle")
        {
            if (rig?.Host == null)
            {
                throw new System.ArgumentException("A host GameObject is required", nameof(rig));
            }

            if (rig.RootBone == null)
            {
                throw new System.ArgumentException("A root bone is required", nameof(rig));
            }

            JiggleRig component = Undo.AddComponent<JiggleRig>(rig.Host);
            Undo.SetCurrentGroupName(undoName);

            if (!CopyPreset(rig.Plan.Preset, component))
            {
                // hasSerializedData is set below, which stops JiggleRig.OnValidate from filling
                // in defaults, so a missing preset would leave every unplanned parameter at 0.
                component.SetInputParameters(JiggleTreeInputParameters.Default());
            }

            // Structure first, through SerializedObject, because these fields are private and
            // have no setter. Applying runs OnValidate, which needs the root bone in place to
            // build the bone cache.
            SerializedObject serialized = new SerializedObject(component);
            WriteRigData(serialized, rig);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Dispose();

            // Parameters afterwards, through the component's own API rather than
            // SerializedObject. An AnimationCurve assigned through SerializedProperty does not
            // survive ApplyModifiedProperties on this component: it reads back as the unit curve
            // the parameter block was initialised with, which would silently flatten every
            // falloff the source data carried. SetInputParameters writes the block directly, so
            // it has to come after the apply above or the stale snapshot would overwrite it.
            component.SetInputParameters(
                Merge(component.GetInputParameters(), rig.Plan.Parameters));

            // Cache the rest pose only once the root bone and exclusions are in place, otherwise
            // it is sampled against whatever the preset had.
            component.ResampleRestPose();
            EditorUtility.SetDirty(component);

            return component;
        }

        private static bool CopyPreset(JigglePreset preset, JiggleRig target)
        {
            JiggleRig source = JigglePresetLibrary.TryLoad(preset);
            if (source == null)
            {
                return false;
            }

            // Copies the serialized fields wholesale, which is the only way to start from the
            // preset's tuning without restating every parameter here. Everything the source data
            // determines is overwritten immediately afterwards.
            EditorUtility.CopySerialized(source, target);
            return true;
        }

        private static void WriteRigData(SerializedObject serialized, ResolvedJiggleRig rig)
        {
            // JiggleRigData.OnValidate refuses to touch data that never claimed to be serialized,
            // and the version string drives its upgrade path.
            serialized.FindProperty(DataPath + "hasSerializedData").boolValue = true;
            // Presets carry v0.0.0, whose upgrade rescales the collision radius. The values
            // written here are already current, so the version is set outright.
            serialized.FindProperty(DataPath + "serializedVersion").stringValue = "v0.0.2";

            serialized.FindProperty(DataPath + "rootBone").objectReferenceValue = rig.RootBone;
            serialized.FindProperty(DataPath + "excludeRoot").boolValue = rig.Plan.ExcludeRoot;
            serialized.FindProperty(DataPath + "lockFromGrabbing").boolValue =
                rig.Plan.LockFromGrabbing;


            SerializedProperty excluded = serialized.FindProperty(DataPath + "excludedTransforms");
            excluded.arraySize = rig.ExcludedTransforms.Count;
            for (int i = 0; i < rig.ExcludedTransforms.Count; i++)
            {
                excluded.GetArrayElementAtIndex(i).objectReferenceValue = rig.ExcludedTransforms[i];
            }

            WriteColliders(serialized, rig.Colliders);
        }

        private static void WriteColliders(
            SerializedObject serialized, List<ResolvedJiggleCollider> colliders)
        {
            SerializedProperty array = serialized.FindProperty(DataPath + "jiggleColliders");

            int count = Mathf.Min(colliders.Count, JiggleRigData.MaxRuntimeJiggleColliders);
            array.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                ResolvedJiggleCollider resolved = colliders[i];
                SerializedProperty entry = array.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("transform").objectReferenceValue = resolved.Transform;

                SerializedProperty collider = entry.FindPropertyRelative("collider");
                collider.FindPropertyRelative("type").enumValueIndex = (int)resolved.Plan.Shape;
                collider.FindPropertyRelative("radius").floatValue = resolved.Plan.Radius;
                collider.FindPropertyRelative("height").floatValue = resolved.Plan.Height;
                collider.FindPropertyRelative("capsuleAxis").enumValueIndex =
                    (int)resolved.Plan.CapsuleAxis;

                SerializedProperty offset = collider.FindPropertyRelative("localOffset");
                offset.FindPropertyRelative("x").floatValue = resolved.Plan.LocalOffset.x;
                offset.FindPropertyRelative("y").floatValue = resolved.Plan.LocalOffset.y;
                offset.FindPropertyRelative("z").floatValue = resolved.Plan.LocalOffset.z;
            }
        }

        private static JiggleTreeInputParameters Merge(
            JiggleTreeInputParameters parameters, JiggleParameterPlan plan)
        {
            // stretch, collisionRadius, ignoreRootMotion, soften and rootStretch are all ignored
            // by ToJigglePointParameters unless this is on, so writing them without it is a
            // silent no-op.
            parameters.advancedToggle = plan.AdvancedToggle;

            if (plan.CollisionToggle.HasValue)
            {
                parameters.collisionToggle = plan.CollisionToggle.Value;
            }

            if (plan.AngleLimitToggle.HasValue)
            {
                parameters.angleLimitToggle = plan.AngleLimitToggle.Value;
            }

            parameters.stiffness = Merge(parameters.stiffness, plan.Stiffness);
            parameters.angleLimit = Merge(parameters.angleLimit, plan.AngleLimit);
            parameters.stretch = Merge(parameters.stretch, plan.Stretch);
            parameters.drag = Merge(parameters.drag, plan.Drag);
            parameters.airDrag = Merge(parameters.airDrag, plan.AirDrag);
            parameters.gravity = Merge(parameters.gravity, plan.Gravity);
            parameters.collisionRadius = Merge(parameters.collisionRadius, plan.CollisionRadius);

            parameters.soften = plan.Soften ?? parameters.soften;
            parameters.angleLimitSoften = plan.AngleLimitSoften ?? parameters.angleLimitSoften;
            parameters.rootStretch = plan.RootStretch ?? parameters.rootStretch;
            parameters.ignoreRootMotion = plan.IgnoreRootMotion ?? parameters.ignoreRootMotion;

            return parameters;
        }

        /// <summary>
        /// A planned value replaces the preset's; no planned value leaves the preset's tuning
        /// alone.
        /// </summary>
        private static JiggleTreeCurvedFloat Merge(
            JiggleTreeCurvedFloat current, JiggleCurvedFloatPlan? planned)
        {
            if (!planned.HasValue)
            {
                return current;
            }

            JiggleCurvedFloatPlan plan = planned.Value;
            JiggleTreeCurvedFloat result = new JiggleTreeCurvedFloat(plan.Value)
            {
                curveEnabled = plan.CurveEnabled,
            };

            if (plan.CurveEnabled)
            {
                result.curve = plan.Curve;
            }

            return result;
        }
    }
}
