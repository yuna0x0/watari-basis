using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Names what a scan of a scene object cannot see. Component data is read from prefab
    /// files, so a component that exists only on the scene object, or an object that is not
    /// linked to a prefab at all, is invisible to the readers. Both look like an avatar with
    /// nothing on it, and both used to pass without a word.
    /// </summary>
    public static class SceneOnlyComponents
    {
        public static void Report(
            AvatarConversionPlan plan, GameObject hierarchyRoot, List<ConversionSource> sources)
        {
            if (hierarchyRoot == null)
            {
                return;
            }

            bool rootLinked = PrefabUtility.IsPartOfPrefabAsset(hierarchyRoot)
                || PrefabUtility.IsPartOfPrefabInstance(hierarchyRoot);

            if (!rootLinked && sources.Count > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "avatar.rootNotPrefab",
                    $"{hierarchyRoot.name} is not linked to a prefab. Only the {sources.Count} "
                    + "prefab instances beneath it were read; components on the object itself "
                    + "and on any other unlinked object were not.");
            }

            if (PrefabUtility.IsPartOfPrefabAsset(hierarchyRoot))
            {
                return;
            }

            int sceneOnly = 0;
            foreach (Transform transform in hierarchyRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject live = transform.gameObject;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(live);
                if (missing == 0)
                {
                    continue;
                }

                GameObject inPrefab = PrefabUtility.GetCorrespondingObjectFromSource(live);
                int inFile = inPrefab == null
                    ? 0
                    : GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(inPrefab);
                sceneOnly += Mathf.Max(0, missing - inFile);
            }

            plan.SceneOnlyMissingScripts = sceneOnly;
            if (sceneOnly == 0)
            {
                return;
            }

            // An avatar placed straight from its FBX is an instance of the model file, which
            // holds the mesh and the skeleton and never a component. "Apply to the prefab" is
            // not an action Unity offers there; saving a prefab is.
            string model = ModelAssetPathOf(hierarchyRoot);
            if (model != null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.modelInstance",
                    $"{System.IO.Path.GetFileName(model)} is a model file and carries no components, so the {sceneOnly} components with missing scripts exist only in the scene. Save the avatar as a prefab where its scripts are installed, and export that.");
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.sceneOnly",
                $"{sceneOnly} components with missing scripts exist only on the scene object, not in the prefab file that is read. Apply them to the prefab where their scripts are installed, then export again.");
        }

        /// <summary>
        /// The model file an instance was placed from, or null when its source is a prefab. A
        /// variant saved from a model is a prefab: its own file holds the added components.
        /// </summary>
        public static string ModelAssetPathOf(GameObject instanceRoot)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(instanceRoot))
            {
                return null;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            if (source == null || PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.Model)
            {
                return null;
            }

            return AssetDatabase.GetAssetPath(source);
        }
    }
}
