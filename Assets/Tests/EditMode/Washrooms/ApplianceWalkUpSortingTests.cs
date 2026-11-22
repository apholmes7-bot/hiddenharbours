using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode.Washrooms
{
    /// <summary>
    /// <b>He walks up to a fixture and is drawn in front of it; he stands north of it and is drawn behind; and the
    /// fixture still draws in front of the wall it backs onto.</b>
    ///
    /// <para>The rule: a piece sorts by its front floor line (the south edge of its footprint), he sorts by his
    /// drawn feet (his transform). His solid foot circle sits below his drawn feet (<see cref="FootBelowMetres"/>,
    /// radius <see cref="FootRadiusMetres"/>, as the game builds him), so a stop drawn on the art let his feet walk
    /// into it and a piece sorted at its pivot hid him. The piece's stop is now its footprint dropped into his foot
    /// frame, so his feet meet its edge.</para>
    ///
    /// <para>The walker's numbers here are the TEST's own reading of the game's player, independent of the config,
    /// so the config cannot drift under the rule unseen.</para>
    /// </summary>
    public class ApplianceWalkUpSortingTests
    {
        /// <summary>The player as the game builds him (PersistentCoreBuilder): a foot circle of radius 0.35 m,
        /// 0.7 m below the transform he is sorted by.</summary>
        const float FootRadiusMetres = 0.35f;
        const float FootBelowMetres = 0.7f;

        /// <summary>One sorting rung: a walker with nothing to stop him is checked from a rung either side.</summary>
        static float Rung => 1f / SortingBands.OrdersPerMetre;

        /// <summary>Somewhere away from the origin, so nothing passes by being at zero.</summary>
        static readonly Vector2 Pivot = new Vector2(-37.3f, 61.85f);

        static readonly List<GameObject> Made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in Made) if (go != null) Object.DestroyImmediate(go);
            Made.Clear();
        }

        static IEnumerable<TestCaseData> EverySheetAtEveryCell()
        {
            foreach (ApplianceLayout l in ApplianceSheets.All)
                for (int cell = 0; cell < ApplianceSheets.Facings; cell++)
                    yield return new TestCaseData(l.Kind, cell).SetName($"{l.Kind}_cell{cell}");
        }

        static int OrderAt(float y)
            => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                    SortingBands.DecorFloor, SortingBands.DecorCeiling);

        static ApplianceArt Piece(ApplianceKind kind, int cell, bool solid)
        {
            var root = new GameObject("piece " + kind);
            Made.Add(root);
            root.transform.position = Pivot;
            var far = new GameObject("Art").AddComponent<SpriteRenderer>();
            far.transform.SetParent(root.transform, false);
            var near = new GameObject("ArtNear").AddComponent<SpriteRenderer>();
            near.transform.SetParent(root.transform, false);
            // Built the way the washroom prefabs were: a small circle on the art, which the piece replaces.
            if (solid) root.AddComponent<CircleCollider2D>().radius = 0.2f;
            var art = root.AddComponent<ApplianceArt>();
            var so = new SerializedObject(art);
            so.FindProperty("_cellOverride").intValue = cell;
            so.ApplyModifiedPropertiesWithoutUndo();
            art.Configure(kind, far, near);
            art.Settle();
            return art;
        }

        static int FarOrder(ApplianceArt art)
        {
            foreach (SpriteRenderer r in art.GetComponentsInChildren<SpriteRenderer>(true))
                if (r.name == "Art") return r.sortingOrder;
            Assert.Fail($"{art.name}: no Art renderer");
            return 0;
        }

        static List<Collider2D> Solids(ApplianceArt art)
            => art.GetComponentsInChildren<Collider2D>().Where(c => c.enabled && !c.isTrigger).ToList();

        static void BoxEdges(BoxCollider2D b, out float south, out float north)
        {
            Transform t = b.transform; Vector3 s = t.lossyScale;
            float cy = t.position.y + b.offset.y * s.y, half = Mathf.Abs(b.size.y * s.y) * 0.5f;
            south = cy - half; north = cy + half;
        }

        static void AssertTheRule(ApplianceArt art, string what)
        {
            ApplianceLayout l = ApplianceSheets.For(art.Kind);
            Rect fp = ApplianceFacing.FootprintBox(l, art.Cell);
            float front = Pivot.y + fp.yMin, back = Pivot.y + fp.yMax;
            int piece = FarOrder(art);

            Assert.AreEqual(OrderAt(front), piece, $"{what}: the idle piece sorts at its front floor line");
            Assert.Greater(piece, OrderAt(back), $"{what}: a wall sorted at the piece's back edge draws behind it");

            float walkedUp, behind;
            bool stops = art.Footprint != null && art.Footprint.enabled;
            if (stops)
            {
                if (l.Pose != FixturePose.Stand)
                    Assert.AreEqual(1, Solids(art).Count, $"{what}: the footprint box is its one solid stop");
                BoxEdges(art.Footprint, out float south, out float north);
                walkedUp = south - FootRadiusMetres + FootBelowMetres;   // his foot circle meets its south side
                behind = north + FootRadiusMetres + FootBelowMetres;     // ... or its north side
                Assert.LessOrEqual(walkedUp, front + 1e-4f, $"{what}: walked up, his feet stop at or before the art's front");
                Assert.GreaterOrEqual(behind, back - 1e-4f, $"{what}: stood north, his feet stop at or past the art's back");
            }
            else
            {
                Assert.IsTrue(l.Pose == FixturePose.Stand || Solids(art).Count == 0,
                              $"{what}: a solid piece he does not step into must stop him with its footprint box");
                walkedUp = front - Rung;   // nothing stops him: from one rung short of its front ...
                behind = back + Rung;      // ... and one rung past its back
            }

            Assert.Greater(OrderAt(walkedUp), piece, $"{what}: walked up to its front (feet at {walkedUp:0.000}) he must draw in front");
            for (float off = 0.05f; off <= 2f; off += 0.05f)
                Assert.Greater(OrderAt(walkedUp - off), piece, $"{what}: {off:0.00} m back from the walk-up spot he must draw in front");

            Assert.Less(OrderAt(behind), piece, $"{what}: stood north of it (feet at {behind:0.000}) he must draw behind");
            for (float off = 0.05f; off <= 2f; off += 0.05f)
                Assert.Less(OrderAt(behind + off), piece, $"{what}: {off:0.00} m further north he must draw behind");
        }

        [TestCaseSource(nameof(EverySheetAtEveryCell))]
        public void ASolidPiece_DrawsHimInFrontWhenWalkedUp_AndBehindWhenNorth(ApplianceKind kind, int cell)
            => AssertTheRule(Piece(kind, cell, solid: true), $"{kind} cell {cell} (built solid)");

        [TestCaseSource(nameof(EverySheetAtEveryCell))]
        public void AWalkThroughPiece_DrawsHimInFrontSouthOfItsFront_AndBehindNorthOfItsBack(ApplianceKind kind, int cell)
            => AssertTheRule(Piece(kind, cell, solid: false), $"{kind} cell {cell} (nothing solid)");

        static IEnumerable<string> ShippedPrefabs()
            => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<ApplianceArt>() != null)
                .OrderBy(p => p);

        [Test]
        public void EverySheetHasAShippedPrefab()
        {
            var kinds = ShippedPrefabs().Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<ApplianceArt>().Kind).ToList();
            foreach (ApplianceLayout l in ApplianceSheets.All)
                Assert.That(kinds, Does.Contain(l.Kind), $"no shipped prefab carries the {l.Kind} art, so the prefab case below misses it");
        }

        [TestCaseSource(nameof(ShippedPrefabs))]
        public void EachShippedPrefab_DrawsHimInFrontWhenWalkedUp_AndBehindWhenNorth(string path)
        {
            foreach (int cell in ApplianceFacing.WallOffsets)
            {
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                Made.Add(go);
                go.transform.position = Pivot;
                var art = go.GetComponent<ApplianceArt>();
                var so = new SerializedObject(art);
                so.FindProperty("_cellOverride").intValue = cell;
                so.ApplyModifiedPropertiesWithoutUndo();
                art.Settle();
                AssertTheRule(art, $"{path} cell {cell}");
            }
        }

        [Test]
        public void TheShippedDrop_IsHowFarHisFootCircleSitsBelowHisFeet()
        {
            Assert.AreEqual(FootBelowMetres, WashVisualsSettings.Default.FixtureStopDropMetres, 1e-4f,
                "the stop is not dropped by as much as his foot circle sits below his drawn feet, so his feet can " +
                "walk into a piece (or stop short of it)");
        }

        [Test]
        public void TheStop_IsTheFootprintDroppedIntoHisFootFrame()
        {
            var fp = new Rect(-0.35f, -0.2f, 0.7f, 0.4f);
            Assert.AreEqual(0f, ApplianceArt.StopCentre(fp, 0.7f).x, 1e-5f);
            Assert.AreEqual(-0.7f, ApplianceArt.StopCentre(fp, 0.7f).y, 1e-5f);
            Assert.AreEqual(0f, ApplianceArt.StopCentre(fp, -1f).y, 1e-5f, "a negative drop is no drop");
        }

        [Test]
        public void SettlingAgain_LeavesOneStopInTheSamePlace()
        {
            ApplianceArt art = Piece(ApplianceKind.Toilet, 0, solid: true);
            BoxEdges(art.Footprint, out float s1, out float n1);
            art.Settle();
            art.SetWall(-1);
            BoxEdges(art.Footprint, out float s2, out float n2);
            Assert.AreEqual(s1, s2, 1e-5f); Assert.AreEqual(n1, n2, 1e-5f);
            Assert.AreEqual(1, art.GetComponents<BoxCollider2D>().Length, "no second box");
        }
    }
}
