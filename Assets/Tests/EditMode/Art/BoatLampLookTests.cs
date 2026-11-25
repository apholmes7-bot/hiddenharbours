using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The boat lamp look as DATA (owner, 2026-11-13: "boat lights still look off"). Three things are
    /// pinned here: the shipped asset is the built-in defaults, the presets really read the data, and
    /// the two new pictures hold: a navigation lamp is a hard core in a faint banded halo, and a
    /// cabin wall lights the deck through the pool pass instead of painting a wedge over it.
    /// </summary>
    public class BoatLampLookTests
    {
        const float Eps = 1e-5f;

        static readonly HullLampKind[] NavKinds =
        {
            HullLampKind.PortSidelight, HullLampKind.StarboardSidelight, HullLampKind.SternLight,
            HullLampKind.Masthead, HullLampKind.AnchorLight, HullLampKind.RangeLight,
        };

        BoatLampLook _override;

        [SetUp]
        public void SetUp() => BoatLampLook.Current = null;   // the shipped asset

        [TearDown]
        public void TearDown()
        {
            BoatLampLook.Current = null;
            if (_override != null) Object.DestroyImmediate(_override);
            _override = null;
        }

        /// <summary>An in-memory look with some fields changed, put in force for this test.</summary>
        BoatLampLook Override(System.Action<SerializedObject> edit)
        {
            _override = BoatLampLook.CreateDefault();
            var so = new SerializedObject(_override);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            BoatLampLook.Current = _override;
            return _override;
        }

        static SceneLight NewLight(string name) =>
            new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }.AddComponent<SceneLight>();

        // ---- the data --------------------------------------------------------------------------------

        [Test]
        public void TheShippedAssetExistsAndIsTheBuiltInDefaults()
        {
            var shipped = Resources.Load<BoatLampLook>(BoatLampLook.ResourcePath);
            Assert.IsNotNull(shipped, "Resources/BoatLampLook.asset is missing: the owner has nothing to tune");

            BoatLampLook builtIn = BoatLampLook.CreateDefault();
            try
            {
                Assert.AreEqual(JsonUtility.ToJson(builtIn, true), JsonUtility.ToJson(shipped, true),
                    "the asset and the code defaults have drifted. If the look was retuned on " +
                    "purpose, move the field defaults in BoatLampLook.cs to match the asset");
            }
            finally { Object.DestroyImmediate(builtIn); }
        }

        [Test]
        public void TheLookInForceIsTheShippedAsset()
        {
            Assert.AreSame(Resources.Load<BoatLampLook>(BoatLampLook.ResourcePath), BoatLampLook.Current);
        }

        [Test]
        public void ThePresetsReadTheData_NotConstants()
        {
            float before = BoatLampPresets.For(HullLampKind.Masthead).Range;
            Override(so =>
            {
                so.FindProperty("_masthead.HaloRadiusMetres").floatValue = before + 0.25f;
                so.FindProperty("_masthead.Intensity").floatValue = 0.7f;
                so.FindProperty("_spillIntensity").floatValue = 0.3f;
                so.FindProperty("_wallSpillWindowMultiple").floatValue = 2f;
                so.FindProperty("_wallSpillHalfAngleDeg").floatValue = 30f;
            });

            LightPresets.Config mast = BoatLampPresets.For(HullLampKind.Masthead);
            Assert.AreEqual(before + 0.25f, mast.Range, Eps, "the masthead's size is the asset's");
            Assert.AreEqual(0.7f, mast.Intensity, Eps);
            Assert.AreEqual(mast.Range, BoatLampPresets.For(HullLampKind.RangeLight).Range, Eps,
                            "the range light is still the same lamp as the masthead");
            Assert.AreEqual(0.3f, BoatLampPresets.For(HullLampKind.CabinGlow).Intensity, Eps);
            Assert.AreEqual(1f, BoatLampPresets.WallSpillThrow(0.5f), Eps, "the throw multiple is the asset's");
            Assert.AreEqual(30f, BoatLampPresets.WallSpillHalfAngleDeg, Eps);
        }

        [Test]
        public void ReadingTheLookTwiceGivesTheSameAnswer()
        {
            // Visual only, but still pure: the same data gives the same lamp every time (rule 5).
            foreach (HullLampKind k in NavKinds)
            {
                LightPresets.Config a = BoatLampPresets.For(k), b = BoatLampPresets.For(k);
                Assert.AreEqual(a.Range, b.Range);
                Assert.AreEqual(a.Intensity, b.Intensity);
                Assert.AreEqual(a.Color, b.Color);
            }
        }

        // ---- slice 1: a navigation lamp is a small hot core in a faint banded halo ------------------

        [Test]
        public void EveryNavigationLampHasACoreSmallerThanItsHalo_AndAFaintBandedHalo()
        {
            BoatLampLook look = BoatLampLook.Current;
            foreach (HullLampKind k in NavKinds)
            {
                NavLampLook l = look.For(k);
                Assert.Greater(l.CoreRadiusMetres, 0f, $"{k}: no core is the old smooth orb");
                Assert.Less(l.CoreRadiusMetres, l.HaloRadiusMetres * 0.5f,
                            $"{k}: the core is the small part of the lamp");
                Assert.GreaterOrEqual(l.BandSteps, 1, $"{k}: an unbanded halo is a gradient in a pixel frame");
                Assert.Less(l.HaloStrength, 0.5f, $"{k}: a halo this bright is a ball, not a lamp");
                Assert.Greater(l.HaloStrength, 0f, $"{k}: no halo at all is a dead pixel");
            }
        }

        [Test]
        public void EveryCoreAndHaloIsAWholeNumberOfArtPixels()
        {
            BoatLampLook look = BoatLampLook.Current;
            float ppm = look.SnapPixelsPerMetre;
            Assert.Greater(ppm, 0f, "the shipped look snaps to the pixel grid");
            foreach (HullLampKind k in NavKinds)
            {
                NavLampLook l = look.For(k);
                float corePx = l.CoreRadiusMetres * ppm, haloPx = l.HaloRadiusMetres * ppm;
                Assert.AreEqual(Mathf.Round(corePx), corePx, 1e-3f, $"{k} core radius is {corePx} px");
                Assert.AreEqual(Mathf.Round(haloPx), haloPx, 1e-3f, $"{k} halo radius is {haloPx} px");
                Assert.GreaterOrEqual(corePx, 1f, $"{k}: a core under one pixel does not draw");
            }
        }

        [Test]
        public void TheSidelightsKeepTheirColour_AndTheWhitesGoWhiter()
        {
            BoatLampLook look = BoatLampLook.Current;
            Assert.Less(look.For(HullLampKind.PortSidelight).CoreWhiten, 0.5f, "a white core is not a red lamp");
            Assert.Less(look.For(HullLampKind.StarboardSidelight).CoreWhiten, 0.5f);
            Assert.Greater(look.For(HullLampKind.Masthead).CoreWhiten,
                           look.For(HullLampKind.PortSidelight).CoreWhiten);
        }

        [Test]
        public void StampingANavigationLampWritesItsCoreBandsAndSnap()
        {
            SceneLight light = NewLight("nav-look");
            try
            {
                BoatLampPresets.Apply(light, HullLampKind.Masthead);
                NavLampLook l = BoatLampLook.Current.For(HullLampKind.Masthead);

                Assert.AreEqual(l.HaloRadiusMetres, light.Range, Eps, "the bloom is the halo");
                Assert.AreEqual(l.CoreRadiusMetres, light.CoreRadiusMetres, Eps);
                Assert.AreEqual(l.HaloStrength, light.HaloStrength, Eps);
                Assert.AreEqual(l.BandSteps, light.BandSteps);
                Assert.AreEqual(l.CoreWhiten, light.CoreWhiten, Eps);
                Assert.AreEqual(BoatLampLook.Current.SnapPixelsPerMetre, light.PixelsPerMetre, Eps);
                Assert.IsTrue(light.DrawsBloom, "a navigation lamp IS its bloom");
                Assert.AreEqual(0f, light.ReachMetres, Eps, "and it still lights no sea");
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void TheSnapSwitchTurnsTheSnapOff()
        {
            Override(so => so.FindProperty("_snapToPixelGrid").boolValue = false);
            SceneLight light = NewLight("nav-nosnap");
            try
            {
                BoatLampPresets.Apply(light, HullLampKind.SternLight);
                Assert.AreEqual(0f, light.PixelsPerMetre, Eps);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ACoreBiggerThanItsHaloIsClampedToIt()
        {
            Override(so => so.FindProperty("_sternLight.CoreRadiusMetres").floatValue = 9f);
            SceneLight light = NewLight("nav-clamp");
            try
            {
                BoatLampPresets.Apply(light, HullLampKind.SternLight);
                Assert.LessOrEqual(light.CoreRadiusMetres, light.Range + Eps);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ThePassthroughAndAPlainStampAreStillTheSmoothGlow()
        {
            SceneLight light = NewLight("nav-legacy");
            try
            {
                BoatLampPresets.Apply(light, HullLampKind.Masthead);
                BoatLampPresets.ApplyLegacy(light, HullLampKind.Masthead);
                Assert.AreEqual(0f, light.CoreRadiusMetres, Eps, "yesterday's lamp had no hard core");
                Assert.AreEqual(0, light.BandSteps);
                Assert.AreEqual(0f, light.PixelsPerMetre, Eps);
                Assert.IsTrue(light.DrawsBloom);

                BoatLampPresets.Apply(light, HullLampKind.Masthead);
                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.AreEqual(0f, light.CoreRadiusMetres, Eps,
                                "a placed lamp re-stamped over a boat lamp must not keep its core");
                Assert.AreEqual(0, light.BandSteps);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void TheSidelightPassthroughIsTheLookBeforeTheLampLook()
        {
            // Frozen record of the numbers the sidelights wore until 2026-11-13.
            foreach (HullLampKind k in new[] { HullLampKind.PortSidelight, HullLampKind.StarboardSidelight })
            {
                LightPresets.Config then = BoatLampPresets.Legacy(k);
                Assert.AreEqual(0.28f, then.Range, Eps);
                Assert.AreEqual(1.4f, then.Intensity, Eps);
                Assert.AreEqual(0.4f, then.EdgeSoftness, Eps);
            }
        }

        // ---- the shape itself, mirrored headless (LightMath.LampShape == the shader branch) --------

        [Test]
        public void TheLampShapeIsAFlatCoreThenFlatBandsThenNothing()
        {
            const float core = 0.3f, halo = 0.2f;
            const int bands = 2;

            Assert.AreEqual(1f, LightMath.LampShape(0f, core, halo, bands), Eps);
            Assert.AreEqual(1f, LightMath.LampShape(core, core, halo, bands), Eps, "flat right to its edge");

            // Just outside the core the halo is at full halo strength: a STEP down, not a slope.
            Assert.AreEqual(halo, LightMath.LampShape(core + 0.01f, core, halo, bands), Eps);
            // The outer band is half of it.
            Assert.AreEqual(halo * 0.5f, LightMath.LampShape(0.9f, core, halo, bands), Eps);
            Assert.AreEqual(0f, LightMath.LampShape(1f, core, halo, bands), Eps, "nothing at the throw");
            Assert.AreEqual(0f, LightMath.LampShape(1.5f, core, halo, bands), Eps, "nor past it");
        }

        [Test]
        public void TheLampShapeNeverRisesOutward_AndHasExactlyItsBands()
        {
            const float core = 0.3f, halo = 0.22f;
            const int bands = 3;
            var levels = new System.Collections.Generic.HashSet<int>();
            float prev = float.MaxValue;
            for (int i = 0; i <= 400; i++)
            {
                float d = i / 400f;
                float v = LightMath.LampShape(d, core, halo, bands);
                Assert.LessOrEqual(v, prev + Eps, $"the lamp got brighter going outward at d={d}");
                prev = v;
                if (d > core && v > 0f) levels.Add(Mathf.RoundToInt(v * 10000f));
            }
            Assert.AreEqual(bands, levels.Count, "the halo is that many flat bands and no gradient");
        }

        [Test]
        public void WithNoBandsTheHaloIsAPlainLinearFade()
        {
            Assert.AreEqual(0.5f, LightMath.LampShape(0.5f, 0f, 1f, 0), Eps);
            Assert.AreEqual(0.25f * 0.4f, LightMath.LampShape(0.75f, 0f, 0.4f, 0), Eps);
        }

        // ---- slice 2: the cabin's light lands on the deck -------------------------------------------

        [Test]
        public void AWallSpillPoolsTheDeckAndPaintsNoWedge()
        {
            SceneLight light = NewLight("spill-pool");
            try
            {
                BoatLampPresets.ApplyWallSpill(light, windowWidthMetres: 0.66f);

                Assert.IsFalse(light.DrawsBloom, "the orange wedge over the frame is gone");
                Assert.AreEqual(BoatLampPresets.WallSpillThrow(0.66f), light.ReachMetres, Eps,
                                "the pool reaches as far as the wedge used to");
                Assert.AreEqual(SceneLight.LightShape.Cone, light.Shape, "and it is still a wedge out of the wall");
                Assert.AreEqual(0f, light.CoreRadiusMetres, Eps, "a wall wash is not a lamp");

                // What BoatWindowGlow gives it every frame: the height of the glass.
                light.LampHeightMeters = 1.6f;
                Assert.IsTrue(LampPoolSystem.PoolsLight(light, 0.1f, true, out float gate),
                              "LampPoolSystem must take a boat's wall spill as a pool candidate");
                Assert.Greater(gate, 0f);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ASwitchedOffSpillCastsNoPool()
        {
            // BoatWindowGlow disables the light for a dark cabin and for a wall facing away.
            SceneLight light = NewLight("spill-off");
            try
            {
                BoatLampPresets.ApplyWallSpill(light, 0.66f);
                light.LampHeightMeters = 1.6f;
                light.enabled = false;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _));
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ThePaintedWedgeIsADataSwitch_AndSoIsThePool()
        {
            Override(so =>
            {
                so.FindProperty("_spillPaintsWedge").boolValue = true;
                so.FindProperty("_spillPoolsTheDeck").boolValue = false;
            });
            SceneLight light = NewLight("spill-wedge");
            try
            {
                BoatLampPresets.ApplyWallSpill(light, 0.66f);
                Assert.IsTrue(light.DrawsBloom, "the way back to the look before 2026-11-13");
                Assert.AreEqual(0f, light.ReachMetres, Eps);
                Assert.AreEqual(BoatLampPresets.WallSpillThrow(0.66f), light.Range, Eps);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ReStampingASpillIsIdempotent()
        {
            SceneLight light = NewLight("spill-twice");
            try
            {
                BoatLampPresets.ApplyWallSpill(light, 0.66f, 1.5f);
                float reach = light.ReachMetres, intensity = light.Intensity;
                BoatLampPresets.ApplyWallSpill(light, 0.66f, 1.5f);
                Assert.AreEqual(reach, light.ReachMetres, Eps);
                Assert.AreEqual(intensity, light.Intensity, Eps);
                Assert.IsFalse(light.DrawsBloom);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ACabinsPoolsFitInsideThePoolBudgetWithRoomForTheWharf()
        {
            // A wheelhouse shows at most two walls to this camera at once (three on a quartering
            // view of a hull glazed all round). The pool budget is shared with the pier lamps.
            var profile = Resources.Load<LampShadowProfile>("LampShadowProfile");
            Assert.IsNotNull(profile);
            Assert.GreaterOrEqual(profile.MaxPools, 6,
                "with fewer slots than this the player's own cabin would crowd the wharf lamps out");
        }
    }
}
