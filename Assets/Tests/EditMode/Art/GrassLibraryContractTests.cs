using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The grass library's <b>shader contract</b>, checked against the committed pixels.
    ///
    /// <para><b>Why this test exists.</b> <c>HiddenHarboursGrass.shader</c> bends blade tips in the
    /// VERTEX stage, weighted by the sprite's own <c>UV.y²</c> — zero at the canvas bottom edge,
    /// one at the top. Nothing in Unity checks a sprite against that, and every way of getting it
    /// wrong fails LATE and LOOKS LIKE SOMETHING ELSE:</para>
    /// <list type="bullet">
    /// <item><description>a tuft not rooted on its bottom row <b>shears off the ground</b> — but only
    /// once the wind picks up, so it passes every static screenshot;</description></item>
    /// <item><description>a detached pixel <b>flies away on its own</b>, at the far end of a gust;</description></item>
    /// <item><description>a soft-alpha pixel is a grey fringe at point filtering — invisible at
    /// authoring zoom, obvious in play;</description></item>
    /// <item><description>an OFF-RAMP colour is invisible until the owner turns the lush→straw knob
    /// up, at which point that one variant becomes a colour island in a tinted field. This is the
    /// one the handoff calls out by name, and it is the one a human reviewer cannot catch.</description></item>
    /// </list>
    ///
    /// <para><b>The manifest is checked against the ART, not against itself.</b> Every number here is
    /// re-measured off the committed PNG bytes — read with <see cref="ImageConversion.LoadImage"/>
    /// rather than through the AssetDatabase, so import settings and platform compression cannot
    /// launder a bad bake into a pass. If the rig is re-baked and a variant comes out shorter, its
    /// declared height class stops matching its pixels and this fails.</para>
    /// </summary>
    public class GrassLibraryContractTests
    {
        const string LfsPointerPrefix = "version https://git-lfs.github.com/spec/v1";

        static GrassLibraryCatalog.Library Library()
        {
            var lib = GrassLibraryCatalog.Load();
            Assert.IsNotNull(
                lib,
                $"The grass manifest at '{GrassLibraryCatalog.ManifestPath}' did not load. It is baked " +
                "from docs/art/rigs/grassRig.js — Hidden Harbours ▸ Dev ▸ Bake Grass Library.");
            return lib;
        }

        /// <summary>One committed PNG, decoded from its own bytes.</summary>
        static Texture2D Decode(GrassLibraryCatalog.Entry e)
        {
            Assert.IsTrue(File.Exists(e.Path), $"{e.Name}: the manifest points at '{e.Path}', which does not exist.");

            byte[] bytes = File.ReadAllBytes(e.Path);
            if (bytes.Length < 200 &&
                System.Text.Encoding.ASCII.GetString(bytes, 0, Mathf.Min(bytes.Length, LfsPointerPrefix.Length))
                    .StartsWith("version https://git-lfs"))
                Assert.Fail(
                    $"{e.Name}: '{e.Path}' is still a Git LFS POINTER, not a PNG. The art is committed " +
                    "through LFS — run `git lfs pull`. (A checkout without its art is not a passing state: " +
                    "the grass would silently render as nothing.)");

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            Assert.IsTrue(tex.LoadImage(bytes, markNonReadable: false),
                          $"{e.Name}: '{e.Path}' did not decode as a PNG.");
            return tex;
        }

        // =====================================================================================

        [Test]
        public void Manifest_DeclaresTheRigAndTheExactBladeRamp()
        {
            var lib = Library();

            Assert.AreEqual(GrassLibraryCatalog.RigName, lib.Rig, "The manifest names a different rig.");
            Assert.AreEqual(GrassLibraryCatalog.Ppu, lib.Ppu, "PPU is locked at 32 (ADR 0006/0022).");
            CollectionAssert.AreEqual(
                GrassLibraryCatalog.BaseRamp, lib.BaseRamp,
                "The base blade ramp has moved. The runtime lush→straw knob MULTIPLIES over this ramp, " +
                "and every species ramp is derived from it at identical lightness — change it and every " +
                "tinted field goes incoherent at once.");
        }

        [Test]
        public void Manifest_KeepsTheThreeShippedTuftsExactlyWhereTheyAre()
        {
            var lib = Library();
            foreach (var (name, path, climb) in new[]
            {
                ("GrassTuft",       "Assets/_Project/Art/Sprites/GrassTuft.png",       28),
                ("GrassTuft_Short", "Assets/_Project/Art/Sprites/GrassTuft_Short.png", 15),
                ("GrassTuft_Tall",  "Assets/_Project/Art/Sprites/GrassTuft_Tall.png",  31),
            })
            {
                var e = lib.Find(name);
                Assert.IsNotNull(e, $"{name} is missing from the library. The three #102 tufts are " +
                                    "extended by this library, never replaced.");
                Assert.AreEqual(path, e.Path, $"{name} has been moved. The owner has painted with it and " +
                                              "the St Peters builder plants it — its path is load-bearing.");
                Assert.AreEqual("shipped", e.Source, $"{name} should be declared as shipped, not re-baked.");
                Assert.AreEqual(climb, e.Climb, $"{name}'s declared climb changed — see the pixel test.");
            }
        }

        [Test]
        public void EveryEntry_IsRootedOnItsBottomEdge_WithNothingDetached()
        {
            var lib = Library();
            Assert.IsNotEmpty(lib.Entries, "The library declares no variants at all.");

            foreach (var e in lib.Entries)
            {
                var tex = Decode(e);
                try
                {
                    Assert.AreEqual(e.Width, tex.width, $"{e.Name}: declared width {e.Width}, PNG is {tex.width}.");
                    Assert.AreEqual(e.Height, tex.height, $"{e.Name}: declared height {e.Height}, PNG is {tex.height}.");
                    Assert.AreEqual(0, tex.width % GrassLibraryCatalog.Ppu,
                                    $"{e.Name}: width {tex.width} is not a whole number of metres at PPU 32, " +
                                    "so the bottom-centre pivot does not land on the grid.");

                    var px = tex.GetPixels32();   // row 0 is the BOTTOM row
                    int w = tex.width, h = tex.height;

                    int rooted = 0;
                    for (int x = 0; x < w; x++) if (px[x].a > 0) rooted++;
                    Assert.Greater(rooted, 0,
                        $"{e.Name}: NOTHING on the bottom row. The shader's bend weight is 0 at the bottom " +
                        "edge and 1 at the top, so a tuft that does not touch its own bottom row is a tuft " +
                        "that shears off the ground the first time the wind blows.");

                    // 8-connectivity down to the bottom edge.
                    var seen = new bool[w * h];
                    var stack = new Stack<int>();
                    for (int x = 0; x < w; x++)
                        if (px[x].a > 0) { seen[x] = true; stack.Push(x); }
                    int reached = 0;
                    while (stack.Count > 0)
                    {
                        int p = stack.Pop(); reached++;
                        int cx = p % w, cy = p / w;
                        for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int n = ny * w + nx;
                            if (seen[n] || px[n].a == 0) continue;
                            seen[n] = true; stack.Push(n);
                        }
                    }
                    int lit = px.Count(c => c.a > 0);
                    Assert.AreEqual(lit, reached,
                        $"{e.Name}: {lit - reached} lit pixel(s) are not 8-connected to the bottom edge. " +
                        "Under the vertex bend those fly away on their own.");
                }
                finally { Object.DestroyImmediate(tex); }
            }
        }

        [Test]
        public void EveryEntry_IsHardAlpha_AndStaysOnItsSpeciesRamp()
        {
            var lib = Library();

            foreach (var e in lib.Entries)
            {
                var tex = Decode(e);
                try
                {
                    var legal = new HashSet<string>(
                        lib.LegalColours(e.Species).Select(c => c.ToLowerInvariant()));
                    Assert.IsNotEmpty(legal, $"{e.Name}: the manifest declares no ramp for species '{e.Species}'.");

                    var offRamp = new HashSet<string>();
                    foreach (var c in tex.GetPixels32())
                    {
                        if (c.a != 0 && c.a != 255)
                            Assert.Fail($"{e.Name}: soft alpha {c.a}. Pixel art imports at point filtering, so " +
                                        "a partial alpha is a grey fringe on every blade.");
                        if (c.a == 0) continue;
                        string hex = $"#{c.r:x2}{c.g:x2}{c.b:x2}";
                        if (!legal.Contains(hex)) offRamp.Add(hex);
                    }

                    Assert.IsEmpty(offRamp,
                        $"{e.Name} ({e.Species}) uses {offRamp.Count} colour(s) off its species ramp: " +
                        $"{string.Join(", ", offRamp.Take(5))}. The lush→straw knob MULTIPLIES a tint over the " +
                        "ramp, so an off-ramp variant reads fine today and becomes a colour island the moment " +
                        "the owner tints a field.");
                }
                finally { Object.DestroyImmediate(tex); }
            }
        }

        [Test]
        public void EveryEntry_ClimbsExactlyAsFarAsItClaims_AndIsFiledByThatNumber()
        {
            var lib = Library();

            foreach (var e in lib.Entries)
            {
                var tex = Decode(e);
                try
                {
                    var px = tex.GetPixels32();
                    int w = tex.width, h = tex.height, top = -1;
                    for (int y = h - 1; y >= 0 && top < 0; y--)
                        for (int x = 0; x < w; x++)
                            if (px[y * w + x].a > 0) { top = y; break; }

                    int climb = top + 1;
                    Assert.AreEqual(e.Climb, climb,
                        $"{e.Name}: the manifest says its blades climb {e.Climb} px but the committed PNG " +
                        $"climbs {climb}. Climb is not decoration — the shader bends by UV.y², so climb IS how " +
                        "much wind this sprite takes, and it is what files the sprite into the height mix.");

                    string expected = climb <= 20 ? "short" : climb <= 34 ? "medium" : "tall";
                    Assert.AreEqual(expected, e.HeightClass,
                        $"{e.Name}: climbs {climb} px (a '{expected}' sprite) but is filed as " +
                        $"'{e.HeightClass}'. A mis-filed variant makes the paint tool's height mix lie.");
                }
                finally { Object.DestroyImmediate(tex); }
            }
        }

        [Test]
        public void TheLibrary_CoversEveryHeightClassAndHabitatItAdvertises()
        {
            var lib = Library();

            foreach (string c in GrassLibraryCatalog.HeightClasses)
                Assert.IsNotEmpty(lib.OfClass(c).ToList(),
                    $"No variant is filed as '{c}'. The paint tool offers that toggle, so ticking it would " +
                    "silently fall back to the whole library.");

            foreach (var tag in lib.Habitats.Keys)
                Assert.IsNotEmpty(lib.OfHabitat(tag).ToList(),
                    $"The manifest documents habitat '{tag}' but no variant carries the tag — the scatter " +
                    "pass would ask for it and get an empty field.");

            // The whole point of the library: a dense field must not visibly repeat.
            Assert.GreaterOrEqual(lib.OfHabitat("meadow").Count(), 8,
                "Fewer than eight meadow silhouettes. St Peters is about to be covered in these, and a field " +
                "of three repeated tufts reads as wallpaper the moment the player walks along it.");
        }

        [Test]
        public void Choose_NarrowsByHabitatAndClass_AndNeverPaintsNothing()
        {
            var lib = Library();

            var duneTall = lib.Choose(new[] { "dune" }, new[] { "tall" });
            Assert.IsNotEmpty(duneTall, "dune × tall should resolve — marram is exactly that.");
            Assert.IsTrue(duneTall.All(e => e.HasHabitat("dune") && e.HeightClass == "tall"),
                          "Choose() returned an entry outside the filter.");

            // The fallback, deliberately: an impossible filter must still paint SOMETHING, because a brush
            // that silently lays down nothing reads to the owner as a broken tool, not as a narrow filter.
            var impossible = lib.Choose(new[] { "no-such-habitat" }, new[] { "no-such-class" });
            CollectionAssert.AreEquivalent(lib.Entries, impossible,
                "An unsatisfiable filter must fall back to the whole library, not to an empty brush.");
        }
    }
}
