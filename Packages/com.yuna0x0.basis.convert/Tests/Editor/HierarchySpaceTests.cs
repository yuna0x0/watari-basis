using System.Collections.Generic;
using System.Linq;
using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Reporting;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A toggle names what it switches on the avatar, wherever the toggle itself was read
    /// from. A VRCFury toggle kept in a prefab of its own carries no renderer; its blendshape
    /// action means every skinned mesh on the avatar, and the rest state of what it sets is
    /// the instance's, with every override the instance carries.
    /// </summary>
    public class HierarchySpaceTests
    {
        private const string AvatarPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFury.prefab";

        private const string TogglePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFuryToggleOnly.prefab";

        private const string VrmAvatarPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrmAvatar/SampleVrm10Avatar.prefab";

        private const string ClothingPath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFuryClothing.prefab";

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

        private GameObject Instantiate(string path)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(path));
            _spawned.Add(instance);
            return instance;
        }

        private GameObject AvatarWearingToggle()
        {
            GameObject avatar = Instantiate(AvatarPath);
            GameObject toggle = Instantiate(TogglePath);
            toggle.transform.SetParent(avatar.transform, false);
            return avatar;
        }

        private static PlannedVixxyControl Control(AvatarConversionPlan plan, string menuName) =>
            plan.VixxyControls.FirstOrDefault(c => c.Plan.MenuName == menuName);

        [Test]
        public void AToggleInItsOwnPrefabFindsTheAvatarsRenderers()
        {
            GameObject avatar = AvatarWearingToggle();
            SkinnedMeshRenderer face = avatar.transform.Find("Face").GetComponent<SkinnedMeshRenderer>();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            PlannedVixxyControl control = Control(plan, "Long Lashes");
            Assert.That(control, Is.Not.Null, "the toggle's prefab holds no renderer; the avatar's are searched");
            Assert.That(control.Plan.Subjects, Has.Count.EqualTo(1));
            Assert.That(control.Plan.Subjects[0].Path, Is.EqualTo("Face"));
            Assert.That(control.Plan.Subjects[0].BlendShapes[0].ShapeName, Is.EqualTo("Smile"));
            Assert.That(control.Plan.Subjects[0].BlendShapes[0].Choices[1], Is.EqualTo(100f));
            Assert.That(control.SourceRenderers[0], Is.EqualTo(face), "the instance's renderer, not the asset's");
            Assert.That(plan.AllDiagnostics().Any(d =>
                    d.Code == "vrcfury.action.unresolved" && d.Message.Contains("Long Lashes")),
                Is.False);
        }

        [Test]
        public void TheRestStateComesFromTheInstance()
        {
            GameObject avatar = Instantiate(AvatarPath);
            SkinnedMeshRenderer face = avatar.transform.Find("Face").GetComponent<SkinnedMeshRenderer>();
            int smile = face.sharedMesh.GetBlendShapeIndex("Smile");
            Assert.That(smile, Is.GreaterThanOrEqualTo(0));
            face.SetBlendShapeWeight(smile, 30f);

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);

            PlannedVixxyControl control = Control(plan, "Prop");
            Assert.That(control, Is.Not.Null);
            VixxyBlendShapePlan shape = control.Plan.Subjects
                .Single(s => s.Path == "Face").BlendShapes.Single(b => b.ShapeName == "Smile");
            Assert.That(shape.Choices[0], Is.EqualTo(30f), "OFF is the weight the instance carries");
            Assert.That(shape.Choices[1], Is.EqualTo(100f));
        }

        [Test]
        public void EveryPlannedControlIsWritten()
        {
            GameObject avatar = AvatarWearingToggle();
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);
            plan.Options.Motion = false;

            ConversionResult result = AvatarConverter.Apply(plan, avatar);

            Assert.That(result.VixxyControlsSkipped, Is.EqualTo(0));
            Assert.That(result.VixxyControlsWritten, Is.EqualTo(plan.SelectedVixxyControlCount));
            Assert.That(Control(plan, "Long Lashes"), Is.Not.Null);
        }

        [Test]
        public void ARigOnLinkedBonesIsWrittenAfterTheLink()
        {
            GameObject avatar = Instantiate(VrmAvatarPath);
            GameObject clothing = Instantiate(ClothingPath);
            clothing.transform.SetParent(avatar.transform, false);
            Transform frill = clothing.transform.Find("Armature/Hips/Frill");
            Transform propHips = frill.parent;
            Transform avatarHips = avatar.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "Hips" && !t.IsChildOf(clothing.transform));

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);
            Assert.That(plan.Rigs.Any(r => r.SourceRootBone != null && r.SourceRootBone.name == "Frill"),
                Is.True, "the clothing's PhysBone is planned");

            ConversionResult result = AvatarConverter.Apply(plan, avatar);

            Assert.That(result.ArmatureLinksApplied, Is.EqualTo(1));
            Assert.That(propHips.parent, Is.EqualTo(avatarHips), "the link moved the bone the rig sits under");
            Assert.That(result.RigsSkipped, Is.EqualTo(0), string.Join("; ",
                result.Diagnostics.Where(d => d.Code == "apply.unresolved").Select(d => d.Message)));
            Assert.That(frill.GetComponent<JiggleRig>(), Is.Not.Null,
                "located before the bones moved, written after");
        }

        [Test]
        public void WriteTimeSkipsAreReported()
        {
            GameObject avatar = Instantiate(AvatarPath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);
            ConversionResult result = new ConversionResult();
            result.RigsSkipped = 1;
            result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                "apply.unresolved", "The rig for Tail has no counterpart in the target hierarchy and was skipped."));

            List<DiagnosticGroup> groups = ConversionReport.Group(plan, result);
            string report = ConversionReport.Write(plan, result);

            Assert.That(groups.Any(g => g.Code == "apply.unresolved"), Is.True);
            Assert.That(report, Does.Contain("Skipped while writing: 1"));
            Assert.That(report, Does.Contain("The rig for Tail has no counterpart"));
        }

        [Test]
        public void WarningsListEveryDistinctMessage()
        {
            GameObject avatar = Instantiate(AvatarPath);
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(avatar);
            ConversionResult result = new ConversionResult();
            result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, "apply.unresolved", "first one"));
            result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, "apply.unresolved", "second one"));
            result.Diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, "apply.unresolved", "second one"));

            string report = ConversionReport.Write(plan, result);

            Assert.That(report, Does.Contain("**apply.unresolved** (3): first one"));
            Assert.That(report, Does.Contain("  - second one"));
            Assert.That(report.Split('\n').Count(line => line.Contains("second one")), Is.EqualTo(1));
        }
    }
}
