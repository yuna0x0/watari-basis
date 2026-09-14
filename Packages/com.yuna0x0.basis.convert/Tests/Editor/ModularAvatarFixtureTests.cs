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
    
}
}
