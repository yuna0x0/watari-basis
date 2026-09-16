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
            if (sceneOnly > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "source.sceneOnly",
                    $"{sceneOnly} components with missing scripts exist only on the scene object, not in the prefab file that is read. Apply them to the prefab where their scripts are installed, then export again.");
            }
        }
    }
}
