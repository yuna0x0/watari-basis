using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A VRCFury Armature Link parents clothing bones under the avatar's, which VRCFury does on
    /// a VRChat build only. The clothing fixture has an armature of three matched bones and one
    /// with no match, worn on the VRM sample avatar, which has a humanoid rig for the link's
    /// Hips target to resolve against.
    /// </summary>
    public class VrcFuryArmatureLinkTests
    {
        private const string AvatarPath =
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

        private GameObject Assembled()
        {
            GameObject avatar = Instantiate(AvatarPath);
            GameObject clothing = Instantiate(ClothingPath);
            clothing.transform.SetParent(avatar.transform, false);
            return avatar;
        }

        private static Transform Bone(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        [Test]
        public void TheLinkPlansOnePairPerMatchedBone()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(Assembled());

            Assert.That(plan.ArmatureLinks, Has.Count.EqualTo(1));
            PlannedArmatureLink link = plan.ArmatureLinks[0];
            Assert.That(link.TargetName, Is.EqualTo("Hips"));
            Assert.That(link.RootName, Is.EqualTo("SampleVrcFuryClothing"));
            Assert.That(link.Bones.Select(b => b.SourceProp.name + ">" + b.SourceAvatar.name),
                Is.EquivalentTo(new[] { "Hips>Hips", "Spine>Spine", "Chest>Chest" }),
                "Frill has no avatar bone and stays with its parent");
            Assert.That(link.Bones[0].SourceProp.name, Is.EqualTo("Chest"), "deepest first");
            Assert.That(plan.Diagnostics.HasCode("vrcfury.armatureLink"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("vrcfury.armatureLink.unresolved"), Is.False);
        }

        [Test]
        public void ApplyingMovesTheBonesUnderTheAvatarsAndAlignsThem()
        {
            GameObject assembled = Assembled();
            Transform clothing = assembled.transform.Find("SampleVrcFuryClothing");
            Transform propHips = clothing.Find("Armature/Hips");
            Transform avatarHips = assembled.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hips" && !t.IsChildOf(clothing));
            Transform avatarSpine = avatarHips.GetComponentsInChildren<Transform>(true).First(t => t.name == "Spine");
            Transform propSpine = propHips.Find("Spine");
            Transform propFrill = propHips.Find("Frill");

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);
            ConversionResult result = AvatarConverter.Apply(plan, assembled);

            Assert.That(result.ArmatureLinksApplied, Is.EqualTo(1));
            Assert.That(result.BonesLinked, Is.EqualTo(3));
            Assert.That(result.Diagnostics.HasCode("apply.unpacked"), Is.True,
                "Unity does not let a bone leave its prefab instance, so the clothing is unpacked");
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(assembled), Is.True, "the avatar's own instance is untouched");
            Assert.That(propHips.parent, Is.EqualTo(avatarHips));
            Assert.That(propSpine.parent, Is.EqualTo(avatarSpine), "matched below the root goes under its own avatar bone");
            Assert.That(propFrill.parent, Is.EqualTo(propHips), "unmatched stays under its clothing parent");
            Assert.That(propHips.position, Is.EqualTo(avatarHips.position));
            Assert.That(propSpine.position, Is.EqualTo(avatarSpine.position));
            Assert.That(propHips.name, Is.EqualTo("[VF] Hips from SampleVrcFuryClothing"));
        }

        [Test]
        public void ConvertingTwiceMovesNothingMore()
        {
            GameObject assembled = Assembled();
            AvatarConverter.Apply(AvatarConversionPlanner.Plan(assembled), assembled);

            ConversionResult again = AvatarConverter.Apply(AvatarConversionPlanner.Plan(assembled), assembled);

            Assert.That(again.BonesLinked, Is.EqualTo(0));
        }

        [Test]
        public void TheTargetCanBeLeftOut()
        {
            GameObject assembled = Assembled();
            Transform propHips = assembled.transform.Find("SampleVrcFuryClothing/Armature/Hips");
            Transform originalParent = propHips.parent;

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(assembled);
            plan.Options.ArmatureLinks = false;
            ConversionResult result = AvatarConverter.Apply(plan, assembled);

            Assert.That(result.BonesLinked, Is.EqualTo(0));
            Assert.That(propHips.parent, Is.EqualTo(originalParent));
            Assert.That(result.Diagnostics.HasCode("apply.unpacked"), Is.False);
        }

        [Test]
        public void ClothingOnItsOwnHasNoAvatarToLinkTo()
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(ClothingPath);

            Assert.That(plan.ArmatureLinks, Is.Empty);
            Assert.That(plan.Diagnostics.HasCode("vrcfury.armatureLink.unresolved"), Is.True);
        }
    }
}
