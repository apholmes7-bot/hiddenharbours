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
        public void ThePanel_StandsOnThePopupsCorner_AndGrowsUpward()
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            float width = InteractChooserLayout.MinPanelWidth;
            Vector2 panel = InteractChooserLayout.PanelAnchoredPosition(home, 3, width);
            Assert.AreEqual(home.x, panel.x, 0.001f, "at the popup's width, the popup's left edge");
            float panelBottom = panel.y - InteractChooserLayout.PanelHeight(3);
            Assert.AreEqual(home.y - InteractPopupLayout.PanelHeight, panelBottom, 0.001f, "the popup's bottom edge");
            Assert.AreEqual(InteractChooserLayout.InsetTop + 3 * InteractChooserLayout.RowHeight
                            + InteractChooserLayout.InsetBottom, InteractChooserLayout.PanelHeight(3), 0.001f);
            Assert.Less(-panel.y, InteractPopupLayout.RefH, "and on the screen");
        }

        [Test]
        public void EvenAFullList_StaysOnTheScreen()
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 top = InteractChooserLayout.PanelAnchoredPosition(home, InteractChoiceList.HardCap,
                                                                      InteractChooserLayout.MaxPanelWidth);
            Assert.LessOrEqual(top.y, 0f, "the panel's top edge is inside the host");
            Assert.GreaterOrEqual(top.x, 0f, "and its left edge");
        }

        [Test]
        public void EveryMeasure_IsTheBubbleKitsOwn_OnItsGrid()
        {
            // The same reference canvas as the bubble, so a kit pixel is ArtScale units in both.
            Assert.AreEqual(DialogueBubbleKit.ArtScale, InteractChooserLayout.Kit);
            Assert.AreEqual(HudBandLayout.RefW, PaperUi.ReferenceResolution.x);
            Assert.AreEqual(HudBandLayout.RefH, PaperUi.ReferenceResolution.y);
            Assert.AreEqual(DialogueBubbleKit.OptionRowHeight * DialogueBubbleKit.ArtScale, InteractChooserLayout.RowHeight);
            Assert.AreEqual(HarbourType.GlyphHeight * DialogueBubbleKit.ArtScale, InteractChooserLayout.FontSize,
                "the face at a whole multiple of its baked size, never resampled");
            Assert.AreEqual(DialogueBubbleKit.OptionTextX * DialogueBubbleKit.ArtScale, InteractChooserLayout.TextX);
            Assert.AreEqual((DialogueBubbleKit.PanelInsetL - 1) * DialogueBubbleKit.ArtScale, InteractChooserLayout.PillInset);
            Assert.AreEqual(0f, InteractChooserLayout.TextDrop % InteractChooserLayout.Kit, 0.001f,
                "the words start a whole number of kit pixels into the row, never centred onto a half pixel");
            Assert.Greater(InteractChooserLayout.TextDrop, 0f);
            Assert.AreEqual(InteractChooserLayout.RowTop(2) - InteractChooserLayout.TextDrop,
                InteractChooserLayout.TextAnchoredPosition(2).y, 0.001f);
            Assert.AreEqual(7f, InteractPopupLayout.SnapToScreen(4.4f, 1.5f) * 1.5f, 0.001f,
                "a corner lands on a whole screen pixel at 1080p's 1.5x");
            foreach (float w in new[] { 0f, 41f, 300f, 437f, 5000f })
                Assert.AreEqual(0f, InteractChooserLayout.PanelWidth(w) % InteractChooserLayout.Kit, 0.001f,
                    "the panel is whole kit pixels wide, so its 9-slice ring lands on the grid");
        }

        [Test]
        public void ARowSays_ItsKey_ItsVerb_AndWhyWhenGreyed()
        {
            Assert.AreEqual("1  Open the door", InteractChooserLayout.RowText(0, "Open the door", InteractChoiceBlock.None));
            Assert.AreEqual("3  Sleep (taken)", InteractChooserLayout.RowText(2, "Sleep", InteractChoiceBlock.Occupied));
            Assert.AreEqual("2  Sell (not now)", InteractChooserLayout.RowText(1, "Sell", InteractChoiceBlock.Unavailable));
            string cut = InteractChooserLayout.RowText(8, new string('x', 80), InteractChoiceBlock.Occupied);
            Assert.AreEqual(InteractChooserLayout.MaxRowCharacters, cut.Length, "one line, cut at the kit's widest line");
            StringAssert.EndsWith(InteractChooserStrings.Cut, cut);
            Assert.AreEqual(-1, cut.IndexOf('…'), "no ellipsis glyph: the face has none");
        }

        [Test]
        public void ALongVerb_IsNotCut_ThePanelWidensToIt()
        {
            // The 2026-11-15 editor look at the Route 91 pumps: row 1 read "Fill up with two-stroke ...".
            string row = InteractChooserLayout.RowText(0, "Fill up with two-stroke mix", InteractChoiceBlock.None);
            Assert.AreEqual("1  Fill up with two-stroke mix", row, "whole, no cut");

            float width = InteractChooserLayout.PanelWidth(InteractChooserLayout.EstimateTextUnits(row));
            Assert.Greater(width, InteractPopupLayout.PanelWidth, "wider than the popup's strip");
            Assert.GreaterOrEqual(InteractChooserLayout.TextSize(width).x,
                InteractChooserLayout.EstimateTextUnits(row) + InteractChooserLayout.Kit,
                "the words' box holds the words with at least a kit pixel spare (the look measured 450 of 450)");
            Assert.AreEqual(InteractChooserLayout.MinPanelWidth, InteractChooserLayout.PanelWidth(40f),
                "short words keep the popup's width");
            Assert.AreEqual(InteractChooserLayout.MaxPanelWidth, InteractChooserLayout.PanelWidth(5000f),
                "and the list never becomes a banner");
            Assert.LessOrEqual(InteractChooserLayout.EstimateTextUnits(new string('x', InteractChooserLayout.MaxRowCharacters)),
                InteractChooserLayout.TextSize(InteractChooserLayout.MaxPanelWidth).x,
                "the longest row the cap allows fits the widest panel");
        }

        [Test]
        public void AWiderPanel_GrowsLeft_KeepsThePopupsRightEdge_AndThePointerFollows()
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            const float width = 420f;
            Vector2 panel = InteractChooserLayout.PanelAnchoredPosition(home, 2, width);
            Assert.AreEqual(home.x + InteractPopupLayout.PanelWidth, panel.x + width, 0.001f, "same right edge");

            var popupPx = new Rect(900f, 20f, 300f, 44f);
            Rect bottom = InteractChooserLayout.RowScreenRect(popupPx, 1, 2, 1f, width);
            Assert.AreEqual(popupPx.xMax, bottom.xMax, 0.001f);
            Assert.AreEqual(width, bottom.width, 0.001f);
            float y = 20f + InteractChooserLayout.InsetBottom + 1f;
            Assert.AreEqual(1, InteractChooserLayout.HitRow(new Vector2(800f, y), popupPx, 2, 1f, width),
                "a point left of the popup's strip but on the wider row hits it");
            Assert.AreEqual(-1, InteractChooserLayout.HitRow(new Vector2(800f, y), popupPx, 2, 1f,
                InteractChooserLayout.MinPanelWidth), "and misses at the popup's width");
        }

        [Test]
        public void ThePointer_HitsTheRowItIsOn_TheRowsMeet_AndTheInsetsHitNothing()
        {
            var popupPx = new Rect(900f, 20f, 300f, 44f);
            const float scale = 2f, width = 300f;
            float b = 20f + InteractChooserLayout.InsetBottom * scale, row = InteractChooserLayout.RowHeight * scale;
            Assert.AreEqual(2, InteractChooserLayout.HitRow(new Vector2(1000f, b + 1f), popupPx, 3, scale, width), "the bottom row");
            Assert.AreEqual(1, InteractChooserLayout.HitRow(new Vector2(1000f, b + row), popupPx, 3, scale, width),
                "the rows meet: the seam belongs to the row above, never to nothing");
            Assert.AreEqual(0, InteractChooserLayout.HitRow(new Vector2(1000f, b + 2.5f * row), popupPx, 3, scale, width), "the top row");
            Assert.AreEqual(-1, InteractChooserLayout.HitRow(new Vector2(1000f, 21f), popupPx, 3, scale, width),
                "the panel's bottom inset is no row");
            Assert.AreEqual(-1, InteractChooserLayout.HitRow(new Vector2(100f, b + 1f), popupPx, 3, scale, width), "off the list");
        }

        [Test]
        public void TheSelectedRow_IsTheBubblesGoldPill_AndItsWordsAreTheHeaviestInk()
        {
            // The kit's own colours: the panel paper, the pill gold, the words notebook ink.
            Assert.AreEqual(NotebookInk.Paper, InteractChooserStyle.PanelTint(false));
            Assert.AreEqual(NotebookInk.Gold, InteractChooserStyle.PillTint(false));
            Assert.AreEqual(Color.white, InteractChooserStyle.PanelTint(true), "the linked art carries its own colour");
            Assert.AreEqual(Color.white, InteractChooserStyle.PillTint(true));

            Color paper = NotebookInk.Paper, gold = NotebookInk.Gold;
            Assert.Greater(Distance(gold, paper), 0.25f, "the pill is a shape in another colour, not a shade of the panel");

            Color picked = InteractChooserStyle.WordsFor(true, true), plain = InteractChooserStyle.WordsFor(false, true);
            Assert.AreEqual(1f, picked.a, "the selected words are full strength");
            Assert.LessOrEqual(Luma(picked), Luma(plain),
                "and at least as heavy as any other row's (the 2026-11-15 look: dark words read thin on the bar)");
            Assert.Greater(Luma(gold) - Luma(picked), 0.5f, "dark ink on the gold");
            Assert.Greater(Luma(paper) - Luma(plain), 0.6f, "dark ink on the paper");
            Assert.Less(InteractChooserStyle.WordsFor(false, false).a, plain.a, "a greyed row is faint ink");
        }

        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        private static float Distance(Color a, Color b)
            => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        [Test]
        public void TheLink_PointsAtTheBubblesOwnSprites_NotCopies()
        {
            BubbleKitLink.ForgetCache();
            BubbleKitLink link = BubbleKitLink.Shipped;
            Assert.IsNotNull(link, "Resources/" + BubbleKitLink.ResourceKey);
            Assert.IsNotNull(link.Panel);
            Assert.IsNotNull(link.Gold);
            Assert.IsNotNull(link.Cursor);
            Assert.AreEqual(DialogueBubbleKit.PiecePanel, link.Panel.name);
            Assert.AreEqual(DialogueBubbleKit.PieceGold, link.Gold.name);
            Assert.AreEqual(DialogueBubbleKit.ShippedCursor, link.Cursor.name, "the owner's shipped hand");
            Assert.AreNotEqual(Vector4.zero, link.Panel.border, "the panel is the kit's 9-slice");
            Assert.AreNotEqual(Vector4.zero, link.Gold.border, "and so is the pill");
#if UNITY_EDITOR
            StringAssert.StartsWith("Assets/_Project/Art/UI/DialogueBubble/",
                UnityEditor.AssetDatabase.GetAssetPath(link.Panel), "the bubble's own file, not a copy");
            StringAssert.StartsWith("Assets/_Project/Art/UI/DialogueBubble/",
                UnityEditor.AssetDatabase.GetAssetPath(link.Gold));
            StringAssert.StartsWith("Assets/_Project/Art/UI/DialogueBubble/",
                UnityEditor.AssetDatabase.GetAssetPath(link.Cursor));
#endif
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

                // The bubble's own pieces, linked; the game's one face.
                Assert.IsTrue(view.HasKitArt, "the panel, the pill and the hand are the kit's");
                BubbleKitLink link = BubbleKitLink.Shipped;
                view.ShownArt(out Sprite panelArt, out Sprite pillArt, out Sprite handArt);
                Assert.AreSame(link.Panel, panelArt, "the same sprite object the bubble wears, not a copy");
                Assert.AreSame(link.Gold, pillArt);
                Assert.AreSame(link.Cursor, handArt);
                Assert.AreSame(Resources.Load<Font>(HarbourType.ResourceKey), view.ShownFace, "the face");

                view.ShownSelection(out int pillRow, out int handRow);
                Assert.AreEqual(0, pillRow, "the list opens on row 1: the gold pill behind it");
                Assert.AreEqual(0, handRow, "and the hand beside it");
                Assert.AreEqual(InteractChooserStyle.SelectedWords, view.ShownWords(0), "in the strongest ink");
                Assert.AreEqual(InteractChooserStyle.Words, view.ShownWords(1));

                InteractChooser.Step(1);
                view.Tick();
                view.ShownSelection(out pillRow, out handRow);
                Assert.AreEqual(1, pillRow, "a step moves the pill with it");
                Assert.AreEqual(1, handRow, "and the hand");
                Assert.AreEqual(InteractChooserStyle.Words, view.ShownWords(0));
                Assert.AreEqual(InteractChooserStyle.SelectedWords, view.ShownWords(1));
                Assert.GreaterOrEqual(view.ShownWidth, InteractChooserLayout.MinPanelWidth);

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
