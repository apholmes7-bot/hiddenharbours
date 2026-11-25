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
    /// in are the only time there is.
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
