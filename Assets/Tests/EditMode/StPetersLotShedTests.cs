using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;          // StPetersCamperLot, StPetersGinnyPlot, StPetersBuilder
using HiddenHarbours.Tools.RigBaking;     // RigScriptHostFactory, RigCatalog
using HiddenHarbours.World;               // TidalTerrain

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CAMPER LOT'S SMALL SHED, ON THE GROUND</b> — that the rig fits the ground
    /// <see cref="StPetersCamperLot"/> reserves for it, that the rig's frame lands the door where the lot
    /// says the door faces, and that the reserved spot and the ground in front of the door are dry.
    ///
    /// <para>The shed's own fit (rods, shelves, door, walking room) is <c>SmallShedRigTests</c>. This
    /// file lives beside <c>StPetersCamperLotTests</c> because it needs the St Peters editor code and
    /// the terrain, read the same way that suite reads them. Nothing is placed in the scene yet.</para>
    /// </summary>
    public class StPetersLotShedTests
    {
        const string Sidecar = "docs/art/rigs/small-shed-kit/smallShedRig.lot_shed.gameplay.json";
        const float FootRadius = 0.35f;
        const float WalkingGap = 2.0f;   // the plot's walking gap (StPetersCamperLotTests.WalkingGapMetres)
        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // 2.2
        const float Eps = 1e-3f;

        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_LotShedTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static IRigScriptHost SidecarHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            string path = Path.Combine(RigCatalog.RepoRoot, Sidecar.Replace('/', Path.DirectorySeparatorChar));
            host.Execute("var __sc = " + File.ReadAllText(path) + ";");
            return host;
        }

        static float N(IRigScriptHost h, string expr) => (float)h.EvaluateNumber(expr);

        /// <summary>The rig frame on the lot: rig +y is away from the door, rig +x its right-handed
        /// perpendicular. World = ShedPos + x * R + y * U.</summary>
        static Vector2 U => -StPetersCamperLot.ShedDoorDirection;
        static Vector2 R => new Vector2(U.y, -U.x);
        static Vector2 World(Vector2 rig) => StPetersCamperLot.ShedPos + rig.x * R + rig.y * U;

        // =================================================================================

        [Test]
        public void TheRigsReservation_IsTheLotsReservation_AndThePlanFitsIt()
        {
            using IRigScriptHost h = SidecarHost();
            Assert.AreEqual(StPetersCamperLot.ShedSize.x, N(h, "__sc.LOT.reservation.footprint_m.along_camper"), 1e-4f,
                "the rig and StPetersCamperLot disagree on the reserved length");
            Assert.AreEqual(StPetersCamperLot.ShedSize.y, N(h, "__sc.LOT.reservation.footprint_m.across_camper"), 1e-4f,
                "the rig and StPetersCamperLot disagree on the reserved depth");
            Assert.AreEqual(StPetersCamperLot.ShedFootprintRadiusMetres, N(h, "__sc.LOT.reservation.reserve_radius_m"), Eps);

            float planX = N(h, "__sc.FIT.plan_with_eaves_m[0]"), planY = N(h, "__sc.FIT.plan_with_eaves_m[1]");
            Assert.LessOrEqual(planX, StPetersCamperLot.ShedSize.x + Eps, "the shed's plan is longer than its ground");
            Assert.LessOrEqual(planY, StPetersCamperLot.ShedSize.y + Eps, "the shed's plan is deeper than its ground");
            Assert.LessOrEqual(new Vector2(planX, planY).magnitude * 0.5f, StPetersCamperLot.ShedFootprintRadiusMetres + Eps,
                "the eave corners leave the reserve circle the camper and the sheds were spaced against");
        }

        [Test]
        public void TheRigFrame_PutsTheDoorWhereTheLotFacesIt_AndTheLongSideAlongTheCamper()
        {
            // NoseDirection, the axis ShedPos is laid along (the camper as the player sees it).
            Assert.AreEqual(1f, Mathf.Abs(Vector2.Dot(R, StPetersCamperLot.NoseDirection)), 1e-4f,
                "the shed's long side (rig x, ShedSize.x) must run along the camper");
            Assert.Greater(R.x * U.y - R.y * U.x, 0f, "+x then +y must turn counter-clockwise (the rig frame is right-handed)");
            Vector2 doorOut = (World(new Vector2(0f, -1f)) - StPetersCamperLot.ShedPos).normalized;
            Assert.Greater(Vector2.Dot(doorOut, StPetersCamperLot.ShedDoorDirection), 0.999f, "rig -y is not the lot's door side");

            // the sidecar's facing is the compass point rig +y faces (N = 0, clockwise), counter-clockwise bake
            int facing = ((Mathf.RoundToInt(Mathf.Atan2(U.x, U.y) * Mathf.Rad2Deg / 45f) % 8) + 8) % 8;
            using IRigScriptHost h = SidecarHost();
            Assert.AreEqual(facing, (int)N(h, "__sc.LOT.facing"), "the sidecar's lot facing is stale");
            Assert.AreEqual((8 - facing) % 8, (int)N(h, "__sc.LOT.rig_dir"), "rig_dir must follow the counter-clockwise convention");
        }

        /// <summary>
        /// 🔴 <b>You cannot keep your rods below the tide.</b> The guard the camper and every shed on the
        /// plot carry: the shed's centre, its eave corners and the ground in front of the door all stand
        /// above spring high water and inside the island's plateau.
        /// </summary>
        [Test]
        public void TheReservedSpot_AndTheGroundAtTheDoor_AreDry()
        {
            using IRigScriptHost h = SidecarHost();
            float hx = N(h, "__sc.FIT.plan_with_eaves_m[0]") * 0.5f, hy = N(h, "__sc.FIT.plan_with_eaves_m[1]") * 0.5f;
            var probes = new[]
            {
                (Vector2.zero, "the shed's centre"),
                (new Vector2(-hx, -hy), "the south-west eave corner"), (new Vector2(hx, -hy), "the south-east eave corner"),
                (new Vector2(hx, hy), "the north-east eave corner"), (new Vector2(-hx, hy), "the north-west eave corner"),
                (new Vector2(N(h, "__sc.THRESHOLD.outside_landing.centre[0]"), N(h, "__sc.THRESHOLD.outside_landing.centre[1]")), "the ground at the door"),
            };
            foreach (var (rig, what) in probes)
            {
                Vector2 p = World(rig);
                float e = _terrain.ElevationAt(p);
                Assert.Greater(e, SpringHighWater, $"{what} at {p} stands at {e:0.00} m, at or below spring high water ({SpringHighWater:0.00} m)");
                Assert.LessOrEqual(
                    TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter, StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY),
                    StPetersBuilder.IslandRadius, $"{what} at {p} is past the plateau edge");
            }
        }

        [Test]
        public void TheGroundAtTheDoor_IsClearOfTheCamperAndTheSheds_AndOnHerLand()
        {
            using IRigScriptHost h = SidecarHost();
            Vector2 landing = World(new Vector2(N(h, "__sc.THRESHOLD.outside_landing.centre[0]"), N(h, "__sc.THRESHOLD.outside_landing.centre[1]")));
            float camperR = StPetersCamperLot.FootprintRadiusMetres;
            Assert.Greater(camperR, 0f, "the camper sidecar did not read: nothing below would mean anything");
            float g = Vector2.Distance(landing, StPetersCamperLot.LotPos) - camperR - FootRadius;
            Assert.GreaterOrEqual(g, WalkingGap, $"the shed's doorstep is {g:0.00} m from the camper's ground");
            foreach (var s in StPetersGinnyPlot.Sheds)
            {
                float d = Vector2.Distance(landing, s.Position) - s.FootprintRadiusMetres - FootRadius;
                Assert.GreaterOrEqual(d, WalkingGap, $"the shed's doorstep is {d:0.00} m from {s.Key}");
            }
            float reach = Vector2.Distance(landing, StPetersGinnyPlot.CottagePos) + FootRadius;
            Assert.LessOrEqual(reach, StPetersGinnyPlot.ClearingRadius, $"the shed's doorstep reaches {reach:0.00} m, outside her clearing");
            Assert.Greater(Vector2.Dot(landing - StPetersCamperLot.ShedPos, StPetersCamperLot.ShedDoorDirection), 0f,
                "the doorstep is not on the door side of the shed");
        }
    }
}
