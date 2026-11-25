using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Cleanliness slice 2, the stink lines: how many show for a given dirt, on and off at the threshold,
    /// where he is drawn, and the pool that is made once and never grows.
    /// </summary>
    public class StinkLinesTests
    {
        private GameObject _go;

        private static StinkLinesSettings D => StinkLinesSettings.Default;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private StinkLinesPresenter MakePlayer(int bodyOrder = 40)
        {
            _go = new GameObject("TestPlayer");
            var body = _go.AddComponent<SpriteRenderer>();
            body.sortingOrder = bodyOrder;
            return _go.AddComponent<StinkLinesPresenter>();
        }

        // ---- count as a function of cleanliness ------------------------------------------------------

        [Test]
        public void Clean_ShowsNoLines()
        {
            StinkLinesSettings s = D;
            Assert.AreEqual(0, StinkLinesMath.LineCount(0f, in s));
            Assert.AreEqual(0, StinkLinesMath.LineCount(0.3f, in s));
        }

        [Test]
        public void Threshold_OffJustBelow_OnAtIt()
        {
            StinkLinesSettings s = D;
            Assert.AreEqual(0, StinkLinesMath.LineCount(s.StartAtDirt - 0.001f, in s));
            Assert.AreEqual(s.MinLines, StinkLinesMath.LineCount(s.StartAtDirt, in s));
        }

        [Test]
        public void Filthy_ShowsMaxLines_AndNeverMore()
        {
            StinkLinesSettings s = D;
            Assert.AreEqual(s.MaxLines, StinkLinesMath.LineCount(s.FullAtDirt, in s));
            Assert.AreEqual(s.MaxLines, StinkLinesMath.LineCount(1f, in s));
            Assert.AreEqual(s.MaxLines, StinkLinesMath.LineCount(5f, in s));
        }

        [Test]
        public void Count_NeverFalls_AsDirtRises_AndStepsThroughEveryCount()
        {
            StinkLinesSettings s = D;
            int last = 0;
            var seen = new bool[s.MaxLines + 1];
            for (int i = 0; i <= 1000; i++)
            {
                int n = StinkLinesMath.LineCount(i / 1000f, in s);
                Assert.GreaterOrEqual(n, last, $"dirt {i / 1000f}");
                Assert.LessOrEqual(n, s.MaxLines);
                seen[n] = true;
                last = n;
            }
            for (int n = 0; n <= s.MaxLines; n++) Assert.IsTrue(seen[n], $"count {n} is reached");
        }

        [Test]
        public void ShippedSteps_OneTwoThree_EvenlyOverGrubbyToFilthy()
        {
            StinkLinesSettings s = D;
            Assert.AreEqual(1, StinkLinesMath.LineCount(0.55f, in s));
            Assert.AreEqual(2, StinkLinesMath.LineCount(0.75f, in s));   // stinky: villagers notice
            Assert.AreEqual(3, StinkLinesMath.LineCount(0.9f, in s));
        }

        [Test]
        public void Disabled_ShowsNothing()
        {
            StinkLinesSettings s = D;
            s.Enabled = false;
            Assert.AreEqual(0, StinkLinesMath.LineCount(1f, in s));
        }

        [Test]
        public void BadSettings_DoNotThrow_AndStayInRange()
        {
            StinkLinesSettings s = D;
            s.MinLines = 9;          // more than the max
            s.FullAtDirt = s.StartAtDirt;   // zero span
            Assert.AreEqual(s.MaxLines, StinkLinesMath.LineCount(s.StartAtDirt, in s));
            s.MaxLines = 0;
            Assert.AreEqual(0, StinkLinesMath.LineCount(1f, in s));
        }

        // ---- where he is drawn -----------------------------------------------------------------------

        [Test]
        public void FigureShown_FollowsWhereHeIsDrawn()
        {
            Assert.IsTrue(StinkLinesMath.FigureShown(false, false, true, false), "ashore, body drawn");
            Assert.IsTrue(StinkLinesMath.FigureShown(true, false, false, false), "aboard, rider drawn");
            Assert.IsFalse(StinkLinesMath.FigureShown(false, false, false, false), "at a helm or in a cab");
            Assert.IsFalse(StinkLinesMath.FigureShown(true, true, false, false), "below decks");
            Assert.IsTrue(StinkLinesMath.FigureShown(true, true, false, true), "below decks, owner allows");
        }

        [Test]
        public void Lines_RiseFromTheHead_AndFadeInAndOut()
        {
            StinkLinesSettings s = D;
            Vector2 start = StinkLinesMath.Offset(0f, 0, in s);
            Vector2 later = StinkLinesMath.Offset(s.LifetimeSeconds * 0.5f, 0, in s);
            Assert.AreEqual(s.HeadOffsetMetres.y, start.y, 1e-5f);
            Assert.Greater(later.y, start.y);
            Assert.AreEqual(0f, StinkLinesMath.Alpha(0f, in s), 1e-5f);
            Assert.AreEqual(1f, StinkLinesMath.Alpha(s.LifetimeSeconds * 0.5f, in s), 1e-5f);
            Assert.AreEqual(0f, StinkLinesMath.Alpha(s.LifetimeSeconds, in s), 1e-5f);
            for (int i = 0; i < 3; i++)
            {
                float age = StinkLinesMath.Age(123.4f, i, 3, in s);
                Assert.GreaterOrEqual(age, 0f);
                Assert.Less(age, s.LifetimeSeconds + 1e-4f);
            }
        }

        [Test]
        public void Columns_MiddleThenRightThenLeft()
        {
            StinkLinesSettings s = D;
            Assert.AreEqual(0f, StinkLinesMath.ColumnX(0, in s));
            Assert.AreEqual(s.SpreadMetres, StinkLinesMath.ColumnX(1, in s), 1e-6f);
            Assert.AreEqual(-s.SpreadMetres, StinkLinesMath.ColumnX(2, in s), 1e-6f);
        }

        // ---- the presenter and its pool --------------------------------------------------------------

        [Test]
        public void Clean_MakesNoPool()
        {
            StinkLinesPresenter p = MakePlayer();
            StinkLinesSettings s = D;
            Assert.AreEqual(0, p.Apply(0.1f, true, 0f, in s));
            Assert.AreEqual(0, p.PoolSize);
            Assert.AreEqual(0, _go.transform.childCount);
        }

        [Test]
        public void Pool_IsMadeOnce_AndNeverGrows()
        {
            StinkLinesPresenter p = MakePlayer();
            StinkLinesSettings s = D;
            p.Apply(1f, true, 0f, in s);
            int size = p.PoolSize;
            int children = _go.transform.childCount;
            Assert.AreEqual(s.MaxLines, size);
            Assert.AreEqual(size, children);

            for (int frame = 0; frame < 600; frame++)
            {
                float dirt = Mathf.PingPong(frame / 100f, 1f);
                bool shown = frame % 97 != 0;
                int n = p.Apply(dirt, shown, frame / 60f, in s);
                Assert.AreEqual(shown ? StinkLinesMath.LineCount(dirt, in s) : 0, n, $"frame {frame}");
                Assert.AreEqual(size, p.PoolSize);
                Assert.AreEqual(children, _go.transform.childCount);
            }

            s.MaxLines = 10;   // a bigger max later does not grow the pool, it caps at what was made
            Assert.AreEqual(size, p.Apply(1f, true, 0f, in s));
            Assert.AreEqual(size, p.PoolSize);
        }

        [Test]
        public void Washing_HidesEveryLine()
        {
            StinkLinesPresenter p = MakePlayer();
            StinkLinesSettings s = D;
            Assert.AreEqual(s.MaxLines, p.Apply(1f, true, 0.7f, in s));
            Assert.AreEqual(0, p.Apply(0f, true, 0.8f, in s));
            for (int i = 0; i < p.PoolSize; i++) Assert.IsFalse(p.Line(i).enabled, $"line {i}");
        }

        [Test]
        public void HiddenFigure_HidesEveryLine()
        {
            StinkLinesPresenter p = MakePlayer();
            StinkLinesSettings s = D;
            p.Apply(1f, true, 0.7f, in s);
            Assert.AreEqual(0, p.Apply(1f, false, 0.8f, in s));
            for (int i = 0; i < p.PoolSize; i++) Assert.IsFalse(p.Line(i).enabled, $"line {i}");
        }

        [Test]
        public void Lines_SortInTheBodysBand_AboveHim_AndFollowHim()
        {
            StinkLinesPresenter p = MakePlayer(bodyOrder: 57);
            StinkLinesSettings s = D;
            p.Apply(1f, true, 0.7f, in s);
            var body = _go.GetComponent<SpriteRenderer>();
            for (int i = 0; i < p.ShownCount; i++)
            {
                SpriteRenderer line = p.Line(i);
                Assert.IsTrue(line.enabled);
                Assert.AreEqual(body.sortingLayerID, line.sortingLayerID);
                Assert.AreEqual(57 + s.OrdersAboveFigure, line.sortingOrder);
                Assert.AreSame(_go.transform, line.transform.parent, "a child of the player: it follows him");
                Assert.GreaterOrEqual(line.transform.localPosition.y, s.HeadOffsetMetres.y - 1e-4f, "above his head");
            }
        }

        // ---- the shipped config ----------------------------------------------------------------------

        [Test]
        public void ShippedConfig_MatchesTheDefaults()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "GameConfig.asset");
            StinkLinesSettings shipped = config.StinkLines, d = D;
            Assert.AreEqual(d.Enabled, shipped.Enabled);
            Assert.AreEqual(d.StartAtDirt, shipped.StartAtDirt, 1e-6f);
            Assert.AreEqual(d.FullAtDirt, shipped.FullAtDirt, 1e-6f);
            Assert.AreEqual(d.MinLines, shipped.MinLines);
            Assert.AreEqual(d.MaxLines, shipped.MaxLines);
            Assert.AreEqual(d.RiseMetresPerSecond, shipped.RiseMetresPerSecond, 1e-6f);
            Assert.AreEqual(d.LifetimeSeconds, shipped.LifetimeSeconds, 1e-6f);
            Assert.AreEqual(d.FadeSeconds, shipped.FadeSeconds, 1e-6f);
            Assert.AreEqual(d.SwayMetres, shipped.SwayMetres, 1e-6f);
            Assert.AreEqual(d.SwayCyclesPerSecond, shipped.SwayCyclesPerSecond, 1e-6f);
            Assert.AreEqual(d.HeadOffsetMetres.y, shipped.HeadOffsetMetres.y, 1e-6f);
            Assert.AreEqual(d.SpreadMetres, shipped.SpreadMetres, 1e-6f);
            Assert.AreEqual(d.LineHeightMetres, shipped.LineHeightMetres, 1e-6f);
            Assert.AreEqual(d.Color.g, shipped.Color.g, 1e-4f);
            Assert.AreEqual(d.OrdersAboveFigure, shipped.OrdersAboveFigure);
            Assert.AreEqual(d.ResampleSeconds, shipped.ResampleSeconds, 1e-6f);
            Assert.AreEqual(d.ShowBelowDecks, shipped.ShowBelowDecks);
        }
    }
}
