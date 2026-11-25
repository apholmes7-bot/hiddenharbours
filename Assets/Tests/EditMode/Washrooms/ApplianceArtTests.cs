using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode.Washrooms
{
    /// <summary>The appliance art's logic: the wall it faces, where its seat lands, what each fixture shows as
    /// a wash or a sit runs, the soap gate on the suds, and the figure's climb and bubbles.</summary>
    public class ApplianceArtTests
    {
        static readonly WashVisualsSettings S = WashVisualsSettings.Default;

        // ---- the table ----------------------------------------------------------------------------------

        [Test]
        public void EveryKindHasItsOwnRowInTheTable()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(ApplianceKind)).Length, ApplianceSheets.All.Length);
            for (int i = 0; i < ApplianceSheets.All.Length; i++)
                Assert.AreEqual((ApplianceKind)i, ApplianceSheets.All[i].Kind, "All[] is indexed by kind");
        }

        [Test]
        public void EveryNamedRowIsInsideItsSheet()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
            {
                int last = Mathf.Max(l.Idle, l.FillLow, l.FillMid, l.Full, l.PourLow, l.PourMid, l.NearFull,
                                     LoopEnd(l.FrothFirst), LoopEnd(l.NearFrothFirst), LoopEnd(l.RunFirst),
                                     LoopEnd(l.NearRunFirst), LoopEnd(l.FlushFirst), LoopEnd(l.DoorFirst));
                Assert.AreEqual(l.Rows - 1, last, $"{l.Stem}: the rows run 0..{l.Rows - 1} with no gap or overrun");
                foreach (int r in l.FillFrames()) Assert.That(r, Is.InRange(0, l.Rows - 1));
                foreach (int r in l.DrainFrames()) Assert.That(r, Is.InRange(0, l.Rows - 1));
            }
        }

        static int LoopEnd(int first) => first < 0 ? -1 : first + ApplianceLayout.LoopFrames - 1;

        [Test]
        public void EveryRowBakesWithOptionsThatSayWhatItIs()
        {
            ApplianceLayout bath = ApplianceSheets.Bath;
            Assert.AreEqual("{}", ApplianceSheetBaker.OptsFor(bath, bath.Idle));
            Assert.AreEqual("{fill:1}", ApplianceSheetBaker.OptsFor(bath, bath.Full));
            Assert.AreEqual("{fill:0.33,pour:true}", ApplianceSheetBaker.OptsFor(bath, bath.PourLow));
            Assert.AreEqual("{fill:1,froth:2}", ApplianceSheetBaker.OptsFor(bath, bath.FrothFirst + 1));
            Assert.AreEqual("{fill:1,froth:3,split:'near'}", ApplianceSheetBaker.OptsFor(bath, bath.NearFrothFirst + 2));
            Assert.AreEqual("{run:1,split:'near'}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Shower, ApplianceSheets.Shower.NearRunFirst));
            Assert.AreEqual("{flush:3}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Toilet, 3));
            Assert.AreEqual("{fill:0.5,pour:true}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Basin, ApplianceSheets.Basin.PourMid));
            Assert.AreEqual("{}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Fridge, ApplianceSheets.Fridge.Idle));
            Assert.AreEqual("{door:1}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Fridge, ApplianceSheets.Fridge.DoorFirst));
            Assert.AreEqual("{door:3}", ApplianceSheetBaker.OptsFor(ApplianceSheets.Fridge, ApplianceSheets.Fridge.DoorFirst + 2));
            Assert.AreEqual("{fill:0.5,pour:true}", ApplianceSheetBaker.OptsFor(ApplianceSheets.KitchenSink, ApplianceSheets.KitchenSink.PourMid));
            Assert.AreEqual("{fill:1,froth:3}", ApplianceSheetBaker.OptsFor(ApplianceSheets.KitchenSink, ApplianceSheets.KitchenSink.FrothFirst + 2));

            // No two rows of a sheet bake the same picture.
            foreach (ApplianceLayout l in ApplianceSheets.All)
            {
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int r = 0; r < l.Rows; r++)
                    Assert.IsTrue(seen.Add(ApplianceSheetBaker.OptsFor(l, r)), $"{l.Stem} row {r} repeats another row");
            }
        }

        [Test]
        public void SpriteNamesParseBackToTheirRowAndCell()
        {
            ApplianceLayout l = ApplianceSheets.Bath;
            Assert.IsTrue(ApplianceArt.TryParse(l.SpriteName(12, 7), l.Stem, out int row, out int cell));
            Assert.AreEqual(12, row); Assert.AreEqual(7, cell);
            Assert.IsFalse(ApplianceArt.TryParse("Appliance_basin_s1_d2", l.Stem, out _, out _));
        }

        // ---- facing ---------------------------------------------------------------------------------------

        [TestCase(0f, 1.6f, 0)]    // against the +y (back) wall
        [TestCase(1.7f, 0f, 2)]    // against +x
        [TestCase(0f, -1.6f, 4)]   // against −y
        [TestCase(-1.7f, 0f, 6)]   // against −x
        public void APieceBacksOntoTheWallItStandsAgainst(float x, float y, int expected)
        {
            // A 4 × 4 m room; a 0.6 m deep piece stands 0.3 m off the wall it backs onto.
            Vector2 foot = new Vector2(0.9f, 0.6f);
            Vector2 model = new Vector2(x == 0f ? 0f : Mathf.Sign(x) * (2f - 0.3f), y == 0f ? 0f : Mathf.Sign(y) * (2f - 0.3f));
            Assert.AreEqual(expected, ApplianceFacing.PickWallOffset(model, 4f, 4f, foot));
        }

        [Test]
        public void ACornerPieceBacksOntoTheWallItsDepthFits()
        {
            // The water closet in a corner: 0.35 m from the back wall (its half-depth) and 0.23 m from the side.
            Vector2 foot = ApplianceSheets.Toilet.Foot;
            Vector2 model = new Vector2(-(1.5f - 0.23f), 1.5f - 0.35f);
            Assert.AreEqual(0, ApplianceFacing.PickWallOffset(model, 3f, 3f, foot));
        }

        [Test]
        public void TheCellIsTheRoomsFacingPlusTheWallTurn()
        {
            Assert.AreEqual(0, ApplianceFacing.CellFor(0, 0));
            Assert.AreEqual(6, ApplianceFacing.CellFor(4, 2));
            Assert.AreEqual(2, ApplianceFacing.CellFor(6, 4));
            Assert.AreEqual(5, ApplianceFacing.CellFor(7, 6));
        }

        [Test]
        public void TheProjectionMatchesTheRoomsOwnAtEveryCell()
        {
            Vector2 prop = new Vector2(0.4f, -0.25f);
            for (int cell = 0; cell < 8; cell++)
            {
                var fp = new InteriorFootprint(Vector2.zero, 4f, 4f, cell, 8, IsoGround.GroundDepthScale);
                Vector2 expect = fp.ModelToWorld(prop);
                Vector2 got = ApplianceFacing.ModelToWorldOffset(cell, new Vector3(prop.x, prop.y, 0f));
                Assert.That(Vector2.Distance(expect, got), Is.LessThan(1e-5f), $"cell {cell}");
            }
            // Height lifts straight up the screen.
            Vector2 up = ApplianceFacing.ModelToWorldOffset(3, new Vector3(0f, 0f, 1f));
            Assert.AreEqual(0f, up.x, 1e-6f);
            Assert.AreEqual(IsoGround.HeightScale, up.y, 1e-6f);
        }

        [Test]
        public void AtTheUnturnedCellTheFigureFacesOutOfTheWall()
        {
            // Cell 0 is the piece against the back wall: out of the wall is south.
            Assert.AreEqual(180f, Mathf.Repeat(ApplianceFacing.BearingDegrees(0, new Vector2(0f, -1f)), 360f), 0.01f);
            // The bath faces along its length, toward the taps: east at cell 0.
            Assert.AreEqual(90f, Mathf.Repeat(ApplianceFacing.BearingDegrees(0, ApplianceSheets.Bath.SeatFacing), 360f), 0.01f);
        }

        [Test]
        public void TheEntryIsInFrontOfThePieceLevelWithItsSeat()
        {
            Vector3 e = ApplianceFacing.EntryProp(ApplianceSheets.Bath, 0.35f);
            Assert.AreEqual(ApplianceSheets.Bath.Seat.x, e.x, 1e-6f);
            Assert.AreEqual(-(0.39f + 0.35f), e.y, 1e-5f);
        }

        // ---- the timeline ---------------------------------------------------------------------------------

        [Test]
        public void ABathFillsThenAsksHimInThenHoldsWhileHeClimbsOutThenDrains()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Bath);
            ApplianceLayout b = ApplianceSheets.Bath;

            t.Tick(true, false, 0f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Filling, t.Current);
            Assert.AreEqual(b.PourLow, t.Row);
            Assert.IsFalse(t.PoseOn, "he waits while it fills");

            t.Tick(true, false, S.FillSeconds * 0.6f, S);
            Assert.AreEqual(b.PourMid, t.Row);

            t.Tick(true, false, S.FillSeconds, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Full, t.Current);
            Assert.AreEqual(b.Full, t.Row);
            Assert.IsTrue(t.PoseOn);
            Assert.AreEqual(b.NearFull, t.NearRow, "the front rim draws over him");

            t.Tick(false, false, 0.01f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Holding, t.Current);
            Assert.IsFalse(t.PoseOn, "he climbs out");
            Assert.AreEqual(b.Full, t.Row);
            Assert.AreEqual(b.NearFull, t.NearRow, "still in front of him while he climbs over");

            t.Tick(false, false, S.ClimbSeconds, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Draining, t.Current);
            Assert.AreEqual(b.FillMid, t.Row);
            Assert.AreEqual(-1, t.NearRow);

            t.Tick(false, false, S.DrainSeconds, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Idle, t.Current);
            Assert.AreEqual(b.Idle, t.Row);
        }

        [Test]
        public void TheSudsCycleOnlyWhileTheWashFroths()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Bath);
            ApplianceLayout b = ApplianceSheets.Bath;
            t.Tick(true, true, 0f, S);
            t.Tick(true, true, S.FillSeconds, S);
            Assert.That(t.Row, Is.InRange(b.FrothFirst, b.FrothFirst + 2));
            Assert.That(t.NearRow, Is.InRange(b.NearFrothFirst, b.NearFrothFirst + 2));

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 12; i++) { t.Tick(true, true, 1f / S.FrothFramesPerSecond, S); seen.Add(t.Row); }
            Assert.AreEqual(3, seen.Count, "all three suds frames show");

            t.Tick(true, false, 0.01f, S);
            Assert.AreEqual(b.Full, t.Row, "plain water when the wash is not soaped");
        }

        [Test]
        public void ABasinFillsAndDrainsWithNoPose()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Basin);
            ApplianceLayout l = ApplianceSheets.Basin;
            t.Tick(true, false, 0f, S);
            Assert.AreEqual(l.PourMid, t.Row);
            t.Tick(true, false, S.FillSeconds, S);
            Assert.AreEqual(l.Full, t.Row);
            Assert.IsFalse(t.PoseOn);
            t.Tick(false, false, 0.01f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Draining, t.Current, "no climb out: it drains at once");
            Assert.AreEqual(l.FillMid, t.Row);
        }

        [Test]
        public void AShowerRunsAndStandsHimUnderIt()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Shower);
            ApplianceLayout l = ApplianceSheets.Shower;
            t.Tick(true, false, 0.05f, S);
            Assert.That(t.Row, Is.InRange(l.RunFirst, l.RunFirst + 2));
            Assert.AreEqual(t.Row - l.RunFirst, t.NearRow - l.NearRunFirst, "the near layer runs in step");
            Assert.IsTrue(t.PoseOn);
            t.Tick(false, false, 0.01f, S);
            Assert.AreEqual(l.Idle, t.Row);
            Assert.IsFalse(t.PoseOn);
        }

        [Test]
        public void AWaterClosetHoldsTheSeatThenFlushesOnce()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Toilet);
            ApplianceLayout l = ApplianceSheets.Toilet;
            t.Tick(true, false, 0.2f, S);
            Assert.IsTrue(t.PoseOn);
            Assert.AreEqual(l.Idle, t.Row);
            t.Tick(false, false, 0.01f, S);
            Assert.AreEqual(l.FlushFirst, t.Row);
            t.Tick(false, false, S.FlushSeconds * 0.5f, S);
            Assert.AreEqual(l.FlushFirst + 1, t.Row);
            t.Tick(false, false, S.FlushSeconds, S);
            Assert.AreEqual(l.Idle, t.Row);
        }

        [Test]
        public void SwitchedOffEveryFixtureStandsStill()
        {
            WashVisualsSettings off = S; off.Enabled = false;
            var t = new WashFixtureTimeline(ApplianceSheets.Bath);
            t.Tick(true, true, 5f, off);
            Assert.AreEqual(0, t.Row);
            Assert.IsFalse(t.PoseOn);
            Assert.AreEqual(-1, t.NearRow);
        }

        [Test]
        public void AKitchenSinkFillsFrothsAndDrainsLikeTheBasin()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.KitchenSink);
            ApplianceLayout l = ApplianceSheets.KitchenSink;
            t.Tick(true, true, 0f, S);
            Assert.AreEqual(l.PourMid, t.Row, "the tap runs as it fills");
            t.Tick(true, true, S.FillSeconds, S);
            Assert.That(t.Row, Is.InRange(l.FrothFirst, l.FrothFirst + 2), "soaped hands froth the water");
            Assert.IsFalse(t.PoseOn, "no pose at a sink");
            Assert.AreEqual(-1, t.NearRow);
            t.Tick(false, false, 0.01f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Draining, t.Current);
            t.Tick(false, false, S.DrainSeconds, S);
            Assert.AreEqual(l.Idle, t.Row);
        }

        [Test]
        public void AFridgeDoorSwingsOpenHoldsThenShuts()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.Fridge);
            ApplianceLayout l = ApplianceSheets.Fridge;

            t.Tick(false, false, 1f, S);
            Assert.AreEqual(l.Idle, t.Row, "shut until asked");

            t.Tick(true, false, 0f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Opening, t.Current);
            Assert.AreEqual(l.DoorFirst, t.Row, "ajar first");
            t.Tick(true, false, S.DoorSeconds * 0.6f, S);
            Assert.AreEqual(l.DoorFirst + 1, t.Row, "half open");
            t.Tick(true, false, S.DoorSeconds, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Open, t.Current);
            Assert.AreEqual(l.DoorFirst + 2, t.Row, "wide open");
            t.Tick(true, false, 5f, S);
            Assert.AreEqual(l.DoorFirst + 2, t.Row, "held open while asked");
            Assert.IsFalse(t.PoseOn);
            Assert.AreEqual(-1, t.NearRow);

            t.Tick(false, false, 0f, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Closing, t.Current);
            Assert.AreEqual(l.DoorFirst + 1, t.Row);
            t.Tick(false, false, S.DoorSeconds * 0.6f, S);
            Assert.AreEqual(l.DoorFirst, t.Row);
            t.Tick(false, false, S.DoorSeconds, S);
            Assert.AreEqual(WashFixtureTimeline.Phase.Idle, t.Current);
            Assert.AreEqual(l.Idle, t.Row);
        }

        [Test]
        public void OnlyTheFridgeHasADoor()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
                Assert.AreEqual(l.Kind == ApplianceKind.Fridge, l.HasDoor, l.Stem);
            CollectionAssert.IsEmpty(ApplianceSheets.CookStove.DoorOpenFrames());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ApplianceSheets.Fridge.DoorOpenFrames());
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, ApplianceSheets.Fridge.DoorCloseFrames());
        }

        [Test]
        public void TheKitchenPiecesNeverLeaveIdle()
        {
            var t = new WashFixtureTimeline(ApplianceSheets.CookStove);
            t.Tick(true, true, 3f, S);
            Assert.AreEqual(0, t.Row);
            Assert.IsFalse(t.PoseOn);
        }

        // ---- soap ------------------------------------------------------------------------------------------

        [Test]
        public void SudsAreTheSoapsUnlessToldOtherwise()
        {
            Assert.IsTrue(WashVisualsSettings.Froths(true, S));
            Assert.IsFalse(WashVisualsSettings.Froths(false, S));
            WashVisualsSettings always = S; always.FrothWithoutSoap = true;
            Assert.IsTrue(WashVisualsSettings.Froths(false, always));
        }

        [Test]
        public void AWashWithoutTheSoapFlagIsPlainWater()
        {
            // Until the soap item's flag is on the event this reads false; once it is, a wash without soap
            // still reads false. Either way a stopped wash is never soaped.
            Assert.IsFalse(WashSoapSignal.IsSoaped(default(PlayerWashChanged)));
        }

        // ---- the figure -----------------------------------------------------------------------------------

        [Test]
        public void TheClimbArcsOverTheRimAndLandsOnTheSeat()
        {
            Vector2 a = new Vector2(0f, 0f), b = new Vector2(1f, 0.5f);
            Assert.AreEqual(a, FixturePoseMath.Climb(a, b, 0f, 0.2f));
            Assert.That(Vector2.Distance(b, FixturePoseMath.Climb(a, b, 1f, 0.2f)), Is.LessThan(1e-5f));
            Vector2 mid = FixturePoseMath.Climb(a, b, 0.5f, 0.2f);
            Assert.AreEqual(0.25f + 0.2f, mid.y, 1e-5f, "the arc peaks at the middle");
        }

        [Test]
        public void TheSeatedPoseIsLiftedAgainstTheHeightItWasBakedAt()
        {
            Vector2 seat = new Vector2(2f, 3f);
            Vector2 low = FixturePoseMath.SeatPivot(seat, 0.24f, 0.40f);
            Assert.AreEqual(2f, low.x);
            Assert.AreEqual(3f + (0.24f - 0.40f) * IsoGround.HeightScale, low.y, 1e-6f, "down into the tub");
            Assert.AreEqual(seat, FixturePoseMath.SeatPivot(seat, 0.40f, 0.40f));
        }

        [Test]
        public void TheBubblesRiseFadeAndNeverShareACycleStart()
        {
            float a0 = FrothMath.Age(0f, 0, 6, S), a1 = FrothMath.Age(0f, 1, 6, S);
            Assert.AreNotEqual(a0, a1, "staggered");
            Vector2 low = FrothMath.Offset(0.1f, 0, 0, S), high = FrothMath.Offset(1.0f, 0, 0, S);
            Assert.Greater(high.y, low.y, "they rise");
            Assert.Less(FrothMath.Alpha(S.BubbleLifetimeSeconds * 0.95f, S), FrothMath.Alpha(S.BubbleLifetimeSeconds * 0.3f, S));
            Assert.AreEqual(FrothMath.Hash01(3, 9), FrothMath.Hash01(3, 9), "stable");
            Assert.That(FrothMath.Hash01(3, 9), Is.InRange(0f, 1f));
        }

        [Test]
        public void TheDefaultsAreTheShippedConfig()
        {
            // GameConfig.asset carries the WashVisuals block (GameConfigAssetCoverageTests holds the keys);
            // this pins that a fresh config field starts at the documented defaults.
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(WashVisualsSettings.Default.FillSeconds, cfg.WashVisuals.FillSeconds);
                Assert.AreEqual(WashVisualsSettings.Default.BubbleCount, cfg.WashVisuals.BubbleCount);
                Assert.AreEqual(WashVisualsSettings.Default.DoorSeconds, cfg.WashVisuals.DoorSeconds);
                Assert.Greater(cfg.WashVisuals.DoorSeconds, 0f, "a door swings, it does not snap");
                Assert.IsFalse(cfg.WashVisuals.FrothWithoutSoap);
            }
            finally { Object.DestroyImmediate(cfg); }
        }
    }
}
