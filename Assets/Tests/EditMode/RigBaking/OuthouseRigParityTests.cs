using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The outhouse's rig and its runtime table agree: the piece is drawn by the rig, its footprint, seat and
    /// heading are the table's, every row draws something different, and the open door shows more than the
    /// shut booth. A rig edit that moves the bench or the walls
    /// without the table would sit him in the wall or fit the collider to the wrong size.
    /// </summary>
    public class OuthouseRigParityTests
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
        static string P => $"{_g}.PROPS['{OuthouseSheet.RigName}']";

        [Test]
        public void TheOuthouseIsARigPiece_WithTheTablesFootprintSeatAndHeading()
        {
            Assert.IsTrue(_host.EvaluateBool($"{P} !== undefined"), "the rig has no outhouse");
            Assert.AreEqual(OuthouseSheet.Foot.x, N($"{P}.foot[0]"), 1e-4f, "width");
            Assert.AreEqual(OuthouseSheet.Foot.y, N($"{P}.foot[1]"), 1e-4f, "depth");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(OuthouseSheet.Seat[i], N($"{P}.seat[{i}]"), 1e-4f, $"seat[{i}]");
            float heading = N($"{P}.heading") * Mathf.Deg2Rad;
            Assert.AreEqual(OuthouseSheet.SeatFacing.x, Mathf.Cos(heading), 1e-4f, "he faces out of the door (x)");
            Assert.AreEqual(OuthouseSheet.SeatFacing.y, Mathf.Sin(heading), 1e-4f, "he faces out of the door (y)");
        }

        [Test]
        public void TheSeatIsInsideTheWalls_InFrontOfTheBench()
        {
            Assert.Less(Mathf.Abs(OuthouseSheet.Seat.x), OuthouseSheet.Foot.x * 0.5f, "seat across the booth");
            Assert.Less(Mathf.Abs(OuthouseSheet.Seat.y), OuthouseSheet.Foot.y * 0.5f, "seat within its depth");
            Assert.Greater(OuthouseSheet.Seat.z, 0f, "the bench is off the floor");
            Assert.Less(N($"{_g}.height('{OuthouseSheet.RigName}',{{}})"), 2.6f, "a privy, not a barn");
        }

        [Test]
        public void EveryRowDrawsSomethingOfItsOwn_TheOpenDoorShowsMore()
        {
            var counts = new int[OuthouseSheet.Rows];
            var pictures = new byte[OuthouseSheet.Rows][];
            for (int r = 0; r < OuthouseSheet.Rows; r++)
            {
                pictures[r] = _host.EvaluateBytes($"{_g}.render('{OuthouseSheet.RigName}',0,{OuthouseSheet.OptsFor(r)})");
                counts[r] = Opaque(pictures[r]);
                Assert.Greater(counts[r], 0, $"row {r} draws nothing");
            }
            for (int a = 0; a < OuthouseSheet.Rows; a++)
            for (int b = a + 1; b < OuthouseSheet.Rows; b++)
                Assert.Greater(Differ(pictures[a], pictures[b]), 0, $"rows {a} and {b} draw the same picture");
            Assert.Greater(counts[OuthouseSheet.Open], counts[OuthouseSheet.Closed], "the open door swings out past the booth");
        }

        static int Opaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) n++;
            return n;
        }

        static int Differ(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }
    }
}
