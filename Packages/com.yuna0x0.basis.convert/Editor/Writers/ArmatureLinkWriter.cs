using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Writers
{
    /// <summary>A bone link with both ends resolved on the target hierarchy.</summary>
    public sealed class ResolvedBoneLink
    {
        public Transform Prop;
        public Transform Avatar;
    }

    public sealed class ResolvedArmatureLink
    {
        public List<ResolvedBoneLink> Bones = new List<ResolvedBoneLink>();

        /// <summary>Prefab instances that had to be unpacked for a bone to leave them, by name.</summary>
        public List<string> Unpacked = new List<string>();
        public bool AlignPosition;
        public bool AlignRotation;
        public bool AlignScale;
        public float ScaleFactor = 1f;
        public string RootName = string.Empty;
    }

    /// <summary>
    /// Parents clothing bones under the avatar's, aligned as the link asks, in one undo group.
    /// <para>
    /// Unity does not let a GameObject leave the prefab instance it belongs to, which is why
    /// VRCFury and Modular Avatar do this on a build clone. There is no clone here, so the
    /// instance a bone belongs to is unpacked first, outermost root only, as many times as it
    /// takes; the avatar's own instance is untouched, since adding children to an instance is
    /// allowed. The unpack is recorded in the same undo group and named in the result.
    /// </para>
    /// <para>
    /// This is the child-merge form of what VRCFury builds: each matched bone goes under its
    /// avatar bone and keeps its own object, so the clothing's skinned meshes keep their bone
    /// references. VRCFury also rewrites skins to reuse the avatar's bones and prunes the moved
    /// ones; that changes bone count, not where the clothing sits, and is not done here. A bone
    /// already under its avatar bone is left as it is, so converting twice moves nothing.
    /// </para>
    /// </summary>
    public static class ArmatureLinkWriter
    {
        public static int Write(ResolvedArmatureLink link, string undoName)
        {
            int moved = 0;
            foreach (ResolvedBoneLink bone in link.Bones)
            {
                if (bone.Prop == null || bone.Avatar == null || bone.Prop == bone.Avatar
                    || bone.Avatar.IsChildOf(bone.Prop))
                {
                    continue;
                }

                if (bone.Prop.parent == bone.Avatar)
                {
                    continue;
                }

                Release(bone.Prop.gameObject, link, undoName);

                Undo.RecordObject(bone.Prop, undoName);
                if (link.AlignPosition)
                {
                    bone.Prop.position = bone.Avatar.position;
                }

                if (link.AlignRotation)
                {
                    bone.Prop.rotation = bone.Avatar.rotation;
                }

                Undo.SetTransformParent(bone.Prop, bone.Avatar, undoName);

                if (link.AlignScale)
                {
                    bone.Prop.localScale = Vector3.one * link.ScaleFactor;
                }

                Undo.RecordObject(bone.Prop.gameObject, undoName);
                bone.Prop.gameObject.name = $"[VF] {bone.Prop.name} from {link.RootName}";
                moved++;
            }

            return moved;
        }

        /// <summary>Unpacks whatever prefab instances still hold the object, outermost first.</summary>
        private static void Release(GameObject target, ResolvedArmatureLink link, string undoName)
        {
            for (int guard = 0; guard < 16 && PrefabUtility.IsPartOfPrefabInstance(target); guard++)
            {
                GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                if (outermost == null)
                {
                    return;
                }

                link.Unpacked.Add(outermost.name);
                PrefabUtility.UnpackPrefabInstance(
                    outermost, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            }
        }
    }
}
