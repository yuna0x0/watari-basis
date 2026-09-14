using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Why a menu toggle is not traced to an animator layer. Most of the reference avatar's
    /// toggles are not, and the reason has never been established.
    /// </summary>
    public class ToggleTracingDiagnosticTests
    {
        private static readonly string AvatarPath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        [Test]
        public void PrintWhyEachMenuToggleIsOrIsNotTraced()
        {
            if (AvatarPath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(AvatarPath);

            HashSet<string> traced = new HashSet<string>();
            foreach (ResolvedToggle toggle in plan.Toggles)
            {
                traced.Add(toggle.Parameter);
            }

            List<string> wanted = new List<string>();
            foreach (VrcExpressionMenu menu in plan.Expressions.Menus)
            {
                foreach (VrcExpressionControl control in menu.Controls)
                {
                    if (control.Type == VrcExpressionControlType.Toggle
                        && !string.IsNullOrEmpty(control.Parameter)
                        && !wanted.Contains(control.Parameter))
                    {
                        wanted.Add(control.Parameter);
                        TestContext.WriteLine(
                            $"menu toggle '{control.Name}' -> parameter '{control.Parameter}'"
                            + (traced.Contains(control.Parameter) ? "  TRACED" : "  not traced"));
                    }
                }
            }

            AnimatorController controller = LoadFxController(plan);
            if (controller == null)
            {
                TestContext.WriteLine("no FX controller resolved");
                Assert.Pass();
            }

            TestContext.WriteLine($"\ncontroller parameters: {controller.parameters.Length}");
            TestContext.WriteLine($"controller layers: {controller.layers.Length}\n");

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorStateMachine machine = layer.stateMachine;
                if (machine == null)
                {
                    TestContext.WriteLine($"layer '{layer.name}': no state machine");
                    continue;
                }

                HashSet<string> steering = new HashSet<string>();
                foreach (ChildAnimatorState child in machine.states)
                {
                    foreach (AnimatorStateTransition transition in child.state.transitions)
                    {
                        foreach (AnimatorCondition condition in transition.conditions)
                        {
                            steering.Add(condition.parameter);
                        }
                    }
                }

                foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
                {
                    foreach (AnimatorCondition condition in transition.conditions)
                    {
                        steering.Add(condition.parameter);
                    }
                }

                foreach (AnimatorTransition transition in machine.entryTransitions)
                {
                    foreach (AnimatorCondition condition in transition.conditions)
                    {
                        steering.Add(condition.parameter);
                    }
                }

                bool touchesWanted = false;
                foreach (string parameter in steering)
                {
                    if (wanted.Contains(parameter))
                    {
                        touchesWanted = true;
                        break;
                    }
                }

                if (!touchesWanted)
                {
                    continue;
                }

                TestContext.WriteLine(
                    $"layer '{layer.name}': states {machine.states.Length}, "
                    + $"substates {machine.stateMachines.Length}, "
                    + $"steering [{string.Join(", ", steering)}]");
            }

            Assert.Pass();
        }

        private static AnimatorController LoadFxController(AvatarConversionPlan plan)
        {
            if (plan.Descriptor?.SourceData == null)
            {
                return null;
            }

            foreach (VrcAnimationLayerEntry layer in plan.Descriptor.SourceData.AnimationLayers)
            {
                if (layer.Layer == VrcAnimationLayer.FX)
                {
                    return ToggleResolver.LoadController(layer.ControllerGuid);
                }
            }

            return null;
        }
    }
}
