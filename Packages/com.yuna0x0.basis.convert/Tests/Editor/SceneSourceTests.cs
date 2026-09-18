using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Components added in the scene rather than in a prefab used to be invisible: their
    /// scripts are missing, so nothing can be read live, and no prefab file holds them. The
    /// saved scene file does. An avatar dragged in from its FBX with everything added on the
    /// instance is the common shape.
    /// </summary>
    public class SceneSourceTests
    {
        private const string SamplePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleAvatar.prefab";

        private const string VrcFuryPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFury.prefab";

        private const string ModelPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleModel.obj";

        private const string ScenePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SceneSourceTemp.unity";

        private Scene _scene;
        private bool _additive;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.Saveable(out _additive);
        }

        [TearDown]
        public void TearDown()
        {
            TestScenes.Release(_scene, _additive);
            AssetDatabase.DeleteAsset(ScenePath);
        }

        private GameObject Place(string assetPath)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            return instance;
        }

        [Test]
        public void AnUnsavedSceneIsNamedAndNothingIsRead()
        {
            GameObject avatar = Place(SamplePath);
            PrefabUtility.UnpackPrefabInstance(
                avatar, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            Assert.That(plan.Diagnostics.HasCode("scene.unsaved"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("avatar.noPrefab"), Is.True);
            Assert.That(plan.PhysBonesFound, Is.EqualTo(0));
        }

        /// <summary>
        /// A scene whose file carries the components: what a scene holds after an avatar was
        /// assembled in it where the scripts existed and then brought here. Written from the
        /// sample prefab's own documents, since instantiating and unpacking a prefab whose
        /// scripts are missing drops their data before a save.
        /// </summary>
        [Test]
        public void AnAvatarWhoseComponentsLiveOnlyInTheSceneIsReadFromItsFile()
        {
            string prefabText = File.ReadAllText(SamplePath);
            int body = prefabText.IndexOf("--- !u!", System.StringComparison.Ordinal);
            File.WriteAllText(ScenePath,
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + prefabText.Substring(body));
            AssetDatabase.ImportAsset(ScenePath);

            TestScenes.Release(_scene, _additive);
            _scene = TestScenes.Open(ScenePath, out _additive);

            GameObject avatar = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.name == "SampleAvatar")
                {
                    avatar = root;
                }
            }

            Assert.That(avatar, Is.Not.Null, "the scene loaded the prefab's documents as scene objects");
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(avatar), Is.False);

            AvatarConversionPlan reference = AvatarConversionPlanner.Plan(SamplePath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            string state = $"read={plan.SceneComponentsRead} sceneOnly={plan.SceneOnlyMissingScripts} "
                + "diagnostics: " + string.Join("; ", plan.Diagnostics);

            Assert.That(plan.Diagnostics.HasCode("avatar.noPrefab"), Is.False, state);
            Assert.That(plan.Diagnostics.HasCode("scene.unsaved"), Is.False, state);
            Assert.That(plan.Diagnostics.HasCode("scene.dirty"), Is.False, state);
            Assert.That(plan.Diagnostics.HasCode("source.scene"), Is.True, state);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.False, state);
            Assert.That(plan.PhysBonesFound, Is.EqualTo(reference.PhysBonesFound), state);
            Assert.That(plan.PhysBonesFound, Is.GreaterThan(0));
            Assert.That(plan.CollidersFound, Is.EqualTo(reference.CollidersFound));
            Assert.That(plan.Rigs.Count, Is.EqualTo(reference.Rigs.Count));
            Assert.That(plan.Sources.Count, Is.EqualTo(1));
            Assert.That(plan.Sources[0].IsScene, Is.True);

            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                Assert.That(rig.SourceRootBone, Is.Not.Null);
                Assert.That(rig.SourceRootBone.IsChildOf(avatar.transform), Is.True,
                    "a scene source resolves onto the live scene objects");
                Assert.That(rig.Source.IsScene, Is.True);
            }
        }

        [Test]
        public void ALinkedInstanceInASavedSceneStillReadsFromItsPrefab()
        {
            GameObject avatar = Place(SamplePath);
            Assert.That(EditorSceneManager.SaveScene(_scene, ScenePath), Is.True);

            AvatarConversionPlan reference = AvatarConversionPlanner.Plan(SamplePath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            Assert.That(plan.PhysBonesFound, Is.EqualTo(reference.PhysBonesFound));
            Assert.That(plan.SceneComponentsRead, Is.EqualTo(0));
            Assert.That(plan.Diagnostics.HasCode("source.scene"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.False);
            Assert.That(plan.Sources.Count, Is.EqualTo(2), "the prefab and the scene");
            Assert.That(plan.Sources[0].IsScene, Is.False, "the prefab stays the primary source");
        }

        /// <summary>
        /// A prefab instance's components appear in the scene file only as stubs, which the
        /// readers skip, so nothing is read twice: the toggle and link counts of an instance in
        /// a saved scene equal those of the prefab alone.
        /// </summary>
        [Test]
        public void AVrcFuryInstanceInASavedSceneIsNotReadTwice()
        {
            GameObject avatar = Place(VrcFuryPath);
            Assert.That(EditorSceneManager.SaveScene(_scene, ScenePath), Is.True);

            AvatarConversionPlan reference = AvatarConversionPlanner.Plan(VrcFuryPath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            Assert.That(plan.VrcFury.Components, Is.EqualTo(reference.VrcFury.Components));
            Assert.That(plan.VrcFury.Toggles, Is.EqualTo(reference.VrcFury.Toggles));
            Assert.That(plan.VrcFury.ArmatureLinks, Is.EqualTo(reference.VrcFury.ArmatureLinks));
            Assert.That(plan.VixxyControls.Count, Is.EqualTo(reference.VixxyControls.Count));
            Assert.That(plan.VixxyControls.Count, Is.GreaterThan(0));
            Assert.That(plan.SceneComponentsRead, Is.EqualTo(0));
        }

        /// <summary>
        /// The same components living only in the scene file are read once, from there, and
        /// yield the same controls as the prefab does.
        /// </summary>
        [Test]
        public void VrcFuryComponentsLivingOnlyInTheSceneYieldTheSameControls()
        {
            string prefabText = File.ReadAllText(VrcFuryPath);
            int body = prefabText.IndexOf("--- !u!", System.StringComparison.Ordinal);
            File.WriteAllText(ScenePath,
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + prefabText.Substring(body));
            AssetDatabase.ImportAsset(ScenePath);

            TestScenes.Release(_scene, _additive);
            _scene = TestScenes.Open(ScenePath, out _additive);

            GameObject avatar = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.name == "SampleVrcFury")
                {
                    avatar = root;
                }
            }

            Assert.That(avatar, Is.Not.Null);

            AvatarConversionPlan reference = AvatarConversionPlanner.Plan(VrcFuryPath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);
            string state = "diagnostics: " + string.Join("; ", plan.Diagnostics);

            Assert.That(plan.VrcFury.Components, Is.EqualTo(reference.VrcFury.Components), state);
            Assert.That(plan.VrcFury.Toggles, Is.EqualTo(reference.VrcFury.Toggles), state);
            Assert.That(plan.VixxyControls.Count, Is.EqualTo(reference.VixxyControls.Count), state);
            Assert.That(plan.VixxyControls.Count, Is.GreaterThan(0));
            Assert.That(plan.SceneComponentsRead, Is.EqualTo(reference.VrcFury.Components), state);
        }

        /// <summary>
        /// A model file placed in the scene and a PhysBone added on that instance. The scene file holds the PhysBone as an added component whose owner and
        /// root transform are stubs pointing into the model. A PhysBone document from the sample
        /// prefab is written into the saved scene by hand, since no VRChat script can be added
        /// here, and the stubs are written the way Unity writes them.
        /// </summary>
        [Test]
        public void AComponentAddedOnAModelInstanceIsReadFromTheScene()
        {
            GameObject root = Place(ModelPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(root), Is.EqualTo(PrefabAssetType.Model));
            Assert.That(EditorSceneManager.SaveScene(_scene, ScenePath), Is.True);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                modelAsset, out string modelGuid, out long modelObjectId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                modelAsset.transform, out string _, out long modelTransformId), Is.True);

            string sceneText = File.ReadAllText(ScenePath);
            Match instance = Regex.Match(sceneText, @"^--- !u!1001 &(?<id>-?\d+)", RegexOptions.Multiline);
            Assert.That(instance.Success, Is.True, "the saved scene holds the model's PrefabInstance");
            string instanceId = instance.Groups["id"].Value;

            UnityYamlDocument physBone = null;
            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(SamplePath))
            {
                if (document.FileId == 400002L)
                {
                    physBone = document;
                }
            }

            Assert.That(physBone, Is.Not.Null, "the sample's first PhysBone document");

            StringBuilder added = new StringBuilder();
            added.AppendLine("--- !u!1 &9001 stripped");
            added.AppendLine("GameObject:");
            added.AppendLine($"  m_CorrespondingSourceObject: {{fileID: {modelObjectId}, guid: {modelGuid}, type: 3}}");
            added.AppendLine($"  m_PrefabInstance: {{fileID: {instanceId}}}");
            added.AppendLine("  m_PrefabAsset: {fileID: 0}");
            added.AppendLine("--- !u!4 &9002 stripped");
            added.AppendLine("Transform:");
            added.AppendLine($"  m_CorrespondingSourceObject: {{fileID: {modelTransformId}, guid: {modelGuid}, type: 3}}");
            added.AppendLine($"  m_PrefabInstance: {{fileID: {instanceId}}}");
            added.AppendLine("  m_PrefabAsset: {fileID: 0}");
            added.AppendLine("--- !u!114 &9003");
            added.AppendLine("MonoBehaviour:");
            foreach (string line in physBone.Lines)
            {
                string written = line;
                if (line.TrimStart().StartsWith("m_GameObject:"))
                {
                    written = "  m_GameObject: {fileID: 9001}";
                }
                else if (line.TrimStart().StartsWith("rootTransform:"))
                {
                    written = "  rootTransform: {fileID: 9002}";
                }

                added.AppendLine(written);
            }

            File.WriteAllText(ScenePath, sceneText.TrimEnd('\n', '\r') + "\n" + added);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(root);

            Assert.That(plan.Diagnostics.HasCode("source.modelInstance"), Is.False);
            Assert.That(plan.PhysBonesFound, Is.EqualTo(1));
            Assert.That(plan.SceneComponentsRead, Is.EqualTo(1));
            Assert.That(plan.Rigs.Count, Is.EqualTo(1));
            Assert.That(plan.Rigs[0].SourceRootBone, Is.EqualTo(root.transform),
                "the stub for the model's transform resolves to the live instance");
        }
    }
}
