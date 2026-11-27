using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The line leaves the mouth of the fish that is drawn, on every frame.</b> The rig publishes each
    /// surfaced frame's mouth off its skeleton, and on most frames that point lay 1 to 6 art px off the
    /// drawn fish, so the line ended in open water beside her. The import now snaps each frame's anchor onto
    /// that frame's own art (<see cref="RodKitAnchorMath.TrySnapToDrawn"/>), and the drawer takes the anchor
    /// of the very sprite it sets (<see cref="FishArtLibrary.TryMouthFor"/>) at the scale it is drawn.
    ///
    /// <para>Walked through the one drawer both fishers use (<see cref="SurfacedFishDrawer"/>): at scale 1
    /// (the player's rod) and 2 (a villager's look), every dart and thrash frame of every rung, the line's
    /// end is checked against the sheet's own pixels on disk. Then the villager's presenter is driven end
    /// to end; the player's presenter is pinned the same way in PlayMode
    /// (<c>RodFightPresenterGoldenTests.TheLine_LeavesTheDrawnMouth_OnEverySurfacedFrame</c>).</para>
    /// </summary>
    public class FishMouthAnchorTests
    {
        private const float ArtPixel = 1f;            // the tolerance, in art pixels
        private const float SecondsPerFrame = 0.1f;
        private const float DartSpeedMps = 1f;
        private const int Directions = 8;

        private readonly List<Object> _made = new List<Object>();
        private readonly Dictionary<string, Texture2D> _sheets = new Dictionary<string, Texture2D>();
        private IFishBehaviourFacts _factsWas;

        private sealed class StillFacts : IFishBehaviourFacts
        {
            public bool Jumps(string fishId) => false;
            public float FightJumpPeriodSeconds(string fishId) => 0f;
            public bool TryWeightRangeKg(string fishId, out float minKg, out float maxKg) { minKg = maxKg = 0f; return false; }
        }

        [SetUp]
        public void SetUp()
        {
            _factsWas = GameServices.FishBehaviour;
            GameServices.FishBehaviour = new StillFacts();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.FishBehaviour = _factsWas;
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            foreach (Texture2D t in _sheets.Values) if (t != null) Object.DestroyImmediate(t);
            _sheets.Clear();
        }

        // ---- the snap rule --------------------------------------------------------------------------

        [Test]
        public void TheSnap_KeepsADrawnPoint_AndOtherwiseTakesTheNearestDrawnTexel_LipBeforeCheek()
        {
            // A 9x9 cell with its pivot on the corner at (4, 4).
            var drawn = new HashSet<(int, int)> { (4, 4), (5, 4), (6, 4), (2, 6) };
            bool Drawn(int x, int y) => drawn.Contains((x, y));

            Assert.IsTrue(RodKitAnchorMath.TrySnapToDrawn(Drawn, 9, 9, 5, 4, 4f, 4f, 8, out int x, out int y));
            Assert.AreEqual((5, 4), (x, y), "a point already on the drawing moved");

            Assert.IsTrue(RodKitAnchorMath.TrySnapToDrawn(Drawn, 9, 9, 8, 4, 4f, 4f, 8, out x, out y));
            Assert.AreEqual((6, 4), (x, y), "not the nearest drawn texel");

            // Two drawn texels 2 px either side of an undrawn point: the tie goes to the one farther from the
            // pivot (the lip, not the cheek).
            var pair = new HashSet<(int, int)> { (3, 4), (7, 4) };
            Assert.IsTrue(RodKitAnchorMath.TrySnapToDrawn((a, b) => pair.Contains((a, b)), 9, 9, 5, 4, 4f, 4f, 8,
                                                          out x, out y));
            Assert.AreEqual((7, 4), (x, y), "a tie must go to the texel farther from the pivot");

            Assert.IsFalse(RodKitAnchorMath.TrySnapToDrawn(Drawn, 9, 9, 8, 8, 4f, 4f, 1, out x, out y),
                           "nothing drawn within the radius, yet it snapped");
            Assert.AreEqual((8, 8), (x, y), "a failed snap must hand back the published point");

            Vector2 centre = RodKitAnchorMath.TexelCentreToPivotWorld(5, 4, 4f, 4f, 32f);
            Assert.AreEqual(1.5f / 32f, centre.x, 1e-6f);
            Assert.AreEqual(-0.5f / 32f, centre.y, 1e-6f, "texel centres are y up from the pivot");
        }

        // ---- every frame, through the drawer ---------------------------------------------------------

        [Test]
        public void EverySurfacedFrame_TheLineEndsOnTheDrawnFish_AtScaleOneAndTwo()
        {
            FishArtLibrary library = Library();
            int frames = 0;
            foreach (FishSpeciesVisual species in library.Species)
            {
                if (species?.Rungs == null) continue;
                foreach (FishRungVisual rung in species.Rungs)
                {
                    if (rung == null) continue;
                    foreach (bool dart in new[] { true, false })
                    {
                        Sprite[] sheet = dart ? rung.DartFrames : rung.ThrashFrames;
                        int perDir = dart ? rung.DartFramesPerDir : rung.ThrashFramesPerDir;
                        if (sheet == null || sheet.Length == 0 || perDir <= 0) continue;
                        foreach (int scale in new[] { 1, 2 })
                            frames += WalkEveryFrame(library, species.FishId, rung, sheet, perDir, dart, scale);
                    }
                }
            }
            Assert.Greater(frames, 0, "harness: the fish art table has no surfaced frames");
        }

        [Test]
        public void EveryMouthInTheTable_SitsOnItsOwnFramesArt_RollAndJumpToo()
        {
            FishArtLibrary library = Library();
            var misses = new List<string>();
            int checkedFrames = 0;
            foreach (FishSpeciesVisual species in library.Species)
            {
                if (species?.Rungs == null) continue;
                foreach (FishRungVisual r in species.Rungs)
                {
                    if (r == null) continue;
                    checkedFrames += CheckTable(species.FishId + r.Suffix + " dart", r.DartFrames, r.DartMouthOffsets, misses);
                    checkedFrames += CheckTable(species.FishId + r.Suffix + " thrash", r.ThrashFrames, r.ThrashMouthOffsets, misses);
                    checkedFrames += CheckTable(species.FishId + r.Suffix + " roll", r.RollFrames, r.RollMouthOffsets, misses);
                    checkedFrames += CheckTable(species.FishId + r.Suffix + " jump", r.JumpFrames, r.JumpMouthOffsets, misses);
                }
            }
            Assert.Greater(checkedFrames, 0, "harness: nothing to check");
            Assert.IsEmpty(misses, $"{misses.Count} of {checkedFrames} mouths are off their frame's drawn fish:\n" +
                                   string.Join("\n", misses.GetRange(0, Mathf.Min(20, misses.Count))));
        }

        // ---- the villager's presenter, end to end ----------------------------------------------------

        [Test]
        public void TheVillagersLine_LeavesTheDrawnMouth_AtHisScale_AndSheSortsByHerSideOfHim()
        {
            FishArtLibrary library = Library();
            FishSpeciesVisual cod = library.Find("fish.atlantic_cod");
            Assume.That(cod != null, "no cod ladder in the fish art table at this build");

            GameObject host = Track(new GameObject("NpcAnglerMouth"));
            SpriteRenderer body = host.AddComponent<SpriteRenderer>();
            body.sortingOrder = 400;
            var p = host.AddComponent<NpcAnglerPresenter>();
            var stand = new Stand();
            stand.LookValue.FishDrawScale = 2;
            p.Bind(stand);

            int frame = 10;
            var seen = new HashSet<Sprite>();
            for (int i = 0; i < 48; i++)
            {
                float a = i * 0.41f;
                float offY = 1.6f + 0.9f * Mathf.Sin(a);   // beyond his feet, at the deck lip and out
                stand.State = Surface(0.8f * Mathf.Cos(a), offY, 5.59f);
                stand.Seconds = i * 0.05f;
                p.Present(frame++);
                SpriteRenderer fish = p.FishRenderer;
                Assert.IsTrue(fish.enabled, $"step {i}: no fish drawn");
                Assert.IsTrue(library.TryMouthFor(fish.sprite, out Vector2 mouth), $"step {i}: frame not in the table");
                Vector2 want = (Vector2)fish.transform.position + mouth * 2f;
                Assert.AreEqual(want.x, p.LineFarEnd.x, 1e-4f, $"step {i}: line end x is not the drawn mouth at scale 2");
                Assert.AreEqual(want.y, p.LineFarEnd.y, 1e-4f, $"step {i}: line end y is not the drawn mouth at scale 2");
                AssertOnTheArt(fish.sprite, (p.LineFarEnd - (Vector2)fish.transform.position) / 2f, $"step {i}");
                Assert.Less(fish.sortingOrder, body.sortingOrder, $"step {i}: she drew over him from beyond his feet");
                seen.Add(fish.sprite);
            }
            Assert.Greater(seen.Count, 1, "harness: the walk drew one frame only");

            stand.State = Surface(0.4f, -0.8f, 5.59f);
            p.Present(frame++);
            Assert.Greater(p.FishRenderer.sortingOrder, body.sortingOrder, "short of his feet she stands in front of him");
        }

        [Test]
        public void TheVillagersSplash_BeyondHisFeet_IsOnTheWaterBehindHim_AndShortOfThemInFront()
        {
            GameObject host = Track(new GameObject("NpcAnglerSplash"));
            SpriteRenderer body = host.AddComponent<SpriteRenderer>();
            body.sortingOrder = 1180;
            var p = host.AddComponent<NpcAnglerPresenter>();
            var stand = new Stand();
            var tex = Track(new Texture2D(4, 4));
            stand.LookValue.SplashFrames = new[] { Track(Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f)) };
            stand.LookValue.SplashFps = 10f;
            p.Bind(stand);

            int frame = 10;
            // The fish breaks the water 1.09 m beyond his feet (the wharf head plate's landing point) and again
            // short of them; each edge throws a splash at her water point.
            foreach (float offY in new[] { 1.09f, -0.8f })
            {
                stand.State = Surface(0.1f, offY, 5.59f, phase: FishingPhase.FightDeep);
                stand.Seconds = 0.5f;
                p.Present(frame++);
                stand.State = Surface(0.1f, offY, 5.59f);
                stand.Seconds = 0f;
                p.Present(frame++);
                SpriteRenderer splash = p.SplashRenderer;
                Assert.IsTrue(splash.enabled, $"harness: no splash at the break {offY} m from his feet");
                Assert.AreEqual(host.transform.position.y + offY, splash.transform.position.y, 1e-4f,
                                "harness: the splash is not at her water point");
                if (offY >= 0f)
                {
                    Assert.Less(splash.sortingOrder, body.sortingOrder, "the splash beyond his feet drew over him");
                    Assert.Less(splash.sortingOrder, p.FishRenderer.sortingOrder, "the ring drew over the fish breaking through it");
                }
                else Assert.Greater(splash.sortingOrder, body.sortingOrder, "short of his feet the splash is in front of him");
                p.Present(frame += 30);   // a gap: the next pass starts fresh
            }
        }

        // ---- harness --------------------------------------------------------------------------------

        private sealed class Stand : IAnglerStand
        {
            public FishingState State;
            public float Seconds;
            public AnglerLook LookValue = new AnglerLook
            {
                LineWidthM = 0.02f, LineSegments = 8, FishSecondsPerFrame = SecondsPerFrame,
                FishDartSpeedMps = DartSpeedMps, FishSpeedSmoothing01 = 0.35f, FallbackJumpPeriodSeconds = 0f,
                WaterElementOrder = 30, FishDrawScale = 1,
            };

            public bool TryGetAnglerState(out FishingState state, out float secondsInPhase)
            {
                state = State;
                secondsInPhase = Seconds;
                return true;
            }

            public AnglerLook Look => LookValue;
        }

        /// <summary>Drive the drawer onto every frame of one sheet at <paramref name="scale"/>, checking each
        /// line end against the art. Returns how many frames were drawn.</summary>
        private int WalkEveryFrame(FishArtLibrary library, string fishId, FishRungVisual rung, Sprite[] sheet,
                                   int perDir, bool dart, int scale)
        {
            var go = Track(new GameObject("SurfacedFishMouth"));
            var sr = go.AddComponent<SpriteRenderer>();
            // Every row by her travel alone: the walk moves her along each heading, some of them down the
            // line, which the side-on rule would turn across it.
            var drawer = new SurfacedFishDrawer(sr) { FlankWithinLineDegreesOverride = 0f };
            var only = new[] { new FishSpeciesVisual { FishId = fishId, Rungs = new[] { rung } } };
            FishingState s = Surface(0f, 0f, rung.Kg, fishId);
            drawer.Resolve(in s, only, library, GameServices.FishBehaviour, 0f);

            var drawnFrames = new HashSet<Sprite>();
            string label = $"{fishId}{rung.Suffix} {(dart ? "dart" : "thrash")} x{scale}";
            for (int row = 0; row < Directions; row++)
            {
                Vector2 heading = HeadingForRow(row);
                for (int f = 0; f < perDir; f++)
                {
                    // Two renders: the first parks her, the second moves her along this row's heading at a
                    // dart's speed or a thrash's crawl, on the frame's own slot of the flipbook clock.
                    float dt = dart ? 0.1f : 10f;
                    float step = dart ? 0.5f : 0.01f;
                    Vector2 start = new Vector2(3f, 2f);
                    float clock = (f + 0.5f) * SecondsPerFrame;
                    FishingState here = Surface(start.x, start.y, rung.Kg, fishId);
                    drawer.Render(Vector2.zero, in here, dt, clock, clock, SecondsPerFrame, DartSpeedMps, 1f,
                                  0, scale, out _, out _);
                    Vector2 next = start + heading * step;
                    FishingState there = Surface(next.x, next.y, rung.Kg, fishId);
                    Vector2 end = drawer.Render(Vector2.zero, in there, dt, clock, clock, SecondsPerFrame,
                                                DartSpeedMps, 1f, 0, scale, out _, out _);
                    Assert.IsTrue(sr.enabled, $"{label} row {row} frame {f}: not drawn");
                    Sprite want = sheet[row * perDir + f];
                    Assert.AreSame(want, sr.sprite, $"{label} row {row} frame {f}: the drawer drew another frame");
                    Vector2 local = (end - (Vector2)sr.transform.position) / scale;
                    AssertOnTheArt(sr.sprite, local, $"{label} row {row} frame {f}");
                    drawnFrames.Add(sr.sprite);
                }
            }
            Assert.AreEqual(sheet.Length, drawnFrames.Count, $"{label}: the walk missed frames");
            return drawnFrames.Count;
        }

        private int CheckTable(string label, Sprite[] frames, Vector2[] mouths, List<string> misses)
        {
            if (frames == null || mouths == null) return 0;
            int n = 0;
            for (int i = 0; i < frames.Length && i < mouths.Length; i++)
            {
                if (frames[i] == null) continue;
                n++;
                if (!OnTheArt(frames[i], mouths[i], out float d)) misses.Add($"{label} [{i}] {d:0.0} px");
            }
            return n;
        }

        private void AssertOnTheArt(Sprite cell, Vector2 localWorld, string where)
        {
            Assert.IsTrue(OnTheArt(cell, localWorld, out float d),
                          $"{where}: the line ends {d:0.0} art px from the drawn fish (allowed {ArtPixel})");
        }

        /// <summary>Is a point (world m from the sprite's pivot, at scale 1) within one art pixel of a drawn
        /// texel's centre on <paramref name="cell"/>'s own art, read from the sheet on disk.</summary>
        private bool OnTheArt(Sprite cell, Vector2 localWorld, out float nearest)
        {
            Texture2D png = Sheet(cell);
            float k = png.width / (float)cell.texture.width;
            Rect r = cell.rect;
            float ppu = cell.pixelsPerUnit;
            float px = (cell.pivot.x + localWorld.x * ppu) * k;   // from the rect's left, PNG pixels
            float py = (cell.pivot.y + localWorld.y * ppu) * k;   // from the rect's bottom
            int x0 = Mathf.RoundToInt(r.x * k), y0 = Mathf.RoundToInt(r.y * k);
            int w = Mathf.RoundToInt(r.width * k), h = Mathf.RoundToInt(r.height * k);
            nearest = float.MaxValue;
            int reach = Mathf.CeilToInt(3f * k);
            for (int y = Mathf.FloorToInt(py) - reach; y <= Mathf.FloorToInt(py) + reach; y++)
            for (int x = Mathf.FloorToInt(px) - reach; x <= Mathf.FloorToInt(px) + reach; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                if (png.GetPixel(x0 + x, y0 + y).a < RodKitImporter.DrawnAlpha01) continue;
                float dx = x + 0.5f - px, dy = y + 0.5f - py;
                nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy) / k);
            }
            return nearest <= ArtPixel;
        }

        private Texture2D Sheet(Sprite cell)
        {
            string path = AssetDatabase.GetAssetPath(cell.texture);
            if (_sheets.TryGetValue(path, out Texture2D t)) return t;
            Assert.IsTrue(File.Exists(path), $"harness: no sheet on disk at '{path}'");
            t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(t.LoadImage(File.ReadAllBytes(path)), $"harness: '{path}' does not decode");
            _sheets[path] = t;
            return t;
        }

        private static Vector2 HeadingForRow(int row)
        {
            for (int deg = 0; deg < 360; deg++)
            {
                // The middle of the row's arc: a degree on either side lands in the same row.
                float rad = (deg + 0.5f) * Mathf.Deg2Rad;
                var v = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                bool Here(Vector2 u) => IsoFacing.HeadingToFacingIndex(
                    RodPresenterMath.HeadingDegrees(u.x, u.y), Directions, 0f, false) == row;
                float back = (deg - 2f) * Mathf.Deg2Rad, on = (deg + 3f) * Mathf.Deg2Rad;
                if (Here(v) && Here(new Vector2(Mathf.Cos(back), Mathf.Sin(back)))
                            && Here(new Vector2(Mathf.Cos(on), Mathf.Sin(on))))
                    return v;
            }
            Assert.Fail($"harness: no heading faces row {row}");
            return Vector2.right;
        }

        private static FishArtLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<FishArtLibrary>(FishArtLibraryBuilder.LibraryPath);
            Assert.IsNotNull(library, $"no fish art table at {FishArtLibraryBuilder.LibraryPath}");
            return library;
        }

        private static FishingState Surface(float offX, float offY, float kg, string fish = "fish.atlantic_cod",
                                            FishingPhase phase = FishingPhase.FightSurface)
            => new FishingState(phase, 0.3f, 0.3f, fish, fish, FishCategory.InshoreGroundfish,
                                kg, 0.4f, false, 0.24f, offX, offY, 0f, 0f, 0f, 0f, offX * 0.1f, offY * 0.1f, 0f);

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }
    }
}
