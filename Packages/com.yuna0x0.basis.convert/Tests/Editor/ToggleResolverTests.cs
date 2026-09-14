using System.Collections.Generic;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    public class ToggleResolverTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        [Test]
        public void MenuTogglesAreTracedToTheirAnimatorLayers()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            int simple = 0;
            int withObjects = 0;
            int withShapes = 0;
            int withOther = 0;
            int animated = 0;

            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                if (toggle.IsSimple)
                {
                    simple++;
                }

                int objects = toggle.WhenOn.Activated.Count + toggle.WhenOn.Deactivated.Count
                    + toggle.WhenOff.Activated.Count + toggle.WhenOff.Deactivated.Count;
                if (objects > 0)
                {
                    withObjects++;
                }

                if (toggle.WhenOn.BlendShapes.Count + toggle.WhenOff.BlendShapes.Count > 0)
                {
                    withShapes++;
                }

                if (toggle.WhenOn.OtherCurves + toggle.WhenOff.OtherCurves > 0)
                {
                    withOther++;
                }

                if (toggle.WhenOn.AnimatedCurves + toggle.WhenOff.AnimatedCurves > 0)
                {
                    animated++;
                }
            }

            TestContext.WriteLine($"toggles resolved:        {plan.Toggles.Count}");
            TestContext.WriteLine($"  rebuildable as they are: {simple}");
            TestContext.WriteLine($"  switch objects:          {withObjects}");
            TestContext.WriteLine($"  set blendshapes:         {withShapes}");
            TestContext.WriteLine($"  drive something else:    {withOther}");
            TestContext.WriteLine($"  animate over time:       {animated}");

            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                TestContext.WriteLine(
                    $"  {toggle.MenuName} [{toggle.Parameter}] via layer {toggle.LayerName}: "
                    + $"on={toggle.WhenOn.Activated.Count}+/{toggle.WhenOn.Deactivated.Count}- "
                    + $"shapes={toggle.WhenOn.BlendShapes.Count} "
                    + $"other={toggle.WhenOn.OtherCurves} anim={toggle.WhenOn.AnimatedCurves}");
            }

            Assert.That(plan.Toggles, Is.Not.Empty,
                "The avatar's menu toggles should trace to layers in its FX controller.");

            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                Assert.That(toggle.Parameter, Is.Not.Empty);
                Assert.That(toggle.LayerName, Is.Not.Empty);
                Assert.That(toggle.WhenOn, Is.Not.Null);
                Assert.That(toggle.WhenOff, Is.Not.Null);
            }
        }

        [Test]
        public void AnAvatarWithNoFxControllerResolvesNothingRatherThanFailing()
        {
            const string fixture =
                "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/DynamicBoneChain.prefab";

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(fixture);

            Assert.That(plan.Toggles, Is.Empty);
        }
    }
}
