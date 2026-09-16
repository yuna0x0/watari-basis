using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>One clothing bone and the avatar bone it goes under.</summary>
    public sealed class PlannedBoneLink
    {
        /// <summary>In the clothing prefab's asset.</summary>
        public Transform SourceProp;

        /// <summary>In the avatar prefab's asset.</summary>
        public Transform SourceAvatar;

        public bool IsRoot;
    }

    /// <summary>
    /// A VRCFury Armature Link as it will be applied: which clothing bones go under which avatar
    /// bones, and how they are aligned once there.
    /// </summary>
    public sealed class PlannedArmatureLink
    {
        public bool Include = true;

        /// <summary>The clothing prefab.</summary>
        public ConversionSource Source;

        /// <summary>The prefab holding the avatar's bones; the same as Source when the clothing is inside it.</summary>
        public ConversionSource AvatarSource;

        public string PropName = string.Empty;
        public string TargetName = string.Empty;

        /// <summary>Deepest first, the order VRCFury applies them in.</summary>
        public List<PlannedBoneLink> Bones = new List<PlannedBoneLink>();

        public bool AlignPosition;
        public bool AlignRotation;
        public bool AlignScale;
        public float ScaleFactor = 1f;

        /// <summary>The clothing's own name, for the moved bones' new names.</summary>
        public string RootName = string.Empty;

        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();

        public string Describe() => $"{PropName} to {TargetName}, {Bones.Count} bones";
    }

    /// <summary>
    /// Works out what a VRCFury Armature Link does, from its data and the two prefabs' bones.
    /// <para>
    /// This is VRCFury's own matching: the target bone comes from the link, by humanoid bone
    /// (falling back along the humanoid parent chain), by object, or by a path; the clothing's
    /// bones are then matched by name to the avatar's, child by child, with the suffix the link
    /// names or the one implied by the root's name removed, and VRCFury's four known mid-bone
    /// fixups. Bones under a PhysBone stay with their chain, as VRCFury leaves them, unless the
    /// avatar bone is a humanoid one. Bones with no match stay under their clothing parent and
    /// move with it.
    /// </para>
    /// </summary>
    public static class ArmatureLinkPlanner
    {
        private static readonly string[] MidBoneFixups = { "ChestUp", "TopFut_L", "TopFut_R", "HeadGRP" };

        public static void Plan(AvatarConversionPlan plan, List<VrcFuryArmatureLinkSource> links)
        {
            if (links.Count == 0 || plan.SourceRoot == null)
            {
                return;
            }

            ConversionSource avatarSource = plan.Sources.Count > 0 ? plan.Sources[0] : null;
            Transform avatarRoot = plan.SourceRoot.transform;
            Animator animator = plan.SourceRoot.GetComponent<Animator>();
            HashSet<Transform> humanoidBones = HumanoidBones(animator);

            foreach (VrcFuryArmatureLinkSource link in links)
            {
                PlannedArmatureLink planned = PlanOne(plan, link, avatarSource, avatarRoot, animator, humanoidBones);
                if (planned != null)
                {
                    plan.ArmatureLinks.Add(planned);
                }
            }
        }

        private static PlannedArmatureLink PlanOne(
            AvatarConversionPlan plan, VrcFuryArmatureLinkSource link, ConversionSource avatarSource,
            Transform avatarRoot, Animator animator, HashSet<Transform> humanoidBones)
        {
            VrcFuryArmatureLinkData data = link.Data;
            Transform clothingRoot = link.Source.Root.transform;

            if (data.PropBoneFileId == 0L
                || !link.Resolver.TryResolveTransform(data.PropBoneFileId, out Transform propBone)
                || propBone == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrcfury.armatureLink.unresolved",
                    $"An Armature Link in {link.Source.Name} names no bone to link, so nothing was moved.");
                return null;
            }

            PlannedArmatureLink planned = new PlannedArmatureLink
            {
                Source = link.Source,
                AvatarSource = avatarSource,
                PropName = propBone.name,
                RootName = RootNameOf(propBone, clothingRoot),
                AlignPosition = data.AlignPosition,
                AlignRotation = data.AlignRotation,
                AlignScale = data.AlignScale,
            };

            Transform avatarBone = ResolveTarget(data, link, avatarRoot, animator, out string why);
            if (avatarBone == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrcfury.armatureLink.unresolved",
                    $"The Armature Link on {planned.PropName} in {link.Source.Name} could not find "
                    + $"its target: {why} Convert the avatar with the clothing on it.");
                return null;
            }

            planned.TargetName = avatarBone.name;

            bool recursive = data.Recursive;
            if (!data.RecursiveKnown)
            {
                // The older Auto mode: merge the whole hierarchy when the clothing's meshes are
                // bound to bones outside it, which is what VRCFury checks at build.
                recursive = HasExternalSkinBoneReference(propBone, clothingRoot);
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrcfury.armatureLink.auto",
                    $"The Armature Link on {planned.PropName} in {link.Source.Name} was written in "
                    + "VRCFury's old Auto mode. The clothing's meshes decided: "
                    + (recursive ? "the whole bone tree is merged." : "only the root bone is moved."));
            }

            planned.ScaleFactor = ScaleFactorOf(data, recursive, propBone, avatarBone);

            string suffix = data.RemoveBoneSuffix;
            if (string.IsNullOrWhiteSpace(suffix)
                && propBone.name.Contains(avatarBone.name) && propBone.name != avatarBone.name)
            {
                suffix = propBone.name.Replace(avatarBone.name, string.Empty);
            }

            HashSet<Transform> underPhysBone = BonesUnderPhysBones(plan, link.Source, propBone);
            List<PlannedBoneLink> pairs = new List<PlannedBoneLink>
            {
                new PlannedBoneLink { SourceProp = propBone, SourceAvatar = avatarBone, IsRoot = true },
            };

            int keptWithChain = 0;
            if (recursive)
            {
                Stack<(Transform prop, Transform avatar)> check = new Stack<(Transform, Transform)>();
                check.Push((propBone, avatarBone));
                while (check.Count > 0)
                {
                    (Transform checkProp, Transform checkAvatar) = check.Pop();
                    foreach (Transform childProp in checkProp)
                    {
                        string searchName = childProp.name;
                        if (!string.IsNullOrWhiteSpace(suffix))
                        {
                            searchName = searchName.Replace(suffix, string.Empty);
                        }

                        Transform childAvatar = checkAvatar.Find(searchName);
                        bool recurseOnly = false;
                        foreach (string fixup in MidBoneFixups)
                        {
                            if (childAvatar != null)
                            {
                                break;
                            }

                            if (childProp.name == fixup)
                            {
                                childAvatar = checkAvatar;
                                recurseOnly = true;
                                break;
                            }

                            childAvatar = checkAvatar.Find(fixup + "/" + searchName);
                            if (childAvatar != null)
                            {
                                break;
                            }

                            if (checkAvatar.name == fixup && checkAvatar.parent != null)
                            {
                                childAvatar = checkAvatar.parent.Find(searchName);
                                if (childAvatar != null)
                                {
                                    break;
                                }
                            }
                        }

                        if (childAvatar == null)
                        {
                            continue;
                        }

                        if (!recurseOnly)
                        {
                            if (underPhysBone.Contains(childProp) && !humanoidBones.Contains(childAvatar))
                            {
                                keptWithChain++;
                            }
                            else
                            {
                                pairs.Add(new PlannedBoneLink { SourceProp = childProp, SourceAvatar = childAvatar });
                            }
                        }

                        check.Push((childProp, childAvatar));
                    }
                }
            }

            // Deepest first, as VRCFury applies them.
            pairs.Reverse();
            planned.Bones = pairs;

            if (keptWithChain > 0)
            {
                planned.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Approximated,
                    "vrcfury.armatureLink.physBone",
                    $"{keptWithChain} bones of {planned.PropName} sit inside a PhysBone chain and stay "
                    + "with it, as VRCFury leaves them; they follow the chain's root instead of their "
                    + "own avatar bone."));
            }

            int attached = 0;
            foreach (PlannedBoneLink pair in pairs)
            {
                if (pair.SourceProp.GetComponentInChildren<Renderer>(true) != null)
                {
                    attached++;
                }
            }

            if (attached > 0)
            {
                planned.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Approximated,
                    "vrcfury.armatureLink.attached",
                    $"{attached} linked bones of {planned.PropName} carry renderers beneath them. "
                    + "Those follow the avatar bone now and are no longer under the clothing's "
                    + "own object, so a toggle on that object no longer switches them."));
            }

            if (data.RemoveParentConstraints)
            {
                int removed = 0;
                foreach (PlannedConstraint constraint in plan.Constraints)
                {
                    if (constraint.Source != link.Source || constraint.SourceHost == null)
                    {
                        continue;
                    }

                    foreach (PlannedBoneLink pair in pairs)
                    {
                        if (constraint.SourceHost == pair.SourceProp && constraint.Include)
                        {
                            constraint.Include = false;
                            removed++;
                        }
                    }
                }

                if (removed > 0)
                {
                    planned.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Mapped,
                        "vrcfury.armatureLink.constraintRemoved",
                        $"{removed} constraints on the linked bones of {planned.PropName} were left "
                        + "out, as VRCFury removes them: the link replaces them."));
                }
            }

            return planned;
        }

        private static Transform ResolveTarget(
            VrcFuryArmatureLinkData data, VrcFuryArmatureLinkSource link, Transform avatarRoot,
            Animator animator, out string why)
        {
            why = "the link names no target.";
            foreach (VrcFuryLinkTarget target in data.LinkTo)
            {
                Transform at;
                if (target.UseBone)
                {
                    at = HumanoidBone(animator, target.Bone, out why);
                }
                else if (target.UseObject)
                {
                    if (!link.Resolver.TryResolveTransform(target.ObjectFileId, out at) || at == null)
                    {
                        why = "the object it points at is not in the prefab.";
                        continue;
                    }
                }
                else
                {
                    at = avatarRoot;
                }

                if (at == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(target.Offset))
                {
                    Transform offset = FindRelative(at, target.Offset);
                    if (offset == null)
                    {
                        why = $"nothing at '{target.Offset}' under {at.name}.";
                        continue;
                    }

                    at = offset;
                }

                return at;
            }

            return null;
        }

        /// <summary>
        /// The avatar's bone for a humanoid slot, or the nearest one up the humanoid chain the
        /// avatar does have, the way VRCFury falls back.
        /// </summary>
        private static Transform HumanoidBone(Animator animator, int bone, out string why)
        {
            why = "the avatar has no humanoid rig, so a humanoid bone cannot be found.";
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return null;
            }

            int current = bone;
            for (int step = 0; step < 32 && current >= 0 && current < (int)HumanBodyBones.LastBone; step++)
            {
                Transform found = animator.GetBoneTransform((HumanBodyBones)current);
                if (found != null)
                {
                    return found;
                }

                current = HumanTrait.GetParentBone(current);
            }

            why = $"the avatar maps no bone for {(HumanBodyBones)bone} or anything above it.";
            return null;
        }

        private static Transform FindRelative(Transform from, string path)
        {
            Transform at = from;
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                at = segment == ".." ? at.parent : at.Find(segment);
                if (at == null)
                {
                    return null;
                }
            }

            return at;
        }

        private static HashSet<Transform> HumanoidBones(Animator animator)
        {
            HashSet<Transform> bones = new HashSet<Transform>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return bones;
            }

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null)
                {
                    bones.Add(bone);
                }
            }

            return bones;
        }

        /// <summary>VRCFury's Auto-mode test: is any skinned mesh bound to bones outside the link root.</summary>
        private static bool HasExternalSkinBoneReference(Transform propBone, Transform clothingRoot)
        {
            foreach (SkinnedMeshRenderer skin in clothingRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool usesProp = false;
                bool usesOther = false;
                foreach (Transform bone in skin.bones)
                {
                    if (bone == null)
                    {
                        continue;
                    }

                    if (bone == propBone || bone.IsChildOf(propBone))
                    {
                        usesProp = true;
                    }
                    else
                    {
                        usesOther = true;
                    }
                }

                if (usesProp && usesOther)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ScaleFactorOf(
            VrcFuryArmatureLinkData data, bool recursive, Transform propBone, Transform avatarBone)
        {
            if (!recursive)
            {
                return 1f;
            }

            float avatarScale = Mathf.Abs(avatarBone.lossyScale.x);
            float propScale = Mathf.Abs(propBone.lossyScale.x);
            if (avatarScale <= 0f || propScale <= 0f)
            {
                return 1f;
            }

            float factor = propScale / avatarScale;
            double log = System.Math.Log10(factor);
            double mod = (log % 1 + 1) % 1;
            log = mod > 0.75 ? System.Math.Ceiling(log) : System.Math.Floor(log);
            return (float)System.Math.Pow(10, log);
        }

        /// <summary>The clothing's name: the first ancestor of the link root that is not itself a bone.</summary>
        private static string RootNameOf(Transform bone, Transform clothingRoot)
        {
            HashSet<Transform> bones = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer skin in clothingRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.rootBone != null)
                {
                    bones.Add(skin.rootBone);
                }

                foreach (Transform b in skin.bones)
                {
                    if (b != null)
                    {
                        bones.Add(b);
                    }
                }
            }

            Transform at = bone;
            while (at != null && (bones.Contains(at) || at.name.Trim().ToLowerInvariant() == "armature"))
            {
                at = at.parent;
            }

            return at != null ? at.name : clothingRoot.name;
        }

        private static HashSet<Transform> BonesUnderPhysBones(
            AvatarConversionPlan plan, ConversionSource source, Transform propBone)
        {
            HashSet<Transform> under = new HashSet<Transform>();
            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                if (rig.Source != source || rig.SourceRootBone == null)
                {
                    continue;
                }

                foreach (Transform child in rig.SourceRootBone.GetComponentsInChildren<Transform>(true))
                {
                    if (child != rig.SourceRootBone)
                    {
                        under.Add(child);
                    }
                }
            }

            return under;
        }
    }
}
