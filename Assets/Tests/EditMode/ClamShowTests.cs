using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The bar clam's show (owner ask 2026-11-22): two small siphon holes drawn straight onto the ground,
    /// and a squirt that is the main giveaway. Pins the tuned timing (still deterministic, still the rig's
    /// hash), the frame pick (height ramp plus a collapse frame), the startle squirt when the player steps
    /// close, the jet-height dial keeping its foot on the holes, the shipped config, and the art's own
    /// contract (no mound, no ground colour, boot-sized pair).
    /// </summary>
    public sealed class ClamShowTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string HolePng = "Assets/_Project/Art/Sprites/ClamHole.png";
        const string SquirtPng = "Assets/_Project/Art/Sprites/ClamSquirt.png";

        readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- timing ---------------------------------------------------------------------------------

        [Test]
        public void TheTunedRoll_AtTheRigsNumbers_IsTheRigRoll()
        {
            for (int i = 0; i < 50; i++)
            {
                float x = -40f + i * 1.37f, y = 12f - i * 0.71f;
                ClamSpurtMath.TimingFor(1234, x, y, out float p0, out float t0);
                ClamSpurtMath.TimingFor(1234, x, y, ClamSpurtMath.MinPeriodMilliseconds,
                                        ClamSpurtMath.MaxPeriodMilliseconds,
                                        ClamSpurtMath.PhaseSpanMilliseconds, out float p1, out float t1);
                Assert.AreEqual(p0, p1, 0f, "same phase at the rig's span");
                Assert.AreEqual(t0, t1, 0f, "same period at the rig's span");
            }
        }

        [Test]
        public void ATunedSpan_KeepsEveryHoleInsideIt_AndIsDeterministic()
        {
            for (int i = 0; i < 200; i++)
            {
                float x = i * 0.53f, y = i * -0.29f;
                ClamSpurtMath.TimingFor(77, x, y, 1000f, 2000f, 500f, out float phase, out float period);
                Assert.That(period, Is.InRange(1000f, 2000f));
                Assert.That(phase, Is.InRange(0f, 500f));
                ClamSpurtMath.TimingFor(77, x, y, 1000f, 2000f, 500f, out float phase2, out float period2);
                Assert.AreEqual(phase, phase2, 0f);
                Assert.AreEqual(period, period2, 0f);
            }

            // A max typed below the min reads as the min, never a negative period.
            ClamSpurtMath.TimingFor(77, 1f, 1f, 3000f, 100f, -5f, out float ph, out float pe);
            Assert.AreEqual(3000f, pe, 0f);
            Assert.AreEqual(0f, ph, 0f);
        }

        [Test]
        public void ALongerSquirt_StaysOpenLonger_OnTheRigsPhaseConvention()
        {
            const float period = 5000f, phase = 1000f;
            double open = period - phase;    // the rig's convention: opens at period - phase
            Assert.IsTrue(ClamSpurtMath.TrySpurt(open + 500.0, phase, period, 600f, out float r, out float u));
            Assert.Greater(r, 0f);
            Assert.AreEqual(500f / 600f, u, 1e-4f);
            Assert.IsFalse(ClamSpurtMath.TrySpurt(open + 500.0, phase, period, out _, out _),
                           "the rig's 420 ms window has closed by then");
            Assert.IsFalse(ClamSpurtMath.TrySpurt(open + 601.0, phase, period, 600f, out r, out u));
            Assert.AreEqual(0f, r, 0f, "closed means zero rise, not a stuck jet");
        }

        [Test]
        public void TheWindow_IsZeroOutsideAndASineInside()
        {
            Assert.IsFalse(ClamSpurtMath.TryWindow(-1.0, 600f, out float r, out _));
            Assert.AreEqual(0f, r, 0f);
            Assert.IsTrue(ClamSpurtMath.TryWindow(300.0, 600f, out r, out float u));
            Assert.AreEqual(1f, r, 1e-5f, "the crest at mid-window");
            Assert.AreEqual(0.5f, u, 1e-6f);
            Assert.IsFalse(ClamSpurtMath.TryWindow(10.0, 0f, out _, out _), "a zero-length squirt is quiet");
        }

        // ---- the frame pick -------------------------------------------------------------------------

        [Test]
        public void FourFrames_RampUpToTheCrest_HoldIt_ThenCollapse()
        {
            const float collapse = 0.6f;
            Assert.AreEqual(0, ClamSpurtMath.FrameFor(0f, 0f, 4, collapse), "the lip as it opens");
            Assert.AreEqual(1, ClamSpurtMath.FrameFor(0.5f, 0.17f, 4, collapse), "half height rising");
            Assert.AreEqual(2, ClamSpurtMath.FrameFor(1f, 0.5f, 4, collapse), "the crest at the top");
            Assert.AreEqual(2, ClamSpurtMath.FrameFor(0.8f, 0.7f, 4, collapse), "the crest holds on the way down");
            Assert.AreEqual(3, ClamSpurtMath.FrameFor(0.5f, 0.83f, 4, collapse), "then the collapse");
            Assert.AreEqual(3, ClamSpurtMath.FrameFor(0f, 1f, 4, collapse), "the collapse to the end");

            // Rising never shows the collapse frame.
            for (float u = 0f; u < 0.5f; u += 0.01f)
                Assert.Less(ClamSpurtMath.FrameFor(Mathf.Sin(Mathf.PI * u), u, 4, collapse), 3);
        }

        [Test]
        public void ShortStrips_AreAPlainHeightRamp_AndAnEmptyStripDrawsNothing()
        {
            Assert.AreEqual(0, ClamSpurtMath.FrameFor(1f, 0.5f, 1, 0.6f));
            Assert.AreEqual(1, ClamSpurtMath.FrameFor(1f, 0.5f, 2, 0.6f));
            Assert.AreEqual(0, ClamSpurtMath.FrameFor(0.2f, 0.9f, 2, 0.6f));
            Assert.AreEqual(-1, ClamSpurtMath.FrameFor(1f, 0.5f, 0, 0.6f));
            Assert.AreEqual(0, ClamSpurtMath.FrameFor(float.NaN, 0.1f, 4, 0.6f), "NaN draws the lip, never throws");
        }

        // ---- settings -------------------------------------------------------------------------------

        [Test]
        public void TheSettings_HealASpanThatCouldLeaveTheJetStanding()
        {
            var s = ClamShowSettings.Default;
            s.SquirtSeconds = 2f;
            s.MinIntervalSeconds = 0.5f;   // shorter than the squirt
            s.MaxIntervalSeconds = 0.2f;   // and below the min
            s.PhaseSpanSeconds = -3f;
            Assert.Greater(s.MinIntervalMilliseconds, s.SquirtMilliseconds);
            Assert.GreaterOrEqual(s.MaxIntervalMilliseconds, s.MinIntervalMilliseconds);
            Assert.AreEqual(0f, s.PhaseSpanMilliseconds, 0f);
        }

        [Test]
        public void TheShippedConfig_CarriesTheClamShow_AtItsDefaults()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, $"no GameConfig at {ConfigPath}");
            var shipped = config.ClamShow;
            var d = ClamShowSettings.Default;
            Assert.AreEqual(d.SquirtSeconds, shipped.SquirtSeconds, 1e-6f);
            Assert.AreEqual(d.MinIntervalSeconds, shipped.MinIntervalSeconds, 1e-6f);
            Assert.AreEqual(d.MaxIntervalSeconds, shipped.MaxIntervalSeconds, 1e-6f);
            Assert.AreEqual(d.PhaseSpanSeconds, shipped.PhaseSpanSeconds, 1e-6f);
            Assert.AreEqual(d.JetHeightScale, shipped.JetHeightScale, 1e-6f);
            Assert.AreEqual(d.CollapseBelowRise, shipped.CollapseBelowRise, 1e-6f);
            Assert.AreEqual(d.StartleRadiusMetres, shipped.StartleRadiusMetres, 1e-6f);
            Assert.AreEqual(d.StartleCooldownSeconds, shipped.StartleCooldownSeconds, 1e-6f);
            Assert.Greater(shipped.SquirtSeconds, ClamSpurtMath.SpurtMilliseconds / 1000f,
                           "held longer than the rig's 420 ms so it reads at the game lens");
        }

        // ---- the startle ----------------------------------------------------------------------------

        // A quiet hole: one periodic squirt every 1000 s with no phase spread, so after the first 2 s
        // anything that shows is the startle.
        ClamDig QuietHoleWithStartle(float radius, float cooldown)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            var s = ClamShowSettings.Default;
            s.SquirtSeconds = 0.6f;
            s.MinIntervalSeconds = 1000f;
            s.MaxIntervalSeconds = 1000f;
            s.PhaseSpanSeconds = 0f;
            s.StartleRadiusMetres = radius;
            s.StartleCooldownSeconds = cooldown;
            config.ClamShow = s;
            GameServices.Config = config;

            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            var dig = go.AddComponent<ClamDig>();
            dig.Configure(null, null, go.transform, "gear.shovel", 7);
            Reveal(dig, 2f);                                   // past the periodic window at t = 0
            Assert.IsFalse(dig.ShowingSquirt, "the quiet hole is quiet");
            return dig;
        }

        static readonly MethodInfo UpdateReveal = typeof(ClamDig).GetMethod("UpdateReveal",
            BindingFlags.NonPublic | BindingFlags.Instance);

        static void Reveal(ClamDig dig, float dt) => UpdateReveal.Invoke(dig, new object[] { dt, true });

        void PlayerAt(Vector2 pos)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.transform.position = pos;
            var digger = go.AddComponent<ClamDigger>();
            digger.Configure(go.transform);
            digger.PublishPlayerPosition();
        }

        [Test]
        public void SteppingClose_SquirtsTheClamAtOnce()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);

            PlayerAt(new Vector2(6f, 0f));
            Reveal(dig, 0.1f);
            Assert.IsFalse(dig.ShowingSquirt, "6 m off is outside the 3 m startle radius");

            PlayerAt(new Vector2(2f, 0f));
            Reveal(dig, 0.016f);
            Assert.IsTrue(dig.ShowingSquirt, "stepping within 3 m squirts the clam");
            Assert.IsTrue(dig.SquirtIsStartle);

            Reveal(dig, 0.284f);                               // to the middle of the 0.6 s squirt
            Assert.Greater(dig.SpurtRise, 0.9f, "the startle jet crests like any other");

            Reveal(dig, 0.4f);                                 // past its end
            Assert.IsFalse(dig.ShowingSquirt, "one squirt, not a fountain");

            for (int i = 0; i < 20; i++) Reveal(dig, 0.1f);
            Assert.IsFalse(dig.ShowingSquirt, "standing there does not keep squirting it");
        }

        [Test]
        public void TheStartle_WaitsOutItsCooldown()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            PlayerAt(new Vector2(1f, 0f));
            Reveal(dig, 0.016f);
            Assert.IsTrue(dig.SquirtIsStartle);
            Reveal(dig, 1f);

            PlayerAt(new Vector2(8f, 0f)); Reveal(dig, 0.1f);
            PlayerAt(new Vector2(1f, 0f)); Reveal(dig, 0.016f);
            Assert.IsFalse(dig.ShowingSquirt, "out and back in within 5 s: no second startle");

            PlayerAt(new Vector2(8f, 0f));
            Reveal(dig, 4f);
            PlayerAt(new Vector2(1f, 0f)); Reveal(dig, 0.016f);
            Assert.IsTrue(dig.ShowingSquirt, "after the cooldown, stepping in squirts it again");
        }

        [Test]
        public void StartleRadius_IncludesItsBoundary()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            PlayerAt(new Vector2(3.001f, 0f));
            Reveal(dig, 0.1f);
            Assert.IsFalse(dig.ShowingSquirt);
            PlayerAt(new Vector2(3f, 0f));
            Reveal(dig, 0.1f);
            Assert.IsTrue(dig.ShowingSquirt);
            Assert.IsTrue(dig.SquirtIsStartle);
        }

        [Test]
        public void SpentHole_ClearsEveryLiveSignRead()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            PlayerAt(Vector2.zero);
            Reveal(dig, 0.1f);
            Reveal(dig, 0.2f);
            Assert.IsTrue(dig.ShowingSquirt);
            dig.MarkConsumed();
            Reveal(dig, 0.1f);
            Assert.IsFalse(dig.ShowingSquirt);
            Assert.IsFalse(dig.SquirtIsStartle);
            Assert.AreEqual(0f, dig.SpurtRise);
            Assert.AreEqual(0f, dig.SpurtU);
        }

        [Test]
        public void SubmergedHole_ClearsTheSignWithoutConsumingTheClam()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            PlayerAt(Vector2.zero);
            Reveal(dig, 0.1f);
            Assert.IsTrue(dig.ShowingSquirt);
            UpdateReveal.Invoke(dig, new object[] { 0.1f, false });
            Assert.IsFalse(dig.ShowingSquirt);
            Assert.IsFalse(dig.SquirtIsStartle);
            Assert.AreEqual(0f, dig.SpurtRise);
            Assert.AreEqual(0f, dig.SpurtU);
            Assert.IsFalse(dig.Consumed);
        }

        [Test]
        public void ARadiusOfZero_TurnsTheStartleOff()
        {
            var dig = QuietHoleWithStartle(radius: 0f, cooldown: 5f);
            PlayerAt(new Vector2(8f, 0f)); Reveal(dig, 0.1f);
            PlayerAt(new Vector2(0f, 0f)); Reveal(dig, 0.016f);
            Assert.IsFalse(dig.ShowingSquirt);
        }

        // ---- the visual -----------------------------------------------------------------------------

        Sprite CellSprite(float pixelsPerUnit)
        {
            var tex = new Texture2D(32, 32);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            _spawned.Add(s);
            return s;
        }

        [Test]
        public void TheSquirt_IsDrawnEveryFrame_NotOnTheHolesThrottle()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            var frames = new[] { CellSprite(32f), CellSprite(32f), CellSprite(32f), CellSprite(32f) };
            var visual = dig.gameObject.AddComponent<ClamHoleVisual>();
            visual.Configure(dig, CellSprite(32f), frames);

            // The holes need a bared flat to show; give the visual a flat it can see.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 1f };
            GameServices.Environment = new FlatEnv { Level = 0.5f };
            visual.Tick(0.016f);
            Assert.IsTrue(visual.HoleRenderer.enabled);
            Assert.IsFalse(visual.SquirtRenderer.enabled);

            PlayerAt(new Vector2(1f, 0f));
            Reveal(dig, 0.016f);
            visual.Tick(0.016f);                               // inside the 0.1 s holes throttle
            Assert.IsTrue(visual.SquirtRenderer.enabled, "the jet appears the frame it starts");
            Assert.AreSame(frames[0], visual.SquirtRenderer.sprite, "the lip first");

            Reveal(dig, 0.284f);
            visual.Tick(0.016f);
            Assert.AreSame(frames[2], visual.SquirtRenderer.sprite, "the crest at the top");

            Reveal(dig, 0.2f);
            visual.Tick(0.016f);
            Assert.AreSame(frames[3], visual.SquirtRenderer.sprite, "the collapse on the way down");
        }

        [Test]
        public void TheHeightDial_StretchesTheJet_WithItsFootOnTheHoles()
        {
            var dig = QuietHoleWithStartle(radius: 3f, cooldown: 5f);
            var s = GameServices.Config.ClamShow;
            s.JetHeightScale = 2f;
            GameServices.Config.ClamShow = s;
            var frames = new[] { CellSprite(32f) };
            var visual = dig.gameObject.AddComponent<ClamHoleVisual>();
            visual.Configure(dig, CellSprite(32f), frames);
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 1f };
            GameServices.Environment = new FlatEnv { Level = 0.5f };

            PlayerAt(new Vector2(1f, 0f));
            Reveal(dig, 0.016f);
            visual.Tick(0.016f);

            Transform t = visual.SquirtRenderer.transform;
            Assert.AreEqual(2f, t.localScale.y, 1e-5f);
            float foot = t.localPosition.y - 0.5f * t.localScale.y;   // a 1 m cell, centre pivot
            Assert.AreEqual(0f, foot, 1e-4f, "the jet's foot stays on the holes at any height");
        }

        // ---- the art --------------------------------------------------------------------------------

        static Texture2D Png(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing {path}");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), $"not a decodable PNG: {path}");
            return tex;
        }

        [Test]
        public void TheHoles_AreJustTwoSmallDarkHoles_OnTheGroundUnderThem()
        {
            Texture2D tex = Png(HolePng);
            _spawned.Add(tex);
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1, opaque = 0;
            Color32[] px = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                Color32 c = px[y * tex.width + x];
                if (c.a == 0) continue;
                // Darkening colours only: no mound, no patch of the sprite's own ground colour.
                Assert.Less(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 64, $"a light pixel at ({x},{y}) would be a patch of ground");
                if (c.a > 200) opaque++;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            Assert.Greater(opaque, 1, "two holes, not nothing");
            Assert.LessOrEqual(maxX - minX + 1, 8, "about the width of his boot, not his torso");
            Assert.LessOrEqual(maxY - minY + 1, 8);
            // Centred on the pivot (the dig spot), within a pixel or two.
            Assert.LessOrEqual(Mathf.Abs((minX + maxX) * 0.5f - 15.5f), 2f);
            Assert.LessOrEqual(Mathf.Abs((minY + maxY) * 0.5f - 15.5f), 2f);
        }

        [Test]
        public void TheSquirt_RisesFromTheCellsBottomRow_AndTheCrestIsTheTallest()
        {
            Texture2D tex = Png(SquirtPng);
            _spawned.Add(tex);
            Assert.AreEqual(128, tex.width, "four 32 px cells, the scene's four squirt sprites");
            int[] tops = new int[4];
            for (int f = 0; f < 4; f++)
            {
                int top = -1;
                bool foot = false;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color32 c = tex.GetPixel(f * 32 + x, y);
                    if (c.a < 128) continue;
                    if (y == 0) foot = true;                   // LoadImage rows run bottom-up
                    top = Mathf.Max(top, y);
                }
                Assert.IsTrue(foot, $"frame {f}: the jet stands on the cell's bottom row (the holes)");
                tops[f] = top;
            }
            Assert.Less(tops[0], tops[1], "the lip is lower than the half jet");
            Assert.Less(tops[1], tops[2], "the half jet is lower than the crest");
            Assert.GreaterOrEqual(tops[2], 20, "the crest stands well clear of the holes (over 0.6 m)");
        }

        // ---- fakes ----------------------------------------------------------------------------------

        sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }
    }
}
