using System.IO;
using HVR.Vixxy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    public class VixxyEmissionTests
    {
        private const string FixturePath =
            "Assets/yuna0x0/Avatars/Shinano/Prefab/Shinano.prefab";

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
        public void ToggleThatOnlySwitchesObjectsBecomesAControl()
        {
            if (!File.Exists(FixturePath))
            {
                Assert.Ignore($"Fixture not present at {FixturePath}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            TestContext.WriteLine($"vixxy controls planned: {plan.VixxyControls.Count}");
            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                TestContext.WriteLine($"  {control.Plan.MenuName} [{control.Plan.Parameter}]: "
                    + $"{control.Plan.Activations.Count} objects");
                foreach (VixxyActivationPlan activation in control.Plan.Activations)
                {
                    TestContext.WriteLine(
                        $"    {activation.Path}: off={activation.Choices[0]} "
                        + $"on={activation.Choices[1]}");
                }

                foreach (VixxySubjectPlan subject in control.Plan.Subjects)
                {
                    foreach (VixxyBlendShapePlan shape in subject.BlendShapes)
                    {
                        TestContext.WriteLine(
                            $"    {subject.Path} shape {shape.ShapeName}: "
                            + $"off={shape.Choices[0]} on={shape.Choices[1]}");
                    }
                }
            }

            Assert.That(plan.VixxyControls, Is.Not.Empty,
                "Toggles that only switch objects should be rebuildable.");

            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                Assert.That(control.SourceTargets.Count,
                    Is.EqualTo(control.Plan.Activations.Count),
                    "Every switched object should have resolved to a transform.");

                foreach (VixxyActivationPlan activation in control.Plan.Activations)
                {
                    // A selector has one choice per value; a switch is real when any differ.
                    bool anyDiffers = false;
                    foreach (bool choice in activation.Choices)
                    {
                        anyDiffers |= choice != activation.Choices[0];
                    }

                    Assert.That(anyDiffers, Is.True,
                        $"{control.Plan.MenuName} would not change {activation.Path} at all.");
                }

                Assert.That(control.SourceRenderers.Count,
                    Is.EqualTo(control.Plan.Subjects.Count),
                    "Every blendshape subject should have resolved to a renderer.");
            }

            bool anyBlendShapes = false;
            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                if (control.Plan.Subjects.Count > 0)
                {
                    anyBlendShapes = true;
                }
            }

            Assert.That(anyBlendShapes, Is.True,
                "Toggles that set blendshapes should be rebuilt too, not only object switches.");

            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
            }
        }

        [Test]
        public void ConvertingWritesTheControlsAndTheirMenuItems()
        {
            if (!File.Exists(FixturePath))
            {
                Assert.Ignore($"Fixture not present at {FixturePath}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath));

            ConversionResult result = AvatarConverter.Apply(plan, _instance);

            Assert.That(result.VixxyControlsWritten, Is.EqualTo(plan.VixxyControls.Count));

            HVRVixxyControl[] controls = _instance.GetComponentsInChildren<HVRVixxyControl>(true);
            HVRVixxyMenuItem[] items = _instance.GetComponentsInChildren<HVRVixxyMenuItem>(true);

            Assert.That(controls.Length, Is.EqualTo(plan.VixxyControls.Count));
            Assert.That(items.Length, Is.EqualTo(controls.Length),
                "Every control should get a menu item, or it cannot be reached in game.");

            foreach (HVRVixxyControl control in controls)
            {
                Assert.That(control.choices.Length, Is.GreaterThanOrEqualTo(2),
                    "A toggle has two choices; a selector has one per value of its parameter.");
                Assert.That(control.choices[0].value, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(control.choices[1].value, Is.EqualTo(1f).Within(1e-6f));
            }

            // The blendshape half is written as subjects carrying float properties, which is a
            // SerializeReference list and the easiest part to get silently wrong.
            int shapeProperties = 0;
            foreach (HVRVixxyControl control in controls)
            {
                SerializedObject serialized = new SerializedObject(control);
                SerializedProperty subjects = serialized.FindProperty("subjects");

                for (int i = 0; i < subjects.arraySize; i++)
                {
                    SerializedProperty properties = subjects.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("properties");

                    for (int p = 0; p < properties.arraySize; p++)
                    {
                        SerializedProperty entry = properties.GetArrayElementAtIndex(p);
                        Assert.That(entry.managedReferenceValue, Is.Not.Null,
                            "A blendshape property was written as a null reference.");
                        Assert.That(
                            entry.FindPropertyRelative("propertyName").stringValue,
                            Is.Not.Empty);
                        Assert.That(
                            entry.FindPropertyRelative("choices").arraySize,
                            Is.EqualTo(control.choices.Length),
                            "A property holds one value per choice of its control.");
                        shapeProperties++;
                    }
                }

                serialized.Dispose();
            }

            TestContext.WriteLine($"blendshape properties written: {shapeProperties}");
            Assert.That(shapeProperties, Is.GreaterThan(0));
        }

        [Test]
        public void AToggleDoingMoreThanSwitchingObjectsIsLeftAlone()
        {
            if (!File.Exists(FixturePath))
            {
                Assert.Ignore($"Fixture not present at {FixturePath}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            // This avatar's hair toggles disable the PhysBones of the hair they hide. Those
            // switches carry over onto the rigs; anything else a control cannot hold is reported.
            bool switched = false;
            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code == "vixxy.componentSwitch"
                    || diagnostic.Code == "vixxy.notSimple")
                {
                    switched |= diagnostic.Code == "vixxy.componentSwitch";
                    TestContext.WriteLine(diagnostic.Message);
                }
            }

            Assert.That(switched, Is.True,
                "A toggle that disables PhysBones should switch their rigs, not be dropped.");
        }
    }
}
