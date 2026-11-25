using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Glowing glass as DATA. Pinned here: the shipped asset is the built-in defaults; the look changes
    /// only how a lamp is drawn, never its size, colour or when it shows; signal lamps keep their colour;
    /// switching the look off stamps exactly what was stamped before; the worn headlamp is untouched; a
    /// window glow stops painting its disc; and the cabin lamp's breath is a pure function.
    /// </summary>
    public class GlassLookTests
    {
        const float Eps = 1e-5f;

        static readonly HullLampKind[] NavKinds =
        {
            HullLampKind.PortSidelight, HullLampKind.StarboardSidelight, HullLampKind.SternLight,
            HullLampKind.Masthead, HullLampKind.AnchorLight, HullLampKind.RangeLight,
        };

        GlassLook _override;

        [SetUp]
        public void SetUp()
        {
            GlassLook.Current = null;   // the shipped asset
            BoatLampLook.Current = null;
        }

        [TearDown]
        public void TearDown()
        {
            GlassLook.Current = null;
            BoatLampLook.Current = null;
            if (_override != null) Object.DestroyImmediate(_override);
            _override = null;
        }

        GlassLook Override(System.Action<SerializedObject> edit)
        {
            if (_override != null) Object.DestroyImmediate(_override);
            _override = GlassLook.CreateDefault();
            var so = new SerializedObject(_override);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            GlassLook.Current = _override;
            return _override;
        }

        static SceneLight NewLight(string name) =>
            new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }.AddComponent<SceneLight>();

        [Test]
        public void TheShippedAssetExistsAndIsTheBuiltInDefaults()
        {
            var shipped = Resources.Load<GlassLook>(GlassLook.ResourcePath);
            Assert.IsNotNull(shipped, "Resources/GlassLook.asset is missing: the owner has nothing to tune");
            GlassLook builtIn = GlassLook.CreateDefault();
            try
            {
                Assert.AreEqual(JsonUtility.ToJson(builtIn, true), JsonUtility.ToJson(shipped, true),
                    "the asset and the code defaults have drifted. If the look was retuned on purpose, " +
                    "move the field defaults in GlassLook.cs to match the asset");
            }
            finally { Object.DestroyImmediate(builtIn); }
        }

        [Test]
        public void TheLookInForceIsTheShippedAsset()
        {
            Assert.AreSame(Resources.Load<GlassLook>(GlassLook.ResourcePath), GlassLook.Current);
        }

        [Test]
        public void ANavigationLampIsGlass_AndKeepsItsSizeColourAndBrightness()
        {
            foreach (HullLampKind k in NavKinds)
            {
                SceneLight light = NewLight("glass-" + k);
                try
                {
                    LightPresets.Config c = BoatLampPresets.For(k);
                    BoatLampPresets.Apply(light, k);
                    Assert.IsTrue(light.HasGlass, $"{k}: the shipped look draws navigation lamps as glass");
                    Assert.AreEqual(c.Range, light.Range, Eps, $"{k}: glass must not grow the bloom (the sidelight gap)");
                    Assert.AreEqual(c.Color, light.Color, $"{k}: glass must not change what colour the lamp signals");
                    Assert.AreEqual(c.Intensity, light.Intensity, Eps, $"{k}: glass must not change how bright it is");
                    Assert.Less(light.Glass.CoreFraction + light.Glass.RimPixels / GlassLook.Current.PixelsPerMetre / light.Range,
                                1f, $"{k}: the glass must fit inside its own bloom");
                    Assert.IsTrue(light.Streak.IsOn, $"{k}: a lamp over the sea throws a streak on it");
                }
                finally { Object.DestroyImmediate(light.gameObject); }
            }
        }

        [Test]
        public void ASignalLampsCoreStaysItsOwnColour()
        {
            GlassLook look = GlassLook.Current;
            Assert.Less(look.NavLens.ColouredWhiten, 0.5f, "a white core is not a red lamp");
            Assert.Greater(look.NavLens.Whiten, look.NavLens.ColouredWhiten, "white lamps whiter than signal lamps");
        }

        [Test]
        public void SwitchedOff_EveryLampIsStampedExactlyAsBefore()
        {
            Override(so => so.FindProperty("_enabled").boolValue = false);
            SceneLight light = NewLight("glass-off");
            try
            {
                foreach (HullLampKind k in NavKinds)
                {
                    BoatLampPresets.Apply(light, k);
                    Assert.IsFalse(light.HasGlass, $"{k}: off means no glass");
                    Assert.IsFalse(light.Streak.IsOn, $"{k}: off means no streak");
                }
                LightPresets.Apply(light, LightPresets.Kind.WindowGlow);
                Assert.IsTrue(light.DrawsBloom, "off means the window glow paints as it did");
                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.IsFalse(light.HasGlass);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void TheHeadlampIsNotPartOfThisLook()
        {
            SceneLight light = NewLight("glass-headlamp");
            try
            {
                LightPresets.Apply(light, LightPresets.Kind.Headlamp);
                Assert.IsFalse(light.HasGlass);
                Assert.IsFalse(light.Streak.IsOn);
                Assert.IsTrue(light.DrawsBloom);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void AWharfLampIsGlass_AndAWindowGlowLightsTheGroundNotADisc()
        {
            SceneLight light = NewLight("glass-shore");
            try
            {
                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.IsTrue(light.HasGlass, "a lamp post's lens is glass");
                Assert.AreEqual(0f, light.CoreRadiusMetres, Eps, "glass is its own look, not the boat lamp's core");
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Lightpost), light.ReachMetres, Eps,
                                "its pool on the planks is unchanged");
                Assert.IsFalse(light.Streak.IsOn,
                               "a wharf's planks stand over water on the seabed map: no shore streak until a deck mask exists");

                LightPresets.Apply(light, LightPresets.Kind.WindowGlow);
                Assert.IsFalse(light.HasGlass, "a window glow is not a lamp");
                Assert.IsFalse(light.DrawsBloom, "no disc over the wall: 'nothing blobby'");
                Assert.Greater(light.ReachMetres, 0f, "its light still lands on the ground as a pool");
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void ThePassthroughIsStillTheSmoothGlow()
        {
            SceneLight light = NewLight("glass-legacy");
            try
            {
                BoatLampPresets.Apply(light, HullLampKind.Masthead);
                Assert.IsTrue(light.HasGlass);
                BoatLampPresets.ApplyLegacy(light, HullLampKind.Masthead);
                Assert.IsFalse(light.HasGlass, "yesterday's lamp had no glass");
                Assert.IsFalse(light.Streak.IsOn);
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        [Test]
        public void TheCabinLampBreathIsPureAndSmall()
        {
            Assert.AreEqual(1f, GlassLook.Current.CabinBreath(99, 3.3), Eps,
                            "the shipped look keeps the cabin lamp steady (the breath is the owner's switch)");
            GlassLook look = Override(so => so.FindProperty("_panes.Breath").floatValue = 0.06f);
            float a = look.CabinBreath(1234, 100.25), b = look.CabinBreath(1234, 100.25);
            Assert.AreEqual(a, b, "the same seed and hour must give the same breath (rule 5)");
            float lowest = 1f;
            for (int i = 0; i < 200; i++)
            {
                float f = look.CabinBreath(99, i * 0.37);
                lowest = Mathf.Min(lowest, f);
                Assert.That(f, Is.InRange(1f - look.Panes.Breath - Eps, 1f + Eps),
                            "a breath, not a flicker: never brighter than the steady lamp, never dimmer than its amount");
            }
            Assert.Less(lowest, 1f, "switched on, the lamp does breathe");

            Override(so => so.FindProperty("_enabled").boolValue = false);
            Assert.AreEqual(1f, GlassLook.Current.CabinBreath(99, 3.3), Eps, "off is a steady lamp");
        }

        [Test]
        public void ThePaneGradientIsBrighterLow()
        {
            GlassLook.PaneLook p = GlassLook.Current.Panes;
            Assert.Greater(p.SillLevel, p.HeadLevel, "the lamp inside stands low: the sill is brighter than the head");
            Assert.Greater(p.HeadLevel, 0f, "the head of a lit pane is still lit");
        }
    }
}
