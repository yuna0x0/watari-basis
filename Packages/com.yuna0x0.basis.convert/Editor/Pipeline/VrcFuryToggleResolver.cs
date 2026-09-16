using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Pipeline
{
    public sealed class VrcFuryToggle
    {
        public ResolvedToggle Toggle;

        public ConversionSource Source;
    }

    /// <summary>An Armature Link as read, with the prefab it sits in, for the planner.</summary>
    public sealed class VrcFuryArmatureLinkSource
    {
        public VrcFuryArmatureLinkData Data;
        public long OwnerGameObjectFileId;
        public ConversionSource Source;
        public PrefabObjectResolver Resolver;
    }

    /// <summary>What a pass over one prefab's VRCFury components found, for the report.</summary>
    public sealed class VrcFuryReadResult
    {
        public int Components;
        public int Toggles;
        public int FullControllers;
        public int ArmatureLinks;

        /// <summary>Other feature classes and how many of each, none of them read.</summary>
        public Dictionary<string, int> OtherFeatures = new Dictionary<string, int>();

        /// <summary>Action classes a toggle used that nothing here can express, with counts.</summary>
        public Dictionary<string, int> DroppedActions = new Dictionary<string, int>();

        public List<VrcFuryToggle> Resolved = new List<VrcFuryToggle>();
        public List<VrcFuryArmatureLinkSource> ArmatureLinkData = new List<VrcFuryArmatureLinkSource>();
        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
    }

    /// <summary>
    /// Turns VRCFury's components into the toggles the Vixxy mapper already takes.
    /// <para>
    /// A Toggle feature is a menu item, a parameter and a list of actions; its on state is what
    /// the actions do and its off state is the rest pose, except that an object toggle names
    /// both sides. VRCFury applies the off clip to the resting state at build, so a "turn on"
    /// object is off at rest whatever the prefab says. A Full Controller merges a controller,
    /// menus and parameters from assets, which is the base avatar's own path with the clip
    /// bindings moved under the object carrying the component. An Armature Link moves bones,
    /// which nothing here does yet, so it is counted and warned about.
    /// </para>
    /// </summary>
    public static class VrcFuryToggleResolver
    {
        public static VrcFuryReadResult Resolve(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver, ConversionSource source)
        {
            VrcFuryReadResult result = new VrcFuryReadResult();
            if (documents == null || resolver == null || source?.Root == null)
            {
                return result;
            }

            Transform root = source.Root.transform;

            foreach (UnityYamlDocument document in documents)
            {
                if (document.Stripped
                    || document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId)
                    || KnownScriptIdentities.Resolve(guid, scriptFileId) != SourceComponentKind.VrcFuryComponent)
                {
                    continue;
                }

                result.Components++;
                VrcFuryComponentData data = VrcFuryDocumentReader.Read(document);

                foreach (VrcFuryFeatureData feature in data.Features)
                {
                    switch (feature)
                    {
                        case VrcFuryToggleData toggle:
                            result.Toggles++;
                            ResolveToggle(toggle, data.OwnerGameObjectFileId, root, resolver, source, result);
                            break;

                        case VrcFuryFullControllerData controller:
                            result.FullControllers++;
                            ResolveFullController(controller, data.OwnerGameObjectFileId, root, resolver, source, result);
                            break;

                        case VrcFuryArmatureLinkData link:
                            result.ArmatureLinks++;
                            result.ArmatureLinkData.Add(new VrcFuryArmatureLinkSource
                            {
                                Data = link, OwnerGameObjectFileId = data.OwnerGameObjectFileId,
                                Source = source, Resolver = resolver,
                            });
                            break;

                        default:
                            Count(result.OtherFeatures, feature.Class);
                            break;
                    }
                }
            }

            return result;
        }

        private static void ResolveToggle(
            VrcFuryToggleData data, long ownerFileId, Transform root, PrefabObjectResolver resolver,
            ConversionSource source, VrcFuryReadResult result)
        {
            string menuName = MenuNameOf(data.Name);

            if (string.IsNullOrEmpty(data.Name))
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Dropped,
                    "vrcfury.toggle.dropped",
                    $"A VRCFury toggle driven by the parameter '{data.GlobalParam}' has no menu "
                    + "item; something in the animator drives it. Not rebuilt."));
                return;
            }

            if (data.HoldButton)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Dropped,
                    "vrcfury.toggle.dropped",
                    $"'{menuName}' is a hold button, which acts only while held. Buttons are not "
                    + "rebuilt."));
                return;
            }

            ResolvedToggle toggle = new ResolvedToggle
            {
                MenuName = menuName,
                Parameter = data.UseGlobalParam && !string.IsNullOrEmpty(data.GlobalParam)
                    ? data.GlobalParam
                    : $"VF/{data.Name}#{ownerFileId}",
                LayerName = "VRCFury Toggle",
                Saved = data.Saved,
                NetworkSynced = true,
                IsSlider = data.Slider,
                DefaultValue = data.Slider ? data.DefaultSliderValue : (data.DefaultOn ? 1f : 0f),
            };

            ClipEffects on = new ClipEffects();
            ClipEffects off = new ClipEffects();

            foreach (VrcFuryActionData action in data.Actions)
            {
                switch (action.Kind)
                {
                    case VrcFuryActionKind.ObjectToggle:
                        AddObjectToggle(action, root, resolver, on, off, menuName, result);
                        break;

                    case VrcFuryActionKind.BlendShape:
                        AddBlendShape(action, root, resolver, on, menuName, result);
                        break;

                    case VrcFuryActionKind.MaterialProperty:
                        AddMaterialProperty(action, root, resolver, on, menuName, result);
                        break;

                    case VrcFuryActionKind.MaterialSwap:
                        Count(result.DroppedActions, "MaterialAction");
                        break;

                    default:
                        Count(result.DroppedActions, action.Class);
                        break;
                }
            }

            toggle.Choices.Add(new ResolvedChoice { Name = "OFF", Value = 0, Effects = off });
            toggle.Choices.Add(new ResolvedChoice { Name = "ON", Value = 1, Effects = on });

            List<string> ignored = new List<string>();
            if (data.HasTransition) ignored.Add("a transition animation");
            if (data.SeparateLocal) ignored.Add("a separate local state");
            if (data.SecurityEnabled) ignored.Add("the security lock");
            if (data.EnableDriveGlobalParam) ignored.Add("driving another parameter");
            if (ignored.Count > 0)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Approximated,
                    "vrcfury.toggle.animatorOnly",
                    $"'{menuName}' also has {string.Join(", ", ignored)}, which only exists while "
                    + "an animator runs. The control switches its on and off states directly."));
            }

            if (data.EnableExclusiveTag && !string.IsNullOrEmpty(data.ExclusiveTag))
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Approximated,
                    "vrcfury.exclusiveTag",
                    $"'{menuName}' shares the exclusive tag '{data.ExclusiveTag}', which turned "
                    + "the others off when it went on. Vixxy controls are independent, so more "
                    + "than one can be on at once."));
            }

            result.Resolved.Add(new VrcFuryToggle { Toggle = toggle, Source = source });
        }

        private static void AddObjectToggle(
            VrcFuryActionData action, Transform root, PrefabObjectResolver resolver,
            ClipEffects on, ClipEffects off, string menuName, VrcFuryReadResult result)
        {
            // VRCFury itself does nothing for an action whose object was never set.
            if (action.ObjectFileId == 0L)
            {
                return;
            }

            if (!resolver.TryResolveTransform(action.ObjectFileId, out Transform target)
                || target == null || !target.IsChildOf(root) || target == root)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                    "vrcfury.action.unresolved",
                    $"'{menuName}' switches an object that is not in this prefab. That switch "
                    + "was left out."));
                return;
            }

            bool onState;
            switch (action.Mode)
            {
                case VrcFuryObjectToggleMode.TurnOff:
                    onState = false;
                    break;
                case VrcFuryObjectToggleMode.Toggle:
                    onState = !target.gameObject.activeSelf;
                    break;
                default:
                    onState = true;
                    break;
            }

            string path = ModularAvatarToggleResolver.PathWithin(root, target);
            (onState ? on.Activated : on.Deactivated).Add(path);
            (onState ? off.Deactivated : off.Activated).Add(path);
        }

        private static void AddBlendShape(
            VrcFuryActionData action, Transform root, PrefabObjectResolver resolver,
            ClipEffects on, string menuName, VrcFuryReadResult result)
        {
            if (string.IsNullOrEmpty(action.BlendShape))
            {
                return;
            }

            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            if (action.AllRenderers)
            {
                renderers.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }
            else if (resolver.TryResolve(action.RendererFileId, out Object component)
                && component is SkinnedMeshRenderer single)
            {
                renderers.Add(single);
            }

            int found = 0;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null || mesh.GetBlendShapeIndex(action.BlendShape) < 0)
                {
                    continue;
                }

                found++;
                on.BlendShapes.Add(new BlendShapeEffect
                {
                    Path = ModularAvatarToggleResolver.PathWithin(root, renderer.transform),
                    ShapeName = action.BlendShape,
                    Value = action.BlendShapeValue,
                });
            }

            if (found == 0)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                    "vrcfury.action.unresolved",
                    $"'{menuName}' sets the blendshape '{action.BlendShape}', which no renderer "
                    + "in this prefab has. That part was left out."));
            }
        }

        private static void AddMaterialProperty(
            VrcFuryActionData action, Transform root, PrefabObjectResolver resolver,
            ClipEffects on, string menuName, VrcFuryReadResult result)
        {
            if (string.IsNullOrEmpty(action.PropertyName) || action.PropertyName.Contains("."))
            {
                return;
            }

            // VRCFury itself does nothing for an action whose renderer was never set.
            if (!action.AffectAllMeshes && action.RendererObjectFileId == 0L && action.RendererFileId == 0L)
            {
                return;
            }

            List<Renderer> renderers = new List<Renderer>();
            if (action.AffectAllMeshes)
            {
                renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                Renderer single = null;
                if (action.RendererObjectFileId != 0L
                    && resolver.TryResolveTransform(action.RendererObjectFileId, out Transform host))
                {
                    single = host.GetComponent<Renderer>();
                }
                else if (action.RendererFileId != 0L
                    && resolver.TryResolve(action.RendererFileId, out Object component))
                {
                    single = component as Renderer;
                }

                if (single != null)
                {
                    renderers.Add(single);
                }
            }

            if (renderers.Count == 0)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                    "vrcfury.action.unresolved",
                    $"'{menuName}' sets the material property '{action.PropertyName}' on a "
                    + "renderer that is not in this prefab. That part was left out."));
                return;
            }

            VrcFuryMaterialPropertyType type = action.PropertyType;
            if (type == VrcFuryMaterialPropertyType.LegacyAuto)
            {
                type = DetectPropertyType(renderers, action.PropertyName);
                if (type == VrcFuryMaterialPropertyType.LegacyAuto)
                {
                    result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Dropped,
                        "vrcfury.action.unresolved",
                        $"'{menuName}' sets the material property '{action.PropertyName}' whose "
                        + "kind was left for VRCFury to detect, and no material here has it. "
                        + "That part was left out."));
                    return;
                }
            }

            foreach (Renderer renderer in renderers)
            {
                string path = ModularAvatarToggleResolver.PathWithin(root, renderer.transform);
                string typeName = renderer.GetType().FullName;

                if (type == VrcFuryMaterialPropertyType.Float)
                {
                    on.MaterialProperties.Add(new MaterialPropertyEffect
                    {
                        Path = path, RendererTypeName = typeName,
                        PropertyName = action.PropertyName, Channel = -1, Value = action.Value,
                    });
                    continue;
                }

                bool colour = type == VrcFuryMaterialPropertyType.Color;
                float[] values = colour ? action.ValueColor : action.ValueVector;
                for (int channel = 0; channel < 4; channel++)
                {
                    on.MaterialProperties.Add(new MaterialPropertyEffect
                    {
                        Path = path, RendererTypeName = typeName,
                        PropertyName = action.PropertyName, Channel = channel,
                        ColourChannel = colour, Value = values[channel],
                    });
                }
            }
        }

        /// <summary>
        /// What VRCFury does for a property whose kind was never recorded: ask the materials.
        /// </summary>
        private static VrcFuryMaterialPropertyType DetectPropertyType(
            List<Renderer> renderers, string propertyName)
        {
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    int index = material.shader.FindPropertyIndex(propertyName);
                    if (index < 0)
                    {
                        continue;
                    }

                    switch (material.shader.GetPropertyType(index))
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            return VrcFuryMaterialPropertyType.Color;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            return VrcFuryMaterialPropertyType.Vector;
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                        case UnityEngine.Rendering.ShaderPropertyType.Int:
                            return VrcFuryMaterialPropertyType.Float;
                        default:
                            return VrcFuryMaterialPropertyType.LegacyAuto;
                    }
                }
            }

            return VrcFuryMaterialPropertyType.LegacyAuto;
        }

        private static void ResolveFullController(
            VrcFuryFullControllerData data, long ownerFileId, Transform root,
            PrefabObjectResolver resolver, ConversionSource source, VrcFuryReadResult result)
        {
            // Menus and parameters, the way the base avatar's are read.
            VrcExpressionInventory inventory = new VrcExpressionInventory();
            foreach (VrcFuryMenuEntry entry in data.Menus)
            {
                VrcExpressionInventory loaded = ExpressionInventoryLoader.Load(
                    entry.Menu.Guid, entry.Menu.FileId, null, 0L);
                inventory.Menus.AddRange(loaded.Menus);
                inventory.Problems.AddRange(loaded.Problems);
            }

            foreach (VrcFuryAssetReference parameters in data.Parameters)
            {
                VrcExpressionInventory loaded = ExpressionInventoryLoader.Load(
                    null, 0L, parameters.Guid, parameters.FileId);
                inventory.Parameters.AddRange(loaded.Parameters);
                inventory.Problems.AddRange(loaded.Problems);
            }

            foreach (VrcExpressionAssetProblem problem in inventory.Problems)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                    "vrcfury.fullController.assetMissing",
                    $"A VRCFury Full Controller names a {problem.Role} asset that could not be "
                    + $"read ({problem.Kind}): {(problem.Path.Length > 0 ? problem.Path : problem.Guid)}."));
            }

            // Bindings in the merged clips are relative to the object carrying the component
            // unless the controller was authored against the avatar root.
            string prefix = string.Empty;
            if (!data.RootBindingsApplyToAvatar)
            {
                Transform from = null;
                if (data.RootObjectOverrideFileId != 0L)
                {
                    resolver.TryResolveTransform(data.RootObjectOverrideFileId, out from);
                }

                if (from == null)
                {
                    resolver.TryResolveTransform(ownerFileId, out from);
                }

                if (from != null && from.IsChildOf(root))
                {
                    prefix = ModularAvatarToggleResolver.PathWithin(root, from);
                }
            }

            int traced = 0;
            foreach (VrcFuryControllerEntry entry in data.Controllers)
            {
                if (entry.Type != VrcFuryControllerEntry.TypeFx)
                {
                    continue;
                }

                AnimatorController controller = ToggleResolver.LoadController(entry.Controller.Guid);
                if (controller == null)
                {
                    result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                        "vrcfury.fullController.assetMissing",
                        $"A VRCFury Full Controller names an FX controller that is not in this "
                        + $"project: {entry.Controller.Guid}."));
                    continue;
                }

                foreach (ResolvedToggle toggle in ToggleResolver.Resolve(inventory, controller))
                {
                    foreach (ResolvedChoice choice in toggle.Choices)
                    {
                        choice.Effects = Rewrite(
                            ModularAvatarToggleResolver.Rebase(choice.Effects, prefix), data.RewriteBindings);
                    }

                    if (data.IgnoreSaved)
                    {
                        toggle.Saved = false;
                    }

                    traced++;
                    result.Resolved.Add(new VrcFuryToggle { Toggle = toggle, Source = source });
                }
            }

            if (traced == 0 && inventory.ControlCount > 0)
            {
                result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Dropped,
                    "vrcfury.fullController.untraced",
                    $"A VRCFury Full Controller installs {inventory.ControlCount} menu controls, "
                    + "and none traced to a toggle layer in its FX controller."));
            }
        }

        /// <summary>VRCFury's binding rewrites: a leading path replaced, or a path dropped.</summary>
        private static ClipEffects Rewrite(ClipEffects effects, List<VrcFuryBindingRewrite> rewrites)
        {
            if (rewrites.Count == 0)
            {
                return effects;
            }

            string Apply(string path)
            {
                foreach (VrcFuryBindingRewrite rewrite in rewrites)
                {
                    if (string.IsNullOrEmpty(rewrite.From))
                    {
                        continue;
                    }

                    if (path == rewrite.From || path.StartsWith(rewrite.From + "/"))
                    {
                        if (rewrite.Delete)
                        {
                            return null;
                        }

                        return rewrite.To + path.Substring(rewrite.From.Length);
                    }
                }

                return path;
            }

            effects.Activated = Rewritten(effects.Activated, Apply);
            effects.Deactivated = Rewritten(effects.Deactivated, Apply);
            effects.BlendShapes.RemoveAll(effect => (effect.Path = Apply(effect.Path)) == null);
            effects.MaterialProperties.RemoveAll(effect => (effect.Path = Apply(effect.Path)) == null);
            effects.ComponentEnables.RemoveAll(effect => (effect.Path = Apply(effect.Path)) == null);
            return effects;
        }

        private static List<string> Rewritten(List<string> paths, System.Func<string, string> apply)
        {
            List<string> kept = new List<string>();
            foreach (string path in paths)
            {
                string rewritten = apply(path);
                if (rewritten != null)
                {
                    kept.Add(rewritten);
                }
            }

            return kept;
        }

        /// <summary>The item's own name: the last segment of a menu path.</summary>
        private static string MenuNameOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        private static void Count(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }
    }
}
