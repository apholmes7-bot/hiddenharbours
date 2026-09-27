using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// A region's terrain registers its still water with the seam (ADR 0046), through the real lifecycle —
    /// <see cref="PaintedTidalTerrain"/>'s enable and disable, which only play mode runs. The still water
    /// goes with the terrain: the terrain that enabled last owns both slots, a region with no still map
    /// registers none (so it never inherits another region's pond), and a disable clears only the terrain's
    /// own registration. The walker's live reads are checked at each step, because they are what the
    /// registration is for.
    ///
    /// <para><b>Fixture.</b> Each region is a 4 × 4 map on an 8 × 8 m rectangle, every texel one code, so
    /// the expected depths are exact hand arithmetic on the codes: on the −4 … +7 m range one 8-bit code is
    /// 11/255 m, the bed is code 201 (+4.6706 m) and the pond code 216 (+5.3176 m), 15 codes = 0.6471 m of
    /// water — deeper than the wade limit (0.5), so swum. The format is immaterial to registration; the
    /// still map's 16-bit precision is guarded in <c>StillWaterSeamTests</c> and
    /// <c>PaintedHeightMapR16Tests</c>.</para>
    ///
    /// <para><b>On the base</b> the terrain registers no still water and the walker's water is the tide:
    /// at the highest tide (+2.2 m) the pond reads 2.2 − 4.6706 = −2.47 m, Dry, where each test here asserts
    /// 0.6471 m and Swim (and <see cref="GameServices.StillWater"/> does not exist to compile against).</para>
    /// </summary>
    public sealed class StillWaterRegistrationPlayTests
    {
        private const float MapMin = -4f, MapMax = 7f;
        private const byte BedCode = 201, PondCode = 216, BrookCode = 204;
        private const float Bed = -4f + 11f * BedCode / 255f;          // +4.6706 m
        private const float PondDepth = 11f * (PondCode - BedCode) / 255f;   // 15 codes = 0.6471 m: swum
        private const float BrookDepth = 11f * (BrookCode - BedCode) / 255f; //  3 codes = 0.1294 m: waded
        private const float WadeDepth = 0.5f, SwimLimit = 2.0f;
        private const float Tol = 1e-4f;
        private static readonly float[] Tides = { -2.2f, 0f, 0.88f, 2.2f };

        private static readonly Vector2 PondRegion = new Vector2(40f, 12f);
        private static readonly Vector2 BrookRegion = new Vector2(140f, 12f);
        private static readonly Vector2 DryRegion = new Vector2(240f, 12f);
        private static readonly Vector2 OffEveryMap = new Vector2(40f, 40f);   // 28 m north of the pond

        private readonly List<Object> _made = new List<Object>();
        private FlatEnv _sea;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            _sea = new FlatEnv { Level = 0.88f };
            GameServices.Environment = _sea;   // the clock stays unset: t = 0
        }

        [TearDown]
        public void TearDown()
        {
            // Newest first: each terrain's disable runs while its map is still alive.
            for (int i = _made.Count - 1; i >= 0; i--) if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
            StandableSurfaces.Clear();
            GameServices.Reset();
        }

        [Test]
        public void ARegionsTerrain_RegistersItsStillWater_AndTheWalkerSwimsIt_UntilTheTerrainGoes()
        {
            PaintedHeightMap map = Map("pond", PondRegion, PondCode);
            GameObject go = EnableRegion("~terrain pond", map);
            var terrain = go.GetComponent<PaintedTidalTerrain>();

            Assert.AreSame(terrain, GameServices.TidalTerrain, "premise: the terrain registered itself");
            Assert.IsNotNull(map.StillWater, "premise: the map decoded its still water");
            Assert.AreSame(map.StillWater, GameServices.StillWater,
                "the terrain registers its map's still water — the one instance the walker and the render read");
            Assert.IsTrue(GameServices.StillWater.Map.IsBound, "and the render has the map's texture");

            foreach (float tide in Tides)
            {
                _sea.Level = tide;
                float depth = TidalWalkability.DepthNow(PondRegion);
                Assert.AreEqual(PondDepth, depth, Tol, $"tide {tide:+0.00;-0.00}: the pond is its own depth");
                Assert.AreEqual(DepthBand.Swim, TidalExposure.BandForDepth(depth, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the pond is swum");
                Assert.IsFalse(TidalWalkability.IsWalkableNow(PondRegion),
                    $"tide {tide:+0.00;-0.00}: the pond's bed is not bared ground");
                Assert.AreEqual(tide - Bed, TidalWalkability.DepthNow(OffEveryMap), Tol,
                    $"tide {tide:+0.00;-0.00}: off the map's rectangle there is no still water, only the tide");
            }

            go.SetActive(false);
            Assert.IsNull(GameServices.TidalTerrain, "premise: the terrain's disable cleared its terrain slot");
            Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater,
                "and took its still water with it: no pond outlives its region");
            Assert.IsTrue(float.IsNegativeInfinity(TidalWalkability.DepthNow(PondRegion)),
                "with no terrain the walker is on the absent-service answer, as before the seam");
        }

        [Test]
        public void TheLastTerrainToEnable_OwnsTheStillWater_AndADisableClearsOnlyItsOwn()
        {
            PaintedHeightMap pond = Map("pond", PondRegion, PondCode);
            PaintedHeightMap brook = Map("brook", BrookRegion, BrookCode);
            GameObject pondGo = EnableRegion("~terrain pond", pond);
            GameObject brookGo = EnableRegion("~terrain brook", brook);

            Assert.AreSame(brookGo.GetComponent<PaintedTidalTerrain>(), GameServices.TidalTerrain,
                "premise: the last terrain to enable owns the terrain slot (the base's rule)");
            Assert.AreSame(brook.StillWater, GameServices.StillWater, "and the still water goes with it");
            Assert.AreEqual(BrookDepth, TidalWalkability.DepthNow(BrookRegion), Tol, "the brook is its own depth");
            Assert.AreEqual(DepthBand.Wade,
                TidalExposure.BandForDepth(TidalWalkability.DepthNow(BrookRegion), WadeDepth, SwimLimit),
                "and waded");

            pondGo.SetActive(false);
            Assert.AreSame(brook.StillWater, GameServices.StillWater,
                "the pond region's disable must not clear the brook region's still water");
            Assert.AreEqual(BrookDepth, TidalWalkability.DepthNow(BrookRegion), Tol, "the brook is still waded");

            brookGo.SetActive(false);
            Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater, "the owner's disable clears it");

            pondGo.SetActive(true);
            Assert.AreSame(pond.StillWater, GameServices.StillWater, "a region entered again registers again");
            Assert.AreEqual(PondDepth, TidalWalkability.DepthNow(PondRegion), Tol, "and its pond is swum again");
        }

        [Test]
        public void ARegionWithNoStillMap_RegistersNone_SoItNeverInheritsAnotherRegionsPond()
        {
            PaintedHeightMap pond = Map("pond", PondRegion, PondCode);
            PaintedHeightMap dry = Map("dry", DryRegion, 0);   // no still-level texture: every committed map today
            EnableRegion("~terrain pond", pond);
            Assert.AreEqual(PondDepth, TidalWalkability.DepthNow(PondRegion), Tol, "premise: the pond is swum");

            GameObject dryGo = EnableRegion("~terrain dry", dry);
            Assert.IsNull(dry.StillWater, "a map with no still-level texture has no still water");
            Assert.AreSame(dryGo.GetComponent<PaintedTidalTerrain>(), GameServices.TidalTerrain, "premise");
            Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater,
                "a region with no still map registers none — none is registered too, or it would inherit the pond");
            Assert.AreEqual(_sea.Level - Bed, TidalWalkability.DepthNow(PondRegion), Tol,
                "under the dry region's terrain the walker reads the tide alone, as every region plays today");

            dryGo.SetActive(false);
            Assert.AreSame(EmptyStillWater.Instance, GameServices.StillWater,
                "its disable leaves no still water behind (it registered none)");
        }

        // ---- fixtures -----------------------------------------------------------------------------------

        private GameObject EnableRegion(string name, PaintedHeightMap map)
        {
            var go = new GameObject(name);
            _made.Add(go);
            go.SetActive(false);
            go.AddComponent<PaintedTidalTerrain>().Map = map;
            go.SetActive(true);   // OnEnable: decode, register
            return go;
        }

        /// <summary>A region's map: an 8 × 8 m rectangle at <paramref name="centre"/>, bed code 201 throughout,
        /// and a still map of <paramref name="stillCode"/> throughout (0 = no still-level texture at all).</summary>
        private PaintedHeightMap Map(string name, Vector2 centre, byte stillCode)
        {
#if UNITY_EDITOR
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
            map.name = "~map " + name;
            _made.Add(map);
            var so = new SerializedObject(map);
            so.FindProperty("_heightTexture").objectReferenceValue = Uniform(name + " height", BedCode);
            so.FindProperty("_stillLevelTexture").objectReferenceValue =
                stillCode > 0 ? Uniform(name + " still", stillCode) : null;
            so.FindProperty("_worldCenter").vector2Value = centre;
            so.FindProperty("_worldSize").vector2Value = new Vector2(8f, 8f);
            so.FindProperty("_minElevation").floatValue = MapMin;
            so.FindProperty("_maxElevation").floatValue = MapMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            return map;
#else
            Assert.Ignore("SKIPPED, NOT VERIFIED — a PaintedHeightMap is configured through the editor's SerializedObject.");
            return null;
#endif
        }

        private Texture2D Uniform(string name, byte code)
        {
            var tex = new Texture2D(4, 4, TextureFormat.R8, false, true)
            {
                name = "~" + name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _made.Add(tex);
            var codes = new byte[16];
            for (int i = 0; i < codes.Length; i++) codes[i] = code;
            tex.SetPixelData(codes, 0);
            tex.Apply(false, false);   // stays CPU-readable: the sim decodes it
            return tex;
        }

        /// <summary>An environment whose tide is one level at every time.</summary>
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
