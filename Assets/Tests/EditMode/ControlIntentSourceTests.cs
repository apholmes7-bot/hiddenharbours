using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE INPUT SEAM</b> (ADR 0043, PR 0) — the intents in Core, the bindings asset, the
    /// device-backed sources behind the walk and the deck, and the held sources a scripted journey uses.
    ///
    /// <para><b>What is pinned here is BYTE-IDENTITY with the read the seam replaced.</b> The walk used
    /// to sum W/A/S/D and the arrows per axis off <c>Keyboard.current</c> and read either Shift as the
    /// sprint; the deck and the arrival's cabin walk read the same four letters and four arrows. That
    /// truth table is now DATA — <c>HiddenHarbours.inputactions</c> — so the pin reads the asset: one
    /// <c>2DVector</c> composite per move in DIGITAL mode (the sum, not a normalised sum), each part
    /// OR-ing its letter and its arrow, the sprint either Shift. A binding edited in the inspector reds
    /// this test, which is the point: the asset is the Def, and a Def has a validator.</para>
    ///
    /// <para><b>Why the keys are not pressed here.</b> A virtual keypress does not survive to a read in a
    /// headless editor on this box (memory <c>playmode-virtual-keypress-is-undeliverable</c>; the sprint
    /// fixture measured the same in PlayMode). So the asset is asserted STRUCTURALLY, the assembly of
    /// values into intents through the gates is asserted on the pure <c>Map</c>, and the live read is
    /// asserted only for the one thing it can honestly say with no key held: nothing.</para>
    ///
    /// <para>What is NOT here: that the controllers read their source every frame and move on what it
    /// says. That is a claim about <c>Update</c> and <c>FixedUpdate</c>, and
    /// <c>WalkIntentJourneyPlayTests</c> / <c>IntroCabinPassagePlayTests</c> make it under a running
    /// frame pump.</para>
    /// </summary>
    public class ControlIntentSourceTests
    {
        private const string AssetPath = "Assets/_Project/Data/Input/HiddenHarbours.inputactions";
        private const string TemplatePath = "Assets/InputSystem_Actions.inputactions";

        private readonly List<ActiveControlDeviceChanged> _deviceChanges = new List<ActiveControlDeviceChanged>();

        [SetUp]
        public void SetUp()
        {
            MoveActionClaim.Reset();
            ShellPause.Reset();
            ActiveControlDevice.Reset();
            _deviceChanges.Clear();
            EventBus.Subscribe<ActiveControlDeviceChanged>(OnDeviceChanged);
        }

        [TearDown]
        public void TearDown()
        {
            // By hand, never Clear<T>(): a Clear takes every other listener down with it.
            EventBus.Unsubscribe<ActiveControlDeviceChanged>(OnDeviceChanged);
            MoveActionClaim.Reset();
            ShellPause.Reset();
            ActiveControlDevice.Reset();
        }

        private void OnDeviceChanged(ActiveControlDeviceChanged e) => _deviceChanges.Add(e);

        private static InputActionAsset Asset()
        {
            InputActionAsset asset = InputSystem.actions;
            Assert.IsNotNull(asset,
                "no project-wide actions asset is configured (Project Settings > Input System Package > " +
                "Project-wide Actions) — every seamed control mode reads as nothing.");
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

        private static string[] PathsOf(InputAction action, Func<InputBinding, bool> where = null)
            => action.bindings.Where(b => where == null || where(b)).Select(b => b.path).OrderBy(p => p).ToArray();

        // =============================================================================================
        //  the asset is the Def
        // =============================================================================================

        [Test]
        public void TheProjectWideBindingsAreHiddenHarbours_AndTheTemplateIsGone()
        {
            InputActionAsset asset = Asset();
            Assert.AreEqual(InputBindings.AssetName, asset.name,
                "the project-wide actions asset is not HiddenHarbours.inputactions.");
            Assert.AreEqual(AssetPath, AssetDatabase.GetAssetPath(asset),
                "the bindings Def lives under Data/ with the other Defs (rule 2), and the project-wide " +
                "reference must point at that file.");

            foreach (string map in new[] { InputBindings.WalkMap, InputBindings.DeckMap, InputBindings.HelmMap,
                                           InputBindings.DriveMap, InputBindings.UiMap })
                Assert.IsNotNull(asset.FindActionMap(map, throwIfNotFound: false),
                    $"the '{map}' map is missing — ADR 0043 fixes the mode list as Walk, Deck, Helm, Drive, UI.");

            CollectionAssert.AreEquivalent(
                new[] { InputBindings.KeyboardMouseScheme, InputBindings.GamepadScheme },
                asset.controlSchemes.Select(s => s.name).ToArray(),
                "exactly two control schemes: the keyboard-and-mouse the game ships with, and the pad.");

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<InputActionAsset>(TemplatePath),
                "Unity's untouched template (Player/UI: Move, Look, Attack, Jump…) is still in the project. " +
                "It was never read by anything and it is deleted by ADR 0043 §7; there is ONE bindings asset.");
        }

        [Test]
        public void TheGamepadSchemeIsPresentButEmpty_UntilPr2FillsIt()
        {
            InputActionAsset asset = Asset();
            InputBinding[] pad = asset.actionMaps
                .SelectMany(m => m.bindings)
                .Where(b => (b.groups ?? "").Split(';').Contains(InputBindings.GamepadScheme))
                .ToArray();
            Assert.AreEqual(0, pad.Length,
                "PR 0 declares the Gamepad scheme and binds NOTHING to it — the pad table is the input lane's " +
                "PR 2 and is owner-gated (the feel ruling). When PR 2 lands, this test becomes the pad's " +
                $"pin. Found: {string.Join(", ", pad.Select(b => b.action + "=" + b.path))}");
        }

        /// <summary>
        /// The OLD read, transcribed before it was deleted:
        /// <code>
        ///   if (w || upArrow)    m.y += 1;
        ///   if (s || downArrow)  m.y -= 1;
        ///   if (d || rightArrow) m.x += 1;
        ///   if (a || leftArrow)  m.x -= 1;
        /// </code>
        /// As a composite: <c>2DVector</c> in mode 1 (Digital — up minus down, right minus left, no
        /// normalisation; the controller clamps the magnitude itself, as it always did), each part bound to
        /// its letter AND its arrow (a part with two controls is pressed when either is).
        /// </summary>
        [TestCase("Walk")]
        [TestCase("Deck")]
        public void TheMoveIsTheOldSummedKeys_AsOneDigitalComposite(string map)
        {
            InputAction move = ActionOf(map, "Move");
            Assert.AreEqual(InputActionType.Value, move.type, $"{map}/Move is polled as a value.");
            Assert.AreEqual("Vector2", move.expectedControlType);

            InputBinding[] composites = move.bindings.Where(b => b.isComposite).ToArray();
            Assert.AreEqual(1, composites.Length,
                $"{map}/Move must be exactly ONE composite: two composites on one action make the Input " +
                "System pick the more actuated, and W plus a left-arrow would no longer sum to (−1, +1).");
            string path = composites[0].path;
            StringAssert.StartsWith("2DVector", path, $"{map}/Move is not a 2DVector composite.");
            StringAssert.Contains("mode=1", path,
                $"{map}/Move must be mode=1 (Digital): the old read SUMMED the keys and left the diagonal at " +
                "(±1, ±1) for VelocityFor to clamp. Mode 0 (DigitalNormalized, the default) or 2 (Analog) " +
                "is a different function of the same keys.");

            var expected = new Dictionary<string, string[]>
            {
                { "up",    new[] { "<Keyboard>/upArrow", "<Keyboard>/w" } },
                { "down",  new[] { "<Keyboard>/downArrow", "<Keyboard>/s" } },
                { "left",  new[] { "<Keyboard>/a", "<Keyboard>/leftArrow" } },
                { "right", new[] { "<Keyboard>/d", "<Keyboard>/rightArrow" } },
            };
            foreach (KeyValuePair<string, string[]> part in expected)
            {
                string[] bound = PathsOf(move, b => b.isPartOfComposite &&
                                                    string.Equals(b.name, part.Key, StringComparison.OrdinalIgnoreCase));
                CollectionAssert.AreEqual(part.Value, bound,
                    $"{map}/Move '{part.Key}' is not the letter and the arrow it always was.");
            }

            Assert.AreEqual(9, move.bindings.Count,
                $"{map}/Move carries bindings beyond the composite and its eight parts — a stray key would " +
                "be a walk key nobody documented.");
            foreach (InputBinding b in move.bindings.Where(b => b.isPartOfComposite))
                Assert.AreEqual(InputBindings.KeyboardMouseScheme, b.groups,
                    $"{map}/Move '{b.path}' is not in the {InputBindings.KeyboardMouseScheme} scheme.");
        }

        [Test]
        public void TheSprintIsEitherShift()
        {
            InputAction sprint = ActionOf("Walk", "Sprint");
            Assert.AreEqual(InputActionType.Button, sprint.type);
            CollectionAssert.AreEqual(new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift" }, PathsOf(sprint),
                "the sprint was `leftShiftKey.isPressed || rightShiftKey.isPressed` — either Shift, held.");
        }

        [Test]
        public void TheOnFootPressesAreDeclared_WhereTheirReadersWillFindThemInPr1()
        {
            CollectionAssert.AreEqual(new[] { "<Keyboard>/e" }, PathsOf(ActionOf("Walk", "Interact")), "E interacts.");
            CollectionAssert.AreEqual(new[] { "<Keyboard>/escape" }, PathsOf(ActionOf("Walk", "Cancel")), "Esc cancels.");
            CollectionAssert.AreEqual(new[] { "<Keyboard>/q" }, PathsOf(ActionOf("Walk", "Mooring")),
                "Q works a moored boat's line from the wharf (ControlSwitcher.ToggleMooring).");
            CollectionAssert.AreEqual(new[] { "<Keyboard>/e" }, PathsOf(ActionOf("Deck", "Interact")), "E interacts on deck.");
        }

        // =============================================================================================
        //  the pure map, and the two gates
        // =============================================================================================

        [Test]
        public void TheWalkMapIsTheStruct_AndTheGatesReachExactlyAsFarAsTheyShould()
        {
            var move = new Vector2(-1f, 1f);   // W + A: the diagonal the composite sums, unclamped

            WalkIntents open = DeviceWalkIntentSource.Map(move, true, true, true, worldStopped: false, moveClaimed: false);
            Assert.AreEqual(move, open.Move, "an ungated move is handed on as read.");
            Assert.IsTrue(open.Sprint);
            Assert.IsTrue(open.Interact);
            Assert.IsTrue(open.Cancel);

            WalkIntents stopped = DeviceWalkIntentSource.Map(move, true, true, true, worldStopped: true, moveClaimed: false);
            Assert.AreEqual(Vector2.zero, stopped.Move, "the shell holding the world takes the move…");
            Assert.IsFalse(stopped.Sprint, "…and the sprint…");
            Assert.IsFalse(stopped.Interact, "…and the press: the controls are PARKED, not merely deaf.");
            Assert.IsFalse(stopped.Cancel);

            WalkIntents claimed = DeviceWalkIntentSource.Map(move, true, true, true, worldStopped: false, moveClaimed: true);
            Assert.AreEqual(Vector2.zero, claimed.Move,
                "a UI owning the move axis (a wardrobe, the notebook) takes the move — arrowing down its " +
                "list must not walk the fisher out of the fixture she is standing at.");
            Assert.IsTrue(claimed.Interact,
                "…but NOT the press: the picker steers on the axis and CONFIRMS on Interact (MoveActionClaim's " +
                "own contract), so the press must still arrive.");
            Assert.IsTrue(claimed.Cancel, "and Cancel still arrives, for the same reason.");

            WalkIntents both = DeviceWalkIntentSource.Map(move, true, true, true, worldStopped: true, moveClaimed: true);
            Assert.AreEqual(WalkIntents.None.Move, both.Move);
            Assert.IsFalse(both.Interact, "a stopped world wins over a claim.");
        }

        [Test]
        public void TheDeckMapIsTheStruct_UnderTheSameGates()
        {
            var move = new Vector2(1f, -1f);
            DeckIntents open = DeviceDeckIntentSource.Map(move, true, false, false);
            Assert.AreEqual(move, open.Move);
            Assert.IsTrue(open.Interact);

            DeckIntents stopped = DeviceDeckIntentSource.Map(move, true, true, false);
            Assert.AreEqual(Vector2.zero, stopped.Move,
                "the pause menu now holds a DECK she is standing on too — before the seam the deck walk " +
                "read the keys straight through a pause (ADR 0043 §'what is not byte-identical').");
            Assert.IsFalse(stopped.Interact);

            DeckIntents claimed = DeviceDeckIntentSource.Map(move, true, false, true);
            Assert.AreEqual(Vector2.zero, claimed.Move,
                "the notebook open on deck no longer both scrolls the page and walks her.");
            Assert.IsTrue(claimed.Interact);
        }

        [Test]
        public void NoKeyHeldIsNoIntent()
        {
            // The live read on this box: nobody is holding a key in a test run, and a box with no keyboard
            // device answers the same through the composite — "no device is no key held".
            var walk = new DeviceWalkIntentSource();
            Assert.IsTrue(walk.IsBound, "the Walk map's four actions must all resolve.");
            WalkIntents w = walk.Read();
            Assert.AreEqual(Vector2.zero, w.Move, "the walk read a move with no key held.");
            Assert.IsFalse(w.Sprint, "the walk read a sprint with no key held.");
            Assert.IsFalse(w.Interact);
            Assert.IsFalse(w.Cancel);

            var deck = new DeviceDeckIntentSource();
            Assert.IsTrue(deck.IsBound, "the Deck map's two actions must all resolve.");
            DeckIntents d = deck.Read();
            Assert.AreEqual(Vector2.zero, d.Move, "the deck read a move with no key held.");
            Assert.IsFalse(d.Interact);

            Assert.AreEqual(0, _deviceChanges.Count,
                "an idle read reported a device — activeControl is null while nothing is actuated, and " +
                "the last-used device must stand.");
        }

        // =============================================================================================
        //  the held sources
        // =============================================================================================

        [Test]
        public void AHeldWalkOutlivesTheFrameItWasSetIn()
        {
            var held = new HeldWalkIntents();
            Assert.AreEqual(0, held.Reads);

            held.Walk(new Vector2(0.6f, -0.8f), sprint: true);
            for (int frame = 0; frame < 3; frame++)
            {
                WalkIntents w = held.Read();
                Assert.AreEqual(new Vector2(0.6f, -0.8f), w.Move, $"frame {frame}: the move did not hold.");
                Assert.IsTrue(w.Sprint, $"frame {frame}: the sprint did not hold.");
                Assert.IsFalse(w.Interact, "a held walk is not a press.");
            }
            Assert.AreEqual(3, held.Reads, "the source did not count the frames it answered.");

            held.Release();
            WalkIntents released = held.Read();
            Assert.AreEqual(Vector2.zero, released.Move, "released, and still walking.");
            Assert.IsFalse(released.Sprint);
            Assert.AreEqual(4, held.Reads);

            var deck = new HeldDeckIntents();
            deck.Walk(Vector2.up);
            Assert.AreEqual(Vector2.up, deck.Read().Move);
            Assert.AreEqual(Vector2.up, deck.Read().Move, "the deck move did not hold.");
            Assert.AreEqual(2, deck.Reads);
        }

        [Test]
        public void TheWalkReadsTheAssetUntilHandedAnotherSource()
        {
            var go = new GameObject("fisher");
            try
            {
                var walk = go.AddComponent<PlayerWalkController>();
                Assert.IsInstanceOf<DeviceWalkIntentSource>(walk.WalkInputSource,
                    "with nothing configured the walk must read the bindings asset — the read the seam " +
                    "replaced, not silence.");

                var held = new HeldWalkIntents();
                walk.ConfigureWalkInput(held);
                Assert.AreSame(held, walk.WalkInputSource, "a configured source is not the one the walk exposes.");

                walk.ConfigureWalkInput(null);
                Assert.IsInstanceOf<DeviceWalkIntentSource>(walk.WalkInputSource, "null did not restore the asset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheDeckAndTheCabinReadTheAssetUntilHandedAnotherSource()
        {
            var go = new GameObject("fisher");
            go.SetActive(false);   // the arrival's Awake wants a world; the seam does not
            try
            {
                var deck = go.AddComponent<DeckWalkController>();
                Assert.IsInstanceOf<DeviceDeckIntentSource>(deck.DeckInputSource);
                var heldDeck = new HeldDeckIntents();
                deck.ConfigureDeckInput(heldDeck);
                Assert.AreSame(heldDeck, deck.DeckInputSource);
                deck.ConfigureDeckInput(null);
                Assert.IsInstanceOf<DeviceDeckIntentSource>(deck.DeckInputSource);

                var arrival = go.AddComponent<ArrivalOpening>();
                Assert.IsInstanceOf<DeviceDeckIntentSource>(arrival.CabinInputSource,
                    "the cabin walk is the deck walk with a smaller floor: the SAME source type, the same map.");
                var heldCabin = new HeldDeckIntents();
                arrival.ConfigureWalkInput(heldCabin);
                Assert.AreSame(heldCabin, arrival.CabinInputSource);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // =============================================================================================
        //  the device signal
        // =============================================================================================

        [Test]
        public void TheDeviceSignalPublishesOnAChangeOnly_AndStartsOnTheKeyboard()
        {
            Assert.AreEqual(ControlDevice.KeyboardMouse, ActiveControlDevice.Current,
                "a box with nothing plugged in shows keyboard glyphs from the first frame (ADR 0005).");

            ActiveControlDevice.Report(ControlDevice.KeyboardMouse);
            Assert.AreEqual(0, _deviceChanges.Count, "reporting the current device is not a change.");

            ActiveControlDevice.Report(ControlDevice.Gamepad);
            Assert.AreEqual(1, _deviceChanges.Count, "picking up the pad is one change.");
            Assert.AreEqual(ControlDevice.Gamepad, _deviceChanges[0].Device);
            Assert.AreEqual(ControlDevice.KeyboardMouse, _deviceChanges[0].Previous);
            Assert.AreEqual(ControlDevice.Gamepad, ActiveControlDevice.Current);

            ActiveControlDevice.Report(ControlDevice.Gamepad);
            Assert.AreEqual(1, _deviceChanges.Count, "a pad held for a thousand frames is not a thousand changes.");

            ActiveControlDevice.Report(ControlDevice.KeyboardMouse);
            Assert.AreEqual(2, _deviceChanges.Count, "and going back to the keys is the second.");
            Assert.AreEqual(ControlDevice.Gamepad, _deviceChanges[1].Previous);
        }

        [Test]
        public void OnlyAPadClassifiesAsAPad()
        {
            Assert.AreEqual(ControlDevice.KeyboardMouse, InputBindings.DeviceOf(null), "no control is the keyboard.");

            Keyboard kb = InputSystem.AddDevice<Keyboard>();
            Gamepad gp = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.AreEqual(ControlDevice.KeyboardMouse, InputBindings.DeviceOf(kb.wKey));
                Assert.AreEqual(ControlDevice.Gamepad, InputBindings.DeviceOf(gp.leftStick.up),
                    "a stick's part is the pad's — the classification is by DEVICE, not by control.");
                Assert.AreEqual(ControlDevice.Gamepad, InputBindings.DeviceOf(gp.buttonSouth));
            }
            finally
            {
                InputSystem.RemoveDevice(gp);
                InputSystem.RemoveDevice(kb);
            }
        }

        // =============================================================================================
        //  the laws, mechanised
        // =============================================================================================

        [Test]
        public void CoreReferencesNoInputSystem_Ever()
        {
            string asmdef = File.ReadAllText("Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef");
            StringAssert.DoesNotContain("Unity.InputSystem", asmdef,
                "ADR 0043 §1: Core holds the intents, the interfaces and the held sources — POCOs. The device " +
                "layer lives where Unity.InputSystem already is (Player/Boats/UI/App), never in Core.");
        }

        [Test]
        public void TheSeamedReadersPollNoDevice()
        {
            // The accepted-when criterion, mechanised for the readers PR 0 seams. PR 1 and PR 2 extend
            // this list until it is the whole of gameplay outside the dev rigs and the pointer overlays.
            foreach (string file in new[]
            {
                "Assets/_Project/Code/Player/PlayerWalkController.cs",
                "Assets/_Project/Code/Player/DeckWalkController.cs",
                "Assets/_Project/Code/App/ArrivalOpening.cs",
            })
            {
                // Code lines only: a comment may NAME the old read (it is history worth keeping);
                // a name guard that reddened on its own explanation would prove nothing.
                string src = string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//")));
                StringAssert.DoesNotContain("Keyboard.current", src, $"{file} polls the keyboard again.");
                StringAssert.DoesNotContain("Mouse.current", src, $"{file} polls the mouse again.");
                StringAssert.DoesNotContain("using UnityEngine.InputSystem;", src,
                    $"{file} names the device layer — it consumes intents and nothing else.");
            }
        }
    }
}
