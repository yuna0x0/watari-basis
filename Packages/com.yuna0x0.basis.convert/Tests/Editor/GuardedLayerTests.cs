using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Which layers count as a menu parameter's own, when something besides that parameter is
    /// mentioned in the conditions.
    /// <para>
    /// The controllers here are built rather than fixtures, because what is being tested is the
    /// shape of a state machine rather than anything about an avatar.
    /// </para>
    /// </summary>
    public class GuardedLayerTests
    {
        private const string Folder = "Assets/WatariLayerTest";

        private AnimatorController _controller;

        [TearDown]
        public void TearDown()
        {
            _controller = null;
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private AnimatorController Controller()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "WatariLayerTest");
            }

            _controller = AnimatorController.CreateAnimatorControllerAtPath(
                $"{Folder}/Test.controller");

            return _controller;
        }

        [Test]
        public void AToggleGuardedByABuiltInIsStillTheTogglesLayer()
        {
            // "Only for me" gimmicks test IsLocal alongside their own parameter. Nothing on
            // Basis drives it, so the layer is still the toggle's and the guard is reported.
            AnimatorController controller = Controller();
            controller.AddParameter("Gimmick", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState off = layer.stateMachine.AddState("Off");
            AnimatorState on = layer.stateMachine.AddState("On");
            layer.stateMachine.defaultState = off;

            AnimatorStateTransition toOn = off.AddTransition(on);
            toOn.AddCondition(AnimatorConditionMode.If, 0f, "Gimmick");
            toOn.AddCondition(AnimatorConditionMode.If, 0f, "IsLocal");

            AnimatorStateTransition toOff = on.AddTransition(off);
            toOff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Gimmick");

            var found = FxControllerReader.FindToggleLayers(controller, new[] {"Gimmick"});

            Assert.That(found.Count, Is.EqualTo(1));
            Assert.That(found[0].GuardedBy, Is.EqualTo(new[] {"IsLocal"}));
        }

        [Test]
        public void AToggleGuardedByFaceTrackingIsStillTheTogglesLayer()
        {
            // UserFaceTracking is a VRChat built-in, documented with UserEyeTracking.
            AnimatorController controller = Controller();
            controller.AddParameter("Expression", AnimatorControllerParameterType.Bool);
            controller.AddParameter("UserFaceTracking", AnimatorControllerParameterType.Bool);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState off = layer.stateMachine.AddState("Off");
            AnimatorState on = layer.stateMachine.AddState("On");
            layer.stateMachine.defaultState = off;

            AnimatorStateTransition toOn = off.AddTransition(on);
            toOn.AddCondition(AnimatorConditionMode.If, 0f, "Expression");
            toOn.AddCondition(AnimatorConditionMode.IfNot, 0f, "UserFaceTracking");

            AnimatorStateTransition toOff = on.AddTransition(off);
            toOff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Expression");

            var found = FxControllerReader.FindToggleLayers(controller, new[] {"Expression"});

            Assert.That(found.Count, Is.EqualTo(1));
            Assert.That(found[0].GuardedBy, Is.EqualTo(new[] {"UserFaceTracking"}));
        }

        [Test]
        public void ALayerAGestureSteersOnItsOwnIsLeftAlone()
        {
            // One transition here is decided by the gesture alone, so the layer belongs to the
            // gesture. Reading it as the toggle's would take clips the toggle never selects.
            AnimatorController controller = Controller();
            controller.AddParameter("Gimmick", AnimatorControllerParameterType.Bool);
            controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState idle = layer.stateMachine.AddState("Idle");
            AnimatorState on = layer.stateMachine.AddState("On");
            AnimatorState fist = layer.stateMachine.AddState("Fist");
            layer.stateMachine.defaultState = idle;

            AnimatorStateTransition toOn = idle.AddTransition(on);
            toOn.AddCondition(AnimatorConditionMode.If, 0f, "Gimmick");

            AnimatorStateTransition toFist = layer.stateMachine.AddAnyStateTransition(fist);
            toFist.AddCondition(AnimatorConditionMode.Equals, 1f, "GestureLeft");

            var found = FxControllerReader.FindToggleLayers(controller, new[] {"Gimmick"});

            Assert.That(found, Is.Empty);
        }

        [Test]
        public void ALayerWhereOneValueReachesTwoStatesIsLeftAlone()
        {
            // Every transition names the parameter, so the guard rule is satisfied, but the
            // gesture is what actually picks between the two states. Reading it would keep
            // whichever transition came first.
            AnimatorController controller = Controller();
            controller.AddParameter("Face", AnimatorControllerParameterType.Int);
            controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState neutral = layer.stateMachine.AddState("Neutral");
            AnimatorState smile = layer.stateMachine.AddState("Smile");
            AnimatorState frown = layer.stateMachine.AddState("Frown");
            layer.stateMachine.defaultState = neutral;

            AnimatorStateTransition toSmile = layer.stateMachine.AddAnyStateTransition(smile);
            toSmile.AddCondition(AnimatorConditionMode.Equals, 0f, "Face");
            toSmile.AddCondition(AnimatorConditionMode.Equals, 1f, "GestureLeft");

            AnimatorStateTransition toFrown = layer.stateMachine.AddAnyStateTransition(frown);
            toFrown.AddCondition(AnimatorConditionMode.Equals, 0f, "Face");
            toFrown.AddCondition(AnimatorConditionMode.Equals, 2f, "GestureLeft");

            var found = FxControllerReader.FindToggleLayers(controller, new[] {"Face"});

            Assert.That(found, Is.Empty);
        }

        [Test]
        public void ALayerTwoOfTheAvatarsOwnParametersSteerIsLeftAlone()
        {
            // Unchanged by any of this: a parameter Basis knows nothing about is treated as the
            // avatar's own, which is what keeps an unrecognised name from being read as a guard.
            AnimatorController controller = Controller();
            controller.AddParameter("Gimmick", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Outfit", AnimatorControllerParameterType.Int);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState off = layer.stateMachine.AddState("Off");
            AnimatorState on = layer.stateMachine.AddState("On");
            layer.stateMachine.defaultState = off;

            AnimatorStateTransition toOn = off.AddTransition(on);
            toOn.AddCondition(AnimatorConditionMode.If, 0f, "Gimmick");
            toOn.AddCondition(AnimatorConditionMode.Equals, 2f, "Outfit");

            var found = FxControllerReader.FindToggleLayers(controller, new[] {"Gimmick"});

            Assert.That(found, Is.Empty);
        }
    }
}
