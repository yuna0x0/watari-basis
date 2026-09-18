using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A VRM's own vowel expressions filling the Basis Avatar's visemes. Basis takes fifteen and
    /// VRM names five, so the test is that the five land in the right slots and the ten
    /// consonants stay unset.
    /// </summary>
    public class VrmVisemeTests
    {
        private const string Folder =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrmAvatar";

        private const string Vrm10Path = Folder + "/SampleVrm10Avatar.prefab";
        private const string Vrm0Path = Folder + "/SampleVrm0Avatar.prefab";

        /// <summary>
        /// The slots Basis keeps the vowels in, and the shape each fixture binds to it. `oh` is
        /// missing on purpose: the fixture's own `Oh` moves two shapes at once, which a viseme
        /// slot cannot hold.
        /// </summary>
        private static readonly (int Slot, string Shape)[] Vowels =
        {
            (10, "MouthA"),
            (11, "MouthE"),
            (12, "MouthI"),
            (14, "MouthU"),
        };

        private const int OhSlot = 13;

        private static VrmExpressionData Expression(
            string name, VrmExpressionRole role, params VrmMorphBinding[] bindings)
        {
            VrmExpressionData expression = new VrmExpressionData { Name = name, Role = role };
            expression.Bindings.AddRange(bindings);
            return expression;
        }

        private static VrmMorphBinding Binding(string shape = "Shape")
        {
            return new VrmMorphBinding { Path = "Face", Index = 1, Weight = 100f, ShapeName = shape };
        }

        [TestCase("Aa", 10)]
        [TestCase("A", 10)]
        [TestCase("Ee", 11)]
        [TestCase("E", 11)]
        [TestCase("Ih", 12)]
        [TestCase("I", 12)]
        [TestCase("Oh", 13)]
        [TestCase("O", 13)]
        [TestCase("Ou", 14)]
        [TestCase("U", 14)]
        public void EachVowelKnowsItsBasisSlot(string name, int expected)
        {
            Assert.That(
                VrmExpressionToVisemeMapper.TryGetSlot(
                    Expression(name, VrmExpressionRole.Viseme), out int slot),
                Is.True, name);

            Assert.That(slot, Is.EqualTo(expected));
        }

        [Test]
        public void AnExpressionThatIsNotAVowelFillsNoSlot()
        {
            Assert.That(VrmExpressionToVisemeMapper.TryGetSlot(
                Expression("Happy", VrmExpressionRole.Emotion), out int _), Is.False);

            // The role decides as much as the name: a custom expression an author happened to
            // call "A" is theirs, not the lip sync shape.
            Assert.That(VrmExpressionToVisemeMapper.TryGetSlot(
                Expression("A", VrmExpressionRole.Custom), out int _), Is.False);
        }

        [Test]
        public void OnlyTheTwoEyedBlinkCounts()
        {
            Assert.That(VrmExpressionToVisemeMapper.IsBlink(
                Expression("Blink", VrmExpressionRole.Blink)), Is.True);

            // Basis has one blink slot, so the one-eyed expressions are not it.
            Assert.That(VrmExpressionToVisemeMapper.IsBlink(
                Expression("BlinkLeft", VrmExpressionRole.Blink)), Is.False);
            Assert.That(VrmExpressionToVisemeMapper.IsBlink(
                Expression("Blink_R", VrmExpressionRole.Blink)), Is.False);
        }

        [Test]
        public void AnExpressionMovingSeveralShapesIsNotASingleViseme()
        {
            Assert.That(VrmExpressionToVisemeMapper.SingleBinding(
                Expression("Aa", VrmExpressionRole.Viseme, Binding(), Binding("Other"))),
                Is.Null);

            Assert.That(VrmExpressionToVisemeMapper.SingleBinding(
                Expression("Aa", VrmExpressionRole.Viseme)), Is.Null);

            Assert.That(VrmExpressionToVisemeMapper.SingleBinding(
                Expression("Aa", VrmExpressionRole.Viseme, Binding())), Is.Not.Null);
        }

        /// <summary>
        /// The visemes go on the Basis Avatar component, which is only planned for an avatar with
        /// a humanoid rig. The fixtures are hand-written hierarchies without one, so these say so
        /// and skip; both formats are verified end to end against real avatars instead.
        /// </summary>
        private static AvatarConversionPlan PlanWithDescriptor(string path)
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(path);
            if (plan.Descriptor == null)
            {
                Assert.Ignore("The VRM fixtures have no humanoid rig, so no descriptor is planned.");
            }

            return plan;
        }

        [TestCase(Vrm10Path)]
        [TestCase(Vrm0Path)]
        public void TheVowelExpressionFillsItsSlotOnTheAvatar(string path)
        {
            AvatarConversionPlan plan = PlanWithDescriptor(path);
            Assert.That(plan.Descriptor.SourceVisemeMesh, Is.Not.Null, "viseme mesh");

            List<string> names = plan.Descriptor.Plan.VisemeBlendShapeNames;
            Assert.That(names.Count, Is.EqualTo(15), "fifteen slots");

            foreach ((int slot, string shape) in Vowels)
            {
                Assert.That(names[slot], Is.EqualTo(shape), $"slot {slot}");
            }

            Assert.That(plan.AllDiagnostics().HasCode("vrm.visemes"), Is.True);
        }

        [TestCase(Vrm10Path)]
        [TestCase(Vrm0Path)]
        public void TheConsonantsAreLeftUnset(string path)
        {
            AvatarConversionPlan plan = PlanWithDescriptor(path);
            List<string> names = plan.Descriptor.Plan.VisemeBlendShapeNames;

            // sil through RR: VRM names no shapes for them, and a slot that was filled with a
            // vowel's shape would move the mouth on the wrong sound.
            for (int i = 0; i < 10; i++)
            {
                Assert.That(names[i], Is.Empty, $"slot {i}");
            }
        }

        /// <summary>
        /// The blink slot takes an index rather than a name, and the mesh it reads from is its
        /// own field on the component.
        /// </summary>
        [TestCase(Vrm10Path)]
        [TestCase(Vrm0Path)]
        public void BlinkComesFromTheBlinkExpression(string path)
        {
            AvatarConversionPlan plan = PlanWithDescriptor(path);

            Assert.That(plan.Descriptor.SourceBlinkMesh, Is.Not.Null, "blink mesh");

            Mesh mesh = plan.Descriptor.SourceBlinkMesh.sharedMesh;
            Assert.That(plan.Descriptor.Plan.BlinkBlendShapeIndices, Is.Not.Empty);
            Assert.That(mesh.GetBlendShapeName(plan.Descriptor.Plan.BlinkBlendShapeIndices[0]),
                Is.EqualTo("EyeClose"));

            Assert.That(plan.AllDiagnostics().HasCode("vrm.blink"), Is.True);
        }

        [Test]
        public void ABlinkThatMovesTwoShapesKeepsBoth()
        {
            // Basis blinks with every index in its blink array, so a blink that also lowers the
            // brows is carried whole rather than left unset.
            AvatarConversionPlan plan = PlanWithDescriptor(Vrm10Path);

            Mesh mesh = plan.Descriptor.SourceBlinkMesh.sharedMesh;
            List<string> names = plan.Descriptor.Plan.BlinkBlendShapeIndices
                .ConvertAll(index => mesh.GetBlendShapeName(index));

            Assert.That(names, Is.EqualTo(new[] { "EyeClose", "BrowUp" }));
        }

        [Test]
        public void TheEyeRotationLimitIsWrittenToTheBasisAvatar()
        {
            // The fixture's range maps are 10, 10, 10 and 12 degrees. Basis holds one angle,
            // so the largest is planned and the spread is reported.
            AvatarConversionPlan plan = PlanWithDescriptor(Vrm10Path);

            Assert.That(plan.VrmSettings.EyeRotationLimitDegrees, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(plan.VrmSettings.EyeRotationLimitMinDegrees, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(plan.Descriptor.Plan.EyeMaxLookAngleDegrees, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(plan.AllDiagnostics().HasCode("vrm.lookAt.range.uneven"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.lookAt.range"), Is.False);
        }

        [Test]
        public void OverridesAndContinuousExpressionsAreReported()
        {
            // The fixture's Happy blocks blink while worn and can be worn at any strength. Basis
            // keeps blinking and a choice is all or nothing, so both are said.
            AvatarConversionPlan plan = PlanWithDescriptor(Vrm10Path);

            Assert.That(plan.AllDiagnostics().HasCode("vrm.expression.override"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.expression.continuous"), Is.True);
        }

        /// <summary>
        /// An expression that moves two shapes cannot be one viseme, so its slot stays unset and
        /// the report says why rather than picking one of the two.
        /// </summary>
        [TestCase(Vrm10Path)]
        [TestCase(Vrm0Path)]
        public void AVowelThatMovesTwoShapesIsReportedRatherThanGuessed(string path)
        {
            AvatarConversionPlan plan = PlanWithDescriptor(path);

            Assert.That(plan.Descriptor.Plan.VisemeBlendShapeNames[OhSlot], Is.Empty, "oh");
            Assert.That(plan.AllDiagnostics().HasCode("vrm.visemeCompound"), Is.True);
        }

        /// <summary>
        /// Both formats state where the eyes sit, 0.x on its first person component and 1.0 in
        /// its object asset, and both end up on the same field.
        /// </summary>
        [TestCase(Vrm10Path)]
        [TestCase(Vrm0Path)]
        public void TheEyeOffsetReachesTheAvatar(string path)
        {
            AvatarConversionPlan plan = PlanWithDescriptor(path);

            Assert.That(plan.AllDiagnostics().HasCode("vrm.eyePosition"), Is.True);
            Assert.That(plan.Descriptor.Plan.EyePosition.x, Is.EqualTo(1.51f).Within(0.001f));
            Assert.That(plan.Descriptor.Plan.EyePosition.y, Is.EqualTo(0.08f).Within(0.001f));
        }

        [Test]
        public void AnAvatarWithNoVrmExpressionsIsUntouched()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(
                "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleAvatar/SampleAvatar.prefab");

            Assert.That(plan.AllDiagnostics().HasCode("vrm.visemes"), Is.False);
        }
    }
}
