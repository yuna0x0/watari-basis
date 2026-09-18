using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Three shapes of avatar read as "humanoid rig, nothing on it" and used to pass without a
    /// word: a prefab stored in binary form, components that exist only on the scene object,
    /// and a selected object that is not linked to a prefab. A user with a binary prefab was
    /// told its descriptor did not exist. Each is named now.
    /// </summary>
    public class SourceReadabilityTests
    {
        private const string SamplePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleAvatar.prefab";

        private const string BinaryPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/BinaryPrefab.prefab";

        private const string ModelPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleModel.obj";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        // An unsaved scene of its own, so what the open scene holds cannot change what these
        // read: a saved scene is a source now.
        private UnityEngine.SceneManagement.Scene _scene;
        private bool _additive;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.Unsaved(out _additive);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            TestScenes.Release(_scene, _additive);
        }

        private GameObject Instantiate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SamplePath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            _spawned.Add(instance);
            return instance;
        }

        [Test]
        public void ABinaryPrefabIsNamedAsUnreadable()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(BinaryPath);

            Assert.That(plan.Diagnostics.HasCode("source.notText"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("avatar.notLoaded"), Is.False,
                "the file loads as a prefab; it is its text that is missing");
        }

        [Test]
        public void ATextPrefabIsNotNamedAsUnreadable()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(SamplePath);

            Assert.That(plan.Diagnostics.HasCode("source.notText"), Is.False);
        }

        [Test]
        public void ALinkedInstanceCarriesNoneOfTheWarnings()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Instantiate());

            Assert.That(plan.Diagnostics.HasCode("avatar.rootNotPrefab"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.False);
            Assert.That(plan.SceneOnlyMissingScripts, Is.EqualTo(0));
        }

        [Test]
        public void AnUnpackedInstanceHasItsComponentsNamedAsSceneOnly()
        {
            GameObject instance = Instantiate();
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(instance);

            Assert.That(plan.Diagnostics.HasCode("avatar.noPrefab"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.True);
            Assert.That(plan.SceneOnlyMissingScripts, Is.GreaterThan(0),
                "the sample's VRChat components are missing scripts, and unpacking left them "
                + "on the scene object only");
        }

        /// <summary>
        /// An avatar dragged into the scene from its FBX, with the VRChat components added on
        /// that instance: the root is a model instance, and no prefab holds the components.
        /// </summary>
        [Test]
        public void AModelInstanceWithSceneComponentsIsToldToSaveAPrefab()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(model), Is.EqualTo(PrefabAssetType.Model));
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(model, _scene);
            _spawned.Add(root);

            GameObject avatar = Instantiate();
            PrefabUtility.UnpackPrefabInstance(
                avatar, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            avatar.transform.SetParent(root.transform);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(root);

            Assert.That(plan.Diagnostics.HasCode("source.modelInstance"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.False,
                "the model message replaces the prefab one; there is no prefab to apply to");
            Assert.That(plan.SceneOnlyMissingScripts, Is.GreaterThan(0));
            Assert.That(plan.Diagnostics.HasCode("avatar.rootNotPrefab"), Is.False,
                "a model instance is a linked instance");
        }

        [Test]
        public void AnUnlinkedRootOverALinkedInstanceIsNamed()
        {
            GameObject root = new GameObject("Wrapper");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, _scene);
            _spawned.Add(root);
            Instantiate().transform.SetParent(root.transform);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(root);

            Assert.That(plan.Diagnostics.HasCode("avatar.rootNotPrefab"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.sceneOnly"), Is.False);
            Assert.That(plan.PhysBonesFound, Is.EqualTo(1), "the linked instance is still read");
        }
    }
}
