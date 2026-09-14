using NUnit.Framework;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    public class SourceProfileTests
    {
        private static readonly string AvatarFixture =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private static readonly string ModelFixture =
            LocalFixtures.Find(LocalFixtures.ShinanoModel);

        private const string PropFixture =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/DynamicBoneChain.prefab";

        [Test]
        public void AVrchatAvatarIsNamedAsOne()
        {
            if (AvatarFixture == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            SourceProfile profile = AvatarConversionPlanner.Plan(AvatarFixture).Profile;

            TestContext.WriteLine(profile.Describe());

            Assert.That(profile.Kind, Is.EqualTo("VRChat avatar"));
            Assert.That(profile.HasVrchatDescriptor, Is.True);
            Assert.That(profile.HasVrchatComponents, Is.True);
            Assert.That(profile.HasHumanoidRig, Is.True);
            Assert.That(profile.LooksInconsistent, Is.False);
        }

        [Test]
        public void APropWithDynamicBoneIsNotCalledAnAvatar()
        {
            SourceProfile profile = AvatarConversionPlanner.Plan(PropFixture).Profile;

            TestContext.WriteLine(profile.Describe());

            Assert.That(profile.Kind, Is.EqualTo("Clothing or accessory"));
            Assert.That(profile.HasDynamicBone, Is.True);
            Assert.That(profile.HasHumanoidRig, Is.False);
        }

        [Test]
        public void ABareHumanoidIsFlaggedAsHavingNothingToConvert()
        {
            if (ModelFixture == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoModel}.");
            }

            SourceProfile profile = AvatarConversionPlanner.Plan(ModelFixture).Profile;

            TestContext.WriteLine(profile.Describe());

            Assert.That(profile.Kind, Is.EqualTo("Humanoid avatar"));
            Assert.That(profile.LooksInconsistent, Is.True,
                "A rig with nothing on it usually means the wrong object was picked.");
        }

        [Test]
        public void TheSignalsBackUpTheName()
        {
            SourceProfile profile = new SourceProfile
            {
                HasHumanoidRig = true,
                HasDynamicBone = true,
            };

            Assert.That(profile.Kind, Is.EqualTo("Humanoid avatar with Dynamic Bone"));
            Assert.That(profile.Signals(), Does.Contain("humanoid rig"));
            Assert.That(profile.Signals(), Does.Contain("Dynamic Bone"));
            Assert.That(profile.Signals(), Does.Not.Contain("VRChat components"));
        }
    }
}
