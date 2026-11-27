using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The surfaced fish in the water: her frame cut at her waterline on her own art grid
    /// (<see cref="FishWaterlineCut"/>), the rows under it drawn tinted on a second renderer by the one drawer
    /// both fishers use (<see cref="SurfacedFishDrawer"/>), a jump drawn whole, and the churn thrown while she
    /// thrashes on the fight's own clock (<see cref="RodPresenterMath.ChurnBeat"/>), the same twice running.
    /// </summary>
    public class FishWaterlineCutTests
    {
        // The catch pass 2 cell: 64x80 at 32 px a metre, pivot (32,38) from the top = (32,42) from the bottom.
        const int CellW = 64, CellH = 80, PivotFromBottom = 42;
        const float Ppu = 32f;
        const string Fish = "fish.test_thrasher";

        readonly List<Object> _made = new List<Object>();

        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        sealed class Facts : IFishBehaviourFacts
        {
            public bool Jumps(string fishId) => false;
            public float FightJumpPeriodSeconds(string fishId) => 0f;
            public bool TryWeightRangeKg(string fishId, out float minKg, out float maxKg)
            { minKg = 0f; maxKg = 0f; return false; }
        }

        Sprite Cell(Texture2D tex, int column, string name)
        {
            Sprite s = Track(Sprite.Create(tex, new Rect(column * CellW, 0, CellW, CellH),
                                           new Vector2(0.5f, PivotFromBottom / (float)CellH), Ppu, 0,
                                           SpriteMeshType.FullRect));
            s.name = name;
            return s;
        }

        FishSpeciesVisual Species(out Sprite[] thrash)
        {
            const int per = 2, dirs = 8;
            var tex = Track(new Texture2D(CellW * dirs * per, CellH));
            thrash = new Sprite[dirs * per];
            var dart = new Sprite[dirs * per];
            for (int i = 0; i < thrash.Length; i++)
            {
                thrash[i] = Cell(tex, i, $"thrash#{i}");
                dart[i] = Cell(tex, i, $"dart#{i}");
            }
            var rung = new FishRungVisual
            {
                Kg = 1f, ThrashFrames = thrash, ThrashFramesPerDir = per, DartFrames = dart, DartFramesPerDir = per,
            };
            return new FishSpeciesVisual { FishId = Fish, Rungs = new[] { rung } };
        }

        static FishingState Surface(float offX, float offY)
            => new FishingState(FishingPhase.FightSurface, 0.3f, 0.3f, Fish, Fish, FishCategory.Pelagic,
                                1f, 0.4f, false, 0.2f, offX, offY, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        static FishFeelSettings Feel() =>
            GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;

        // ---- the cut itself ---------------------------------------------------------------------------

        [Test]
        public void TheCutRow_IsThePivotRowPlusTheTunedRows_InWholePixels_AndStaysInTheFrame()
        {
            Assert.AreEqual(42, FishWaterlineCut.CutRow(42f, 0, CellH));
            Assert.AreEqual(45, FishWaterlineCut.CutRow(42f, 3, CellH));
            Assert.AreEqual(40, FishWaterlineCut.CutRow(42f, -2, CellH));
            Assert.AreEqual(42, FishWaterlineCut.CutRow(41.6f, 0, CellH), "a pivot off the grid rounds onto it");
            Assert.AreEqual(CellH, FishWaterlineCut.CutRow(42f, 100, CellH));
            Assert.AreEqual(0, FishWaterlineCut.CutRow(42f, -100, CellH));
            Assert.AreEqual(0, FishWaterlineCut.CutRow(float.NaN, 0, CellH));
            Assert.AreEqual(0, FishWaterlineCut.CutRow(42f, 0, 0));
        }

        [Test]
        public void TheTwoHalves_PutEveryArtPixelBack_MeetOnAWholePixelRow_AndAreMadeOnce()
        {
            var tex = Track(new Texture2D(CellW * 2, CellH));
            Sprite frame = Cell(tex, 1, "mackerel_thrash#3");

            Assert.IsTrue(FishWaterlineCut.TrySplit(frame, 0, out Sprite above, out Sprite below));
            Track(above);
            Track(below);
            Assert.AreSame(tex, above.texture, "the halves are the frame's own texture, nothing copied");
            Assert.AreEqual(new Rect(CellW, PivotFromBottom, CellW, CellH - PivotFromBottom), above.rect);
            Assert.AreEqual(new Rect(CellW, 0, CellW, PivotFromBottom), below.rect);
            Assert.AreEqual(frame.pivot.x, above.pivot.x, 1e-4f);
            Assert.AreEqual(frame.pivot.y - PivotFromBottom, above.pivot.y, 1e-4f, "the upper half keeps the frame's pivot");
            Assert.AreEqual(frame.pivot.x, below.pivot.x, 1e-4f);
            Assert.AreEqual(frame.pivot.y, below.pivot.y, 1e-4f, "the lower half keeps the frame's pivot");

            // Drawn at one transform the halves cover the frame exactly, joined at the waterline.
            Assert.AreEqual(frame.bounds.max.y, above.bounds.max.y, 1e-5f);
            Assert.AreEqual(frame.bounds.min.y, below.bounds.min.y, 1e-5f);
            Assert.AreEqual(above.bounds.min.y, below.bounds.max.y, 1e-5f);
            Assert.AreEqual(0f, above.bounds.min.y, 1e-5f, "at a cut of 0 rows the line is her water-surface point");

            Assert.AreEqual(frame.name, above.name, "the upper half answers to the frame's name");
            Assert.AreEqual(frame.name + FishWaterlineCut.UnderSuffix, below.name);
            Assert.AreSame(frame, FishWaterlineCut.SourceOf(above));
            Assert.AreSame(frame, FishWaterlineCut.SourceOf(below));
            Assert.AreSame(frame, FishWaterlineCut.SourceOf(frame), "a frame that is no half is its own source");

            Assert.IsTrue(FishWaterlineCut.TrySplit(frame, 0, out Sprite again, out _));
            Assert.AreSame(above, again, "the halves were made twice");

            Assert.IsTrue(FishWaterlineCut.TrySplit(frame, 2, out Sprite higher, out Sprite higherBelow));
            Track(higher);
            Track(higherBelow);
            Assert.AreEqual(2f / Ppu, higher.bounds.min.y, 1e-5f, "two rows up is two art pixels up");

            Assert.IsFalse(FishWaterlineCut.TrySplit(frame, -PivotFromBottom, out _, out _), "nothing under the line");
            Assert.IsFalse(FishWaterlineCut.TrySplit(frame, CellH, out _, out _), "nothing over the line");
            Assert.IsFalse(FishWaterlineCut.TrySplit(null, 0, out _, out _));
        }

        // ---- the drawer both fishers use ---------------------------------------------------------------

        [Test]
        public void SheThrashesCutAtHerWaterline_TheRowsUnderItTinted_AndAHideTakesBothAway()
        {
            var sr = Track(new GameObject("ThrashingFish")).AddComponent<SpriteRenderer>();
            var drawer = new SurfacedFishDrawer(sr) { ThrashChurnPeriodSecondsOverride = 0f };
            FishingState s = Surface(1f, 2f);
            Assert.IsTrue(drawer.Resolve(in s, new[] { Species(out Sprite[] thrash) }, null, new Facts(), 0f));
            drawer.ResetMotion(new Vector2(1f, 2f));
            drawer.Render(Vector2.zero, in s, 0.02f, 0f, 0f, 0.1f, 1.2f, 1f, 41, 1f, out _, out _);

            FishFeelSettings feel = Feel();
            Assume.That(feel.ThrashCutsAtWaterline, "the shipped feel cuts the thrash");
            Assert.IsTrue(sr.enabled);
            Sprite frame = drawer.DrawnFrame;
            Assert.IsNotNull(frame);
            CollectionAssert.Contains(thrash, frame, "held still she thrashes");
            Assert.AreNotSame(frame, sr.sprite, "she was drawn whole, not cut at her waterline");
            Assert.AreSame(frame, FishWaterlineCut.SourceOf(sr.sprite));

            SpriteRenderer under = drawer.UnderwaterRenderer;
            Assert.IsNotNull(under, "no renderer for her rows under the water");
            Assert.IsTrue(under.enabled);
            Assert.AreSame(frame, FishWaterlineCut.SourceOf(under.sprite));
            Assert.AreEqual(frame.name + FishWaterlineCut.UnderSuffix, under.sprite.name);
            Assert.AreEqual(feel.FishUnderwaterTint, under.color, "the rows under the water are not the tuned tint");
            Assert.AreEqual(41, under.sortingOrder, "her two halves sort as one");
            Assert.AreEqual(sr.sortingLayerID, under.sortingLayerID);
            Assert.AreEqual(sr.transform.position, under.transform.position, "the halves are not drawn at one place");

            drawer.Hide();
            Assert.IsFalse(sr.enabled);
            Assert.IsFalse(under.enabled, "a hide left her rows under the water on screen");
        }

        [Test]
        public void TheChurn_ComesOncePerPeriodWhileSheThrashes_AtHer_TheSameTwiceRunning_AndNeverWhileSheDarts()
        {
            const float period = 0.5f, dt = 0.02f;
            List<(float t, Vector2 at)> Run(Vector2 hold, bool darting)
            {
                var sr = Track(new GameObject("ChurningFish")).AddComponent<SpriteRenderer>();
                var drawer = new SurfacedFishDrawer(sr) { ThrashChurnPeriodSecondsOverride = period };
                FishingState s = Surface(hold.x, hold.y);
                drawer.Resolve(in s, new[] { Species(out _) }, null, new Facts(), 0f);
                drawer.OnSurfaced();
                drawer.ResetMotion(hold);
                var thrown = new List<(float, Vector2)>();
                Vector2 at = hold;
                for (int f = 0; f * dt < 2.99f; f++)
                {
                    float t = f * dt;
                    if (darting) at = hold + new Vector2(t * 3f, 0f);   // 3 m/s: well over the dart speed
                    FishingState now = Surface(at.x, at.y);
                    drawer.Render(Vector2.zero, in now, dt, t, t, 0.1f, 1.2f, 1f, 30, 1f, out bool due, out Vector2 where);
                    if (due) thrown.Add((t, where));
                }
                return thrown;
            }

            var hold = new Vector2(0.5f, 1.5f);
            List<(float t, Vector2 at)> first = Run(hold, false);
            Assert.AreEqual(5, first.Count, "one churn per whole period up to 2.99 s (0.5, 1.0, 1.5, 2.0, 2.5)");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual((i + 1) * period, first[i].t, dt, $"churn {i} off its period");
                Assert.AreEqual(hold, first[i].at, $"churn {i} was not at her");
            }
            CollectionAssert.AreEqual(first, Run(hold, false), "the churn is not the same twice running");
            Assert.IsEmpty(Run(hold, true), "a darting fish churned");
        }

        [Test]
        public void ChurnBeat_CountsWholePeriods_AndIsZeroWhenOff()
        {
            Assert.AreEqual(0, RodPresenterMath.ChurnBeat(0.49, 0.5f));
            Assert.AreEqual(1, RodPresenterMath.ChurnBeat(0.5, 0.5f));
            Assert.AreEqual(5, RodPresenterMath.ChurnBeat(2.7, 0.5f));
            Assert.AreEqual(0, RodPresenterMath.ChurnBeat(10.0, 0f), "a period of 0 is off");
            Assert.AreEqual(0, RodPresenterMath.ChurnBeat(-1.0, 0.5f));
            Assert.AreEqual(0, RodPresenterMath.ChurnBeat(double.NaN, 0.5f));
        }
    }
}
