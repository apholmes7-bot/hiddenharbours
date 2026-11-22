using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode.Washrooms
{
    /// <summary>
    /// The outhouse's art: the door's frames as he goes in and comes out, the booth drawn over him while he is
    /// inside, the baked sheet, and the prefab that wires it (the seat kept, the walls fitted to
    /// the footprint, the shadow every standing outdoor piece casts).
    /// </summary>
    public sealed class OuthouseArtTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Washrooms/Outhouse.prefab";
        private const string DefPath = "Assets/_Project/Data/Resources/Washrooms/Outhouse.asset";
        private const float Per = 0.1f, Hold = 0.3f, Tick = 0.01f;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<FixturePoseRequested> _poses = new List<FixturePoseRequested>();

        [SetUp]
        public void SetUp() => EventBus.Subscribe<FixturePoseRequested>(OnPose);

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FixturePoseRequested>(OnPose);
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            _poses.Clear();
        }

        private void OnPose(FixturePoseRequested e) => _poses.Add(e);

        private static void Run(OuthouseDoorTimeline t, bool seated, float seconds)
        {
            for (float s = 0f; s < seconds - 1e-5f; s += Tick) t.Tick(seated, Tick, Per, Hold);
        }

        // ================================================================ the door

        [Test]
        public void HeSits_TheDoorSwingsOpen_HoldsABeat_HeIsIn_ThenItShutsOverHim()
        {
            var t = new OuthouseDoorTimeline();
            Assert.AreEqual(OuthouseSheet.Closed, t.Row, "it stands shut");
            Run(t, true, Per * 1.05f);
            Assert.AreEqual(OuthouseSheet.Ajar, t.Row, "one frame in, ajar");
            Assert.IsFalse(t.Inside);
            Run(t, true, Per);
            Assert.AreEqual(OuthouseSheet.Open, t.Row, "then wide open");
            Assert.IsFalse(t.Inside, "he is not in before the door has stood open its beat");
            Run(t, true, Hold + Tick * 2f);
            Assert.IsTrue(t.Inside, "after the beat he is in");
            Run(t, true, Per * 3f);
            Assert.AreEqual(OuthouseSheet.Closed, t.Row, "the door shut behind him");
            Assert.IsTrue(t.Inside, "shut in");
        }

        [Test]
        public void HeGetsUp_TheDoorOpensWithHimInside_HeStepsOut_ThenItShuts()
        {
            var t = new OuthouseDoorTimeline();
            Run(t, true, Per * 3f + Hold + Per * 3f);
            Assert.IsTrue(t.Inside);
            Run(t, false, Per * 2.05f);
            Assert.AreEqual(OuthouseSheet.Open, t.Row);
            Assert.IsTrue(t.Inside, "still inside while the door finishes opening");
            Run(t, false, Hold + Tick * 2f);
            Assert.IsFalse(t.Inside, "out after the beat");
            Assert.AreEqual(OuthouseSheet.Open, t.Row, "the door stays open while he steps clear");
            Run(t, false, Hold + Per * 3f);
            Assert.AreEqual(OuthouseSheet.Closed, t.Row, "then it falls shut");
        }

        [Test]
        public void GettingUpBeforeHeIsIn_LetsTheDoorFallShut_AndNeverPutsHimIn()
        {
            var t = new OuthouseDoorTimeline();
            Run(t, true, Per * 1.05f);
            Assert.AreEqual(OuthouseSheet.Ajar, t.Row);
            Run(t, false, Per * 3f);
            Assert.AreEqual(OuthouseSheet.Closed, t.Row);
            Assert.IsFalse(t.Inside);
        }

        // ================================================================ the sheet and the Def

        [Test]
        public void EveryRowIsDrawnWithItsOwnOptions_AndEveryCellIsBakedAndNamed()
        {
            var seen = new HashSet<string>();
            for (int r = 0; r < OuthouseSheet.Rows; r++)
                Assert.IsTrue(seen.Add(OuthouseSheet.OptsFor(r)), $"row {r} draws the same as another row");
            for (int r = 0; r < OuthouseSheet.Rows; r++)
            for (int c = 0; c < OuthouseSheet.Facings; c++)
            {
                Sprite s = OuthouseSheet.SpriteAt(r, c);
                Assert.IsNotNull(s, $"row {r} cell {c} is missing");
                Assert.IsTrue(ApplianceArt.TryParse(s.name, OuthouseSheet.Stem, out int pr, out int pc) && pr == r && pc == c, s.name);
                Assert.AreEqual(32f, s.pixelsPerUnit, 1e-3f, "the locked scale");
            }
        }

        [Test]
        public void TheDoorTimingIsTheDefs()
        {
            var def = AssetDatabase.LoadAssetAtPath<WashroomDef>(DefPath);
            Assert.IsNotNull(def, DefPath);
            Assert.AreEqual("washroom.outhouse", def.Id);
            Assert.Greater(def.DoorFrameSeconds, 0f);
            Assert.GreaterOrEqual(def.DoorHoldOpenSeconds, 0f);
        }

        // ================================================================ the prefab

        [Test]
        public void ThePrefab_KeepsItsSeat_WiresTheArt_FitsTheWalls_AndCasts()
        {
            var def = AssetDatabase.LoadAssetAtPath<WashroomDef>(DefPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            Assert.AreSame(prefab, def.Prefab, "the Def still stands as this prefab");

            var seat = prefab.GetComponent<SitPoint>();
            Assert.IsNotNull(seat, "the seat is kept");
            Assert.AreEqual("washstation.toilet", seat.StationId);

            var art = prefab.GetComponent<OuthouseArt>();
            Assert.IsNotNull(art);
            Assert.AreSame(def, art.Def);
            Assert.AreEqual(OuthouseSheet.FrontCell, art.Cell);
            Assert.IsNull(prefab.GetComponent<SpriteRenderer>(), "no placeholder on the root");
            Assert.AreEqual(1, prefab.transform.childCount, "the placeholder squares are gone: the Art child only");

            Transform booth = prefab.transform.Find("Art");
            Assert.IsNotNull(booth);
            Assert.AreSame(booth.GetComponent<SpriteRenderer>(), art.Art);
            Assert.AreSame(OuthouseSheet.SpriteAt(OuthouseSheet.Closed, OuthouseSheet.FrontCell), art.Art.sprite, "it stands shut");
            Assert.IsNotNull(booth.GetComponent<SpriteShadow>(), "it casts like every standing outdoor piece");

            Assert.AreEqual(1, prefab.GetComponents<Collider2D>().Length, "one collider: the walls");
            var walls = prefab.GetComponent<PolygonCollider2D>();
            Assert.IsNotNull(walls);
            Assert.AreSame(walls, art.Walls);
            Assert.IsFalse(walls.isTrigger, "the walls are solid");
            Vector2[] want = OuthouseSheet.FootprintOutline(OuthouseSheet.FrontCell), got = walls.GetPath(0);
            Assert.AreEqual(want.Length, got.Length);
            for (int i = 0; i < want.Length; i++) Assert.Less((want[i] - got[i]).magnitude, 1e-4f, $"corner {i}");
            float front = want.Min(p => p.y);
            Assert.Less(Mathf.Abs(front), seat.ReachMeters, "he can reach the seat from in front of the door");
        }

        [Test]
        public void InUse_HeIsSatOnTheBench_HiddenByTheBooth_ThenPutBackOutside()
        {
            OuthouseArt art = Booth(out SitPoint seat);
            Collider2D walls = art.Walls;
            float per = art.Def.DoorFrameSeconds, hold = art.Def.DoorHoldOpenSeconds;

            Drive(art, true, per * 2.5f);
            Assert.AreEqual(0, _poses.Count, "no pose before the door has stood open");
            Drive(art, true, hold + Tick * 3f);   // short of a door frame past the beat: the door still stands open
            Assert.AreEqual(1, _poses.Count, "he is asked onto the bench");
            FixturePoseRequested on = _poses[0];
            Assert.IsTrue(on.On);
            Assert.AreEqual(FixturePose.Sit, on.Pose);
            Assert.AreEqual(seat.Id, on.FixtureId);
            Assert.Less((on.SeatWorld - art.SeatWorld()).magnitude, 1e-4f);
            Assert.Less(on.EntryWorld.y, art.transform.position.y - OuthouseSheet.Foot.y * 0.5f * IsoGround.GroundDepthScale,
                        "he steps out in front of the door");
            Assert.IsFalse(walls.enabled, "the walls never shove him while he sits");
            int figure = ApplianceArt.OrderFor(on.SeatWorld.y);
            Assert.AreSame(OuthouseSheet.SpriteAt(OuthouseSheet.Open, art.Cell), art.Art.sprite, "the door still open as he goes in");
            Assert.Greater(art.Art.sortingOrder, figure, "the booth covers him even through the open doorway");

            Drive(art, true, per * 3f);
            Assert.AreSame(OuthouseSheet.SpriteAt(OuthouseSheet.Closed, art.Cell), art.Art.sprite, "the door shut");
            Assert.Greater(art.Art.sortingOrder, figure, "the shut booth covers him");

            Drive(art, false, per * 2.5f + hold + 0.05f);
            Assert.AreEqual(2, _poses.Count, "he is put back outside");
            Assert.IsFalse(_poses[1].On);
            Assert.IsTrue(walls.enabled, "the walls are back");
            Assert.AreEqual(ApplianceArt.OrderFor(art.transform.position.y), art.Art.sortingOrder, "sorted by its own foot once he is out");
            Drive(art, false, hold + per * 3f);
            Assert.AreSame(OuthouseSheet.SpriteAt(OuthouseSheet.Closed, art.Cell), art.Art.sprite, "then it falls shut");
        }

        [Test]
        public void TurningTheBooth_TurnsItsArtAndItsWalls()
        {
            OuthouseArt art = Booth(out _);
            art.SetCell(2);
            Assert.AreEqual(2, art.Cell);
            Assert.AreSame(OuthouseSheet.SpriteAt(OuthouseSheet.Closed, 2), art.Art.sprite);
            Vector2[] want = OuthouseSheet.FootprintOutline(2), got = art.Walls.GetPath(0);
            for (int i = 0; i < want.Length; i++) Assert.Less((want[i] - got[i]).magnitude, 1e-4f, $"corner {i}");
        }

        // ================================================================ harness

        private static void Drive(OuthouseArt art, bool seated, float seconds)
        {
            for (float s = 0f; s < seconds - 1e-5f; s += Tick) art.Step(seated, Tick);
        }

        private OuthouseArt Booth(out SitPoint seat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            go.transform.position = new Vector3(4f, -3f, 0f);
            seat = go.GetComponent<SitPoint>();
            var art = go.GetComponent<OuthouseArt>();
            art.Configure(art.Def, art.Art, art.Walls, art.Cell);   // EditMode: Awake does not run
            return art;
        }
    }
}
