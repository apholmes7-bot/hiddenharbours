using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE PATCH OF GROUND A LAMP MAKES BRIGHTER</b> (ADR 0016 amendment, world-lighting PR 2c) — the
    /// maths headless, the ladder pinned, and the rules about which lamps pool at all.
    ///
    /// <para>#733 took the disc off the SOURCE, on the owner's ruling that a lamp should <i>"glow from within
    /// the lamp"</i>, and left the pier honestly dark. This is the other half: what the lamp does to the
    /// ground. It is a different picture of the same lamp, and the numbers below are what stop it becoming
    /// the disc again by another route.</para>
    /// </summary>
    public class LampPoolTests
    {
        private const float Eps = 1e-4f;

        // ---- the shape: a lamp is a POINT at a HEIGHT ------------------------------------------------

        /// <summary>
        /// <b>Directly under the lamp the ground is struck square-on; out at the rim it is grazed.</b>
        /// <c>h/√(h²+d²)</c> is the cosine between the lamp's ray and the ground's normal, and it is the
        /// whole reason a pool has a shape rather than being a disc: the sun is a direction at infinity and
        /// strikes every pixel of ground alike, a lamp does not.
        /// </summary>
        [Test]
        public void GroundIncidence_IsOneUnderTheLamp_AndFallsAwayWithDistance()
        {
            Assert.AreEqual(1f, LightMath.GroundIncidence(2.5f, 0f), Eps, "straight down is square-on");

            // At d = h the ray is at 45°: exactly 1/√2.
            Assert.AreEqual(0.70710678f, LightMath.GroundIncidence(2.5f, 2.5f), Eps);
            Assert.AreEqual(0.70710678f, LightMath.GroundIncidence(7.8f, 7.8f), Eps, "scale-free, as a cosine is");

            float prev = float.MaxValue;
            for (float d = 0f; d <= 12f; d += 0.5f)
            {
                float v = LightMath.GroundIncidence(3f, d);
                Assert.LessOrEqual(v, prev + Eps, $"incidence must never rise with distance (d={d})");
                prev = v;
            }
            Assert.Less(LightMath.GroundIncidence(3f, 100f), 0.05f, "far away, a lamp barely grazes the ground");
        }

        /// <summary>
        /// <b>A TALL lamp pools broad and even; a LOW one pools tight and drops away.</b> Nothing tunes this —
        /// it is the same one line — and it is what makes a 7.8 m flood mast and a 2.46 m lantern post read
        /// as different KINDS of light rather than as the same disc at two sizes.
        /// </summary>
        [Test]
        public void ATallLampPoolsFlatter_AndALowOneDropsAwayFaster()
        {
            const float d = 3f;
            float lantern = LightMath.GroundIncidence(2.46f, d);   // wharfDecor lanternPost
            float mast = LightMath.GroundIncidence(7.8f, d);       // utilityIso floodMast

            Assert.Greater(mast, lantern,
                "at the same ground distance the taller lamp strikes more squarely — that is the whole " +
                "difference between a mast lighting a yard and a lantern lighting its own feet");

            // And the drop-off over the same span is steeper for the low lamp.
            float lanternDrop = LightMath.GroundIncidence(2.46f, 1f) - LightMath.GroundIncidence(2.46f, 4f);
            float mastDrop = LightMath.GroundIncidence(7.8f, 1f) - LightMath.GroundIncidence(7.8f, 4f);
            Assert.Greater(lanternDrop, mastDrop, "the low lamp's pool has the harder edge, from geometry alone");
        }

        /// <summary>
        /// <b>A lamp that will not say how high it is draws NOTHING.</b> Zero is not "assume 2.5 m" and it is
        /// certainly not "draw a flat disc" — a disc is precisely the picture the owner refused, and falling
        /// back to it on missing data is how it would come back.
        /// </summary>
        [Test]
        public void ALampWithNoHeight_HasNoOpinionAboutTheGround()
        {
            Assert.AreEqual(0f, LightMath.GroundIncidence(0f, 0f), Eps);
            Assert.AreEqual(0f, LightMath.GroundIncidence(0f, 3f), Eps);
            Assert.AreEqual(0f, LightMath.PoolGain(baseGain: 5f, lampHeightMetres: 0f,
                                                   groundDistanceMetres: 0f, reachMetres: 3.6f,
                                                   edgeSoftness: 0.5f), Eps,
                "no height ⇒ no gain anywhere, at any brightness");
        }

        // ---- the edge and the gate --------------------------------------------------------------------

        [Test]
        public void PoolFalloff_IsOneInsideTheSoftBand_AndZeroAtTheReach()
        {
            // softness 0.5 on a 4 m reach: full out to 2 m, gone by 4 m.
            Assert.AreEqual(1f, LightMath.PoolFalloff(0f, 4f, 0.5f), Eps);
            Assert.AreEqual(1f, LightMath.PoolFalloff(2f, 4f, 0.5f), Eps);
            Assert.AreEqual(0f, LightMath.PoolFalloff(4f, 4f, 0.5f), Eps, "a pool ends at its reach");
            Assert.AreEqual(0f, LightMath.PoolFalloff(40f, 4f, 0.5f), Eps);

            float mid = LightMath.PoolFalloff(3f, 4f, 0.5f);
            Assert.Greater(mid, 0f); Assert.Less(mid, 1f);

            // A hard edge is available and is what a decal looks like; the shipped profile does not use it.
            Assert.AreEqual(1f, LightMath.PoolFalloff(3.99f, 4f, 0f), Eps, "softness 0 = a hard rim");
        }

        /// <summary>
        /// <b>The night gate is a factor, so a pool is a night thing by the same machinery every other light
        /// in the project gates on.</b> No preset, no profile and no pool may reach around it.
        /// </summary>
        [Test]
        public void PoolGain_IsGatedByTheNight_AndCappedSoItCannotFlattenTheGround()
        {
            const float night = 0.134f;   // the shipped 02:00 tint's luminance, measured on the pier
            float lit = LightMath.PoolBaseGain(0.6f, 1.3f, nightGate: 1f, tintLuminance: night);
            Assert.Greater(lit, 0f);
            Assert.AreEqual(0f, LightMath.PoolBaseGain(0.6f, 1.3f, 0f, night), Eps,
                "by day a lamp does nothing to the ground");
            Assert.AreEqual(lit * 0.5f, LightMath.PoolBaseGain(0.6f, 1.3f, 0.5f, night), Eps,
                "and it fades through dusk rather than switching");

            Assert.LessOrEqual(LightMath.PoolBaseGain(1f, 100f, 1f, 0.001f), LightMath.MaxPoolGain + Eps,
                "a runaway intensity over a near-black tint must not be able to clip the ground to white — " +
                "that is the flattening the additive disc was refused for, arriving through the other door");
        }

        /// <summary>
        /// <b>⚠⚠ THE DIVISION BY THE NIGHT IS WHAT MAKES THE POOL VISIBLE AT ALL, and leaving it out
        /// produced a pass that was working and invisible.</b> A multiply is bounded by what it multiplies,
        /// and ADR 0013's tint has crushed the pier to a mean luminance around 0.04 by the time the pool
        /// draws — so a naive <c>dst × 1.6</c> lifts a plank by six values in 255. Measured on the real pier
        /// before this existed: ZERO pixels changed.
        ///
        /// <para>The factor that reconstructs <c>albedo × (ambient + lamp)</c> from a frame holding
        /// <c>albedo × ambient</c> is exactly <c>1 + lamp/ambient</c> — so the DARKER the night, the LARGER
        /// the multiplier, and the lit ground lands in the same place either way. That is the property this
        /// pins.</para>
        /// </summary>
        [Test]
        public void TheGain_RisesAsTheNightDeepens_SoTheLitGroundLandsInTheSamePlace()
        {
            const float strength = 0.6f, intensity = 1.3f, albedo = 0.5f;
            float dusk = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.40f);
            float night = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.134f);
            float moonless = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.04f);

            Assert.Less(dusk, night, "a deeper night needs a bigger multiplier to reach the same light");
            Assert.Less(night, moonless);

            // ⭐ THE INVARIANT is the LAMP'S OWN CONTRIBUTION — albedo × tint × gain — which the division
            // makes independent of the tint: it comes out as albedo × strength × intensity, every time.
            // (What the pool sits IN still changes with the night, and should: dusk has more ambient in it.)
            float LampAdds(float tint, float gain) => albedo * tint * gain;
            Assert.AreEqual(albedo * strength * intensity, LampAdds(0.40f, dusk), 1e-4f);
            Assert.AreEqual(albedo * strength * intensity, LampAdds(0.134f, night), 1e-4f,
                "the lamp puts the same light on the same planks at dusk and at midnight — the night " +
                "changes what is AROUND the pool, not the pool");

            // ⚠ And the CAP deliberately breaks that identity in the dark, which is the trade it exists to
            // make: past MaxPoolGain a lamp stops reaching for a factor of fifty and settles for eight.
            Assert.AreEqual(LightMath.MaxPoolGain, moonless, Eps, "a moonless night hits the ceiling");
            Assert.Less(LampAdds(0.04f, moonless), albedo * strength * intensity,
                "so a moonless pool IS dimmer than a moonlit one — stated, because the alternative is a " +
                "runaway multiplier clipping the ground to white");

            Assert.AreEqual(LightMath.PoolBaseGain(strength, intensity, 1f, LightMath.MinPoolAmbientLuminance),
                            LightMath.PoolBaseGain(strength, intensity, 1f, 0f), Eps,
                "a tint of zero is a frame with no cycle running, not an infinitely dark one — the floor " +
                "stops the division exploding");
        }

        /// <summary>
        /// <b>⭐ THE CLAIM THE WHOLE DESIGN RESTS ON: a multiply cannot flatten what it lights.</b> The pass
        /// is <c>Blend DstColor One</c>, i.e. <c>dst × (1 + gain)</c>. Relative contrast is a RATIO — a step
        /// between neighbours over the local mean — and a uniform scale multiplies both of its terms, so it
        /// cancels exactly. Two planks and the dark gap between them come back in the same proportion they
        /// went in, however bright the pool. An additive term cannot make that promise at any strength that
        /// reads, and that difference is the entire reason this is not the disc again.
        /// </summary>
        [Test]
        public void AMultiplyPreservesContrast_WhichIsWhyThePlanksSurviveIt()
        {
            // A scrap of plank: light board, dark seam, light board.
            float[] ground = { 0.42f, 0.11f, 0.44f, 0.10f, 0.40f };
            float baseGain = LightMath.PoolBaseGain(0.6f, 1.3f, 1f, 0.134f);
            float gain = LightMath.PoolGain(baseGain, 2.5f, 0.5f, 3.6f, 0.55f);
            Assert.Greater(gain, 0.1f, "the fixture needs a pool that actually lights, or it proves nothing");

            float ContrastOf(float[] v)
            {
                float step = 0f, mean = 0f;
                for (int i = 0; i < v.Length; i++)
                {
                    mean += v[i];
                    if (i > 0) step += Mathf.Abs(v[i] - v[i - 1]);
                }
                return step / mean;
            }

            var lit = new float[ground.Length];
            for (int i = 0; i < ground.Length; i++) lit[i] = ground[i] * (1f + gain);

            Assert.AreEqual(ContrastOf(ground), ContrastOf(lit), 1e-5f,
                "the multiply must leave relative local contrast EXACTLY where it found it");

            // The additive arm, for contrast — the same light energy added instead of scaled.
            var added = new float[ground.Length];
            for (int i = 0; i < ground.Length; i++) added[i] = ground[i] + gain * 0.3f;
            Assert.Less(ContrastOf(added), ContrastOf(ground) * 0.85f,
                "and an ADDITIVE term of the same order flattens it — which is what the owner was looking at");
        }

        // ---- the ladder ---------------------------------------------------------------------------------

        /// <summary>
        /// <b>The three lamp quads draw in the only order that composes.</b> All three sit at the same
        /// sorting order, so the 2D renderer breaks the tie back-to-front along the view axis and the DEPTH
        /// PINS are the whole ordering. Farther draws first:
        /// <list type="number">
        ///   <item>the POOL multiplies the ground up;</item>
        ///   <item>the BLOOM adds the lit fitting, so the lamp itself stays the hottest thing in frame;</item>
        ///   <item>the SHADOWS multiply back down, so a bollard's shadow is cut into the light the pool just
        ///         laid — which is what makes them one picture instead of two.</item>
        /// </list>
        /// Get this backwards and a shadow lands under the pool that is supposed to erase it.
        /// </summary>
        [Test]
        public void TheDepthPins_PutThePoolUnderTheBloom_AndTheShadowsOverBoth()
        {
            Assert.Greater(LampPoolSystem.PoolDepthOffset, SceneLight.DefaultCameraDepthOffset,
                "the pool must be FARTHER from the camera than the bloom, so it draws first");
            Assert.Greater(SceneLight.DefaultCameraDepthOffset, LampShadowSystem.ShadowDepthOffset,
                "and the shadows nearest of all, so they draw last and multiply both");

            Assert.AreEqual(0.14f, LampPoolSystem.PoolDepthOffset, 1e-6f);
        }

        // ---- which lamps pool at all --------------------------------------------------------------------

        /// <summary>
        /// <b>A lamp pools only if it says it lights the ground AND says from how high.</b> Both are real
        /// answers rather than missing ones: a boat's sidelight is a SIGNAL and lighting the sea under it
        /// would be a lie about what it is for, and a lamp with no height cannot be shaped.
        /// </summary>
        [Test]
        public void OnlyALampThatClaimsAReachAndAHeight_CastsAPool()
        {
            var go = new GameObject("poolCandidate") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();
                light.Intensity = 1f;
                light.LampHeightMeters = 2.5f;
                light.ReachMetres = 3.6f;

                Assert.IsTrue(LampPoolSystem.PoolsLight(light, 0.1f, true, out float gate));
                Assert.Greater(gate, 0f);

                light.ReachMetres = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _),
                    "no reach ⇒ no pool: a sidelight is a signal, not a floodlight");

                light.ReachMetres = 3.6f;
                light.LampHeightMeters = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _),
                    "no height ⇒ no pool, rather than a flat disc");

                light.LampHeightMeters = 2.5f;
                light.Intensity = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _));

                light.Intensity = 1f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 1f, true, out _),
                    "and by day the night gate closes it, like every other light in the project");

                Assert.IsFalse(LampPoolSystem.PoolsLight(null, 0.1f, true, out _), "null is not a lamp");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>A placed lamp carries its REACH into the runtime, not just into the siting.</b> #733 split the
        /// bloom from the pool in the preset LIBRARY, where the builders could read it; the thing that draws
        /// a pool is handed a <see cref="SceneLight"/> and never a preset kind, so the reach has to make the
        /// crossing. Without this the split would exist and nothing would be able to use it.
        /// </summary>
        [Test]
        public void ApplyingAPreset_CarriesTheReachOntoTheLight_NotJustTheBloom()
        {
            var go = new GameObject("reachCarry") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();

                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Lightpost), light.ReachMetres, Eps,
                    "the lamp post lights 3.6 m of ground");
                Assert.AreEqual(LightPresets.For(LightPresets.Kind.Lightpost).Range, light.Range, Eps,
                    "and blooms at its 0.40 m lantern — the two are different numbers and both arrive");

                LightPresets.ApplyFitting(light, LightPresets.Kind.Floodlight, 1.49f);
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Floodlight), light.ReachMetres, Eps,
                    "and a per-piece fitting overrides the BLOOM without disturbing the reach");
                Assert.AreEqual(1.49f, light.Range, Eps);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>A boat's lamps light no ground, and that is a decision.</b> <c>BoatLampPresets</c> stamps
        /// through <c>LightPresets.Stamp</c>, which writes a <c>Config</c> — and a <c>Config</c> carries the
        /// bloom only. So every navigation lamp leaves its reach at zero and casts no pool: a sidelight is
        /// how another skipper reads your aspect, and a red pool on the sea under it would be a lie about
        /// what the lamp is for.
        /// </summary>
        [Test]
        public void ABoatsNavigationLamps_CastNoPoolOnTheSea()
        {
            var go = new GameObject("navLamp") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();
                light.ReachMetres = 99f;                    // prove the stamp is what leaves it alone
                BoatLampPresets.Apply(light, HiddenHarbours.Core.HullLampKind.PortSidelight);

                Assert.AreEqual(99f, light.ReachMetres, Eps,
                    "Stamp writes the Config and nothing else — a boat lamp's reach is simply never set, " +
                    "so it stays whatever it was, and on a freshly added light that is zero");

                var fresh = new GameObject("freshNav") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var l2 = fresh.AddComponent<SceneLight>();
                    BoatLampPresets.Apply(l2, HiddenHarbours.Core.HullLampKind.Masthead);
                    Assert.AreEqual(0f, l2.ReachMetres, Eps, "a masthead lights the night, not the water");
                    Assert.IsFalse(LampPoolSystem.PoolsLight(l2, 0.1f, true, out _));
                }
                finally { Object.DestroyImmediate(fresh); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- the dial ------------------------------------------------------------------------------------

        /// <summary>
        /// <b>The way back is one field.</b> The pool makes a trade — a screen-space multiply brightens
        /// whatever occupies the pixel, including something passing OVER a pool rather than standing in it —
        /// and the owner has to be able to refuse it without a build. Pinned at the shipped values so a
        /// change to them is deliberate.
        /// </summary>
        [Test]
        public void TheProfileShipsThePoolOn_WithTheWayBackAtOneField()
        {
            var p = LampShadowProfile.CreateDefault();
            try
            {
                Assert.IsTrue(p.PoolsEnabled, "the owner asked for the ground to be lit");
                Assert.AreEqual(0.6f, p.PoolStrength, Eps);
                Assert.AreEqual(0.55f, p.PoolEdgeSoftness, Eps);
                Assert.AreEqual(8, p.MaxPools);

                p.PoolsEnabled = false;
                Assert.IsFalse(p.PoolsEnabled, "and can turn it off again");
                p.PoolStrength = 5f;
                Assert.AreEqual(1f, p.PoolStrength, Eps, "clamped: the ground may double, not quintuple");
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
