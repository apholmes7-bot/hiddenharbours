using System.Globalization;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The appliance rig and the runtime table agree: every piece the table bakes is drawn by the rig, at the
    /// footprint and seat the runtime uses to face it, seat the figure, and split its near layer. A rig edit
    /// that moves a seat without the table (or the other way round) puts the player beside the bath.
    /// </summary>
    public class ApplianceRigParityTests
    {
        static IRigScriptHost _host;
        static string _g;

        [OneTimeSetUp]
        public void SetUp()
        {
            _host = RigScriptHostFactory.Create();
            RigEntry entry = RigCatalog.Get(ApplianceSheetBaker.RigKey);
            RigCatalog.Install(_host, entry);
            _g = entry.GlobalName;
        }

        [OneTimeTearDown]
        public void TearDown() { _host?.Dispose(); _host = null; }

        static float N(string js) => (float)_host.EvaluateNumber(js);

        [Test]
        public void EveryPieceInTheTableIsDrawnByTheRig()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
                Assert.IsTrue(_host.EvaluateBool($"{_g}.PROPS['{l.RigName}'] !== undefined"), l.RigName);
        }

        [Test]
        public void TheFootprintsMatchTheRig()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
            {
                Assert.AreEqual(N($"{_g}.PROPS['{l.RigName}'].foot[0]"), l.Foot.x, 1e-4f, $"{l.RigName} width");
                Assert.AreEqual(N($"{_g}.PROPS['{l.RigName}'].foot[1]"), l.Foot.y, 1e-4f, $"{l.RigName} depth");
            }
        }

        [Test]
        public void TheSeatsMatchTheRig()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
            {
                bool rigHasSeat = _host.EvaluateBool($"!!{_g}.PROPS['{l.RigName}'].seat");
                Assert.AreEqual(l.HasSeat, rigHasSeat, $"{l.RigName}: a seat on one side only");
                if (!rigHasSeat) continue;
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual(N($"{_g}.PROPS['{l.RigName}'].seat[{i}]"), l.Seat[i], 1e-4f,
                                    $"{l.RigName} seat[{i.ToString(CultureInfo.InvariantCulture)}]");
            }
        }

        [Test]
        public void ANearLayerIsLessThanTheWholePiece()
        {
            // The split really splits: the near rim of the full bath covers fewer pixels than the bath.
            ApplianceLayout b = ApplianceSheets.Bath;
            int whole = Opaque(_host.EvaluateBytes($"{_g}.render('bath',0,{ApplianceSheetBaker.OptsFor(b, b.Full)})"));
            int near = Opaque(_host.EvaluateBytes($"{_g}.render('bath',0,{ApplianceSheetBaker.OptsFor(b, b.NearFull)})"));
            Assert.Greater(near, 0, "the near layer draws something");
            Assert.Less(near, whole, "and less than the whole tub");
        }

        [Test]
        public void TheFridgeDoorReallySwingsAndShowsTheInside()
        {
            // At the front facing every door frame is its own picture, and the wide-open door covers more of
            // the cell than the shut one (the door stands out beside the cabinet).
            ApplianceLayout f = ApplianceSheets.Fridge;
            byte[] shut = Render("fridge", ApplianceSheetBaker.OptsFor(f, f.Idle));
            byte[] prev = shut;
            for (int r = f.DoorFirst; r < f.DoorFirst + ApplianceLayout.LoopFrames; r++)
            {
                byte[] frame = Render("fridge", ApplianceSheetBaker.OptsFor(f, r));
                Assert.AreNotEqual(Hash(prev), Hash(frame), $"door row {r} draws the same as the row before");
                prev = frame;
            }
            Assert.Greater(Opaque(prev), Opaque(shut), "the open door stands out beside the cabinet");
            Assert.AreEqual(Hash(shut), Hash(Render("fridge", "{door:0}")), "door:0 is the shut fridge");
        }

        [Test]
        public void TheKitchenSinkFillsAndFroths()
        {
            ApplianceLayout k = ApplianceSheets.KitchenSink;
            var seen = new System.Collections.Generic.HashSet<long>();
            for (int r = 0; r < k.Rows; r++)
                Assert.IsTrue(seen.Add(Hash(Render("kitchenSink", ApplianceSheetBaker.OptsFor(k, r)))),
                              $"kitchen sink row {r} draws the same picture as another row");
        }

        [Test]
        public void TheNewPiecesStandAtFridgeAndCounterHeight()
        {
            // A household fridge about his head height; the sink's swan-neck tap rises above a 0.9 m counter.
            Assert.AreEqual(1.65f, N($"{_g}.height('fridge',{{}})"), 0.05f);
            Assert.AreEqual(1.17f, N($"{_g}.height('kitchenSink',{{}})"), 0.05f);
        }

        static byte[] Render(string piece, string opts) => _host.EvaluateBytes($"{_g}.render('{piece}',0,{opts})");

        static long Hash(byte[] rgba)
        {
            unchecked
            {
                long h = -3750763034362895579;
                for (int i = 0; i < rgba.Length; i++) h = (h ^ rgba[i]) * 1099511628211;
                return h;
            }
        }

        static int Opaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) n++;
            return n;
        }
    }
}
