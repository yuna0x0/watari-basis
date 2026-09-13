using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A variant stores only what it overrides, so reading its file alone finds none of the
    /// physics it inherits.
    /// <para>
    /// The fixtures are built here because neither shortcut works: Unity will not save a prefab
    /// holding a missing script, which `SampleAvatar` is made of, and a hand-written variant
    /// file does not load. So both prefabs are made with the API while the base is still plain,
    /// and the PhysBone is appended to the base afterwards.
    /// </para>
    /// </summary>
    public class PrefabVariantTests
    {
        private const string PlainFixturePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleAvatar.prefab";

        private const string Folder = "Assets/WatariVariantTests";

        private const string PhysBoneScript =
            "  m_Script: {fileID: 1661641543, guid: 2a2c05204084d904aa4945ccff20d8e5, type: 3}";

        private string _basePath;
        private string _variantPath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "WatariVariantTests");
            }

            GameObject root = new GameObject("VariantTestBase");
            GameObject chain = new GameObject("Chain");
            chain.transform.SetParent(root.transform, false);
            new GameObject("ChainEnd").transform.SetParent(chain.transform, false);

            _basePath = Path.Combine(Folder, "VariantTestBase.prefab");
            GameObject baseAsset = PrefabUtility.SaveAsPrefabAsset(root, _basePath);
            Object.DestroyImmediate(root);

            // Saving an instance of a prefab under a new path is what makes a variant of it.
            // Done while the base is still plain, since a missing script would block the save.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
            _variantPath = Path.Combine(Folder, "VariantTestVariant.prefab");
            PrefabUtility.SaveAsPrefabAsset(instance, _variantPath);
            Object.DestroyImmediate(instance);

            AddPhysBoneToBase();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        /// <summary>
        /// Appends a PhysBone to the base prefab's file, rooted at its Chain object. It arrives
        /// as a missing script, which is exactly how a VRChat component reaches a Basis project
        /// and why the pipeline reads the file rather than the objects.
        /// </summary>
        private void AddPhysBoneToBase()
        {
            GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_basePath);
            Transform chain = baseAsset.transform.Find("Chain");
            Assert.That(chain, Is.Not.Null);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                chain.gameObject, out string _, out long chainGameObjectId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                chain, out string _, out long chainTransformId);

            const long physBoneId = 8100000000000000001L;

            File.WriteAllText(_basePath, AddComponentTo(
                File.ReadAllText(_basePath), chainGameObjectId, physBoneId));

            File.AppendAllText(_basePath,
                $"--- !u!114 &{physBoneId}\n"
                + "MonoBehaviour:\n"
                + "  m_ObjectHideFlags: 0\n"
                + "  m_CorrespondingSourceObject: {fileID: 0}\n"
                + "  m_PrefabInstance: {fileID: 0}\n"
                + "  m_PrefabAsset: {fileID: 0}\n"
                + $"  m_GameObject: {{fileID: {chainGameObjectId}}}\n"
                + "  m_Enabled: 1\n"
                + "  m_EditorHideFlags: 0\n"
                + PhysBoneScript + "\n"
                + "  m_Name:\n"
                + "  m_EditorClassIdentifier:\n"
                + "  version: 1\n"
                + "  integrationType: 0\n"
                + $"  rootTransform: {{fileID: {chainTransformId}}}\n"
                + "  ignoreTransforms: []\n"
                + "  endpointPosition: {x: 0, y: 0, z: 0}\n"
                + "  multiChildType: 0\n"
                + "  pull: 0.3\n"
                + "  spring: 0.6\n"
                + "  stiffness: 0.2\n"
                + "  gravity: 0.1\n"
                + "  gravityFalloff: 0\n"
                + "  immobileType: 0\n"
                + "  immobile: 0.25\n"
                + "  limitType: 0\n"
                + "  maxAngleX: 45\n"
                + "  radius: 0.05\n"
                + "  allowCollision: 1\n"
                + "  allowGrabbing: 1\n"
                + "  allowPosing: 1\n"
                + "  isAnimated: 0\n");

            AssetDatabase.ImportAsset(_basePath, ImportAssetOptions.ForceSynchronousImport);

            Assert.That(AvatarConversionPlanner.Plan(_basePath).PhysBonesFound, Is.EqualTo(1),
                "The appended PhysBone has to be readable for these tests to mean anything.");
        }

        /// <summary>
        /// Lists the new component on the GameObject that owns it. Without this Unity treats the
        /// appended block as orphaned and drops it on import, saying the object "does not
        /// reference component MonoBehaviour".
        /// </summary>
        private static string AddComponentTo(string yaml, long gameObjectId, long componentId)
        {
            string anchor = $"--- !u!1 &{gameObjectId}\n";
            int start = yaml.IndexOf(anchor, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The Chain GameObject is not in the file.");

            int layer = yaml.IndexOf("\n  m_Layer:", start, System.StringComparison.Ordinal);
            Assert.That(layer, Is.GreaterThan(start), "The component list did not end as expected.");

            return yaml.Insert(layer, $"\n  - component: {{fileID: {componentId}}}");
        }

        [Test]
        public void AVariantNamesTheBaseItInheritsFrom()
        {
            ConversionSource source = ConversionSource.ForAsset(_variantPath);

            Assert.That(source.BaseAssetPath(), Is.EqualTo(_basePath));
            Assert.That(source.InheritedAssetPaths(), Is.EqualTo(new[] { _basePath }));
        }

        [Test]
        public void APlainPrefabNamesNoBase()
        {
            ConversionSource source = ConversionSource.ForAsset(_basePath);

            Assert.That(source.BaseAssetPath(), Is.Null);
            Assert.That(source.InheritedAssetPaths(), Is.Empty);
        }

        /// <summary>
        /// The point of the whole exercise. The variant's own file does not mention the PhysBone;
        /// it converts with it anyway, because the base is read too.
        /// </summary>
        [Test]
        public void AVariantConvertsTheComponentsItInherits()
        {
            Assert.That(File.ReadAllText(_variantPath), Does.Not.Contain(PhysBoneScript),
                "The variant's own file must not hold the PhysBone, or nothing is being tested.");

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(_variantPath);

            Assert.That(plan.PhysBonesFound, Is.EqualTo(1),
                "A variant inherits its base's PhysBones, so it has to find it.");
            Assert.That(plan.Rigs.Count, Is.EqualTo(1), "And plan a jiggle rig from it.");
            Assert.That(plan.InheritedSourcesRead, Is.EqualTo(1));
        }

        /// <summary>
        /// The rig has to land on the variant's own transform, not the base's, or applying the
        /// plan would write into the wrong asset.
        /// </summary>
        [Test]
        public void AnInheritedRigResolvesOntoTheVariantsOwnObjects()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(_variantPath);
            GameObject variantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_variantPath);

            Assert.That(plan.Rigs, Is.Not.Empty);

            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                Assert.That(rig.SourceRootBone, Is.Not.Null, "The rig root did not resolve.");
                Assert.That(rig.SourceRootBone.IsChildOf(variantAsset.transform), Is.True,
                    $"{rig.SourceRootBone.name} resolved outside the variant being converted.");
            }
        }

        /// <summary>
        /// A variant's own file holds only overrides. One on the inherited PhysBone has to reach
        /// the reader, or the variant converts with the base's numbers.
        /// </summary>
        [Test]
        public void AVariantsOverrideOnAnInheritedComponentIsApplied()
        {
            string baseGuid = AssetDatabase.AssetPathToGUID(_basePath);
            string yaml = File.ReadAllText(_variantPath);
            const string list = "    m_Modifications:\n";
            int at = yaml.IndexOf(list, System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "The variant has no modification list.");

            yaml = yaml.Insert(at + list.Length,
                $"    - target: {{fileID: 8100000000000000001, guid: {baseGuid}, type: 3}}\n"
                + "      propertyPath: pull\n"
                + "      value: 0.9\n"
                + "      objectReference: {fileID: 0}\n");
            File.WriteAllText(_variantPath, yaml);
            AssetDatabase.ImportAsset(_variantPath, ImportAssetOptions.ForceSynchronousImport);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(_variantPath);

            Assert.That(plan.Rigs.Count, Is.EqualTo(1));
            Assert.That(plan.OverridesApplied, Is.GreaterThanOrEqualTo(1));
            Assert.That(plan.Rigs[0].Plan.Parameters.Stiffness.Value.Value,
                Is.EqualTo(0.9f).Within(1e-4f),
                "pull 0.9 from the variant, not 0.3 from the base, drives the stiffness fit.");
            Assert.That(plan.Diagnostics.HasCode("source.overridesApplied"), Is.True);
        }

        [Test]
        public void OverridePathsReachNestedListEntriesAndGrowLists()
        {
            List<string> lines = new List<string>
            {
                "  m_Enabled: 1",
                "  pull: 0.3",
                "  m_shapes:",
                "  - Object:",
                "      referencePath: Body",
                "      targetObject: {fileID: 0}",
                "    ShapeName: A",
                "    ChangeType: 1",
                "    Value: 50",
                "  colliders:",
                "  - {fileID: 11}",
                "  m_threshold: 0.01",
            };

            Assert.That(PrefabOverrides.SetPath(lines, "pull", "0.9"), Is.True);
            Assert.That(lines[1], Is.EqualTo("  pull: 0.9"));

            Assert.That(PrefabOverrides.SetPath(lines, "m_shapes.Array.data[0].ShapeName", "B"), Is.True);
            Assert.That(lines[6], Is.EqualTo("    ShapeName: B"));

            Assert.That(PrefabOverrides.SetPath(
                lines, "m_shapes.Array.data[0].Object.targetObject", "{fileID: 42}"), Is.True);
            Assert.That(lines[5], Is.EqualTo("      targetObject: {fileID: 42}"));

            // Unity grows a list by repeating its last entry, then overrides fields of the copy.
            Assert.That(PrefabOverrides.SetPath(lines, "m_shapes.Array.size", "2"), Is.True);
            Assert.That(PrefabOverrides.SetPath(lines, "m_shapes.Array.data[1].ShapeName", "C"), Is.True);
            Assert.That(lines[9], Is.EqualTo("  - Object:"));
            Assert.That(lines[12], Is.EqualTo("    ShapeName: C"));
            Assert.That(lines[6], Is.EqualTo("    ShapeName: B"), "The first entry is untouched.");

            Assert.That(PrefabOverrides.SetPath(lines, "colliders.Array.data[0]", "{fileID: 99}"), Is.True);
            Assert.That(lines, Does.Contain("  - {fileID: 99}"));
        }

        [Test]
        public void AVariantIsReported()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(_variantPath);

            Assert.That(plan.Diagnostics.HasCode("source.prefabVariant"), Is.True,
                "Reading a second file is worth saying, since its objects are not in this one.");
        }

        /// <summary>
        /// An avatar prefab is normally made from an FBX, which Unity calls a variant of that
        /// model. A model holds the imported hierarchy and no authored components, so there is
        /// nothing to read and nothing to report. Found by converting a real avatar, which
        /// reported itself.
        /// </summary>
        [Test]
        public void APrefabMadeFromAModelIsNotTreatedAsAVariant()
        {
            string modelPath = ModelFixturePath();
            if (string.IsNullOrEmpty(modelPath))
            {
                Assert.Ignore("No model in this project to build a prefab from.");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            string path = Path.Combine(Folder, "FromModel.prefab");
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            Assert.That(
                PrefabUtility.GetPrefabAssetType(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)),
                Is.EqualTo(PrefabAssetType.Variant),
                "Unity calls a prefab made from a model a variant; the point of the test is that "
                + "the converter does not treat it as one.");

            ConversionSource source = ConversionSource.ForAsset(path);

            Assert.That(source.BaseAssetPath(), Is.Null);
            Assert.That(source.InheritedAssetPaths(), Is.Empty);
            Assert.That(AvatarConversionPlanner.Plan(path).Diagnostics
                .HasCode("source.prefabVariant"), Is.False);

            Assert.That(source.ModelAssetPath(), Is.EqualTo(modelPath),
                "The model is still named, so an empty conversion can say why.");
        }

        /// <summary>
        /// A prefab saved from an imported model without unpacking converts as though it were
        /// empty, because the components are in the model file and that is not read. Saying so
        /// beats the generic message, whose only suggested cause does not apply here.
        /// </summary>
        [Test]
        public void APrefabSavedFromAModelSaysItNeedsUnpacking()
        {
            string modelPath = ModelFixturePath();
            if (string.IsNullOrEmpty(modelPath))
            {
                Assert.Ignore("No model in this project to build a prefab from.");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            string path = Path.Combine(Folder, "NotUnpacked.prefab");
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(path);

            Assert.That(plan.ComponentsRead, Is.Zero,
                "Nothing is readable in a prefab that only points at a model.");
            Assert.That(plan.Diagnostics.HasCode("source.notUnpacked"), Is.True);
        }

        /// <summary>The message is for an empty conversion, not for every model-based prefab.</summary>
        [Test]
        public void APrefabThatConvertedSomethingIsNotToldToUnpack()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(PlainFixturePath);

            Assert.That(plan.ComponentsRead, Is.GreaterThan(0));
            Assert.That(plan.Diagnostics.HasCode("source.notUnpacked"), Is.False);
        }

        [Test]
        public void APlainPrefabIsNotReportedAsAVariant()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(PlainFixturePath);

            Assert.That(plan.Diagnostics.HasCode("source.prefabVariant"), Is.False,
                "The fixture is a plain prefab, not a variant.");
        }

        /// <summary>Any imported model in the project, since none ships with the package.</summary>
        private static string ModelFixturePath()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
