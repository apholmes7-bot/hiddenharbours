using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The wardrobe picker's pure parts — which row the cursor is on, what a confirm would wear, what a
    /// cancel must put back, where the panel sits and which colour a row shows.
    ///
    /// <para><b>The property these exist for</b> is that a PREVIEW CANNOT REACH THE SAVE. In the running
    /// game that is a claim about a component, a signal, a locker and a renderer; here it is one readonly
    /// field, and these tests are the reason it can be stated as a fact rather than as a hope. The
    /// wardrobe's own PlayMode test drives the real thing end to end; this is what makes a red there
    /// legible.</para>
    /// </summary>
    public class WardrobePickerModelTests
    {
        private static readonly List<string> Eight = new List<string>
        {
            "outfit.harbour_teal", "outfit.deep_navy", "outfit.oilskins", "outfit.rust_and_cream",
            "outfit.squall_grey", "outfit.spruce", "outfit.capelin", "outfit.dogwatch",
        };

        /// <summary>One push of the axis: over the threshold, then back to neutral. Feeding a held value
        /// twice is what the latch exists to ignore, so a test that "pushes" without releasing is testing
        /// nothing.</summary>
        private static bool Push(WardrobePickerModel m, float axis)
        {
            bool moved = m.Step(axis);
            m.Step(0f);
            return moved;
        }

        // =============================================================================
        //  opening
        // =============================================================================

        [Test]
        public void It_opens_on_the_outfit_the_player_is_already_wearing()
        {
            var m = new WardrobePickerModel(Eight, "outfit.oilskins");

            Assert.AreEqual(2, m.Index, "the cursor starts on the row you are wearing, not at the top");
            Assert.AreEqual("outfit.oilskins", m.SelectedOutfitId);
        }

        [Test]
        public void A_player_in_the_baked_look_opens_at_the_top_and_reverts_to_the_baked_look()
        {
            var m = new WardrobePickerModel(Eight, "");

            Assert.AreEqual(0, m.Index, "an empty worn id is the baked look, which is not a row");
            Assert.AreEqual(Eight[0], m.SelectedOutfitId);
            Assert.AreEqual("", m.RevertOutfitId,
                            "and cancelling must put the BAKED look back, not the first row");
        }

        [Test]
        public void A_null_worn_id_is_normalised_rather_than_carried()
        {
            var m = new WardrobePickerModel(Eight, null);

            Assert.NotNull(m.RevertOutfitId, "never null — a caller must be able to compare without guarding");
            Assert.AreEqual("", m.RevertOutfitId);
        }

        [Test]
        public void An_outfit_the_wardrobe_no_longer_holds_opens_at_the_top_and_is_STILL_what_cancel_restores()
        {
            // A retired outfit sitting in an old save. The list cannot point at it, but the save is the
            // player's and this picker has no business rewriting it on the way past.
            var m = new WardrobePickerModel(Eight, "outfit.retired_sou_wester");

            Assert.AreEqual(0, m.Index, "there is no row to start on, so the top");
            Assert.AreEqual("outfit.retired_sou_wester", m.RevertOutfitId,
                            "cancel restores what was SAVED, even when the wardrobe can no longer show it");
        }

        // =============================================================================
        //  stepping
        // =============================================================================

        [Test]
        public void One_push_moves_one_row_and_up_the_list_means_toward_the_top()
        {
            var m = new WardrobePickerModel(Eight, "outfit.oilskins");   // row 2

            Assert.IsTrue(Push(m, +1f));
            Assert.AreEqual(1, m.Index, "+1 is UP the list — toward the row drawn at the top");

            Assert.IsTrue(Push(m, -1f));
            Assert.AreEqual(2, m.Index);
        }

        [Test]
        public void A_held_axis_steps_once_and_then_waits_for_neutral()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);

            Assert.IsTrue(m.Step(-1f), "the first frame of the push moves");
            Assert.IsTrue(m.IsLatched);
            Assert.IsFalse(m.Step(-1f), "still held — no second row");
            Assert.IsFalse(m.Step(-1f));
            Assert.AreEqual(1, m.Index, "one push, one row, however many frames it was held for");

            Assert.IsFalse(m.Step(0f), "releasing is not itself a move");
            Assert.IsFalse(m.IsLatched);
            Assert.IsTrue(m.Step(-1f), "and now the next push counts");
            Assert.AreEqual(2, m.Index);
        }

        [Test]
        public void A_nudge_under_the_threshold_is_not_a_push()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);
            float drift = WardrobePickerModel.AxisThreshold - 0.01f;

            Assert.IsFalse(m.Step(drift), "stick drift must not step a row");
            Assert.IsFalse(m.Step(-drift));
            Assert.AreEqual(0, m.Index);
        }

        [Test]
        public void The_list_wraps_at_both_ends()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);

            Push(m, +1f);
            Assert.AreEqual(Eight.Count - 1, m.Index, "off the top lands on the bottom");

            Push(m, -1f);
            Assert.AreEqual(0, m.Index, "and back again");
        }

        [Test]
        public void A_one_outfit_wardrobe_cannot_be_stepped_but_can_be_confirmed()
        {
            var m = new WardrobePickerModel(new List<string> { "outfit.only" }, "");

            Assert.IsFalse(Push(m, -1f), "nowhere to go");
            Assert.AreEqual(0, m.Index);
            Assert.IsTrue(m.TryConfirm(out string id));
            Assert.AreEqual("outfit.only", id);
        }

        // =============================================================================
        //  the mouse's direct select
        // =============================================================================

        [Test]
        public void Selecting_a_row_directly_moves_the_cursor_and_reports_whether_it_moved()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);

            Assert.IsTrue(m.SelectIndex(5));
            Assert.AreEqual(5, m.Index);
            Assert.IsFalse(m.SelectIndex(5), "re-selecting the row you are on is not a move — no re-preview");
        }

        [Test]
        public void Selecting_a_row_that_is_not_there_is_ignored_rather_than_clamped()
        {
            var m = new WardrobePickerModel(Eight, Eight[3]);

            Assert.IsFalse(m.SelectIndex(-1));
            Assert.IsFalse(m.SelectIndex(Eight.Count));
            Assert.AreEqual(3, m.Index, "the cursor did not move to the nearest legal row");
        }

        [Test]
        public void Selecting_with_the_mouse_does_not_release_a_held_key()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);
            m.Step(-1f);                       // key down, latched
            Assert.IsTrue(m.IsLatched);

            m.SelectIndex(4);

            Assert.IsTrue(m.IsLatched, "a pointer has not released the key that is still held");
            Assert.IsFalse(m.Step(-1f), "so the held key still cannot step again");
        }

        // =============================================================================
        //  confirming and cancelling
        // =============================================================================

        [Test]
        public void Confirm_returns_the_row_the_cursor_is_on_and_nothing_else()
        {
            var m = new WardrobePickerModel(Eight, Eight[0]);
            Push(m, -1f);
            Push(m, -1f);

            Assert.IsTrue(m.TryConfirm(out string id));
            Assert.AreEqual(Eight[2], id);
            Assert.AreEqual(m.SelectedOutfitId, id, "the confirmed id IS the previewed id, structurally");
        }

        [Test]
        public void The_revert_target_never_moves_however_far_the_cursor_walks()
        {
            var m = new WardrobePickerModel(Eight, "outfit.spruce");

            for (int i = 0; i < Eight.Count * 3 + 1; i++) Push(m, -1f);

            Assert.AreEqual("outfit.spruce", m.RevertOutfitId,
                            "twenty-five pushes and the thing a cancel puts back is unchanged");
            Assert.AreNotEqual(m.RevertOutfitId, m.SelectedOutfitId,
                               "…and it is genuinely different from what is being previewed");
        }

        // =============================================================================
        //  the empty wardrobe — the negative control
        // =============================================================================

        [Test]
        public void An_empty_wardrobe_is_not_usable_and_cannot_be_confirmed()
        {
            var m = new WardrobePickerModel(new List<string>(), "outfit.harbour_teal");

            Assert.AreEqual(0, m.Count);
            Assert.IsFalse(m.IsUsable, "the picker must decline to open on this, not show an empty box");
            Assert.AreEqual("", m.SelectedOutfitId, "nothing is selected because there is nothing to select");
            Assert.IsFalse(m.TryConfirm(out string id), "and no caller can be handed a choice out of it");
            Assert.AreEqual("", id);
            Assert.IsFalse(Push(m, -1f));
            Assert.AreEqual("outfit.harbour_teal", m.RevertOutfitId,
                            "what the player was wearing is still known — an empty wardrobe undresses nobody");
        }

        [Test]
        public void A_null_list_is_the_same_as_an_empty_one_rather_than_a_throw()
        {
            var m = new WardrobePickerModel(null, "");

            Assert.AreEqual(0, m.Count);
            Assert.IsFalse(m.IsUsable);
            Assert.AreEqual("", m.OutfitIdAt(0));
        }

        // =============================================================================
        //  the panel's geometry
        // =============================================================================

        private static readonly Vector2 Canvas = new Vector2(1280f, 720f);

        [Test]
        public void The_panel_takes_the_right_of_the_wardrobe_when_there_is_room()
        {
            Vector2 size = WardrobePickerLayout.PanelSize(8);
            var place = WardrobePickerLayout.Solve(new Vector2(400f, 360f), size, Canvas,
                                                   WardrobePickerLayout.ScreenMargin,
                                                   WardrobePickerLayout.Gap);

            Assert.IsTrue(place.OnRight);
            Assert.Greater(place.PanelCentre.x, 400f, "…and it is actually to the right of the fixture");
            Assert.AreEqual(400f + WardrobePickerLayout.Gap + size.x * 0.5f, place.PanelCentre.x, 0.001f,
                            "cleared by exactly the gap, so the fixture and the fisher stay uncovered");
        }

        [Test]
        public void A_wardrobe_against_the_right_edge_puts_the_panel_on_the_left_instead()
        {
            Vector2 size = WardrobePickerLayout.PanelSize(8);
            var place = WardrobePickerLayout.Solve(new Vector2(1240f, 360f), size, Canvas,
                                                   WardrobePickerLayout.ScreenMargin,
                                                   WardrobePickerLayout.Gap);

            Assert.IsFalse(place.OnRight, "the right would run off the view, so it flips");
            Assert.Less(place.PanelCentre.x, 1240f);
            Assert.GreaterOrEqual(place.PanelCentre.x - size.x * 0.5f, WardrobePickerLayout.ScreenMargin - 0.001f,
                                  "and the flipped panel is still inside the margin");
        }

        [Test]
        public void The_panel_slides_into_the_frame_rather_than_being_cropped_at_top_or_bottom()
        {
            Vector2 size = WardrobePickerLayout.PanelSize(8);

            var high = WardrobePickerLayout.Solve(new Vector2(400f, 715f), size, Canvas,
                                                   WardrobePickerLayout.ScreenMargin,
                                                   WardrobePickerLayout.Gap);
            Assert.LessOrEqual(high.PanelCentre.y + size.y * 0.5f, Canvas.y - WardrobePickerLayout.ScreenMargin + 0.001f);

            var low = WardrobePickerLayout.Solve(new Vector2(400f, 4f), size, Canvas,
                                                  WardrobePickerLayout.ScreenMargin,
                                                  WardrobePickerLayout.Gap);
            Assert.GreaterOrEqual(low.PanelCentre.y - size.y * 0.5f, WardrobePickerLayout.ScreenMargin - 0.001f);
        }

        [Test]
        public void A_panel_taller_than_the_canvas_is_centred_rather_than_solved_into_an_inverted_range()
        {
            Vector2 tall = new Vector2(WardrobePickerLayout.PanelWidth, 900f);
            var place = WardrobePickerLayout.Solve(new Vector2(400f, 100f), tall, Canvas,
                                                   WardrobePickerLayout.ScreenMargin,
                                                   WardrobePickerLayout.Gap);

            Assert.AreEqual(WardrobePickerLayout.ScreenMargin + tall.y * 0.5f, place.PanelCentre.y, 0.001f,
                            "clamped to the low edge, never to a max below the min");
        }

        [Test]
        public void The_mouse_hit_test_agrees_with_where_the_rows_were_laid_out()
        {
            // The one property that matters: the builder positions row i at RowOffsetY(i), so a point AT
            // that offset must hit-test to i. Two spellings of this offset is exactly how a list selects
            // the row above the one you clicked.
            Vector2 size = WardrobePickerLayout.PanelSize(8);
            Vector2 centre = new Vector2(640f, 360f);

            for (int i = 0; i < 8; i++)
            {
                var point = centre + new Vector2(0f, WardrobePickerLayout.RowOffsetY(size, i));
                Assert.AreEqual(i, WardrobePickerLayout.RowIndexAt(point, centre, size, 8),
                                $"the centre of row {i} must hit-test to row {i}");
            }
        }

        [Test]
        public void The_hit_test_returns_no_row_off_the_list()
        {
            Vector2 size = WardrobePickerLayout.PanelSize(8);
            Vector2 centre = new Vector2(640f, 360f);

            Assert.AreEqual(-1, WardrobePickerLayout.RowIndexAt(centre + new Vector2(0f, size.y), centre, size, 8),
                            "above the heading");
            Assert.AreEqual(-1, WardrobePickerLayout.RowIndexAt(centre - new Vector2(0f, size.y), centre, size, 8),
                            "below the hint");
            Assert.AreEqual(-1, WardrobePickerLayout.RowIndexAt(centre + new Vector2(size.x, 0f), centre, size, 8),
                            "beside the panel");
            Assert.AreEqual(-1, WardrobePickerLayout.RowIndexAt(centre, centre, size, 0),
                            "an empty list has no rows to be over");
        }

        [Test]
        public void The_panel_grows_by_exactly_one_row_per_outfit()
        {
            float one = WardrobePickerLayout.PanelSize(1).y;
            float two = WardrobePickerLayout.PanelSize(2).y;

            Assert.AreEqual(WardrobePickerLayout.RowHeight, two - one, 0.001f);
            Assert.AreEqual(WardrobePickerLayout.PanelSize(0).y + WardrobePickerLayout.RowHeight, one, 0.001f);
        }

        // =============================================================================
        //  the colour chip
        // =============================================================================

        private static CharacterWardrobeDef WardrobeWithRamps()
        {
            var ramps = ScriptableObject.CreateInstance<CharacterRampsDef>();
            ramps.Axes = new[]
            {
                new CharacterRampAxis
                {
                    Axis = "outfit",
                    DefaultKey = "teal",
                    Ramps = new[]
                    {
                        new CharacterRamp
                        {
                            Id = "ramp.outfit.teal", Key = "teal", Label = "Teal",
                            Shades = new[] { new Color32(1, 1, 1, 255), new Color32(2, 2, 2, 255),
                                             new Color32(3, 3, 3, 255) },
                        },
                        new CharacterRamp
                        {
                            Id = "ramp.outfit.oil", Key = "oil", Label = "Oil",
                            Shades = new[] { new Color32(10, 10, 10, 255), new Color32(20, 20, 20, 255),
                                             new Color32(30, 30, 30, 255) },
                        },
                    },
                },
            };

            var wardrobe = ScriptableObject.CreateInstance<CharacterWardrobeDef>();
            wardrobe.Ramps = ramps;
            wardrobe.BakedOutfit = "teal";
            wardrobe.BakedShirt = "white";
            return wardrobe;
        }

        private static CharacterOutfitDef Outfit(string id, string outfit, string shirt)
        {
            var o = ScriptableObject.CreateInstance<CharacterOutfitDef>();
            o.Id = id;
            o.Label = id;
            o.Outfit = outfit;
            o.Shirt = shirt;
            return o;
        }

        [Test]
        public void The_chip_is_the_MIDDLE_shade_of_the_outfits_ramp()
        {
            var wardrobe = WardrobeWithRamps();
            Color c = WardrobeChip.ColourFor(wardrobe, Outfit("outfit.x", "oil", "navy"), Color.magenta);

            // Middle of a 3-step ramp, not the shadow end and not the highlight.
            Assert.AreEqual((Color)new Color32(20, 20, 20, 255), c);
        }

        [Test]
        public void A_shirt_only_outfit_shows_the_BAKED_overalls_it_actually_leaves_you_in()
        {
            var wardrobe = WardrobeWithRamps();
            Color c = WardrobeChip.ColourFor(wardrobe, Outfit("outfit.shirt_only", "", "navy"), Color.magenta);

            Assert.AreEqual((Color)new Color32(2, 2, 2, 255), c,
                            "no outfit key means the baked one stands — so that is what the chip shows");
        }

        [Test]
        public void A_key_the_ramps_do_not_hold_falls_back_without_throwing()
        {
            var wardrobe = WardrobeWithRamps();

            Assert.AreEqual(Color.magenta,
                            WardrobeChip.ColourFor(wardrobe, Outfit("outfit.x", "gone", ""), Color.magenta));
            Assert.AreEqual(Color.magenta, WardrobeChip.ColourFor(wardrobe, null, Color.magenta));
            Assert.AreEqual(Color.magenta,
                            WardrobeChip.ColourFor(null, Outfit("outfit.x", "oil", ""), Color.magenta));
        }
    }
}
