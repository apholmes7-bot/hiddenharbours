using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE LOOK OF A LANTERN — pinned, because a mark's colour is a message and not a taste.</b>
    ///
    /// <para><see cref="NavLightPresets"/> is pure data, so every value in it can be held here without
    /// a scene or a GPU. The point is not that the numbers are right — that is the owner's eye on the
    /// plates — but that changing one is a thing somebody has to MEAN.</para>
    /// </summary>
    public class NavLightPresetsTests
    {
        /// <summary>
        /// ⭐⭐ <b>The inversion that will catch somebody one day.</b>
        ///
        /// <para>A vessel shows RED to port and GREEN to starboard. A lateral MARK in IALA Region B
        /// is the other way about: the port-hand buoy — the one you leave to your left going
        /// upstream — shows GREEN, and the starboard-hand buoy shows RED. So a buoy's green must
        /// match the fleet's STARBOARD sidelight and a buoy's red the fleet's PORT one, and anybody
        /// "fixing" this file to line the two up name-for-name would put every channel in the game
        /// inside out.</para>
        ///
        /// <para>They are pinned EQUAL as colours because the signal is the same signal — the same
        /// red on a bow and on a nun means the same thing to the same eye — and holding them equal
        /// here is what makes a future divergence a decision rather than a drift.</para>
        /// </summary>
        [Test]
        public void AMarksColourIsTheFleetsColourWithTheHandsTheOtherWayRound()
        {
            Color buoyGreen = NavLightPresets.For(NavLightColour.Green).Color;
            Color buoyRed   = NavLightPresets.For(NavLightColour.Red).Color;

            Color vesselStarboard = BoatLampPresets.For(HullLampKind.StarboardSidelight).Color;
            Color vesselPort      = BoatLampPresets.For(HullLampKind.PortSidelight).Color;

            Assert.That(buoyGreen, Is.EqualTo(vesselStarboard),
                        "a PORT-HAND mark's green should be the same green a vessel shows to " +
                        "STARBOARD — Region B puts the colours on the opposite hands.");
            Assert.That(buoyRed, Is.EqualTo(vesselPort),
                        "a STARBOARD-HAND mark's red should be the same red a vessel shows to PORT.");
            Assert.That(buoyGreen, Is.Not.EqualTo(buoyRed), "green and red have become one colour");
        }

        /// <summary>Every lantern is a round halo, dead steady, at the shipped reach and brightness.</summary>
        [Test]
        public void EveryLanternIsARadialSteadyGlowAtTheShippedReach()
        {
            foreach (NavLightColour colour in new[]
                     { NavLightColour.White, NavLightColour.Green, NavLightColour.Red, NavLightColour.Yellow })
            {
                LightPresets.Config c = NavLightPresets.For(colour);
                Assert.That(c.Shape, Is.EqualTo(SceneLight.LightShape.Radial),
                            $"{colour}: a lantern shows all round, not in a cone");
                Assert.That(c.FlickerAmount, Is.Zero,
                            $"{colour}: a buoy lantern is an LED on a battery — a wobble would blur " +
                            "the character a skipper is counting");
                Assert.That(c.Range, Is.EqualTo(NavLightPresets.LanternRangeMetres).Within(1e-4f));
                Assert.That(c.Intensity, Is.EqualTo(NavLightPresets.LanternIntensity).Within(1e-4f));
                Assert.That(c.OriginOffset, Is.EqualTo(Vector2.zero),
                            $"{colour}: the lantern's place is the mark's data, not the preset's");
            }
        }

        /// <summary>
        /// A lantern is brighter and reaches further than a sidelight. The two fittings have
        /// opposite jobs — a sidelight must NOT reach its opposite number, a mark must be picked up
        /// from as far off as possible — and this is the assertion that says so out loud.
        /// </summary>
        [Test]
        public void ALanternOutreachesASidelightBecauseItHasTheOppositeJob()
        {
            LightPresets.Config lantern = NavLightPresets.For(NavLightColour.Green);
            LightPresets.Config sidelight = BoatLampPresets.For(HullLampKind.StarboardSidelight);

            Assert.That(lantern.Range, Is.GreaterThan(sidelight.Range),
                        "a channel mark that threw no further than a sidelight would be invisible " +
                        "at the range it exists to be seen from");
            Assert.That(lantern.Intensity, Is.GreaterThan(sidelight.Intensity));
        }

        /// <summary>
        /// ⭐ <c>Apply</c> turns cast shadows OFF and takes the lantern height from the mark. The
        /// shadow system rescans every registered lamp against every caster on a 10 Hz tick; a buoy
        /// stands in open water with nothing inside her 1.6 m, so every pair she adds is work that
        /// provably yields no shadow — and a flashing mark would add and remove them twice a second.
        /// </summary>
        [Test]
        public void ApplyLeavesALanternCastingNoShadowsAtHerOwnHeight()
        {
            var go = new GameObject("NavLightPresetProbe");
            try
            {
                var light = go.AddComponent<SceneLight>();
                light.CastsShadows = true;

                NavLightPresets.Apply(light, NavLightColour.Red, 1.8125f);

                Assert.That(light.CastsShadows, Is.False,
                            "a buoy lantern registered as a shadow caster — 23 marks flashing would " +
                            "churn the 10 Hz lamp/caster scan for no shadow at all");
                Assert.That(light.LampHeightMeters, Is.EqualTo(1.8125f).Within(1e-4f));
                Assert.That(light.Color, Is.EqualTo(NavLightPresets.For(NavLightColour.Red).Color));
                Assert.That(light.Shape, Is.EqualTo(SceneLight.LightShape.Radial));
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>A negative height cannot reach the light — a mark with no size rung reads 0.</summary>
        [Test]
        public void ANegativeLanternHeightIsRefused()
        {
            var go = new GameObject("NavLightPresetProbe2");
            try
            {
                var light = go.AddComponent<SceneLight>();
                NavLightPresets.Apply(light, NavLightColour.White, -5f);
                Assert.That(light.LampHeightMeters, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
