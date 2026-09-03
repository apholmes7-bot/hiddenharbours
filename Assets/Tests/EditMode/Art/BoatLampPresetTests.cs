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
        // The cape wears her pair 0.6048 m apart (±0.3024 from the centreline), and she is still the
        // tightest in the fleet — but that is now MEASURED off the shipped defs rather than typed here,
        // because PR 2 gave twenty-six more hulls sidelights and the bound must come from whichever of
        // them wears hers closest together. Kept as a named number only so a change to the tightest
        // hull is legible in the failure message.
        const float CapeSidelightSeparationMetres = 0.6048f;

        static LightPresets.Config Port => BoatLampPresets.For(HullLampKind.PortSidelight);
        static LightPresets.Config Star => BoatLampPresets.For(HullLampKind.StarboardSidelight);
        static LightPresets.Config Stern => BoatLampPresets.For(HullLampKind.SternLight);
        static LightPresets.Config Mast => BoatLampPresets.For(HullLampKind.Masthead);
        static LightPresets.Config Cabin => BoatLampPresets.For(HullLampKind.CabinGlow);
        static LightPresets.Config Anchor => BoatLampPresets.For(HullLampKind.AnchorLight);
        static LightPresets.Config Range => BoatLampPresets.For(HullLampKind.RangeLight);

        /// <summary>
        /// The tightest sidelight pair anywhere in the shipped fleet, and the hull that wears it.
        ///
        /// <para><b>Read off the DEFS, not restated.</b> The preset's radius is bounded by real
        /// geometry — two radial glows overlap wherever their radii sum exceeds the gap between them —
        /// so the bound has to come from the boat that has the least room, whichever boat that turns
        /// out to be. A constant here would have been correct on the day it was typed and silently
        /// wrong the day a narrower hull was imported.</para>
        /// </summary>
        static (float Metres, string Hull) TightestSidelightPair()
        {
            float tightest = float.MaxValue;
            string who = "(none)";
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:HullMeshDef"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
                if (def == null || def.Lamps == null) continue;

                float portX = float.NaN, starX = float.NaN;
                foreach (HullLamp l in def.Lamps)
                {
                    if (l.Kind == HullLampKind.PortSidelight) portX = l.RigLocalMetres.x;
                    if (l.Kind == HullLampKind.StarboardSidelight) starX = l.RigLocalMetres.x;
                }
                if (float.IsNaN(portX) || float.IsNaN(starX)) continue;

                float gap = Mathf.Abs(starX - portX);
                if (gap < tightest) { tightest = gap; who = def.Id; }
            }
            return (tightest, who);
        }

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
            //
            // ⭐ The gap is the TIGHTEST in the fleet, measured off the shipped defs. One preset dresses
            // twenty-seven hulls, from an 8.6 m inshore lobster boat to a 110 m tanker, so a radius
            // that fits the boat it was designed on proves nothing about the narrowest one.
            (float tightest, string who) = TightestSidelightPair();
            float ceiling = tightest * 0.5f;

            Assert.Less(tightest, float.MaxValue,
                        "no hull in the fleet declares a sidelight pair — the bound cannot be measured");
            Assert.LessOrEqual(Port.Range, ceiling,
                               $"the port sidelight's glow stops short of the centreline on the " +
                               $"tightest hull in the fleet ({who}, pair {tightest:F4} m apart)");
            Assert.LessOrEqual(Star.Range, ceiling,
                               "and so does the starboard one, so they never mix to yellow");
            Assert.LessOrEqual(Port.Range + Star.Range, tightest,
                               "stated the other way round: the two radii cannot span the gap");

            Assert.AreEqual(CapeSidelightSeparationMetres, tightest, 1e-4f,
                            $"the Cape Islander is expected to still be the tightest pair in the fleet; " +
                            $"'{who}' now wears hers {tightest:F4} m apart. That is not a failure of the " +
                            "boat — it means the preset's ceiling has moved and the assertions above are " +
                            "the ones to read.");
        }

        // ---- the two kinds PR 2 added ------------------------------------------------------------------

        [Test]
        public void TheAnchorLightIsAWhiteAllRoundLampAndDimmerThanTheMasthead()
        {
            Assert.AreEqual(SceneLight.LightShape.Radial, Anchor.Shape,
                            "an anchor light shows all round the horizon");
            Assert.AreEqual(Mast.Color, Anchor.Color, "and it is white, like every masthead");
            Assert.AreEqual(0f, Anchor.FlickerAmount,
                            "steady: a wobbling anchor light reads as a fire aboard");

            // A masthead says "under power, coming through" and is the brightest lamp on the boat. An
            // anchor light says only "something is here". A wharf of seven of them, each as bright as
            // a steaming light, would read as a fleet getting under way rather than a fleet asleep.
            Assert.Less(Anchor.Intensity, Mast.Intensity,
                        "the anchor light burns lower than the masthead it hangs in place of");
            Assert.Less(Anchor.Range, Mast.Range, "and reaches less far");
            Assert.Less(Anchor.Range, Cabin.Range,
                        "it is a point of light, not a room — well inside the cabin glow's reach");
            Assert.Greater(Anchor.Range, Port.Range,
                           "but further than a sidelight, which is a lamp in a box on the bow");
        }

        [Test]
        public void TheRangeLightIsTheMastheadsOwnLook()
        {
            // A range light IS a masthead light — the rule of the road tells the two apart by where
            // they are hung and how high, never by what they look like. Only the KINDS are separate,
            // so that a hull carrying two of them cannot collapse into one duplicated row.
            Assert.AreEqual(Mast.Shape, Range.Shape);
            Assert.AreEqual(Mast.Color, Range.Color);
            Assert.AreEqual(Mast.Intensity, Range.Intensity);
            Assert.AreEqual(Mast.Range, Range.Range);
            Assert.AreEqual(Mast.EdgeSoftness, Range.EdgeSoftness);
            Assert.AreEqual(Mast.FlickerAmount, Range.FlickerAmount);
        }

        [Test]
        public void NoKindFallsThroughToTheSternLightByAccident()
        {
            // For..Kind ends in `default: goto case SternLight`, which is a safe landing but a silent
            // one: a kind added to the enum and forgotten here would quietly become a stern light —
            // white, where a new coloured lamp might have needed not to be. Every kind that is NOT the
            // stern light must therefore differ from it in something.
            foreach (HullLampKind kind in System.Enum.GetValues(typeof(HullLampKind)))
            {
                if (kind == HullLampKind.SternLight) continue;
                if (kind == HullLampKind.Spotlight) continue;   // BoatSpotlight draws it, not a preset

                LightPresets.Config c = BoatLampPresets.For(kind);
                bool differs = c.Color != Stern.Color
                            || !Mathf.Approximately(c.Intensity, Stern.Intensity)
                            || !Mathf.Approximately(c.Range, Stern.Range)
                            || !Mathf.Approximately(c.EdgeSoftness, Stern.EdgeSoftness)
                            || !Mathf.Approximately(c.FlickerAmount, Stern.FlickerAmount);
                Assert.IsTrue(differs,
                              $"{kind} is indistinguishable from the stern light — either it has no " +
                              "case of its own in BoatLampPresets.For and fell through the default, " +
                              "or it genuinely wants one and should say so with `goto case`.");
            }
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
