using System;
using HVR.Vixxy;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// An outer prefab points a nested VRCFury toggle's action at one of its own objects by
    /// overriding a field of a managed reference. Unity records that override under a path of
    /// its own, checked here against Unity itself rather than assumed, and the override reader
    /// has to follow it into the RefIds entry the id names.
    /// </summary>
    public class ManagedReferenceOverrideTests
    {
        private const string ScratchFolder = "Assets/WatariTestScratch";
        private const string ProbePath = ScratchFolder + "/ManagedReferenceProbe.prefab";

        private const string NestedPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFuryNested.prefab";

        private static readonly Regex ManagedPath = new Regex(@"^managedReferences\[(?<id>-?\d+)\]\.propertyName$");

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    UnityEngine.Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();

            if (AssetDatabase.IsValidFolder(ScratchFolder))
            {
                AssetDatabase.DeleteAsset(ScratchFolder);
            }
        }

        /// <summary>
        /// Watari ships no runtime component, and Unity refuses to attach an editor script, so
        /// the probe is HVR's own Vixxy control: its property list is serialized by reference,
        /// which is how the writer fills it.
        /// </summary>
        [Test]
        public void UnityRecordsAManagedReferenceOverrideUnderTheEntrysId()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
            {
                AssetDatabase.CreateFolder("Assets", "WatariTestScratch");
            }

            GameObject authored = new GameObject("Probe");
            _spawned.Add(authored);
            HVRVixxyControl control = authored.AddComponent<HVRVixxyControl>();
            SerializedObject authoredObject = new SerializedObject(control);
            SerializedProperty subjects = authoredObject.FindProperty("subjects");
            subjects.arraySize = 1;
            SerializedProperty properties = subjects.GetArrayElementAtIndex(0).FindPropertyRelative("properties");
            properties.arraySize = 1;
            properties.GetArrayElementAtIndex(0).managedReferenceValue = new HVRVixxyPropertyFloat
            {
                fullClassName = typeof(SkinnedMeshRenderer).FullName,
                variant = HVRVixxyPropertyVariant.BlendShape,
                propertyName = "Authored",
            };
            authoredObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(authored, ProbePath);
            Assert.That(asset, Is.Not.Null);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            _spawned.Add(instance);

            SerializedObject serialized = new SerializedObject(instance.GetComponent<HVRVixxyControl>());
            SerializedProperty field = serialized.FindProperty(
                "subjects.Array.data[0].properties.Array.data[0].propertyName");
            Assert.That(field, Is.Not.Null);
            field.stringValue = "Overridden";
            serialized.ApplyModifiedProperties();

            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instance);
            Assert.That(modifications, Is.Not.Null);
            PropertyModification recorded = modifications.FirstOrDefault(m => m.propertyPath.EndsWith(".propertyName"));
            Assert.That(recorded, Is.Not.Null, string.Join(", ", modifications.Select(m => m.propertyPath)));

            Match match = ManagedPath.Match(recorded.propertyPath);
            Assert.That(match.Success, Is.True, recorded.propertyPath);

            // The id in the path is the rid of the entry in the saved file.
            string file = File.ReadAllText(ProbePath);
            Match rid = Regex.Match(file, @"- rid: (?<id>-?\d+)");
            Assert.That(rid.Success, Is.True);
            Assert.That(match.Groups["id"].Value, Is.EqualTo(rid.Groups["id"].Value));
        }

        [Test]
        public void SetPathReachesAManagedReferenceField()
        {
            List<string> lines = new List<string>
            {
                "  m_Enabled: 1",
                "  content:",
                "    rid: 1001",
                "  references:",
                "    version: 2",
                "    RefIds:",
                "    - rid: 1001",
                "      type: {class: Toggle, ns: VF.Model.Feature, asm: VRCFury}",
                "      data:",
                "        version: 3",
                "        name: Face/Long Lashes",
                "    - rid: 1003",
                "      type: {class: ObjectToggleAction, ns: VF.Model.StateAction, asm: VRCFury}",
                "      data:",
                "        version: 1",
                "        obj: {fileID: 0}",
                "        mode: 0",
            };

            Assert.That(PrefabOverrides.SetPath(lines, "managedReferences[1003].obj", "{fileID: 42}"), Is.True);
            Assert.That(lines[15], Is.EqualTo("        obj: {fileID: 42}"));
            Assert.That(PrefabOverrides.SetPath(lines, "managedReferences[1003].mode", "1"), Is.True);
            Assert.That(lines[16], Is.EqualTo("        mode: 1"));
            Assert.That(PrefabOverrides.SetPath(lines, "managedReferences[1001].name", "Other"), Is.True);
            Assert.That(lines[10], Is.EqualTo("        name: Other"));
            Assert.That(PrefabOverrides.SetPath(lines, "managedReferences[9999].obj", "{fileID: 1}"), Is.False);
            Assert.That(lines[5], Is.EqualTo("    RefIds:"), "nothing else moved");
        }

        [Test]
        public void ANestedTogglePointedAtTheOuterPrefabsObjectSwitchesIt()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(NestedPath));
            _spawned.Add(instance);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(instance);
            Assert.That(plan.Sources, Has.Count.EqualTo(2), "the outer prefab and the nested toggle");

            Assert.That(plan.OverridesApplied, Is.GreaterThanOrEqualTo(1));
            PlannedVixxyControl control = plan.VixxyControls.FirstOrDefault(c => c.Plan.MenuName == "Long Lashes");
            Assert.That(control, Is.Not.Null, string.Join("\n",
                plan.AllDiagnostics().Select(d => d.Code + ": " + d.Message)));
            Assert.That(control.Plan.Activations.Select(a => a.Path), Is.EquivalentTo(new[] { "Prop" }),
                "the object the outer prefab pointed the action at");
            Assert.That(control.Plan.Activations[0].Choices, Is.EqualTo(new[] { false, true }));
            Assert.That(control.SourceTargets[0], Is.EqualTo(instance.transform.Find("Prop")));
            Assert.That(control.Plan.Subjects.Select(s => s.Path), Is.EquivalentTo(new[] { "Face" }));
        }
    }
}
