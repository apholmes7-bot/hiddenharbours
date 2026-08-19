using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>Which outfit the wardrobe's cursor is on, what a confirm would wear, and what a cancel must put
    /// back.</b> The whole picker rule, pure: no input device, no canvas, no Unity lifecycle and no
    /// ScriptableObject — so "cancelling restores exactly what was saved" is an EditMode assertion rather
    /// than a thing you hope about while looking at a fisher.
    ///
    /// <para>A deliberate near-copy of <c>DialogueOptionPicker</c>'s shape (same latch, same threshold,
    /// same wrap): the two are the same interaction — steer with the move axis, confirm with Interact —
    /// and a second spelling of "one row per push" would be a second thing to keep in step. What this one
    /// adds is the pair of ids either side of the choice, because a wardrobe can be walked away from and a
    /// dialogue row cannot be un-picked.</para>
    ///
    /// <para><b>The revert target is fixed at construction and never moves.</b> That is the property the
    /// whole feature turns on: the preview walks up and down the list as the cursor does, and the one id
    /// that must survive it is the one that was in the save when the doors opened. Making it readonly
    /// means no sequence of steps can lose it — there is no code path that could.</para>
    ///
    /// <para><b>Ids, not Defs.</b> The model takes a list of strings so it stays engine-free and so a test
    /// needs no assets; resolving an id to a label, a colour chip and a set of pixels is the presenting
    /// half's job (<see cref="WardrobePicker"/>), and Core's <c>CharacterWardrobeDef</c> already owns the
    /// resolving.</para>
    /// </summary>
    public sealed class WardrobePickerModel
    {
        /// <summary>How far the move axis must travel before it counts as a push, and how far back it must
        /// fall before the next one counts. Input plumbing, not owner feel — deliberately the same number
        /// as <c>DialogueOptionPicker.AxisThreshold</c>, because it is the same push on the same keys and
        /// two thresholds would mean the two pickers felt different for no reason anybody chose.</summary>
        public const float AxisThreshold = 0.5f;

        private readonly List<string> _ids;
        private bool _latched;

        /// <summary>
        /// Open a wardrobe holding <paramref name="outfitIds"/>, on a player currently wearing
        /// <paramref name="wornOutfitId"/> (empty = the baked look).
        ///
        /// <para>The cursor starts on what you are ALREADY wearing where the wardrobe holds it, so the
        /// first thing the list tells you is which one is yours — and stepping off it and back lands you
        /// exactly where you began. A worn id the wardrobe does not hold (a retired outfit still sitting
        /// in a save) starts the cursor at the top instead, and is still what a cancel restores.</para>
        /// </summary>
        public WardrobePickerModel(IReadOnlyList<string> outfitIds, string wornOutfitId)
        {
            RevertOutfitId = string.IsNullOrEmpty(wornOutfitId) ? string.Empty : wornOutfitId;

            _ids = new List<string>(outfitIds?.Count ?? 0);
            if (outfitIds != null)
                for (int i = 0; i < outfitIds.Count; i++)
                    _ids.Add(string.IsNullOrEmpty(outfitIds[i]) ? string.Empty : outfitIds[i]);

            Index = Mathf.Max(0, IndexOf(RevertOutfitId));
        }

        /// <summary>How many outfits are on offer.</summary>
        public int Count => _ids.Count;

        /// <summary>Which row the cursor is on. Always in range while <see cref="Count"/> &gt; 0; 0 on an
        /// empty wardrobe, where it selects nothing because there is nothing to select.</summary>
        public int Index { get; private set; }

        /// <summary>
        /// True when there is anything to choose between. False for a wardrobe with no outfits in it —
        /// which is a content state, not a fault, and the one the picker must decline to open on rather
        /// than showing an empty box with a cursor in it.
        /// </summary>
        public bool IsUsable => _ids.Count > 0;

        /// <summary>True while the axis is still pushed from the last step — the next step waits for it to
        /// come back to neutral. Exposed because it is the half of the latch a test can see.</summary>
        public bool IsLatched => _latched;

        /// <summary>
        /// The outfit id to be SHOWING right now — the preview, not the choice. Empty on an empty
        /// wardrobe.
        /// </summary>
        public string SelectedOutfitId =>
            _ids.Count == 0 ? string.Empty : _ids[Mathf.Clamp(Index, 0, _ids.Count - 1)];

        /// <summary>
        /// What was in the save when this picker opened, and therefore exactly what a cancel must put
        /// back. Never null; empty means the baked look. <b>Readonly on purpose</b> — see the type
        /// summary.
        /// </summary>
        public string RevertOutfitId { get; }

        /// <summary>The outfit id at a row, or empty when the row is out of range — what the drawing half
        /// walks to build its list.</summary>
        public string OutfitIdAt(int index) =>
            index < 0 || index >= _ids.Count ? string.Empty : _ids[index];

        /// <summary>The row an outfit id sits on, or -1. Ordinal — these are ids, not prose.</summary>
        public int IndexOf(string outfitId)
        {
            if (string.IsNullOrEmpty(outfitId)) return -1;
            for (int i = 0; i < _ids.Count; i++)
                if (string.Equals(_ids[i], outfitId, System.StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// Feed this frame's move axis (+1 = up the list, -1 = down). Returns true when the cursor actually
        /// moved — which is the caller's cue to re-publish the preview, and the reason this returns a bool
        /// rather than nothing: a held key must not re-skin the fisher sixty times a second (rule 7).
        ///
        /// <para>Wraps at both ends, as the dialogue picker does: with eight coats, running off the bottom
        /// onto the top is the Animal Crossing feel and costs nobody a wrong pick, because confirming is a
        /// separate press.</para>
        /// </summary>
        public bool Step(float axis)
        {
            if (_ids.Count <= 1) { _latched = Mathf.Abs(axis) >= AxisThreshold; return false; }

            if (Mathf.Abs(axis) < AxisThreshold) { _latched = false; return false; }
            if (_latched) return false;

            _latched = true;
            // Up the list means TOWARD row 0 — the first outfit is drawn at the top of the panel.
            Index = axis > 0f ? (Index - 1 + _ids.Count) % _ids.Count : (Index + 1) % _ids.Count;
            return true;
        }

        /// <summary>
        /// Put the cursor on a row directly — the MOUSE's move, where a pointer names a row outright
        /// instead of walking to it. Out-of-range is ignored rather than clamped: a caller asking for a row
        /// that is not there has a bug, and silently landing on the nearest one would hide it.
        ///
        /// <para>Deliberately does NOT touch the latch. The latch belongs to the axis — it is the state of
        /// a key that is still held — and a mouse has not pushed it. Clearing it here would let a key that
        /// was already down step again the moment the pointer moved.</para>
        /// </summary>
        public bool SelectIndex(int index)
        {
            if (index < 0 || index >= _ids.Count || index == Index) return false;
            Index = index;
            return true;
        }

        /// <summary>
        /// Confirm the row the cursor is on. Returns false (and an empty id) when there is nothing to
        /// confirm, so a caller can never be handed a choice out of an empty wardrobe.
        ///
        /// <para><b>It returns <see cref="SelectedOutfitId"/> and nothing else</b> — it takes no argument
        /// and reads no input, so there is no path by which a confirm can wear a coat the cursor was not
        /// on. Structural, rather than asserted.</para>
        /// </summary>
        public bool TryConfirm(out string outfitId)
        {
            if (_ids.Count == 0) { outfitId = string.Empty; return false; }
            outfitId = SelectedOutfitId;
            return true;
        }
    }

    /// <summary>
    /// <b>What colour to draw beside an outfit's name.</b> The wardrobe lists coats by their colour, so
    /// the chip is the row's real content and the label is the caption — which makes "which pixel do we
    /// show" a question with a right answer rather than a decoration.
    ///
    /// <para><b>The MIDDLE shade of the outfit's ramp.</b> Each ramp is the rig's own ~3 steps dark →
    /// light, drawn so cloth does not read as speckle at 32 px/m; the darkest step is the shadow side and
    /// the lightest is the highlight, so neither is what the garment "is". The middle is the body colour —
    /// the one the player will actually see most of on the fisher standing next to the panel.</para>
    ///
    /// <para><b>An outfit that names no outfit colour still gets a chip.</b> A shirt-only outfit is a
    /// complete, legal outfit (<c>CharacterOutfitDef</c>), and it leaves the overalls as the sheets were
    /// baked — so its chip is the wardrobe's <c>BakedOutfit</c>, which is exactly what the player would
    /// be wearing. Showing nothing there would be a blank square against a coat that has a colour.</para>
    ///
    /// <para><b>It never logs.</b> An unresolvable key returns the fallback and says nothing, because the
    /// same id is about to be refused loudly at the point it reaches a pixel (<c>PlayerOutfitVisual</c>
    /// logs the status and the reason). A second complaint from the swatch would be noise on the same
    /// fault.</para>
    /// </summary>
    public static class WardrobeChip
    {
        /// <summary>
        /// The chip for <paramref name="outfit"/> in <paramref name="wardrobe"/>, or
        /// <paramref name="fallback"/> when the ramps cannot answer.
        /// </summary>
        public static Color ColourFor(CharacterWardrobeDef wardrobe, CharacterOutfitDef outfit,
                                      Color fallback)
        {
            if (wardrobe == null || wardrobe.Ramps == null || outfit == null) return fallback;

            string key = string.IsNullOrEmpty(outfit.Outfit) ? wardrobe.BakedOutfit : outfit.Outfit;
            var ramp = wardrobe.Ramps.Ramp("outfit", key);
            if (ramp == null || !ramp.IsUsable) return fallback;

            return ramp.Shades[ramp.Shades.Length / 2];
        }
    }
}
