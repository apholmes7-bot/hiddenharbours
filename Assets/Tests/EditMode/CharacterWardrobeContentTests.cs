using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the WARDROBE, and — more importantly — the standing proof that the
    /// mechanism behind it is still legal against the shipped art.
    ///
    /// <para><b>Why an art measurement lives in a content test.</b> The recolour is a colour-keyed
    /// substitution, which is only exact because the character sheets are perfectly palette-disciplined:
    /// every opaque pixel is a literal entry of one of the rig's ramps or that entry run through the
    /// rig's tinted-outline pass, and alpha is strictly 0 or 255. That is a measured property of the
    /// PNGs, not a promise anyone made — so if a future drop shades off-palette, anti-aliases an edge,
    /// or bakes the fisher in a different outfit, the wardrobe would quietly stop recolouring some
    /// fraction of the figure and nothing else in the project would notice. These tests are what
    /// notices. They re-run the classification that established the mechanism in the first place, over
    /// the real sheets, every time CI runs.</para>
    ///
    /// <para><b>Everything is LIFTED, never transcribed.</b> The ramps come from the shipped
    /// <see cref="CharacterRampsDef"/>, the fixed materials and the outline mix from the rig's own
    /// source, and the baked keys from the rig kit's <c>presets.json</c>. So a drop that changes any of
    /// them changes what these tests expect, rather than leaving a stale constant agreeing with
    /// itself.</para>
    /// </summary>
    public class CharacterWardrobeContentTests
    {
        const string WardrobePath = "Assets/_Project/Resources/CharacterWardrobe.asset";
        const string OutfitsFolder = "Assets/_Project/Data/Characters/Outfits";
        const string SheetsFolder = "Assets/_Project/Art/Characters/Iso";
        const string RigRelative = "docs/art/rigs/characterIsoRig6.js";
        const string PresetsRelative = "docs/art/rigs/character/presets.json";

        static CharacterWardrobeDef Wardrobe() =>
            AssetDatabase.LoadAssetAtPath<CharacterWardrobeDef>(WardrobePath);

        static CharacterOutfitDef[] OutfitAssets() =>
            AssetDatabase.FindAssets("t:CharacterOutfitDef", new[] { OutfitsFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<CharacterOutfitDef>)
                         .Where(o => o != null)
                         .ToArray();

        static string RepoFile(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        static string RigSource() => File.ReadAllText(RepoFile(RigRelative));

        // ================================================================================
        //  The wardrobe is well-formed data
        // ================================================================================

        [Test]
        public void Wardrobe_Exists_AndCarriesItsRamps()
        {
            var w = Wardrobe();
            Assert.IsNotNull(w, $"No CharacterWardrobeDef at {WardrobePath}. It must live under " +
                                "Resources so PlayerOutfitVisual can load it with no scene wiring.");
            Assert.IsNotNull(w.Ramps, "The wardrobe must name the ramps its keys resolve in.");
            Assert.Greater(w.Count, 0, "A wardrobe with nothing in it is a fixture, not a feature.");
        }

        [Test]
        public void EveryOutfit_HasAStableId_AndColoursThatResolve()
        {
            var w = Wardrobe();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var o in OutfitAssets())
            {
                Assert.IsTrue(Regex.IsMatch(o.Id ?? "", @"^outfit\.[a-z0-9_]+$"),
                    $"'{o.name}' has id '{o.Id}'. Ids are type.snake_case and append-only (CLAUDE.md §5).");
                Assert.IsTrue(seen.Add(o.Id), $"Duplicate outfit id '{o.Id}'.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(o.Label), $"'{o.Id}' has no label to show the player.");
                Assert.IsTrue(o.HasAnyChoice, $"'{o.Id}' changes nothing, so it is not an outfit.");

                foreach (var (axis, key) in o.ColourChoices())
                {
                    Assert.IsNotNull(w.Ramps.Ramp(axis, key),
                        $"'{o.Id}' asks for {axis} '{key}', which {w.Ramps.name} does not carry. " +
                        "A preset naming a missing ramp is exactly what the runtime refuses — " +
                        "catch it here instead.");
                }
            }
        }

        [Test]
        public void TheWardrobe_HoldsEveryOutfitOnDisk_AndNoneTwice()
        {
            var w = Wardrobe();
            var onDisk = OutfitAssets().Select(o => o.Id).OrderBy(s => s, System.StringComparer.Ordinal);
            var listed = w.Outfits.Where(o => o != null).Select(o => o.Id).ToArray();

            CollectionAssert.AreEqual(onDisk.ToArray(), listed.OrderBy(s => s, System.StringComparer.Ordinal).ToArray(),
                "An outfit asset that is not in the wardrobe's list is invisible to the player, and a " +
                "wardrobe entry with no asset is a null the picker would have to guard.");
            CollectionAssert.AllItemsAreUnique(listed);
        }

        /// <summary>
        /// A new game must look EXACTLY like the shipped art. The default outfit therefore has to name
        /// the colours the sheets were baked in — if it named anything else, every new player would be
        /// silently recoloured on their first frame, and the "no palette is a pixel-identical
        /// passthrough" guarantee would never actually be exercised in play.
        /// </summary>
        [Test]
        public void TheDefaultOutfit_IsTheBakedLook()
        {
            var w = Wardrobe();
            var def = w.Outfit(w.DefaultOutfitId);

            Assert.IsNotNull(def, $"DefaultOutfitId '{w.DefaultOutfitId}' is not in the wardrobe.");
            Assert.AreEqual(w.BakedOutfit, def.Outfit, "The default outfit must be the baked outfit colour.");
            Assert.AreEqual(w.BakedShirt, def.Shirt, "The default outfit must be the baked shirt colour.");

            var swaps = new List<CharacterColourSwap>();
            Assert.AreEqual(CharacterPaletteStatus.Ok, w.TryBuildPalette(w.DefaultOutfitId, swaps, out var why), why);
            Assert.AreEqual(0, swaps.Count, "Wearing the default must cost zero swaps.");
        }

        // ================================================================================
        //  The wardrobe agrees with the ART it is recolouring
        // ================================================================================

        /// <summary>
        /// The outline mix is a fact of the rig, and the swap is wrong the moment the two disagree —
        /// silently, because a mismatched outline colour simply fails to match and stays as it was.
        /// </summary>
        [Test]
        public void TheOutlineFacts_MatchTheRigSource()
        {
            var w = Wardrobe();
            string src = RigSource();

            var key = Regex.Match(src, @"const\s+KEY\s*=\s*'(#[0-9a-fA-F]{6})'");
            Assert.IsTrue(key.Success, $"Could not find `const KEY` in {RigRelative}.");
            Assert.AreEqual(key.Groups[1].Value.ToLowerInvariant(), HexOf(w.KeyTintColour),
                "The wardrobe's KeyTintColour must be the rig's KEY.");

            var mix = Regex.Match(src, @"_mix\(KEY,\s*c,\s*([0-9.]+)\)");
            Assert.IsTrue(mix.Success, $"Could not find the keyTint mix in {RigRelative}.");
            Assert.AreEqual(float.Parse(mix.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                w.KeyTintAmount, 1e-6f,
                "The wardrobe's KeyTintAmount must be the rig's own outline mix.");
        }

        /// <summary>
        /// The source end of every swap is what the sheets were BAKED in. If a drop re-bakes the fisher
        /// in a different outfit, every swap in the game stops matching a pixel and the wardrobe
        /// silently does nothing at all — the single most invisible way this feature can die.
        /// </summary>
        [Test]
        public void TheBakedKeys_MatchTheRigsFisherPreset()
        {
            var w = Wardrobe();
            var fisher = FisherPreset();

            Assert.AreEqual(fisher["outfit"], w.BakedOutfit,
                $"{PresetsRelative} bakes the fisher in outfit '{fisher["outfit"]}'.");
            Assert.AreEqual(fisher["shirt"], w.BakedShirt,
                $"{PresetsRelative} bakes the fisher in shirt '{fisher["shirt"]}'.");
        }

        // ================================================================================
        //  The measurement the whole mechanism rests on
        // ================================================================================

        /// <summary>
        /// <b>The load-bearing test.</b> Every opaque pixel of every baked Fisher sheet must be a colour
        /// the rig's palette can account for — a ramp entry, or that entry's tinted outline. When this
        /// holds, a colour-keyed substitution is exact; the moment it stops holding, the wardrobe
        /// recolours some of the fisher and leaves the rest, which is worse than doing nothing.
        ///
        /// <para>Alpha is checked in the same pass: a soft edge would blend two palette colours into
        /// one that is on neither, so anti-aliasing arriving in a drop breaks the mechanism just as
        /// surely as an off-palette shade would.</para>
        /// </summary>
        [Test]
        public void EveryPixelOfEveryFisherSheet_IsARigPaletteColour()
        {
            var palette = BakedPalette();
            var sheets = Directory.GetFiles(SheetsFolder, "Fisher_*.png").OrderBy(p => p).ToArray();
            Assert.Greater(sheets.Length, 0, $"No Fisher_*.png under {SheetsFolder}.");

            var strays = new Dictionary<int, int>();
            var strayWhere = new Dictionary<int, string>();
            long opaque = 0;
            var softAlpha = new HashSet<byte>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                foreach (var path in sheets)
                {
                    Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)), $"Could not decode {path}");
                    foreach (var px in tex.GetPixels32())
                    {
                        if (px.a != 0 && px.a != 255) softAlpha.Add(px.a);
                        if (px.a == 0) continue;

                        opaque++;
                        int packed = (px.r << 16) | (px.g << 8) | px.b;
                        if (palette.ContainsKey(packed)) continue;

                        strays.TryGetValue(packed, out int n);
                        strays[packed] = n + 1;
                        if (!strayWhere.ContainsKey(packed)) strayWhere[packed] = Path.GetFileName(path);
                    }
                }
            }
            finally { Object.DestroyImmediate(tex); }

            CollectionAssert.IsEmpty(softAlpha,
                "The character sheets must be hard-edged (alpha 0 or 255). A blended edge is a colour " +
                "that is on no ramp, so the wardrobe could not recolour it — and it would show as a " +
                "halo of the OLD outfit around the new one.");

            if (strays.Count > 0)
            {
                string worst = string.Join("\n  ", strays.OrderByDescending(kv => kv.Value).Take(10)
                    .Select(kv => $"#{kv.Key:x6}  {kv.Value,8:n0} px  (first in {strayWhere[kv.Key]})"));
                Assert.Fail(
                    $"{strays.Count} colour(s) in the baked Fisher sheets are on no rig ramp and are not " +
                    $"a ramp entry's tinted outline, covering {strays.Values.Sum():n0} of {opaque:n0} " +
                    "opaque pixels.\n\n" +
                    "The wardrobe's recolour is a colour-keyed substitution, so any pixel this test " +
                    "cannot account for is a pixel the wardrobe will silently leave in the old outfit. " +
                    "Either the rig gained a material this test does not know about (teach it, from the " +
                    "rig source — never by pasting hexes), or a drop started shading off-palette, in " +
                    "which case the mechanism itself needs revisiting.\n\n  " + worst);
            }

            Assert.Greater(opaque, 0, "No opaque pixels were read at all — the sheets did not decode.");
        }

        /// <summary>
        /// The two axes the wardrobe sells must actually be ON the player, and the four the audit found
        /// empty must still be empty. This is the ruling that scoped the feature (owner, 2026-08-18:
        /// clothes only), written down as an assertion: if a drop bakes the fisher in a hat, hatCol
        /// becomes a real axis and this test says so.
        /// </summary>
        [Test]
        public void TheLiveAxesAreOutfitAndShirt_AndTheOthersDrawNothing()
        {
            var w = Wardrobe();
            var present = ColoursInSheets();
            var fisher = FisherPreset();

            foreach (string axis in new[] { "outfit", "shirt" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                Assert.IsNotNull(ramp, $"The ramps do not carry the baked {axis} '{fisher[axis]}'.");

                int hits = ramp.Shades.Count(s => present.Contains(Pack(s)));
                Assert.Greater(hits, 0,
                    $"Not one shade of the baked {axis} ('{fisher[axis]}') appears in the sheets. The " +
                    "wardrobe would sell a colour that changes nothing on screen.");
            }

            foreach (string axis in new[] { "hatCol", "apronCol" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                if (ramp == null) continue;

                // These are only meaningful if the colour is not ALSO some live axis's colour.
                var exclusive = ramp.Shades.Where(s => !IsLiveAxisColour(w, fisher, s)).ToArray();
                int hits = exclusive.Count(s => present.Contains(Pack(s)));
                Assert.AreEqual(0, hits,
                    $"The fisher's sheets now contain {axis} pixels ('{fisher[axis]}'). The wardrobe was " +
                    "scoped to outfit + shirt because the baked fisher wears no hat and no apron — if " +
                    "that has changed, the ruling is worth revisiting rather than quietly ignoring.");
            }
        }

        /// <summary>
        /// A colour-keyed swap cannot separate two axes that share a source colour: recolour one and the
        /// other follows. The fisher's baked palette has exactly one such pair — the eye colour
        /// <c>sea</c> is byte-identical to the outfit's teal shade 3 — and it is harmless only because
        /// the head rig draws no iris in that colour at 32 px/m. This test is what keeps that "only
        /// because" true for the axes the wardrobe actually sells.
        /// </summary>
        [Test]
        public void NoTwoAxesTheWardrobeSells_ShareASourceColour()
        {
            var w = Wardrobe();
            var fisher = FisherPreset();
            var owner = new Dictionary<int, string>();

            foreach (string axis in new[] { "outfit", "shirt" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                for (int i = 0; i < ramp.Shades.Length; i++)
                {
                    foreach (int packed in new[]
                    {
                        Pack(ramp.Shades[i]),
                        Pack(CharacterPalette.KeyTint(w.KeyTintColour, ramp.Shades[i], w.KeyTintAmount)),
                    })
                    {
                        if (owner.TryGetValue(packed, out string other) && other != axis)
                            Assert.Fail($"#{packed:x6} is both a '{other}' colour and a '{axis}' colour on " +
                                        "the baked fisher. A colour-keyed swap cannot tell them apart, so " +
                                        "changing one would change the other.");
                        owner[packed] = axis;
                    }
                }
            }
        }

        /// <summary>
        /// The shader matches in LINEAR space with a tolerance, so the tolerance has to be smaller than
        /// the gap between a colour the wardrobe owns and its nearest neighbour — otherwise a swap would
        /// capture a pixel that belongs to the boots or the skin. The number in the shader is derived
        /// from this gap; this test re-derives the gap from the shipped art and fails if it closes.
        /// </summary>
        [Test]
        public void TheMatchTolerance_CannotCaptureANeighbouringColour()
        {
            var w = Wardrobe();
            var fisher = FisherPreset();
            var everything = BakedPalette().Keys.Select(Unpack).ToArray();

            var owned = new List<Color32>();
            foreach (string axis in new[] { "outfit", "shirt" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                foreach (var s in ramp.Shades)
                {
                    owned.Add(s);
                    owned.Add(CharacterPalette.KeyTint(w.KeyTintColour, s, w.KeyTintAmount));
                }
            }

            float closestSq = float.MaxValue;
            string pair = "";
            foreach (var mine in owned)
                foreach (var other in everything)
                {
                    if (Pack(mine) == Pack(other)) continue;
                    float d = LinearDistanceSquared(mine, other);
                    if (d < closestSq) { closestSq = d; pair = $"{HexOf(mine)} vs {HexOf(other)}"; }
                }

            // The shipped tolerance. Kept here rather than read off the material so the test states the
            // contract the shader has to honour, not whatever the shader currently says.
            const float ToleranceSq = 3.1e-06f;

            Assert.Less(ToleranceSq, closestSq * 0.5f,
                $"The closest confusable pair is now {pair} at a squared linear distance of {closestSq:E3}, " +
                $"which leaves no room under the shipped tolerance of {ToleranceSq:E3}. Widening the " +
                "wardrobe's axes can close this gap — re-derive the tolerance (and the one in " +
                "PlayerOutfitVisual and CharacterPalette.hlsl) before shipping the new axis.");
        }

        // ================================================================================
        //  Helpers — everything lifted from the rig, nothing transcribed
        // ================================================================================

        static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        static Color32 Unpack(int p) =>
            new Color32((byte)((p >> 16) & 255), (byte)((p >> 8) & 255), (byte)(p & 255), 255);

        static string HexOf(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        static float LinearDistanceSquared(Color32 a, Color32 b)
        {
            Color la = ((Color)a).linear, lb = ((Color)b).linear;
            float dr = la.r - lb.r, dg = la.g - lb.g, db = la.b - lb.b;
            return dr * dr + dg * dg + db * db;
        }

        static bool IsLiveAxisColour(CharacterWardrobeDef w, Dictionary<string, string> fisher, Color32 c)
        {
            foreach (string axis in new[] { "outfit", "shirt", "skin", "hair" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                if (ramp == null) continue;
                foreach (var s in ramp.Shades) if (Pack(s) == Pack(c)) return true;
            }
            return false;
        }

        /// <summary>The fisher's own build, read from the rig kit rather than restated here.</summary>
        static Dictionary<string, string> FisherPreset()
        {
            string json = File.ReadAllText(RepoFile(PresetsRelative));
            var block = Regex.Match(json, "\"fisher\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, $"No 'fisher' preset in {PresetsRelative}.");

            var map = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(block.Groups[1].Value, "\"([A-Za-z]+)\"\\s*:\\s*\"([^\"]*)\""))
                map[m.Groups[1].Value] = m.Groups[2].Value;
            return map;
        }

        /// <summary>A fixed ramp declared in the rig source, e.g. <c>const BOOT = ['#101317', ...]</c>.</summary>
        static Color32[] RigConstRamp(string src, string name)
        {
            var m = Regex.Match(src, @"const\s+" + name + @"\s*=\s*\[([^\]]*)\]");
            Assert.IsTrue(m.Success, $"Could not find `const {name}` in {RigRelative}.");
            return Regex.Matches(m.Groups[1].Value, "#[0-9a-fA-F]{6}")
                        .Cast<Match>()
                        .Select(x => HexTo32(x.Value))
                        .ToArray();
        }

        static Color32 HexTo32(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color32(
                (byte)System.Convert.ToInt32(hex.Substring(0, 2), 16),
                (byte)System.Convert.ToInt32(hex.Substring(2, 2), 16),
                (byte)System.Convert.ToInt32(hex.Substring(4, 2), 16),
                255);
        }

        /// <summary>Every distinct colour actually present in the shipped sheets.</summary>
        static HashSet<int> ColoursInSheets()
        {
            var present = new HashSet<int>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (var path in Directory.GetFiles(SheetsFolder, "Fisher_*.png"))
                {
                    if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) continue;
                    foreach (var px in tex.GetPixels32())
                        if (px.a != 0) present.Add(Pack(px));
                }
            }
            finally { Object.DestroyImmediate(tex); }
            return present;
        }

        /// <summary>
        /// Every colour the rig could have written into the fisher's sheets, labelled by the material
        /// that owns it: the four ramp axes at her baked keys, her eye colour, the derived brow, the
        /// fixed materials (boots, leather, brass, ink, key) — and the tinted outline of every one of
        /// them.
        /// </summary>
        static Dictionary<int, string> BakedPalette()
        {
            var w = Wardrobe();
            var fisher = FisherPreset();
            string src = RigSource();

            Color32 key = w.KeyTintColour;
            float amount = w.KeyTintAmount;
            var palette = new Dictionary<int, string>();

            void AddRamp(string label, IEnumerable<Color32> shades)
            {
                foreach (var c in shades)
                {
                    palette[Pack(c)] = label;
                    palette[Pack(CharacterPalette.KeyTint(key, c, amount))] = "~" + label;
                }
            }

            foreach (string axis in new[] { "skin", "hair", "outfit", "shirt", "eyes" })
            {
                var ramp = w.Ramps.Ramp(axis, fisher[axis]);
                Assert.IsNotNull(ramp, $"The ramps do not carry the fisher's baked {axis} '{fisher[axis]}'.");
                AddRamp($"{axis}.{fisher[axis]}", ramp.Shades);
            }

            var ink = RigConstRamp(src, "INK");
            AddRamp("boot", RigConstRamp(src, "BOOT"));
            AddRamp("leather", RigConstRamp(src, "LEATHER"));
            AddRamp("brass", RigConstRamp(src, "BRASS"));
            AddRamp("ink", ink);
            AddRamp("key", new[] { key });

            // The brow is a DERIVED colour, not a ramp entry: the rig mixes the darkest hair shade with
            // ink. Lift the mix amount from the rig line rather than restating it.
            var brow = Regex.Match(src, @"brow:\s*\{\s*ramp:\s*\[\s*_mix\(hair\[0\],\s*INK\[0\],\s*([0-9.]+)\)");
            Assert.IsTrue(brow.Success, $"Could not find the brow mix in {RigRelative}.");
            float browMix = float.Parse(brow.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var hairRamp = w.Ramps.Ramp("hair", fisher["hair"]);
            AddRamp("brow", new[] { CharacterPalette.KeyTint(hairRamp.Shades[0], ink[0], browMix) });

            return palette;
        }
    }
}
