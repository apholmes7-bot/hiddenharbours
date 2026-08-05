using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SaveData v9 → v10 (ADR 0025 S6 — the chartplotter's navigation: marked waypoints, the planned
    /// route and the track breadcrumb). The step ADR 0030 named in advance when it said the per-hull
    /// instrument shape needed no further bump "for S3–S5" but that the chartplotter's waypoints and
    /// routes "are a genuinely different shape and still need their own step".
    ///
    /// <para><b>Two different obligations, tested apart.</b> v9's bump had to HEAL (a zero range is a
    /// divide-by-zero). This one mostly has to ADD — a v9 player had no navigation and must get exactly
    /// that, an unmarked chart, with nothing invented. The healing here is for a DIFFERENT hazard: rows
    /// that cannot be drawn honestly at all (a NaN position, a region-less row). Those are DROPPED
    /// rather than repaired, because unlike a range there is no correct position to substitute — putting
    /// a NaN waypoint at the origin would plant a mark on water the player never marked.</para>
    ///
    /// <para><b>Asserted off <c>JsonUtility.FromJson&lt;SaveData&gt;</c> DIRECTLY.</b>
    /// <c>SaveSerialization.FromJson</c> already migrates, so a migration defect is invisible through the
    /// public path (the S3 Step-0 finding). Every blob below is deserialized raw and then migrated by
    /// hand, and the v9-shaped JSON is proved genuinely v9 (the nav keys absent) before it is trusted.</para>
    /// </summary>
    public class SaveMigrationV10Tests
    {
        private const string Region = "region.st_peters";
        private const string Other = "region.coddle_cove";

        /// <summary>A v9 save as it exists on disk today: real play state, and NO nav keys at all.</summary>
        private const string V9Json =
            "{\"SchemaVersion\":9,\"WorldSeed\":4242,\"GameTimeSeconds\":9001.5,\"Money\":175," +
            "\"ActiveHullId\":\"boat.novi\",\"OwnedBoats\":[\"boat.dory\",\"boat.novi\"]}";

        // ---- the add ---------------------------------------------------------------------------------

        [Test]
        public void TheV9Blob_IsGenuinelyV9_BeforeAnythingElseTrustsIt()
        {
            // Guard the guard: if this JSON ever gained the nav keys, every assertion below would pass
            // for the wrong reason.
            StringAssert.DoesNotContain("NavWaypoints", V9Json);
            StringAssert.DoesNotContain("NavRoute", V9Json);
            StringAssert.DoesNotContain("NavTrack", V9Json);

            SaveData raw = JsonUtility.FromJson<SaveData>(V9Json);
            Assert.AreEqual(9, raw.SchemaVersion, "the blob must arrive as v9, un-migrated");
        }

        [Test]
        public void V9_MigratesToCurrent_WithEmptyNavigation_AndInventsNothing()
        {
            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V9Json));

            // VERSION-AGNOSTIC on the counter (the SaveMigrationV3Tests rule): this test owns the v10
            // CONTRACT, not the value of a counter every later lane bumps.
            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion,
                "a v9 blob climbs the whole chain to the current schema");
            Assert.GreaterOrEqual(d.SchemaVersion, 10, "and v10's navigation exists from that climb");

            Assert.IsNotNull(d.NavWaypoints);
            Assert.IsNotNull(d.NavRoute);
            Assert.IsNotNull(d.NavTrack);
            Assert.IsEmpty(d.NavWaypoints, "a v9 player had marked nothing — the chart comes up unmarked");
            Assert.IsEmpty(d.NavRoute, "and had planned no passage");
            Assert.IsEmpty(d.NavTrack, "and has no history the game could honestly claim to remember");

            // …and the step reinterprets nothing else.
            Assert.AreEqual(4242, d.WorldSeed);
            Assert.AreEqual(9001.5, d.GameTimeSeconds, 1e-9);
            Assert.AreEqual(175, d.Money);
            Assert.AreEqual("boat.novi", d.ActiveHullId);
            CollectionAssert.AreEqual(new[] { "boat.dory", "boat.novi" }, d.OwnedBoats);
        }

        [Test]
        public void AV9Save_RoundTrips_LoadResaveReload_Identically()
        {
            // §3.3: a v9 save loads, invents no data, re-saves at v10, and reloads the same.
            SaveData first = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V9Json));
            NavLocker.AddWaypoint(first, new NavWaypoint(Region, "WRECK", new Vector2(120f, -40f),
                                                         NavWaypointKind.Hazard),
                                  ChartplotterSettings.Default);
            NavLocker.RecordTrack(first, Region, new Vector2(0f, 0f), ChartplotterSettings.Default);
            NavLocker.RecordTrack(first, Region, new Vector2(50f, 0f), ChartplotterSettings.Default);

            string json = JsonUtility.ToJson(first);
            SaveData second = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));

            Assert.AreEqual(SaveMigration.CurrentVersion, second.SchemaVersion);
            Assert.AreEqual(1, second.NavWaypoints.Count);
            Assert.AreEqual(2, second.NavTrack.Count);
            NavWaypoint w = second.NavWaypoints[0].ToWaypoint();
            Assert.AreEqual(Region, w.RegionId);
            Assert.AreEqual("WRECK", w.Name);
            Assert.AreEqual(NavWaypointKind.Hazard, w.Kind);
            Assert.AreEqual(120f, w.Pos.x, 1e-4f);
            Assert.AreEqual(-40f, w.Pos.y, 1e-4f);
            Assert.AreEqual(JsonUtility.ToJson(second), json, "a second trip through must change nothing");
        }

        // ---- the heal --------------------------------------------------------------------------------

        [Test]
        public void GarbageRows_AreDropped_AndTheLoadDoesNotThrow()
        {
            // §3.2: negative counts, NaN positions → drop the entry, never throw.
            var d = new SaveData { SchemaVersion = 10 };
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "GOOD", X = 10f, Y = 20f, Kind = (int)NavWaypointKind.Mark });
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "NAN", X = float.NaN, Y = 3f, Kind = 0 });
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "INF", X = 1f, Y = float.PositiveInfinity, Kind = 0 });
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = "", Name = "NOWHERE", X = 1f, Y = 2f, Kind = 0 });
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "BADKIND", X = 5f, Y = 6f, Kind = 9999 });

            d.NavRoute.Add(new NavRoutePointDto { RegionId = Region, X = 1f, Y = 1f });
            d.NavRoute.Add(new NavRoutePointDto { RegionId = Region, X = float.NaN, Y = 2f });
            d.NavTrack.Add(new NavTrackPointDto { RegionId = Region, X = 3f, Y = 4f });
            d.NavTrack.Add(new NavTrackPointDto { RegionId = Region, X = 5f, Y = float.NaN });

            SaveData healed = null;
            Assert.DoesNotThrow(() => healed = SaveMigration.Migrate(d),
                "a hand-edited save must LOAD with its bad rows gone, never throw");

            Assert.AreEqual(2, healed.NavWaypoints.Count, "only the two drawable waypoints survive");
            CollectionAssert.AreEquivalent(new[] { "GOOD", "BADKIND" },
                new[] { healed.NavWaypoints[0].Name, healed.NavWaypoints[1].Name });
            Assert.AreEqual(1, healed.NavRoute.Count, "the NaN route point goes");
            Assert.AreEqual(1, healed.NavTrack.Count, "the NaN crumb goes");
        }

        [Test]
        public void AnUnrenderableKind_KeepsItsPosition_AndBecomesAPlainMark()
        {
            // A symbol we cannot draw is not a reason to forget WHERE the player marked.
            var d = new SaveData { SchemaVersion = 10 };
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "ODD", X = 7f, Y = 8f, Kind = 12345 });
            d.NavWaypoints.Add(new NavWaypointDto
            { RegionId = Region, Name = "NEG", X = 9f, Y = 10f, Kind = -3 });

            SaveData healed = SaveMigration.Migrate(d);
            Assert.AreEqual(2, healed.NavWaypoints.Count);
            foreach (NavWaypointDto row in healed.NavWaypoints)
            {
                Assert.AreEqual((int)NavWaypointKind.Mark, row.Kind, "clamped to a symbol that exists");
                Assert.IsTrue(row.ToWaypoint().IsValid, "and the position is kept");
            }
        }

        [Test]
        public void OverCapLists_AreTrimmed_OnLoad()
        {
            // A cap lowered between builds, or a hand-edited file, must not leave an unbounded save.
            ChartplotterSettings cfg = ChartplotterSettings.Default;
            var d = new SaveData { SchemaVersion = 10 };
            for (int i = 0; i < cfg.MaxWaypoints + 25; i++)
                d.NavWaypoints.Add(new NavWaypointDto
                { RegionId = Region, Name = "W" + i, X = i, Y = 0f, Kind = 0 });
            for (int i = 0; i < cfg.MaxTrackPoints + 40; i++)
                d.NavTrack.Add(new NavTrackPointDto { RegionId = Region, X = i, Y = 0f });

            SaveData healed = SaveMigration.Migrate(d);

            Assert.AreEqual(cfg.MaxWaypoints, healed.NavWaypoints.Count);
            Assert.AreEqual("W0", healed.NavWaypoints[0].Name,
                "waypoints keep the OLDEST — the marks the player has had longest");
            Assert.AreEqual(cfg.MaxTrackPoints, healed.NavTrack.Count);
            Assert.AreEqual(40f, healed.NavTrack[0].X, 1e-4f,
                "the track is a ring: the oldest crumbs are the ones that go");
        }

        [Test]
        public void TheHealIsIdempotent()
        {
            SaveData once = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V9Json));
            NavLocker.AddWaypoint(once, new NavWaypoint(Other, "A", new Vector2(1f, 2f),
                                                        NavWaypointKind.Fuel),
                                  ChartplotterSettings.Default);
            string after1 = JsonUtility.ToJson(once);
            string after2 = JsonUtility.ToJson(SaveMigration.Migrate(once));
            Assert.AreEqual(after1, after2, "migrating an already-current save must be a no-op");
        }

        // ---- NEGATIVE CONTROL ------------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheUnhealedRows_AreGenuinelyUnrenderable()
        {
            // The defect stated as arithmetic, the V9 tests' shape: prove the rows the heal drops would
            // really have produced a broken picture, so the heal is guarding something real.
            var nan = new NavWaypointDto { RegionId = Region, X = float.NaN, Y = 1f, Kind = 0 };
            Assert.IsFalse(nan.ToWaypoint().IsValid);
            Vector2 p = nan.ToWaypoint().Pos;
            Assert.IsTrue(float.IsNaN(NavMath.DistanceMetres(Vector2.zero, p)),
                "an unhealed NaN waypoint poisons every distance the chart computes from it");

            var nowhere = new NavWaypointDto { RegionId = "", X = 1f, Y = 1f, Kind = 0 };
            Assert.IsFalse(nowhere.ToWaypoint().IsValid,
                "and a region-less row can never be matched to a chart to be drawn on");

            // And the positive half of the control: a good row is NOT dropped, so the heal is not simply
            // deleting everything.
            var good = new NavWaypointDto { RegionId = Region, X = 1f, Y = 1f, Kind = 0 };
            Assert.IsTrue(good.ToWaypoint().IsValid);
        }
    }
}
