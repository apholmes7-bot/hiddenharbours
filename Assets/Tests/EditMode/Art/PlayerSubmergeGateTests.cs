using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The submersion driver's CONTROL-MODE gate (the owner's "underwater animation on deck" bug): only
    /// ON FOOT does the wade depth drive the player's waterline — on the DECK or at the HELM the fisher
    /// stands on planking above the water, so the body is forced fully dry (a pixel-identical passthrough)
    /// however deep the sea under the hull. A driver-side gate: the shader and the buoys' own waterline
    /// driver are untouched. Pure maths pinned directly; the component's mode tracking driven through its
    /// public bus handler (the established convention — no play-mode lifecycle).
    /// </summary>
    public class PlayerSubmergeGateTests
    {
        private const float Body = 1.8f;
        private const float Cap = 0.85f;

        [Test]
        public void DrivesSubmersion_OnlyOnFoot()
        {
            Assert.IsTrue(PlayerSubmergeMath.DrivesSubmersion(ControlMode.OnFoot), "wading on foot ships as-is");
            Assert.IsFalse(PlayerSubmergeMath.DrivesSubmersion(ControlMode.OnDeck), "on the deck the body is dry");
            Assert.IsFalse(PlayerSubmergeMath.DrivesSubmersion(ControlMode.Aboard), "at the helm too");
        }

        [Test]
        public void GatedDepth_PassesTheRealDepthOnFoot_ForcesDryAboard()
        {
            Assert.AreEqual(1.2f, PlayerSubmergeMath.GatedDepth(ControlMode.OnFoot, 1.2f), 1e-6f,
                "on foot the live wade depth drives the shader unchanged");
            Assert.IsTrue(float.IsNegativeInfinity(PlayerSubmergeMath.GatedDepth(ControlMode.OnDeck, 1.2f)),
                "on deck the depth is forced to the fully-dry sentinel");
            Assert.IsTrue(float.IsNegativeInfinity(PlayerSubmergeMath.GatedDepth(ControlMode.Aboard, 3.5f)),
                "at the helm too");
        }

        [Test]
        public void OnDeckOverDeepWater_TheWaterlineIsZero()
        {
            // The exact composition Tick() uses: deep water under the hull, but the gated depth keeps the
            // waterline at 0 — a pixel-identical passthrough — while the same depth on foot wades for real.
            float deep = 1.2f;
            float onDeck = PlayerSubmergeMath.WaterlineFraction(
                PlayerSubmergeMath.GatedDepth(ControlMode.OnDeck, deep), Body, Cap);
            float onFoot = PlayerSubmergeMath.WaterlineFraction(
                PlayerSubmergeMath.GatedDepth(ControlMode.OnFoot, deep), Body, Cap);

            Assert.AreEqual(0f, onDeck, 1e-6f, "no waterline up the body while standing on the deck");
            Assert.Greater(onFoot, 0.5f, "the SAME depth on foot still wades (the shipped effect is untouched)");
        }

        [Test]
        public void TheDriver_TracksTheControlMode_ThroughTheBusHandler()
        {
            var go = new GameObject("Player");
            try
            {
                var visual = go.AddComponent<PlayerSubmergeVisual>();   // RequireComponent adds the renderer
                Assert.AreEqual(ControlMode.OnFoot, visual.Mode, "boot starts ashore (the switcher's default)");

                visual.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                Assert.AreEqual(ControlMode.OnDeck, visual.Mode, "boarding is seen through the Core signal");

                visual.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                Assert.AreEqual(ControlMode.Aboard, visual.Mode);

                visual.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                Assert.AreEqual(ControlMode.OnFoot, visual.Mode, "stepping ashore re-arms the wade effect");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
