using System.IO;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// A rebuilt toggle has to switch the same way round as the toggle it came from.
    /// <para>
    /// It did not: a clip that only switches an object off on one side looks identical, by value
    /// alone, to a side that animated nothing, so the authored state was written into the side
    /// that had animated and every one-sided toggle came out inverted. Found by wearing the
    /// avatar in Basis, where turning "Tail_OFF" on showed the tail.
    /// </para>
    /// </summary>
    public class ToggleDirectionTests
    {
        private const string AvatarPath =
            "Assets/yuna0x0/Avatars/Shinano/Prefab/Shinano.prefab";

        [Test]
        public void PrintTheDirectionOfEveryRebuiltToggle()
        {
            if (!File.Exists(AvatarPath))
            {
                Assert.Ignore($"Fixture not present at {AvatarPath}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(AvatarPath);

            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                TestContext.WriteLine($"[{toggle.MenuName}] parameter {toggle.Parameter}");
                TestContext.WriteLine(
                    $"    clip when off: activates [{string.Join(", ", toggle.WhenOff.Activated)}] "
                    + $"deactivates [{string.Join(", ", toggle.WhenOff.Deactivated)}]");
                TestContext.WriteLine(
                    $"    clip when on:  activates [{string.Join(", ", toggle.WhenOn.Activated)}] "
                    + $"deactivates [{string.Join(", ", toggle.WhenOn.Deactivated)}]");
            }

            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                TestContext.WriteLine($"CONTROL [{control.Plan.MenuName}] "
                    + $"default {control.Plan.DefaultValue}");
                foreach (VixxyActivationPlan activation in control.Plan.Activations)
                {
                    TestContext.WriteLine(
                        $"    {activation.Path}: choice0(off)={activation.Choices[0]} "
                        + $"choice1(on)={activation.Choices[1]} "
                        + $"set=[{string.Join(", ", activation.Set)}]");
                }
            }

            Assert.Pass();
        }

        [Test]
        public void ASideThatAnimatesNothingKeepsTheAuthoredState()
        {
            if (!File.Exists(AvatarPath))
            {
                Assert.Ignore($"Fixture not present at {AvatarPath}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(AvatarPath);

            int checkedControls = 0;
            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                // Only the on side animates, and it switches the object off. So the control has
                // to read: off leaves the object as authored, on hides it.
                foreach (string path in toggle.WhenOn.Deactivated)
                {
                    if (toggle.WhenOff.Activated.Contains(path)
                        || toggle.WhenOff.Deactivated.Contains(path))
                    {
                        continue;
                    }

                    VixxyActivationPlan activation = Find(plan, toggle.Parameter, path);
                    if (activation == null)
                    {
                        continue;
                    }

                    Assert.That(activation.Choices[1], Is.False,
                        $"'{toggle.MenuName}' switches {path} off, so the on choice hides it.");
                    Assert.That(activation.Choices[0], Is.True,
                        $"'{toggle.MenuName}' leaves {path} alone when off, and the avatar was "
                        + "authored with it visible.");
                    checkedControls++;
                }
            }

            Assert.That(checkedControls, Is.GreaterThan(0),
                "This avatar has one-sided toggles; if it stops having them, this proves nothing.");
        }

        private static VixxyActivationPlan Find(
            AvatarConversionPlan plan, string parameter, string path)
        {
            foreach (PlannedVixxyControl control in plan.VixxyControls)
            {
                if (control.Plan.Parameter != parameter)
                {
                    continue;
                }

                foreach (VixxyActivationPlan activation in control.Plan.Activations)
                {
                    if (activation.Path == path)
                    {
                        return activation;
                    }
                }
            }

            return null;
        }
    
        [Test]
        public void ATwoStateControlWithAnotherOnValueIsWrittenAsZeroAndOne()
        {
            // HVRVixxyControl.IsRegularToggle needs values exactly 0 and 1, and Basis's menu
            // presents anything else as a selector.
            ResolvedToggle toggle = new ResolvedToggle
            {
                MenuName = "Hat",
                Parameter = "Outfit",
                DefaultValue = 2f,
            };
            toggle.Choices.Add(new ResolvedChoice { Name = "OFF", Value = 0, Effects = new ClipEffects() });
            toggle.Choices.Add(new ResolvedChoice { Name = "ON", Value = 2, Effects = new ClipEffects() });
            toggle.Choices[1].Effects.Activated.Add("Hat");

            VixxyControlPlan plan = ToggleToVixxyMapper.Map(toggle);

            Assert.That(plan.ChoiceValues, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(plan.DefaultValue, Is.EqualTo(1f), "The default was the ON value.");
            Assert.That(plan.Diagnostics.HasCode("vixxy.values.normalized"), Is.True);
            Assert.That(plan.Saved, Is.True);
            Assert.That(plan.NetworkSynced, Is.True);
        }
}
}
