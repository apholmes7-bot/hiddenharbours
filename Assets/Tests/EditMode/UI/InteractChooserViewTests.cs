using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>The interaction chooser's UI half</b> (owner ask 2026-11-15: "select the desired action with keys
    /// or mouse"): where the rows sit, what they say, which row the pointer is on, and what one frame of
    /// the player's hands does to the open list. Pure maths on <see cref="InteractChooserLayout"/> and
    /// <see cref="InteractChooserControls"/>, plus one drive of the real view through its held source.
    /// The LOOK is the coordinator's eyeball and the owner's walk.
    /// </summary>
    public class InteractChooserViewTests
    {
        private sealed class Fake : IInteractable
        {
            public string Id { get; set; }
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters => 2f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnFoot;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public string VerbLabel { get; set; }
            public int Calls;
            public void Interact(in InteractActor actor) => Calls++;
        }

        private int _frame;
        private GameConfig _savedConfig;
        private Fake _door, _lid, _bench;

        private static InteractActor Actor() => InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot);

        [SetUp]
        public void SetUp()
        {
            _savedConfig = GameServices.Config;
            GameServices.Config = null;
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            _frame = 10;
            InteractChooser.FrameClock = () => _frame;

            _door = new Fake { Id = "a.door", WorldPosition = new Vector2(1f, 0f), VerbLabel = "Open the door" };
            _lid = new Fake { Id = "b.lid", WorldPosition = new Vector2(1.2f, 0f), VerbLabel = "Open the lid" };
            _bench = new Fake { Id = "c.bench", WorldPosition = new Vector2(1.4f, 0f), VerbLabel = "Sit" };
            Interactables.Register(_door);
            Interactables.Register(_lid);
            Interactables.Register(_bench);
        }

        [TearDown]
        public void TearDown()
        {
            InteractChooser.Reset();
            InteractVerb.Reset();
            Interactables.Clear();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            GameServices.Config = _savedConfig;
        }

        private void OpenAndAdvance()
        {
            Assert.IsTrue(InteractVerb.TryPress(Actor(), InteractResolver.DefaultFacingArcDegrees));
            Assert.IsTrue(InteractChooser.IsOpen);
            _frame++;
        }

        private static ChooserIntents Intents(float nav = 0f, int pick = 0, bool confirm = false, bool cancel = false,
                                              bool click = false, Vector2 point = default)
            => new ChooserIntents(nav, pick, confirm, cancel, click, point);

        // =====================================================================================
        //  LAYOUT AND WORDS
        // =====================================================================================

        [Test]
        public void TheBottomRow_StandsWhereThePopupStands_AndTheRestClimbAboveIt()
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            Assert.AreEqual(home, InteractChooserLayout.RowAnchoredPosition(home, 2, 3), "the last row is the popup's place");
            Vector2 top = InteractChooserLayout.RowAnchoredPosition(home, 0, 3);
            Assert.AreEqual(home.x, top.x, "one column");
            Assert.AreEqual(home.y + 2 * InteractChooserLayout.RowPitch, top.y, 0.001f,
                "row 0 is two pitches higher (y grows down, so less negative)");
            Assert.Less(-top.y, InteractPopupLayout.RefH, "and still on the screen");
        }

        [Test]
        public void EvenAFullList_StaysOnTheScreen()
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 top = InteractChooserLayout.RowAnchoredPosition(home, 0, InteractChoiceList.HardCap);
            Assert.LessOrEqual(top.y, 0f, "the top row's top edge is inside the host");
        }

        [Test]
        public void ARowSays_ItsKey_ItsVerb_AndWhyWhenGreyed()
        {
            Assert.AreEqual("1  Open the door", InteractChooserLayout.RowText(0, "Open the door", InteractChoiceBlock.None));
            Assert.AreEqual("3  Sleep (taken)", InteractChooserLayout.RowText(2, "Sleep", InteractChoiceBlock.Occupied));
            Assert.AreEqual("2  Sell (not now)", InteractChooserLayout.RowText(1, "Sell", InteractChoiceBlock.Unavailable));
            Assert.LessOrEqual(
                InteractChooserLayout.RowText(8, new string('x', 80), InteractChoiceBlock.Occupied).Length,
                InteractPopupLayout.MaxCharacters, "one line, cut like the popup's");
        }

        [Test]
        public void ThePointer_HitsTheRowItIsOn_AndTheSeamsHitNothing()
        {
            var bottom = new Rect(900f, 20f, 300f, 44f);
            const float scale = 1f;
            Assert.AreEqual(2, InteractChooserLayout.HitRow(new Vector2(1000f, 40f), bottom, 3, scale), "the bottom row");
            float topY = 20f + 2 * InteractChooserLayout.RowPitch + 10f;
            Assert.AreEqual(0, InteractChooserLayout.HitRow(new Vector2(1000f, topY), bottom, 3, scale), "the top row");
            float seam = 20f + 44f + InteractChooserLayout.RowGap * 0.5f;
            Assert.AreEqual(-1, InteractChooserLayout.HitRow(new Vector2(1000f, seam), bottom, 3, scale), "the gap");
            Assert.AreEqual(-1, InteractChooserLayout.HitRow(new Vector2(100f, 40f), bottom, 3, scale), "off the list");
        }

        // =====================================================================================
        //  THE HANDS
        // =====================================================================================

        [Test]
        public void TheOpeningPress_DoesNotAlsoPick()
        {
            Assert.IsTrue(InteractVerb.TryPress(Actor(), InteractResolver.DefaultFacingArcDegrees));
            var latch = new AxisLatch();
            Assert.AreEqual(InteractChooserControls.Outcome.Nothing,
                InteractChooserControls.Apply(Intents(confirm: true), latch, -1, false),
                "the E that opened the list is still down this frame");
            Assert.IsTrue(InteractChooser.IsOpen);
            Assert.AreEqual(0, _door.Calls);
        }

        [Test]
        public void UpAndDown_StepOneRowPerPush_AndConfirmDoesTheRow()
        {
            OpenAndAdvance();
            var latch = new AxisLatch();
            Assert.AreEqual(InteractChooserControls.Outcome.Stepped,
                InteractChooserControls.Apply(Intents(nav: -1f), latch, -1, false), "down");
            Assert.AreEqual(1, InteractChooser.Rows.Selected);
            Assert.AreEqual(InteractChooserControls.Outcome.Nothing,
                InteractChooserControls.Apply(Intents(nav: -1f), latch, -1, false), "a held key does not run on");
            InteractChooserControls.Apply(Intents(), latch, -1, false);
            InteractChooserControls.Apply(Intents(nav: 1f), latch, -1, false);
            Assert.AreEqual(0, InteractChooser.Rows.Selected, "up");
            InteractChooserControls.Apply(Intents(), latch, -1, false);
            InteractChooserControls.Apply(Intents(nav: 1f), latch, -1, false);
            Assert.AreEqual(2, InteractChooser.Rows.Selected, "up from the top wraps to the bottom");

            Assert.AreEqual(InteractChooserControls.Outcome.Confirmed,
                InteractChooserControls.Apply(Intents(confirm: true), latch, -1, false));
            Assert.AreEqual(1, _bench.Calls);
            Assert.IsFalse(InteractChooser.IsOpen);
        }

        [Test]
        public void ANumberKey_PicksThatRow_AndOneTooHighDoesNothing()
        {
            OpenAndAdvance();
            var latch = new AxisLatch();
            Assert.AreEqual(InteractChooserControls.Outcome.Refused,
                InteractChooserControls.Apply(Intents(pick: 7), latch, -1, false), "no seventh row");
            Assert.IsTrue(InteractChooser.IsOpen);
            Assert.AreEqual(InteractChooserControls.Outcome.Picked,
                InteractChooserControls.Apply(Intents(pick: 2), latch, -1, false));
            Assert.AreEqual(1, _lid.Calls);
        }

        [Test]
        public void TheMouse_HoverSelects_AndAClickPicks()
        {
            OpenAndAdvance();
            var latch = new AxisLatch();
            Assert.AreEqual(InteractChooserControls.Outcome.Hovered,
                InteractChooserControls.Apply(Intents(), latch, hitRow: 2, pointerMoved: true));
            Assert.AreEqual(2, InteractChooser.Rows.Selected);
            Assert.AreEqual(InteractChooserControls.Outcome.Nothing,
                InteractChooserControls.Apply(Intents(), latch, hitRow: 0, pointerMoved: false),
                "a still pointer resting on a row does not steal the selection back");
            Assert.AreEqual(2, InteractChooser.Rows.Selected);

            Assert.AreEqual(InteractChooserControls.Outcome.Nothing,
                InteractChooserControls.Apply(Intents(click: true), latch, hitRow: -1, pointerMoved: false),
                "a click off the list picks nothing");
            Assert.AreEqual(InteractChooserControls.Outcome.Picked,
                InteractChooserControls.Apply(Intents(click: true), latch, hitRow: 0, pointerMoved: false));
            Assert.AreEqual(1, _door.Calls);
        }

        [Test]
        public void Cancel_PutsTheListDown_DoingNothing()
        {
            OpenAndAdvance();
            Assert.AreEqual(InteractChooserControls.Outcome.Cancelled,
                InteractChooserControls.Apply(Intents(cancel: true, confirm: true), new AxisLatch(), -1, false),
                "cancel outranks everything else in the frame");
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(0, _door.Calls + _lid.Calls + _bench.Calls);
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        // =====================================================================================
        //  THE REAL VIEW, DRIVEN
        // =====================================================================================

        [Test]
        public void TheView_DrawsTheRows_AndAHeldSourceDrivesIt()
        {
            var go = new GameObject("ChooserViewTest");
            var view = go.AddComponent<InteractChooserView>();
            var held = new HeldChooserIntents();
            view.ConfigureChooserInput(held);
            try
            {
                view.Tick();
                Assert.IsFalse(view.IsShowing, "nothing open, nothing drawn");

                OpenAndAdvance();
                view.Tick();
                Assert.IsTrue(view.IsShowing);
                Assert.AreEqual(3, view.ShownCount);
                Assert.AreEqual("1  Open the door", view.ShownRow(0));
                Assert.AreEqual("3  Sit", view.ShownRow(2));

                held.PressNumber(3);
                view.Tick();
                held.Release();
                Assert.AreEqual(1, _bench.Calls, "the third number key did the third row");
                Assert.IsFalse(view.IsShowing, "and the list went down");
                Assert.Greater(held.Reads, 0, "the view consulted its source");
            }
            finally
            {
                Object.DestroyImmediate(view.CanvasObject);
                Object.DestroyImmediate(go);
            }
        }
    }
}
