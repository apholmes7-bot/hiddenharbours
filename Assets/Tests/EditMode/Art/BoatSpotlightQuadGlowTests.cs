using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The searchlight's additive quad is a SOURCE glow, not the illumination</b> — the owner's ruling of
    /// 2026-09-04, pinned.
    ///
    /// <para>Playing the St Peters arrival at 06:13 he said: <i>"spotlight doesnt read on water or
    /// enviroement its just a flat white"</i>. That is a description of the mechanism, not of a bug. #691
    /// built the honest illumination — the water shader lights each pixel by N·L against the wave field's
    /// own normal, so crests catch the beam and troughs fall into shadow — and then ADR 0016's additive
    /// quad, thrown at the beam's FULL range, lays a flat amber cone over the top of it. The same light,
    /// added twice, and the flat copy is the brighter one. So the sea under the beam goes featureless.</para>
    ///
    /// <para><see cref="BoatSpotlight.DefaultQuadGlowScale"/> pulls the quad back to the lamp and leaves the
    /// throw to the water's own term. The dial has existed since #691 and its tooltip named this number all
    /// along; what it lacked was a plate and the owner's eye, which it now has
    /// (<c>docs/art/spikes/lights-are-sources/</c>).</para>
    ///
    /// <para><b>⚠ What this canNOT assert.</b> A C# default cannot reach a field a scene already serialized:
    /// <c>StPeters.unity</c> carries <c>_quadGlowScale: 1</c> on the arrival cape's spotlight, and the
    /// owner's next St Peters Build is what lands 0.3 there. A test that asserted the scene's value would
    /// go red the moment he built, so the scene is named in the PR body instead of guarded here.</para>
    /// </summary>
    public class BoatSpotlightQuadGlowTests
    {
        /// <summary>
        /// The shipped fraction is 0.3 — a 2.7 m bloom on a 9 m beam. Pinned as a number rather than as an
        /// inequality because it is a LOOK the owner ruled on: the next change to it should be one somebody
        /// meant, shot against the same plate.
        /// </summary>
        [Test]
        public void TheQuadGlowScale_IsTheRuledFraction_NotTheFullThrow()
        {
            Assert.AreEqual(0.3f, BoatSpotlight.DefaultQuadGlowScale, 1e-6f,
                "the value the dial's own tooltip has named since #691, now measured on a 06:13 plate");

            Assert.Less(BoatSpotlight.DefaultQuadGlowScale, 1f,
                "1 is the look the owner refused — the full-length flat cone over the water's own relief");
            Assert.GreaterOrEqual(BoatSpotlight.DefaultQuadGlowScale, 0.05f,
                "and the dial's own floor: below this the lamp stops reading as a source at all");
        }

        /// <summary>
        /// <b>The serialized field carries the const, so a fresh spotlight ships the ruled look.</b> The
        /// const alone would be a number nothing reads; this is the half that decides what a
        /// newly-placed <see cref="BoatSpotlight"/> — and therefore every hull the shipwright mints from a
        /// def — actually draws.
        /// </summary>
        [Test]
        public void AFreshSpotlight_SerializesTheRuledFraction()
        {
            var go = new GameObject("quadGlowDefault") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var spot = go.AddComponent<BoatSpotlight>();

                FieldInfo f = typeof(BoatSpotlight).GetField(
                    "_quadGlowScale", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(f,
                    "BoatSpotlight._quadGlowScale is the serialized dial the owner's ruling lands on; if it " +
                    "was renamed, every scene that already serialized it silently keeps the old look");

                Assert.AreEqual(BoatSpotlight.DefaultQuadGlowScale, (float)f.GetValue(spot), 1e-6f,
                    "the field initializer must be the const, or the two can drift and the const becomes " +
                    "documentation of a look nothing draws");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>The WATER keeps the full throw.</b> This is the half of the trade that makes the dial honest
        /// rather than just a shorter beam: the quad is the lamp's own bloom, and the 9 m of lit sea is
        /// published to the water shader independently of it. If the two ever came off one number, dialling
        /// the bloom back would shorten the beam itself and the owner would have traded a flat wedge for a
        /// stub.
        /// </summary>
        [Test]
        public void TheWaterThrow_IsIndependentOfTheQuad()
        {
            FieldInfo range = typeof(BoatSpotlight).GetField(
                "_range", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(range);

            var go = new GameObject("quadGlowThrow") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var spot = go.AddComponent<BoatSpotlight>();
                Assert.AreEqual(BoatSpotlight.DefaultRangeMetres, (float)range.GetValue(spot), 1e-6f,
                    "the beam's own throw is untouched by the bloom ruling — 9 m of lit water, as shipped");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
