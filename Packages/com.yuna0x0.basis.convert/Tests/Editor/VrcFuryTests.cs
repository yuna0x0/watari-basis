using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// VRCFury components are read from the original avatar, not from a copy VRCFury built.
    /// The fixture carries the shapes the public corpus shows in use: a toggle with an object,
    /// a blendshape and a material property action, a hold button, a toggle driven by a global
    /// parameter with no menu item, a Full Controller pointing at the sample avatar's own
    /// controller and menu, an Armature Link, and one component in the older list layout.
    /// </summary>
    public class VrcFuryTests
    {
        private const string FixturePath =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrcFury.prefab";

        private static List<VrcFuryComponentData> ReadAll()
        {
            List<VrcFuryComponentData> read = new List<VrcFuryComponentData>();
            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(FixturePath))
            {
                if (document.ClassId == UnityYamlScanner.ClassIdMonoBehaviour
                    && document.TryGetScriptIdentity(out string guid, out long fileId)
                    && KnownScriptIdentities.Resolve(guid, fileId) == SourceComponentKind.VrcFuryComponent)
                {
                    read.Add(VrcFuryDocumentReader.Read(document));
                }
            }

            return read;
        }

        [Test]
        public void EveryComponentReadsItsFeatureWithVersionAndClass()
        {
            List<VrcFuryComponentData> read = ReadAll();

            Assert.That(read, Has.Count.EqualTo(6));
            List<string> classes = read.SelectMany(c => c.Features).Select(f => f.Class).ToList();
            Assert.That(classes.Count(c => c == "Toggle"), Is.EqualTo(4));
            Assert.That(classes.Count(c => c == "FullController"), Is.EqualTo(1));
            Assert.That(classes.Count(c => c == "ArmatureLink"), Is.EqualTo(1));
        }

        [Test]
        public void AToggleReadsItsFieldsAndActions()
        {
            VrcFuryToggleData toggle = ReadAll().SelectMany(c => c.Features)
                .OfType<VrcFuryToggleData>().First(t => t.Name == "Clothes/Prop");

            Assert.That(toggle.Version, Is.EqualTo(3));
            Assert.That(toggle.Saved, Is.True);
            Assert.That(toggle.DefaultOn, Is.False);
            Assert.That(toggle.Actions, Has.Count.EqualTo(3));
            Assert.That(toggle.Actions[0].Kind, Is.EqualTo(VrcFuryActionKind.ObjectToggle));
            Assert.That(toggle.Actions[0].ObjectFileId, Is.EqualTo(200000L));
            Assert.That(toggle.Actions[0].Mode, Is.EqualTo(VrcFuryObjectToggleMode.TurnOn));
            Assert.That(toggle.Actions[1].Kind, Is.EqualTo(VrcFuryActionKind.BlendShape));
            Assert.That(toggle.Actions[1].BlendShape, Is.EqualTo("Smile"));
            Assert.That(toggle.Actions[1].AllRenderers, Is.True);
            Assert.That(toggle.Actions[2].Kind, Is.EqualTo(VrcFuryActionKind.MaterialProperty));
            Assert.That(toggle.Actions[2].PropertyName, Is.EqualTo("_Cutoff"));
            Assert.That(toggle.Actions[2].Value, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheOlderListLayoutIsReadToo()
        {
            VrcFuryToggleData legacy = ReadAll().SelectMany(c => c.Features)
                .OfType<VrcFuryToggleData>().FirstOrDefault(t => t.Name == "Legacy/Old");

            Assert.That(legacy, Is.Not.Null);
            Assert.That(legacy.DefaultOn, Is.True);
            Assert.That(legacy.Actions[0].Mode, Is.EqualTo(VrcFuryObjectToggleMode.TurnOff));
        }

        [Test]
        public void AFullControllerReadsItsAssetsByGuid()
        {
            VrcFuryFullControllerData controller = ReadAll().SelectMany(c => c.Features)
                .OfType<VrcFuryFullControllerData>().First();

            Assert.That(controller.Controllers[0].Controller.Guid, Is.EqualTo("2bf27e0ddd7ab4bb08127337254d4ed9"));
            Assert.That(controller.Controllers[0].Type, Is.EqualTo(5));
            Assert.That(controller.Menus[0].Menu.Guid, Is.EqualTo("7c0a1b2c3d4e5f60718293a4b5c6d702"));
            Assert.That(controller.Menus[0].Prefix, Is.EqualTo("Sample"));
            Assert.That(controller.Parameters[0].Guid, Is.EqualTo("7c0a1b2c3d4e5f60718293a4b5c6d703"));
            Assert.That(controller.RootBindingsApplyToAvatar, Is.True);
        }

        [Test]
        public void AToggleBecomesAResolvedToggleWithBothSides()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            ResolvedToggle prop = plan.VrcFuryToggles.Select(t => t.Toggle).First(t => t.MenuName == "Prop");
            Assert.That(prop.Saved, Is.True);
            Assert.That(prop.DefaultValue, Is.EqualTo(0f));
            Assert.That(prop.WhenOn.Activated, Is.EquivalentTo(new[] { "Prop" }));
            Assert.That(prop.WhenOff.Deactivated, Is.EquivalentTo(new[] { "Prop" }),
                "a turn-on object is off at rest, as VRCFury applies the off clip to the resting state");
            Assert.That(prop.WhenOn.BlendShapes.Select(b => b.Path + ":" + b.ShapeName + "=" + b.Value),
                Is.EquivalentTo(new[] { "Face:Smile=100" }));
            Assert.That(prop.WhenOn.MaterialProperties.Select(m => m.Path + ":" + m.PropertyName),
                Is.EquivalentTo(new[] { "Face:_Cutoff" }));

            ResolvedToggle old = plan.VrcFuryToggles.Select(t => t.Toggle).First(t => t.MenuName == "Old");
            Assert.That(old.DefaultValue, Is.EqualTo(1f));
            Assert.That(old.WhenOn.Deactivated, Is.EquivalentTo(new[] { "Prop" }));
            Assert.That(old.WhenOff.Activated, Is.EquivalentTo(new[] { "Prop" }));
        }

        [Test]
        public void AFullControllerTracesTheMenuItInstalls()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            List<string> names = plan.VrcFuryToggles.Select(t => t.Toggle.MenuName).ToList();
            Assert.That(names, Does.Contain("Tail_OFF"), "the sample menu's toggle, traced through the merged controller");
            Assert.That(plan.Diagnostics.HasCode("vrcfury.fullController.assetMissing"), Is.False);
        }

        [Test]
        public void WhatCannotBeRebuiltIsNamed()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            Assert.That(plan.VrcFury.Components, Is.EqualTo(6));
            Assert.That(plan.VrcFury.Toggles, Is.EqualTo(4));
            Assert.That(plan.VrcFury.FullControllers, Is.EqualTo(1));
            Assert.That(plan.VrcFury.ArmatureLinks, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.Count(d => d.Code == "vrcfury.toggle.dropped"), Is.EqualTo(2),
                "the hold button and the toggle with no menu item");
            Assert.That(plan.Diagnostics.HasCode("vrcfury.armatureLink"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.vrcfury"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("source.unknownScript"), Is.False);
            Assert.That(plan.AllDiagnostics().HasCode("vrcfury.togglesRebuilt"), Is.True);
        }

        [Test]
        public void TheTogglesBecomeVixxyControls()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            List<string> controls = plan.VixxyControls.Select(c => c.Plan.MenuName).ToList();
            Assert.That(controls, Does.Contain("Prop"));
            Assert.That(controls, Does.Contain("Old"));
            Assert.That(controls, Does.Contain("Tail_OFF"));
        }
    }
}
