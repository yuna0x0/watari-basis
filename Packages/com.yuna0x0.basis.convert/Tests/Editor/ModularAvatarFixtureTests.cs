using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Clothing that installs a toggle through Modular Avatar, which is how clothing is usually
    /// built. Ships with this package, so it needs no purchased asset.
    /// </summary>
    public class ModularAvatarFixtureTests
    {
        private const string FixturePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleClothing/SampleClothing.prefab";

        [Test]
        public void TheHierarchyComponentsAreLeftToModularAvatar()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                TestContext.WriteLine($"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
            }

            Assert.That(plan.ModularAvatarHierarchyFound, Is.EqualTo(1),
                "The merged armature rearranges the hierarchy, which Modular Avatar does on Basis.");
            Assert.That(plan.Diagnostics.Find(d => d.Code == "source.unknownScript"), Is.Null,
                "Every Modular Avatar component is named rather than reported as unknown.");
        }

        [Test]
        public void AnObjectToggleBecomesAControlWithNoAnimatorInvolved()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            Assert.That(plan.ModularAvatarToggles.Count, Is.EqualTo(1),
                "A menu item and an object toggle on one object describe a toggle completely.");

            ResolvedToggle toggle = plan.ModularAvatarToggles[0].Toggle;
            Assert.That(toggle.MenuName, Is.EqualTo("Scarf Toggle"),
                "Modular Avatar labels an item by its label, else its object's name, never "
                + "Control.name.");
            Assert.That(toggle.Parameter, Is.EqualTo("Scarf"));
            Assert.That(toggle.WhenOn.Deactivated, Does.Contain("Scarf"),
                "The component switches the scarf off while the menu item is on.");
            Assert.That(toggle.WhenOff.IsEmpty, Is.True,
                "Nothing is said about the other side, so it keeps the authored state.");
        }

        [Test]
        public void TheControlSwitchesTheRightWayRound()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            PlannedVixxyControl control =
                plan.VixxyControls.Find(c => c.Plan.Parameter == "Scarf");
            Assert.That(control, Is.Not.Null, "The toggle rebuilds as a Vixxy control.");

            VixxyActivationPlan activation = control.Plan.Activations[0];
            Assert.That(activation.Path, Is.EqualTo("Scarf"));
            Assert.That(activation.Choices[0], Is.True, "Off leaves the scarf as authored.");
            Assert.That(activation.Choices[1], Is.False, "On hides it, as the component says.");
        }

        [Test]
        public void ConvertingWritesTheControl()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath));

            try
            {
                ConversionResult result = AvatarConverter.Apply(plan, instance);
                Assert.That(result.VixxyControlsWritten, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    
        [Test]
        public void AShapeChangerIsReadWithItsDeletesAndSets()
        {
            // The shape of a real outfit's component: a Delete entry (ChangeType 0) that removes
            // vertices at build, and a Set entry with a value.
            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(new[]
            {
                "--- !u!114 &900",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 10}",
                "  m_Enabled: 1",
                "  m_Script: {fileID: 11500000, guid: 2db441f589c3407bb6fb5f02ff8ab541, type: 3}",
                "  m_inverted: 0",
                "  m_shapes:",
                "  - Object:",
                "      referencePath: Body",
                "      targetObject: {fileID: 0}",
                "    ShapeName: Shrink_Hip",
                "    ChangeType: 0",
                "    Value: 100",
                "  - Object:",
                "      referencePath: Body",
                "      targetObject: {fileID: 42}",
                "    ShapeName: Breast_small",
                "    ChangeType: 1",
                "    Value: 50",
                "  m_threshold: 0.01",
            });

            MaShapeChangerData data = ModularAvatarDocumentReader.ReadShapeChanger(documents[0]);

            Assert.That(data.OwnerGameObjectFileId, Is.EqualTo(10L));
            Assert.That(data.Inverted, Is.False);
            Assert.That(data.Shapes.Count, Is.EqualTo(2));
            Assert.That(data.Shapes[0].Path, Is.EqualTo("Body"));
            Assert.That(data.Shapes[0].ShapeName, Is.EqualTo("Shrink_Hip"));
            Assert.That(data.Shapes[0].IsDelete, Is.True);
            Assert.That(data.Shapes[1].TargetObjectFileId, Is.EqualTo(42L));
            Assert.That(data.Shapes[1].IsDelete, Is.False);
            Assert.That(data.Shapes[1].Value, Is.EqualTo(50f).Within(1e-6f));
        }
}
}
