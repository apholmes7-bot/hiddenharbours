using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The player's rod fight, frame by frame, pinned before it is taken apart.</b> The presenter is
    /// driven through one fixed fight (wind back, cast, wait, bite, deep, surface with a jumper, land)
    /// and a snapped one, on a fixed frame step, and every frame records what the player would see of
    /// the four pieces that are to be shared with the villagers' presenter: the line (count, every
    /// point, width, colour, order), the splash (on, where, which frame, order), the surfaced fish (on,
    /// where, which frame, jumps, airborne) and the landed catch (mesh made, drawing, refusal). The
    /// capture is compared to a recording taken on main before the extraction, so a refactor that
    /// changes a single bit of any of it fails here.
    ///
    /// <para>Tensions stay below the shudder's start and the fish's size is 0, so the shudder phase
    /// (which reads the wall clock on main) contributes nothing and the capture is exact. The boil and
    /// the ripple ride the wall clock too and are not recorded.</para>
    ///
    /// <para>Re-record (only for a deliberate change to what the player sees): set
    /// <c>HH_GOLDEN_RECORD=1</c> in the shell that launches Unity; the test writes the golden file and
    /// fails so a recording is never mistaken for a pass.</para>
    /// </summary>
    public class RodFightPresenterGoldenTests
    {
        const float Step = 0.02f;
        const string GoldenPath = "Assets/Tests/PlayMode/Golden/RodFightPresenter.golden.txt";
        const string Bass = "fish.striped_bass";
        const string Cod = "fish.atlantic_cod";
        const int Dirs = 8;

        readonly List<Object> _made = new List<Object>();
        IFishBehaviourFacts _factsWas;
        GameConfig _configWas;
        CountingFacts _facts;
        float _captureWas;

        /// <summary>The species seam, counted: how often the presenter asked about a fish.</summary>
        sealed class CountingFacts : IFishBehaviourFacts
        {
            public readonly List<string> Asked = new List<string>();
            public bool Jumps(string fishId) { Asked.Add(fishId); return fishId == Bass; }
            public float FightJumpPeriodSeconds(string fishId) => fishId == Bass ? 0.9f : 0f;
            public bool TryWeightRangeKg(string fishId, out float minKg, out float maxKg)
            { minKg = 0f; maxKg = 0f; return false; }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _captureWas = Time.captureDeltaTime;
            Time.captureDeltaTime = Step;
            _factsWas = GameServices.FishBehaviour;
            _facts = new CountingFacts();
            GameServices.FishBehaviour = _facts;
            _configWas = GameServices.Config;
            var config = Track(ScriptableObject.CreateInstance<GameConfig>());
            config.MeshRodInHand = false;
            config.MeshCatchInHand = true;
            GameServices.Config = config;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            GameServices.FishBehaviour = _factsWas;
            GameServices.Config = _configWas;
            Time.captureDeltaTime = _captureWas;
            yield return null;
        }

        // ---- the capture ----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheFight_DrawsWhatItDrewOnMain_FrameByFrame()
        {
            var capture = new StringBuilder();
            Fisher(out RodFightPresenter rod, out _);
            yield return Drive(rod, capture);

            string got = capture.ToString().Replace("\r\n", "\n");
            if (System.Environment.GetEnvironmentVariable("HH_GOLDEN_RECORD") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath));
                File.WriteAllText(GoldenPath, got);
                Assert.Fail($"recorded {GoldenPath} ({got.Length} chars); unset HH_GOLDEN_RECORD and run again");
            }
            Assert.IsTrue(File.Exists(GoldenPath), $"no golden at {GoldenPath}: record it on main (HH_GOLDEN_RECORD=1)");
            string want = File.ReadAllText(GoldenPath).Replace("\r\n", "\n");
            string[] w = want.Split('\n'), g = got.Split('\n');
            for (int i = 0; i < Mathf.Min(w.Length, g.Length); i++)
                if (w[i] != g[i])
                    Assert.Fail($"the fight drew differently from main at capture line {i + 1}:\n main: {w[i]}\n  now: {g[i]}");
            Assert.AreEqual(w.Length, g.Length, "the fight drew a different number of frames from main");
        }

        // ---- the species is looked up once per fish -------------------------------------------------

        [UnityTest]
        public IEnumerator TheSpecies_IsAskedAboutOncePerFish_NeverPerFrame()
        {
            Fisher(out RodFightPresenter rod, out _);
            var published = new List<string>();
            yield return Drive(rod, null, published);

            var want = new List<string>();
            string last = null;
            foreach (string id in published)
            {
                if (!string.Equals(id, last, System.StringComparison.Ordinal)) want.Add(id);
                last = id;
            }
            CollectionAssert.AreEqual(want, _facts.Asked,
                "the presenter asked the species seam about a fish other than once per change of fish");
        }

        // ---- sorting over the fisher, every frame ---------------------------------------------------

        [UnityTest]
        public IEnumerator TheLineAndTheSplash_FollowTheFishersOrder_WhenItMoves()
        {
            Fisher(out RodFightPresenter rod, out SpriteRenderer body);
            LineRenderer line = Child<LineRenderer>(rod, "FishingLine");
            SpriteRenderer splash = Child<SpriteRenderer>(rod, "Splash");

            Publish(rod, State(FishingPhase.Waiting, aimX: 2f, aimY: -1f));
            yield return Frames(2);
            Publish(rod, State(FishingPhase.Bite, Bass, aimX: 2f, aimY: -1f));
            yield return Frames(1);
            Publish(rod, State(FishingPhase.FightDeep, Bass, tension: 0.3f, offX: 2f, offY: -1f));
            yield return Frames(1);
            Assert.IsTrue(line.enabled, "harness: the line is out in the deep fight");
            Assert.IsTrue(splash.enabled, "harness: the pulled-under splash is playing");

            foreach (int order in new[] { 237, 12, 900 })
            {
                body.sortingOrder = order;
                yield return Frames(1);
                Assert.AreEqual(EffectSorting.OrderOver(SortedEffect.RodLine, order), line.sortingOrder,
                    $"the line did not follow the fisher to order {order} on the next frame");
                if (splash.enabled)
                    Assert.AreEqual(EffectSorting.OrderOver(SortedEffect.RodSplash, order), splash.sortingOrder,
                        $"the splash did not follow the fisher to order {order} on the next frame");
            }
        }

        // ---- a line field tuned live is in the next frame -------------------------------------------

        [UnityTest]
        public IEnumerator ALineColourTunedAfterAwake_IsDrawnOnTheNextFrame()
        {
            Fisher(out RodFightPresenter rod, out _);
            LineRenderer line = Child<LineRenderer>(rod, "FishingLine");
            Publish(rod, State(FishingPhase.Waiting, aimX: 2f, aimY: -1f));
            yield return Frames(2);
            Assert.IsTrue(line.enabled, "harness: the line is out while waiting");

            var field = typeof(RodFightPresenter).GetField("_lineColor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "harness: no _lineColor field");
            // The renderer keeps colours at 8 bits a channel, so the tuned colour is one it can hold exactly.
            Color tuned = new Color32(26, 204, 77, 255);
            Assert.AreNotEqual((Color32)tuned, (Color32)line.startColor, "harness: the tuned colour is already drawn");
            field.SetValue(rod, tuned);
            yield return Frames(1);
            Assert.AreEqual((Color32)tuned, (Color32)line.startColor, "a line colour set after Awake was not drawn on the next frame");
            Assert.AreEqual((Color32)tuned, (Color32)line.endColor);
        }

        // ---- the splash edges, in order -------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSplash_FiresOnTheFourEdges_AtTheirOwnPlaces_AndNowhereElse()
        {
            Fisher(out RodFightPresenter rod, out _);
            SpriteRenderer splash = Child<SpriteRenderer>(rod, "Splash");
            Vector2 angler = rod.transform.position;

            // (from, to, where it should splash or null for none)
            var edges = new (FishingState from, FishingState to, Vector2? at, string name)[]
            {
                (State(FishingPhase.Cast, aimX: 2f, aimY: -1f), State(FishingPhase.Waiting, aimX: 2.5f, aimY: -1.5f),
                    angler + new Vector2(2.5f, -1.5f), "touchdown at the NEW aim"),
                (State(FishingPhase.Bite, Bass, aimX: 2f, aimY: -1f), State(FishingPhase.FightDeep, Bass, offX: 3f, offY: 1f),
                    angler + new Vector2(2f, -1f), "pulled under at the OLD aim"),
                (State(FishingPhase.Bite, Bass), State(FishingPhase.FightDeep, Bass, offX: 3f, offY: 1f),
                    null, "no cast path, no pulled-under splash"),
                (State(FishingPhase.FightDeep, Bass, offX: 1f, offY: 1f), State(FishingPhase.FightSurface, Bass, offX: 1.5f, offY: 0.5f),
                    angler + new Vector2(1.5f, 0.5f), "she breaks at her NEW offset"),
                (State(FishingPhase.Waiting, aimX: 2f, aimY: -1f), State(FishingPhase.Bite, Bass, aimX: 2f, aimY: -1f),
                    null, "a bite is not a splash"),
            };

            foreach (var e in edges)
            {
                Publish(rod, FishingState.Idle);
                yield return Frames(30);   // any earlier flourish has run out
                Publish(rod, e.from);
                yield return Frames(30);
                Assert.IsFalse(splash.enabled, $"harness ({e.name}): a flourish from the set-up is still playing");
                Publish(rod, e.to);
                yield return Frames(1);
                if (e.at.HasValue)
                {
                    Assert.IsTrue(splash.enabled, $"{e.name}: no splash");
                    Assert.Less(((Vector2)splash.transform.position - e.at.Value).magnitude, 1e-5f, $"{e.name}: splashed in the wrong place");
                }
                else Assert.IsFalse(splash.enabled, $"{e.name}: splashed");
            }

            // The result: at the line's last far end, for a landing and for a snap alike.
            foreach (FishingPhase result in new[] { FishingPhase.Landed, FishingPhase.Snapped })
            {
                Publish(rod, FishingState.Idle);
                yield return Frames(30);
                Publish(rod, State(FishingPhase.FightSurface, Cod, tension: 0.3f, offX: 1.2f, offY: 0.4f, kg: 5.59f));
                yield return Frames(30);
                LineRenderer line = Child<LineRenderer>(rod, "FishingLine");
                Assert.IsTrue(line.enabled && line.positionCount > 1, $"harness ({result}): the line is out");
                Vector3 far = line.GetPosition(line.positionCount - 1);
                Publish(rod, State(result, Cod, kg: 5.59f));
                yield return Frames(1);
                Assert.IsTrue(splash.enabled, $"{result}: no result splash");
                Assert.Less(((Vector2)splash.transform.position - (Vector2)far).magnitude, 1e-5f,
                    $"{result}: the result splash is not at the line's last far end");
            }
        }

        // ---- cleanup on disable ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Disabling_HidesEveryPiece_AndTheSplashDoesNotResume()
        {
            Fisher(out RodFightPresenter rod, out _);
            Publish(rod, State(FishingPhase.FightDeep, Bass, tension: 0.3f, offX: 1f, offY: 1f));
            yield return Frames(3);
            Publish(rod, State(FishingPhase.FightSurface, Bass, tension: 0.3f, offX: 1.5f, offY: 0.5f));
            yield return Frames(1);
            LineRenderer line = Child<LineRenderer>(rod, "FishingLine");
            SpriteRenderer splash = Child<SpriteRenderer>(rod, "Splash");
            SpriteRenderer fish = Child<SpriteRenderer>(rod, "FishOnTheLine");
            Assert.IsTrue(line.enabled && splash.enabled && fish.enabled, "harness: line, splash and fish are up");

            rod.enabled = false;
            Assert.IsFalse(line.enabled, "the line outlived the disable");
            Assert.AreEqual(0, line.positionCount, "the line kept its points");
            Assert.IsFalse(splash.enabled, "the splash outlived the disable");
            Assert.IsFalse(fish.enabled, "the fish outlived the disable");
            Assert.IsFalse(Child<SpriteRenderer>(rod, "HeldCatch").enabled, "the held fish outlived the disable");

            rod.enabled = true;
            yield return Frames(1);
            Assert.IsFalse(splash.enabled, "the splash resumed after a re-enable");
        }

        [UnityTest]
        public IEnumerator Destroying_DisposesTheLandedCatchMesh()
        {
            Fisher(out RodFightPresenter rod, out _);
            Publish(rod, State(FishingPhase.FightSurface, Cod, tension: 0.3f, offX: 1.5f, offY: 0.5f, kg: 5.59f));
            yield return Frames(3);
            Publish(rod, State(FishingPhase.Landed, Cod, kg: 5.59f));
            yield return Frames(3);
            HeldCatchMesh land = rod.LandMesh;
            Assume.That(land != null, "the landing asked for no mesh (no catch art for cod at this build)");
            Assert.AreEqual(HeldCatchMesh.Refusal.NoMeshFigure, land.LastRefusal,
                "harness: with no mesh figure the landing is refused at the figure");
            Object.Destroy(rod.gameObject);
            yield return null;
            Assert.IsNull(rod.LandMesh, "the presenter kept its land mesh past destroy");
            Assert.IsFalse(land.Drawing, "the disposed land mesh still reads as drawing");
            Assert.IsTrue(land.Renderer == null, "the land mesh's renderer outlived the presenter");
        }

        // ---- the drive ------------------------------------------------------------------------------

        /// <summary>The fixed fight. Every step publishes a state then runs its frames; the capture
        /// records each frame after LateUpdate.</summary>
        IEnumerator Drive(RodFightPresenter rod, StringBuilder capture, List<string> published = null)
        {
            var steps = new List<(FishingState s, int frames, string name)>
            {
                (FishingState.Idle, 3, "idle"),
                (State(FishingPhase.WindBack, aimX: 2f, aimY: -1f), 6, "windback"),
                (State(FishingPhase.Cast, aimX: 2f, aimY: -1f), 8, "cast"),
                (State(FishingPhase.Waiting, aimX: 2.2f, aimY: -1.1f), 10, "waiting"),
                (State(FishingPhase.Bite, Bass, aimX: 2.2f, aimY: -1.1f, kg: 3.1f), 4, "bite"),
            };
            for (int i = 0; i < 8; i++)
                steps.Add((State(FishingPhase.FightDeep, Bass, tension: 0.2f + 0.02f * i,
                                 offX: 2f + 0.1f * i, offY: -1f + 0.05f * i, slack: i == 5, kg: 3.1f), 3, $"deep{i}"));
            for (int i = 0; i < 40; i++)
            {
                float a = i * 0.35f;
                steps.Add((State(FishingPhase.FightSurface, Bass, tension: 0.25f + 0.004f * i,
                                 offX: 1.6f + 0.6f * Mathf.Cos(a), offY: -0.6f + 0.4f * Mathf.Sin(a),
                                 slack: i % 9 == 4, kg: 3.1f), 3, $"surface{i}"));
            }
            steps.Add((State(FishingPhase.Landed, Bass, kg: 3.1f), 20, "landed"));
            steps.Add((FishingState.Idle, 20, "idle"));
            steps.Add((State(FishingPhase.Waiting), 4, "waiting-weighted"));
            steps.Add((State(FishingPhase.Bite, Cod, kg: 5.59f), 3, "bite-cod"));
            steps.Add((State(FishingPhase.FightDeep, Cod, tension: 0.3f, offX: 0.5f, offY: -1.5f, kg: 5.59f), 6, "deep-cod"));
            steps.Add((State(FishingPhase.FightSurface, Cod, tension: 0.35f, offX: 0.8f, offY: -1.2f, kg: 5.59f), 6, "surface-cod"));
            steps.Add((State(FishingPhase.Snapped, Cod, kg: 5.59f), 20, "snapped"));
            steps.Add((FishingState.Idle, 4, "idle"));

            int frame = 0;
            foreach (var (s, frames, name) in steps)
            {
                published?.Add(s.FishId);
                Publish(rod, s);
                for (int f = 0; f < frames; f++)
                {
                    yield return null;
                    if (capture != null) Record(capture, rod, frame, name);
                    frame++;
                }
            }
        }

        static void Record(StringBuilder c, RodFightPresenter rod, int frame, string step)
        {
            LineRenderer line = Child<LineRenderer>(rod, "FishingLine");
            SpriteRenderer splash = Child<SpriteRenderer>(rod, "Splash");
            SpriteRenderer fish = Child<SpriteRenderer>(rod, "FishOnTheLine");
            SpriteRenderer held = Child<SpriteRenderer>(rod, "HeldCatch");
            c.Append(frame).Append(' ').Append(step);
            c.Append(" | line ").Append(line.enabled ? 1 : 0).Append(' ').Append(line.positionCount)
             .Append(" w ").Append(F(line.startWidth)).Append(' ').Append(F(line.endWidth))
             .Append(" c ").Append(C(line.startColor)).Append(' ').Append(C(line.endColor))
             .Append(" o ").Append(line.sortingOrder);
            for (int i = 0; i < line.positionCount; i++) c.Append(' ').Append(V(line.GetPosition(i)));
            c.Append(" | anchor ").Append(V(rod.LineAnchorWorld)).Append(" lean ").Append(F(rod.RodLeanDegrees));
            c.Append(" | splash ").Append(splash.enabled ? 1 : 0);
            if (splash.enabled) c.Append(' ').Append(V(splash.transform.position)).Append(' ').Append(Name(splash.sprite)).Append(" o ").Append(splash.sortingOrder);
            c.Append(" | fish ").Append(fish.enabled ? 1 : 0);
            if (fish.enabled) c.Append(' ').Append(V(fish.transform.position)).Append(' ').Append(Name(fish.sprite)).Append(" o ").Append(fish.sortingOrder);
            c.Append(" jumps ").Append(rod.JumpsDrawn).Append(" air ").Append(rod.FishAirborne ? 1 : 0)
             .Append(" sp ").Append(rod.SpeciesIndex).Append(" rung ").Append(rod.Rung != null ? F(rod.Rung.Kg) : "-");
            c.Append(" | held ").Append(held.enabled ? 1 : 0);
            HeldCatchMesh land = rod.LandMesh;
            c.Append(" | land ").Append(land != null ? $"{(land.Drawing ? 1 : 0)} {land.LastRefusal}" : "-");
            c.Append('\n');
        }

        // ---- harness --------------------------------------------------------------------------------

        void Fisher(out RodFightPresenter rod, out SpriteRenderer body)
        {
            var go = Track(new GameObject("GoldenFisher"));
            go.transform.position = new Vector3(3.25f, -1.75f, 0f);
            body = go.AddComponent<SpriteRenderer>();
            body.sortingOrder = 237;
            var anim = go.AddComponent<PlayerFishingAnimator>();
            anim.Configure(Sheet("bite", 4), Sheet("strike", 3), Sheet("reel", 4), Sheet("land", 4));
            anim.ConfigureCastAndHold(Sheet("hold", 4), Sheet("castBack", 3), Sheet("castRelease", 3));
            rod = go.AddComponent<RodFightPresenter>();

            var rodStates = new RodStateVisual[RodPresenterMath.RodStateCount];
            for (int s = 0; s < rodStates.Length; s++)
            {
                const int per = 4;
                var grips = new Vector2[Dirs * per];
                var tips = new Vector2[Dirs * per];
                for (int i = 0; i < grips.Length; i++)
                {
                    grips[i] = new Vector2(0.11f + 0.013f * i, 0.42f - 0.007f * s);
                    tips[i] = new Vector2(0.9f - 0.011f * i, 0.55f + 0.009f * s);
                }
                rodStates[s] = new RodStateVisual
                {
                    State = RodPresenterMath.RodStates[s], CellW = 64, CellH = 64, PivotX = 20f, PivotY = 40f,
                    Tier = "cane", BlankLenM = 2.4f, Frames = Sheet("rod_" + RodPresenterMath.RodStates[s], per),
                    FramesPerDir = per, GripOffsets = grips, TipOffsets = tips,
                };
            }
            var landMid = new Vector2[Dirs * 4];
            var landRight = new Vector2[Dirs * 4];
            for (int i = 0; i < landMid.Length; i++) { landMid[i] = new Vector2(0.05f * i, 0.3f); landRight[i] = new Vector2(0.2f, 0.03f * i); }
            rod.Configure(rodStates, new[] { 0, 1, 7 }, null,
                          new[] { Species(Bass, 3.1f), Species(Cod, 5.59f) },
                          landMid, landRight, 4, Sheet("splash", 1, 7), Cell("ripple"));
        }

        FishSpeciesVisual Species(string id, float kg)
        {
            const int per = 3;
            var mouths = new Vector2[Dirs * per];
            for (int i = 0; i < mouths.Length; i++) mouths[i] = new Vector2(0.02f * i, -0.01f * i);
            var jumpMouths = new Vector2[Dirs * FishJumpArc.Frames];
            for (int i = 0; i < jumpMouths.Length; i++) jumpMouths[i] = new Vector2(-0.015f * i, 0.012f * i);
            var rung = new FishRungVisual
            {
                Kg = kg, TwoHanded = id == Cod,
                ShadowFrames = Sheet(id + "_shadow", per), ShadowFramesPerDir = per,
                DartFrames = Sheet(id + "_dart", per), DartFramesPerDir = per, DartMouthOffsets = mouths,
                ThrashFrames = Sheet(id + "_thrash", per), ThrashFramesPerDir = per, ThrashMouthOffsets = mouths,
                HeldFrames = Sheet(id + "_held", per), HeldFramesPerDir = per,
                JumpFrames = Sheet(id + "_jump", FishJumpArc.Frames), JumpFramesPerDir = FishJumpArc.Frames,
                JumpMouthOffsets = jumpMouths,
            };
            return new FishSpeciesVisual { FishId = id, Rungs = new[] { rung } };
        }

        static FishingState State(FishingPhase phase, string fish = null, float tension = 0f, float offX = 0f,
                                  float offY = 0f, float aimX = 0f, float aimY = 0f, bool slack = false,
                                  float kg = 0f)
            => new FishingState(phase, tension, 0.3f, fish, fish, FishCategory.InshoreGroundfish,
                                fish != null ? kg : 0f, 0.4f, slack, tension * 0.8f, offX, offY,
                                0f, aimX, aimY, 0f, offX * 0.1f, offY * 0.1f, 0f);

        static void Publish(RodFightPresenter rod, FishingState s) => EventBus.Publish(new FishingStateChanged(s));

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static T Child<T>(RodFightPresenter rod, string name) where T : Component
        {
            Transform t = rod.transform.Find(name);
            Assert.IsNotNull(t, $"harness: the presenter made no '{name}'");
            return t.GetComponent<T>();
        }

        Sprite[] Sheet(string name, int perDir, int dirs = Dirs)
        {
            var s = new Sprite[dirs * perDir];
            for (int i = 0; i < s.Length; i++) s[i] = Cell($"{name}#{i}");
            return s;
        }

        Sprite Cell(string name)
        {
            var tex = Track(new Texture2D(2, 2));
            var sp = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f));
            sp.name = name;
            return sp;
        }

        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        static string Name(Sprite s) => s != null ? s.name : "null";
        static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => $"({F(v.x)},{F(v.y)},{F(v.z)})";
        static string V(Vector2 v) => $"({F(v.x)},{F(v.y)})";
        static string C(Color c) => $"[{F(c.r)},{F(c.g)},{F(c.b)},{F(c.a)}]";
    }
}
