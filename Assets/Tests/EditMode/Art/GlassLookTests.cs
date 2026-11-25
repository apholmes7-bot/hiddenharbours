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

        // =========================================================================================
        //  OCCLUSION (owner, 2026-11-25: "objects shouldnt glow through objects they normally wouldnt")
        // =========================================================================================

        [Test]
        public void ANavigationLampSortsOneRungOverItsBoat_AndItsStreakUnderEveryHullAndDeckRow()
        {
            var boat = new GameObject("occlusion-boat") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var group = boat.AddComponent<UnityEngine.Rendering.SortingGroup>();
                group.sortingOrder = HullSorting.OrderAt(3f);
                var hull = new GameObject("hull");
                hull.transform.SetParent(boat.transform, false);
                hull.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 1;   // her local order inside the boat
                var mast = new GameObject("masthead");
                mast.transform.SetParent(hull.transform, false);
                SceneLight light = mast.AddComponent<SceneLight>();
                BoatLampPresets.Apply(light, HullLampKind.Masthead);

                Assert.IsTrue(light.SortsInWorld, "the shipped look sorts a navigation lamp among the world");
                Assert.AreEqual(group.sortingOrder + SceneLight.RungsOverCarrier, light.BloomSortingOrder,
                    "one rung over the boat's own group (the one that sorts at the root), not over a local order inside her");
                Assert.Less(light.BloomSortingOrder, SortingBands.WorldTint,
                    "sorted among the world, so under the day/night tint whose darkening the shader divides out");
                Assert.Greater(HullSorting.OrderAt(3f - 1f), light.BloomSortingOrder,
                    "a hull a metre nearer the camera sorts over this lamp, so it hides it");
                Assert.Less(HullSorting.OrderAt(3f + 1f), light.BloomSortingOrder,
                    "and a hull a metre further away sorts under it");

                Assert.AreEqual(SortingBands.WharfDeckMin, light.StreakSortingOrder,
                    "the streak lies on the sea: over it, under every other deck row, every hull and every stander");
                Assert.Greater(light.StreakSortingOrder, SortingBands.Sea);
                Assert.Less(light.StreakSortingOrder, SortingBands.DecorFloor);

                group.sortingOrder += 7;   // she moved: her one writer re-sorts her
                Assert.AreEqual(group.sortingOrder + SceneLight.RungsOverCarrier, light.BloomSortingOrder,
                    "the lamp follows its boat's order as she moves");
            }
            finally { Object.DestroyImmediate(boat); }
        }

        [Test]
        public void AWharfLampSortsOneRungOverItsOwnPost_AndALooseLampByItsFoot()
        {
            var post = new GameObject("occlusion-post") { hideFlags = HideFlags.HideAndDontSave };
            SceneLight loose = NewLight("occlusion-loose");
            try
            {
                post.transform.position = new Vector3(4f, -12f, 0f);
                var sr = post.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SceneLight.CarrierOrderAtFoot(-12f);
                SceneLight light = post.AddComponent<SceneLight>();
                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.IsTrue(light.SortsInWorld);
                Assert.AreEqual(sr.sortingOrder + SceneLight.RungsOverCarrier, light.BloomSortingOrder,
                    "over its own post, under whatever stands a quarter metre nearer the camera");

                loose.transform.position = new Vector3(0f, 20f, 0f);
                LightPresets.Apply(loose, LightPresets.Kind.Lightpost);
                Assert.AreEqual(SceneLight.CarrierOrderAtFoot(loose.WorldOrigin.y) + SceneLight.RungsOverCarrier,
                    loose.BloomSortingOrder, "a lamp nothing carries sorts as a thing standing at its foot");
            }
            finally
            {
                Object.DestroyImmediate(post);
                Object.DestroyImmediate(loose.gameObject);
            }
        }

        [Test]
        public void ABareLight_TheHeadlamp_AndTheLookSwitchedOff_StillSortOverEverything()
        {
            SceneLight bare = NewLight("occlusion-bare");
            SceneLight head = NewLight("occlusion-headlamp");
            try
            {
                Assert.IsFalse(bare.SortsInWorld, "a light nobody stamped keeps the old ladder");
                Assert.AreEqual(SceneLight.MaxSortingOrder, bare.BloomSortingOrder);
                Assert.AreEqual(SceneLight.MaxSortingOrder, bare.StreakSortingOrder);

                LightPresets.Apply(head, LightPresets.Kind.Headlamp);
                Assert.IsFalse(head.SortsInWorld, "the worn headlamp is not part of this look");

                Override(so => so.FindProperty("_sortInWorld").boolValue = false);
                BoatLampPresets.Apply(bare, HullLampKind.Masthead);
                Assert.IsFalse(bare.SortsInWorld, "switched off, a lamp sorts over everything as before");
                Assert.AreEqual(SceneLight.MaxSortingOrder, bare.BloomSortingOrder);

                Override(so => so.FindProperty("_enabled").boolValue = false);
                LightPresets.Apply(bare, LightPresets.Kind.Lightpost);
                Assert.IsFalse(bare.SortsInWorld, "the whole look off is the look before");
                Assert.IsFalse(GlassLook.Current.SortsInWorld);
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
                Object.DestroyImmediate(head.gameObject);
            }
        }

        [Test]
        public void ALightSortedUnderTheTint_DividesOutTheTintTheOverlayApplies()
        {
            const string additive = "Assets/_Project/Art/Shaders/HiddenHarboursAdditiveLight.shader";
            const string response = "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl";
            string src = System.IO.File.ReadAllText(additive);
            string law = System.IO.File.ReadAllText(response);

            float Define(string text, string name)
            {
                var m = System.Text.RegularExpressions.Regex.Match(text, @"#define\s+" + name + @"\s+([0-9.eE+-]+)");
                Assert.IsTrue(m.Success, name + " is not defined");
                return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            StringAssert.Contains("tint = HHSrgbToLinear(tint);", src,
                "the overlay multiplies by the LINEAR tint, so that is what is divided out");
            StringAssert.Contains("return rgb / max(tint, HH_LIGHT_INWORLD_MIN_CHANNEL);", src);
            StringAssert.Contains("if (_InWorld < 0.5) return rgb;", src, "off draws exactly as before");
            Assert.AreEqual(3, System.Text.RegularExpressions.Regex.Matches(src, @"return half4\(InWorld\(").Count,
                "the pane, the streak and the lens all undo the tint when sorted among the world");
            float floorCh = Define(src, "HH_LIGHT_INWORLD_MIN_CHANNEL");
            Assert.Less(floorCh, Define(law, "SPRITE_LIGHT_DN_COMP_MIN_CHANNEL"),
                "the in-world floor sits below a deep-night linear channel, so a night lamp is not clamped dim");

            // The twin: what lands on the frame is what the light added over the tint, where uncovered.
            // The published tint is the night value the 2026-11-25 capture read; the overlay applies it linear.
            var published = new Color(0.118f, 0.135f, 0.183f, 1f);
            Color tint = published.linear;
            Assert.Greater(Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)), floorCh, "a real night channel is above the floor");
            var lamp = new Color(0.9f, 0.7f, 0.4f, 1f);
            var under = new Color(0.3f, 0.35f, 0.4f, 1f);
            Color comp = LightMath.CompensateForDayNightTint(lamp, tint, floorCh);
            Color inWorld = (under + comp) * tint;
            Color overTint = under * tint + lamp;
            Assert.AreEqual(overTint.r, inWorld.r, 1e-4f);
            Assert.AreEqual(overTint.g, inWorld.g, 1e-4f);
            Assert.AreEqual(overTint.b, inWorld.b, 1e-4f);
        }

        [Test]
        public void ThePaneGradientIsBrighterLow()
        {
            GlassLook.PaneLook p = GlassLook.Current.Panes;
            Assert.Greater(p.SillLevel, p.HeadLevel, "the lamp inside stands low: the sill is brighter than the head");
            Assert.Greater(p.HeadLevel, 0f, "the head of a lit pane is still lit");
        }

        [Test]
        public void ALampsGlassIsAHotCentre_AColouredBody_AndASofterRim()
        {
            foreach (GlassLook.Lens lens in new[] { GlassLook.Current.NavLens, GlassLook.Current.ShoreLens })
            {
                Assert.Greater(lens.HotFraction, 0f, "there is a white-hot centre");
                Assert.Less(lens.HotFraction, 1f, "only the centre is white: the rest of the core is the body");
                Assert.Less(lens.BodyWhiten, 0.5f, "the body keeps the lamp's own colour");
                Assert.Greater(lens.BodyLevel, lens.RimLevel, "the rim is softer than the body it rings");
                Assert.Greater(lens.RimLevel, 0f, "the rim is still drawn");
                Assert.Less(lens.Tail, lens.RimLevel, "the tail outside the glass is fainter than the glass");
            }
        }

        [Test]
        public void ALitPaneHasAFrame_DimsAtItsSides_AndEachWindowIsItsOwnWarmColour()
        {
            GlassLook.PaneLook p = GlassLook.Current.Panes;
            Assert.GreaterOrEqual(p.FramePixels, 1f, "a boat pane draws a frame");
            Assert.Greater(p.AcrossFalloff, 0f, "a pane is dimmer at its sides than in its middle");
            Assert.Less(p.AcrossFalloff, 0.5f, "subtle: the sides are still lit");
            Assert.Greater(p.Unevenness, 0f, "the glass is a little uneven");
            Assert.Less(p.Unevenness, 0.15f, "subtle: unevenness, not noise");
            Assert.Greater(p.TintVariation, 0f, "windows differ a little in colour");
            Assert.Greater(p.TintAlt.r, p.TintAlt.b, "the second interior colour is warm");
        }
    }
}
