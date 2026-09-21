using System.Collections.Generic;
using System.Linq;
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
        public int ArmatureLinksApplied;
        public int BonesLinked;
        public bool DescriptorWritten;
        public int HeadChopsWritten;

        /// <summary>
        /// UniVRM runtime components taken off the converted avatar. Left in place, they keep
        /// driving expressions, spring bones and look-at every frame over what was written.
        /// </summary>
        public int VrmRuntimeRemoved;
        public int VixxyControlsWritten;
        public int VixxyControlsSkipped;
        public int HeadChopsSkipped;
        public int ArmatureLinksSkipped;
        public int DescriptorSkipped;
        public int AuthoredMotionsWritten;

        /// <summary>Baked motion clips written to the project, which an undo does not remove.</summary>
        public List<string> MotionAssets = new List<string>();
        public List<JiggleRig> Written = new List<JiggleRig>();
        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();

        public int TotalWritten =>
            RigsWritten + ConstraintsWritten + VixxyControlsWritten + AuthoredMotionsWritten
            + HeadChopsWritten + (DescriptorWritten ? 1 : 0);
        public int TotalSkipped =>
            RigsSkipped + ConstraintsSkipped + VixxyControlsSkipped + HeadChopsSkipped
            + ArmatureLinksSkipped + DescriptorSkipped;
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
            Dictionary<PlannedJiggleRig, JiggleRig> rigs = new Dictionary<PlannedJiggleRig, JiggleRig>();

            // Everything is located before any bone moves. An armature link takes clothing
            // bones out of their prefab instance, after which the sibling-index path from that
            // prefab no longer finds them; a transform located first survives the move.
            List<(PlannedJiggleRig Planned, ResolvedJiggleRig Resolved)> plannedRigs =
                ResolveRigs(plan, roots, target, result);
            List<ResolvedBasisConstraint> constraints = ResolveConstraints(plan, roots, target, result);
            ResolvedBasisAvatar descriptor = ResolveDescriptor(plan, roots, target, result);
            List<ResolvedHeadChop> headChops = ResolveHeadChops(plan, roots, target, result);
            List<LocatedVixxyControl> controls = ResolveVixxyControls(plan, roots, target, result);

            // Bones move before anything is written, so the rest poses the rigs and constraints
            // capture from the transforms are the linked ones.
            foreach (PlannedArmatureLink planned in plan.SelectedArmatureLinks())
            {
                ResolvedArmatureLink resolved = new ResolvedArmatureLink
                {
                    AlignPosition = planned.AlignPosition,
                    AlignRotation = planned.AlignRotation,
                    AlignScale = planned.AlignScale,
                    ScaleFactor = planned.ScaleFactor,
                    RootName = planned.RootName,
                };

                foreach (PlannedBoneLink bone in planned.Bones)
                {
                    if (TryTranslate(plan, roots, target, planned.Source, bone.SourceProp, out Transform prop)
                        && TryTranslate(plan, roots, target, planned.AvatarSource, bone.SourceAvatar, out Transform avatar))
                    {
                        resolved.Bones.Add(new ResolvedBoneLink { Prop = prop, Avatar = avatar });
                    }
                }

                if (resolved.Bones.Count == 0)
                {
                    result.ArmatureLinksSkipped++;
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The armature link {planned.Describe()} has no counterpart in the target "
                        + "hierarchy and was skipped.");
                    continue;
                }

                result.BonesLinked += ArmatureLinkWriter.Write(resolved, undoName);
                result.ArmatureLinksApplied++;

                if (resolved.Unpacked.Count > 0)
                {
                    result.Diagnostics.Add(DiagnosticSeverity.Approximated, "apply.unpacked",
                        $"The prefab instance {string.Join(", ", resolved.Unpacked.Distinct())} was "
                        + "unpacked so its bones could go under the avatar's; Unity allows no "
                        + "other way. What it carried was already read, and undo restores it.");
                }
            }

            foreach ((PlannedJiggleRig planned, ResolvedJiggleRig resolved) in plannedRigs)
            {
                JiggleRig written = JiggleRigWriter.Write(resolved, undoName);
                rigs[planned] = written;
                result.Written.Add(written);
                result.RigsWritten++;
            }

            foreach (ResolvedBasisConstraint resolved in constraints)
            {
                BasisConstraintWriter.Write(resolved, undoName);
                result.ConstraintsWritten++;
            }

            if (descriptor != null)
            {
                BasisAvatarWriter.Write(descriptor, undoName);
                result.DescriptorWritten = true;
            }

            foreach (ResolvedHeadChop resolved in headChops)
            {
                BasisHeadChopWriter.Write(resolved, undoName);
                result.HeadChopsWritten++;
            }

            // Motions are written first because a control that switches one has to hold the
            // component, and the component does not exist until it is written.
            Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> motions =
                WriteAuthoredMotions(plan, roots, target, undoName, result);

            WriteVixxyControls(controls, target, motions, rigs, undoName, result);
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
                $"{result.VrmRuntimeRemoved} UniVRM runtime components removed (" + string.Join(", ", names) + "). They rewrote expressions, spring bones and look-at every frame. Undo restores them; Basis strips them at build.");
        }

        private static List<(PlannedJiggleRig Planned, ResolvedJiggleRig Resolved)> ResolveRigs(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionResult result)
        {
            List<(PlannedJiggleRig, ResolvedJiggleRig)> located =
                new List<(PlannedJiggleRig, ResolvedJiggleRig)>();

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

                located.Add((planned, resolved));
            }

            return located;
        }

        private static List<ResolvedBasisConstraint> ResolveConstraints(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionResult result)
        {
            List<ResolvedBasisConstraint> located = new List<ResolvedBasisConstraint>();

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

                located.Add(resolved);
            }

            return located;
        }

        private static List<ResolvedHeadChop> ResolveHeadChops(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionResult result)
        {
            List<ResolvedHeadChop> located = new List<ResolvedHeadChop>();
            foreach (PlannedHeadChop planned in plan.SelectedHeadChops())
            {
                if (!TryTranslate(plan, roots, target, planned.Source, planned.SourceHost,
                        out Transform host))
                {
                    result.HeadChopsSkipped++;
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
                    result.HeadChopsSkipped++;
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.unresolved",
                        $"The head chop for {planned.Describe()} names no bone with a "
                        + "counterpart in the target hierarchy and was skipped.");
                    continue;
                }

                located.Add(resolved);
            }

            return located;
        }

        private static ResolvedBasisAvatar ResolveDescriptor(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionResult result)
        {
            if (!plan.DescriptorSelected)
            {
                return null;
            }

            if (!TryTranslate(plan, roots, target, plan.Descriptor.Source,
                    plan.Descriptor.SourceRoot, out Transform root))
            {
                result.DescriptorSkipped++;
                result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.descriptorUnresolved",
                    "The avatar descriptor has no counterpart in the target hierarchy and was "
                    + "skipped.");
                return null;
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

            return resolved;
        }

        /// <summary>A planned control with its objects located in the target, ahead of the write.</summary>
        private sealed class LocatedVixxyControl
        {
            public PlannedVixxyControl Planned;

            /// <summary>One per activation: the target's transform, or null for a motion or rig slot.</summary>
            public List<Transform> Targets = new List<Transform>();

            public List<Renderer> Renderers = new List<Renderer>();
        }

        /// <summary>
        /// Locates every object a control touches. A control's transforms are in the scanned
        /// hierarchy's space whichever prefab its toggle came from, so they translate from the
        /// hierarchy root rather than from a source.
        /// </summary>
        private static List<LocatedVixxyControl> ResolveVixxyControls(
            AvatarConversionPlan plan, Dictionary<ConversionSource, Transform> roots,
            Transform target, ConversionResult result)
        {
            List<LocatedVixxyControl> located = new List<LocatedVixxyControl>();

            foreach (PlannedVixxyControl planned in plan.SelectedVixxyControls())
            {
                LocatedVixxyControl control = new LocatedVixxyControl { Planned = planned };
                bool ok = true;

                for (int i = 0; i < planned.SourceTargets.Count && ok; i++)
                {
                    int motionIndex = i < planned.Plan.Activations.Count
                        ? planned.Plan.Activations[i].MotionIndex
                        : -1;

                    if (motionIndex >= 0)
                    {
                        control.Targets.Add(null);
                        continue;
                    }

                    if (TryTranslate(plan, roots, target, null, planned.SourceTargets[i],
                            out Transform translated))
                    {
                        control.Targets.Add(translated);
                    }
                    else
                    {
                        ok = false;
                    }
                }

                foreach (Renderer renderer in planned.SourceRenderers)
                {
                    if (!ok)
                    {
                        break;
                    }

                    Renderer translated = TranslateRenderer(plan, roots, target, null, renderer);
                    if (translated == null)
                    {
                        ok = false;
                        break;
                    }

                    control.Renderers.Add(translated);
                }

                if (!ok)
                {
                    result.VixxyControlsSkipped++;
                    result.Diagnostics.Add(DiagnosticSeverity.Warning, "apply.vixxyUnresolved",
                        $"'{planned.Plan.MenuName}' switches an object with no counterpart in "
                        + "the target hierarchy and was skipped.");
                    continue;
                }

                located.Add(control);
            }

            return located;
        }

        private static void WriteVixxyControls(
            List<LocatedVixxyControl> controls, Transform target,
            Dictionary<PlannedAuthoredMotion, BasisAuthoredMotion> motions,
            Dictionary<PlannedJiggleRig, JiggleRig> rigs, string undoName, ConversionResult result)
        {
            foreach (LocatedVixxyControl control in controls)
            {
                PlannedVixxyControl planned = control.Planned;
                ResolvedVixxyControl resolved = new ResolvedVixxyControl
                {
                    Plan = planned.Plan,
                    Host = targetRootOf(target),
                };

                for (int i = 0; i < control.Targets.Count; i++)
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

                    VixxyActivationTarget kind = i < planned.Plan.Activations.Count
                        ? planned.Plan.Activations[i].Target
                        : VixxyActivationTarget.Object;

                    // A PhysBone switch drives the rig written for it. Left out of the write,
                    // the control still switches whatever else it holds.
                    if (kind == VixxyActivationTarget.Component)
                    {
                        PlannedJiggleRig rig = i < planned.SourceRigs.Count ? planned.SourceRigs[i] : null;
                        if (rig != null && rigs.TryGetValue(rig, out JiggleRig component))
                        {
                            component.enabled = StartsOn(planned.Plan, i);
                            resolved.Targets.Add(component);
                        }
                        else
                        {
                            resolved.Targets.Add(null);
                        }

                        continue;
                    }

                    if (kind == VixxyActivationTarget.Renderer)
                    {
                        resolved.Targets.Add(control.Targets[i].GetComponent<Renderer>());
                        continue;
                    }

                    resolved.Targets.Add(control.Targets[i]);
                }

                resolved.Renderers.AddRange(control.Renderers);

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

            // No source: the transform is in the scanned hierarchy's own space, which is where
            // every Vixxy target lives whichever prefab its toggle was read from.
            if (source == null)
            {
                GameObject hierarchy = plan.HierarchyRoot != null ? plan.HierarchyRoot : plan.SourceRoot;
                if (hierarchy == null)
                {
                    return false;
                }

                sourceRoot = hierarchy.transform;
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
