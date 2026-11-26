using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The four pieces the player's rod fight shares (<see cref="RodLineStrand"/>,
    /// <see cref="RodSplashFlourish"/>, <see cref="SurfacedFishDrawer"/>, <see cref="LandedCatchHold"/>)
    /// and the pure splash-edge table, tested on their own: each reads no clock, so the seconds handed
    /// in are the only time there is. The line is pinned at a zero feel and at the shipped feel with a
    /// heavy fish; the surfaced fish at the player's own speed smoothing and her rung-and-jump resolve
    /// table; and the presenter takes a state before Awake without throwing.
    /// </summary>
    public class RodFightPiecesTests
    {
        const string LibraryPath = "Assets/_Project/Data/Art/CatchItemLibrary.asset";
        const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        const string Cod = "fish.atlantic_cod";

        static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<Object> _made = new List<Object>();
        readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        readonly List<LandedCatchHold> _holds = new List<LandedCatchHold>();

        [TearDown]
        public void TearDown()
        {
            foreach (LandedCatchHold h in _holds)
            {
                if (h.Mesh != null && h.Mesh.Renderer != null && s_propOnDestroy != null)
                    s_propOnDestroy.Invoke(h.Mesh.Renderer, null);
                h.Dispose();
            }
            _holds.Clear();
            foreach (IsoCharacterFigureRenderer f in _figures)
            {
                if (f == null) continue;
                if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(f, null);
                Object.DestroyImmediate(f.gameObject);
            }
            _figures.Clear();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---- the splash edge table ------------------------------------------------------------------

        [Test]
        public void SplashFor_NamesTheFourEdges_InFirstMatchOrder_AndOnlyOnAPhaseChange()
        {
            FishingState S(FishingPhase p, float aimX = 0f, float aimY = 0f)
                => new FishingState(p, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f, 0f, false, 0f,
                                    0f, 0f, 0f, aimX, aimY, 0f, 0f, 0f, 0f);

            Assert.AreEqual(RodSplashEdge.Touchdown, RodPresenterMath.SplashFor(S(FishingPhase.Cast), S(FishingPhase.Waiting)));
            foreach (FishingPhase set in new[] { FishingPhase.Fighting, FishingPhase.FightDeep, FishingPhase.FightSurface })
            {
                Assert.AreEqual(RodSplashEdge.PulledUnder,
                    RodPresenterMath.SplashFor(S(FishingPhase.Bite, 2f, -1f), S(set)), $"bite → {set} on the cast path");
                Assert.AreEqual(RodSplashEdge.None,
                    RodPresenterMath.SplashFor(S(FishingPhase.Bite), S(set)), $"bite → {set} off the cast path");
            }
            Assert.AreEqual(RodSplashEdge.Breaks,
                RodPresenterMath.SplashFor(S(FishingPhase.FightDeep), S(FishingPhase.FightSurface)));
            Assert.AreEqual(RodSplashEdge.Result, RodPresenterMath.SplashFor(S(FishingPhase.FightSurface), S(FishingPhase.Landed)));
            Assert.AreEqual(RodSplashEdge.Result, RodPresenterMath.SplashFor(S(FishingPhase.FightDeep), S(FishingPhase.Snapped)));
            // A cast-path bite snapped straight away: the result, since pulled-under needs a hook-set phase.
            Assert.AreEqual(RodSplashEdge.Result, RodPresenterMath.SplashFor(S(FishingPhase.Bite, 2f, 0f), S(FishingPhase.Snapped)));

            Assert.AreEqual(RodSplashEdge.None, RodPresenterMath.SplashFor(S(FishingPhase.Landed), S(FishingPhase.Landed)),
                            "no phase change, no splash");
            Assert.AreEqual(RodSplashEdge.None, RodPresenterMath.SplashFor(S(FishingPhase.Waiting), S(FishingPhase.Bite)));
            Assert.AreEqual(RodSplashEdge.None, RodPresenterMath.SplashFor(S(FishingPhase.FightSurface), S(FishingPhase.FightDeep)));
        }

        // ---- the line reads no clock ----------------------------------------------------------------

        [Test]
        public void TheLine_ShuddersAtTheSecondsItIsHanded_AndDrawsTheLookItIsHanded()
        {
            var home = Track(new GameObject("RodLineStrandHome"));
            var strand = new RodLineStrand(home.transform, 0.02f, 12);
            var look = new RodLineLook
            {
                MaxSagM = 0.5f, LineColor = Color.white, StrainColor = Color.red, SlackColor = Color.gray,
                WhitenLateBias = 2f, ShudderStart01 = 0.5f, ShudderAmpM = 0.03f, ShudderHz = 9f,
                RestTaut = 0.2f, SinkTaut = 0.6f, FightTautFloor = 0.5f, SlackOvershoot01 = 0.4f, SlackSettleSeconds = 0.3f,
            };
            var feel = default(FishFeelSettings);
            // A fish with no size near the snap: the shudder is the line's own, not her weight's.
            var s = new FishingState(FishingPhase.FightDeep, 0.9f, 0.6f, Cod, Cod, FishCategory.InshoreGroundfish,
                                     0f, 0f, false, 0.7f, 1f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            Vector2 tip = new Vector2(0.2f, 0.9f), far = new Vector2(2.1f, -0.7f);

            foreach (float t in new[] { 0f, 0.137f, 1.5f })
            {
                strand.Render(tip, far, in s, in look, in feel, 0.6f, 0f, t, true, 41);
                var want = new Vector2[12];
                float taut = RodPresenterMath.TautFor(s.Phase, false, s.RodBend01, s.Tension01,
                                                      look.RestTaut, look.SinkTaut, look.FightTautFloor);
                taut = FishFeelMath.LineTaut01(taut, s.Size01, false, in feel);
                RodLineMath.SampleLine(tip, far, taut, look.MaxSagM, want);
                float shudder01 = FishFeelMath.LineShudder01(
                    RodLineMath.StrainShudder01(s.Tension01, look.ShudderStart01), s.Size01, 0.6f, false, in feel);
                float amp = look.ShudderAmpM * shudder01;
                Vector2 span = far - tip;
                Vector2 perp = new Vector2(-span.y, span.x) / span.magnitude;
                float phase = t * look.ShudderHz * (2f * Mathf.PI);
                Assert.AreEqual(12, strand.Line.positionCount);
                Assert.AreEqual(41, strand.Line.sortingOrder);
                for (int i = 0; i < 12; i++)
                {
                    Vector2 p = want[i];
                    if (amp > 1e-5f) p += perp * RodLineMath.ShudderOffset(i, 12, phase, amp);
                    Vector3 got = strand.Line.GetPosition(i);
                    Assert.AreEqual(p.x, got.x, 1e-6f, $"t={t} point {i} x");
                    Assert.AreEqual(p.y, got.y, 1e-6f, $"t={t} point {i} y");
                }
            }

            look.LineColor = Color.blue;
            look.StrainColor = Color.blue;
            strand.Render(tip, far, in s, in look, in feel, 0.6f, 0f, 0f, false, 999);
            Assert.AreEqual(Color.blue, strand.Line.startColor, "a changed look was not drawn on the next render");
            Assert.AreEqual(41, strand.Line.sortingOrder, "an unsorted render wrote the order");

            strand.Hide();
            Assert.IsFalse(strand.Line.enabled);
            Assert.AreEqual(0, strand.Line.positionCount);
        }

        [Test]
        public void AHeavyFish_TightensWhitensAndHumsTheLine_ByTheFeelItIsHanded()
        {
            var home = Track(new GameObject("RodLineStrandHeavyHome"));
            var strand = new RodLineStrand(home.transform, 0.02f, 12);
            // A low fight floor, so her weight's own floor is what holds the line tight.
            var look = new RodLineLook
            {
                MaxSagM = 0.5f, LineColor = Color.white, StrainColor = Color.red, SlackColor = Color.gray,
                WhitenLateBias = 2f, ShudderStart01 = 0.5f, ShudderAmpM = 0.03f, ShudderHz = 9f,
                RestTaut = 0.2f, SinkTaut = 0.6f, FightTautFloor = 0.1f, SlackOvershoot01 = 0.4f, SlackSettleSeconds = 0.3f,
            };
            FishFeelSettings feel = FishFeelSettings.Default;
            const float size = 0.8f, load = 0.6f, slackEdge = 0.05f;
            // Below the strain shudder's start: any hum is her weight's alone.
            var s = new FishingState(FishingPhase.FightDeep, 0.3f, 0.6f, Cod, Cod, FishCategory.InshoreGroundfish,
                                     5.59f, 0f, false, 0.2f, 1f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, size);
            Vector2 tip = new Vector2(0.2f, 0.9f), far = new Vector2(2.1f, -0.7f);

            float baseTaut = RodPresenterMath.TautFor(s.Phase, false, s.RodBend01, s.Tension01,
                                                      look.RestTaut, look.SinkTaut, look.FightTautFloor);
            float taut = FishFeelMath.LineTaut01(baseTaut, size, false, in feel);
            float baseWhiten = RodLineMath.Whiten01(s.Tension01, look.WhitenLateBias);
            float whiten = FishFeelMath.LineWhiten01(baseWhiten, size, in feel);
            float shudder01 = FishFeelMath.LineShudder01(
                RodLineMath.StrainShudder01(s.Tension01, look.ShudderStart01), size, load, false, in feel);
            float amp = look.ShudderAmpM * shudder01;
            Assert.Greater(taut, baseTaut + 1e-3f, "harness: her weight does not tighten this line");
            Assert.Greater(whiten, baseWhiten + 1e-3f, "harness: her weight does not whiten this line");
            Assert.Greater(amp, 1e-4f, "harness: her weight does not hum this line");

            foreach (float t in new[] { 0f, 0.137f, 1.5f })
            {
                strand.Render(tip, far, in s, in look, in feel, load, slackEdge, t, true, 41);
                var want = new Vector2[12];
                RodLineMath.SampleLine(tip, far, taut, look.MaxSagM, want);
                Vector2 span = far - tip;
                Vector2 perp = new Vector2(-span.y, span.x) / span.magnitude;
                float phase = t * look.ShudderHz * (2f * Mathf.PI);
                for (int i = 0; i < 12; i++)
                {
                    Vector2 p = want[i] + perp * RodLineMath.ShudderOffset(i, 12, phase, amp);
                    Vector3 got = strand.Line.GetPosition(i);
                    Assert.AreEqual(p.x, got.x, 1e-6f, $"t={t} point {i} x");
                    Assert.AreEqual(p.y, got.y, 1e-6f, $"t={t} point {i} y");
                }
                Color c = Color.Lerp(look.LineColor, look.StrainColor, whiten);
                Color drawn = strand.Line.startColor;
                Assert.AreEqual(c.r, drawn.r, 1.5f / 255f, $"t={t} colour r");
                Assert.AreEqual(c.g, drawn.g, 1.5f / 255f, $"t={t} colour g");
                Assert.AreEqual(c.b, drawn.b, 1.5f / 255f, $"t={t} colour b");
            }
        }

        // ---- the surfaced fish reads no clock -------------------------------------------------------

        sealed class Facts : IFishBehaviourFacts
        {
            public int Asked;
            public bool Jumps(string fishId) { Asked++; return fishId == Jumper; }
            public float FightJumpPeriodSeconds(string fishId) => fishId == Jumper ? 0.9f : 0f;
            public bool TryWeightRangeKg(string fishId, out float minKg, out float maxKg)
            { minKg = 0f; maxKg = 0f; return false; }
        }

        const string Jumper = "fish.test_jumper";
        const string Sitter = "fish.test_sitter";

        Sprite[] Sheet(string name, int count)
        {
            var tex = Track(new Texture2D(2, 2));
            var s = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                s[i] = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero));
                s[i].name = $"{name}#{i}";
            }
            return s;
        }

        FishSpeciesVisual Species(string id)
        {
            const int per = 2, dirs = 8;
            var rung = new FishRungVisual
            {
                Kg = 3f,
                DartFrames = Sheet(id + "_dart", dirs * per), DartFramesPerDir = per,
                ThrashFrames = Sheet(id + "_thrash", dirs * per), ThrashFramesPerDir = per,
                JumpFrames = Sheet(id + "_jump", dirs * FishJumpArc.Frames), JumpFramesPerDir = FishJumpArc.Frames,
            };
            return new FishSpeciesVisual { FishId = id, Rungs = new[] { rung } };
        }

        static FishingState Surface(string fish, float kg, float offX, float offY)
            => new FishingState(FishingPhase.FightSurface, 0.3f, 0.3f, fish, fish, FishCategory.InshoreGroundfish,
                                kg, 0.4f, false, 0.2f, offX, offY, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        [Test]
        public void HeldStill_SheTurnsFromDartToThrash_OnTheFrameTheSmoothingSays()
        {
            // The player's own smoothing, read off the presenter, so a changed constant fails here.
            FieldInfo k = typeof(RodFightPresenter).GetField("FishSpeedSmoothing01",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(k, "harness: the presenter's smoothing constant moved");
            float smoothing = (float)k.GetRawConstantValue();
            Assert.AreEqual(0.35f, smoothing, "the player's fish speed smoothing changed");

            const float dt = 0.02f, dartMps = 1.2f;
            var sr = Track(new GameObject("SurfacedFish")).AddComponent<SpriteRenderer>();
            var drawer = new SurfacedFishDrawer(sr);
            var species = new[] { Species(Sitter) };
            FishingState s = Surface(Sitter, 3f, 1f, -1f);
            Assert.IsTrue(drawer.Resolve(in s, species, null, new Facts(), 4.5f));
            Assert.IsNotNull(drawer.Rung, "harness: no rung for the test fish");

            // The formula: her first drawn frame jumps from the origin, then she holds still.
            float speed = 0f;
            Vector2 last = Vector2.zero, pos = new Vector2(1f, -1f);
            int predicted = -1;
            for (int f = 0; f < 60; f++)
            {
                speed = Mathf.Lerp(speed, (pos - last).magnitude / dt, smoothing);
                last = pos;
                if (!RodPresenterMath.IsDarting(speed, dartMps)) { predicted = f; break; }
            }
            Assert.Greater(predicted, 2, "harness: the switch comes too soon to tell the arguments apart");

            int switched = -1;
            for (int f = 0; f < 60; f++)
            {
                drawer.Render(Vector2.zero, in s, dt, f * dt, f * dt, 0.1f, dartMps, smoothing, 30,
                              out bool splashDue, out _);
                Assert.IsFalse(splashDue, "a fish that does not jump threw a splash");
                Assert.IsTrue(sr.enabled, $"frame {f}: she was not drawn");
                bool thrash = sr.sprite.name.StartsWith(Sitter + "_thrash");
                if (thrash && switched < 0) switched = f;
                if (switched < 0)
                    StringAssert.StartsWith(Sitter + "_dart", sr.sprite.name, $"frame {f}: she should still dart");
                else
                    Assert.IsTrue(thrash, $"frame {f}: she went back to darting while held still");
                Assert.AreEqual(30, sr.sortingOrder);
            }
            Assert.AreEqual(predicted, switched, "she turned from dart to thrash on a different frame from the formula");
        }

        [Test]
        public void Resolve_IsTrueOnTheFirstPublish_AndOnAMove_AndASpeciesChangeAloneResetsTheJumps()
        {
            var sr = Track(new GameObject("SurfacedJumper")).AddComponent<SpriteRenderer>();
            var drawer = new SurfacedFishDrawer(sr);
            var facts = new Facts();
            var species = new[] { Species(Jumper), Species(Sitter) };

            FishingState first = Surface(Jumper, 3.1f, 1f, 0.5f);
            Assert.IsTrue(drawer.Resolve(in first, species, null, facts, 4.5f), "the first publish (NaN weight) did not resolve");
            Assert.AreEqual(0, drawer.SpeciesIndex);
            Assert.AreEqual(1, facts.Asked);
            Assert.IsFalse(drawer.Resolve(in first, species, null, facts, 4.5f), "the same fish at the same weight re-resolved");
            Assert.AreEqual(1, facts.Asked, "the species seam was asked again for the same fish");

            // Let her jump at least once.
            drawer.OnSurfaced();
            for (int f = 0; f < 200 && drawer.JumpsDrawn == 0; f++)
                drawer.Render(Vector2.zero, in first, 0.02f, f * 0.02f, f * 0.02f, 0.1f, 1.2f, 0.35f, 30, out _, out _);
            Assert.Greater(drawer.JumpsDrawn, 0, "harness: the jumper never jumped");
            int jumps = drawer.JumpsDrawn;

            FishingState heavier = Surface(Jumper, 3.4f, 1f, 0.5f);
            Assert.IsTrue(drawer.Resolve(in heavier, species, null, facts, 4.5f), "a weight change did not re-pick the rung");
            Assert.AreEqual(jumps, drawer.JumpsDrawn, "a weight change reset this fight's jumps");
            Assert.AreEqual(1, facts.Asked, "a weight change asked the species seam again");

            FishingState other = Surface(Sitter, 3.4f, 1f, 0.5f);
            Assert.IsTrue(drawer.Resolve(in other, species, null, facts, 4.5f), "a species change did not resolve");
            Assert.AreEqual(0, drawer.JumpsDrawn, "a species change kept the last fish's jumps");
            Assert.IsFalse(drawer.FishAirborne);
            Assert.AreEqual(1, drawer.SpeciesIndex);
            Assert.AreEqual(2, facts.Asked);
        }

        [Test]
        public void Prime_MidJump_DrawsHerInTheAir_CountsNoJump_AndThrowsNoSplash_ThenTheNextJumpIsSeen()
        {
            const float kg = 3.1f, period = 0.9f, dt = 0.02f;
            var sr = Track(new GameObject("PrimedJumper")).AddComponent<SpriteRenderer>();
            var drawer = new SurfacedFishDrawer(sr);
            FishingState s = Surface(Jumper, kg, 1f, 0.5f);
            Assert.IsTrue(drawer.Resolve(in s, new[] { Species(Jumper) }, null, new Facts(), 4.5f));
            int seed = SurfacedFishDrawer.FightSeed(Jumper, kg);

            // An instant she is in the air, a little way into her jump.
            float t0 = -1f;
            double primed = 0d;
            for (int i = 1; i < 5000 && t0 < 0f; i++)
            {
                float t = i * 0.01f;
                if (FishJumpArc.TryNextJump(seed, t, period, out double st) && FishJumpArc.InAir(st, t) && t - st > dt)
                { t0 = t; primed = st; }
            }
            Assert.Greater(t0, 0f, "harness: the jumper is never in the air");

            drawer.Prime(t0);
            Assert.IsTrue(drawer.FishAirborne, "primed mid-jump she is drawn in the air");
            bool sawNext = false, splashedNext = false;
            float now = t0;
            for (int f = 0; f < 5000 && !splashedNext; f++, now += dt)
            {
                drawer.Render(Vector2.zero, in s, dt, now, now, 0.1f, 1.2f, 0.35f, 30, out bool due, out _);
                if (drawer.JumpsDrawn == 0)
                    Assert.IsFalse(due, $"the primed jump (from {primed:F3}s) threw a re-entry splash at {now:F3}s");
                if (drawer.JumpsDrawn == 1 && drawer.FishAirborne) sawNext = true;
                if (sawNext && due) splashedNext = true;
                Assert.LessOrEqual(drawer.JumpsDrawn, 1, "harness: ran past the next jump");
            }
            Assert.IsTrue(sawNext, "the jump after the primed one was not counted");
            Assert.IsTrue(splashedNext, "the jump after the primed one threw no splash");
        }

        [Test]
        public void Prime_AfterAJumpCameDown_ThrowsNoSplash_WhereAnUnprimedDrawerWould()
        {
            const float kg = 3.1f, period = 0.9f;
            int seed = SurfacedFishDrawer.FightSeed(Jumper, kg);
            float t1 = -1f;
            for (int i = 1; i < 5000 && t1 < 0f; i++)
            {
                float t = i * 0.01f;
                if (FishJumpArc.TryNextJump(seed, t, period, out double st) && t > st && !FishJumpArc.InAir(st, t)) t1 = t;
            }
            Assert.Greater(t1, 0f, "harness: no instant after a jump came down");
            FishingState s = Surface(Jumper, kg, 1f, 0.5f);

            var unprimed = new SurfacedFishDrawer(Track(new GameObject("Unprimed")).AddComponent<SpriteRenderer>());
            Assert.IsTrue(unprimed.Resolve(in s, new[] { Species(Jumper) }, null, new Facts(), 4.5f));
            unprimed.Render(Vector2.zero, in s, 0.02f, t1, t1, 0.1f, 1.2f, 0.35f, 30, out bool staleSplash, out _);
            Assert.IsTrue(staleSplash, "harness: an unprimed drawer throws the stale splash, so this test bites");

            var primed = new SurfacedFishDrawer(Track(new GameObject("Primed")).AddComponent<SpriteRenderer>());
            Assert.IsTrue(primed.Resolve(in s, new[] { Species(Jumper) }, null, new Facts(), 4.5f));
            primed.Prime(t1);
            Assert.IsFalse(primed.FishAirborne);
            primed.Render(Vector2.zero, in s, 0.02f, t1, t1, 0.1f, 1.2f, 0.35f, 30, out bool due, out _);
            Assert.IsFalse(due, "a primed drawer threw the splash of a jump nobody saw");
            Assert.AreEqual(0, primed.JumpsDrawn);
        }

        [Test]
        public void ResetMotion_ForgetsTheLastPosition_SoTheFirstFrameReadsNoSpeed()
        {
            const float dt = 0.02f, dartMps = 1.2f;
            FishingState s = Surface(Sitter, 3f, 1f, -1f);
            var species = new[] { Species(Sitter) };

            // Without it, her first frame reads a dash from the origin: she darts.
            var plainSr = Track(new GameObject("NotReset")).AddComponent<SpriteRenderer>();
            var plain = new SurfacedFishDrawer(plainSr);
            Assert.IsTrue(plain.Resolve(in s, species, null, new Facts(), 4.5f));
            plain.Render(Vector2.zero, in s, dt, 0f, 0f, 0.1f, dartMps, 0.35f, 30, out _, out _);
            StringAssert.StartsWith(Sitter + "_dart", plainSr.sprite.name, "harness: the first frame should read a dash");

            // Reset where she is: no dash, she thrashes in place.
            var resetSr = Track(new GameObject("Reset")).AddComponent<SpriteRenderer>();
            var reset = new SurfacedFishDrawer(resetSr);
            Assert.IsTrue(reset.Resolve(in s, species, null, new Facts(), 4.5f));
            reset.ResetMotion(new Vector2(s.FishOffsetX, s.FishOffsetY));
            reset.Render(Vector2.zero, in s, dt, 0f, 0f, 0.1f, dartMps, 0.35f, 30, out _, out _);
            StringAssert.StartsWith(Sitter + "_thrash", resetSr.sprite.name, "after a reset her first frame read a speed");
        }

        // ---- the presenter before Awake --------------------------------------------------------------

        [Test]
        public void ThePresenter_TakesAStateBeforeAwake_WithoutThrowing()
        {
            var go = Track(new GameObject("RodFightPresenterBeforeAwake"));
            go.SetActive(false);
            var rod = go.AddComponent<RodFightPresenter>();
            Assert.DoesNotThrow(() =>
            {
                // Every edge that reaches for a shared piece: the bite, the hook-set, her break (a splash
                // and the surfacing), the landing.
                rod.OnFishingState(new FishingStateChanged(new FishingState(
                    FishingPhase.Bite, 0f, 0f, Jumper, Jumper, FishCategory.InshoreGroundfish, 3.1f)));
                rod.OnFishingState(new FishingStateChanged(new FishingState(
                    FishingPhase.FightDeep, 0.3f, 0.2f, Jumper, Jumper, FishCategory.InshoreGroundfish, 3.1f)));
                rod.OnFishingState(new FishingStateChanged(Surface(Jumper, 3.1f, 1f, 0.5f)));
                rod.OnFishingState(new FishingStateChanged(new FishingState(
                    FishingPhase.Landed, 0f, 1f, Jumper, Jumper, FishCategory.InshoreGroundfish, 3.1f)));
            });
            Assert.AreEqual(-1, rod.SpeciesIndex);
            Assert.AreEqual(0, rod.JumpsDrawn);
        }

        // ---- the splash reads no clock --------------------------------------------------------------

        [Test]
        public void TheSplash_PlaysAtTheSecondsItIsHanded_ThenEnds_AndAStopDoesNotResume()
        {
            var sr = Track(new GameObject("Splash")).AddComponent<SpriteRenderer>();
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Track(Sprite.Create(Track(new Texture2D(2, 2)), new Rect(0, 0, 2, 2), Vector2.zero));
            var splash = new RodSplashFlourish(sr);

            Assert.IsFalse(splash.Begin(Vector2.one, null), "a splash with no frames started");
            Assert.IsFalse(splash.Active);
            Assert.IsTrue(splash.Begin(new Vector2(3f, -2f), frames));
            splash.Render(0.26f, frames, 10f, true, 77);
            Assert.IsTrue(sr.enabled);
            Assert.AreSame(frames[2], sr.sprite, "0.26 s at 10 fps is the third frame");
            Assert.AreEqual(77, sr.sortingOrder);
            Assert.AreEqual(new Vector3(3f, -2f, 0f), sr.transform.position);
            splash.Render(0.41f, frames, 10f, true, 77);
            Assert.IsFalse(splash.Active, "the flipbook played through and the splash did not end");
            Assert.IsFalse(sr.enabled);

            Assert.IsTrue(splash.Begin(Vector2.zero, frames));
            splash.Stop();
            splash.Render(0f, frames, 10f, true, 77);
            Assert.IsFalse(sr.enabled, "a stopped splash drew");
        }

        // ---- the landed catch's hand ----------------------------------------------------------------

        [Test]
        public void TheLandedCatch_TakesTheNearerHand_ByTheInlineRule_OnTheFigureItIsHanded()
        {
            var config = Track(ScriptableObject.CreateInstance<GameConfig>());
            config.MeshCatchInHand = true;
            var art = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(LibraryPath);
            Assert.IsNotNull(art, $"harness: no library at {LibraryPath}");
            var figure = new GameObject("LandedCatchHoldFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher did not configure");
            Assert.IsTrue(figure.SetPose("idle", 0));

            var hold = new LandedCatchHold(Track(new GameObject("LandedCatchHoldHome")).transform);
            _holds.Add(hold);
            Assert.IsFalse(hold.Wants(config), "nothing resolved yet, nothing wanted");
            hold.Resolve(art, Cod, 5.59f);
            Assert.IsTrue(hold.Wants(config), "harness: the cod has no held mesh at 5.59 kg");
            Assert.AreEqual(HeldPropSockets.LeftHandTool, hold.Socket, "a new catch starts in the free hand");

            int swaps = 0;
            for (int yaw = 0; yaw < 720; yaw += 30)
            {
                figure.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                string was = hold.Socket;
                string other = was == HeldPropSockets.LeftHandTool ? HeldPropSockets.RightHandTool : HeldPropSockets.LeftHandTool;
                Assert.IsTrue(HeldCatchMesh.TrySocketDepth(figure, was, out float here), "harness: no depth for the hand");
                Assert.IsTrue(HeldCatchMesh.TrySocketDepth(figure, other, out float there));
                string want = HeldCatchMesh.NearerSocket(was, other, here, there, config.HeldFishHandSwapDepthMeters);
                hold.TryDraw(config, figure);
                Assert.AreEqual(want, hold.Socket, $"yaw {yaw}: the hand pick left the inline rule");
                if (hold.Socket != was) swaps++;
            }
            Assert.Greater(swaps, 0, "harness: turning him all the way round never swapped hands");
            Assert.IsNotNull(hold.Mesh, "the mesh driver was not made");

            hold.Resolve(art, Cod, 5.59f);
            Assert.AreEqual(HeldPropSockets.LeftHandTool, hold.Socket, "a new landing did not start in the free hand");
            hold.Hide();
            Assert.IsFalse(hold.Mesh.Drawing);
        }

        T Track<T>(T o) where T : Object { _made.Add(o); return o; }
    }
}
