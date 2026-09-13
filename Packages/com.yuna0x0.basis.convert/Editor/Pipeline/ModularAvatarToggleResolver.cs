using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>A menu toggle Modular Avatar would install, and the prefab it came from.</summary>
    public sealed class ModularAvatarToggle
    {
        public ResolvedToggle Toggle;

        /// <summary>The prefab the toggle and everything it switches belong to.</summary>
        public ConversionSource Source;
    }

    /// <summary>
    /// Finds the toggles a piece of clothing installs through Modular Avatar.
    /// <para>
    /// Modular Avatar's hierarchy components work on Basis, but these two do not: a menu item
    /// targets VRChat's expression menu and a merged animator targets its animator layer slots,
    /// neither of which Basis has. Read together they describe a toggle completely, which is all
    /// a Vixxy control needs.
    /// </para>
    /// <para>
    /// A merged animator addresses objects relative to the object holding it, so its clip paths
    /// are rebased onto that object before anything is resolved.
    /// </para>
    /// </summary>
    public static class ModularAvatarToggleResolver
    {
        /// <summary>Paths in a merged animator's clips are relative to the component's object.</summary>
        private const int PathModeRelative = 0;

        public static List<ModularAvatarToggle> Resolve(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            ConversionSource source)
        {
            List<ModularAvatarToggle> resolved = new List<ModularAvatarToggle>();
            if (documents == null || resolver == null || source?.Root == null)
            {
                return resolved;
            }

            List<MergedAnimator> animators = ReadAnimators(documents, resolver, source);
            List<MaMenuItemData> items = ReadMenuItems(documents);

            // A merged animator is only needed by the toggles that have one. An Object Toggle
            // describes what it switches by itself, so requiring an animator here missed them.
            if (items.Count == 0)
            {
                return resolved;
            }

            // Entries are grouped by parameter for the same reason the avatar's own menu is:
            // several of them commonly share one and pick different values from it.
            Dictionary<string, List<MaMenuItemData>> byParameter =
                new Dictionary<string, List<MaMenuItemData>>();

            foreach (MaMenuItemData item in items)
            {
                if (!item.IsToggle)
                {
                    continue;
                }

                // Modular Avatar assigns a parameter to a toggle that names none, from the
                // object's name (ParameterAssignerPass, AUTOMATIC_PARAMETER_PREFIX).
                if (string.IsNullOrEmpty(item.Parameter)
                    && resolver.TryResolveTransform(item.OwnerGameObjectFileId, out Transform owner))
                {
                    item.Parameter = AutomaticParameterPrefix + owner.name;
                }

                if (string.IsNullOrEmpty(item.Parameter))
                {
                    continue;
                }

                if (!byParameter.TryGetValue(item.Parameter, out List<MaMenuItemData> shared))
                {
                    shared = new List<MaMenuItemData>();
                    byParameter[item.Parameter] = shared;
                }

                shared.Add(item);
            }

            // An Object Toggle needs no animator at all: the menu item makes its object active,
            // and the component lists what that switches. Read first, so a parameter covered
            // this way is not looked for in a merged animator.
            ReadObjectToggles(documents, resolver, source, byParameter, resolved);

            foreach (KeyValuePair<string, List<MaMenuItemData>> group in byParameter)
            {
                if (Already(resolved, group.Key))
                {
                    continue;
                }

                foreach (MergedAnimator animator in animators)
                {
                    List<FxToggleLayer> layers = FxControllerReader.FindToggleLayers(
                        animator.Controller, new[] {group.Key});

                    if (layers.Count == 0)
                    {
                        continue;
                    }

                    FxToggleLayer layer = layers[0];
                    ResolvedToggle toggle = new ResolvedToggle
                    {
                        MenuName = NameOf(group.Value, group.Key, resolver),
                        Parameter = group.Key,
                        LayerName = layer.LayerName,
                    };

                    toggle.GuardedBy.AddRange(layer.GuardedBy);

                    foreach (FxParameterState state in layer.States)
                    {
                        toggle.Choices.Add(new ResolvedChoice
                        {
                            Name = ChoiceName(group.Value, state.Value, layer),
                            Value = state.Value,
                            Effects = Rebase(ToggleResolver.EffectsOf(state), animator.Prefix),
                            Clip = state.Clip,
                        });
                    }

                    resolved.Add(new ModularAvatarToggle {Source = source, Toggle = toggle});
                    break;
                }
            }

            return resolved;
        }

        /// <summary>Modular Avatar's prefix for parameters it assigns at build time.</summary>
        public const string AutomaticParameterPrefix = "__MA/AutoParam/";

        private static bool Already(List<ModularAvatarToggle> resolved, string parameter)
        {
            foreach (ModularAvatarToggle toggle in resolved)
            {
                if (toggle.Toggle.Parameter == parameter)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Turns an Object Toggle into a control, when a menu item on the same object drives it.
        /// <para>
        /// The component switches objects while its own object is active, and a menu item on
        /// that object is what makes it active, so the two together say the same thing a toggle
        /// and its clips say. What the component does not name keeps the state the avatar was
        /// authored with, which is the same rule everywhere else.
        /// </para>
        /// </summary>
        private static void ReadObjectToggles(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            ConversionSource source, Dictionary<string, List<MaMenuItemData>> byParameter,
            List<ModularAvatarToggle> resolved)
        {
            foreach (UnityYamlDocument document in documents)
            {
                if (!IsKind(document, SourceComponentKind.MaObjectToggle))
                {
                    continue;
                }

                MaObjectToggleData data =
                    ModularAvatarDocumentReader.ReadObjectToggle(document);

                if (data.Objects.Count == 0)
                {
                    continue;
                }

                MaMenuItemData item = MenuItemOn(byParameter, data.OwnerGameObjectFileId);
                if (item == null)
                {
                    continue;
                }

                ResolvedToggle toggle = new ResolvedToggle
                {
                    MenuName = MenuNameOf(item, resolver),
                    Parameter = item.Parameter,
                    LayerName = "Object Toggle",
                    Saved = item.IsSaved,
                    NetworkSynced = item.IsSynced,
                    // ParameterAssignerPass: a lone default item with an automatic value starts
                    // on; a default item with an authored value starts at that value.
                    DefaultValue = item.IsDefault ? (item.AutomaticValue ? 1f : item.Value) : 0f,
                };

                // Inverted means the component acts while the menu item is off, and leaves the
                // scene state alone while it is on (ReactionRule.InitiallyActive ^ Inverted).
                ClipEffects acting = new ClipEffects();
                foreach (MaToggledObject switched in data.Objects)
                {
                    string path = PathOf(switched, resolver, source);
                    if (path == null)
                    {
                        continue;
                    }

                    if (switched.Active)
                    {
                        acting.Activated.Add(path);
                    }
                    else
                    {
                        acting.Deactivated.Add(path);
                    }
                }

                if (data.Inverted)
                {
                    toggle.Choices.Add(new ResolvedChoice {Name = "OFF", Value = 0, Effects = acting});
                    toggle.Choices.Add(new ResolvedChoice {Name = "ON", Value = 1});
                }
                else
                {
                    toggle.Choices.Add(new ResolvedChoice {Name = "OFF", Value = 0});
                    toggle.Choices.Add(new ResolvedChoice {Name = "ON", Value = 1, Effects = acting});
                }

                resolved.Add(new ModularAvatarToggle {Source = source, Toggle = toggle});
            }
        }

        /// <summary>Modular Avatar shows the label, else the object's name (ModularAvatarMenuItem).</summary>
        private static string MenuNameOf(MaMenuItemData item, PrefabObjectResolver resolver)
        {
            if (!string.IsNullOrEmpty(item.Label))
            {
                return item.Label;
            }

            if (resolver.TryResolveTransform(item.OwnerGameObjectFileId, out Transform owner))
            {
                return owner.name;
            }

            return string.IsNullOrEmpty(item.Name) ? item.Parameter : item.Name;
        }

        /// <summary>
        /// The path of a toggled object within this prefab. Modular Avatar prefers the object
        /// reference, then a path from the avatar root. Inside a prefab that path starts with the
        /// prefab's own root name when the outfit was authored under an avatar.
        /// </summary>
        private static string PathOf(
            MaToggledObject switched, PrefabObjectResolver resolver, ConversionSource source)
        {
            Transform root = source.Root.transform;

            if (switched.TargetObjectFileId != 0L
                && resolver.TryResolveTransform(switched.TargetObjectFileId, out Transform target)
                && target.IsChildOf(root))
            {
                return target == root ? null : PathWithin(root, target);
            }

            string path = switched.Path;
            if (string.IsNullOrEmpty(path) || path == "$$$AVATAR_ROOT$$$")
            {
                return null;
            }

            if (root.Find(path) != null)
            {
                return path;
            }

            string prefix = root.name + "/";
            if (path.StartsWith(prefix) && root.Find(path.Substring(prefix.Length)) != null)
            {
                return path.Substring(prefix.Length);
            }

            return path;
        }

        private static MaMenuItemData MenuItemOn(
            Dictionary<string, List<MaMenuItemData>> byParameter, long ownerFileId)
        {
            foreach (KeyValuePair<string, List<MaMenuItemData>> group in byParameter)
            {
                foreach (MaMenuItemData item in group.Value)
                {
                    if (item.OwnerGameObjectFileId == ownerFileId)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private sealed class MergedAnimator
        {
            public AnimatorController Controller;

            /// <summary>Path of the object holding the component, within its own prefab.</summary>
            public string Prefix = string.Empty;
        }

        private static List<MergedAnimator> ReadAnimators(
            List<UnityYamlDocument> documents, PrefabObjectResolver resolver,
            ConversionSource source)
        {
            List<MergedAnimator> animators = new List<MergedAnimator>();

            foreach (UnityYamlDocument document in documents)
            {
                if (!IsKind(document, SourceComponentKind.MaMergeAnimator))
                {
                    continue;
                }

                MaMergeAnimatorData data =
                    ModularAvatarDocumentReader.ReadMergeAnimator(document);

                AnimatorController controller = ToggleResolver.LoadController(data.ControllerGuid);
                if (controller == null)
                {
                    continue;
                }

                string prefix = string.Empty;
                if (data.PathMode == PathModeRelative
                    && resolver.TryResolveTransform(data.OwnerGameObjectFileId,
                        out Transform host))
                {
                    prefix = PathWithin(source.Root.transform, host);
                }

                animators.Add(new MergedAnimator { Controller = controller, Prefix = prefix });
            }

            return animators;
        }

        private static List<MaMenuItemData> ReadMenuItems(List<UnityYamlDocument> documents)
        {
            List<MaMenuItemData> items = new List<MaMenuItemData>();

            foreach (UnityYamlDocument document in documents)
            {
                if (IsKind(document, SourceComponentKind.MaMenuItem))
                {
                    items.Add(ModularAvatarDocumentReader.ReadMenuItem(document));
                }
            }

            return items;
        }

        /// <summary>
        /// A stripped document is excluded: it is a back reference to a component defined in a
        /// prefab this one is built from and holds none of its data, which is read from that
        /// file instead.
        /// </summary>
        private static bool IsKind(UnityYamlDocument document, SourceComponentKind kind)
        {
            return !document.Stripped
                && document.ClassId == UnityYamlScanner.ClassIdMonoBehaviour
                && document.TryGetScriptIdentity(out string guid, out long fileId)
                && KnownScriptIdentities.Resolve(guid, fileId) == kind;
        }

        /// <summary>
        /// A menu item with no label of its own is named after the object carrying it. Several
        /// items sharing a parameter name the choices instead, so the parameter names the
        /// control.
        /// </summary>
        private static string NameOf(
            List<MaMenuItemData> items, string parameter, PrefabObjectResolver resolver)
        {
            if (items.Count != 1)
            {
                return parameter;
            }

            MaMenuItemData item = items[0];
            if (!string.IsNullOrEmpty(item.Name))
            {
                return item.Name;
            }

            return resolver.TryResolveTransform(item.OwnerGameObjectFileId, out Transform host)
                ? host.name
                : parameter;
        }

        private static string ChoiceName(
            List<MaMenuItemData> items, int value, FxToggleLayer layer)
        {
            foreach (MaMenuItemData item in items)
            {
                if (Mathf.RoundToInt(item.Value) == value && !string.IsNullOrEmpty(item.Name))
                {
                    return item.Name;
                }
            }

            if (!layer.IsSelector)
            {
                return value == 0 ? "OFF" : "ON";
            }

            return $"{layer.Parameter} {value}";
        }

        private static string PathWithin(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform walk = target.parent;

            while (walk != null && walk != root)
            {
                path = walk.name + "/" + path;
                walk = walk.parent;
            }

            return walk == root ? path : string.Empty;
        }

        /// <summary>
        /// Moves a clip's paths from the object the animator was merged onto into the prefab's
        /// own space, which is where everything else in this source is addressed from.
        /// </summary>
        private static ClipEffects Rebase(ClipEffects effects, string prefix)
        {
            if (effects == null || string.IsNullOrEmpty(prefix))
            {
                return effects;
            }

            for (int i = 0; i < effects.Activated.Count; i++)
            {
                effects.Activated[i] = Join(prefix, effects.Activated[i]);
            }

            for (int i = 0; i < effects.Deactivated.Count; i++)
            {
                effects.Deactivated[i] = Join(prefix, effects.Deactivated[i]);
            }

            for (int i = 0; i < effects.AnimatedRotationPaths.Count; i++)
            {
                effects.AnimatedRotationPaths[i] =
                    Join(prefix, effects.AnimatedRotationPaths[i]);
            }

            foreach (BlendShapeEffect shape in effects.BlendShapes)
            {
                shape.Path = Join(prefix, shape.Path);
            }

            foreach (MaterialPropertyEffect material in effects.MaterialProperties)
            {
                material.Path = Join(prefix, material.Path);
            }

            return effects;
        }

        private static string Join(string prefix, string path)
        {
            return string.IsNullOrEmpty(path) ? prefix : prefix + "/" + path;
        }
    }
}
