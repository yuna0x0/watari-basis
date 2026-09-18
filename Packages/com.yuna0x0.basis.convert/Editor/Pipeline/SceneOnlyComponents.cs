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
        /// <param name="sceneRead">
        /// Whether the saved scene file was read. When it was, what exists only in the scene
        /// came from there and is reported as read rather than as missing.
        /// </param>
        public static void Report(
            AvatarConversionPlan plan, GameObject hierarchyRoot, List<ConversionSource> sources,
            bool sceneRead)
        {
            if (hierarchyRoot == null)
            {
                return;
            }

            bool rootLinked = PrefabUtility.IsPartOfPrefabAsset(hierarchyRoot)
                || PrefabUtility.IsPartOfPrefabInstance(hierarchyRoot);

            if (!rootLinked && sources.Count > 0 && !sceneRead)
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

            if (sceneRead)
            {
                if (plan.SceneComponentsRead > 0)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "source.scene",
                        $"{sceneOnly} components exist only in the scene, not in a prefab file, so they were read from the saved scene: {plan.SceneComponentsRead} recognised.");
                }
                else
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.sceneOnly",
                        $"{sceneOnly} components with missing scripts exist only in the scene, and the saved scene holds no data for them. Unpacking a prefab here drops that data; a scene saved where the scripts existed keeps it.");
                }

                return;
            }

            // An avatar placed straight from its FBX is an instance of the model file, which
            // holds the mesh and the skeleton and never a component. Everything on it lives in
            // the scene alone, and the scene could not be read.
            string model = ModelAssetPathOf(hierarchyRoot);
            if (model != null)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.modelInstance",
                    $"{System.IO.Path.GetFileName(model)} is a model file and carries no components, so the {sceneOnly} components with missing scripts exist only in the scene, which could not be read. Save the scene and rescan.");
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.sceneOnly",
                $"{sceneOnly} components with missing scripts exist only in the scene, not in a prefab file, and the scene could not be read. Save the scene and rescan.");
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
