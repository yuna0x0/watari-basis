using System.Collections.Generic;
using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// An avatar with clothing on it, which is how avatars are actually worn. The clothing is a
    /// prefab of its own carrying its own physics, so this is the case the single-prefab reader
    /// used to convert a third of and report as complete.
    /// <para>
    /// Both fixtures are third-party assets that cannot be distributed, so this skips itself
    /// when they are absent.
    /// </para>
    /// </summary>
    public class AssembledAvatarTests
    {
        private static readonly string AvatarPath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private static readonly string ClothingPath = LocalFixtures.Find(
            "Avatar Cloth/EXTENSION CLOTHING/BISQUE DOLL/For Shinano/Black Gimmick.prefab");

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

        private GameObject Assembled()
        {
            if (AvatarPath == null || ClothingPath == null)
            {
                Assert.Ignore("This needs an avatar and a piece of clothing for it.");
            }

            GameObject avatar = Instantiate(AvatarPath);
            GameObject clothing = Instantiate(ClothingPath);
            clothing.transform.SetParent(avatar.transform, false);
            return avatar;
        }

        private GameObject Instantiate(string path)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(path));
            _spawned.Add(instance);
            return instance;
        }

        [Test]
        public void TheClothingsPhysicsIsReadAlongWithTheAvatars()
        {
            GameObject assembled = Assembled();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);

            TestContext.WriteLine($"sources:      {plan.Sources.Count}");
            foreach (ConversionSource source in plan.Sources)
            {
                int rigs = 0;
                foreach (PlannedJiggleRig rig in plan.Rigs)
                {
                    if (rig.Source == source)
                    {
                        rigs++;
                    }
                }

                TestContext.WriteLine(
                    $"  {source.Name} [{string.Join("/", source.PathInHierarchy)}] {rigs} rigs "
                    + $"({source.AssetPath})");
            }

            TestContext.WriteLine($"physbones:    {plan.PhysBonesFound}");
            TestContext.WriteLine($"colliders:    {plan.CollidersFound}");
            TestContext.WriteLine($"constraints:  {plan.ConstraintsFound}");
            TestContext.WriteLine($"rigs planned: {plan.Rigs.Count}");
            TestContext.WriteLine($"unresolved:   {plan.Unresolved}");

            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code.StartsWith("source.")
                    || diagnostic.Code.StartsWith("physics.collider")
                    || diagnostic.Code.StartsWith("apply."))
                {
                    TestContext.WriteLine($"  [{diagnostic.Severity}] {diagnostic.Code}: "
                        + diagnostic.Message);
                }
            }

            Assert.That(plan.Sources.Count, Is.GreaterThanOrEqualTo(2),
                "The avatar's prefab and the clothing's.");
            Assert.That(plan.PhysBonesFound, Is.GreaterThan(61),
                "More than the avatar's own, since the clothing carries its own.");
        }

        [Test]
        public void TheClothingsRigsAreWrittenOntoTheClothing()
        {
            GameObject assembled = Assembled();
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);

            ConversionResult result = AvatarConverter.Apply(plan, assembled);

            Transform clothing = assembled.transform.GetChild(assembled.transform.childCount - 1);
            int onClothing = clothing.GetComponentsInChildren<JiggleRig>(true).Length;
            int total = assembled.GetComponentsInChildren<JiggleRig>(true).Length;

            TestContext.WriteLine($"written: {result.RigsWritten}, on the clothing: {onClothing}");
            foreach (ConversionDiagnostic diagnostic in result.Diagnostics)
            {
                TestContext.WriteLine($"  [{diagnostic.Severity}] {diagnostic.Code}: "
                    + diagnostic.Message);
            }

            Assert.That(result.RigsWritten, Is.EqualTo(plan.SelectedRigCount));
            Assert.That(onClothing, Is.GreaterThan(0),
                "The clothing's own rigs belong on the clothing.");
            Assert.That(total, Is.EqualTo(result.RigsWritten));
        }

        [Test]
        public void TogglesModularAvatarWouldInstallAreRebuilt()
        {
            GameObject assembled = Assembled();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);

            TestContext.WriteLine(
                $"modular avatar toggles found: {plan.ModularAvatarToggles.Count}");
            foreach (ModularAvatarToggle toggle in plan.ModularAvatarToggles)
            {
                TestContext.WriteLine(
                    $"  {toggle.Toggle.MenuName} [{toggle.Toggle.Parameter}] "
                    + $"layer {toggle.Toggle.LayerName} from {toggle.Source.Name}: "
                    + $"on {toggle.Toggle.WhenOn.Activated.Count} on / "
                    + $"{toggle.Toggle.WhenOn.Deactivated.Count} off, "
                    + $"other {toggle.Toggle.WhenOn.OtherCurves}, "
                    + $"animated {toggle.Toggle.WhenOn.AnimatedCurves}");
            }

            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                if (control.Source != null && !control.Source.IsPrimary)
                {
                    TestContext.WriteLine(
                        $"  rebuilt from {control.Source.Name}: {control.Plan.MenuName}, "
                        + $"{control.Plan.Activations.Count} objects");
                }
            }

            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code.StartsWith("modularAvatar."))
                {
                    TestContext.WriteLine($"  [{diagnostic.Severity}] {diagnostic.Code}: "
                        + diagnostic.Message);
                }
            }

            Assert.That(plan.ModularAvatarToggles, Is.Not.Empty,
                "This clothing installs its toggles through Modular Avatar, which does nothing "
                + "with them on Basis.");
        }

        [Test]
        public void LeavingTheClothingOutConvertsOnlyTheAvatar()
        {
            GameObject assembled = Assembled();
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);

            int all = plan.SelectedRigCount;
            foreach (ConversionSource source in plan.Sources)
            {
                if (!source.IsPrimary)
                {
                    source.Include = false;
                }
            }

            Assert.That(plan.SelectedRigCount, Is.LessThan(all));

            AvatarConverter.Apply(plan, assembled);

            Transform clothing = assembled.transform.GetChild(assembled.transform.childCount - 1);
            Assert.That(clothing.GetComponentsInChildren<JiggleRig>(true), Is.Empty);
        }
    }
}
