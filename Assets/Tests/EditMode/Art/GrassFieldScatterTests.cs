using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE DETERMINISM THE 114 MB SCENE BOUGHT ITS WAY OUT OF.</b> The grass layer stopped being 27,000
    /// serialized GameObjects and became a byte plane the meadow is DERIVED from at load. That trade is only
    /// honest if the derivation is exactly reproducible: same field, same seed, same tufts — on this
    /// machine, on the owner's, in a build, twice in a row, for ever (rule 5). If it drifts by one blade,
    /// the meadow is no longer authored content and the tufts would have to go back into the file.
    ///
    /// <para>So these tests derive the same field twice and require the two runs to be identical tuft for
    /// tuft, then do it again on a SECOND component to rule out per-instance state, then show the seed is
    /// actually wired (a "deterministic" scatter that ignores its seed is deterministic by accident).</para>
    ///
    /// <para>Everything here is pure and headless — no scene, no GPU, no editor asset. The field is
    /// synthetic on purpose: it exercises the codec and the scatter without needing St Peters' terrain,
    /// which is what makes it fast enough to run on every commit.</para>
    /// </summary>
    public class GrassFieldScatterTests
    {
        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        // the fixture — a small synthetic field, built without a grain of randomness
        // =====================================================================================

        const int CellsX = 11, CellsY = 9, Slots = 2;
        const float CellSize = 0.85f;

        /// <summary>A field-shaped plane: mostly grass, with bald runs through it — the shape the codec is
        /// asked to compress and the scatter is asked to walk. Built from an integer pattern so it is the
        /// same plane on every machine and in every run.</summary>
        static byte[] SyntheticPlane()
        {
            var plane = new byte[CellsX * CellsY * Slots];
            for (int iy = 0; iy < CellsY; iy++)
            for (int ix = 0; ix < CellsX; ix++)
            for (int s = 0; s < Slots; s++)
            {
                int at = (iy * CellsX + ix) * Slots + s;
                if ((ix * 7 + iy * 3 + s) % 5 == 0) continue;          // a bald run
                int habitat = ((ix + iy + s) % 3) + 1;                  // ids 1..3
                bool broad = (ix % 2) == 0;
                plane[at] = GrassFieldScatter.PackSlot(habitat, broad);
            }
            return plane;
        }

        static Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(32, 32) { name = name + "_tex" };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 32f);
            sprite.name = name;
            return sprite;
        }

        GrassField MakeField(int seed, out Sprite[] variants)
        {
            var go = new GameObject($"GrassField_seed{seed}");
            _spawned.Add(go);
            var field = go.AddComponent<GrassField>();

            variants = new[] { MakeSprite("A"), MakeSprite("B"), MakeSprite("C"), MakeSprite("D") };
            foreach (var s in variants) { _spawned.Add(s); _spawned.Add(s.texture); }

            var pools = new[]
            {
                new GrassField.HabitatPool { Name = "one",   Pool = new[] { 0, 1 },    BroadPool = new[] { 2 } },
                new GrassField.HabitatPool { Name = "two",   Pool = new[] { 1, 2, 3 }, BroadPool = new[] { 3 } },
                new GrassField.HabitatPool { Name = "three", Pool = new[] { 0, 3 },    BroadPool = Array.Empty<int>() },
            };

            var straw = new byte[16 * 12];
            for (int i = 0; i < straw.Length; i++) straw[i] = (byte)((i * 13) % 256);

            // material: null on purpose — the derive is what these tests are about, and a null material
            // makes Rebuild a no-op so no meshes are built for a determinism check.
            field.SetField(
                new Vector2(-3f, 2f), CellSize, CellSize * 0.41f, CellSize * 0.32f,
                CellsX, CellsY, Slots, seed, SyntheticPlane(),
                4f, 16, 12, straw, new Color(0.86f, 0.78f, 0.52f, 1f),
                variants, pools, material: null);

            return field;
        }

        static void AssertSameMeadow(List<GrassField.Blade> a, List<GrassField.Blade> b, string what)
        {
            Assert.AreEqual(a.Count, b.Count, $"{what}: the two runs grew a different NUMBER of tufts.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 0f, $"{what}: tuft {i} moved in X.");
                Assert.AreEqual(a[i].Position.y, b[i].Position.y, 0f, $"{what}: tuft {i} moved in Y.");
                Assert.AreEqual(a[i].Variant, b[i].Variant, $"{what}: tuft {i} changed its blade.");
                Assert.AreEqual(a[i].Scale, b[i].Scale, 0f, $"{what}: tuft {i} changed size.");
                Assert.AreEqual(a[i].Mirror, b[i].Mirror, $"{what}: tuft {i} flipped.");
                Assert.AreEqual(a[i].Tint, b[i].Tint, $"{what}: tuft {i} changed colour.");
            }
        }

        // =====================================================================================
        // the determinism this whole change stands on
        // =====================================================================================

        [Test]
        public void SameFieldAndSeed_DerivedTwice_GrowsTheIdenticalMeadow()
        {
            var field = MakeField(seed: 0, out _);
            AssertSameMeadow(field.DeriveBlades(), field.DeriveBlades(),
                             "deriving the same field twice");
        }

        [Test]
        public void SameFieldAndSeed_OnTwoComponents_GrowsTheIdenticalMeadow()
        {
            // Two separate instances: this is the "every machine grows the same meadow" claim, minus the
            // machines. Per-instance state (a cached list, a lazily seeded RNG) would show up here.
            var a = MakeField(seed: 7, out _);
            var b = MakeField(seed: 7, out _);
            AssertSameMeadow(a.DeriveBlades(), b.DeriveBlades(), "two components with the same seed");
        }

        [Test]
        public void ADifferentSeed_GrowsADifferentMeadow()
        {
            // A scatter that ignores its seed is "deterministic" by accident, and would keep passing the
            // tests above while silently making the seed a lie.
            var a = MakeField(seed: 0, out _).DeriveBlades();
            var b = MakeField(seed: 1, out _).DeriveBlades();

            Assert.Greater(a.Count, 0, "sanity: the synthetic field grew nothing");
            bool anyMoved = false;
            int n = Mathf.Min(a.Count, b.Count);
            for (int i = 0; i < n && !anyMoved; i++)
                if (a[i].Position != b[i].Position) anyMoved = true;

            Assert.IsTrue(anyMoved,
                "Two different seeds grew the same meadow — the seed is not reaching the scatter, so " +
                "GrassFieldLayout.Seed is decoration rather than an input.");
        }

        [Test]
        public void SeedZero_LeavesEverySaltWhereItShipped()
        {
            // The migration must be look-neutral: seed 0 has to reproduce the scatter the owner ratified,
            // which is only true if the seed shifts nothing at zero.
            for (int salt = -3; salt <= 300; salt += 37)
                Assert.AreEqual(salt, GrassFieldScatter.Salt(salt, 0),
                    "Seed 0 moved a salt, so the field bake cannot reproduce the ratified meadow.");
        }

        [Test]
        public void EveryDerivedTuft_LandsInsideItsCellPlusTheJitterAndSpread()
        {
            // The bake gates a site at exactly the point the renderer draws it. If the derive could wander
            // further than the bake modelled, grass would appear on ground the pass believed it had kept
            // clear — a footpath, a doorstep, the wharf.
            var field = MakeField(seed: 3, out _);
            var layout = field.Layout;
            float reach = layout.JitterMetres + layout.SpreadMetres;

            foreach (var blade in field.DeriveBlades())
            {
                float gx = (blade.Position.x - layout.OriginX) / layout.CellSize;
                float gy = (blade.Position.y - layout.OriginY) / layout.CellSize;
                Assert.GreaterOrEqual(gx, -reach / layout.CellSize, "a tuft grew off the west edge");
                Assert.GreaterOrEqual(gy, -reach / layout.CellSize, "a tuft grew off the south edge");
                Assert.LessOrEqual(gx, layout.CellsX + reach / layout.CellSize, "a tuft grew off the east edge");
                Assert.LessOrEqual(gy, layout.CellsY + reach / layout.CellSize, "a tuft grew off the north edge");
            }
        }

        [Test]
        public void ABroadSiteWithNoWideBake_KeepsItsNormalPool_RatherThanGoingBald()
        {
            // Habitat "three" ships an empty BroadPool. Sparser art beats a bald patch the owner then has
            // to diagnose from a screenshot.
            var field = MakeField(seed: 0, out var variants);
            var blades = field.DeriveBlades();
            Assert.Greater(blades.Count, 0, "sanity: nothing grew");
            foreach (var b in blades)
                Assert.IsTrue(b.Variant >= 0 && b.Variant < variants.Length,
                    "a derived tuft indexed outside the palette — an empty pool leaked through");
        }

        // =====================================================================================
        // the slot byte
        // =====================================================================================

        [Test]
        public void SlotByte_PacksAndUnpacksEveryHabitatAndTheBroadFlag()
        {
            for (int id = 1; id <= GrassFieldScatter.MaxHabitats; id++)
            foreach (bool broad in new[] { false, true })
            {
                byte b = GrassFieldScatter.PackSlot(id, broad);
                Assert.AreEqual(id, GrassFieldScatter.HabitatOf(b), $"habitat {id} did not survive packing");
                Assert.AreEqual(broad, GrassFieldScatter.BroadOf(b), $"broad flag lost for habitat {id}");
                Assert.IsFalse(GrassFieldScatter.IsEmpty(b), $"habitat {id} packed as empty");
            }
        }

        [Test]
        public void EmptyGround_PacksToZero_SoARunOfItCostsAlmostNothing()
        {
            // Empty must be 0, and must carry no flags: a run of zeroes is what turns the sea, the walked
            // paths and the building plots into a few bytes instead of a few hundred kilobytes.
            Assert.AreEqual(0, GrassFieldScatter.PackSlot(0, broad: false));
            Assert.AreEqual(0, GrassFieldScatter.PackSlot(0, broad: true),
                "An empty site kept its broad flag — an empty run would stop being one run.");
            Assert.IsTrue(GrassFieldScatter.IsEmpty(GrassFieldScatter.EmptySlot));
        }

        // =====================================================================================
        // the codec
        // =====================================================================================

        [Test]
        public void Codec_RoundTripsAFieldExactly()
        {
            byte[] plane = SyntheticPlane();
            CollectionAssert.AreEqual(plane, GrassFieldCodec.Decode(GrassFieldCodec.Encode(plane)),
                "The field did not survive a round trip — the meadow in the scene is not the meadow the " +
                "pass baked.");
        }

        [Test]
        public void Codec_RoundTripsEveryByteValue_IncludingRunsOfOneAndRunsOverTwoHundredAndFiftyFive()
        {
            // The varint run length is the part most likely to be wrong: 255 is the boundary a naive
            // byte-count encoder trips on, and a 1-long run is the degenerate case at the other end.
            var plane = new byte[1000];
            for (int i = 0; i < 256; i++) plane[i] = (byte)i;            // 256 runs of one
            for (int i = 256; i < 1000; i++) plane[i] = 42;              // one run of 744
            CollectionAssert.AreEqual(plane, GrassFieldCodec.Decode(GrassFieldCodec.Encode(plane)));
        }

        [Test]
        public void Codec_TurnsAFieldShapedPlaneIntoAFractionOfItsSize()
        {
            // The number this whole change exists for. A real field is long runs of one habitat and longer
            // runs of nothing; if the codec ever stopped compressing that, the field would creep back
            // toward the file size it replaced.
            var plane = new byte[40000];
            for (int i = 0; i < plane.Length; i++)
                plane[i] = (byte)(i < 12000 ? 0 : (i / 800) % 4 + 1);    // bald half, then broad bands

            string payload = GrassFieldCodec.Encode(plane);
            Assert.Less(payload.Length, plane.Length / 4,
                $"A field-shaped plane of {plane.Length} bytes encoded to {payload.Length} characters — " +
                "the run-length step has stopped earning its place.");
        }

        [Test]
        public void Codec_ReadsAnEmptyFieldAsEmpty_RatherThanThrowing()
        {
            // An unbaked field draws nothing. That is the honest answer for a region whose pass has not
            // been run, and it must not be a crash on load.
            CollectionAssert.IsEmpty(GrassFieldCodec.Decode(string.Empty));
            CollectionAssert.IsEmpty(GrassFieldCodec.Decode(null));
            Assert.AreEqual(string.Empty, GrassFieldCodec.Encode(Array.Empty<byte>()));
            Assert.AreEqual(string.Empty, GrassFieldCodec.Encode(null));
        }

        [Test]
        public void Codec_RefusesAPayloadThatIsNotAField()
        {
            // Fail loudly. A payload that decodes to garbage would draw a half-grown meadow somebody then
            // has to diagnose from a screenshot.
            Assert.Throws<FormatException>(() => GrassFieldCodec.Decode("bm90IGEgZmllbGQ="),
                "A foreign Base64 payload decoded without complaint.");
            Assert.Throws<FormatException>(() => GrassFieldCodec.Decode("!!!not base64!!!"),
                "A payload that is not even Base64 decoded without complaint.");
        }

        [Test]
        public void Codec_RefusesATruncatedPayload()
        {
            string payload = GrassFieldCodec.Encode(SyntheticPlane());
            byte[] raw = Convert.FromBase64String(payload);
            var cut = new byte[raw.Length - 3];
            Array.Copy(raw, cut, cut.Length);

            Assert.Throws<FormatException>(() => GrassFieldCodec.Decode(Convert.ToBase64String(cut)),
                "A truncated field decoded to a short meadow instead of naming itself.");
        }

        [Test]
        public void Codec_RefusesAFutureFormatVersion()
        {
            byte[] raw = Convert.FromBase64String(GrassFieldCodec.Encode(SyntheticPlane()));
            raw[3] = (byte)(GrassFieldCodec.Version + 1);
            Assert.Throws<FormatException>(() => GrassFieldCodec.Decode(Convert.ToBase64String(raw)),
                "A field written by a newer format was decoded by this build anyway.");
        }
    }
}
