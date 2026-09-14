using System.Collections.Generic;
using HVR.Vixxy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Material properties are the third thing a menu toggle commonly does, after switching
    /// objects and setting blendshapes. VRChat clips hold them one channel at a time, as
    /// `material._Color.r` and its siblings, and Vixxy holds one property with a value per
    /// choice, so the channels have to be gathered back together.
    /// </summary>
    public class MaterialPropertyTests
    {
        private static readonly string AvatarPath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

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

        private static AnimationClip ClipWith(params (string path, string property, float value)[] curves)
        {
            AnimationClip clip = new AnimationClip();
            foreach ((string path, string property, float value) in curves)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), property),
                    AnimationCurve.Constant(0f, 1f / 60f, value));
            }

            return clip;
        }

        [Test]
        public void APlainMaterialPropertyIsReadAsAFloat()
        {
            ClipEffects effects = AnimationClipReader.Read(
                ClipWith(("Body", "material._UseBacklight", 1f)));

            Assert.That(effects.OtherCurves, Is.Zero,
                "A material property is no longer counted as something with nowhere to go.");
            Assert.That(effects.MaterialProperties.Count, Is.EqualTo(1));

            MaterialPropertyEffect read = effects.MaterialProperties[0];
            Assert.That(read.Path, Is.EqualTo("Body"));
            Assert.That(read.PropertyName, Is.EqualTo("_UseBacklight"),
                "The shader property name is what Vixxy turns into a property id.");
            Assert.That(read.Channel, Is.EqualTo(-1));
            Assert.That(read.Value, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ColourChannelsAreReadSeparatelyAndTellTheirChannelApart()
        {
            ClipEffects effects = AnimationClipReader.Read(ClipWith(
                ("Body", "material._Color.r", 0.25f),
                ("Body", "material._Color.a", 1f)));

            Assert.That(effects.MaterialProperties.Count, Is.EqualTo(2));

            foreach (MaterialPropertyEffect read in effects.MaterialProperties)
            {
                Assert.That(read.PropertyName, Is.EqualTo("_Color"),
                    "The channel suffix belongs to the binding, not to the shader property.");
                Assert.That(read.ColourChannel, Is.True);
            }
        }

        [Test]
        public void ACurveOnSomethingThatIsNotARendererIsNotAMaterialProperty()
        {
            AnimationClip clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, 1f / 60f, 1f));

            ClipEffects effects = AnimationClipReader.Read(clip);

            Assert.That(effects.MaterialProperties, Is.Empty);
            Assert.That(effects.OtherCurves, Is.EqualTo(1),
                "Vixxy applies material properties through a MaterialPropertyBlock, which only "
                + "renderers have.");
        }

        [Test]
        public void ChannelsOfOneColourBecomeOneProperty()
        {
            ResolvedToggle toggle = new ResolvedToggle { MenuName = "Backlit" };
            toggle.WhenOff.MaterialProperties.AddRange(new[]
            {
                Effect("Body", "_Color", 0, true, 1f),
                Effect("Body", "_Color", 1, true, 1f),
                Effect("Body", "_Color", 2, true, 1f),
                Effect("Body", "_Color", 3, true, 1f),
            });
            toggle.WhenOn.MaterialProperties.AddRange(new[]
            {
                Effect("Body", "_Color", 0, true, 0.5f),
                Effect("Body", "_Color", 1, true, 0.25f),
                Effect("Body", "_Color", 2, true, 0f),
                Effect("Body", "_Color", 3, true, 1f),
            });

            VixxyControlPlan plan = ToggleToVixxyMapper.Map(toggle);

            Assert.That(plan.Subjects.Count, Is.EqualTo(1));
            Assert.That(plan.Subjects[0].MaterialProperties.Count, Is.EqualTo(1),
                "Four channel curves are one colour, not four properties.");

            VixxyMaterialPropertyPlan property = plan.Subjects[0].MaterialProperties[0];
            Assert.That(property.PropertyName, Is.EqualTo("_Color"));
            Assert.That(property.Kind, Is.EqualTo(VixxyMaterialPropertyKind.Colour),
                "Channels named r, g, b and a are a colour; x, y, z and w are a vector.");
            Assert.That(property.Choices[0], Is.EqualTo(new Vector4(1f, 1f, 1f, 1f)));
            Assert.That(property.Choices[1], Is.EqualTo(new Vector4(0.5f, 0.25f, 0f, 1f)));
        }

        [Test]
        public void AChannelNeitherSideSetIsLeftForTheMaterialToFillIn()
        {
            ResolvedToggle toggle = new ResolvedToggle { MenuName = "Tint" };
            toggle.WhenOn.MaterialProperties.Add(Effect("Body", "_Color", 0, true, 0.5f));

            VixxyMaterialPropertyPlan property =
                ToggleToVixxyMapper.Map(toggle).Subjects[0].MaterialProperties[0];

            Assert.That(property.Set[1][0], Is.True);
            Assert.That(property.Set[0][0], Is.False,
                "The off state keeps whatever the material was authored with.");
            Assert.That(property.Set[1][1], Is.False);
        }

        [Test]
        public void AToggleThatSwitchesObjectsAndSetsAMaterialPropertyDoesBoth()
        {
            ResolvedToggle toggle = new ResolvedToggle { MenuName = "Backlit" };
            toggle.WhenOn.Activated.Add("Hair");
            toggle.WhenOn.MaterialProperties.Add(Effect("Body", "_UseBacklight", -1, false, 1f));

            VixxyControlPlan plan = ToggleToVixxyMapper.Map(toggle);

            Assert.That(plan.Diagnostics, Is.Empty,
                "A material property is no longer a reason to leave a toggle alone.");
            Assert.That(plan.Activations.Count, Is.EqualTo(1));
            Assert.That(plan.Subjects[0].MaterialProperties[0].Kind,
                Is.EqualTo(VixxyMaterialPropertyKind.Float));
        }

        [Test]
        public void AToggleThatAnimatesOverTimeIsStillLeftAlone()
        {
            ResolvedToggle toggle = new ResolvedToggle { MenuName = "Spin" };
            toggle.WhenOn.AnimatedCurves = 1;

            VixxyControlPlan plan = ToggleToVixxyMapper.Map(toggle);

            Assert.That(plan.Subjects, Is.Empty);
            Assert.That(plan.Diagnostics[0].Code, Is.EqualTo("vixxy.notSimple"));
        }

        [Test]
        public void TheAvatarsMaterialTogglesConvertAndCarryTheirPropertyNames()
        {
            if (AvatarPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(AvatarPath);

            List<string> named = new List<string>();
            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                foreach (VixxySubjectPlan subject in control.Plan.Subjects)
                {
                    foreach (VixxyMaterialPropertyPlan property in subject.MaterialProperties)
                    {
                        named.Add($"{control.Plan.MenuName}: {subject.Path} {property.PropertyName} "
                            + $"[{property.Kind}] off={property.Choices[0]} on={property.Choices[1]}");
                    }
                }
            }

            foreach (string line in named)
            {
                TestContext.WriteLine(line);
            }

            Assert.That(named, Is.Not.Empty,
                "This avatar has toggles driving material properties, which used to be reported "
                + "as unconvertible.");

            // What is left, and why. Reading this is how the next gap gets found.
            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code.StartsWith("vixxy.")
                    || diagnostic.Code.StartsWith("expressions."))
                {
                    TestContext.WriteLine($"[{diagnostic.Severity}] {diagnostic.Code}: "
                        + diagnostic.Message);
                }
            }
        }

        [Test]
        public void MaterialPropertiesAreWrittenAsVixxyPropertiesOnTheAvatar()
        {
            if (AvatarPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(AvatarPath);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath));

            AvatarConverter.Apply(plan, _instance);

            int written = 0;
            foreach (HVRVixxyControl control in
                     _instance.GetComponentsInChildren<HVRVixxyControl>(true))
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
                        if (entry.FindPropertyRelative("variant").enumValueIndex
                            != (int)HVRVixxyPropertyVariant.MaterialProperty)
                        {
                            continue;
                        }

                        Assert.That(entry.FindPropertyRelative("propertyName").stringValue,
                            Does.StartWith("_"),
                            "The shader property name is written as the shader declares it.");
                        Assert.That(entry.FindPropertyRelative("fullClassName").stringValue,
                            Does.Contain("Renderer"),
                            "Vixxy only applies material properties to renderers.");
                        Assert.That(entry.FindPropertyRelative("choices").arraySize,
                            Is.EqualTo(2));
                        written++;
                    }
                }

                serialized.Dispose();
            }

            Assert.That(written, Is.GreaterThan(0));
        }

        private static MaterialPropertyEffect Effect(
            string path, string property, int channel, bool colour, float value)
        {
            return new MaterialPropertyEffect
            {
                Path = path,
                RendererTypeName = typeof(SkinnedMeshRenderer).FullName,
                PropertyName = property,
                Channel = channel,
                ColourChannel = colour,
                Value = value,
            };
        }
    }
}
