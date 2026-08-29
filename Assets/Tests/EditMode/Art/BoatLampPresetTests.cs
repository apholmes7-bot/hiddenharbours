using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The fleet's lamp presets</b> (ADR 0016) — pure, headless, no scene and no GPU.
    ///
    /// <para>Most of what is pinned here is not taste, it is the rule of the road: red to port, green
    /// to starboard, white astern. Those are the only thing a player can read a boat's aspect from at
    /// this camera, so they are asserted as facts rather than left as tunables somebody could drift.
    /// The sizes and softnesses are taste and are pinned only where a change would break something —
    /// a sidelight whose reach exceeded the gap between the pair would mix red and green into a
    /// colour that means nothing.</para>
    /// </summary>
    public class BoatLampPresetTests
    {
        // The cape wears her pair 0.6048 m apart (±0.3024 from the centreline), and she is the boat
        // the whole feature was measured on. A sidelight reaching further than the gap floods its
        // opposite number.
        const float CapeSidelightSeparationMetres = 0.6048f;

        static LightPresets.Config Port => BoatLampPresets.For(HullLampKind.PortSidelight);
        static LightPresets.Config Star => BoatLampPresets.For(HullLampKind.StarboardSidelight);
        static LightPresets.Config Stern => BoatLampPresets.For(HullLampKind.SternLight);
        static LightPresets.Config Mast => BoatLampPresets.For(HullLampKind.Masthead);
        static LightPresets.Config Cabin => BoatLampPresets.For(HullLampKind.CabinGlow);

        // ---- the rule of the road ---------------------------------------------------------------------

        [Test]
        public void ThePortSidelightIsRedAndTheStarboardOneIsGreen()
        {
            Assert.Greater(Port.Color.r, 0.8f, "the port sidelight is RED");
            Assert.Less(Port.Color.g, 0.3f, "and not appreciably green");
            Assert.Less(Port.Color.b, 0.3f, "and not appreciably blue");

            Assert.Greater(Star.Color.g, 0.8f, "the starboard sidelight is GREEN");
            Assert.Less(Star.Color.r, 0.3f, "and not appreciably red");

            // The one mistake here that could actually mislead a player about which way a boat is
            // heading, stated as its own assertion so a swap cannot pass by both halves moving.
            Assert.Greater(Port.Color.r, Star.Color.r, "port is the redder of the two");
            Assert.Greater(Star.Color.g, Port.Color.g, "starboard is the greener of the two");
        }

        [Test]
        public void TheSternAndMastheadLampsAreWhite()
        {
            foreach (LightPresets.Config c in new[] { Stern, Mast })
            {
                Assert.Greater(c.Color.r, 0.8f);
                Assert.Greater(c.Color.g, 0.8f);
                Assert.Greater(c.Color.b, 0.8f, "a masthead or stern lamp is WHITE, near enough");
            }
        }

        [Test]
        public void TheNavigationLampsAreSteady()
        {
            // Electric, off her own batteries. A wobbling sidelight reads as a fire.
            Assert.AreEqual(0f, Port.FlickerAmount);
            Assert.AreEqual(0f, Star.FlickerAmount);
            Assert.AreEqual(0f, Stern.FlickerAmount);
            Assert.AreEqual(0f, Mast.FlickerAmount);
        }

        [Test]
        public void EveryLampIsARadialGlow()
        {
            // The searchlight is the only cone on a boat, and it is BoatSpotlight's, not a preset
            // here. A lamp that became a cone would need an orientation nothing feeds it, and would
            // point along whatever axis its node happened to carry.
            foreach (HullLampKind k in new[]
                     {
                         HullLampKind.PortSidelight, HullLampKind.StarboardSidelight,
                         HullLampKind.SternLight, HullLampKind.Masthead, HullLampKind.CabinGlow,
                     })
                Assert.AreEqual(SceneLight.LightShape.Radial, BoatLampPresets.For(k).Shape,
                                $"{k} is a radial glow");
        }

        // ---- sizes that have to hold ------------------------------------------------------------------

        [Test]
        public void TheTwoSidelightGlowsNeverOverlap()
        {
            // Two radial glows overlap wherever their radii SUM exceeds the gap between them, and
            // where red and green overlap additively the answer is yellow — the two lamps whose whole
            // job is to be told apart merged into one colour that says nothing. Half the separation
            // each is therefore the ceiling, not the whole separation: the pair meet exactly at the
            // centreline at zero intensity and never cross.
            float ceiling = CapeSidelightSeparationMetres * 0.5f;

            Assert.LessOrEqual(Port.Range, ceiling,
                               "the port sidelight's glow stops short of the centreline");
            Assert.LessOrEqual(Star.Range, ceiling,
                               "and so does the starboard one, so they never mix to yellow");
            Assert.LessOrEqual(Port.Range + Star.Range, CapeSidelightSeparationMetres,
                               "stated the other way round: the two radii cannot span the gap");
        }

        [Test]
        public void TheCabinGlowIsTheBigSoftOneAndTheLampsAreNot()
        {
            Assert.Greater(Cabin.Range, Mast.Range,
                           "a lit room spills further than any single lamp aboard");
            Assert.Greater(Cabin.EdgeSoftness, Stern.EdgeSoftness,
                           "and reads as a spill rather than as a source you look at");
            Assert.Greater(Cabin.Color.r, Cabin.Color.b, "warm, not cold — it is a lit room");
        }

        [Test]
        public void TheMastheadIsTheFurthestReachingLamp()
        {
            Assert.Greater(Mast.Range, Stern.Range);
            Assert.Greater(Mast.Range, Port.Range, "it is mounted highest and seen furthest");
        }

        // ---- stamping ---------------------------------------------------------------------------------

        [Test]
        public void AnUnsetIntensityScaleMeansThePresetRatherThanDarkness()
        {
            // A struct deserialised out of a def written before the field existed carries 0, and a
            // lamp silently scaled to nothing is the worst kind of bug in this feature: it looks
            // exactly like the night gate doing its job.
            var unset = new HullLamp { Kind = HullLampKind.SternLight, IntensityScale = 0f };
            Assert.AreEqual(1f, unset.SafeIntensityScale);

            var negative = new HullLamp { Kind = HullLampKind.SternLight, IntensityScale = -3f };
            Assert.AreEqual(1f, negative.SafeIntensityScale);

            var set = new HullLamp { Kind = HullLampKind.SternLight, IntensityScale = 0.4f };
            Assert.AreEqual(0.4f, set.SafeIntensityScale, 1e-6f);
        }

        [Test]
        public void StampingALampWritesThePresetAndLayersThisPlacementsTrim()
        {
            var go = new GameObject("lamp-stamp-test");
            try
            {
                var light = go.AddComponent<SceneLight>();
                BoatLampPresets.Apply(light, HullLampKind.PortSidelight, 0.5f);

                Assert.AreEqual(Port.Color, light.Color, "the colour is the preset's");
                Assert.AreEqual(Port.Range, light.Range, 1e-6f, "so is the reach");
                Assert.AreEqual(Port.Intensity * 0.5f, light.Intensity, 1e-6f,
                                "and the trim multiplies the preset rather than replacing it");

                // Re-stamping is how the cabin boost is applied, so it must be idempotent from the
                // preset rather than compounding on the live value — a hundred trips below decks
                // cannot be allowed to ratchet a lamp to white.
                BoatLampPresets.Apply(light, HullLampKind.PortSidelight, 0.5f);
                Assert.AreEqual(Port.Intensity * 0.5f, light.Intensity, 1e-6f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void StampingIsNullSafe()
        {
            Assert.DoesNotThrow(() => BoatLampPresets.Apply(null, HullLampKind.Masthead));
        }
    }
}
