using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Rig
{
    /// <summary>
    /// Checks whether an avatar's rig meets what Basis's full-body IK needs.
    /// <para>
    /// Nothing here is a conversion: full-body tracking is client side, and a VRChat avatar
    /// carries no data about it. What the avatar does carry is its humanoid rig, and Basis makes
    /// specific demands of that which an imported avatar often does not meet. Those live in the
    /// model importer rather than in components, so they are checked separately from everything
    /// else and reported the same way.
    /// </para>
    /// </summary>
    public static class RigReadiness
    {
        /// <summary>
        /// Bones the Basis avatar validator reports as errors when unmapped. Unity accepts a
        /// humanoid without Chest or Neck; Basis does not.
        /// </summary>
        private static readonly HumanBodyBones[] Required =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        /// <summary>
        /// Bones the Basis validator warns about when unmapped. Eyes are checked separately.
        /// </summary>
        private static readonly HumanBodyBones[] Recommended =
        {
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
        };

        /// <summary>The four arm bones Basis looks under for a twist child.</summary>
        private static readonly HumanBodyBones[] TwistParents =
        {
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
        };

        /// <summary>
        /// <paramref name="expectAvatar"/> says whether this is meant to be an avatar. Props and
        /// clothing pieces also carry physics components and have no humanoid rig, so demanding
        /// one of them would be noise rather than a finding.
        /// </summary>
        public static List<ConversionDiagnostic> Inspect(GameObject avatarRoot, bool expectAvatar)
        {
            List<ConversionDiagnostic> log = new List<ConversionDiagnostic>();
            if (avatarRoot == null)
            {
                return log;
            }

            Animator animator = avatarRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null)
            {
                if (expectAvatar)
                {
                    log.Add(DiagnosticSeverity.Warning, "rig.noAnimator",
                        "No Animator with an avatar was found. Basis drives avatars through a "
                        + "humanoid rig, so set the model's Animation Type to Humanoid and "
                        + "configure its bone mapping.");
                }

                return log;
            }

            if (!animator.avatar.isValid || !animator.isHuman)
            {
                log.Add(DiagnosticSeverity.Warning, "rig.notHumanoid",
                    "The rig is not a valid humanoid. Basis requires Animation Type Humanoid, so "
                    + "set it on the model and check the bone mapping under Configure.");
                return log;
            }

            InspectRequiredBones(animator, log);
            InspectJaw(animator, log);
            InspectEyes(animator, log);
            InspectTwistBones(animator, log);

            return log;
        }

        private static void InspectRequiredBones(
            Animator animator, List<ConversionDiagnostic> log)
        {
            List<string> missing = new List<string>();
            foreach (HumanBodyBones bone in Required)
            {
                if (animator.GetBoneTransform(bone) == null)
                {
                    missing.Add(bone.ToString());
                }
            }

            if (missing.Count > 0)
            {
                log.Add(DiagnosticSeverity.Warning, "rig.missingBones",
                    $"The humanoid mapping is missing {string.Join(", ", missing)}. The Basis "
                    + "avatar validator reports these as errors.");
            }
            else
            {
                log.Add(DiagnosticSeverity.Mapped, "rig.bonesComplete",
                    "Every humanoid bone the Basis validator requires is mapped.");
            }

            List<string> recommended = new List<string>();
            foreach (HumanBodyBones bone in Recommended)
            {
                if (animator.GetBoneTransform(bone) == null)
                {
                    recommended.Add(bone.ToString());
                }
            }

            if (recommended.Count > 0)
            {
                log.Add(DiagnosticSeverity.Warning, "rig.recommendedBones",
                    $"The humanoid mapping is missing {string.Join(", ", recommended)}. Basis "
                    + "places them from a fallback table and its validator warns.");
            }
        }

        private static void InspectJaw(Animator animator, List<ConversionDiagnostic> log)
        {
            if (animator.GetBoneTransform(HumanBodyBones.Jaw) == null)
            {
                return;
            }

            log.Add(DiagnosticSeverity.Warning, "rig.jawMapped",
                "A Jaw bone is mapped. Basis does not drive it and retargeting moves whatever is mapped there. Clear it in the model's Configure, under Head, unless it is a jaw.");
        }

        private static void InspectEyes(Animator animator, List<ConversionDiagnostic> log)
        {
            bool left = animator.GetBoneTransform(HumanBodyBones.LeftEye) != null;
            bool right = animator.GetBoneTransform(HumanBodyBones.RightEye) != null;

            if (left && right)
            {
                log.Add(DiagnosticSeverity.Mapped, "rig.eyesMapped",
                    "Both eye bones are mapped, so Basis can calibrate gaze from them.");
                return;
            }

            log.Add(DiagnosticSeverity.Warning, "rig.eyesMissing",
                left || right
                    ? "Only one eye bone is mapped. Basis calibrates gaze from both, so map the "
                        + "other or clear both."
                    : "No eye bones are mapped. Basis moves the eyes with bones and calibrates "
                        + "them at load, so gaze will not work without them.");
        }

        /// <summary>
        /// Basis finds a twist bone by taking the first direct child of an arm bone whose name
        /// contains "twist" or "roll", case-insensitively. Verified against
        /// <c>BasisTransformMapping.FindTwistBone</c> in com.basis.common; note the constraints
        /// documentation attributes this to a type that does not exist in the shipped source.
        /// </summary>
        private static void InspectTwistBones(Animator animator, List<ConversionDiagnostic> log)
        {
            List<string> without = new List<string>();
            int found = 0;

            foreach (HumanBodyBones bone in TwistParents)
            {
                Transform parent = animator.GetBoneTransform(bone);
                if (parent == null)
                {
                    continue;
                }

                if (FindTwistChild(parent) != null)
                {
                    found++;
                }
                else
                {
                    without.Add(bone.ToString());
                }
            }

            if (found > 0)
            {
                log.Add(DiagnosticSeverity.Mapped, "rig.twistBones",
                    $"{found} of the four arm bones have a twist child Basis will pick up.");
            }

            if (without.Count > 0)
            {
                log.Add(DiagnosticSeverity.Mapped, "rig.twistBonesAbsent",
                    $"No twist child under {string.Join(", ", without)}. Basis looks for a direct "
                    + "child whose name contains \"twist\" or \"roll\", and applies no twist where "
                    + "there is none.");
            }
        }

        /// <summary>Mirrors Basis's own lookup so the report matches what it will actually find.</summary>
        public static Transform FindTwistChild(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string name = child.name;
                if (name.IndexOf("twist", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("roll", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// The model importer behind an avatar, when there is one. The Jaw mapping lives there
        /// rather than on the instance, so clearing it means editing the import settings.
        /// </summary>
        public static ModelImporter TryGetModelImporter(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            Animator animator = avatarRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(animator.avatar);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetImporter.GetAtPath(path) as ModelImporter;
        }

        /// <summary>
        /// Removes the Jaw entry from a model's humanoid mapping and reimports.
        /// <para>
        /// This edits the model's import settings, so it affects every avatar using that model,
        /// and it is not covered by scene undo. Callers confirm first.
        /// </para>
        /// </summary>
        public static bool ClearJawMapping(ModelImporter importer)
        {
            if (importer == null)
            {
                return false;
            }

            HumanDescription description = importer.humanDescription;
            HumanBone[] bones = description.human;
            if (bones == null)
            {
                return false;
            }

            List<HumanBone> kept = new List<HumanBone>(bones.Length);
            bool removed = false;

            foreach (HumanBone bone in bones)
            {
                if (bone.humanName == HumanBodyBones.Jaw.ToString())
                {
                    removed = true;
                    continue;
                }

                kept.Add(bone);
            }

            if (!removed)
            {
                return false;
            }

            description.human = kept.ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
            return true;
        }
    }
}
