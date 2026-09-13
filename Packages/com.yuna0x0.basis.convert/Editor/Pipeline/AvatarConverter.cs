using System.Collections.Generic;
using Basis.Scripts.BasisSdk.Constraints;
using GatorDragonGames.JigglePhysics;
using HVR.Vixxy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.Pipeline
{
    public sealed class ConversionResult
    {
        public int RigsWritten;
        public int RigsSkipped;
        public int ConstraintsWritten;
        public int ConstraintsSkipped;
        public bool DescriptorWritten;
        public int HeadChopsWritten;

        /// <summary>
        /// UniVRM runtime components taken off the converted avatar. Left in place, they keep
        /// driving expressions, spring bones and look-at every frame over what was written.
        /// </summary>
        public int VrmRuntimeRemoved;
        public int VixxyControlsWritten;
        public int AuthoredMotionsWritten;

        /// <summary>Blendshapes set for Modular Avatar Shape Changers with no menu item.</summary>
        public int ShapeConstantsWritten;

        /// <summary>Baked motion clips written to the project, which an undo does not remove.</summary>
        public List<string> MotionAssets = new List<string>();
        public List<JiggleRig> Written = new List<JiggleRig>();
        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();

        public int TotalWritten =>
            RigsWritten + ConstraintsWritten + VixxyControlsWritten + AuthoredMotionsWritten
            + HeadChopsWritten + ShapeConstantsWritten + (DescriptorWritten ? 1 : 0);
        public int TotalSkipped => RigsSkipped + ConstraintsSkipped;
    }

    /// <summary>
    /// Applies a plan, producing real components.
    /// <para>
    /// The plan is read from a prefab asset, but the components are written onto whichever
    /// hierarchy is being converted, usually a scene instance of that prefab. The two have the
    /// same shape, so each transform is located in the target by the sibling-index path it has
    /// in the source. Names are not used, since avatars repeat bone names.
    /// </para>
    /// </summary>
    public static class AvatarConverter
    {
        public static ConversionResult Apply(
            AvatarConversionPlan plan, GameObject targetRoot, string undoName = "Convert PhysBones")
        {
            ConversionResult result = new ConversionResult();

            if (plan == null || targetRoot == null)
            {
                result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.noTarget",
                    "A plan and a target hierarchy are both required.");
                return result;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            int group = Undo.GetCurrentGroup();

            Transform target = targetRoot.transform;
            Dictionary<ConversionSource, Transform> roots = LocateSources(plan, target, result);

            foreach (PlannedJiggleRig planned in plan.SelectedRigs())
            {
                if (!TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host)
                    || !TryTranslate(plan, roots, target, planned.Source, planned.SourceRootBone,
                        out Transform root))
                {
                    result.RigsSkipped++;
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The rig for {planned.Describe()} has no counterpart in the target "
                        + "hierarchy and was skipped.");
                    continue;
                }

                ResolvedJiggleRig resolved = new ResolvedJiggleRig
                {
                    Plan = planned.Plan,
                    Host = host.gameObject,
                    RootBone = root,
                };

                foreach (Transform excluded in planned.SourceExcludedTransforms)
                {
                    if (TryTranslate(plan, roots, target, planned.Source, excluded,
                            out Transform translated))
                    {
                        resolved.ExcludedTransforms.Add(translated);
                    }
                }

                if (plan.Options.Colliders)
                {
                    foreach (PlannedJiggleCollider collider in planned.Colliders)
                    {
                        if (!TryTranslate(plan, roots, target, collider.Source ?? planned.Source,
                                collider.SourceTransform, out Transform translated))
                        {
                            continue;
                        }

                        resolved.Colliders.Add(new ResolvedJiggleCollider
                        {
                            Plan = collider.Plan,
                            Transform = translated,
                        });
                    }
                }

                result.Written.Add(JiggleRigWriter.Write(resolved, undoName));
                result.RigsWritten++;
            }

            foreach (PlannedConstraint planned in plan.SelectedConstraints())
            {
                if (!TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host))
                {
                    result.ConstraintsSkipped++;
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The constraint for {planned.Describe()} has no counterpart in the "
                        + "target hierarchy and was skipped.");
                    continue;
                }

                ResolvedBasisConstraint resolved = new ResolvedBasisConstraint
                {
                    Plan = planned.Plan,
                    Host = host.gameObject,
                };

                foreach (Transform sourceTransform in planned.SourceTransforms)
                {
                    resolved.Sources.Add(
                        TryTranslate(plan, roots, target, planned.Source, sourceTransform,
                            out Transform translated)
                            ? translated
                            : null);
                }

                if (TryTranslate(plan, roots, target, planned.Source, planned.SourceWorldUpObject,
                        out Transform worldUp))
                {
                    resolved.WorldUpObject = worldUp;
                }

                BasisConstraintWriter.Write(resolved, undoName);
                result.ConstraintsWritten++;
            }

            WriteDescriptor(plan, roots, target, undoName, result);
            WriteHeadChops(plan, roots, target, undoName, result);
            WriteShapeConstants(plan, roots, target, undoName, result);

            // Motions are written first because a control that switches one has to hold the
            // component, and the component does not exist until it is written.
            Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> motions =
                WriteAuthoredMotions(plan, roots, target, undoName, result);

            WriteVixxyControls(plan, roots, target, motions, undoName, result);
            RemoveVrmRuntime(target, undoName, result);

            Undo.CollapseUndoOperations(group);
            return result;
        }

        /// <summary>
        /// UniVRM's runtime drivers, by type name so the package does not depend on UniVRM.
        /// `Vrm10Instance.LateUpdate` calls `Runtime.Process`, which writes every expression's
        /// blendshapes each frame, zeros included, simulates its own spring bones and aims the
        /// eyes; the 0.x components do the same from their own Update. Any of them left on the
        /// converted avatar undoes the Vixxy controls and the jiggle rigs every frame. The data
        /// components (joints, colliders, constraints, expressions) stay: they hold no behaviour
        /// without the driver, a later conversion reads them, and Basis strips them at build.
        /// </summary>
        private static readonly HashSet<string> VrmRuntimeTypeNames = new HashSet<string>
        {
            "Vrm10Instance",
            "VRMSpringBone",
            "VRMBlendShapeProxy",
            "VRMLookAtHead",
            "VRMLookAt",
            "VRMLookAtBoneApplyer",
            "VRMLookAtBlendShapeApplyer",
        };

        private static void RemoveVrmRuntime(
            Transform target, string undoName, ConversionResult result)
        {
            List<Component> drivers = new List<Component>();
            foreach (Component component in target.GetComponentsInChildren<Component>(true))
            {
                if (component != null && VrmRuntimeTypeNames.Contains(component.GetType().Name))
                {
                    drivers.Add(component);
                }
            }

            if (drivers.Count == 0)
            {
                return;
            }

            HashSet<string> names = new HashSet<string>();
            foreach (Component driver in drivers)
            {
                names.Add(driver.GetType().Name);
                Undo.DestroyObjectImmediate(driver);
                result.VrmRuntimeRemoved++;
            }

            Undo.SetCurrentGroupName(undoName);
            result.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.runtimeRemoved",
                $"{result.VrmRuntimeRemoved} UniVRM runtime components were removed from the "
                + $"converted avatar ({string.Join(", ", names)}). They drove the expressions, "
                + "spring bones and look-at every frame over what was written. Undo restores "
                + "them; Basis strips them at build in any case.");
        }

        /// <summary>
        /// Sets each shape constant on its renderer. A plain property write: undo restores the
        /// weight, and a second conversion writes the same value again.
        /// </summary>
        private static void WriteShapeConstants(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, string undoName, ConversionResult result)
        {
            foreach (ModularAvatarShapeConstant constant in plan.SelectedShapeConstants())
            {
                Transform translated = !string.IsNullOrEmpty(constant.LivePath)
                    ? target.Find(constant.LivePath)
                    : null;
                if (translated == null
                    && !TryTranslate(plan, roots, target, constant.Source, constant.Renderer,
                        out translated))
                {
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The renderer for the Shape Changer on {constant.Origin} has no "
                        + "counterpart in the target hierarchy and was skipped.");
                    continue;
                }

                SkinnedMeshRenderer skinned = translated.GetComponent<SkinnedMeshRenderer>();
                int index = skinned != null && skinned.sharedMesh != null
                    ? skinned.sharedMesh.GetBlendShapeIndex(constant.ShapeName)
                    : -1;
                if (index < 0)
                {
                    continue;
                }

                Undo.RecordObject(skinned, undoName);
                skinned.SetBlendShapeWeight(index, constant.Value);
                EditorUtility.SetDirty(skinned);
                result.ShapeConstantsWritten++;
            }
        }

        private static void WriteHeadChops(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, string undoName, ConversionResult result)
        {
            foreach (PlannedHeadChop planned in plan.SelectedHeadChops())
            {
                if (!TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host))
                {
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The head chop for {planned.Describe()} has no counterpart in the "
                        + "target hierarchy and was skipped.");
                    continue;
                }

                ResolvedHeadChop resolved = new ResolvedHeadChop
                {
                    Plan = planned.Plan,
                    Host = host.gameObject,
                };

                foreach (PlannedHeadChopTarget bone in planned.SourceTargets)
                {
                    if (TryTranslate(plan, roots, target, planned.Source, bone.Transform,
                            out Transform translated))
                    {
                        resolved.Targets.Add(new ResolvedHeadChopTarget
                        {
                            Transform = translated,
                            Scale = bone.Scale,
                        });
                    }
                }

                if (resolved.Targets.Count == 0)
                {
                    continue;
                }

                BasisHeadChopWriter.Write(resolved, undoName);
                result.HeadChopsWritten++;
            }
        }

        private static void WriteDescriptor(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, string undoName, ConversionResult result)
        {
            if (!plan.DescriptorSelected)
            {
                return;
            }

            if (!TryTranslate(plan, roots, target, plan.Descriptor.Source,
                    plan.Descriptor.SourceRoot, out Transform root))
            {
                result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.descriptorUnresolved",
                    "The avatar descriptor has no counterpart in the target hierarchy and was "
                    + "skipped.");
                return;
            }

            ResolvedBasisAvatar resolved = new ResolvedBasisAvatar
            {
                Plan = plan.Descriptor.Plan,
                Root = root.gameObject,
                VisemeMesh = TranslateRenderer(plan, roots, target, plan.Descriptor.Source,
                    plan.Descriptor.SourceVisemeMesh),
                BlinkMesh = TranslateRenderer(plan, roots, target, plan.Descriptor.Source,
                    plan.Descriptor.SourceBlinkMesh),
            };

            BasisAvatarWriter.Write(resolved, undoName);
            result.DescriptorWritten = true;
        }

        private static void WriteVixxyControls(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> motions,
            string undoName, ConversionResult result)
        {
            foreach (PlannedVixxyControl planned in plan.SelectedVixxyControls())
            {
                ResolvedVixxyControl resolved = new ResolvedVixxyControl
                {
                    Plan = planned.Plan,
                    Host = targetRootOf(target),
                };

                bool ok = true;
                for (int i = 0; i < planned.SourceTargets.Count; i++)
                {
                    int motionIndex = i < planned.Plan.Activations.Count
                        ? planned.Plan.Activations[i].MotionIndex
                        : -1;

                    if (motionIndex >= 0)
                    {
                        // The control switches a motion this conversion wrote. If that motion
                        // was left out, the control still switches whatever else it holds.
                        if (motionIndex < planned.Motions.Count
                            && motions.TryGetValue(planned.Motions[motionIndex],
                                out BasisAuthoredMotion component))
                        {
                            // A switched motion starts in whichever state the control's default
                            // choice puts it, so the avatar looks right before anything is
                            // touched. Vixxy sets it again when the control initialises.
                            component.enabled = StartsOn(planned.Plan, i);

                            resolved.Targets.Add(component);
                        }
                        else
                        {
                            resolved.Targets.Add(null);
                        }

                        continue;
                    }

                    if (!TryTranslate(plan, roots, target, planned.Source,
                            planned.SourceTargets[i], out Transform translated))
                    {
                        ok = false;
                        break;
                    }

                    resolved.Targets.Add(translated);
                }

                foreach (Renderer renderer in planned.SourceRenderers)
                {
                    Renderer translated =
                        TranslateRenderer(plan, roots, target, planned.Source, renderer);
                    if (translated == null)
                    {
                        ok = false;
                        break;
                    }

                    resolved.Renderers.Add(translated);
                }

                if (!ok)
                {
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.vixxyUnresolved",
                        $"'{planned.Plan.MenuName}' switches an object with no counterpart in "
                        + "the target hierarchy and was skipped.");
                    continue;
                }

                VixxyWriter.Write(resolved, undoName);
                result.VixxyControlsWritten++;
            }
        }

        /// <summary>
        /// Writes the avatar's ambient motion, baking a clip for each one.
        /// <para>
        /// The bake poses the hierarchy being converted, which is why this runs against the
        /// target rather than the prefab the plan was read from: a clip's paths have to resolve
        /// against a live object for its rotations to be sampled at all.
        /// </para>
        /// </summary>
        private static Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> WriteAuthoredMotions(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, string undoName, ConversionResult result)
        {
            Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> written =
                new Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion>();

            foreach (PlannedAuthoredMotion planned in plan.SelectedAuthoredMotions())
            {
                Transform root = target;
                if (planned.Source != null && !roots.TryGetValue(planned.Source, out root))
                {
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.motionUnresolved",
                        $"'{planned.Describe()}' came from a prefab that is no longer where it "
                        + "was scanned, so its motion was not written.");
                    continue;
                }

                WrittenAuthoredMotion motion = AuthoredMotionWriter.Write(
                    new ResolvedAuthoredMotion
                    {
                        Plan = planned.Plan,
                        Host = root.gameObject,
                        Root = root,
                        Clip = planned.SourceClip,
                        OutputFolder = planned.OutputFolder,
                    },
                    undoName);

                result.Diagnostics.AddRange(motion.Diagnostics);

                if (motion.Component == null)
                {
                    continue;
                }

                written[planned] = motion.Component;
                result.AuthoredMotionsWritten++;
                if (!string.IsNullOrEmpty(motion.AssetPath))
                {
                    result.MotionAssets.Add(motion.AssetPath);
                }
            }

            return written;
        }

        /// <summary>
        /// Whether the control's default choice has this activation switched on. The default is
        /// a value rather than an index, matched against the choices' own values the way Vixxy
        /// matches it; an avatar declaring a default no choice carries starts at the first.
        /// </summary>
        private static bool StartsOn(VixxyControlPlan plan, int activation)
        {
            bool[] choices = plan.Activations[activation].Choices;
            int start = 0;

            for (int i = 0; i < plan.ChoiceValues.Count; i++)
            {
                if (Mathf.Approximately(plan.ChoiceValues[i], plan.DefaultValue))
                {
                    start = i;
                    break;
                }
            }

            return start < choices.Length && choices[start];
        }

        private static GameObject targetRootOf(Transform target) => target.gameObject;

        private static T TranslateRenderer<T>(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionSource source, T renderer)
            where T : Renderer
        {
            if (renderer == null)
            {
                return null;
            }

            return TryTranslate(plan, roots, target, source, renderer.transform,
                out Transform translated)
                ? translated.GetComponent<T>()
                : null;
        }

        /// <summary>
        /// Components of the kinds a conversion writes, on exactly the transforms this plan
        /// would write to, with the current options applied.
        /// <para>
        /// This is how converting twice avoids stacking a second set on top of the first. It
        /// needs no stored state: the plan already knows every transform it targets, so the
        /// previous output can be found by looking there. Anything on a transform the plan does
        /// not touch is left alone, so rigs added by hand elsewhere survive, as does an earlier
        /// conversion's output where the options have since narrowed what gets written.
        /// </para>
        /// </summary>
        public static List<Component> FindReplaceable(
            AvatarConversionPlan plan, GameObject targetRoot)
        {
            List<Component> found = new List<Component>();
            if (plan == null || targetRoot == null || plan.SourceRoot == null)
            {
                return found;
            }

            Transform target = targetRoot.transform;
            Dictionary<ConversionSource, Transform> roots =
                LocateSources(plan, target, new ConversionResult());
            HashSet<Transform> seen = new HashSet<Transform>();

            foreach (PlannedJiggleRig planned in plan.SelectedRigs())
            {
                if (TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host)
                    && seen.Add(host))
                {
                    found.AddRange(host.GetComponents<JiggleRig>());
                }
            }

            FindReplaceableOnRoot(plan, targetRoot, found);

            seen.Clear();
            foreach (PlannedConstraint planned in plan.SelectedConstraints())
            {
                if (TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host)
                    && seen.Add(host))
                {
                    found.AddRange(host.GetComponents<BasisConstraintBase>());
                }
            }

            return found;
        }

        /// <summary>
        /// The Vixxy controls and authored motions a re-conversion replaces.
        /// <para>
        /// These all sit on the avatar root rather than on a transform of their own, so the rule
        /// the rest of the converter uses, replace only what is on a transform this plan writes
        /// to, says nothing here. What separates ours from a control somebody added by hand is
        /// the name: a menu item titled after a toggle this conversion is about to write is the
        /// one it wrote last time. Anything else on the root is left alone.
        /// </para>
        /// </summary>
        private static void FindReplaceableOnRoot(
            AvatarConversionPlan plan, GameObject targetRoot, List<Component> found)
        {
            HashSet<string> titles = new HashSet<string>();
            foreach (PlannedVixxyControl planned in plan.SelectedVixxyControls())
            {
                titles.Add(planned.Plan.MenuName);
            }

            if (titles.Count > 0)
            {
                foreach (HVRVixxyMenuItem item in targetRoot.GetComponents<HVRVixxyMenuItem>())
                {
                    SerializedObject serialized = new SerializedObject(item);
                    string title = serialized.FindProperty("title")?.stringValue;
                    Object linked =
                        serialized.FindProperty("control")?.objectReferenceValue;
                    serialized.Dispose();

                    if (string.IsNullOrEmpty(title) || !titles.Contains(title))
                    {
                        continue;
                    }

                    found.Add(item);
                    if (linked is HVRVixxyControl control)
                    {
                        found.Add(control);
                    }
                }
            }

            HashSet<string> labels = new HashSet<string>();
            foreach (PlannedAuthoredMotion planned in plan.SelectedAuthoredMotions())
            {
                labels.Add(planned.Plan.Label);
            }

            if (labels.Count == 0)
            {
                return;
            }

            foreach (BasisAuthoredMotion motion in targetRoot.GetComponents<BasisAuthoredMotion>())
            {
                foreach (BasisAuthoredMotion.Movement movement in motion.movements)
                {
                    if (movement != null && labels.Contains(movement.label))
                    {
                        found.Add(motion);
                        break;
                    }
                }
            }
        }

        /// <summary>Removes what <see cref="FindReplaceable"/> finds, under one undo step.</summary>
        public static int RemoveReplaceable(
            AvatarConversionPlan plan, GameObject targetRoot, string undoName)
        {
            List<Component> replaceable = FindReplaceable(plan, targetRoot);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            int removed = 0;
            foreach (Component component in replaceable)
            {
                if (component != null)
                {
                    Undo.DestroyObjectImmediate(component);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Where each prefab the plan was read from sits in the hierarchy being converted.
        /// <para>
        /// The avatar's own prefab is the target root. Clothing and accessories sit somewhere
        /// under it, at the path they were found at, and their transforms are in their own
        /// prefab's space, so each needs its own root to translate against.
        /// </para>
        /// </summary>
        private static Dictionary<ConversionSource, Transform> LocateSources(
            AvatarConversionPlan plan, Transform target, ConversionResult result)
        {
            Dictionary<ConversionSource, Transform> roots =
                new Dictionary<ConversionSource, Transform>();

            foreach (ConversionSource source in plan.Sources)
            {
                if (source.IsPrimary)
                {
                    roots[source] = target;
                    continue;
                }

                if (TransformIndexPath.TryResolve(target, source.PathInHierarchy,
                        out Transform at))
                {
                    roots[source] = at;
                    continue;
                }

                result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.sourceUnresolved",
                    $"{source.Name} is not where it was when this was scanned, so nothing read "
                    + "from it was written. Rescan and convert again.");
            }

            return roots;
        }

        /// <summary>
        /// Locates a transform of one source prefab in the hierarchy being converted.
        /// <para>
        /// An item with no source recorded belongs to the avatar itself. A plan built by hand
        /// and anything resolved against the avatar root are in that case.
        /// </para>
        /// </summary>
        private static bool TryTranslate(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionSource source, Transform sourceTransform,
            out Transform translated)
        {
            translated = null;
            if (sourceTransform == null)
            {
                return false;
            }

            Transform sourceRoot;
            Transform targetRoot;

            if (source == null)
            {
                if (plan.SourceRoot == null)
                {
                    return false;
                }

                sourceRoot = plan.SourceRoot.transform;
                targetRoot = target;
            }
            else
            {
                if (!roots.TryGetValue(source, out targetRoot) || source.Root == null)
                {
                    return false;
                }

                sourceRoot = source.Root.transform;
            }

            if (sourceRoot == targetRoot)
            {
                translated = sourceTransform;
                return true;
            }

            int[] path = TransformIndexPath.Of(sourceRoot, sourceTransform);
            return path != null && TransformIndexPath.TryResolve(targetRoot, path, out translated);
        }
    }
}
