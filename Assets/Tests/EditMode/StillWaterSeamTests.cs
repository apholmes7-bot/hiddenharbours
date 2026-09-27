using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The still-water SEAM (ADR 0046, terrain pass 9 PR 4). Subjects: Core's <see cref="StillWaterLevels"/>
    /// — the ONE composition, <b>water = max(tide, still)</b>, and the still map's 16-bit code —,
    /// <see cref="EmptyStillWater"/> and <see cref="GameServices.StillWater"/> (never null; a registrant clears
    /// only its own registration), and World's <see cref="PaintedStillWater"/> with
    /// <see cref="PaintedHeightMap.StillWater"/>: the painted source the sim reads and the render samples.
    ///
    /// <para><b>Every expectation is a literal worked by hand</b> from the terrain plan — the Bog Pond's
    /// surface at +5.30 m, the spring tide at ±2.2 m, the bar crest at +0.88 m, the map's −4 … +7 m scale over
    /// 65535 codes — never a value read back from the code under test.</para>
    ///
    /// <para><b>On the base code</b> none of this exists, and the file does not compile: there is no
    /// <c>IStillWater</c>, no <c>StillWaterLevels</c> and no <c>GameServices.StillWater</c>. The base's
    /// water is the tide everywhere, so the pond these tests stand in would read −2.2 m at spring low water,
    /// not +5.30 m.</para>
    /// </summary>
    public class StillWaterSeamTests
    {
        // The terrain plan's numbers: the Bog Pond stands at +5.30 m; the spring tide runs ±2.2 m; the bar
        // crest is +0.88 m.
        const float BogPond = 5.30f;
        const float SpringLow = -2.2f;
        const float SpringHigh = 2.2f;
        const float BarCrest = 0.88f;

        // The still map's scale is the height map's (plan decision 11): −4 … +7 m over 65535 codes, so one
        // code is 11 / 65535 = 0.000168 m.
        const float MapMin = -4f;
        const float MapMax = 7f;
        const float OneCode = (MapMax - MapMin) / 65535f;

        // The Bog Pond's code on that scale, by hand: (5.30 − (−4)) / 11 × 65535 = 55406.86 → 55407.
        const ushort BogPondCode = 55407;

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            GameServices.Reset();
        }

        /// <summary>
        /// Subject: <see cref="StillWaterLevels.Compose"/>. A pond above the tide is the water at every tide
        /// below it; where the tide stands higher it rises through the pond and the water is the tide.
        /// <b>On the base</b> the water is the tide alone: −2.2 m where this expects +5.30 m.
        /// </summary>
        [Test]
        public void Compose_IsTheHigherOfTheTideAndTheStillLevel()
        {
            Assert.AreEqual(BogPond, StillWaterLevels.Compose(SpringLow, BogPond),
                "a pond at +5.30 m over a spring low tide of −2.2 m: the water is the pond.");
            Assert.AreEqual(BogPond, StillWaterLevels.Compose(SpringHigh, BogPond),
                "a pond at +5.30 m over a spring high tide of +2.2 m: the water is still the pond.");
            Assert.AreEqual(SpringHigh, StillWaterLevels.Compose(SpringHigh, 1.0f),
                "a pan at +1.0 m under a +2.2 m tide: the tide rises through it, and the water is the tide.");
            Assert.AreEqual(1.0f, StillWaterLevels.Compose(SpringLow, 1.0f),
                "the same pan when the tide falls to −2.2 m: the pan holds its +1.0 m.");
            Assert.AreEqual(BarCrest, StillWaterLevels.Compose(BarCrest, BarCrest),
                "a still level equal to the tide: both are +0.88 m.");
            Debug.Log("[StillWaterSeam] compose: max(tide, still) at the pond, the pan and the bar crest.");
        }

        /// <summary>
        /// Subject: <see cref="StillWaterLevels.Compose"/> and <see cref="StillWaterLevels.WaterLevelAt"/>
        /// with no still water. The neutrality every region without a still map depends on: the tide comes
        /// back <b>bit for bit</b>, for "none", for a NaN still level (a corrupt decode) and for a still level
        /// under the tide. Negative controls: <see cref="Mathf.Max(float,float)"/> and <see cref="Math.Max(float,float)"/>
        /// both return NaN for a NaN still level, so either would fail here.
        /// </summary>
        [Test]
        public void Compose_WithNoStillWater_IsTheTideBitForBit()
        {
            Assert.IsTrue(float.IsNegativeInfinity(StillWaterLevels.None),
                "\"no still water\" must be negative infinity, so it loses every max.");

            float[] tides =
            {
                SpringLow, -1.0f, 0f, -0f, BarCrest, 1.0f, SpringHigh, float.Epsilon, -float.Epsilon,
                -1000f, 1000f, float.MinValue, float.MaxValue,
            };
            float[] noWater = { StillWaterLevels.None, float.NaN, float.MinValue };
            var env = new FlatEnv();
            int checks = 0, bad = 0;
            string first = null;
            foreach (float tide in tides)
            {
                foreach (float still in noWater)
                {
                    if (still == float.MinValue && tide == float.MinValue) continue;   // equal: either is right
                    checks++;
                    float got = StillWaterLevels.Compose(tide, still);
                    if (Bits(got) != Bits(tide))
                    {
                        bad++;
                        first ??= $"Compose({tide:R}, {still:R}) = {got:R} (bits {Bits(got):X8}), " +
                                  $"not the tide's bits {Bits(tide):X8}";
                    }
                }

                // The same through the whole read: a null still water, and the empty one.
                env.Level = tide;
                foreach (IStillWater still in new IStillWater[] { null, EmptyStillWater.Instance })
                {
                    checks++;
                    float got = StillWaterLevels.WaterLevelAt(env, still, 0.0, new Vector2(12f, -7f));
                    if (Bits(got) != Bits(tide))
                    {
                        bad++;
                        first ??= $"WaterLevelAt with {(still == null ? "null" : "Empty")} = {got:R}, not the " +
                                  $"tide {tide:R}";
                    }
                }
            }

            Assert.AreEqual(0, bad, $"{bad} of {checks} reads with no still water did not return the tide bit " +
                                    $"for bit. First: {first}");
            Debug.Log($"[StillWaterSeam] no still water: {checks} reads, every one the tide bit for bit.");
        }

        /// <summary>
        /// Subject: <see cref="StillWaterLevels.WaterLevelAt"/>. The environment's tide at the query time,
        /// composed with the still water AT THE QUERY POSITION: the pond inside it, the tide outside it; the
        /// same query twice gives the same bits. <b>On the base</b> the water level at the pond over a −2.2 m
        /// tide is −2.2 m, where this expects +5.30 m.
        /// </summary>
        [Test]
        public void WaterLevelAt_ComposesTheRegionsTideWithTheStillWaterAtThatPoint()
        {
            var pondCentre = new Vector2(-120f, 214f);
            var pond = new PondDouble(pondCentre, 6f, BogPond);
            var env = new FlatEnv { Level = SpringLow };
            Vector2 offPond = pondCentre + new Vector2(10f, 0f);

            Assert.AreEqual(BogPond, StillWaterLevels.WaterLevelAt(env, pond, 0.0, pondCentre),
                "in the pond at spring low water, the water is the pond's +5.30 m.");
            Assert.AreEqual(SpringLow, StillWaterLevels.WaterLevelAt(env, pond, 0.0, offPond),
                "10 m outside the pond, the water is the tide's −2.2 m.");
            Assert.AreEqual(SpringLow, StillWaterLevels.WaterLevelAt(env, null, 0.0, pondCentre),
                "no still water given: the tide alone.");

            env.Level = SpringHigh;
            Assert.AreEqual(BogPond, StillWaterLevels.WaterLevelAt(env, pond, 0.0, pondCentre),
                "in the pond at spring high water, the water is still the pond's +5.30 m.");
            Assert.AreEqual(SpringHigh, StillWaterLevels.WaterLevelAt(env, pond, 0.0, offPond),
                "outside the pond at spring high water, the tide's +2.2 m.");

            Assert.AreEqual(Bits(StillWaterLevels.WaterLevelAt(env, pond, 3600.0, pondCentre)),
                            Bits(StillWaterLevels.WaterLevelAt(env, pond, 3600.0, pondCentre)),
                "the same query twice must give the same bits.");
            Debug.Log("[StillWaterSeam] WaterLevelAt: the pond inside it, the tide outside it, at both springs.");
        }

        /// <summary>
        /// Subjects: <see cref="EmptyStillWater"/> and <see cref="GameServices.StillWater"/>. With nothing
        /// registered the service reads the empty still water — never null —, which is no water anywhere and
        /// an unbound map. <b>On the base</b> there is no such service.
        /// </summary>
        [Test]
        public void TheEmptyStillWater_IsNoWaterAnywhere_AndTheServiceIsNeverNull()
        {
            GameServices.Reset();
            Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater,
                "with nothing registered the service must read the empty still water, not null.");

            Vector2[] points =
            {
                Vector2.zero, new Vector2(-234.1f, 0f), new Vector2(1e6f, -1e6f),
                new Vector2(float.NaN, 3f), new Vector2(float.PositiveInfinity, float.NegativeInfinity),
            };
            foreach (Vector2 p in points)
                Assert.IsTrue(float.IsNegativeInfinity(EmptyStillWater.Instance.StillLevelAt(p)),
                    $"the empty still water must be none at {p}.");

            StillWaterMap map = EmptyStillWater.Instance.Map;
            Assert.IsFalse(map.IsBound, "the empty still water must hand the render no map.");
            Assert.IsNull(map.Texture, "the empty still water's map must carry no texture.");
            Assert.IsFalse(default(StillWaterMap).IsBound, "the default map must be unbound.");
            Debug.Log("[StillWaterSeam] the empty still water: none everywhere, no map; the service is never null.");
        }

        /// <summary>
        /// Subject: <see cref="GameServices.StillWater"/>'s slot. A registrant reads back as itself; a second
        /// registration replaces it, after which the first is no longer registered — so the first one's
        /// teardown, which asks <see cref="GameServices.IsRegisteredStillWater"/>, cannot clear the second;
        /// clearing reads the empty still water again. <see cref="GameServices.StillWaterChanged"/> is raised
        /// once per change and never for a self-assignment (the render republishes on it).
        /// </summary>
        [Test]
        public void TheServiceSlot_ReadsItsRegistrant_AndOnlyTheRegistrantIsRegistered()
        {
            GameServices.Reset();
            int raised = 0;
            Action onChanged = () => raised++;
            GameServices.StillWaterChanged += onChanged;
            try
            {
                var first = new PondDouble(Vector2.zero, 5f, BogPond);
                var second = new PondDouble(Vector2.zero, 5f, 5.25f);

                Assert.IsFalse(GameServices.IsRegisteredStillWater(EmptyStillWater.Instance),
                    "the empty stand-in is not a registration.");
                Assert.IsFalse(GameServices.IsRegisteredStillWater(null), "null is never registered.");

                GameServices.StillWater = first;
                Assert.AreSame(first, GameServices.StillWater, "the service must read its registrant.");
                Assert.IsTrue(GameServices.IsRegisteredStillWater(first), "the registrant must be registered.");
                Assert.AreEqual(1, raised, "registering must raise StillWaterChanged once.");

                GameServices.StillWater = first;
                Assert.AreEqual(1, raised, "re-assigning the same still water must not raise again.");

                GameServices.StillWater = second;
                Assert.AreSame(second, GameServices.StillWater, "a second registration must replace the first.");
                Assert.IsFalse(GameServices.IsRegisteredStillWater(first),
                    "once replaced, the first must no longer be registered, so its teardown cannot clear the second.");
                Assert.IsTrue(GameServices.IsRegisteredStillWater(second), "the second must be registered.");
                Assert.AreEqual(2, raised, "the replacement must raise once.");

                GameServices.StillWater = null;
                Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater,
                    "cleared, the service must read the empty still water.");
                Assert.IsFalse(GameServices.IsRegisteredStillWater(second), "cleared, nothing is registered.");
                Assert.AreEqual(3, raised, "clearing must raise once.");
            }
            finally
            {
                GameServices.StillWaterChanged -= onChanged;
            }
            Debug.Log("[StillWaterSeam] the service slot: reads its registrant; one raise per change.");
        }

        /// <summary>
        /// Subjects: <see cref="StillWaterLevels.EncodeCode"/> and <see cref="StillWaterLevels.DecodeCode01"/>,
        /// the map's code. Code 0 is "none" and a real level is never code 0; the Bog Pond encodes to the code
        /// worked by hand (55407); the top of the scale is 65535 and above it clamps; and every level on the
        /// scale decodes back within half a code (0.000084 m).
        /// </summary>
        [Test]
        public void TheMapCode_ZeroIsNone_AndALevelComesBackWithinHalfACode()
        {
            Assert.AreEqual(0, StillWaterLevels.EncodeCode(StillWaterLevels.None, MapMin, MapMax), "none → code 0.");
            Assert.AreEqual(0, StillWaterLevels.EncodeCode(float.NaN, MapMin, MapMax), "NaN → code 0.");
            Assert.AreEqual(0, StillWaterLevels.EncodeCode(MapMin, MapMin, MapMax),
                "a level at the scale's floor (−4 m) is no still water: code 0.");
            Assert.AreEqual(0, StillWaterLevels.EncodeCode(-5f, MapMin, MapMax), "below the floor → code 0.");
            Assert.AreEqual(1, StillWaterLevels.EncodeCode(-3.99999f, MapMin, MapMax),
                "a real level just above the floor rounds to code 0 but must be stored as code 1, never as none.");
            Assert.AreEqual(BogPondCode, StillWaterLevels.EncodeCode(BogPond, MapMin, MapMax),
                "the Bog Pond (+5.30 m) on −4 … +7 m: (9.30 / 11) × 65535 = 55406.86 → 55407.");
            Assert.AreEqual(65535, StillWaterLevels.EncodeCode(MapMax, MapMin, MapMax), "the top of the scale → 65535.");
            Assert.AreEqual(65535, StillWaterLevels.EncodeCode(9f, MapMin, MapMax), "above the top → clamped to 65535.");

            Assert.IsTrue(float.IsNegativeInfinity(StillWaterLevels.DecodeCode01(0f, MapMin, MapMax)),
                "code 0 must decode to none.");
            Assert.AreEqual(MapMax, StillWaterLevels.DecodeCode01(1f, MapMin, MapMax), "code 65535 → +7 m.");
            Assert.AreEqual(1.5f, StillWaterLevels.DecodeCode01(0.5f, MapMin, MapMax), 1e-6f,
                "half the scale → −4 + 5.5 = +1.5 m.");
            Assert.AreEqual(BogPond, StillWaterLevels.DecodeCode01(BogPondCode / 65535f, MapMin, MapMax),
                0.5f * OneCode, "code 55407 → the Bog Pond, within half a code.");

            int levels = 0, bad = 0;
            float worst = 0f;
            for (float level = -3.9f; level <= MapMax; level += 0.0137f)
            {
                levels++;
                ushort code = StillWaterLevels.EncodeCode(level, MapMin, MapMax);
                float back = StillWaterLevels.DecodeCode01(code / 65535f, MapMin, MapMax);
                float err = Mathf.Abs(back - level);
                if (code == 0 || !(err <= 0.5f * OneCode + 4e-6f)) bad++;
                if (err > worst) worst = err;
            }
            Assert.AreEqual(0, bad, $"{bad} of {levels} levels on −3.9 … +7 m did not come back within half a " +
                                    $"code ({0.5f * OneCode:0.000000} m); the worst was {worst:0.000000} m.");
            Debug.Log($"[StillWaterSeam] the map code: {levels} levels round-trip, worst {worst:0.0000000} m " +
                      $"(half a code is {0.5f * OneCode:0.0000000} m).");
        }

        /// <summary>
        /// Subject: <see cref="PaintedStillWater"/> over an R16 map. A 4 × 4 map, 2 m texels, centred on the
        /// origin; the Bog Pond's code fills the middle 2 × 2 block and the top-right corner texel; every
        /// other texel is 0. By hand: at a pond texel's centre the level is +5.30 m; at a none texel's centre
        /// it is none; <b>halfway from a pond texel to a none texel the render's bilinear gives half the pond's
        /// code, which decodes to (5.30 + (−4)) / 2 = +0.65 m</b> — the codes are filtered, then decoded, as
        /// the shaders do (decoding first would average +5.30 with −∞ and give −∞; nearest-texel would give
        /// +5.30 or none); off the map's rectangle there is no still water, even beside a pond texel on its
        /// edge; a NaN position is none; and two decodes of one texture agree to the bit.
        /// </summary>
        [Test]
        public void APaintedStillWater_FiltersItsCodesThenDecodes_AsTheRenderDoes()
        {
            Texture2D tex = MakeR16Map(4, 4, PondBlockCodes());
            var still = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);

            Assert.IsTrue(still.IsBound, "a readable R16 map must bind.");
            StillWaterMap map = still.Map;
            Assert.IsTrue(map.IsBound, "the render must be handed the map.");
            Assert.AreSame(tex, map.Texture, "the render must sample the SAME texture the sim decoded.");
            Assert.AreEqual(new Vector2(-4f, -4f), map.WorldMin, "the map's rectangle starts at centre − size / 2.");
            Assert.AreEqual(new Vector2(8f, 8f), map.WorldSize, "the map's rectangle is its world size.");
            Assert.AreEqual(MapMin, map.MinLevel, "the map's scale floor.");
            Assert.AreEqual(MapMax, map.MaxLevel, "the map's scale top.");

            // Texel (i, j) has its centre at (−3 + 2i, −3 + 2j).
            Assert.AreEqual(BogPond, still.StillLevelAt(new Vector2(-1f, -1f)), 0.5f * OneCode + 1e-6f,
                "at pond texel (1, 1)'s centre the level is the Bog Pond's.");
            Assert.AreEqual(BogPond, still.StillLevelAt(Vector2.zero), 0.5f * OneCode + 1e-6f,
                "at the middle of the pond block the level is the Bog Pond's.");
            Assert.IsTrue(float.IsNegativeInfinity(still.StillLevelAt(new Vector2(3f, -3f))),
                "at none texel (3, 0)'s centre there is no still water.");
            Assert.AreEqual(0.65f, still.StillLevelAt(new Vector2(2f, -1f)), OneCode,
                "halfway from pond texel (2, 1) to none texel (3, 1): half the pond's code, decoded, is +0.65 m.");

            Assert.AreEqual(BogPond, still.StillLevelAt(new Vector2(3f, 3f)), 0.5f * OneCode + 1e-6f,
                "the corner texel (3, 3) is a pond texel.");
            Assert.AreEqual(BogPond, still.StillLevelAt(new Vector2(4f, 3f)), 0.5f * OneCode + 1e-6f,
                "on the map's right edge beside it, still the pond.");
            Assert.IsTrue(float.IsNegativeInfinity(still.StillLevelAt(new Vector2(4.01f, 3f))),
                "1 cm off the map's right edge there is no still water, though the edge texel is a pond.");
            Assert.IsTrue(float.IsNegativeInfinity(still.StillLevelAt(new Vector2(3f, 40f))),
                "36 m above the map there is no still water.");
            Assert.IsTrue(float.IsNegativeInfinity(still.StillLevelAt(new Vector2(float.NaN, 0f))),
                "a NaN position has no still water.");

            var again = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);
            int differ = 0;
            for (float x = -4.5f; x <= 4.5f; x += 0.37f)
            for (float y = -4.5f; y <= 4.5f; y += 0.41f)
            {
                var p = new Vector2(x, y);
                if (Bits(still.StillLevelAt(p)) != Bits(again.StillLevelAt(p))) differ++;
            }
            Assert.AreEqual(0, differ, "two decodes of one texture must agree to the bit.");
            Debug.Log("[StillWaterSeam] the painted still water: pond +5.30 m, rim +0.65 m, none off the map.");
        }

        /// <summary>
        /// Subject: <see cref="PaintedStillWater"/> with nothing it can read. A null texture, or a texture
        /// the CPU cannot read, is no still water anywhere AND an unbound map: the render must not draw a
        /// pond the sim cannot wade.
        /// </summary>
        [Test]
        public void APaintedStillWater_WithNoReadableMap_IsNoneAndHandsTheRenderNothing()
        {
            var none = new PaintedStillWater(null, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);
            Assert.IsFalse(none.IsBound, "no texture: not bound.");
            Assert.IsFalse(none.Map.IsBound, "no texture: no map for the render.");
            Assert.IsTrue(float.IsNegativeInfinity(none.StillLevelAt(Vector2.zero)), "no texture: none.");

            Texture2D tex = MakeR16Map(4, 4, PondBlockCodes());
            tex.Apply(false, true);   // drop the CPU copy
            Assert.IsFalse(tex.isReadable, "premise: the texture is no longer CPU-readable.");
            var unreadable = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);
            Assert.IsFalse(unreadable.IsBound, "an unreadable texture: not bound.");
            Assert.IsFalse(unreadable.Map.IsBound, "an unreadable texture: no map for the render.");
            Assert.IsTrue(float.IsNegativeInfinity(unreadable.StillLevelAt(Vector2.zero)),
                "an unreadable texture: none, even in the middle of its pond block.");
            Debug.Log("[StillWaterSeam] an unreadable or absent still map: none, and no map for the render.");
        }

        /// <summary>
        /// Subject: <see cref="PaintedHeightMap.StillWater"/>. A height map with a still-level texture decodes
        /// it on the HEIGHT MAP's own rectangle and scale: centred at (10, 20), 8 m square, −4 … +7 m, the pond
        /// block reads +5.30 m at the map's centre and nothing off its rectangle. The same code on the default
        /// −4 … +6 m scale would read −4 + 10 × 0.845 = +4.45 m, so the scale is pinned. A height map without
        /// a still-level texture has no still water (null), and the instance is stable between reads — the
        /// terrain's teardown clears the service only if it still holds THIS instance.
        /// </summary>
        [Test]
        public void AHeightMap_DecodesItsStillWater_OnItsOwnRectangleAndScale()
        {
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
            _made.Add(map);
            Texture2D tex = MakeR16Map(4, 4, PondBlockCodes());
            var so = new SerializedObject(map);
            so.FindProperty("_worldCenter").vector2Value = new Vector2(10f, 20f);
            so.FindProperty("_worldSize").vector2Value = new Vector2(8f, 8f);
            so.FindProperty("_minElevation").floatValue = MapMin;
            so.FindProperty("_maxElevation").floatValue = MapMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.Rebuild();
            Assert.IsNull(map.StillWater, "a height map with no still-level texture has no still water.");

            so.Update();
            so.FindProperty("_stillLevelTexture").objectReferenceValue = tex;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.Rebuild();

            PaintedStillWater still = map.StillWater;
            Assert.IsNotNull(still, "a height map with a readable still-level texture must have still water.");
            Assert.AreSame(still, map.StillWater, "the still water must be one instance between reads.");
            Assert.AreSame(tex, map.StillLevelTexture, "the map must expose the texture it was given.");
            Assert.AreEqual(new Vector2(6f, 16f), still.Map.WorldMin, "the still map sits on the height map's rectangle.");
            Assert.AreEqual(new Vector2(8f, 8f), still.Map.WorldSize, "the still map is the height map's size.");
            Assert.AreEqual(MapMin, still.Map.MinLevel, "the still map's floor is the height map's.");
            Assert.AreEqual(MapMax, still.Map.MaxLevel, "the still map's top is the height map's.");
            Assert.AreEqual(BogPond, still.StillLevelAt(new Vector2(10f, 20f)), 0.5f * OneCode + 1e-6f,
                "the pond block at the map's centre reads the Bog Pond's +5.30 m on −4 … +7 m.");
            Assert.IsTrue(float.IsNegativeInfinity(still.StillLevelAt(Vector2.zero)),
                "the origin is off this map's rectangle: no still water.");
            Debug.Log("[StillWaterSeam] a height map's still water: its own rectangle and scale; one instance.");
        }

        // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The 4 × 4 code grid: the Bog Pond in the middle 2 × 2 block and the top-right corner
        /// texel, 0 elsewhere. Row-major, row 0 at the bottom.</summary>
        static ushort[] PondBlockCodes()
        {
            var codes = new ushort[16];
            codes[1 * 4 + 1] = BogPondCode;
            codes[1 * 4 + 2] = BogPondCode;
            codes[2 * 4 + 1] = BogPondCode;
            codes[2 * 4 + 2] = BogPondCode;
            codes[3 * 4 + 3] = BogPondCode;
            return codes;
        }

        Texture2D MakeR16Map(int width, int height, ushort[] codes)
        {
            var tex = new Texture2D(width, height, TextureFormat.R16, false, true)
            {
                name = "StillWaterSeamTests map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _made.Add(tex);
            tex.SetPixelData(codes, 0);
            tex.Apply(false, false);
            Assert.AreEqual(TextureFormat.R16, tex.format, "premise: the still map is R16.");
            return tex;
        }

        static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        /// <summary>One round pond at a fixed level; no still water elsewhere.</summary>
        private sealed class PondDouble : IStillWater
        {
            private readonly Vector2 _centre;
            private readonly float _radius;
            private readonly float _level;

            public PondDouble(Vector2 centre, float radius, float level)
            {
                _centre = centre;
                _radius = radius;
                _level = level;
            }

            public float StillLevelAt(Vector2 worldPos)
                => (worldPos - _centre).sqrMagnitude <= _radius * _radius ? _level : StillWaterLevels.None;

            public StillWaterMap Map => default;
        }

        /// <summary>A tide that stands still at <see cref="Level"/>.</summary>
        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }
    }
}
