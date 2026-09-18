using System.Collections.Generic;
using Basis.Scripts.BasisSdk;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Writers
{
    public sealed class ResolvedBasisAvatar
    {
        public BasisAvatarPlan Plan;
        public GameObject Root;
        public SkinnedMeshRenderer VisemeMesh;
        public SkinnedMeshRenderer BlinkMesh;
    }

    /// <summary>
    /// Puts a <see cref="BasisAvatar"/> on the avatar root and fills in what the VRChat
    /// descriptor knew.
    /// <para>
    /// Deliberately partial. The animator, human scale, renderer list and mouth position are
    /// left alone, because Basis fills those itself the first time the Basis Avatar inspector is
    /// opened, and it only fills values that are still empty. Writing our own guesses would
    /// stop that and duplicate logic that is not ours.
    /// </para>
    /// </summary>
    public static class BasisAvatarWriter
    {
        public const int VisemeCount = 15;

        public static BasisAvatar Write(
            ResolvedBasisAvatar avatar, string undoName = "Convert avatar descriptor")
        {
            if (avatar?.Root == null)
            {
                throw new System.ArgumentException("An avatar root is required", nameof(avatar));
            }

            BasisAvatar component = avatar.Root.GetComponent<BasisAvatar>();
            if (component == null)
            {
                component = Undo.AddComponent<BasisAvatar>(avatar.Root);
            }
            else
            {
                Undo.RecordObject(component, undoName);
            }

            Undo.SetCurrentGroupName(undoName);

            BasisAvatarPlan plan = avatar.Plan;
            component.AvatarEyePosition = plan.EyePosition;

            if (avatar.VisemeMesh != null)
            {
                component.FaceVisemeMesh = avatar.VisemeMesh;
                component.FaceVisemeMovement =
                    ResolveVisemes(avatar.VisemeMesh, plan.VisemeBlendShapeNames);
            }

            if (avatar.BlinkMesh != null && plan.BlinkBlendShapeIndices.Count > 0)
            {
                component.FaceBlinkMesh = avatar.BlinkMesh;
                component.BlinkViseme = plan.BlinkBlendShapeIndices.ToArray();
            }

            // A source that states no limit leaves the setting as it is, so a value set by
            // hand on the component survives a repeated conversion.
            if (plan.EyeMaxLookAngleDegrees > 0f)
            {
                component.EyeMaxLookAngleEnabled = true;
                component.EyeMaxLookAngle = BasisAvatar.ClampEyeMaxLookAngle(plan.EyeMaxLookAngleDegrees);
            }

            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>
        /// Blendshape names to indices on the mesh that carries them. Basis stores indices, so a
        /// name that is not on the mesh becomes -1, its value for "no viseme".
        /// </summary>
        public static int[] ResolveVisemes(
            SkinnedMeshRenderer mesh, IReadOnlyList<string> names)
        {
            int[] indices = new int[VisemeCount];
            for (int i = 0; i < VisemeCount; i++)
            {
                indices[i] = -1;
            }

            Mesh shared = mesh == null ? null : mesh.sharedMesh;
            if (shared == null || names == null)
            {
                return indices;
            }

            for (int i = 0; i < VisemeCount && i < names.Count; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                int index = shared.GetBlendShapeIndex(name);
                if (index < 0)
                {
                    index = FindIgnoringCase(shared, name);
                }

                indices[i] = index;
            }

            return indices;
        }

        /// <summary>
        /// Avatars are exported by many tools and blendshape casing is not consistent between
        /// them, so an exact miss is retried case-insensitively before giving up.
        /// </summary>
        private static int FindIgnoringCase(Mesh mesh, string name)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (string.Equals(mesh.GetBlendShapeName(i), name,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
