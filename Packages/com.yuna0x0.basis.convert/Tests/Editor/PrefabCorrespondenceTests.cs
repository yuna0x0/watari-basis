using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A prefab instance whose scene adds or removes children no longer has the asset's
    /// sibling indices. Each asset transform is found through the instance's prefab links.
    /// </summary>
    public class PrefabCorrespondenceTests
    {
        private const string Folder = "Assets/WatariCorrespondenceTest";

        private const string DynamicBoneFixture =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/DynamicBoneChain.prefab";

        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
                _instance = null;
            }

            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        /// <summary>Root with children A and B, and C under B.</summary>
        private static GameObject SavePrefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "WatariCorrespondenceTest");
            }

            GameObject root = new GameObject("Root");
            new GameObject("A").transform.SetParent(root.transform, false);
            GameObject b = new GameObject("B");
            b.transform.SetParent(root.transform, false);
            new GameObject("C").transform.SetParent(b.transform, false);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Root.prefab");
            Object.DestroyImmediate(root);
            return asset;
        }

        [Test]
        public void AChildAddedAheadOfThePrefabsOwnIsSkipped()
        {
            GameObject asset = SavePrefab();
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            GameObject added = new GameObject("Added");
            added.transform.SetParent(_instance.transform, false);
            added.transform.SetSiblingIndex(0);

            PrefabCorrespondence.Outcome outcome = PrefabCorrespondence.Find(
                _instance.transform, asset.transform, asset.transform.Find("B/C"), out Transform live);

            Assert.That(outcome, Is.EqualTo(PrefabCorrespondence.Outcome.Found));
            Assert.That(live, Is.SameAs(_instance.transform.Find("B/C")));
        }

        [Test]
        public void AChildRemovedOnTheInstanceIsReportedAndItsSiblingsStillFound()
        {
            GameObject asset = SavePrefab();
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Object.DestroyImmediate(_instance.transform.Find("A").gameObject);

            Assert.That(PrefabCorrespondence.Find(_instance.transform, asset.transform,
                    asset.transform.Find("A"), out _),
                Is.EqualTo(PrefabCorrespondence.Outcome.Removed));

            PrefabCorrespondence.Outcome outcome = PrefabCorrespondence.Find(
                _instance.transform, asset.transform, asset.transform.Find("B"), out Transform live);
            Assert.That(outcome, Is.EqualTo(PrefabCorrespondence.Outcome.Found));
            Assert.That(live.name, Is.EqualTo("B"));
        }

        [Test]
        public void AHierarchyThatIsNotAnInstanceHasNoLink()
        {
            GameObject asset = SavePrefab();
            _instance = Object.Instantiate(asset);

            Assert.That(PrefabCorrespondence.Find(_instance.transform, asset.transform,
                    asset.transform.Find("B"), out _),
                Is.EqualTo(PrefabCorrespondence.Outcome.NoLink));
        }

        [Test]
        public void ARigLandsOnItsOwnBoneWhenTheSceneAddsAChildAhead()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(DynamicBoneFixture);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(DynamicBoneFixture));
            GameObject added = new GameObject("Added");
            added.transform.SetParent(_instance.transform, false);
            added.transform.SetSiblingIndex(0);

            ConversionResult result = AvatarConverter.Apply(plan, _instance);

            Assert.That(result.RigsWritten, Is.EqualTo(1));
            JiggleRigData data = _instance.GetComponentInChildren<JiggleRig>(true).GetJiggleRigData();
            Assert.That(data.rootBone.name, Is.EqualTo("Bone1"));
            Assert.That(data.jiggleColliders[0].transform.name, Is.EqualTo("Collider"));
        }

        [Test]
        public void ARigWhoseRootBoneWasRemovedIsNotWrittenOntoASibling()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(DynamicBoneFixture);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(DynamicBoneFixture));
            Object.DestroyImmediate(_instance.transform.Find("Bone1").gameObject);

            ConversionResult result = AvatarConverter.Apply(plan, _instance);

            Assert.That(result.RigsWritten, Is.Zero);
            Assert.That(_instance.GetComponentsInChildren<JiggleRig>(true), Is.Empty);
        }
    }
}
