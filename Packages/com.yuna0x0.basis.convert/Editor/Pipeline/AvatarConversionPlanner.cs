using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Rig;
using yuna0x0.Basis.Convert.Sources;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Reads an avatar prefab, maps what it finds, and reports what a conversion would produce.
    /// Changes nothing.
    /// </summary>
    public static class AvatarConversionPlanner
    {
        /// <summary>Reads one prefab. What a conversion of a bare avatar reads.</summary>
        public static AvatarConversionPlan Plan(string prefabAssetPath,
            JiggleMappingProfile profile = null)
        {
            profile ??= JiggleMappingProfile.Default;

            AvatarConversionPlan plan = new AvatarConversionPlan { SourceAssetPath = prefabAssetPath };

            if (string.IsNullOrEmpty(prefabAssetPath)
                || !System.IO.File.Exists(prefabAssetPath))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "avatar.missing",
                    $"No prefab at {prefabAssetPath}.");
                return plan;
            }

            ConversionSource source = ConversionSource.ForAsset(prefabAssetPath);
            if (source == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "avatar.notLoaded",
                    $"{prefabAssetPath} did not load as a prefab.");
                return plan;
            }

            plan.Sources.Add(source);
            plan.SourceRoot = source.Root;

            HashSet<string> unknownIdentities = new HashSet<string>();
            ReadSource(plan, source, profile, unknownIdentities);
            ReportVariantSources(plan, plan.Sources);
            Finish(plan, unknownIdentities);
            return plan;
        }

        /// <summary>
        /// Reads a whole hierarchy, which is normally an avatar with clothing and accessories on
        /// it. Each prefab it is built from is read separately, because that is where each one's
        /// component data lives, and its results are placed where that prefab sits.
        /// </summary>
        public static AvatarConversionPlan Plan(GameObject hierarchyRoot,
            JiggleMappingProfile profile = null)
        {
            profile ??= JiggleMappingProfile.Default;

            AvatarConversionPlan plan = new AvatarConversionPlan();
            List<ConversionSource> sources = ConversionSourceDiscovery.Discover(hierarchyRoot);
            SceneOnlyComponents.Report(plan, hierarchyRoot, sources);

            if (sources.Count == 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "avatar.noPrefab",
                    "Nothing here is linked to a prefab, so there is no file to read the source "
                    + "data from.");
                return plan;
            }

            plan.Sources.AddRange(sources);
            plan.SourceAssetPath = sources[0].AssetPath;
            plan.SourceRoot = sources[0].Root;

            HashSet<string> unknownIdentities = new HashSet<string>();
            foreach (ConversionSource source in sources)
            {
                ReadSource(plan, source, profile, unknownIdentities);
            }

            if (sources.Count > 1)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.severalPrefabs",
                    $"This avatar is built from {sources.Count} prefabs. Each was read from its "
                    + "own file and its components placed where that prefab sits: "
                    + Names(sources) + ".");
            }

            ReportVariantSources(plan, sources);

            Finish(plan, unknownIdentities);
            return plan;
        }

        private static void Register(
            AvatarConversionPlan plan, string assetPath, List<UnityYamlDocument> documents)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            guid = guid.ToLowerInvariant();
            if (!plan.DocumentsByGuid.TryGetValue(guid, out Dictionary<long, UnityYamlDocument> byId))
            {
                byId = new Dictionary<long, UnityYamlDocument>();
                plan.DocumentsByGuid[guid] = byId;
            }

            foreach (UnityYamlDocument document in documents)
            {
                byId[document.FileId] = document;
            }
        }

        /// <summary>Names the prefabs a variant inherits from, since they were read too.</summary>
        private static void ReportVariantSources(
            AvatarConversionPlan plan, List<ConversionSource> sources)
        {
            if (plan.OverridesApplied > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.overridesApplied",
                    $"{plan.OverridesApplied} property overrides from prefab variants and nested "
                    + "prefab instances were applied to the prefabs they modify before reading.");
            }

            foreach (ConversionSource source in sources)
            {
                string basePath = source.BaseAssetPath();
                if (string.IsNullOrEmpty(basePath))
                {
                    continue;
                }

                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.prefabVariant",
                    $"{source.Name} is a variant of {System.IO.Path.GetFileNameWithoutExtension(basePath)} "
                    + $"({basePath}). A variant's own file holds only its overrides, so the base "
                    + "was read as well and what it carries was converted onto this variant's "
                    + "own objects.");
            }
        }

        /// <summary>
        /// The prefabs by name, capped. A gimmick pack nests the same prefab a dozen times, so
        /// the whole list is unreadable and mostly repetition.
        /// </summary>
        private static string Names(List<ConversionSource> sources)
        {
            const int limit = 6;
            List<string> names = new List<string>();

            foreach (ConversionSource source in sources)
            {
                if (!names.Contains(source.Name))
                {
                    names.Add(source.Name);
                }
            }

            // The count is of names left unlisted, not of prefabs: the same prefab nested a
            // dozen times is one name, and saying "and eleven more" would describe repetition
            // as variety.
            if (names.Count <= limit)
            {
                return string.Join(", ", names);
            }

            List<string> listed = names.GetRange(0, limit);
            return string.Join(", ", listed) + $" and {names.Count - limit} more";
        }

        /// <summary>
        /// Reads one prefab into a plan, tagging what it finds with where it came from. A
        /// variant's file holds only its overrides, so the prefabs above it are read too,
        /// resolved onto this one's objects so the rest of the pipeline sees no difference.
        /// </summary>
        private static void ReadSource(AvatarConversionPlan plan, ConversionSource source,
            JiggleMappingProfile profile, HashSet<string> unknownIdentities)
        {
            List<UnityYamlDocument> documents = UnityYamlScanner.ScanFile(source.AssetPath);
            ReportNotText(plan, source.AssetPath, documents);

            // A variant's file holds only overrides; the prefabs above it hold the data. Both
            // are scanned first so the overrides land on the inherited documents before any
            // reader sees them. Overrides in a prefab that nests this one arrived with that
            // prefab, which is read first, so they are already waiting.
            List<(string path, List<UnityYamlDocument> documents)> inheritedFiles =
                new List<(string, List<UnityYamlDocument>)>();
            foreach (string inherited in source.InheritedAssetPaths())
            {
                List<UnityYamlDocument> inheritedDocuments = UnityYamlScanner.ScanFile(inherited);
                ReportNotText(plan, inherited, inheritedDocuments);
                inheritedFiles.Add((inherited, inheritedDocuments));
            }

            Register(plan, source.AssetPath, documents);
            foreach ((string path, List<UnityYamlDocument> inheritedDocuments) in inheritedFiles)
            {
                Register(plan, path, inheritedDocuments);
            }

            for (int i = inheritedFiles.Count - 1; i >= 0; i--)
            {
                plan.Modifications.AddRange(PrefabOverrides.Read(inheritedFiles[i].documents));
            }

            plan.Modifications.AddRange(PrefabOverrides.Read(documents));
            plan.OverridesApplied += PrefabOverrides.Apply(plan.Modifications, plan.DocumentsByGuid);

            PrefabObjectResolver resolver =
                PrefabObjectResolver.Create(source.AssetPath, documents);

            if (resolver.Root == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "avatar.notLoaded",
                    $"{source.AssetPath} did not load as a prefab.");
                return;
            }

            if (documents.Count == 0)
            {
                // Nothing to scan means the file is not Unity YAML: an imported `.vrm` is binary
                // glTF behind a ScriptedImporter. Its components are real types rather than
                // missing scripts, since the importer that made them had to be installed, so
                // they are read through the object API instead.
                ReadComponents(plan, source, resolver);
            }
            else
            {
                ReadDocuments(plan, source, profile, unknownIdentities, documents, resolver);
            }

            foreach ((string inherited, List<UnityYamlDocument> inheritedDocuments) in inheritedFiles)
            {
                PrefabObjectResolver inheritedResolver =
                    PrefabObjectResolver.CreateForInherited(
                        source.AssetPath, inherited, inheritedDocuments);

                if (inheritedResolver == null || inheritedResolver.Root == null)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.inheritedUnreadable",
                        $"{source.Name} inherits from {inherited}, which could not be read, so "
                        + "anything it carries was not converted.");
                    continue;
                }

                plan.InheritedSourcesRead++;
                ReadDocuments(plan, source, profile, unknownIdentities,
                    inheritedDocuments, inheritedResolver);
            }

            string model = source.ModelAssetPath();
            if (model != null)
            {
                ReadModelComponents(plan, source, model);
            }
        }

        /// <summary>
        /// A prefab file that yields no text documents is stored in Unity's binary form. Nothing
        /// in it can be read, and a missing script keeps Unity from saving it as text in this
        /// project, so the fix lives where the scripts are installed. A model or an imported
        /// .vrm is binary by nature and is read another way, so only .prefab files are named.
        /// </summary>
        private static void ReportNotText(
            AvatarConversionPlan plan, string assetPath, List<UnityYamlDocument> documents)
        {
            if (documents.Count > 0
                || !assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.notText",
                $"{assetPath} is not a text file, so nothing in it can be read. Unity refuses to "
                + "save a prefab whose scripts are missing, so switch the project where its "
                + "scripts are installed to Force Text (Edit > Project Settings > Editor > Asset "
                + "Serialization), save the prefab there, and export it again.");
        }

        /// <summary>
        /// A prefab saved from an imported model without unpacking holds only its overrides, and
        /// the model holds the components. An FBX carries none, but a `.vrm` does, as live types,
        /// so they are read from the model and resolved onto this prefab's own objects, the way a
        /// variant's base is read as text.
        /// </summary>
        private static void ReadModelComponents(
            AvatarConversionPlan plan, ConversionSource source, string modelPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            PrefabObjectResolver resolver =
                PrefabObjectResolver.CreateForInherited(source.AssetPath, modelPath, null);

            if (model == null || resolver == null || resolver.Root == null)
            {
                return;
            }

            int before = plan.ComponentsRead;
            ReadComponents(plan, source, resolver, model);

            if (plan.ComponentsRead > before)
            {
                plan.InheritedSourcesRead++;
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.modelRead",
                    $"{source.Name} was saved from {System.IO.Path.GetFileName(modelPath)} "
                    + "without unpacking, so its components were read from that file and "
                    + "converted onto this prefab's own objects.");
            }
        }

        /// <summary>
        /// What a hierarchy's live components hold, for a source whose file cannot be read as
        /// text. Only VRM arrives this way today: an imported `.vrm` is binary, and UniVRM has to
        /// be installed for it to import at all.
        /// </summary>
        private static void ReadComponents(
            AvatarConversionPlan plan, ConversionSource source, PrefabObjectResolver resolver,
            GameObject readFrom = null)
        {
            VrmComponentReader.Result read = VrmComponentReader.Read(readFrom ?? resolver.Root);
            if (!read.Any)
            {
                return;
            }

            plan.ComponentsRead += read.ComponentsRead;

            AssembleVrmChains(read.Chains, read.Joints, read.Colliders, read.Groups,
                resolver, plan, source);

            foreach (VrmConstraintData constraint in read.Constraints)
            {
                plan.VrmConstraintsFound++;

                PlannedConstraint planned = PlanVrmConstraint(constraint, resolver, plan);
                if (planned != null)
                {
                    planned.Source = source;
                    plan.Constraints.Add(planned);
                }
            }

            if (read.Instance == null)
            {
                return;
            }

            AssembleVrmExpressions(
                VrmComponentReader.ReadExpressions10(read.Instance), plan, source);

            plan.VrmMeta ??= VrmComponentReader.ReadMeta10(read.Instance);
            ApplyVrmSettings(VrmComponentReader.ReadSettings10(read.Instance), resolver, plan);
        }

        /// <summary>One file's worth of documents, resolved against the objects they belong to.</summary>
        private static void ReadDocuments(AvatarConversionPlan plan, ConversionSource source,
            JiggleMappingProfile profile, HashSet<string> unknownIdentities,
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver)
        {
            Dictionary<long, PlannedJiggleCollider> colliders =
                MapColliders(documents, resolver, plan);

            foreach (PlannedJiggleCollider collider in colliders.Values)
            {
                collider.Source = source;
            }

            plan.ModularAvatarToggles.AddRange(
                ModularAvatarToggleResolver.Resolve(documents, resolver, source));

            // VRM chains are read in a pass of their own: a spring names joint components that
            // sit anywhere in the file, so they cannot be resolved as the documents go past.
            PlanVrmChains(documents, resolver, plan, source);
            PlanVrmExpressions(documents, resolver, plan, source);
            ReadVrmAvatarSettings(documents, resolver, plan);

            foreach (UnityYamlDocument document in documents)
            {
                // Stripped: defined in a prefab above this one, and read from that file.
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                SourceComponentKind kind = KnownScriptIdentities.Resolve(guid, scriptFileId);
                if (kind == SourceComponentKind.Unknown)
                {
                    unknownIdentities.Add($"{guid}:{scriptFileId}");
                    continue;
                }

                plan.ComponentsRead++;

                if (VrcConstraintDocumentReader.TryGetKind(kind, out VrcConstraintKind constraintKind))
                {
                    plan.ConstraintsFound++;
                    if (!document.IsEnabled)
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "constraint.disabled",
                            $"A {constraintKind} constraint was disabled, so it drove nothing. "
                            + "None was written.");
                        continue;
                    }

                    PlannedConstraint constraint =
                        PlanConstraint(document, constraintKind, resolver, plan);
                    if (constraint != null)
                    {
                        constraint.Source = source;
                        plan.Constraints.Add(constraint);
                    }

                    continue;
                }

                if (kind == SourceComponentKind.VrcContactReceiver
                    || kind == SourceComponentKind.VrcContactSender)
                {
                    plan.ContactsFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsVrmConstraint(kind))
                {
                    plan.VrmConstraintsFound++;

                    PlannedConstraint vrmConstraint =
                        PlanVrmConstraint(document, kind, resolver, plan);

                    if (vrmConstraint != null)
                    {
                        vrmConstraint.Source = source;
                        plan.Constraints.Add(vrmConstraint);
                    }

                    continue;
                }

                if (kind == SourceComponentKind.VrmLookAt)
                {
                    plan.VrmLookAtFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsHandledByModularAvatar(kind))
                {
                    plan.ModularAvatarHierarchyFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsModularAvatarMenuOrAnimator(kind)
                    || KnownScriptIdentities.IsModularAvatarReactive(kind))
                {
                    plan.ModularAvatarMenuFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsModularAvatarVrchatOnly(kind))
                {
                    plan.ModularAvatarVrchatOnlyFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsEditorOnlyAuthoringTool(kind))
                {
                    plan.EditorOnlyToolsFound++;
                    continue;
                }

                if (kind == SourceComponentKind.DynamicBone)
                {
                    plan.DynamicBonesFound++;
                    if (!document.IsEnabled)
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "dynamicbone.disabled",
                            "A Dynamic Bone was disabled, so it simulated nothing. No rig was "
                            + "written.");
                        continue;
                    }

                    PlanDynamicBone(document, resolver, colliders, profile, plan, source);
                    continue;
                }

                if (kind == SourceComponentKind.VrcHeadChop)
                {
                    if (!document.IsEnabled)
                    {
                        plan.HeadChopsFound++;
                        plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "headChop.disabled",
                            "A VRC Head Chop was disabled, so it hid nothing. None was written.");
                        continue;
                    }

                    PlannedHeadChop headChop = PlanHeadChop(document, resolver, plan);
                    if (headChop != null)
                    {
                        headChop.Source = source;
                        plan.HeadChops.Add(headChop);
                    }

                    continue;
                }

                if (kind == SourceComponentKind.VrcRaycast)
                {
                    plan.RaycastsFound++;
                    continue;
                }

                if (KnownScriptIdentities.IsVrcBuildSetting(kind))
                {
                    plan.VrcBuildSettingsFound++;
                    continue;
                }

                if (kind == SourceComponentKind.VrcAvatarDescriptor)
                {
                    // The avatar's own descriptor is the one that counts, and it is read first
                    // because the avatar's own prefab is the first source. Clothing is often
                    // shipped with a descriptor of its own for previewing, and reading that one
                    // would report visemes and a view position for something not being written.
                    if (plan.Descriptor == null)
                    {
                        PlannedAvatarDescriptor descriptor =
                            PlanDescriptor(document, resolver, plan);

                        if (descriptor != null)
                        {
                            descriptor.Source = source;
                            plan.Descriptor = descriptor;
                        }
                    }

                    continue;
                }

                if (kind != SourceComponentKind.VrcPhysBone)
                {
                    continue;
                }

                plan.PhysBonesFound++;
                if (!document.IsEnabled)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "physbone.disabled",
                        "A PhysBone was disabled, so it simulated nothing. No rig was written.");
                    continue;
                }

                PlannedJiggleRig rig =
                    PlanOne(document, resolver, colliders, profile, plan);
                if (rig == null)
                {
                    plan.Unresolved++;
                    continue;
                }

                rig.Source = source;
                plan.Rigs.Add(rig);
            }

        }

        /// <summary>
        /// A VRCHeadChop becomes a Basis head chop on the same object, naming the same bones.
        /// </summary>
        private static PlannedHeadChop PlanHeadChop(
            UnityYamlDocument document, PrefabObjectResolver resolver, AvatarConversionPlan plan)
        {
            plan.HeadChopsFound++;

            VrcHeadChopData data = VrcHeadChopReader.Read(document);
            BasisHeadChopPlan mapped = VrcHeadChopToBasisMapper.Map(data);

            if (!resolver.TryResolveTransform(data.OwnerGameObjectFileId, out Transform host))
            {
                plan.Unresolved++;
                return null;
            }

            PlannedHeadChop planned = new PlannedHeadChop
            {
                Plan = mapped,
                SourceHost = host,
            };

            // Basis always scales the humanoid Head itself and skips any head chop entry naming
            // it, so a factor of 1 on Head (how VRChat content keeps the head visible) is lost.
            Animator animator = host.GetComponentInParent<Animator>(true);
            Transform head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;

            foreach (HeadChopTargetPlan target in mapped.Targets)
            {
                if (resolver.TryResolveTransform(target.TransformFileId, out Transform bone))
                {
                    if (head != null && bone == head)
                    {
                        mapped.Diagnostics.Add(DiagnosticSeverity.Dropped, "headChop.head.ignored",
                            $"The head chop names the humanoid Head with scale {target.Scale}. "
                            + "Basis scales the Head itself in first person and ignores head chop "
                            + "entries for it, so this entry was dropped.");
                        continue;
                    }

                    planned.SourceTargets.Add(new PlannedHeadChopTarget
                    {
                        Transform = bone,
                        Scale = target.Scale,
                    });
                }
                else
                {
                    mapped.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "headChop.target.unresolved",
                        "A head chop bone could not be resolved and was dropped.");
                }
            }

            return planned;
        }

        /// <summary>What is worked out once, after every prefab has been read.</summary>
        private static void Finish(AvatarConversionPlan plan, HashSet<string> unknownIdentities)
        {
            if (plan.SourceRoot == null)
            {
                return;
            }

            if (plan.ModularAvatarHierarchyFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "modularAvatar.hierarchy",
                    $"{plan.ModularAvatarHierarchyFound} Modular Avatar components rearrange the "
                    + "hierarchy or the meshes: merged armatures, bone proxies, mesh settings and "
                    + "the like. They are left to Modular Avatar, which applies them at Basis "
                    + "build time when it and the Basis NDMF platform are installed. Blendshape "
                    + "Sync and Parameters have VRChat-only passes and do nothing on Basis.");
            }

            if (plan.ModularAvatarMenuFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "modularAvatar.menus",
                    $"{plan.ModularAvatarMenuFound} Modular Avatar components build menus, merge "
                    + "animator layers or react to them. All of those target structures VRChat "
                    + "has and Basis does not, so they do nothing there as they stand. A menu "
                    + "item read together with a merged animator or an object toggle is rebuilt "
                    + "as a Vixxy control; anything else is listed here and left.");
            }

            if (plan.ModularAvatarVrchatOnlyFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "modularAvatar.vrchatOnly",
                    $"{plan.ModularAvatarVrchatOnlyFound} Modular Avatar components act on "
                    + "VRChat's own systems: its colliders, its head chop, its MMD layers. There "
                    + "is nothing for them to act on under Basis.");
            }

            if (plan.EditorOnlyToolsFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.editorOnlyTool",
                    $"{plan.EditorOnlyToolsFound} components of an editor-time authoring tool "
                    + "were found. They carry no runtime behaviour, so there was nothing to "
                    + "convert and nothing was lost. The tool itself does not run under Basis.");
            }

            if (plan.VrmLookAtFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.lookAt",
                    $"{plan.VrmLookAtFound} VRM look at components say how the avatar aims its "
                    + "eyes. Basis drives gaze from the eye bones itself, so they are not "
                    + "converted. Where the eyes sit does carry across.");
            }

            if (plan.ContactsFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "contacts.dropped",
                    $"{plan.ContactsFound} VRChat contact senders and receivers were found. Basis "
                    + "has no contact system, so anything driven by touch does not come across.");
            }

            if (plan.RaycastsFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "raycast.dropped",
                    $"{plan.RaycastsFound} VRChat raycast components were found. They fire a ray "
                    + "and set animator parameters from what it hits. Basis has nothing that "
                    + "does this, so anything driven by them does not come across.");
            }

            if (plan.VrcBuildSettingsFound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrchat.buildSettings",
                    $"{plan.VrcBuildSettingsFound} VRChat build settings were found: per-platform "
                    + "overrides or impostor settings. They tell VRChat's uploader what to do "
                    + "and carry no behaviour, so there was nothing to convert and nothing lost.");
            }

            BuildProfile(plan);
            EnsureAvatarComponent(plan);
            ApplyVrmEyePosition(plan);
            ApplyVrmVisemes(plan);
            LoadExpressions(plan);

            // Clothing has no descriptor and no expression menu of its own, so this is not part
            // of reading one: what Modular Avatar installs stands on its own.
            BuildModularAvatarControls(plan);
            ReportOverlaps(plan);

            // Only meaningful once the descriptor is known: a prop has physics but no rig, and
            // asking it for a humanoid mapping would be noise.
            plan.RigDiagnostics = RigReadiness.Inspect(plan.SourceRoot, plan.Descriptor != null);

            // An unrecognised script is reported rather than skipped silently: VRChat ships its
            // components in DLLs, so a new SDK release can introduce identities this table has
            // never seen, and that is how the table grows.
            foreach (string identity in unknownIdentities)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.unknownScript",
                    $"A component with script identity {identity} was not recognised and was "
                    + "skipped.");
            }

            ReportUnpackNeeded(plan);
        }

        /// <summary>
        /// A prefab saved from an imported model without unpacking holds only its overrides,
        /// while the components stay in the model file, which is binary. It converts as though
        /// it were empty, so when nothing was found that is the likeliest reason and is worth
        /// naming: the alternative is a reader guessing at the generic causes instead.
        /// </summary>
        private static void ReportUnpackNeeded(AvatarConversionPlan plan)
        {
            // Components read, not things planned: a bare humanoid gets an empty Basis Avatar
            // whether or not anything was read, so counting that would hide exactly the case
            // this is for.
            if (plan.ComponentsRead > 0 || plan.Sources.Count == 0)
            {
                return;
            }

            string model = plan.Sources[0].ModelAssetPath();
            if (string.IsNullOrEmpty(model))
            {
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.notUnpacked",
                $"Nothing was found, and this prefab was saved from {model} without unpacking. "
                + "Its components are still inside that file, which is not read. Unpack the "
                + "prefab completely and save it again.");
        }

        /// <summary>
        /// Maps every collider in the file once. A collider referenced by many PhysBones is one
        /// mapping shared between them, so its diagnostics are reported once.
        /// </summary>
        private static Dictionary<long, PlannedJiggleCollider> MapColliders(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            AvatarConversionPlan plan)
        {
            Dictionary<long, PlannedJiggleCollider> colliders =
                new Dictionary<long, PlannedJiggleCollider>();

            foreach (UnityYamlDocument document in documents)
            {
                // Stripped: defined in a prefab above this one, and read from that file.
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                SourceComponentKind kind = KnownScriptIdentities.Resolve(guid, scriptFileId);
                JiggleColliderPlan colliderPlan;
                long transformFileId;

                switch (kind)
                {
                    case SourceComponentKind.VrcPhysBoneCollider:
                    {
                        PhysBoneColliderData data = PhysBoneDocumentReader.ReadCollider(document);
                        colliderPlan = PhysBoneColliderToJiggleMapper.Map(data);

                        // No Root Transform means the collider sits on its own object.
                        transformFileId = data.RootTransformFileId != 0L
                            ? data.RootTransformFileId
                            : data.OwnerGameObjectFileId;
                        break;
                    }

                    case SourceComponentKind.DynamicBoneCollider:
                    case SourceComponentKind.DynamicBonePlaneCollider:
                    {
                        DynamicBoneColliderData data = DynamicBoneDocumentReader.ReadCollider(
                            document, kind == SourceComponentKind.DynamicBonePlaneCollider);
                        colliderPlan = DynamicBoneColliderToJiggleMapper.Map(data);
                        transformFileId = data.OwnerGameObjectFileId;
                        break;
                    }

                    default:
                        continue;
                }

                plan.CollidersFound++;

                if (!document.IsEnabled)
                {
                    // Both sources skip a disabled collider, so rigs that list it get nothing.
                    colliders[document.FileId] = new PlannedJiggleCollider
                    {
                        Plan = colliderPlan,
                        Disabled = true,
                    };
                    plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "collider.disabled",
                        "A collider component was disabled, so nothing collided with it. It was "
                        + "left off the rigs that list it.");
                    continue;
                }

                if (!resolver.TryResolveTransform(transformFileId, out Transform transform))
                {
                    colliderPlan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "collider.transform.unresolved",
                        "This collider could not be tied to a transform, so rigs referencing it "
                        + "will not collide with it.");
                }

                PlannedJiggleCollider planned = new PlannedJiggleCollider
                {
                    Plan = colliderPlan,
                    SourceTransform = transform,
                };

                colliders[document.FileId] = planned;
                plan.Colliders.Add(planned);
            }

            return colliders;
        }

        /// <summary>
        /// One Dynamic Bone can drive several chains, so it produces one rig per root rather
        /// than one per component.
        /// </summary>
        /// <summary>
        /// Plans the jiggle rigs a VRM avatar's spring bones describe.
        /// <para>
        /// Both formats end up here. VRM 0.x names its chains on one component; VRM 1.0 puts a
        /// joint on each bone and lists which joints make up which chain on the avatar's own
        /// component, so the joints are gathered first and the chains resolved against them.
        /// </para>
        /// </summary>
        private static void PlanVrmChains(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            AvatarConversionPlan plan, ConversionSource source)
        {
            Dictionary<long, VrmSpringJointData> joints = new Dictionary<long, VrmSpringJointData>();
            Dictionary<long, VrmColliderData> colliders = new Dictionary<long, VrmColliderData>();
            Dictionary<long, VrmColliderGroupData> groups =
                new Dictionary<long, VrmColliderGroupData>();
            List<VrmSpringChainData> chains = new List<VrmSpringChainData>();

            foreach (UnityYamlDocument document in documents)
            {
                // Stripped: defined in a prefab above this one, and read from that file.
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                switch (KnownScriptIdentities.Resolve(guid, scriptFileId))
                {
                    case SourceComponentKind.Vrm10SpringBoneJoint:
                        joints[document.FileId] = VrmDocumentReader.ReadJoint(document);
                        break;
                    case SourceComponentKind.Vrm10SpringBoneCollider:
                        colliders[document.FileId] = VrmDocumentReader.ReadCollider(document);
                        break;
                    case SourceComponentKind.Vrm10SpringBoneColliderGroup:
                        groups[document.FileId] =
                            VrmDocumentReader.ReadColliderGroup(document, true);
                        break;
                    case SourceComponentKind.VrmSpringBoneColliderGroup:
                        groups[document.FileId] =
                            VrmDocumentReader.ReadColliderGroup(document, false);
                        break;
                    case SourceComponentKind.VrmSpringBone:
                        if (!document.IsEnabled)
                        {
                            plan.Diagnostics.Add(DiagnosticSeverity.Mapped,
                                "vrm.springBone.disabled",
                                "A VRMSpringBone was disabled, so it simulated nothing. No rig "
                                + "was written for its chains.");
                            continue;
                        }

                        chains.AddRange(VrmDocumentReader.ReadSpringBone0X(document));
                        break;
                    case SourceComponentKind.Vrm10Instance:
                        chains.AddRange(VrmDocumentReader.ReadInstanceSprings(document));
                        break;
                }
            }

            AssembleVrmChains(chains, joints, colliders, groups, resolver, plan, source);
        }

        /// <summary>
        /// Turns read spring data into jiggle rigs. Shared by both readers: the text one and the
        /// component one produce the same data, so only the reading differs.
        /// </summary>
        private static void AssembleVrmChains(
            List<VrmSpringChainData> chains,
            Dictionary<long, VrmSpringJointData> joints,
            Dictionary<long, VrmColliderData> colliders,
            Dictionary<long, VrmColliderGroupData> groups,
            PrefabObjectResolver resolver, AvatarConversionPlan plan, ConversionSource source)
        {
            if (chains.Count == 0)
            {
                return;
            }

            Dictionary<long, PlannedJiggleCollider> mapped =
                new Dictionary<long, PlannedJiggleCollider>();

            foreach (VrmSpringChainData chain in chains)
            {
                ResolveVrmJoints(chain, joints);
                plan.VrmChainsFound++;

                if (chain.Joints.Count == 0
                    || !resolver.TryResolveTransform(chain.RootTransformFileId,
                        out Transform rootBone))
                {
                    plan.Unresolved++;
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.unresolved",
                        $"A VRM spring chain{Named(chain)} could not be tied to a bone and was "
                        + "skipped.");
                    continue;
                }

                JiggleRigPlan rigPlan = VrmSpringBoneToJiggleMapper.Map(chain);
                rigPlan.Preset = JigglePresetLibrary.GuessFrom(
                    string.IsNullOrEmpty(chain.Name) ? rootBone.name : chain.Name);

                PlannedJiggleRig planned = new PlannedJiggleRig
                {
                    Plan = rigPlan,
                    SourceHost = rootBone,
                    SourceRootBone = rootBone,
                    Source = source,
                };

                ExcludeBonesOutsideTheChain(chain, joints, resolver, rootBone, rigPlan, planned);
                AttachVrmColliders(chain, groups, colliders, mapped, resolver, plan, planned,
                    source);

                plan.Rigs.Add(planned);
            }
        }

        /// <summary>
        /// Reads what a VRM avatar says about its own eyes, and holds it for the Basis Avatar
        /// component. VRM measures the eyes as an offset from the head bone, and Basis stores
        /// the same point relative to the avatar root.
        /// </summary>
        private static void ReadVrmAvatarSettings(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            AvatarConversionPlan plan)
        {
            VrmAvatarSettingsData settings = null;

            foreach (UnityYamlDocument document in documents)
            {
                // Stripped: defined in a prefab above this one, and read from that file.
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                switch (KnownScriptIdentities.Resolve(guid, scriptFileId))
                {
                    case SourceComponentKind.VrmFirstPerson:
                        settings ??= VrmObjectReader.ReadVrm0Settings(document);
                        break;
                    case SourceComponentKind.Vrm10Instance:
                        // Held inside the .vrm means there is no text to follow, so the object
                        // asset is read through the live component instead.
                        settings ??= VrmObjectReader.IsUnreadableSource(document)
                                     && resolver.TryResolve(document, out Object live)
                                     && live is Component instance
                            ? VrmComponentReader.ReadSettings10(instance)
                            : VrmObjectReader.ReadVrm10Settings(document);

                        plan.VrmMeta ??= VrmObjectReader.ReadVrm10Meta(document);
                        break;
                    case SourceComponentKind.VrmMeta:
                        plan.VrmMeta ??= VrmObjectReader.ReadVrm0Meta(document);
                        break;
                }
            }

            ApplyVrmSettings(settings, resolver, plan);
        }

        /// <summary>What a VRM avatar's own settings mean for the conversion, from either reader.</summary>
        private static void ApplyVrmSettings(
            VrmAvatarSettingsData settings, PrefabObjectResolver resolver,
            AvatarConversionPlan plan)
        {
            ReportVrmLicence(plan);

            if (settings == null)
            {
                return;
            }

            plan.VrmSettings = settings;

            if (settings.HeadBoneFileId != 0L
                && resolver.TryResolveTransform(settings.HeadBoneFileId, out Transform origin))
            {
                plan.VrmEyeOrigin = origin;
            }

            // Basis's eye driver turns every avatar's eyes up to the same angle, 25 degrees, and
            // counter-rotates them while the head turns; nothing on the avatar lowers it. A model
            // built for VRM's usual 10 shows the white of the eye well before 25.
            const float basisEyeLimitDegrees = 25f;
            if (!settings.LookAtByExpression && settings.EyeRotationLimitDegrees > 0f
                && settings.EyeRotationLimitDegrees < basisEyeLimitDegrees)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.lookAt.range",
                    $"The avatar limits its eye bones to {settings.EyeRotationLimitDegrees:0.#} "
                    + $"degrees. Basis turns eyes up to {basisEyeLimitDegrees:0} degrees and has "
                    + "no setting on the avatar to lower it, so on a large eye the white shows "
                    + "when the eyes track past the model's limit.");
            }

            if (settings.LookAtByExpression)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.lookAt.expression",
                    "The avatar aims its eyes with its look up, down, left and right expressions "
                    + "rather than with eye bones. Basis rotates the eye bones, so gaze does not "
                    + "move these eyes.");
            }

            if (settings.ThirdPersonOnlyRenderers > 0 || settings.FirstPersonOnlyRenderers > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.firstPerson",
                    $"{settings.ThirdPersonOnlyRenderers} renderers are marked to hide from the "
                    + $"wearer and {settings.FirstPersonOnlyRenderers} to show only to them. "
                    + "Basis hides the head bone and everything under it in first person, which "
                    + "covers the usual case. If something still blocks the camera, add a Basis "
                    + "Head Chop naming it.");
            }
        }

        /// <summary>
        /// Says what the avatar's licence allows, before anything is written.
        /// <para>
        /// Every VRM states who may wear it and what may be done to it, and converting one is a
        /// modification. Nothing here blocks a conversion: the licence is the wearer's to judge,
        /// and this makes sure they have seen it.
        /// </para>
        /// </summary>
        private static void ReportVrmLicence(AvatarConversionPlan plan)
        {
            VrmMetaData meta = plan.VrmMeta;
            if (meta == null || !meta.HasAnything)
            {
                return;
            }

            string url = string.IsNullOrEmpty(meta.LicenseUrl)
                ? string.Empty
                : $" {meta.LicenseUrl}";

            if (meta.ForbidsModification
                || meta.AvatarPermission == VrmAvatarPermission.OnlyAuthor)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.licence.restricted",
                    meta.Summarise() + " Converting an avatar changes it, and using it on Basis "
                    + "is a use. Check you are allowed to before you convert." + url);
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.licence",
                meta.Summarise() + " Converting an avatar changes it, so read the licence before "
                + "you rely on the result." + url);
        }

        /// <summary>
        /// Turns a VRM eye offset into the avatar's eye position. VRM measures from the head
        /// bone, and Basis stores the height and depth of the same point relative to the avatar
        /// root, the same point a VRChat view position holds.
        /// </summary>
        private static void ApplyVrmEyePosition(AvatarConversionPlan plan)
        {
            VrmAvatarSettingsData settings = plan.VrmSettings;
            if (settings == null || !settings.HasEyeOffset || plan.SourceRoot == null)
            {
                return;
            }

            // VRM 0.x names the bone its offset is measured from, and it need not be the head.
            // VRM 1.0 always means the head, and says so by naming none.
            Animator animator = plan.SourceRoot.GetComponentInChildren<Animator>(true);
            Transform head = plan.VrmEyeOrigin;

            if (head == null && animator != null && animator.avatar != null
                && animator.avatar.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // The offset is measured from the head, and it goes on the Basis Avatar component.
            // Without a humanoid rig there is neither, so the eye position is left for Basis.
            if (head == null || plan.Descriptor == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.eyePosition.noRig",
                    "The avatar says where its eyes sit relative to the head bone, but the rig "
                    + "is not humanoid with a head mapped, so there was nothing to measure from "
                    + "and nothing to write it to.");
                return;
            }

            Vector2 eyes = EyePositionFrom(
                plan.SourceRoot.transform, head, settings.EyeOffsetFromHead);

            plan.Descriptor.Plan.EyePosition = eyes;
            plan.Descriptor.Plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.eyePosition",
                $"The eyes sit {settings.EyeOffsetFromHead} from the head bone, which is "
                + $"{eyes.x:0.###} up and {eyes.y:0.###} forward of the avatar root. That is "
                + "what Basis stores as the eye position.");
        }

        /// <summary>
        /// Fills the Basis Avatar's viseme and blink slots from a VRM's own expressions.
        /// <para>
        /// Basis takes fifteen visemes and VRM names five vowels, so five are filled and the ten
        /// consonants are left unset: the mouth opens on vowels and stays still on the rest,
        /// which is what the avatar itself could do. A viseme slot holds one blendshape, so an
        /// expression that drives several is reported rather than approximated by one of them.
        /// </para>
        /// <para>
        /// Basis holds one mesh for all fifteen and one for blink. An avatar that spreads its
        /// vowels over several renderers gets the one that carries the most of them, and the
        /// rest are reported as left unset.
        /// </para>
        /// </summary>
        private static void ApplyVrmVisemes(AvatarConversionPlan plan)
        {
            if (plan.Descriptor == null || plan.VrmExpressions.Count == 0
                || plan.SourceRoot == null)
            {
                return;
            }

            BasisAvatarPlan descriptor = plan.Descriptor.Plan;

            // Already filled from a VRChat descriptor: this avatar carries both, and the
            // descriptor is the one that names all fifteen.
            if (descriptor.VisemeBlendShapeNames.Count > 0)
            {
                return;
            }

            Dictionary<SkinnedMeshRenderer, Dictionary<int, string>> byRenderer =
                new Dictionary<SkinnedMeshRenderer, Dictionary<int, string>>();

            List<VrmMorphBinding> blink = null;
            int compound = 0;

            foreach (VrmExpressionData expression in plan.VrmExpressions)
            {
                bool viseme = VrmExpressionToVisemeMapper.TryGetSlot(expression, out int slot);
                if (!viseme && !VrmExpressionToVisemeMapper.IsBlink(expression))
                {
                    continue;
                }

                // Basis blinks with any number of shapes on one mesh, so blink keeps all of
                // its bindings. A viseme slot holds one shape.
                if (!viseme)
                {
                    blink ??= expression.Bindings.FindAll(
                        candidate => !string.IsNullOrEmpty(candidate.ShapeName));
                    continue;
                }

                VrmMorphBinding binding = VrmExpressionToVisemeMapper.SingleBinding(expression);
                if (binding == null)
                {
                    compound++;
                    continue;
                }

                SkinnedMeshRenderer renderer = RendererFor(plan, binding);
                if (renderer == null)
                {
                    continue;
                }

                if (!byRenderer.TryGetValue(renderer, out Dictionary<int, string> slots))
                {
                    slots = new Dictionary<int, string>();
                    byRenderer[renderer] = slots;
                }

                slots[slot] = binding.ShapeName;
            }

            int filled = ApplyVowels(plan, descriptor, byRenderer);
            ApplyBlink(plan, descriptor, blink);

            if (compound > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.visemeCompound",
                    $"{compound} of the avatar's vowel expressions move more than one "
                    + "blendshape at once. A Basis viseme names a single shape, so these were "
                    + "left unset rather than reduced to one of the shapes they move.");
            }

            if (filled == 0 && descriptor.BlinkBlendShapeIndices.Count == 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "descriptor.visemesUnset",
                    "Nothing named this avatar's visemes or blink, so they are unset on the "
                    + "Basis Avatar component and have to be assigned by hand.");
            }
        }

        /// <summary>
        /// The vowels of whichever renderer carries most of them. Basis holds one mesh for all
        /// fifteen visemes, so the rest cannot be written even when they were read.
        /// </summary>
        private static int ApplyVowels(AvatarConversionPlan plan, BasisAvatarPlan descriptor,
            Dictionary<SkinnedMeshRenderer, Dictionary<int, string>> byRenderer)
        {
            SkinnedMeshRenderer best = null;
            int bestCount = 0;
            int total = 0;

            foreach (KeyValuePair<SkinnedMeshRenderer, Dictionary<int, string>> pair in byRenderer)
            {
                total += pair.Value.Count;
                if (pair.Value.Count > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value.Count;
                }
            }

            if (best == null)
            {
                return 0;
            }

            for (int i = 0; i < VrmExpressionToVisemeMapper.VisemeCount; i++)
            {
                descriptor.VisemeBlendShapeNames.Add(
                    byRenderer[best].TryGetValue(i, out string name) ? name : string.Empty);
            }

            plan.Descriptor.SourceVisemeMesh = best;

            plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.visemes",
                $"{bestCount} of the fifteen visemes were filled from the avatar's own vowel "
                + $"expressions, on {best.name}. VRM names the five vowels and no consonants, so "
                + "the rest are unset: the mouth moves on vowels and holds still on the "
                + "consonants.");

            if (total > bestCount)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.visemeMeshSplit",
                    $"{total - bestCount} vowel expressions drive a renderer other than "
                    + $"{best.name}. Basis reads all fifteen visemes from one mesh, so those "
                    + "were left unset.");
            }

            return bestCount;
        }

        /// <summary>
        /// Every shape the blink expression moves, on the renderer the first one names. Basis
        /// reads all of its blink shapes from one mesh, so shapes on another renderer are left.
        /// </summary>
        private static void ApplyBlink(AvatarConversionPlan plan, BasisAvatarPlan descriptor,
            List<VrmMorphBinding> blink)
        {
            if (blink == null || blink.Count == 0)
            {
                return;
            }

            SkinnedMeshRenderer renderer = RendererFor(plan, blink[0]);
            if (renderer == null)
            {
                return;
            }

            List<string> names = new List<string>();
            int elsewhere = 0;
            foreach (VrmMorphBinding binding in blink)
            {
                if (RendererFor(plan, binding) != renderer)
                {
                    elsewhere++;
                    continue;
                }

                descriptor.BlinkBlendShapeIndices.Add(binding.Index);
                names.Add(binding.ShapeName);
            }

            plan.Descriptor.SourceBlinkMesh = renderer;

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.blink",
                "Blink was taken from the avatar's own blink expression: "
                + $"{string.Join(", ", names)} on {renderer.name}."
                + (elsewhere > 0
                    ? $" {elsewhere} more shapes sit on another renderer, which Basis cannot blink."
                    : string.Empty));
        }

        /// <summary>The renderer a binding names, relative to the prefab it was read from.</summary>
        private static SkinnedMeshRenderer RendererFor(
            AvatarConversionPlan plan, VrmMorphBinding binding)
        {
            Transform root = plan.SourceRoot.transform;
            Transform at = string.IsNullOrEmpty(binding.Path) ? root : root.Find(binding.Path);
            return at == null ? null : at.GetComponent<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// The height and depth of a point offset from the head bone, measured in the avatar
        /// root's space. VRM states the eyes that way and Basis stores them this way.
        /// </summary>
        public static Vector2 EyePositionFrom(Transform root, Transform head, Vector3 offset)
        {
            Vector3 local = root.InverseTransformPoint(head.TransformPoint(offset));
            return new Vector2(local.y, local.z);
        }

        /// <summary>
        /// Rebuilds a VRM avatar's expressions as Vixxy controls.
        /// <para>
        /// An expression is a named set of blendshape weights, and a Vixxy control with two
        /// choices holds the same. VRM names a blendshape by its position in the mesh, so each
        /// binding is resolved against the renderer it names before anything is mapped.
        /// </para>
        /// </summary>
        private static void PlanVrmExpressions(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            AvatarConversionPlan plan, ConversionSource source)
        {
            if (plan.SourceRoot == null)
            {
                return;
            }

            List<VrmExpressionData> expressions = new List<VrmExpressionData>();

            foreach (UnityYamlDocument document in documents)
            {
                // Stripped: defined in a prefab above this one, and read from that file.
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                switch (KnownScriptIdentities.Resolve(guid, scriptFileId))
                {
                    case SourceComponentKind.VrmBlendShapeProxy:
                        expressions.AddRange(VrmObjectReader.ReadVrm0(document));
                        break;
                    case SourceComponentKind.Vrm10Instance:
                        expressions.AddRange(VrmObjectReader.ReadVrm10(document));

                        // A .vrm keeps its expressions, licence and look at inside the binary
                        // file, where there is no YAML to read. The component itself is live, so
                        // the object asset it points at is read through it instead.
                        if (VrmObjectReader.IsUnreadableSource(document))
                        {
                            ReadVrm10ObjectFromComponent(document, resolver, plan, expressions);
                        }

                        break;
                }
            }

            AssembleVrmExpressions(expressions, plan, source);
        }

        /// <summary>
        /// Reads what a VRM 1.0 avatar keeps in its object asset through the live component,
        /// for a prefab whose instance still points into the `.vrm` file. Everything there is a
        /// sub-asset of a binary file, so there is no text to follow.
        /// </summary>
        private static void ReadVrm10ObjectFromComponent(
            UnityYamlDocument document, PrefabObjectResolver resolver, AvatarConversionPlan plan,
            List<VrmExpressionData> expressions)
        {
            if (!resolver.TryResolve(document, out Object resolved)
                || !(resolved is Component instance))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.objectUnreadable",
                    "This avatar's expressions, licence and eye offset are held inside the .vrm "
                    + "file itself rather than as assets in the project, so none of them could "
                    + "be read. In the .vrm's import settings, press \"Extract Meta And "
                    + "Expressions\", then convert again. The spring bones are not affected.");
                return;
            }

            expressions.AddRange(VrmComponentReader.ReadExpressions10(instance));
            plan.VrmMeta ??= VrmComponentReader.ReadMeta10(instance);
        }

        /// <summary>
        /// Turns read expressions into Vixxy controls. Shared by both readers, which produce the
        /// same data whether the avatar arrived as text or as an imported file.
        /// </summary>
        private static void AssembleVrmExpressions(
            List<VrmExpressionData> expressions, AvatarConversionPlan plan, ConversionSource source)
        {
            if (expressions.Count == 0 || plan.SourceRoot == null)
            {
                return;
            }

            Transform root = source?.Root != null
                ? source.Root.transform
                : plan.SourceRoot.transform;

            List<VrmExpressionData> choices = new List<VrmExpressionData>();
            int driven = 0;
            Dictionary<string, List<VrmMaterialHost>> hosts = MaterialHosts(root);

            foreach (VrmExpressionData expression in expressions)
            {
                plan.VrmExpressionsFound++;
                NameBlendShapes(expression, root);
                plan.VrmExpressions.Add(expression);

                if (VrmExpressionToVixxyMapper.IsMenuWorthy(expression, hosts))
                {
                    choices.Add(expression);
                }
                else if ((expression.Role == VrmExpressionRole.Custom
                          || expression.Role == VrmExpressionRole.Emotion)
                         && expression.MaterialBindingCount > 0)
                {
                    plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped,
                        "vrm.expression.materials",
                        $"'{expression.Name}' changes only materials no renderer on this avatar "
                        + "uses, so nothing was written for it.");
                }
                else if (expression.Role != VrmExpressionRole.Neutral
                         && expression.Role != VrmExpressionRole.Custom
                         && expression.Role != VrmExpressionRole.Emotion)
                {
                    driven++;
                }
            }

            if (choices.Count > 0)
            {
                VixxyControlPlan control = VrmExpressionToVixxyMapper.MapSelector(choices, hosts);
                foreach (ConversionDiagnostic diagnostic in control.Diagnostics)
                {
                    plan.ToggleDiagnostics.Add(diagnostic);
                }

                PlannedVixxyControl planned = new PlannedVixxyControl { Plan = control };
                if (control.Subjects.Count > 0 && ResolveSubjects(plan, control, planned, root))
                {
                    planned.Source = source;
                    plan.VixxyControls.Add(planned);
                    plan.ToggleDiagnostics.Add(DiagnosticSeverity.Mapped, "vrm.expressionsRebuilt",
                        $"{choices.Count} VRM expressions became one Expression selector: Neutral "
                        + "and one choice each. VRM has no menu, so the application playing the "
                        + "avatar drove these; on Basis the wearer picks one.");
                }
            }

            if (driven > 0)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "vrm.expressionsDriven",
                    $"{driven} expressions are ones Basis drives itself: the lip sync shapes, "
                    + "blinking and looking around. They were left for it rather than turned "
                    + "into menu controls the wearer would have to hold down.");
            }
        }

        /// <summary>
        /// Every renderer under the root, by the name of each material it uses. VRM names the
        /// material an expression changes; Vixxy changes a renderer's properties.
        /// </summary>
        private static Dictionary<string, List<VrmMaterialHost>> MaterialHosts(Transform root)
        {
            Dictionary<string, List<VrmMaterialHost>> hosts =
                new Dictionary<string, List<VrmMaterialHost>>();

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, root);
                Material[] materials = renderer.sharedMaterials;
                HashSet<string> seen = new HashSet<string>();
                foreach (Material material in materials)
                {
                    if (material == null || !seen.Add(material.name))
                    {
                        continue;
                    }

                    int others = 0;
                    foreach (Material other in materials)
                    {
                        if (other != null && other.name != material.name) others++;
                    }

                    if (!hosts.TryGetValue(material.name, out List<VrmMaterialHost> list))
                    {
                        list = new List<VrmMaterialHost>();
                        hosts[material.name] = list;
                    }

                    list.Add(new VrmMaterialHost
                    {
                        Path = path,
                        RendererTypeName = renderer.GetType().FullName,
                        OtherMaterials = others,
                    });
                }
            }

            return hosts;
        }

        /// <summary>
        /// Fills in each binding's blendshape name. VRM stores the index of a shape within its
        /// mesh, and Vixxy sets shapes by name, so the mesh is what translates between them.
        /// </summary>
        private static void NameBlendShapes(VrmExpressionData expression, Transform root)
        {
            foreach (VrmMorphBinding binding in expression.Bindings)
            {
                Transform at = string.IsNullOrEmpty(binding.Path)
                    ? root
                    : root.Find(binding.Path);

                SkinnedMeshRenderer renderer =
                    at == null ? null : at.GetComponent<SkinnedMeshRenderer>();

                Mesh mesh = renderer == null ? null : renderer.sharedMesh;
                if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount)
                {
                    continue;
                }

                binding.ShapeName = mesh.GetBlendShapeName(binding.Index);
            }
        }

        private static string Named(VrmSpringChainData chain) =>
            string.IsNullOrEmpty(chain.Name) ? string.Empty : $" named '{chain.Name}'";

        /// <summary>
        /// Turns a VRM 1.0 spring's joint references into the joints themselves, and takes the
        /// first as the bone the chain hangs from.
        /// </summary>
        private static void ResolveVrmJoints(
            VrmSpringChainData chain, IReadOnlyDictionary<long, VrmSpringJointData> joints)
        {
            if (!chain.IsVrm10)
            {
                return;
            }

            foreach (long jointFileId in chain.JointComponentFileIds)
            {
                if (joints.TryGetValue(jointFileId, out VrmSpringJointData joint))
                {
                    chain.Joints.Add(joint);
                }
            }

            if (chain.Joints.Count > 0)
            {
                chain.RootTransformFileId = chain.Joints[0].OwnerGameObjectFileId;
            }
        }

        /// <summary>
        /// A VRM 1.0 spring names the bones it moves. Jiggle simulates everything under the
        /// root, so a bone hanging off the chain that the spring never named would start moving
        /// where VRM left it still. Those are excluded rather than left to swing.
        /// </summary>
        private static void ExcludeBonesOutsideTheChain(
            VrmSpringChainData chain, IReadOnlyDictionary<long, VrmSpringJointData> joints,
            PrefabObjectResolver resolver, Transform rootBone, JiggleRigPlan rigPlan,
            PlannedJiggleRig planned)
        {
            if (!chain.IsVrm10)
            {
                return;
            }

            HashSet<Transform> inChain = new HashSet<Transform>();
            foreach (VrmSpringJointData joint in chain.Joints)
            {
                if (resolver.TryResolveTransform(joint.OwnerGameObjectFileId, out Transform bone))
                {
                    inChain.Add(bone);
                }
            }

            int excluded = 0;
            foreach (Transform candidate in rootBone.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == rootBone || inChain.Contains(candidate)
                    || candidate.parent == null || !inChain.Contains(candidate.parent))
                {
                    continue;
                }

                planned.SourceExcludedTransforms.Add(candidate);
                excluded++;
            }

            if (excluded > 0)
            {
                rigPlan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.branchesExcluded",
                    $"{excluded} bones hang off this chain that the spring did not name. A "
                    + "jiggle rig simulates everything under its root, so they were excluded to "
                    + "leave them as still as VRM did.");
            }
        }

        /// <summary>
        /// Attaches the colliders a chain's groups name. VRM 1.0 groups reference collider
        /// components; 0.x groups hold their spheres inline, so both are turned into the same
        /// shared collider list the rest of the converter uses.
        /// </summary>
        private static void AttachVrmColliders(
            VrmSpringChainData chain, IReadOnlyDictionary<long, VrmColliderGroupData> groups,
            IReadOnlyDictionary<long, VrmColliderData> colliders,
            Dictionary<long, PlannedJiggleCollider> mapped, PrefabObjectResolver resolver,
            AvatarConversionPlan plan, PlannedJiggleRig planned, ConversionSource source)
        {
            foreach (long groupFileId in chain.ColliderGroupFileIds)
            {
                if (!groups.TryGetValue(groupFileId, out VrmColliderGroupData group))
                {
                    planned.Plan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "physics.collider.unresolved",
                        "A referenced collider group was not found in the file and was dropped.");
                    continue;
                }

                foreach (long colliderFileId in group.ColliderFileIds)
                {
                    if (colliders.TryGetValue(colliderFileId, out VrmColliderData collider))
                    {
                        Attach(colliderFileId, collider);
                    }
                }

                // A 0.x group holds its spheres rather than referencing them, so they are keyed
                // by where they sit in the group.
                for (int i = 0; i < group.InlineColliders.Count; i++)
                {
                    Attach(unchecked((groupFileId * 397) + i + 1), group.InlineColliders[i]);
                }
            }

            void Attach(long key, VrmColliderData collider)
            {
                if (!mapped.TryGetValue(key, out PlannedJiggleCollider entry))
                {
                    entry = new PlannedJiggleCollider
                    {
                        Plan = VrmColliderToJiggleMapper.Map(collider),
                        Source = source,
                    };

                    if (resolver.TryResolveTransform(collider.OwnerGameObjectFileId,
                            out Transform on))
                    {
                        entry.SourceTransform = on;
                    }

                    mapped[key] = entry;
                    plan.Colliders.Add(entry);
                    plan.CollidersFound++;
                }

                planned.Colliders.Add(entry);
            }
        }

        private static void PlanDynamicBone(
            UnityYamlDocument document, PrefabObjectResolver resolver,
            IReadOnlyDictionary<long, PlannedJiggleCollider> colliders,
            JiggleMappingProfile profile, AvatarConversionPlan plan, ConversionSource source)
        {
            DynamicBoneData bone = DynamicBoneDocumentReader.ReadBone(document);

            if (!resolver.TryResolveTransform(bone.OwnerGameObjectFileId, out Transform host))
            {
                plan.Unresolved++;
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "dynamicbone.unresolved",
                    $"A Dynamic Bone at &{document.FileId} could not be tied to a transform and "
                    + "was skipped.");
                return;
            }

            List<JiggleRigPlan> rigPlans = DynamicBoneToJiggleMapper.Map(bone, profile);
            if (rigPlans.Count == 0)
            {
                string reason = Mathf.Approximately(bone.BlendWeight, 0f)
                    ? "has Blend Weight 0, so it simulates nothing"
                    : "names no root, so it simulates nothing";
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "dynamicbone.noRoot",
                    $"The Dynamic Bone on {host.name} {reason}. No rig was written.");
                return;
            }

            foreach (JiggleRigPlan rigPlan in rigPlans)
            {
                if (!resolver.TryResolveTransform(rigPlan.RootBoneFileId, out Transform rootBone))
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "dynamicbone.rootUnresolved",
                        $"A root of the Dynamic Bone on {host.name} could not be resolved. That "
                        + "chain was skipped.");
                    continue;
                }

                rigPlan.Preset = JigglePresetLibrary.GuessFrom(rootBone.name);

                PlannedJiggleRig planned = new PlannedJiggleRig
                {
                    Plan = rigPlan,
                    SourceHost = host,
                    SourceRootBone = rootBone,
                };

                AttachExclusionsAndColliders(rigPlan, planned, resolver, colliders);
                planned.Source = source;
                plan.Rigs.Add(planned);
            }
        }

        /// <summary>
        /// Shared by both physics sources: their exclusion and collider lists are the same shape
        /// once mapped.
        /// </summary>
        private static void AttachExclusionsAndColliders(
            JiggleRigPlan rigPlan, PlannedJiggleRig planned, PrefabObjectResolver resolver,
            IReadOnlyDictionary<long, PlannedJiggleCollider> colliders)
        {
            foreach (long excludedFileId in rigPlan.ExcludedTransformFileIds)
            {
                if (resolver.TryResolveTransform(excludedFileId, out Transform excluded))
                {
                    // Jiggle's bone cache skips an excluded root and then looks it up, which
                    // throws in the editor and aborts the conversion. The source ignored the
                    // root's own motion; the rig's motionless root is the nearest thing.
                    if (excluded == planned.SourceRootBone)
                    {
                        rigPlan.ExcludeRoot = true;
                        rigPlan.Diagnostics.Add(DiagnosticSeverity.Approximated,
                            "physics.excludedRoot",
                            "The root bone was in its own ignore list. The rig keeps the root "
                            + "motionless instead of excluding it.");
                        continue;
                    }

                    planned.SourceExcludedTransforms.Add(excluded);
                }
                else
                {
                    rigPlan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "physics.excludedTransform.unresolved",
                        "An excluded transform could not be resolved and was dropped.");
                }
            }

            foreach (long colliderFileId in rigPlan.ColliderSourceFileIds)
            {
                if (!colliders.TryGetValue(colliderFileId, out PlannedJiggleCollider collider))
                {
                    rigPlan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "physics.collider.unresolved",
                        "A referenced collider was not found in the file and was dropped.");
                    continue;
                }

                if (collider.SourceTransform != null && !collider.Disabled)
                {
                    planned.Colliders.Add(collider);
                }
            }

            if (planned.Colliders.Count > JiggleRigDataLimits.MaxColliders)
            {
                rigPlan.Diagnostics.Add(DiagnosticSeverity.Warning, "collider.limit",
                    $"{planned.Colliders.Count} colliders were referenced but a jiggle rig "
                    + $"supports {JiggleRigDataLimits.MaxColliders}. The extras were dropped.");
            }
        }

        private static void BuildProfile(AvatarConversionPlan plan)
        {
            Animator animator = plan.SourceRoot == null
                ? null
                : plan.SourceRoot.GetComponentInChildren<Animator>(true);

            plan.Profile = new SourceProfile
            {
                HasVrchatDescriptor = plan.Descriptor != null,
                HasVrchatComponents = plan.PhysBonesFound > 0 || plan.ConstraintsFound > 0
                    || plan.ContactsFound > 0,
                HasDynamicBone = plan.DynamicBonesFound > 0,
                HasVrmSpringBones = plan.VrmChainsFound > 0,
                HasHumanoidRig = animator != null && animator.avatar != null
                    && animator.avatar.isHuman,
            };
        }

        /// <summary>
        /// An avatar that never came from VRChat has no descriptor, but if it has a humanoid rig
        /// it still needs a Basis Avatar component to be usable. Dynamic Bone in particular is an
        /// ordinary Unity asset that plenty of avatars use without VRChat ever being involved.
        /// <para>
        /// Nothing is known about visemes or blink in that case, so the component is created
        /// empty and Basis fills what it can when its inspector is first opened.
        /// </para>
        /// </summary>
        private static void EnsureAvatarComponent(AvatarConversionPlan plan)
        {
            if (plan.Descriptor != null || plan.SourceRoot == null)
            {
                return;
            }

            Animator animator = plan.SourceRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return;
            }

            BasisAvatarPlan descriptorPlan = new BasisAvatarPlan
            {
                AvatarRootFileId = 0L,
            };

            descriptorPlan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.noSource",
                "This avatar has a humanoid rig but no VRChat descriptor, so a Basis Avatar "
                + "component was added empty. Open its inspector once and Basis fills in the "
                + "animator, scale, renderers, eye and mouth positions itself.");

            plan.Descriptor = new PlannedAvatarDescriptor
            {
                Plan = descriptorPlan,
                SourceRoot = plan.SourceRoot.transform,
            };
        }

        /// <summary>
        /// Reads the expression menu tree and parameters, and says what rebuilding them means.
        /// None of it converts: Basis has no menu format and no synced parameter list.
        /// </summary>
        private static void LoadExpressions(AvatarConversionPlan plan)
        {
            if (plan.Descriptor == null)
            {
                return;
            }

            VrcAvatarDescriptorData source = plan.Descriptor.SourceData;
            if (source == null)
            {
                return;
            }

            plan.Expressions = ExpressionInventoryLoader.Load(
                source.ExpressionsMenuGuid, source.ExpressionsMenuFileId,
                source.ExpressionParametersGuid, source.ExpressionParametersFileId);

            VrcExpressionInventory inventory = plan.Expressions;
            ReportExpressionAssetProblems(plan, inventory);
            if (inventory.ControlCount == 0 && inventory.Parameters.Count == 0)
            {
                return;
            }

            int toggles = inventory.CountOf(VrcExpressionControlType.Toggle);
            int buttons = inventory.CountOf(VrcExpressionControlType.Button);
            int subMenus = inventory.CountOf(VrcExpressionControlType.SubMenu);
            int radials = inventory.CountOf(VrcExpressionControlType.RadialPuppet);
            int axisPuppets = inventory.CountOf(VrcExpressionControlType.TwoAxisPuppet)
                + inventory.CountOf(VrcExpressionControlType.FourAxisPuppet);
            int puppets = radials + axisPuppets;

            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "expressions.menu",
                $"The expression menu has {inventory.ControlCount} controls across "
                + $"{inventory.Menus.Count} menus: {toggles} toggles, {buttons} buttons, "
                + $"{subMenus} submenus, {puppets} puppets. Basis has no menu of its own, so the "
                + "toggles and radials are rebuilt as Vixxy controls with a menu item each, "
                + "listed one after another. Buttons, which act only while held, and the nesting "
                + "the submenus gave the menu are not rebuilt.");

            if (inventory.Parameters.Count > 0)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "expressions.parameters",
                    $"{inventory.Parameters.Count} expression parameters were declared. Vixxy "
                    + "controls hold their own state, so there is no parameter list to recreate, "
                    + "but anything driven by these has to be rebuilt control by control.");
            }

            ResolveToggles(plan, source);

            // Radials are rebuilt as sliders and counted with the rest of what was rebuilt.
            // Only the ones with nowhere to go are reported as dropped.
            if (axisPuppets > 0)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "expressions.puppets",
                    $"{axisPuppets} of the controls are two or four axis puppets. Each drives two "
                    + "parameters at once, which no single Vixxy control expresses, so they are "
                    + "not rebuilt.");
            }
        }

        /// <summary>
        /// A menu the descriptor names but nothing could read. Missing, because a unitypackage
        /// export leaves out the Packages/ folder a build tool wrote it to; or binary, because
        /// VRCFury packs a built copy's menus and parameters into a container it saves that way,
        /// and only the project with its scripts can save it again.
        /// </summary>
        private static void ReportExpressionAssetProblems(
            AvatarConversionPlan plan, VrcExpressionInventory inventory)
        {
            foreach (VrcExpressionAssetProblem problem in inventory.Problems)
            {
                switch (problem.Kind)
                {
                    case VrcExpressionAssetProblemKind.Missing:
                        plan.Diagnostics.Add(DiagnosticSeverity.Warning,
                            "expressions.assetMissing",
                            $"The {problem.Role} asset {problem.Guid} is not in this project. A "
                            + "tool that builds a copy of the avatar, VRCFury among them, writes "
                            + "its output under Packages/, which a unitypackage export leaves "
                            + "out; copy that folder with its .meta files.");
                        break;

                    case VrcExpressionAssetProblemKind.NotText:
                        plan.Diagnostics.Add(DiagnosticSeverity.Warning,
                            "expressions.assetNotText",
                            $"{problem.Path} holds the {problem.Role} in Unity's binary form and "
                            + "cannot be read. VRCFury saves a built copy's menus and parameters "
                            + "that way, and only the project with its scripts can save the file "
                            + "again, so convert the original avatar or rebuild the menu there as "
                            + "assets of their own.");
                        break;

                    case VrcExpressionAssetProblemKind.NoDocument:
                        plan.Diagnostics.Add(DiagnosticSeverity.Warning,
                            "expressions.assetUnread",
                            $"{problem.Path} holds no readable {problem.Role} at the file id the "
                            + "descriptor names.");
                        break;
                }
            }
        }

        /// <summary>
        /// Ties the menu's toggles to the animator layers behind them, and says how many could be
        /// rebuilt as they stand.
        /// </summary>
        private static void ResolveToggles(
            AvatarConversionPlan plan, VrcAvatarDescriptorData source)
        {
            string fxGuid = null;
            foreach (VrcAnimationLayerEntry layer in source.AnimationLayers)
            {
                if (layer.Layer == VrcAnimationLayer.FX)
                {
                    fxGuid = layer.ControllerGuid;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fxGuid))
            {
                return;
            }

            if (ToggleResolver.LoadController(fxGuid) == null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "fx.controllerMissing",
                    $"The FX controller {fxGuid} is not in this project, so no toggle or motion "
                    + "could be traced. A tool that builds a copy of the avatar, VRCFury among "
                    + "them, writes its output under Packages/, which a unitypackage export "
                    + "leaves out; copy that folder with its .meta files.");
                return;
            }

            ResolveAmbientMotion(plan, fxGuid);

            plan.Toggles = ToggleResolver.Resolve(plan.Expressions, fxGuid);
            ReportUnreadLayers(plan, fxGuid);
            if (plan.Toggles.Count == 0)
            {
                return;
            }

            int simple = 0;
            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                if (toggle.IsSimple)
                {
                    simple++;
                }
            }

            int before = plan.VixxyControls.Count;

            BuildVixxyControls(plan, plan.Toggles, plan.SourceRoot.transform,
                plan.Sources.Count > 0 ? plan.Sources[0] : null);

            int rebuilt = plan.VixxyControls.Count - before;
            int toggleControls = plan.Expressions.CountOf(VrcExpressionControlType.Toggle)
                + plan.Expressions.CountOf(VrcExpressionControlType.RadialPuppet);

            // Counting controls against traced parameters compares unlike things: a menu often
            // has several controls sharing one parameter, each selecting a different value. The
            // rebuilt count is what was actually produced rather than what looked rebuildable,
            // since a layer that animates over time becomes a control and a motion together.
            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Mapped, "expressions.togglesResolved",
                $"{plan.Toggles.Count} animator layers were traced from the {toggleControls} "
                + "menu toggles and radials, which share fewer parameters between them. "
                + $"{rebuilt} of those layers became Vixxy controls, {simple} of them holding "
                + "nothing but object switching, blendshapes and material properties. The rest "
                + "are listed above with why.");
        }

        /// <summary>
        /// Plans an authored motion for every layer that plays without being switched on.
        /// <para>
        /// A Basis avatar carries no animator layers of its own, so animation that runs
        /// unprompted has nowhere else to go. Only clips that turn transforms are read, because
        /// that is what a baked Basis motion clip holds.
        /// </para>
        /// </summary>
        /// <summary>
        /// Names the FX layers nothing here read: gesture and locomotion layers, layers built on
        /// sub-state machines or Direct blend trees, layers steered by parameters outside the
        /// menu. Silence would read as though they had been handled.
        /// </summary>
        private static void ReportUnreadLayers(AvatarConversionPlan plan, string fxGuid)
        {
            AnimatorController controller = ToggleResolver.LoadController(fxGuid);
            if (controller == null)
            {
                return;
            }

            HashSet<string> read = new HashSet<string>();
            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                read.Add(toggle.LayerName);
            }

            foreach (PlannedAuthoredMotion motion in plan.AuthoredMotions)
            {
                read.Add(motion.Plan.Label);
            }

            // Layers the motion pass named in a diagnostic were read too, if only to be dropped.
            foreach (ConversionDiagnostic diagnostic in plan.MotionDiagnostics)
            {
                int start = diagnostic.Message.IndexOf('\'');
                int end = start >= 0 ? diagnostic.Message.IndexOf('\'', start + 1) : -1;
                if (start >= 0 && end > start)
                {
                    read.Add(diagnostic.Message.Substring(start + 1, end - start - 1));
                }
            }

            List<string> unread = new List<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (!read.Contains(layer.name))
                {
                    unread.Add(layer.name);
                }
            }

            if (unread.Count == 0)
            {
                return;
            }

            const int shown = 8;
            string names = "'" + string.Join("', '",
                unread.GetRange(0, Mathf.Min(shown, unread.Count))) + "'";
            if (unread.Count > shown)
            {
                names += $" and {unread.Count - shown} more";
            }

            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "fx.layersUnread",
                $"{unread.Count} of {controller.layers.Length} FX layers were not read: {names}. "
                + "Only layers a menu toggle or radial steers, and layers that play on their "
                + "own, are read.");
        }

        private static void ResolveAmbientMotion(AvatarConversionPlan plan, string fxGuid)
        {
            AnimatorController controller = ToggleResolver.LoadController(fxGuid);
            if (controller == null)
            {
                return;
            }

            foreach (AmbientMotionLayer layer in FxControllerReader.FindAmbientLayers(controller))
            {
                // A state whose time is driven by a parameter is a slider, not a motion: the
                // parameter scrubs through the clip.
                if (layer.MotionTime)
                {
                    plan.MotionDiagnostics.Add(DiagnosticSeverity.Dropped, "motion.motionTime",
                        $"'{layer.LayerName}' scrubs its clip with a parameter (motion time). "
                        + "Nothing here reads that; rebuild it as a Vixxy slider by hand.");
                    continue;
                }

                ClipEffects effects = AnimationClipReader.Read(layer.Clip);
                if (effects.AnimatedRotationPaths.Count == 0)
                {
                    if (effects.AnimatedCurves > 0)
                    {
                        plan.MotionDiagnostics.Add(DiagnosticSeverity.Dropped, "motion.notRotation",
                            $"'{layer.LayerName}' plays on its own and animates something other "
                            + "than rotation. A baked motion holds rotation only, so it was "
                            + "dropped.");
                    }

                    continue;
                }

                AuthoredMotionPlan motion = MotionToAuthoredMapper.MapAmbient(
                    layer.LayerName, layer.Loop, effects, layer.Speed);

                foreach (ConversionDiagnostic diagnostic in motion.Diagnostics)
                {
                    plan.MotionDiagnostics.Add(diagnostic);
                }

                plan.AuthoredMotions.Add(new PlannedAuthoredMotion
                {
                    Plan = motion,
                    SourceClip = layer.Clip,
                    OutputFolder = OutputFolderFor(layer.Clip),
                });
            }
        }

        /// <summary>
        /// Where a baked clip goes: a folder of our own beside the animation it was baked from,
        /// so it sits with the avatar's assets rather than anywhere in particular, and a second
        /// conversion writes over it rather than beside it.
        /// </summary>
        private static string OutputFolderFor(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(folder) ? string.Empty : $"{folder}/{ProductInfo.Name} Motion";
        }

        /// <summary>
        /// Turns the toggles Modular Avatar would install into Vixxy controls. Each belongs to
        /// the prefab it came from, and its paths are resolved inside that prefab.
        /// </summary>
        /// <summary>
        /// <summary>
        /// Two controls touching the same object, shape or property fight: in VRChat the later
        /// FX layer wins every frame, on Basis the control used last wins.
        /// </summary>
        private static void ReportOverlaps(AvatarConversionPlan plan)
        {
            Dictionary<string, List<string>> owners = new Dictionary<string, List<string>>();

            void Note(string key, string control)
            {
                if (!owners.TryGetValue(key, out List<string> names))
                {
                    names = new List<string>();
                    owners[key] = names;
                }

                if (!names.Contains(control))
                {
                    names.Add(control);
                }
            }

            foreach (PlannedVixxyControl planned in plan.VixxyControls)
            {
                VixxyControlPlan control = planned.Plan;
                foreach (VixxyActivationPlan activation in control.Activations)
                {
                    if (activation.MotionIndex < 0)
                    {
                        Note("object " + activation.Path, control.MenuName);
                    }
                }

                foreach (VixxySubjectPlan subject in control.Subjects)
                {
                    foreach (VixxyBlendShapePlan shape in subject.BlendShapes)
                    {
                        Note($"blendshape {shape.ShapeName} on {subject.Path}", control.MenuName);
                    }

                    foreach (VixxyMaterialPropertyPlan property in subject.MaterialProperties)
                    {
                        Note($"{property.PropertyName} on {subject.Path}", control.MenuName);
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in owners)
            {
                if (entry.Value.Count < 2)
                {
                    continue;
                }

                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.overlap",
                    $"'{string.Join("', '", entry.Value)}' all set {entry.Key}. On Basis the "
                    + "control used last wins; in VRChat the later FX layer did.");
            }
        }

        private static void BuildModularAvatarControls(AvatarConversionPlan plan)
        {
            int before = plan.VixxyControls.Count;

            foreach (ModularAvatarToggle toggle in plan.ModularAvatarToggles)
            {
                if (toggle.Source?.Root == null)
                {
                    continue;
                }

                BuildVixxyControls(plan, new[] { toggle.Toggle },
                    toggle.Source.Root.transform, toggle.Source);
            }

            int rebuilt = plan.VixxyControls.Count - before;
            if (plan.ModularAvatarToggles.Count > 0)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Mapped, "modularAvatar.togglesRebuilt",
                    $"{rebuilt} of {plan.ModularAvatarToggles.Count} Modular Avatar menu toggles "
                    + "were rebuilt as Vixxy controls. They would otherwise do nothing on Basis, "
                    + "which has no expression menu for Modular Avatar to install them into.");
            }
        }

        /// <summary>
        /// Turns the toggles that can be rebuilt into Vixxy controls, resolving each switched
        /// object's path against the prefab the toggle belongs to.
        /// </summary>
        private static void BuildVixxyControls(AvatarConversionPlan plan,
            IEnumerable<ResolvedToggle> toggles, Transform root, ConversionSource source)
        {
            foreach (ResolvedToggle toggle in toggles)
            {
                VixxyControlPlan control = ToggleToVixxyMapper.Map(toggle);

                foreach (ConversionDiagnostic diagnostic in control.Diagnostics)
                {
                    plan.ToggleDiagnostics.Add(diagnostic);
                }

                // A control may switch nothing and only set blendshapes, which is how a body
                // shape slider is built.
                if (control.Activations.Count == 0 && control.Subjects.Count == 0)
                {
                    continue;
                }

                PlannedVixxyControl planned = new PlannedVixxyControl { Plan = control };
                List<VixxyActivationPlan> kept = new List<VixxyActivationPlan>();
                int rigSwitches = 0;
                int rendererSwitches = 0;

                foreach (VixxyActivationPlan activation in control.Activations)
                {
                    // A motion is switched by the component this conversion writes, so there is
                    // no transform to look up. The slot is kept so the two lists stay in step.
                    if (activation.MotionIndex >= 0)
                    {
                        kept.Add(activation);
                        planned.SourceTargets.Add(null);
                        planned.SourceRigs.Add(null);
                        continue;
                    }

                    // A script switch is a PhysBone switch when a rig is planned on that object;
                    // the control then drives the rig. Any other script has no counterpart.
                    if (activation.Target == VixxyActivationTarget.Component)
                    {
                        Transform host = root.Find(activation.Path);
                        PlannedJiggleRig rig = host == null ? null : RigHostedAt(plan, host);
                        if (rig == null)
                        {
                            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped,
                                "vixxy.componentSwitch.dropped",
                                $"'{control.MenuName}' switches a component on {activation.Path} "
                                + "that is not a converted PhysBone. That switch was left out.");
                            continue;
                        }

                        // A converted PhysBone is enabled; disabled ones write no rig at all.
                        VixxyAuthoredDefaults.Apply(activation, true);
                        if (AllEqual(activation.Choices))
                        {
                            continue;
                        }

                        kept.Add(activation);
                        planned.SourceTargets.Add(host);
                        planned.SourceRigs.Add(rig);
                        rigSwitches++;
                        continue;
                    }

                    // Vixxy refuses to toggle the avatar root (CannotToggleRootGameObject).
                    if (string.IsNullOrEmpty(activation.Path) || root.Find(activation.Path) == root)
                    {
                        plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.rootActivation",
                            $"'{control.MenuName}' switches the avatar root itself, which Vixxy "
                            + "refuses. That object was left out of the control.");
                        continue;
                    }

                    // Unity ignores a binding to a missing object, so the control keeps its
                    // other targets.
                    Transform target = root.Find(activation.Path);
                    if (target == null)
                    {
                        plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.targetMissing",
                            $"'{control.MenuName}' switches {activation.Path}, which is not in "
                            + "this avatar. That object was left out of the control.");
                        continue;
                    }

                    // A renderer switch drives the renderer's enabled flag, which Vixxy does for
                    // a Renderer component the same way it sets an object active.
                    if (activation.Target == VixxyActivationTarget.Renderer)
                    {
                        Renderer renderer = target.GetComponent<Renderer>();
                        if (renderer == null)
                        {
                            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.targetMissing",
                                $"'{control.MenuName}' switches a renderer on {activation.Path}, "
                                + "which has none in this avatar. That switch was left out.");
                            continue;
                        }

                        VixxyAuthoredDefaults.Apply(activation, renderer.enabled);
                        if (AllEqual(activation.Choices))
                        {
                            continue;
                        }

                        kept.Add(activation);
                        planned.SourceTargets.Add(target);
                        planned.SourceRigs.Add(null);
                        rendererSwitches++;
                        continue;
                    }

                    // Whatever the toggle did not animate stays as the avatar was authored.
                    VixxyAuthoredDefaults.Apply(activation, target.gameObject.activeSelf);

                    // Every choice the same is a no-op on VRChat. Vixxy reads only the ON entry
                    // of a two-choice activation, so it would switch the object off instead.
                    if (AllEqual(activation.Choices))
                    {
                        continue;
                    }

                    kept.Add(activation);
                    planned.SourceTargets.Add(target);
                    planned.SourceRigs.Add(null);
                }

                control.Activations = kept;

                if (rigSwitches > 0 || rendererSwitches > 0)
                {
                    plan.ToggleDiagnostics.Add(DiagnosticSeverity.Mapped, "vixxy.componentSwitch",
                        $"'{control.MenuName}' switches {rigSwitches} PhysBones and "
                        + $"{rendererSwitches} renderers. The control enables and disables the "
                        + "jiggle rigs and renderers the same way it switches objects.");
                }

                if (control.Activations.Count == 0 && control.Subjects.Count == 0)
                {
                    plan.ToggleDiagnostics.Add(DiagnosticSeverity.Dropped, "vixxy.nothingToSwitch",
                        $"'{control.MenuName}' changed nothing that exists on this avatar, so no "
                        + "control was written.");
                    continue;
                }

                bool resolved = ResolveSubjects(plan, control, planned, root);

                if (resolved)
                {
                    AttachMotions(plan, control, planned, toggle, source);
                    planned.Source = source;
                    plan.VixxyControls.Add(planned);
                }
            }

            if (plan.VixxyControls.Count > 0)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Mapped, "vixxy.rebuilt",
                    $"{plan.VixxyControls.Count} menu toggles were rebuilt as Vixxy controls, "
                    + "each with a menu item. The rest are listed above with why they were not.");
                WarnIfCommsMissing(plan);
            }
        }

        /// <summary>The planned rig whose PhysBone sits on this object, if any.</summary>
        private static PlannedJiggleRig RigHostedAt(AvatarConversionPlan plan, Transform host)
        {
            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                if (rig.SourceHost == host)
                {
                    return rig;
                }
            }

            return null;
        }

        private static bool AllEqual(bool[] choices)
        {
            for (int i = 1; i < choices.Length; i++)
            {
                if (choices[i] != choices[0])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A Vixxy control inside an avatar is initialised by HVRAvatarComms, which the
        /// HVR.Networking prefab carries. Without it every control is inert.
        /// </summary>
        private static void WarnIfCommsMissing(AvatarConversionPlan plan)
        {
            if (plan.SourceRoot == null || plan.ToggleDiagnostics.HasCode("vixxy.commsMissing"))
            {
                return;
            }

            // Matched by name: HVRAvatarComms derives from a Basis Framework type this assembly
            // does not reference, and only its presence matters here.
            foreach (Component component in plan.SourceRoot.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "HVRAvatarComms")
                {
                    return;
                }
            }

            plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.commsMissing",
                "The avatar carries no HVR Avatar Comms. Vixxy controls are initialised through "
                + "it, so add HVR Basis Comms's HVR.Networking prefab to the avatar.");
        }

        /// <summary>
        /// Plans the motions a control switches, alongside the control itself.
        /// <para>
        /// They go in the plan's own motion list as well, so they are written by the same pass
        /// that writes ambient motion, are counted and can be deselected with the rest.
        /// </para>
        /// </summary>
        private static void AttachMotions(
            AvatarConversionPlan plan, VixxyControlPlan control, PlannedVixxyControl planned,
            ResolvedToggle toggle, ConversionSource source)
        {
            foreach (VixxyMotionPlan motion in control.Motions)
            {
                AnimationClip clip = motion.Choice < toggle.Choices.Count
                    ? toggle.Choices[motion.Choice].Clip
                    : null;

                PlannedAuthoredMotion authored = new PlannedAuthoredMotion
                {
                    Plan = motion.Motion,
                    SourceClip = clip,
                    OutputFolder = OutputFolderFor(clip),
                    Source = source,
                    SwitchedBy = planned,
                };

                foreach (ConversionDiagnostic diagnostic in motion.Motion.Diagnostics)
                {
                    plan.MotionDiagnostics.Add(diagnostic);
                }

                planned.Motions.Add(authored);
                plan.AuthoredMotions.Add(authored);
            }
        }

        /// <summary>
        /// Resolves each blendshape subject to its renderer, and fills in the weight for whichever
        /// side of the toggle did not set it from what the avatar was authored with.
        /// </summary>
        private static bool ResolveSubjects(
            AvatarConversionPlan plan, VixxyControlPlan control, PlannedVixxyControl planned,
            Transform root)
        {
            foreach (VixxySubjectPlan subject in control.Subjects)
            {
                Transform target = root.Find(subject.Path);
                Renderer renderer = target == null ? null : target.GetComponent<Renderer>();

                if (renderer == null)
                {
                    plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.rendererMissing",
                        $"'{control.MenuName}' sets {subject.Path}, which is not a renderer in "
                        + "this avatar.");
                    return false;
                }

                if (subject.BlendShapes.Count > 0)
                {
                    SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                    if (skinned == null || skinned.sharedMesh == null)
                    {
                        plan.ToggleDiagnostics.Add(DiagnosticSeverity.Warning, "vixxy.rendererMissing",
                            $"'{control.MenuName}' sets blendshapes on {subject.Path}, which is "
                            + "not a skinned mesh in this avatar.");
                        return false;
                    }

                    FillBlendShapeDefaults(skinned, subject);
                }

                if (subject.MaterialProperties.Count > 0)
                {
                    // Vixxy resolves the component by class name, so it has to be the type that
                    // is actually there rather than the one the clip was authored against.
                    subject.RendererTypeName = renderer.GetType().FullName;
                    FillMaterialDefaults(plan, control, renderer, subject);
                }

                planned.SourceRenderers.Add(renderer);
            }

            return true;
        }

        private static void FillBlendShapeDefaults(
            SkinnedMeshRenderer renderer, VixxySubjectPlan subject)
        {
            foreach (VixxyBlendShapePlan shape in subject.BlendShapes)
            {
                if (shape.AllChoicesSet)
                {
                    continue;
                }

                int index = renderer.sharedMesh.GetBlendShapeIndex(shape.ShapeName);
                float authored = index >= 0 ? renderer.GetBlendShapeWeight(index) : 0f;

                VixxyAuthoredDefaults.Apply(shape, authored);
            }
        }

        /// <summary>
        /// Fills in the channels neither side of the toggle set, from the material as authored.
        /// A clip that sets only the red channel of a colour, or only sets it in one state, is
        /// the common case rather than the exception.
        /// </summary>
        private static void FillMaterialDefaults(
            AvatarConversionPlan plan, VixxyControlPlan control, Renderer renderer,
            VixxySubjectPlan subject)
        {
            Material material = renderer.sharedMaterial;

            foreach (VixxyMaterialPropertyPlan property in subject.MaterialProperties)
            {
                Vector4 authored = AuthoredValue(material, property);

                for (int choice = 0; choice < property.Choices.Length; choice++)
                {
                    for (int channel = 0; channel < property.Channels; channel++)
                    {
                        if (property.Set[choice][channel])
                        {
                            continue;
                        }

                        Vector4 value = property.Choices[choice];
                        value[channel] = authored[channel];
                        property.Choices[choice] = value;
                    }
                }
            }

            // Vixxy sets material properties through a MaterialPropertyBlock, which the renderer
            // applies to all of its materials at once.
            if (renderer.sharedMaterials.Length > 1)
            {
                plan.ToggleDiagnostics.Add(DiagnosticSeverity.Approximated, "vixxy.materialBlock",
                    $"'{control.MenuName}' sets material properties on {subject.Path}, which has "
                    + $"{renderer.sharedMaterials.Length} materials. Vixxy applies them through a "
                    + "MaterialPropertyBlock, so every material on that renderer is affected.");
            }
        }

        /// <summary>What the material holds for a property, or zero when it does not have it.</summary>
        private static Vector4 AuthoredValue(
            Material material, VixxyMaterialPropertyPlan property)
        {
            if (material == null || !material.HasProperty(property.PropertyName))
            {
                return Vector4.zero;
            }

            switch (property.Kind)
            {
                case VixxyMaterialPropertyKind.Colour:
                    return material.GetColor(property.PropertyName);
                case VixxyMaterialPropertyKind.Vector:
                    return material.GetVector(property.PropertyName);
                default:
                    return new Vector4(material.GetFloat(property.PropertyName), 0f, 0f, 0f);
            }
        }

        private static PlannedAvatarDescriptor PlanDescriptor(
            UnityYamlDocument document, PrefabObjectResolver resolver, AvatarConversionPlan plan)
        {
            VrcAvatarDescriptorData source = VrcAvatarDescriptorReader.Read(document);
            BasisAvatarPlan descriptorPlan = VrcAvatarDescriptorToBasisMapper.Map(source);

            if (!resolver.TryResolveTransform(descriptorPlan.AvatarRootFileId, out Transform root))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "descriptor.unresolved",
                    "The avatar descriptor could not be tied to a transform and was skipped.");
                return null;
            }

            PlannedAvatarDescriptor planned = new PlannedAvatarDescriptor
            {
                Plan = descriptorPlan,
                SourceData = source,
                SourceRoot = root,
                SourceVisemeMesh = ResolveRenderer(
                    resolver, descriptorPlan.VisemeMeshFileId, descriptorPlan.Diagnostics,
                    "viseme"),
                SourceBlinkMesh = ResolveRenderer(
                    resolver, descriptorPlan.BlinkMeshFileId, descriptorPlan.Diagnostics,
                    "blink"),
            };

            return planned;
        }

        private static SkinnedMeshRenderer ResolveRenderer(
            PrefabObjectResolver resolver, long fileId,
            List<ConversionDiagnostic> diagnostics, string role)
        {
            if (fileId == 0L)
            {
                return null;
            }

            if (resolver.TryResolve(fileId, out Object resolved)
                && resolved is SkinnedMeshRenderer renderer)
            {
                return renderer;
            }

            diagnostics.Add(DiagnosticSeverity.Warning, $"descriptor.{role}Mesh.unresolved",
                $"The {role} mesh could not be resolved, so it was left unset.");
            return null;
        }

        /// <summary>
        /// Plans one VRM node constraint. It drives the object it sits on and follows a single
        /// source, so there is neither a target to relocate nor a source list to flatten.
        /// </summary>
        private static PlannedConstraint PlanVrmConstraint(
            UnityYamlDocument document, SourceComponentKind kind, PrefabObjectResolver resolver,
            AvatarConversionPlan plan)
        {
            VrmConstraintKind vrmKind = kind switch
            {
                SourceComponentKind.Vrm10AimConstraint => VrmConstraintKind.Aim,
                SourceComponentKind.Vrm10RollConstraint => VrmConstraintKind.Roll,
                _ => VrmConstraintKind.Rotation,
            };

            return PlanVrmConstraint(
                VrmDocumentReader.ReadConstraint(document, vrmKind), resolver, plan);
        }

        /// <summary>Plans one VRM node constraint from data either reader produced.</summary>
        private static PlannedConstraint PlanVrmConstraint(
            VrmConstraintData source, PrefabObjectResolver resolver, AvatarConversionPlan plan)
        {
            BasisConstraintPlan constraintPlan = VrmConstraintToBasisMapper.Map(source);

            if (!resolver.TryResolveTransform(constraintPlan.HostFileId, out Transform host))
            {
                plan.Unresolved++;
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.unresolved",
                    $"A VRM {source.Kind} constraint at &{source.DocumentFileId} could not be "
                    + "tied to a transform and was skipped.");
                return null;
            }

            PlannedConstraint planned = new PlannedConstraint
            {
                Plan = constraintPlan,
                SourceHost = host,
            };

            // The rest pose is the one the avatar was authored in, which is what both systems
            // measure from.
            constraintPlan.RotationAtRest = host.localEulerAngles;

            foreach (BasisConstraintSourcePlan entry in constraintPlan.Sources)
            {
                planned.SourceTransforms.Add(
                    resolver.TryResolveTransform(entry.TransformFileId, out Transform from)
                        ? from
                        : null);
            }

            // A VRM rotation or roll constraint copies the source's turn away from its rest
            // pose, so the destination is unchanged while the source rests. A Basis rotation
            // constraint takes the source's rotation itself, so the offset between the two rest
            // poses is written as the rotation offset to keep the authored pose at rest.
            if (source.Kind != VrmConstraintKind.Aim
                && planned.SourceTransforms.Count > 0
                && planned.SourceTransforms[0] != null)
            {
                Transform from = planned.SourceTransforms[0];
                constraintPlan.RotationOffset =
                    (Quaternion.Inverse(from.rotation) * host.rotation).eulerAngles;
            }

            return planned;
        }

        private static PlannedConstraint PlanConstraint(
            UnityYamlDocument document, VrcConstraintKind kind, PrefabObjectResolver resolver,
            AvatarConversionPlan plan)
        {
            VrcConstraintData source = VrcConstraintDocumentReader.Read(document, kind);
            BasisConstraintPlan constraintPlan = VrcConstraintToBasisMapper.Map(source);

            if (!resolver.TryResolveTransform(constraintPlan.HostFileId, out Transform host))
            {
                plan.Unresolved++;
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "constraint.unresolved",
                    $"A {kind} constraint at &{document.FileId} could not be tied to a transform "
                    + "and was skipped.");
                return null;
            }

            // TargetTransform names a Transform and m_GameObject a GameObject, so the mapper
            // cannot tell a target that is the constraint's own object from a real retarget.
            if (resolver.TryResolveTransform(source.OwnerGameObjectFileId, out Transform owner)
                && owner == host)
            {
                constraintPlan.Diagnostics.RemoveAll(d => d.Code == "constraint.retargeted");
            }

            PlannedConstraint planned = new PlannedConstraint
            {
                Plan = constraintPlan,
                SourceHost = host,
            };

            foreach (BasisConstraintSourcePlan entry in constraintPlan.Sources)
            {
                if (resolver.TryResolveTransform(entry.TransformFileId, out Transform sourceTransform))
                {
                    planned.SourceTransforms.Add(sourceTransform);
                }
                else
                {
                    planned.SourceTransforms.Add(null);
                    constraintPlan.Diagnostics.Add(DiagnosticSeverity.Warning,
                        "constraint.source.unresolved",
                        "A constraint source could not be resolved and was dropped.");
                }
            }

            if (constraintPlan.WorldUpTransformFileId != 0L
                && resolver.TryResolveTransform(
                    constraintPlan.WorldUpTransformFileId, out Transform worldUp))
            {
                planned.SourceWorldUpObject = worldUp;
            }

            return planned;
        }

        private static int CountChildren(Transform bone, HashSet<Transform> ignored)
        {
            int count = 0;
            for (int i = 0; i < bone.childCount; i++)
            {
                if (!ignored.Contains(bone.GetChild(i)))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Bones below the root with two or more children, ignored subtrees left out.</summary>
        private static int CountBranchesBelow(Transform root, HashSet<Transform> ignored)
        {
            int branches = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (ignored.Contains(child))
                {
                    continue;
                }

                if (CountChildren(child, ignored) >= 2)
                {
                    branches++;
                }

                branches += CountBranchesBelow(child, ignored);
            }

            return branches;
        }

        private static PlannedJiggleRig PlanOne(
            UnityYamlDocument document,
            PrefabObjectResolver resolver,
            IReadOnlyDictionary<long, PlannedJiggleCollider> colliders,
            JiggleMappingProfile profile,
            AvatarConversionPlan plan)
        {
            PhysBoneData source = PhysBoneDocumentReader.ReadPhysBone(document);

            if (!resolver.TryResolveTransform(source.OwnerGameObjectFileId, out Transform host))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "physbone.unresolved",
                    $"A PhysBone at &{document.FileId} could not be tied to a transform and was "
                    + "skipped.");
                return null;
            }

            // VRChat treats an empty Root Transform as "this object". A set one that cannot be
            // resolved is not the same case, so nothing is written for it.
            Transform rootBone = host;
            if (source.RootTransformFileId != 0L
                && !resolver.TryResolveTransform(source.RootTransformFileId, out rootBone))
            {
                plan.Unresolved++;
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "physbone.rootUnresolved",
                    $"The Root Transform of the PhysBone on {host.name} could not be resolved. "
                    + "The PhysBone was skipped.");
                return null;
            }

            HashSet<Transform> ignored = new HashSet<Transform>();
            foreach (long ignoredFileId in source.IgnoreTransformFileIds)
            {
                if (resolver.TryResolveTransform(ignoredFileId, out Transform ignoredTransform))
                {
                    ignored.Add(ignoredTransform);
                }
            }

            JiggleRigPlan rigPlan = PhysBoneToJiggleMapper.Map(
                source, profile, CountChildren(rootBone, ignored));
            rigPlan.Preset = JigglePresetLibrary.GuessFrom(rootBone.name);

            if (source.MultiChildType == PhysBoneMultiChildType.Ignore)
            {
                int branches = CountBranchesBelow(rootBone, ignored);
                if (branches > 0)
                {
                    rigPlan.Diagnostics.Add(DiagnosticSeverity.Approximated,
                        "physbone.multiChildType.ignoreBranches",
                        $"{branches} bone(s) below the root have several children. PhysBone "
                        + "leaves such bones still under Ignore; jiggle swings them.");
                }
            }

            PlannedJiggleRig planned = new PlannedJiggleRig
            {
                Plan = rigPlan,
                SourceHost = host,
                SourceRootBone = rootBone,
            };

            AttachExclusionsAndColliders(rigPlan, planned, resolver, colliders);

            return planned;
        }
    }

    internal static class JiggleRigDataLimits
    {
        internal const int MaxColliders =
            GatorDragonGames.JigglePhysics.JiggleRigData.MaxRuntimeJiggleColliders;
    }
}
