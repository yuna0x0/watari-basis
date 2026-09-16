using NUnit.Framework;
using UnityEditor;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A menu the descriptor names is not always readable. A build tool writes its output under
    /// Packages/, which a unitypackage export leaves out; VRCFury packs a built copy's menus into
    /// a container it saves in binary form. Both used to read as an avatar without a menu. The
    /// container case also needs the file id: several assets share one file there.
    /// </summary>
    public class ExpressionAssetTests
    {
        private const string SampleMenuPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleMenu.asset";

        private const string ContainerPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleMenuContainer.asset";

        private const string BinaryPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/BinaryPrefab.prefab";

        private const string SamplePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleAvatar.prefab";

        private static string GuidOf(string path) => AssetDatabase.AssetPathToGUID(path);

        [Test]
        public void AMenuWhoseGuidResolvesToNothingIsRecordedAsMissing()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load("00000000000000000000000000000001", null);

            Assert.That(inventory.Menus, Is.Empty);
            Assert.That(inventory.Problems, Has.Count.EqualTo(1));
            Assert.That(inventory.Problems[0].Kind, Is.EqualTo(VrcExpressionAssetProblemKind.Missing));
            Assert.That(inventory.Problems[0].Role, Is.EqualTo("menu"));
        }

        [Test]
        public void AMenuInABinaryFileIsRecordedAsNotText()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load(GuidOf(BinaryPath), null);

            Assert.That(inventory.Menus, Is.Empty);
            Assert.That(inventory.Problems, Has.Count.EqualTo(1));
            Assert.That(inventory.Problems[0].Kind, Is.EqualTo(VrcExpressionAssetProblemKind.NotText));
        }

        [Test]
        public void AMenuPackedIntoAContainerIsReadByItsFileId()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load(GuidOf(ContainerPath), -2000L, null, 0L);

            Assert.That(inventory.Problems, Is.Empty);
            Assert.That(inventory.Menus, Has.Count.EqualTo(1));
            Assert.That(inventory.Menus[0].Controls, Has.Count.EqualTo(1));
            Assert.That(inventory.Menus[0].Controls[0].Name, Is.EqualTo("Packed_Toggle"));
        }

        [Test]
        public void AContainerWithoutTheNamedFileIdIsRecordedAsUnread()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load(GuidOf(ContainerPath), -3000L, null, 0L);

            Assert.That(inventory.Menus, Is.Empty);
            Assert.That(inventory.Problems[0].Kind, Is.EqualTo(VrcExpressionAssetProblemKind.NoDocument));
        }

        [Test]
        public void AMenuOfItsOwnStillReadsWithoutAFileId()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load(GuidOf(SampleMenuPath), 0L, null, 0L);

            Assert.That(inventory.Problems, Is.Empty);
            Assert.That(inventory.Menus[0].Controls, Has.Count.EqualTo(7));
        }

        [Test]
        public void VrcFuryComponentsAndBuildMarkersAreNamed()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(
                "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFury.prefab");

            Assert.That(plan.VrcFuryComponentsFound, Is.EqualTo(1));
            Assert.That(plan.VrcFuryBuildMarkersFound, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.HasCode("source.vrcfury"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.builtCopy"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.unknownScript"), Is.False,
                "VRCFury's scripts are known, whatever this version does with them");
        }

        [Test]
        public void AMissingPackedMenuIsNamedAsPacked()
        {
            VrcExpressionInventory inventory =
                ExpressionInventoryLoader.Load("00000000000000000000000000000002", -5L, null, 0L);

            Assert.That(inventory.Problems[0].FileId, Is.EqualTo(-5L));
        }

        [Test]
        public void TheSampleAvatarCarriesNoneOfTheWarnings()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(SamplePath);

            Assert.That(plan.Diagnostics.HasCode("expressions.assetMissing"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("expressions.assetNotText"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("fx.controllerMissing"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("source.vrcfury"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("source.builtCopy"), Is.False);
        }
    }
}
