using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The character recolour as ARITHMETIC — no assets, no scene, no renderer. What is pinned here is
    /// the shape of the swap and, above all, the two things that would fail silently: the rig's
    /// rounding, and the refusal.
    /// </summary>
    public class CharacterPaletteTests
    {
        static readonly Color32 Key = new Color32(0x10, 0x1a, 0x19, 0xff);
        const float Amount = 0.22f;

        static CharacterRampsDef Ramps(params (string axis, string key, string[] hex)[] rows)
        {
            var byAxis = new Dictionary<string, List<CharacterRamp>>();
            var order = new List<string>();
            foreach (var (axis, key, hex) in rows)
            {
                if (!byAxis.TryGetValue(axis, out var list))
                {
                    byAxis[axis] = list = new List<CharacterRamp>();
                    order.Add(axis);
                }
                var shades = new Color32[hex.Length];
                for (int i = 0; i < hex.Length; i++) shades[i] = Hex(hex[i]);
                list.Add(new CharacterRamp { Id = $"ramp.{axis}.{key}", Key = key, Label = key, Shades = shades });
            }

            var def = ScriptableObject.CreateInstance<CharacterRampsDef>();
            var axes = new List<CharacterRampAxis>();
            foreach (var axis in order)
                axes.Add(new CharacterRampAxis { Axis = axis, DefaultKey = byAxis[axis][0].Key, Ramps = byAxis[axis].ToArray() });
            def.Axes = axes.ToArray();
            return def;
        }

        static Color32 Hex(string s)
        {
            s = s.TrimStart('#');
            return new Color32(
                (byte)System.Convert.ToInt32(s.Substring(0, 2), 16),
                (byte)System.Convert.ToInt32(s.Substring(2, 2), 16),
                (byte)System.Convert.ToInt32(s.Substring(4, 2), 16),
                255);
        }

        static string HexOf(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        // ---- the rounding, which is the one silent failure ------------------------------------

        /// <summary>
        /// The rig computes its tinted outline in JavaScript, whose <c>Math.round</c> breaks ties
        /// UPWARD. C#'s <see cref="Mathf.RoundToInt"/> breaks them to EVEN, and on this exact colour the
        /// two disagree: the green channel lands on 64.5, so half-up gives 0x41 and banker's gives 0x40.
        ///
        /// <para>That one byte is not cosmetic. It is the difference between an outline pixel matching
        /// the palette and not matching it — a swap that silently leaves every skin-edge pixel behind.
        /// <c>#414137</c> is a real colour in the shipped sheets (4,507 pixels of it), which is how the
        /// discrepancy was found in the first place, so it is pinned here by value.</para>
        /// </summary>
        [Test]
        public void KeyTint_RoundsHalfUp_LikeTheRigNotLikeCSharp()
        {
            var skinLightest = Hex("#f0c9a2");

            Assert.AreEqual("#414137", HexOf(CharacterPalette.KeyTint(Key, skinLightest, Amount)),
                "The tinted outline must be computed the way the rig computes it. Mathf.RoundToInt " +
                "would give #404137 here and every skin-edge pixel would stop matching the palette.");
        }

        [Test]
        public void KeyTint_ReproducesShippedOutlineColours()
        {
            // Measured out of the shipped sheets: each is a real colour in Fisher_idle.png.
            Assert.AreEqual("#163835", HexOf(CharacterPalette.KeyTint(Key, Hex("#2ba39a"), Amount)));
            Assert.AreEqual("#1d3d39", HexOf(CharacterPalette.KeyTint(Key, Hex("#49b8aa"), Amount)));
            Assert.AreEqual("#33301a", HexOf(CharacterPalette.KeyTint(Key, Hex("#b07d1f"), Amount)));
            Assert.AreEqual("#1a2325", HexOf(CharacterPalette.KeyTint(Key, Hex("#3d454e"), Amount)));
        }

        // ---- the swap itself --------------------------------------------------------------------

        [Test]
        public void TryBuild_EmitsAShadeAndItsOutline_ForEveryStep()
        {
            var ramps = Ramps(
                ("outfit", "teal", new[] { "#0d3f3c", "#14554e", "#1c7367", "#2ba39a", "#49b8aa" }),
                ("outfit", "rust", new[] { "#4a100e", "#7c1a15", "#a8241b", "#cf3626", "#e2573c" }));

            var swaps = new List<CharacterColourSwap>();
            var status = CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "teal", "rust") }, Key, Amount, swaps, out var why);

            Assert.AreEqual(CharacterPaletteStatus.Ok, status, why);
            Assert.AreEqual(10, swaps.Count, "Five shades, each with its tinted outline.");

            // The shade pairs are positional, and the outline pairs are the tint of the same pair.
            Assert.AreEqual("#0d3f3c", HexOf(swaps[0].From));
            Assert.AreEqual("#4a100e", HexOf(swaps[0].To));
            Assert.AreEqual(HexOf(CharacterPalette.KeyTint(Key, Hex("#0d3f3c"), Amount)), HexOf(swaps[1].From));
            Assert.AreEqual(HexOf(CharacterPalette.KeyTint(Key, Hex("#4a100e"), Amount)), HexOf(swaps[1].To));
        }

        [Test]
        public void TryBuild_WearingWhatIsAlreadyOn_IsNoSwapsAtAll()
        {
            var ramps = Ramps(("outfit", "teal", new[] { "#0d3f3c", "#2ba39a" }));

            var swaps = new List<CharacterColourSwap>();
            var status = CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "teal", "teal") }, Key, Amount, swaps, out var why);

            Assert.AreEqual(CharacterPaletteStatus.Ok, status, why);
            Assert.AreEqual(0, swaps.Count,
                "The baked outfit must cost nothing to wear — an empty palette is the shader's " +
                "pixel-identical passthrough, and that is what keeps this feature free when unused.");
        }

        [Test]
        public void TryBuild_DropsIdentityPairs_WhereTwoRampsShareAShade()
        {
            // The kit really does this: hair 'blond' and outfit 'oil' share four of five entries.
            var ramps = Ramps(
                ("outfit", "a", new[] { "#111111", "#222222" }),
                ("outfit", "b", new[] { "#111111", "#333333" }));

            var swaps = new List<CharacterColourSwap>();
            CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "a", "b") }, Key, Amount, swaps, out _);

            foreach (var s in swaps)
                Assert.IsFalse(s.IsIdentity, "A pair that changes nothing is never uploaded.");
            Assert.AreEqual(2, swaps.Count, "Only the shade that actually moves, and its outline.");
        }

        // ---- the refusals: the sabotage arm ------------------------------------------------------

        [Test]
        public void TryBuild_RefusesAnUnknownKey_AndAppliesNothingAtAll()
        {
            var ramps = Ramps(("outfit", "teal", new[] { "#0d3f3c", "#2ba39a" }));

            var swaps = new List<CharacterColourSwap>();
            var status = CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "teal", "retired_colour") },
                Key, Amount, swaps, out var why);

            Assert.AreEqual(CharacterPaletteStatus.UnknownKey, status);
            Assert.AreEqual(0, swaps.Count,
                "A preset naming a ramp that no longer exists must leave the fisher in the outfit she " +
                "was baked in — never in half of one.");
            StringAssert.Contains("retired_colour", why, "The refusal has to name what it could not find.");
        }

        [Test]
        public void TryBuild_RefusesAnUnknownAxis()
        {
            var ramps = Ramps(("outfit", "teal", new[] { "#0d3f3c" }));
            var swaps = new List<CharacterColourSwap>();

            Assert.AreEqual(CharacterPaletteStatus.UnknownAxis, CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("hatCol", "char", "rust") }, Key, Amount, swaps, out _));
            Assert.AreEqual(0, swaps.Count);
        }

        [Test]
        public void TryBuild_RefusesRampsOfDifferentLength()
        {
            var ramps = Ramps(
                ("outfit", "short", new[] { "#111111", "#222222" }),
                ("outfit", "long", new[] { "#111111", "#222222", "#333333" }));

            var swaps = new List<CharacterColourSwap>();
            var status = CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "short", "long") }, Key, Amount, swaps, out _);

            Assert.AreEqual(CharacterPaletteStatus.ArityMismatch, status,
                "Shades are matched BY INDEX, so ramps of different lengths do not correspond and a " +
                "positional swap would move the wrong pixels.");
            Assert.AreEqual(0, swaps.Count);
        }

        [Test]
        public void TryBuild_RefusesMoreSwapsThanARendererCanCarry()
        {
            // One axis whose ramps are long enough that shades + outlines overrun the ceiling.
            var many = new string[CharacterPalette.MaxSwaps];
            var other = new string[CharacterPalette.MaxSwaps];
            for (int i = 0; i < many.Length; i++)
            {
                many[i] = $"#{i + 1:x2}0000";
                other[i] = $"#{i + 1:x2}00ff";
            }
            var ramps = Ramps(("outfit", "a", many), ("outfit", "b", other));

            var swaps = new List<CharacterColourSwap>();
            var status = CharacterPalette.TryBuild(
                ramps, new[] { new CharacterAxisChange("outfit", "a", "b") }, Key, Amount, swaps, out _);

            Assert.AreEqual(CharacterPaletteStatus.Overflow, status,
                "Past the ceiling the build refuses. A truncated palette would recolour some of the " +
                "garment and not the rest, which is worse than not recolouring it.");
            Assert.AreEqual(0, swaps.Count);
        }

        [Test]
        public void TryBuild_WithNoRamps_RefusesRatherThanThrowing()
        {
            var swaps = new List<CharacterColourSwap>();
            Assert.AreEqual(CharacterPaletteStatus.NoRamps, CharacterPalette.TryBuild(
                null, new[] { new CharacterAxisChange("outfit", "teal", "rust") }, Key, Amount, swaps, out _));
            Assert.AreEqual(0, swaps.Count);
        }
    }
}
