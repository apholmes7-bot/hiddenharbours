using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The interaction chooser's row of the input table</b> (ADR 0043; owner ask 2026-11-15: "select the
    /// desired action with keys or mouse"). The bindings asset is the Def, so the number keys are pinned
    /// in it: the <c>Chooser</c> map binds 1–9 to Pick1..Pick9 on the keyboard, and nothing else in the
    /// asset binds a digit. The rest of the list's keys are actions the asset already had (UI/Navigate,
    /// Submit, Cancel, Point, Click, and Interact), pinned as present. The assembly through the gates is
    /// asserted on the pure <see cref="DeviceChooserIntentSource.Map"/>.
    /// </summary>
    public class ChooserInputTests
    {
        private static InputActionAsset Asset()
        {
            InputActionAsset asset = InputSystem.actions;
            Assert.IsNotNull(asset, "no project-wide actions asset is configured.");
            return asset;
        }

        private static InputAction ActionOf(string map, string action)
        {
            InputActionMap m = Asset().FindActionMap(map, throwIfNotFound: false);
            Assert.IsNotNull(m, $"HiddenHarbours.inputactions has no '{map}' map.");
            InputAction a = m.FindAction(action, throwIfNotFound: false);
            Assert.IsNotNull(a, $"the '{map}' map has no '{action}' action.");
            return a;
        }

        // What binding resolution does, without the action's runtime state: does any non-composite binding
        // of the action match this control's path.
        private static bool Binds(InputAction action, InputControl control)
            => action.bindings.Any(b => !b.isComposite && !string.IsNullOrEmpty(b.path) &&
                                        InputControlPath.Matches(b.path, control));

        [Test]
        public void TheNumberKeys_PickTheRows_OneToNine_AndAreBoundNowhereElse()
        {
            for (int n = 1; n <= InteractChoiceList.HardCap; n++)
            {
                InputAction pick = ActionOf(InputBindings.ChooserMap, DeviceChooserIntentSource.PickActionPrefix + n);
                Assert.AreEqual(InputActionType.Button, pick.type, "a row pick is a press");
                CollectionAssert.AreEqual(new[] { "<Keyboard>/" + n }, pick.bindings.Select(b => b.path).ToArray(),
                    $"Pick{n} is the {n} key and only that");
                Assert.IsTrue(pick.bindings.All(b => (b.groups ?? "").Split(';').Contains(InputBindings.KeyboardMouseScheme)),
                    "in the keyboard-and-mouse scheme (the pad's rows are PR 2's)");
            }

            for (int n = 0; n <= 9; n++)
            {
                string path = "<Keyboard>/" + n;
                var users = Asset().actionMaps.SelectMany(m => m.bindings)
                    .Where(b => string.Equals(b.path, path, StringComparison.OrdinalIgnoreCase))
                    .Select(b => b.action).ToArray();
                if (n == 0) Assert.AreEqual(0, users.Length, "0 picks nothing: rows are numbered from 1");
                else CollectionAssert.AreEqual(new[] { DeviceChooserIntentSource.PickActionPrefix + n }, users,
                    $"the {n} key means one thing in the whole asset");
            }
        }

        [Test]
        public void TheListsOtherKeys_AreActionsTheAssetAlreadyHas()
        {
            ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.NavigateAction);
            ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.SubmitAction);
            ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.CancelAction);
            ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.PointAction);
            ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.ClickAction);
            ActionOf(InputBindings.WalkMap, DeviceChooserIntentSource.InteractAction);
            Assert.IsTrue(new DeviceChooserIntentSource().IsBound, "the device source finds what it needs");
        }

        /// <summary>
        /// ADR 0043 PR 2, the chooser's pad rows: the stick and the d-pad move the highlight through
        /// <c>Chooser/PadNavigate</c> (data, in the Gamepad scheme), and the pad's south and east buttons
        /// confirm and cancel through UI/Submit and UI/Cancel's usage bindings, which the pad's layout tags
        /// Submit and Cancel. UI/Navigate, the keyboard's up/down and the EventSystem's, is untouched.
        /// Proved on a virtual pad by path matching, no hand on a real one needed.
        /// </summary>
        [Test]
        public void APad_MovesConfirmsAndCancels_ThroughTheAsset()
        {
            InputAction padNav = ActionOf(InputBindings.ChooserMap, DeviceChooserIntentSource.PadNavigateAction);
            Assert.AreEqual(InputActionType.Value, padNav.type,
                "a Value action, idle inside the stick's dead zone, so a resting stick never claims the list");
            Assert.AreEqual("Vector2", padNav.expectedControlType);
            CollectionAssert.AreEquivalent(new[] { "<Gamepad>/dpad", "<Gamepad>/leftStick" },
                padNav.bindings.Select(b => b.path).ToArray(), "the stick and the d-pad, and nothing else");
            Assert.IsTrue(padNav.bindings.All(b => (b.groups ?? "").Split(';').Contains(InputBindings.GamepadScheme)),
                "in the pad's scheme");

            InputAction keysNav = ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.NavigateAction);
            Assert.IsFalse(keysNav.bindings.Any(b => (b.path ?? "").StartsWith("<Gamepad>")),
                "UI/Navigate stays the keyboard's: the pad slice adds no row to the EventSystem's action");

            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.IsTrue(Binds(padNav, pad.leftStick), "the stick steers the list");
                Assert.IsTrue(Binds(padNav, pad.dpad), "and the d-pad");
                Assert.IsTrue(Binds(ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.SubmitAction), pad.buttonSouth),
                    "south confirms");
                Assert.IsTrue(Binds(ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.CancelAction), pad.buttonEast),
                    "east cancels");
                Assert.IsFalse(Binds(ActionOf(InputBindings.UiMap, DeviceChooserIntentSource.SubmitAction), pad.buttonEast),
                    "and east is not also a confirm");
                Assert.AreEqual(ControlDevice.Gamepad, InputBindings.DeviceOf(pad.leftStick),
                    "a push on the stick reports the pad, which is what lets a held push run on");
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        [Test]
        public void TheKeysAndThePad_AreJoined_ByWhicheverIsPushedFurther()
        {
            Assert.AreEqual(1f, DeviceChooserIntentSource.Strongest(1f, 0f), "a key with the pad at rest reads as before");
            Assert.AreEqual(-1f, DeviceChooserIntentSource.Strongest(-1f, 0.3f), "and wins over a half-pushed stick");
            Assert.AreEqual(-0.8f, DeviceChooserIntentSource.Strongest(0f, -0.8f), "the stick alone steers");
            Assert.AreEqual(1f, DeviceChooserIntentSource.Strongest(1f, -1f), "a tie goes to the keys");
            Assert.AreEqual(0f, DeviceChooserIntentSource.Strongest(0f, 0f));
        }
        [Test]
        public void TheMap_CarriesThePresses_AndTheGatesStopThem()
        {
            var point = new Vector2(10f, 20f);
            ChooserIntents open = DeviceChooserIntentSource.Map(1f, 3, true, true, true, point,
                                                                worldStopped: false, keysCaptured: false);
            Assert.AreEqual(1f, open.Navigate);
            Assert.AreEqual(3, open.Pick);
            Assert.IsTrue(open.Confirm && open.Cancel && open.Click);
            Assert.AreEqual(point, open.Point);

            ChooserIntents paused = DeviceChooserIntentSource.Map(1f, 3, true, true, true, point,
                                                                  worldStopped: true, keysCaptured: false);
            Assert.AreEqual(0, paused.Pick, "the world held: nothing reaches the list");
            Assert.IsFalse(paused.Confirm || paused.Cancel || paused.Click);
            Assert.AreEqual(0f, paused.Navigate);
        }

        [Test]
        public void TheChooser_ReadsIntents_NeverADevice()
        {
            foreach (string file in new[]
            {
                "Assets/_Project/Code/UI/InteractChooserView.cs",
                "Assets/_Project/Code/UI/InteractChooserControls.cs",
                "Assets/_Project/Code/Core/Interaction/InteractChooser.cs",
            })
            {
                string src = string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//")));
                StringAssert.DoesNotContain("Keyboard.current", src, $"{file} polls the keyboard.");
                StringAssert.DoesNotContain("Mouse.current", src, $"{file} polls the mouse.");
                StringAssert.DoesNotContain("using UnityEngine.InputSystem;", src, $"{file} names the device layer.");
            }
        }
    }
}
