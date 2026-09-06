using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A LIGHT IN HER HAND</b> — world-lighting PR 3 (owner, 2026-09-03: <i>"a latern and a spotlight"</i>),
    /// asserted as RULES rather than as pixels: who owns the switch, who owns the one decor lamp, and what
    /// the two presets mean. The plates are the owner's walk; these are the things a plate cannot show you.
    ///
    /// <para><b>⭐ The fixture this file exists for is <see cref="TheHeadlampWritesTheDecorLamp_WhenTheBoatsStandOff"/>.</b>
    /// The lit-decor path has ONE global lamp, and PR 3 makes every <c>BoatSpotlight</c> stand off it while
    /// she is walking her own beam. Standing them off is only half a handover — and it was the half that
    /// was written. With nothing put in their place the five globals keep whatever a boat wrote last, so
    /// the trees in front of her are lit by a beam frozen at a moored dory's bow, or by nothing at all in a
    /// region with no boat in it. Nothing errors, nothing warns, and no render test on the boat's beam
    /// moves: it is exactly the shape of defect that ships. This asserts the other half.</para>
    /// </summary>
    public class WalkerLightsTests
    {
        private static readonly int IdPos     = Shader.PropertyToID("_BoatLightPos");
        private static readonly int IdDir     = Shader.PropertyToID("_BoatLightDir");
        private static readonly int IdColor   = Shader.PropertyToID("_BoatLightColor");
        private static readonly int IdParams  = Shader.PropertyToID("_BoatLightParams");
        private static readonly int IdParams2 = Shader.PropertyToID("_BoatLightParams2");

        private readonly List<Object> _spawned = new List<Object>();
        private Vector4 _posBefore, _dirBefore, _paramsBefore, _params2Before;
        private Color _colorBefore;

        [SetUp]
        public void SetUp()
        {
            // ⚠️ These five are SHARED global state, and a fixture that leaves them moved poisons every
            // later lighting test in the run — the lesson the shade-receiver fixture is chartered for.
            _posBefore = Shader.GetGlobalVector(IdPos);
            _dirBefore = Shader.GetGlobalVector(IdDir);
            _colorBefore = Shader.GetGlobalColor(IdColor);
            _paramsBefore = Shader.GetGlobalVector(IdParams);
            _params2Before = Shader.GetGlobalVector(IdParams2);
            WalkerLights.ResetInstallForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            WalkerLights.ResetInstallForTests();
            InteractActorProbe.Clear();
            Shader.SetGlobalVector(IdPos, _posBefore);
            Shader.SetGlobalVector(IdDir, _dirBefore);
            Shader.SetGlobalColor(IdColor, _colorBefore);
            Shader.SetGlobalVector(IdParams, _paramsBefore);
            Shader.SetGlobalVector(IdParams2, _params2Before);
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>A headlamp on its own object, configured the way <c>WalkerLights.Build</c> configures
        /// hers, and switched on. (EditMode runs no <c>Awake</c>/<c>OnEnable</c>, which is why nothing here
        /// relies on either: <see cref="Headlamp.Light"/> resolves itself lazily.)</summary>
        private Headlamp LitHeadlamp(Vector2 at, Vector2 facing)
        {
            var go = new GameObject("headlamp");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.up = new Vector3(facing.x, facing.y, 0f);
            var lamp = go.AddComponent<Headlamp>();
            lamp.Configure(WalkerLights.HeadlampLiftMetres);
            lamp.SetOn(true);
            return lamp;
        }

        private static void PutHerIn(ControlMode mode) =>
            InteractActorProbe.Set(InteractActor.For(Vector2.zero, Vector2.up, mode));

        // ---- the switch ---------------------------------------------------------------------------

        /// <summary>
        /// <b>⭐ THE PARTITION, over every member of <see cref="ControlMode"/>.</b> The charter asked that
        /// only one of the two beams answer the key at a time. PR 3 delivers that by CONSTRUCTION rather
        /// than by arbitrating — the walker answers for <c>OnFoot</c>, and the boat answers when the helm
        /// slot says the player is aboard her, which <c>ControlSwitcher</c> makes true for exactly
        /// <c>Aboard</c> and <c>OnDeck</c>. A claim like that is worth exactly as much as the test that
        /// walks the enum.
        ///
        /// <para><b>Why over EVERY member and not over the three interesting ones.</b>
        /// <see cref="ControlMode"/> is append-only Core contract. The next mode appended to it either
        /// belongs to nobody (a beam that answers no key) or to both (two beams on one press), and both
        /// are silent. <c>Driving</c> was the last mode appended and it belongs to NEITHER by design: a
        /// road vehicle's headlights are the road fleet's, not hers.</para>
        /// </summary>
        [Test]
        public void HerBeamAndTheBoatsNeverBothAnswerTheKey_OverEveryControlMode()
        {
            foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
            {
                PutHerIn(mode);

                bool hers = WalkerLights.WalksHerOwnBeam;
                // The boat's half, stated as ControlSwitcher establishes it: the helm slot holds the
                // player's boat at the wheel and on the deck, and nowhere else.
                bool theBoats = mode == ControlMode.Aboard || mode == ControlMode.OnDeck;

                Assert.IsFalse(hers && theBoats,
                    "both beams would answer the key in " + mode + " — one press, two lamps");
                Assert.AreEqual(mode == ControlMode.OnFoot, hers,
                    "the walker's beam must answer in OnFoot and nowhere else; " + mode + " disagreed");
            }

            PutHerIn(ControlMode.Driving);
            Assert.IsFalse(WalkerLights.WalksHerOwnBeam,
                "behind a wheel the beam is the vehicle's problem, not hers");
        }

        /// <summary>
        /// With nobody published at all — a bare art scene, a fixture, the frames before the player is
        /// spawned — she owns nothing. <see cref="InteractActorProbe"/>'s <c>Current</c> is <c>default</c>
        /// then, whose context is <see cref="InteractContext.None"/>; a <c>default</c> that happened to
        /// read as OnFoot would hand her the key in the main menu.
        /// </summary>
        [Test]
        public void WithNoActorPublished_SheOwnsNothing()
        {
            InteractActorProbe.Clear();
            Assert.IsFalse(WalkerLights.WalksHerOwnBeam);
        }

        // ---- the one decor lamp -------------------------------------------------------------------

        /// <summary>
        /// <b>⭐ THE OTHER HALF OF "ONE PUBLISHER".</b> While she owns the decor lamp the boats stand off
        /// it — so she must WRITE it, or the trees hold whatever a boat wrote last. Driven exactly that
        /// way: a boat's numbers go into the globals first, she takes ownership, and the globals must
        /// then be HERS.
        /// </summary>
        [Test]
        public void TheHeadlampWritesTheDecorLamp_WhenTheBoatsStandOff()
        {
            // What a boat last wrote: a lamp somewhere else entirely, pointing the other way.
            var stale = new Vector4(-40f, 12f, 2.5f, 0f);
            Shader.SetGlobalVector(IdPos, stale);
            Shader.SetGlobalVector(IdDir, new Vector4(0f, -1f, 0f, 0f));

            Headlamp lamp = LitHeadlamp(new Vector2(7f, 3f), Vector2.right);
            WalkerLights.SetOwnsDecorLightForTests(true);

            Assert.IsTrue(lamp.PublishDecorLampIfOwned(),
                "she owns the lamp and her beam is lit — she must publish");

            Vector4 pos = Shader.GetGlobalVector(IdPos);
            Vector4 dir = Shader.GetGlobalVector(IdDir);

            Assert.AreNotEqual(stale, pos,
                "the decor lamp is still the boat's — the trees are lit by a beam that is not there");
            Assert.AreEqual(7f, pos.x, 1e-4f, "the lamp is where she is standing");
            Assert.AreEqual(3f, pos.y, 1e-4f);
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, pos.z, 1e-4f,
                "and its height is her brow — the height the water's relief reads");
            Assert.AreEqual(1f, dir.x, 1e-3f, "pointing where she is looking");
            Assert.AreEqual(0f, dir.y, 1e-3f);
            Assert.Greater(Shader.GetGlobalVector(IdParams).x, 0f,
                "and burning: a zero intensity is how a lamp says it is not lit");
        }

        /// <summary>
        /// The negative control, and it asserts its OWN premise: when she does not own the lamp she must
        /// not touch it, or the boats and the walker would fight over it every frame instead of handing
        /// it between them. Without the premise assertion this would also pass if the publish were simply
        /// broken.
        /// </summary>
        [Test]
        public void WhenSheDoesNotOwnTheDecorLamp_SheDoesNotTouchIt()
        {
            Headlamp lamp = LitHeadlamp(new Vector2(7f, 3f), Vector2.right);

            WalkerLights.SetOwnsDecorLightForTests(true);
            Assert.IsTrue(lamp.PublishDecorLampIfOwned(),
                "premise: this same lamp DOES publish when it owns the singleton");
            WalkerLights.SetOwnsDecorLightForTests(false);

            var boats = new Vector4(-40f, 12f, 2.5f, 0f);
            Shader.SetGlobalVector(IdPos, boats);

            Assert.IsFalse(lamp.PublishDecorLampIfOwned(), "not hers — she writes nothing");
            Assert.AreEqual(boats, Shader.GetGlobalVector(IdPos),
                "and the boat's lamp is exactly where the boat left it");
        }

        /// <summary>
        /// An unlit beam publishes nothing even while she owns the flag — the decor lamp cannot be driven
        /// by a lamp that is switched off.
        /// </summary>
        [Test]
        public void AnUnlitHeadlamp_PublishesNothing()
        {
            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            lamp.SetOn(false);
            WalkerLights.SetOwnsDecorLightForTests(true);

            Assert.IsFalse(lamp.PublishDecorLampIfOwned());
        }

        /// <summary>
        /// <b>ONE PACKING.</b> The decor globals and the beam's water-bridge state must be the same
        /// numbers, because they are the same lamp: her beam cannot light a spruce unlike it lights the
        /// sea she is pointing it over. This is what keeps the two publishing routes from drifting apart
        /// the way two hand-written copies of one packing always do.
        /// </summary>
        [Test]
        public void TheDecorLampAndHerWaterLight_AreTheSameNumbers()
        {
            Headlamp lamp = LitHeadlamp(new Vector2(-2f, 5f), new Vector2(0f, -1f));
            WalkerLights.SetOwnsDecorLightForTests(true);

            Assert.IsTrue(lamp.TryGetWaterLight(out WaterLightState water), "her beam lights water too");
            Assert.IsTrue(lamp.PublishDecorLampIfOwned());

            Vector4 pos = Shader.GetGlobalVector(IdPos);
            Vector4 prm = Shader.GetGlobalVector(IdParams);

            Assert.AreEqual(water.LampWorld.x, pos.x, 1e-4f);
            Assert.AreEqual(water.LampWorld.y, pos.y, 1e-4f);
            Assert.AreEqual(water.LampHeightMeters, pos.z, 1e-4f);
            Assert.AreEqual(water.Intensity, prm.x, 1e-4f);
            Assert.AreEqual(water.Range, prm.y, 1e-4f);
            Assert.AreEqual(water.CosHalfAngle, prm.z, 1e-4f);
            Assert.AreEqual(water.CosInnerAngle, prm.w, 1e-4f);
        }

        // ---- the two lamps ------------------------------------------------------------------------

        /// <summary>
        /// The lantern is a FLAME and the headlamp is a BEAM, and the presets have to say so: the one
        /// visible flicker in the library against a steady battery lamp, a radial pool against the only
        /// cone in the library, and a throw that goes further than the pool precisely because it is aimed.
        /// </summary>
        [Test]
        public void TheLanternIsAFlame_AndTheHeadlampIsABeam()
        {
            LightPresets.Config lantern = LightPresets.For(LightPresets.Kind.Lantern);
            LightPresets.Config headlamp = LightPresets.For(LightPresets.Kind.Headlamp);

            Assert.AreEqual(SceneLight.LightShape.Radial, lantern.Shape, "a lantern pools around her");
            Assert.AreEqual(SceneLight.LightShape.Cone, headlamp.Shape, "a headlamp is thrown");

            Assert.Greater(lantern.FlickerAmount, 0f, "a wick behind glass breathes");
            Assert.AreEqual(0f, headlamp.FlickerAmount, "a battery lamp does not");

            Assert.Greater(lantern.Color.r, lantern.Color.b, "flame is warm");
            Assert.Greater(headlamp.Color.b, lantern.Color.b, "an LED is not");

            float lanternReach = LightPresets.ReachMetres(LightPresets.Kind.Lantern);
            float headlampReach = LightPresets.ReachMetres(LightPresets.Kind.Headlamp);
            Assert.Greater(headlampReach, lanternReach,
                "the cone concentrates what the radial spreads — that is what she switches it on FOR");
            Assert.Greater(lanternReach, 0f);
        }

        /// <summary>
        /// Both lamps ride at a height ON HER, and the beam rides higher than the lantern — her brow above
        /// the sling on her back. The heights are what the bloom hangs at (#733) and what the cast shadows
        /// are thrown from, so a lamp left at ground level would pool correctly and glow at her feet.
        /// </summary>
        [Test]
        public void BothLampsRideOnHer_AndTheBeamRidesHigher()
        {
            Assert.Greater(WalkerLights.LanternLiftMetres, 0f, "the lantern is not on the ground");
            Assert.Greater(WalkerLights.HeadlampLiftMetres, WalkerLights.LanternLiftMetres,
                "her brow is above the sling on her back");
            Assert.Less(WalkerLights.HeadlampLiftMetres, 2f, "and both are on a person, not on a post");

            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, lamp.Light.BloomLiftMetres, 1e-4f,
                "the lit fitting hangs at her brow");
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, lamp.Light.LampHeightMeters, 1e-4f,
                "and its shadows are thrown from there");
            Assert.AreEqual(SceneLight.LightShape.Cone, lamp.Light.Shape);
            // The angle comes from the preset library, not from a constant on the walker: one place it is
            // written, and LightPresetsTests' shape guard can assert it there.
            Assert.IsTrue(LightPresets.IsDirected(LightPresets.Kind.Headlamp),
                "the library must declare the headlamp a beam, or its cone is undeclared");
            Assert.AreEqual(LightPresets.ConeHalfDegrees(LightPresets.Kind.Headlamp),
                lamp.Light.ConeHalfAngle, 1e-4f);
        }

        /// <summary>
        /// Stepping aboard and back off again gives her the beam she HAD. <c>SetLive</c> stands the whole
        /// lamp down off her feet, but it must not clear the switch — a light she has to turn on twice is
        /// a light she stops trusting.
        /// </summary>
        [Test]
        public void SteppingAboardAndBackOff_GivesHerTheBeamSheHad()
        {
            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            Assert.IsTrue(lamp.IsOn);

            lamp.SetLive(false);
            Assert.IsTrue(lamp.IsOn, "the switch is hers; the liveness is the mode's");
            Assert.IsFalse(lamp.Light.enabled, "but nothing of hers burns while she is at a wheel");

            lamp.SetLive(true);
            Assert.IsTrue(lamp.Light.enabled, "and it comes back lit, without a second press");
        }
    }
}
