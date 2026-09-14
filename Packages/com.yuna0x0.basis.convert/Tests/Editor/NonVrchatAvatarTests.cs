using Basis.Scripts.BasisSdk;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Dynamic Bone is an ordinary Unity asset, and plenty of avatars use it with no VRChat
    /// involvement at all. Those still need converting, and still need a Basis Avatar component.
    /// </summary>
    public class NonVrchatAvatarTests
    {
        private static readonly string HumanoidModelPath =
            LocalFixtures.Find(LocalFixtures.ShinanoModel);

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
        }

        [Test]
        public void AHumanoidWithNoVrchatComponentsStillGetsABasisAvatar()
        {
            if (HumanoidModelPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoModel}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(HumanoidModelPath);

            Assert.That(plan.PhysBonesFound, Is.Zero, "A bare model has no VRChat components.");
            Assert.That(plan.ConstraintsFound, Is.Zero);
            Assert.That(plan.Descriptor, Is.Not.Null,
                "A humanoid avatar needs a Basis Avatar component whatever it was made for.");

            bool explained = false;
            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code == "descriptor.noSource")
                {
                    explained = true;
                }
            }

            Assert.That(explained, Is.True,
                "The report should say the component was added empty and why.");
        }

        [Test]
        public void TheRigIsCheckedWhateverTheAvatarWasMadeFor()
        {
            if (HumanoidModelPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoModel}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(HumanoidModelPath);

            Assert.That(plan.RigDiagnostics, Is.Not.Empty,
                "The rig check keys off the humanoid rig, not off a VRChat descriptor.");
        }

        [Test]
        public void APropWithDynamicBoneGetsNoAvatarComponent()
        {
            // The fixture is a short bone chain with no humanoid rig. Adding a Basis Avatar to a
            // prop would be wrong, so the check is on the rig rather than on having physics.
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(DynamicBoneFixture);

            Assert.That(plan.DynamicBonesFound, Is.EqualTo(1));
            Assert.That(plan.Descriptor, Is.Null,
                "A prop has physics but no rig, so it needs no avatar component.");
        }

        [Test]
        public void ConvertingAHumanoidWithNoVrchatDataWritesTheComponent()
        {
            if (HumanoidModelPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoModel}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(HumanoidModelPath);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidModelPath));
            Assert.That(_instance, Is.Not.Null);

            ConversionResult result = AvatarConverter.Apply(plan, _instance);

            Assert.That(result.DescriptorWritten, Is.True);

            BasisAvatar avatar = _instance.GetComponent<BasisAvatar>();
            Assert.That(avatar, Is.Not.Null);

            // Left at zero on purpose: Basis fills these itself when its inspector is opened, and
            // only fills what is still empty.
            Assert.That(avatar.AvatarEyePosition, Is.EqualTo(Vector2.zero));
        }
    }
}
