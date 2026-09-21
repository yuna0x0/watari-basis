using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>One rig the conversion intends to produce, and where it will go.</summary>
    public sealed class PlannedJiggleRig
    {
        public JiggleRigPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        /// <summary>Bone the rig is rooted at, in the source hierarchy.</summary>
        public Transform SourceRootBone;

        /// <summary>The transform carrying the source component, in the source hierarchy.</summary>
        public Transform SourceHost;

        public List<Transform> SourceExcludedTransforms = new List<Transform>();
        public List<PlannedJiggleCollider> Colliders = new List<PlannedJiggleCollider>();

        /// <summary>Everything the mapping reported, plus anything the planner added.</summary>
        public IEnumerable<ConversionDiagnostic> Diagnostics => Plan.Diagnostics;

        public string Describe()
        {
            return SourceRootBone != null ? SourceRootBone.name : "(unresolved)";
        }
    }

    public sealed class PlannedJiggleCollider
    {
        public JiggleColliderPlan Plan;
        public Transform SourceTransform;

        /// <summary>
        /// The source component was disabled. Both PhysBone and Dynamic Bone skip such a
        /// collider, so rigs referencing it get nothing and no warning.
        /// </summary>
        public bool Disabled;

        /// <summary>The prefab this was read from, which its transform belongs to.</summary>
        public ConversionSource Source;
    }

    /// <summary>One Vixxy control the conversion intends to produce.</summary>
    public sealed class PlannedVixxyControl
    {
        public VixxyControlPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        /// <summary>
        /// One per activation, in the same order. An activation that switches a motion rather
        /// than an object has no transform of its own and holds null here.
        /// </summary>
        public List<Transform> SourceTargets = new List<Transform>();

        /// <summary>
        /// One per activation, in the same order: the planned rig a script switch resolved to,
        /// or null for every other kind of activation.
        /// </summary>
        public List<PlannedJiggleRig> SourceRigs = new List<PlannedJiggleRig>();

        /// <summary>
        /// Renderers the subjects name, in the same order as the plan's subjects. Blendshapes
        /// need a skinned mesh; material properties work on any renderer.
        /// </summary>
        public List<Renderer> SourceRenderers = new List<Renderer>();

        /// <summary>
        /// The motions this control switches, in the same order as the plan's motions. Each is
        /// also in the plan's own motion list, so it is written and can be deselected there.
        /// </summary>
        public List<PlannedAuthoredMotion> Motions = new List<PlannedAuthoredMotion>();
    }

    /// <summary>One authored motion the conversion intends to produce, and the clip behind it.</summary>
    public sealed class PlannedAuthoredMotion
    {
        public AuthoredMotionPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        /// <summary>The animation to bake. Baking needs a scene, so it happens when applied.</summary>
        public AnimationClip SourceClip;

        /// <summary>Project folder the baked clip asset goes in, beside the animation.</summary>
        public string OutputFolder = string.Empty;

        /// <summary>
        /// The control that switches this motion on, when a menu toggle does. A motion with
        /// nothing to switch it plays from load, so one written without its control would play
        /// permanently: it is only written when that control is.
        /// </summary>
        public PlannedVixxyControl SwitchedBy;

        public string Describe() => Plan != null ? Plan.Label : "(unnamed)";
    }

    /// <summary>The avatar descriptor the conversion intends to produce.</summary>
    public sealed class PlannedAvatarDescriptor
    {
        public BasisAvatarPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        /// <summary>Kept so the expression assets it references can be followed.</summary>
        public VrcAvatarDescriptorData SourceData;
        public Transform SourceRoot;
        public SkinnedMeshRenderer SourceVisemeMesh;
        public SkinnedMeshRenderer SourceBlinkMesh;
    }

    /// <summary>One Basis constraint the conversion intends to produce, and where it will go.</summary>
    public sealed class PlannedConstraint
    {
        public BasisConstraintPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        /// <summary>
        /// The transform the component will sit on, which is the transform the constraint drives.
        /// </summary>
        public Transform SourceHost;

        public List<Transform> SourceTransforms = new List<Transform>();
        public Transform SourceWorldUpObject;

        public string Describe()
        {
            return SourceHost != null
                ? $"{SourceHost.name} ({Plan.Kind})"
                : $"(unresolved {Plan.Kind})";
        }
    }

    public sealed class PlannedHeadChopTarget
    {
        public Transform Transform;
        public float Scale;
    }

    /// <summary>One Basis head chop the conversion intends to produce, and where it will go.</summary>
    public sealed class PlannedHeadChop
    {
        public BasisHeadChopPlan Plan;

        /// <summary>Whether the conversion will write this one. Cleared from the window.</summary>
        public bool Include = true;

        /// <summary>The prefab this was read from, which its transforms belong to.</summary>
        public ConversionSource Source;

        public Transform SourceHost;
        public List<PlannedHeadChopTarget> SourceTargets = new List<PlannedHeadChopTarget>();

        public string Describe()
        {
            return SourceHost != null
                ? $"{SourceHost.name} (head chop, {SourceTargets.Count} bones)"
                : "(unresolved head chop)";
        }
    }

    /// <summary>
    /// The result of reading and mapping an avatar, before anything is written. This is what a
    /// dry run shows.
    /// </summary>
    public sealed class AvatarConversionPlan
    {
        public string SourceAssetPath;

        /// <summary>
        /// Root of the hierarchy the plan was read from, which is the avatar's own prefab.
        /// Clothing and accessories are prefabs of their own; see <see cref="Sources"/>.
        /// </summary>
        public GameObject SourceRoot;

        /// <summary>
        /// The object that was scanned: the scene instance when a hierarchy was planned, the
        /// asset itself when a prefab path was. Every Vixxy target and renderer this plan holds
        /// is a transform under it, so a control switches the object the avatar carries, with
        /// its instance overrides, wherever the toggle was read from.
        /// </summary>
        public GameObject HierarchyRoot;

        /// <summary>Finds the hierarchy transform for a transform of any source's asset.</summary>
        public HierarchyLocator Locator;

        /// <summary>
        /// <summary>Every scanned file's documents by the file's guid, for variant overrides.</summary>
        internal Dictionary<string, Dictionary<long, UnityYamlDocument>> DocumentsByGuid =
            new Dictionary<string, Dictionary<long, UnityYamlDocument>>();

        /// <summary>Overrides read from every scanned file so far, applied as files arrive.</summary>
        internal List<PrefabModification> Modifications = new List<PrefabModification>();
        public int OverridesApplied;

        /// <summary>Each read file's resolver, by the file's guid, for references that cross files.</summary>
        public Dictionary<string, PrefabObjectResolver> ResolversByGuid =
            new Dictionary<string, PrefabObjectResolver>();

        /// <summary>Colliders planned so far, by the file and id that define them.</summary>
        public Dictionary<(string guid, long fileId), PlannedJiggleCollider> ColliderIndex =
            new Dictionary<(string, long), PlannedJiggleCollider>();

        private readonly Dictionary<long, (string guid, long fileId)> _foreign =
            new Dictionary<long, (string, long)>();

        /// <summary>
        /// An id of the plan's own for an object of another file, stable for the pair. Real file
        /// ids never carry the top bits set here.
        /// </summary>
        public long ForeignId(string guid, long fileId)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (char c in guid)
                {
                    hash = (hash ^ c) * 1099511628211L;
                }

                hash = (hash ^ fileId) * 1099511628211L;
                long alias = (hash & 0x0FFFFFFFFFFFFFFFL) | 0x4000000000000000L;
                _foreign[alias] = (guid, fileId);
                return alias;
            }
        }

        /// <summary>
        /// Records what a stub in the scene file stands for, under the stub's own id. A scene
        /// component naming a prefab collider names the stub, and this is what lets that name
        /// reach the collider's file and id.
        /// </summary>
        public void RegisterForeign(long alias, string guid, long fileId)
        {
            _foreign[alias] = (guid, fileId);
        }

        public bool TryForeign(long alias, out string guid, out long fileId)
        {
            if (_foreign.TryGetValue(alias, out (string guid, long fileId) pair))
            {
                guid = pair.guid;
                fileId = pair.fileId;
                return true;
            }

            guid = null;
            fileId = 0L;
            return false;
        }

        /// <summary>The live object behind a plan-owned id, through the file's own resolver.</summary>
        public UnityEngine.Object ResolveForeign(long alias)
        {
            if (!TryForeign(alias, out string guid, out long fileId)
                || !ResolversByGuid.TryGetValue(guid, out PrefabObjectResolver resolver))
            {
                return null;
            }

            return resolver.TryResolve(fileId, out UnityEngine.Object resolved) ? resolved : null;
        }

        /// <summary>
        /// Every prefab this plan was read from, the avatar's own first. Each planned item
        /// records which one it came from, because its transforms are in that prefab's space.
        /// </summary>
        public List<ConversionSource> Sources = new List<ConversionSource>();

        /// <summary>
        /// Which parts of this plan a conversion will write. Everything, unless the window
        /// narrows it.
        /// </summary>
        public ConversionOptions Options = new ConversionOptions();

        public List<PlannedJiggleRig> Rigs = new List<PlannedJiggleRig>();

        /// <summary>
        /// Every collider the avatar defines, mapped once. Rigs reference entries from here
        /// rather than each mapping its own copy, so a collider shared by twenty PhysBones is
        /// reported once rather than twenty times.
        /// </summary>
        public List<PlannedJiggleCollider> Colliders = new List<PlannedJiggleCollider>();

        public List<PlannedConstraint> Constraints = new List<PlannedConstraint>();

        /// <summary>Written with the descriptor: both describe the avatar rather than its parts.</summary>
        public List<PlannedHeadChop> HeadChops = new List<PlannedHeadChop>();

        /// <summary>The avatar descriptor, when the source had one.</summary>
        public PlannedAvatarDescriptor Descriptor;

        /// <summary>
        /// The expression menu tree and parameters. Nothing here converts; it is read so the
        /// report can describe what rebuilding it in HVR Vixxy involves, and because the menu
        /// entries are what name a control and its choices.
        /// </summary>
        public VrcExpressionInventory Expressions = new VrcExpressionInventory();

        /// <summary>
        /// Menu toggles whose animator layer and clips were found. Each one a Vixxy control can
        /// hold is in <see cref="VixxyControls"/>; the rest are here and reported with why.
        /// </summary>
        public List<ResolvedToggle> Toggles = new List<ResolvedToggle>();

        /// <summary>
        /// Toggles Modular Avatar would install from a piece of clothing, with the prefab each
        /// came from. Separate from <see cref="Toggles"/>, which are the avatar's own.
        /// </summary>
        public List<ModularAvatarToggle> ModularAvatarToggles = new List<ModularAvatarToggle>();

        /// <summary>Toggles read from VRCFury components, one entry per prefab they came from.</summary>
        public List<VrcFuryToggle> VrcFuryToggles = new List<VrcFuryToggle>();

        /// <summary>Clothing bones to parent under the avatar's, from VRCFury Armature Links.</summary>
        public List<PlannedArmatureLink> ArmatureLinks = new List<PlannedArmatureLink>();

        /// <summary>What the VRCFury pass found across every prefab, for the report.</summary>
        public VrcFuryReadResult VrcFury = new VrcFuryReadResult();

        /// <summary>Menu toggles that can be rebuilt as Vixxy controls, with their targets.</summary>
        public List<PlannedVixxyControl> VixxyControls = new List<PlannedVixxyControl>();

        /// <summary>

        /// <summary>
        /// Animation that plays unprompted, rebuilt as authored motion. Basis has no animator
        /// layers on an avatar, so this is the only place looping motion can live.
        /// </summary>
        public List<PlannedAuthoredMotion> AuthoredMotions = new List<PlannedAuthoredMotion>();

        /// <summary>Diagnostics about the avatar as a whole, rather than one component.</summary>
        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();

        /// <summary>
        /// Diagnostics about the menu and the controls rebuilt from it. Kept apart from the rest
        /// so that switching menu toggles off leaves their losses out of the report, the same as
        /// every other category.
        /// </summary>
        public List<ConversionDiagnostic> ToggleDiagnostics = new List<ConversionDiagnostic>();

        /// <summary>Diagnostics about authored motion, gated the same way.</summary>
        public List<ConversionDiagnostic> MotionDiagnostics = new List<ConversionDiagnostic>();

        /// <summary>
        /// What the humanoid rig looks like to Basis's full-body IK. Not a conversion, so these
        /// are kept apart from the component diagnostics.
        /// </summary>
        public List<ConversionDiagnostic> RigDiagnostics = new List<ConversionDiagnostic>();

        /// <summary>
        /// Modular Avatar components that rearrange the hierarchy. They work on Basis, so these
        /// are counted to be reported as left alone rather than as unrecognised.
        /// </summary>
        public int ModularAvatarHierarchyFound;

        /// <summary>
        /// Modular Avatar components that target VRChat's menu and animator layers, which Basis
        /// does not have.
        /// </summary>
        public int ModularAvatarMenuFound;

        /// <summary>
        /// Modular Avatar components tied to VRChat's own systems, which have nothing to act on
        /// under Basis.
        /// </summary>
        public int ModularAvatarVrchatOnlyFound;

        /// <summary>Components of editor-time tools that carry no runtime behaviour.</summary>
        public int EditorOnlyToolsFound;

        /// <summary>VRCFury components, whose data this version does not read.</summary>
        public int VrcFuryComponentsFound;

        /// <summary>VRCFury's debug markers, which only a built copy carries.</summary>
        public int VrcFuryBuildMarkersFound;

        /// <summary>Prefabs read because a source inherits from them, being a variant.</summary>
        public int InheritedSourcesRead;

        /// <summary>Recognised components read out of the source files, whatever became of them.</summary>
        public int ComponentsRead;

        /// <summary>Components with missing scripts that exist on the scene object only.</summary>
        public int SceneOnlyMissingScripts;

        /// <summary>Recognised components read from the saved scene file.</summary>
        public int SceneComponentsRead;

        public int PhysBonesFound;

        /// <summary>VRM spring chains found, from either VRM format.</summary>
        public int VrmChainsFound;

        /// <summary>VRM expressions found, whether or not each became a control.</summary>
        public int VrmExpressionsFound;

        /// <summary>VRM 0.x look at components, which Basis has no use for.</summary>
        public int VrmLookAtFound;

        /// <summary>
        /// The expressions as read, kept because the ones Basis drives itself, the vowels and
        /// blink, fill the Basis Avatar's viseme slots rather than becoming controls.
        /// </summary>
        public List<VrmExpressionData> VrmExpressions = new List<VrmExpressionData>();

        /// <summary>VRM 1.0 node constraints found. Not converted yet.</summary>
        public int VrmConstraintsFound;

        /// <summary>What a VRM avatar says about its eyes and its first person view.</summary>
        public VrmAvatarSettingsData VrmSettings;

        /// <summary>
        /// The bone a VRM 0.x avatar measures its eye offset from, when it named one. Usually the
        /// head, but the format lets it be any bone.
        /// </summary>
        public Transform VrmEyeOrigin;

        /// <summary>
        /// The licence a VRM avatar carries. Converting one is a modification, so this is read
        /// and shown before anything is written.
        /// </summary>
        public VrmMetaData VrmMeta;

        public int CollidersFound;
        public int ConstraintsFound;
        public int DynamicBonesFound;
        public int ContactsFound;
        public int HeadChopsFound;
        public int RaycastsFound;
        public int SpatialAudioFound;
        public int VrcBuildSettingsFound;

        /// <summary>What kind of source this appears to be, for the reader to sanity check.</summary>
        public SourceProfile Profile = new SourceProfile();

        /// <summary>Components identified in the file but not tied to a live transform.</summary>
        public int Unresolved;

        /// <summary>Everything the plan holds, whatever the options are set to.</summary>
        public int TotalPlanned =>
            Rigs.Count + Constraints.Count + VixxyControls.Count + AuthoredMotions.Count
            + HeadChops.Count + (Descriptor != null ? 1 : 0);

        /// <summary>What a conversion would write, with the current options applied.</summary>
        public int TotalSelected =>
            SelectedRigCount + SelectedConstraintCount + SelectedVixxyControlCount
            + SelectedAuthoredMotionCount + SelectedHeadChopCount + (DescriptorSelected ? 1 : 0);

        /// <summary>Head chops go with the descriptor: both describe the avatar, not its parts.</summary>
        public IEnumerable<PlannedHeadChop> SelectedHeadChops()
        {
            if (!Options.Descriptor)
            {
                yield break;
            }

            foreach (PlannedHeadChop chop in HeadChops)
            {
                if (chop.Include && IsIncluded(chop.Source))
                {
                    yield return chop;
                }
            }
        }

        public int SelectedHeadChopCount => Tally(SelectedHeadChops());

        public IEnumerable<PlannedJiggleRig> SelectedRigs()
        {
            if (!Options.Physics)
            {
                yield break;
            }

            foreach (PlannedJiggleRig rig in Rigs)
            {
                if (rig.Include && IsIncluded(rig.Source))
                {
                    yield return rig;
                }
            }
        }

        public IEnumerable<PlannedArmatureLink> SelectedArmatureLinks()
        {
            if (!Options.ArmatureLinks)
            {
                yield break;
            }

            foreach (PlannedArmatureLink link in ArmatureLinks)
            {
                if (link.Include)
                {
                    yield return link;
                }
            }
        }

        public IEnumerable<PlannedConstraint> SelectedConstraints()
        {
            if (!Options.Constraints)
            {
                yield break;
            }

            foreach (PlannedConstraint constraint in Constraints)
            {
                if (constraint.Include && IsIncluded(constraint.Source))
                {
                    yield return constraint;
                }
            }
        }
        public IEnumerable<PlannedVixxyControl> SelectedVixxyControls()
        {
            if (!Options.Toggles)
            {
                yield break;
            }

            foreach (PlannedVixxyControl control in VixxyControls)
            {
                if (control.Include && IsIncluded(control.Source))
                {
                    yield return control;
                }
            }
        }

        public IEnumerable<PlannedAuthoredMotion> SelectedAuthoredMotions()
        {
            if (!Options.Motion)
            {
                yield break;
            }

            foreach (PlannedAuthoredMotion motion in AuthoredMotions)
            {
                if (motion.Include && IsIncluded(motion.Source)
                    && (motion.SwitchedBy == null || IsSelected(motion.SwitchedBy)))
                {
                    yield return motion;
                }
            }
        }

        /// <summary>Whether a conversion with these options would write this control.</summary>
        public bool IsSelected(PlannedVixxyControl control) =>
            Options.Toggles && control != null && control.Include && IsIncluded(control.Source);

        public bool DescriptorSelected =>
            Options.Descriptor && Descriptor != null && Descriptor.Include
            && IsIncluded(Descriptor.Source);

        /// <summary>
        /// Whether what was read from a prefab is being written. An item with no source recorded
        /// belongs to the avatar itself.
        /// </summary>
        public static bool IsIncluded(ConversionSource source) => source == null || source.Include;

        public int SelectedRigCount => Tally(SelectedRigs());
        public int SelectedConstraintCount => Tally(SelectedConstraints());
        public int SelectedArmatureLinkCount => Tally(SelectedArmatureLinks());
        public int SelectedVixxyControlCount => Tally(SelectedVixxyControls());
        public int SelectedAuthoredMotionCount => Tally(SelectedAuthoredMotions());

        private static int Tally<T>(IEnumerable<T> items)
        {
            int count = 0;
            foreach (T unused in items)
            {
                count++;
            }

            return count;
        }

        /// <summary>Everything reported about the avatar, whatever the options are set to.</summary>
        public IEnumerable<ConversionDiagnostic> AllDiagnostics() => DiagnosticsOf(false);

        /// <summary>
        /// What the conversion the options describe would report. A narrowed conversion should
        /// not be read alongside the losses of the parts it leaves out; what it leaves out is
        /// stated as such instead, by the window and by the report.
        /// </summary>
        public IEnumerable<ConversionDiagnostic> SelectedDiagnostics() => DiagnosticsOf(true);

        private IEnumerable<ConversionDiagnostic> DiagnosticsOf(bool selectedOnly)
        {
            foreach (ConversionDiagnostic diagnostic in Diagnostics)
            {
                yield return diagnostic;
            }

            if (!selectedOnly || Options.Toggles)
            {
                foreach (ConversionDiagnostic diagnostic in ToggleDiagnostics)
                {
                    yield return diagnostic;
                }
            }

            if (!selectedOnly || Options.Motion)
            {
                foreach (ConversionDiagnostic diagnostic in MotionDiagnostics)
                {
                    yield return diagnostic;
                }
            }

            foreach (PlannedJiggleRig rig in selectedOnly ? SelectedRigs() : Rigs)
            {
                foreach (ConversionDiagnostic diagnostic in rig.Plan.Diagnostics)
                {
                    yield return diagnostic;
                }
            }

            if (!selectedOnly || (Options.Physics && Options.Colliders))
            {
                foreach (PlannedJiggleCollider collider in Colliders)
                {
                    if (selectedOnly && !IsIncluded(collider.Source))
                    {
                        continue;
                    }

                    foreach (ConversionDiagnostic diagnostic in collider.Plan.Diagnostics)
                    {
                        yield return diagnostic;
                    }
                }
            }

            foreach (PlannedConstraint constraint in
                     selectedOnly ? SelectedConstraints() : Constraints)
            {
                foreach (ConversionDiagnostic diagnostic in constraint.Plan.Diagnostics)
                {
                    yield return diagnostic;
                }
            }

            if (Descriptor != null && (!selectedOnly || DescriptorSelected))
            {
                foreach (ConversionDiagnostic diagnostic in Descriptor.Plan.Diagnostics)
                {
                    yield return diagnostic;
                }
            }

            foreach (PlannedHeadChop chop in selectedOnly ? SelectedHeadChops() : HeadChops)
            {
                foreach (ConversionDiagnostic diagnostic in chop.Plan.Diagnostics)
                {
                    yield return diagnostic;
                }
            }


            foreach (ConversionDiagnostic diagnostic in RigDiagnostics)
            {
                yield return diagnostic;
            }
        }

        public int CountOf(DiagnosticSeverity severity)
        {
            int count = 0;
            foreach (ConversionDiagnostic diagnostic in AllDiagnostics())
            {
                if (diagnostic.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
