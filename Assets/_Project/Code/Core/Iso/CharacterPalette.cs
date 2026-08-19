using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One source colour in the baked sheets and the colour that replaces it.</summary>
    public readonly struct CharacterColourSwap
    {
        /// <summary>The colour as it sits in the shipped PNG.</summary>
        public readonly Color32 From;

        /// <summary>What the fisher should wear instead.</summary>
        public readonly Color32 To;

        public CharacterColourSwap(Color32 from, Color32 to) { From = from; To = to; }

        /// <summary>True when this pair would not change a pixel — worth skipping, never worth sending.</summary>
        public bool IsIdentity => From.r == To.r && From.g == To.g && From.b == To.b;
    }

    /// <summary>Why a palette build refused. Anything but <see cref="Ok"/> means NOTHING was applied.</summary>
    public enum CharacterPaletteStatus
    {
        /// <summary>Built; the swap list is complete and safe to send to a renderer.</summary>
        Ok,
        /// <summary>No <see cref="CharacterRampsDef"/> was supplied, so no colour can be resolved.</summary>
        NoRamps,
        /// <summary>An axis name that <see cref="CharacterRampsDef"/> does not carry.</summary>
        UnknownAxis,
        /// <summary>An axis exists but the ramp key does not — a retired or mistyped colour.</summary>
        UnknownKey,
        /// <summary>Two ramps on one axis have different lengths, so their shades do not correspond.</summary>
        ArityMismatch,
        /// <summary>More swaps than <see cref="CharacterPalette.MaxSwaps"/>; a renderer could not carry them.</summary>
        Overflow,
    }

    /// <summary>One axis moving from the colour the sheets were baked in to the colour to draw instead.</summary>
    public readonly struct CharacterAxisChange
    {
        /// <summary>The ramps' axis name — "outfit", "shirt".</summary>
        public readonly string Axis;

        /// <summary>The key the SHEETS were baked in. What the swap replaces.</summary>
        public readonly string FromKey;

        /// <summary>The key to draw instead.</summary>
        public readonly string ToKey;

        public CharacterAxisChange(string axis, string fromKey, string toKey)
        {
            Axis = axis; FromKey = fromKey; ToKey = toKey;
        }
    }

    /// <summary>
    /// <b>The character recolour, as arithmetic</b> — turn "wear the rust overalls instead of the teal
    /// ones" into the exact list of source-to-target colour pairs a renderer applies to the baked
    /// sheets. Pure and engine-light: no textures, no materials, no <c>MonoBehaviour</c>, so the whole
    /// recolour is decided and tested without a scene.
    ///
    /// <para><b>Why a colour LIST and not a re-render.</b> ADR 0021 keeps the rig's JavaScript out of
    /// shipped builds, so nothing at runtime can draw a person; ADR 0029 answers that by splitting the
    /// character's parameter space — structure is baked, colour is a ramp swap. This class is the
    /// second half of that ruling, which had been decided and shipped as data
    /// (<see cref="CharacterRampsDef"/>) but never actually built: until now no runtime code read a
    /// ramp at all.</para>
    ///
    /// <para><b>Why the swap can be EXACT, and how that was established.</b> Every one of the
    /// 1,034,817 opaque pixels across the 37 baked <c>Fisher_*</c> sheets is one of just 69 colours,
    /// and every one of those 69 is either a literal entry of a rig ramp or that entry run through the
    /// rig's tinted-outline pass. Alpha is strictly 0 or 255 — the sheets carry no anti-aliasing and no
    /// shading off the palette. So a recolour is not an approximation of the art: it is the same
    /// arithmetic the rig did, redone against a different ramp. <c>CharacterPaletteContentTests</c>
    /// re-runs that classification over the shipped PNGs, so a drop that shades off-palette fails
    /// loudly instead of leaving three quarters of a fisher un-recoloured.</para>
    ///
    /// <para><b>The outline is not decoration, it is a quarter of the pixels.</b> The rig draws
    /// silhouette and depth edges as <c>keyTint(c) = mix(KEY, c, 0.22)</c> — dark, but carrying the
    /// local hue — which is 27% of the figure. Swapping only the ramp entries would recolour a garment
    /// and leave its outline the old colour, so every pair below is emitted twice: once for the shade,
    /// once for the shade's tint. <see cref="KeyTint"/> reproduces the rig's own rounding, because
    /// JavaScript's <c>Math.round</c> breaks ties UPWARD and C#'s <c>Mathf.RoundToInt</c> breaks them
    /// to even — the fisher's <c>#414137</c> is the pixel that tells the two apart.</para>
    ///
    /// <para><b>It refuses rather than half-applies.</b> A preset naming a ramp that no longer exists
    /// returns a status and an EMPTY list, so the fisher keeps the look she was baked in. A partially
    /// applied palette — teal sleeves over rust overalls — is the failure that would ship unnoticed,
    /// so it is the one thing this class cannot produce.</para>
    /// </summary>
    public static class CharacterPalette
    {
        /// <summary>
        /// The most swaps a renderer will carry, and therefore the most this will build. Twenty-two are
        /// needed for a clothes-only wardrobe (outfit's 5 shades and shirt's 6, each with its tint);
        /// the slack covers one more axis arriving without a shader change. It is a hard ceiling, not a
        /// budget: past it the build REFUSES, because a silently truncated palette is a half-recoloured
        /// fisher.
        /// </summary>
        public const int MaxSwaps = 24;

        /// <summary>
        /// The rig's silhouette colour — <c>KEY</c> in <c>characterIsoRig6.js</c>. The fallback for a
        /// <see cref="CharacterRampsDef"/> generated before the field existed; the def's own value
        /// wins. <c>CharacterPaletteContentTests</c> asserts this against the rig source, so the
        /// constant cannot drift away from the art.
        /// </summary>
        public static readonly Color32 DefaultKeyTintColour = new Color32(0x10, 0x1a, 0x19, 0xff);

        /// <summary>How much of the local colour the tinted outline keeps — the rig's <c>0.22</c>.</summary>
        public const float DefaultKeyTintAmount = 0.22f;

        /// <summary>
        /// The rig's tinted outline: <c>mix(KEY, shade, amount)</c>, rounded the way the rig rounds
        /// (half UP, not to even — see the class remarks).
        /// </summary>
        public static Color32 KeyTint(Color32 key, Color32 shade, float amount)
        {
            return new Color32(
                Channel(key.r, shade.r, amount),
                Channel(key.g, shade.g, amount),
                Channel(key.b, shade.b, amount),
                255);

            static byte Channel(byte a, byte b, float t)
            {
                float v = a + (b - a) * t;
                int r = Mathf.FloorToInt(v + 0.5f);
                return (byte)(r < 0 ? 0 : r > 255 ? 255 : r);
            }
        }

        /// <summary>
        /// Build the swap list for a set of axis changes. On anything but
        /// <see cref="CharacterPaletteStatus.Ok"/>, <paramref name="into"/> is left EMPTY and
        /// <paramref name="reason"/> carries prose a log can print.
        /// </summary>
        /// <param name="ramps">The colour space the keys resolve in.</param>
        /// <param name="changes">Per axis: the key the sheets were BAKED in, and the key to draw.</param>
        /// <param name="keyTintColour">The rig's silhouette colour.</param>
        /// <param name="keyTintAmount">How much local colour the outline keeps.</param>
        /// <param name="into">Cleared, then filled. Identity pairs are dropped, never sent.</param>
        /// <param name="reason">Empty on success; player-neutral prose for a log otherwise.</param>
        public static CharacterPaletteStatus TryBuild(
            CharacterRampsDef ramps,
            IReadOnlyList<CharacterAxisChange> changes,
            Color32 keyTintColour,
            float keyTintAmount,
            List<CharacterColourSwap> into,
            out string reason)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            reason = "";

            if (ramps == null)
            {
                reason = "no CharacterRampsDef — the character's colour space is missing";
                return CharacterPaletteStatus.NoRamps;
            }
            if (changes == null || changes.Count == 0) return CharacterPaletteStatus.Ok;

            // Built into a scratch list so a refusal half way through leaves the caller's list empty
            // rather than holding whichever axes happened to resolve first.
            var built = new List<CharacterColourSwap>(MaxSwaps);

            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (string.IsNullOrEmpty(change.Axis) || string.IsNullOrEmpty(change.ToKey)) continue;
                if (string.Equals(change.FromKey, change.ToKey, StringComparison.Ordinal)) continue;

                if (ramps.Axis(change.Axis) == null)
                {
                    reason = $"axis '{change.Axis}' is not in {ramps.name} (it carries: {AxisNames(ramps)})";
                    return CharacterPaletteStatus.UnknownAxis;
                }

                var from = ramps.Ramp(change.Axis, change.FromKey);
                var to = ramps.Ramp(change.Axis, change.ToKey);

                if (from == null || !from.IsUsable)
                {
                    reason = $"the baked key '{change.FromKey}' is not a usable ramp on axis " +
                             $"'{change.Axis}' — the sheets were baked in a colour the ramps no " +
                             "longer carry, so nothing can be swapped out";
                    return CharacterPaletteStatus.UnknownKey;
                }
                if (to == null || !to.IsUsable)
                {
                    reason = $"'{change.ToKey}' is not a usable ramp on axis '{change.Axis}' — a " +
                             "retired or mistyped colour, so nothing is applied and the baked look stands";
                    return CharacterPaletteStatus.UnknownKey;
                }
                if (from.Shades.Length != to.Shades.Length)
                {
                    reason = $"axis '{change.Axis}': '{change.FromKey}' has {from.Shades.Length} " +
                             $"shades but '{change.ToKey}' has {to.Shades.Length} — the shades do not " +
                             "correspond, so a positional swap would move the wrong pixels";
                    return CharacterPaletteStatus.ArityMismatch;
                }

                for (int s = 0; s < from.Shades.Length; s++)
                {
                    Color32 a = from.Shades[s];
                    Color32 b = to.Shades[s];
                    Add(built, new CharacterColourSwap(a, b));
                    Add(built, new CharacterColourSwap(
                        KeyTint(keyTintColour, a, keyTintAmount),
                        KeyTint(keyTintColour, b, keyTintAmount)));
                }

                if (built.Count > MaxSwaps)
                {
                    reason = $"{built.Count} swaps needed but a renderer carries {MaxSwaps} — " +
                             "refusing rather than truncating to a half-recoloured fisher";
                    return CharacterPaletteStatus.Overflow;
                }
            }

            into.AddRange(built);
            return CharacterPaletteStatus.Ok;

            // FIRST WRITER WINS on a repeated source colour. Two ramps can legitimately share a shade
            // (the kit's hair 'blond' and outfit 'oil' share four of five), and a source colour maps to
            // exactly one target or the swap is not a function. The content test asserts that no two
            // LIVE axes collide on the baked build, which is what keeps this rule from ever mattering
            // in practice — it is here so that if one day it does, the result is stable rather than
            // dependent on axis order.
            static void Add(List<CharacterColourSwap> list, CharacterColourSwap swap)
            {
                if (swap.IsIdentity) return;
                for (int i = 0; i < list.Count; i++)
                    if (Same(list[i].From, swap.From)) return;
                list.Add(swap);
            }

            static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
        }

        /// <summary>The axis names a def carries, for a refusal message.</summary>
        private static string AxisNames(CharacterRampsDef ramps)
        {
            if (ramps.Axes == null || ramps.Axes.Length == 0) return "none";
            var names = new string[ramps.Axes.Length];
            for (int i = 0; i < ramps.Axes.Length; i++) names[i] = ramps.Axes[i]?.Axis ?? "?";
            return string.Join(", ", names);
        }
    }
}
