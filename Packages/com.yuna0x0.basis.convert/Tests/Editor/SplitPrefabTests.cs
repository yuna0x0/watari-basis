using System.Collections.Generic;
using System.Linq;
using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// One workflow keeps the armature and meshes clean and puts every VRChat component on a
    /// prefab of its own, nested under the avatar. The descriptor then sits on a child, and a
    /// PhysBone names its root bone through a reference the outer prefab stores as an override,
    /// pointing into the outer file. A user with exactly this layout was told the avatar had no
    /// descriptor and no PhysBones.
    /// </summary>
    public class SplitPrefabTests
    {
        private const string AvatarPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleSplitAvatar.prefab";

        private readonly List<GameObject> _spawned = new List<GameObject>();

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
        }

        private GameObject Instantiate()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath));
            _spawned.Add(instance);
            return instance;
        }

        [Test]
        public void TheDescriptorOnTheNestedPrefabIsFound()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Instantiate());

            Assert.That(plan.Sources, Has.Count.EqualTo(2));
            Assert.That(plan.Descriptor, Is.Not.Null);
            Assert.That(plan.Descriptor.SourceData, Is.Not.Null, "read from the nested prefab, not made up");
        }

        [Test]
        public void APhysBoneWhoseRootBoneIsInTheOuterPrefabIsRead()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Instantiate());

            Assert.That(plan.PhysBonesFound, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.HasCode("physbone.rootUnresolved"), Is.False,
                "the root transform is an override in the outer file, pointing at the outer file's Hips");
            Assert.That(plan.Rigs, Has.Count.EqualTo(1));
            Assert.That(plan.Rigs[0].SourceRootBone.name, Is.EqualTo("Hips"));
        }

        [Test]
        public void AColliderInTheOuterPrefabIsFoundFromTheNestedPhysBone()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Instantiate());

            Assert.That(plan.Rigs, Has.Count.EqualTo(1));
            Assert.That(plan.Rigs[0].Colliders, Has.Count.EqualTo(1));
            Assert.That(plan.Rigs[0].Colliders[0].SourceTransform.name, Is.EqualTo("Hips"));
            Assert.That(plan.Diagnostics.HasCode("physics.collider.unresolved"), Is.False);
        }

        [Test]
        public void TheDescriptorsVisemeMeshInTheOuterPrefabIsFound()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Instantiate());

            Assert.That(plan.Descriptor, Is.Not.Null);
            Assert.That(plan.Descriptor.SourceVisemeMesh, Is.Not.Null);
            Assert.That(plan.Descriptor.SourceVisemeMesh.name, Is.EqualTo("Face"));
        }

        private const string VariantPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleSplitAvatarVariant.prefab";

        private GameObject InstantiateVariant()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath));
            _spawned.Add(instance);
            return instance;
        }

        private static Transform Hips(GameObject instance) =>
            instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hips");

        /// <summary>
        /// The root bone resolved to the outer prefab's asset, not the nested one's, so the
        /// sibling-index path from the nested prefab cannot find it: it is located through the
        /// hierarchy instead.
        /// </summary>
        [Test]
        public void ARootBoneInTheOuterPrefabIsWrittenOntoTheInstance()
        {
            GameObject instance = Instantiate();
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(instance);

            ConversionResult result = AvatarConverter.Apply(plan, instance);

            Assert.That(result.RigsSkipped, Is.EqualTo(0), string.Join("; ",
                result.Diagnostics.Where(d => d.Code == "apply.unresolved").Select(d => d.Message)));
            Assert.That(result.RigsWritten, Is.EqualTo(1));
            JiggleRig rig = instance.GetComponentInChildren<JiggleRig>(true);
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.GetJiggleRigData().rootBone, Is.SameAs(Hips(instance)),
                "the instance's Hips, which the nested PhysBone's override named");
        }

        /// <summary>
        /// A variant of the split avatar: the nested PhysBone's root override sits in the base
        /// file and names the base's own Hips, which only the base file's resolver can answer.
        /// </summary>
        [Test]
        public void AVariantOfTheSplitAvatarStillFindsTheRootBoneInTheBase()
        {
            GameObject instance = InstantiateVariant();
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(instance);

            string state = "sources: " + string.Join(", ", plan.Sources.Select(s => s.Name + "@" + s.AssetPath))
                + $"; physBones {plan.PhysBonesFound}; components {plan.ComponentsRead}; overrides {plan.OverridesApplied}\n"
                + string.Join("\n", plan.AllDiagnostics().Select(d => d.Code + ": " + d.Message));
            Assert.That(plan.Diagnostics.HasCode("source.prefabVariant"), Is.True, state);
            Assert.That(plan.Diagnostics.HasCode("physbone.rootUnresolved"), Is.False, state);
            Assert.That(plan.Rigs, Has.Count.EqualTo(1), state);
            Assert.That(plan.Rigs[0].SourceRootBone.name, Is.EqualTo("Hips"), state);
            Assert.That(plan.Rigs[0].Colliders, Has.Count.EqualTo(1), state);
            Assert.That(plan.Rigs[0].Colliders[0].SourceTransform.name, Is.EqualTo("Hips"));

            ConversionResult result = AvatarConverter.Apply(plan, instance);

            Assert.That(result.RigsSkipped, Is.EqualTo(0));
            Assert.That(result.RigsWritten, Is.EqualTo(1));
            JiggleRig rig = instance.GetComponentInChildren<JiggleRig>(true);
            Assert.That(rig.GetJiggleRigData().rootBone, Is.SameAs(Hips(instance)));
        }
    }
}
