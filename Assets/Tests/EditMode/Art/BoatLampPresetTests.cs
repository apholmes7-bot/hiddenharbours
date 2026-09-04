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
        public void EveryNavigationLampIsARadialGlow()
        {
            // A NAVIGATION lamp that became a cone would need an orientation nothing feeds it, and
            // would point along whatever axis its node happened to carry. At this camera a sidelight
            // is a handful of pixels anyway: the COLOUR is the signal, not the sector.
            foreach (HullLampKind k in new[]
                     {
                         HullLampKind.PortSidelight, HullLampKind.StarboardSidelight,
                         HullLampKind.SternLight, HullLampKind.Masthead,
                         HullLampKind.AnchorLight, HullLampKind.RangeLight,
                     })
                Assert.AreEqual(SceneLight.LightShape.Radial, BoatLampPresets.For(k).Shape,
                                $"{k} is a radial glow");
        }

        [Test]
        public void TheCabinGlowIsTheOneConeBecauseItLeavesAWall()
        {
            // Owner's ruling, 2026-09-03: "the glows should be constrained to their space, if its
            // interior it should be confined to the cabin with the glow only coming through the
            // windows." What is left of the cabin glow is the WASH that leaves a glazed wall, and a
            // wash off a wall is directional by construction — the wall behind it is what makes it
            // so. It is the one lamp on a boat that pays for an orientation, and BoatWindowGlow is
            // what feeds it one (that wall's outward direction, through the hull's own posed frame).
            Assert.AreEqual(SceneLight.LightShape.Cone, Cabin.Shape,
                            "the cabin glow is a wall wash now, not a disc over the roof");
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
        public void TheCabinWashReachesFurtherAndSofterThanAnySingleLamp()
        {
            // ⚠️ Against a REAL wash, not against the preset's Range — that field carries the FLOOR a
            // wall with almost no glazing gets, and the floor is a backstop nothing in the fleet
            // reaches. Comparing it to a lamp says nothing about the boats we actually draw. The
            // narrowest window in the whole fleet is the tanker's 0.42 m inscribed porthole; even she
            // washes further than the masthead's bloom, and that is the claim worth holding.
            Assert.Greater(BoatLampPresets.WallSpillThrow(0.42f), Mast.Range,
                           "a lit room washes further than any single lamp aboard — the smallest " +
                           "porthole in the fleet still reaches past a masthead's bloom");
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

        // ---- the 2026-09-03 shrink, and what it was not allowed to change -----------------------------

        [Test]
        public void EveryRoundLampIsTheSizeOfItsOwnFittingNotAPoolOnTheDeck()
        {
            // The ruling's other half — "the glows should be constrained to their space" — applied to
            // the lamps that are not a cabin. Half a metre is already generous for a fitting you
            // could hold in one hand; the point of the bound is that NONE of them may go back to
            // being a pool of light on the deck, which is what 1.0 m and 1.35 m read as at the zoom
            // the owner plays at.
            foreach (HullLampKind k in new[]
                     {
                         HullLampKind.PortSidelight, HullLampKind.StarboardSidelight,
                         HullLampKind.SternLight, HullLampKind.Masthead,
                         HullLampKind.AnchorLight, HullLampKind.RangeLight,
                     })
                Assert.LessOrEqual(BoatLampPresets.For(k).Range, 0.5f,
                                   $"{k} is a lamp, not a pool — it may not be bigger than its own " +
                                   "fitting (owner's ruling, 2026-09-03)");
        }

        [Test]
        public void TheShrinkKeptEveryLampInItsOldOrder()
        {
            // ⚠️ THE ORDER IS THE MEANING, AND IT IS THE THING THE SHRINK COULD HAVE BROKEN SILENTLY.
            // A masthead says "under power, coming through" and must stay the brightest and biggest
            // white; an anchor light says only "something is here" and must stay the dimmest and
            // smallest, or a wharf of sleeping boats reads as a fleet getting under way. All three
            // moved together, so all three comparisons that held before must still hold.
            foreach (HullLampKind k in new[] { HullLampKind.SternLight, HullLampKind.AnchorLight })
            {
                Assert.Less(BoatLampPresets.For(k).Range, Mast.Range,
                            $"the masthead still reaches further than the {k}");
                Assert.Less(BoatLampPresets.For(k).Intensity, Mast.Intensity,
                            $"and still burns brighter than the {k}");
            }
            Assert.Less(Anchor.Range, Stern.Range, "and the anchor light is the smallest white");

            // And the same in the arm the owner can flip back to, from the numbers that shipped.
            foreach (HullLampKind k in new[] { HullLampKind.SternLight, HullLampKind.AnchorLight })
                Assert.Less(BoatLampPresets.Legacy(k).Range, BoatLampPresets.Legacy(HullLampKind.Masthead).Range,
                            $"the passthrough holds the same order for the {k}");
        }

        [Test]
        public void TheLampsThatDidNotNeedToShrinkDidNotMove()
        {
            // The sidelights were ALREADY bounded, by the gap between them rather than by taste (see
            // TheTwoSidelightGlowsNeverOverlap) — a harder constraint than the ruling's, and already
            // being met. So they are the same object in both arms, and a future edit that quietly
            // retunes them under cover of this ruling has to answer this test.
            foreach (HullLampKind k in new[] { HullLampKind.PortSidelight, HullLampKind.StarboardSidelight })
            {
                LightPresets.Config now = BoatLampPresets.For(k), then = BoatLampPresets.Legacy(k);
                Assert.AreEqual(then.Range, now.Range, 1e-6f, $"{k} reach");
                Assert.AreEqual(then.Intensity, now.Intensity, 1e-6f, $"{k} intensity");
                Assert.AreEqual(then.Color, now.Color, $"{k} colour");
            }
        }

        // ---- the passthrough: yesterday's picture, exactly ---------------------------------------------

        [Test]
        public void ThePassthroughIsYesterdaysNumbers()
        {
            // ⚠️ PINNED AGAINST THE LITERALS THAT SHIPPED, not against For(). An A/B whose "before"
            // arm drifts with the "after" one is not an A/B at all — it is two copies of today — and
            // the drift would be invisible, because both arms would still look self-consistent.
            LightPresets.Config cabin = BoatLampPresets.Legacy(HullLampKind.CabinGlow);
            Assert.AreEqual(SceneLight.LightShape.Radial, cabin.Shape, "yesterday's cabin was a DISC");
            Assert.AreEqual(1.5f, cabin.Range, 1e-6f);
            Assert.AreEqual(0.55f, cabin.Intensity, 1e-6f);
            Assert.AreEqual(0.92f, cabin.EdgeSoftness, 1e-6f);
            Assert.AreEqual(0.03f, cabin.FlickerAmount, 1e-6f);

            LightPresets.Config stern = BoatLampPresets.Legacy(HullLampKind.SternLight);
            Assert.AreEqual(1.0f, stern.Range, 1e-6f);
            Assert.AreEqual(1.1f, stern.Intensity, 1e-6f);

            LightPresets.Config mast = BoatLampPresets.Legacy(HullLampKind.Masthead);
            Assert.AreEqual(1.35f, mast.Range, 1e-6f);
            Assert.AreEqual(1.25f, mast.Intensity, 1e-6f);

            LightPresets.Config anchor = BoatLampPresets.Legacy(HullLampKind.AnchorLight);
            Assert.AreEqual(0.75f, anchor.Range, 1e-6f);
            Assert.AreEqual(0.8f, anchor.Intensity, 1e-6f);

            // The range light was, and remains, the masthead's own look — one lamp, two stations.
            Assert.AreEqual(mast.Range, BoatLampPresets.Legacy(HullLampKind.RangeLight).Range, 1e-6f);
        }

        [Test]
        public void ThePassthroughIsActuallyDifferentFromToday()
        {
            // The negative control on the test above: if somebody "fixed" Legacy by making it call
            // For(), every assertion up there would still pass and the A/B would be dead. This is
            // the one that would go red.
            foreach (HullLampKind k in new[]
                     {
                         HullLampKind.CabinGlow, HullLampKind.SternLight,
                         HullLampKind.Masthead, HullLampKind.AnchorLight,
                     })
                Assert.AreNotEqual(BoatLampPresets.Legacy(k).Range, BoatLampPresets.For(k).Range,
                                   $"the {k} is the same in both arms — the A/B has collapsed");
        }

        // ---- the wall wash ------------------------------------------------------------------------------

        [Test]
        public void AWallWashIsAimedSoftAndCored()
        {
            var go = new GameObject("spill");
            try
            {
                var light = go.AddComponent<SceneLight>();
                BoatLampPresets.ApplyWallSpill(light, windowWidthMetres: 0.66f);

                Assert.AreEqual(SceneLight.LightShape.Cone, light.Shape);
                Assert.AreEqual(BoatLampPresets.WallSpillHalfAngleDeg, light.ConeHalfAngle, 1e-6f);
                Assert.AreEqual(0.924f, light.Range, 1e-4f,
                                "the throw is twice that WINDOW's own width, from the data");

                // ⭐ ZERO CORE, AND THIS IS THE ASSERTION THE WHOLE RULING TURNS ON. Every other lamp
                // in the library wants a hot point at its origin because every other lamp IS a point.
                // A spill's origin is a WALL; the bright thing there is the glass, drawn as a real
                // rectangle by BoatWindowGlow. A core boost here would put a round blob back at the
                // wall, which is exactly the picture the owner refused.
                Assert.AreEqual(0f, light.CoreBoost, 1e-6f, "a wall wash has no hot point");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AWashIsScaledOffAWindowAndNotOffTheWall()
        {
            // ⭐ THE NUMBER THE RULING TURNS ON, tested at the two ends of the fleet's real range.
            //
            // The tanker's accommodation carries FIVE portholes strung over 6.8 m of side. Scaled off
            // that SPAN her wash would be a seven-metre floodlight — the very thing this lane retired,
            // put back on the biggest hull in the game. Scaled off one of her windows (0.42 m of
            // inscribed glass) it is 0.84 m: a lit room seen through portholes, which is what she is.
            Assert.AreEqual(0.588f, BoatLampPresets.WallSpillThrow(0.42f), 1e-4f,
                            "a porthole washes twice its own width, however many neighbours it has");

            // And the cape's 0.66 m side light reaches further than the tanker's porthole does —
            // from the data, not from a dial, and in the right direction.
            Assert.Greater(BoatLampPresets.WallSpillThrow(0.66f), BoatLampPresets.WallSpillThrow(0.42f),
                           "a bigger window throws further");

            // The clamps are backstops, not working numbers: nothing in the fleet reaches either.
            Assert.AreEqual(BoatLampPresets.MaxWallSpillMetres, BoatLampPresets.WallSpillThrow(9f), 1e-6f);
            Assert.AreEqual(BoatLampPresets.MinWallSpillMetres, BoatLampPresets.WallSpillThrow(0.05f), 1e-6f);
            Assert.Less(BoatLampPresets.MaxWallSpillMetres,
                        BoatLampPresets.Legacy(HullLampKind.CabinGlow).Range * 2f,
                        "even the backstop must stay under the diameter of the disc this replaced");
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
