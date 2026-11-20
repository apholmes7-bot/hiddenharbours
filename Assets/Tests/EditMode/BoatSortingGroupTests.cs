using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0059 step 3: <b>each boat sorts as one object</b>. The owner saw parts of one boat interleave with
    /// another boat, with piers, with the player and with effects, because every hull sat at a fixed 1 and
    /// whatever rode on her sat at its own fixed order. These pin the boat's root sorting group: installed by
    /// the skinner on both paths, ordered by <see cref="HullSorting"/> at her waterline pivot, the one writer of
    /// that order (the St Peters pier is an input to it), everything aboard inside it at its old local order,
    /// and the ropes escaping it.
    /// </summary>
    public sealed class BoatSortingGroupTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;

        [SetUp] public void SetUp() => _previousService = HullMeshPresentation.Service;

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>The order a rock (a static <c>YSortSprite</c>) standing at this Y sorts at.</summary>
        static int RockAt(float y) => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        // ---- fixtures ---------------------------------------------------------------------------------

        private Sprite[] Facings(int n)
        {
            var facings = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                var tex = new Texture2D(4, 4); _spawned.Add(tex);
                var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); _spawned.Add(spr);
                facings[i] = spr;
            }
            return facings;
        }

        private BoatVisualDef SpriteVisual()
        {
            var v = BoatVisualDef.CreateRuntime(Facings(8));
            _spawned.Add(v);
            return v;
        }

        private GameObject Root(string name, Vector2 at)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            root.transform.position = new Vector3(at.x, at.y, 0f);
            return root;
        }

        private GameObject SkinnedBoat(string name, Vector2 at)
        {
            var root = Root(name, at);
            var rig = BoatHullSkinner.Apply(root, SpriteVisual(), boat: null,
                                            new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.IsTrue(rig.Skinned, "the sprite fixture must skin");
            return root;
        }

        /// <summary>The group a renderer under a boat finally sorts in against the world: the outermost
        /// ancestor group, stopping at one that sorts at the root (an escape).</summary>
        static SortingGroup WorldGroupOf(Component c)
        {
            SortingGroup found = null;
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                var g = t.GetComponent<SortingGroup>();
                if (g == null) continue;
                found = g;
                if (g.sortAtRoot) break;
            }
            return found;
        }

        private sealed class FakeRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public int SortingOrder = int.MinValue;
            public void SetSorting(int layerId, int order) => SortingOrder = order;
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
        }

        private sealed class FakeService : IHullMeshPresentationService
        {
            public readonly FakeRenderer Renderer = new FakeRenderer();
            public IHullMeshRenderer Install(GameObject host, HullMeshDef def, HullPaintSchemeDef scheme = null) => Renderer;
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        private BoatVisualDef MeshVisual()
        {
            var def = ScriptableObject.CreateInstance<HullMeshDef>(); _spawned.Add(def);
            var mesh = new Mesh(); _spawned.Add(mesh);
            def.Id = "hullmesh.sorting_test";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = 40f;
            var v = ScriptableObject.CreateInstance<BoatVisualDef>(); _spawned.Add(v);
            v.Id = "visual.sorting_mesh_test";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = def;
            return v;
        }

        // ---- the group ------------------------------------------------------------------------------

        [Test] public void TheSpritePath_GivesTheRootItsGroup_AtTheBandsYSortAtHerPivot_OneRungBehind()
        {
            var at = new Vector2(189.67f, -55.88f);
            var boat = SkinnedBoat("Boat", at);
            var group = boat.GetComponent<SortingGroup>();
            Assert.IsNotNull(group, "the boat root carries the sorting group");
            Assert.IsNotNull(boat.GetComponent<BoatSortingGroup>(), "and its one writer");
            Assert.AreEqual(HullSorting.OrderAt(at.y), group.sortingOrder);
            Assert.AreEqual(RockAt(at.y) - SortingBands.HullYieldRungs, group.sortingOrder);
            Assert.AreEqual(0, group.sortingLayerID, "the one Default layer");
            Assert.IsFalse(group.sortAtRoot, "a boat is one object among the scenery, not an escape");
        }

        [Test] public void TheHullPicture_KeepsItsLocalOrder_InsideTheGroup()
        {
            var boat = SkinnedBoat("Boat", new Vector2(3f, 7f));
            var hull = boat.transform.Find(BoatHullSkinner.VisualChildName).GetComponent<SpriteRenderer>();
            Assert.AreEqual(1, hull.sortingOrder, "BoatVisualDef.SortingOrder, now a local order");
            Assert.AreSame(boat.GetComponent<SortingGroup>(), WorldGroupOf(hull));
        }

        [Test] public void TheMeshPath_GivesTheRootItsGroup_AndTheOverlayKeepsTheHullsLocalOrder()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;
            var at = new Vector2(-20f, 41.3f);
            var root = Root("MeshBoat", at);
            var visual = MeshVisual();
            var rig = BoatHullSkinner.Apply(root, visual, boat: null, new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.IsNotNull(rig.Presenter, "the mesh path ran");
            Assert.AreEqual(HullSorting.OrderAt(at.y), root.GetComponent<SortingGroup>().sortingOrder);
            Assert.AreEqual(visual.SortingOrder, service.Renderer.SortingOrder,
                            "the overlay's own group takes the hull's local order, inside the boat's");
        }

        [Test] public void AnUnskinnedHull_IsStillOneObject()
        {
            var root = Root("Plain", new Vector2(0f, -3f));
            var sr = root.AddComponent<SpriteRenderer>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>(); _spawned.Add(hull);
            BoatHullSkinner.ApplyHull(root, sr, hull, boat: null);
            Assert.AreEqual(HullSorting.OrderAt(-3f), root.GetComponent<SortingGroup>().sortingOrder);
        }

        [Test] public void ReSkinning_KeepsOneGroupAndOneWriter()
        {
            var boat = SkinnedBoat("Boat", new Vector2(1f, 2f));
            BoatHullSkinner.Apply(boat, SpriteVisual(), boat: null, new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.AreEqual(1, boat.GetComponents<SortingGroup>().Length);
            Assert.AreEqual(1, boat.GetComponents<BoatSortingGroup>().Length);
        }

        [Test] public void TwoBoats_SortByWhereTheyLie_AndSwapWhenTheyPass()
        {
            var a = SkinnedBoat("A", new Vector2(0f, 10f));
            var b = SkinnedBoat("B", new Vector2(2f, 6f));
            int Order(GameObject g) => g.GetComponent<SortingGroup>().sortingOrder;
            Assert.Less(Order(a), Order(b), "A north of B draws first, under her: the whole of A, not parts of it");

            a.transform.position = new Vector3(0f, 2f, 0f);
            a.GetComponent<BoatSortingGroup>().Sort();
            Assert.Greater(Order(a), Order(b), "A passes south of B and now draws over the whole of B");
        }

        [Test] public void EverythingAboard_SortsInsideHerGroup_AndLeavesItWhenHeStepsOff()
        {
            var boat = SkinnedBoat("Boat", new Vector2(5f, 5f));
            var group = boat.GetComponent<SortingGroup>();

            // The player parented aboard, with his Y-sorted order, and a deck container at its parked 50.
            var player = new GameObject("Player"); _spawned.Add(player);
            var body = player.AddComponent<SpriteRenderer>();
            body.sortingOrder = RockAt(4.6f);
            player.transform.SetParent(boat.transform, worldPositionStays: true);
            var crate = new GameObject("Crate"); _spawned.Add(crate);
            var crateSr = crate.AddComponent<SpriteRenderer>();
            crateSr.sortingOrder = 50;
            crate.transform.SetParent(boat.transform, worldPositionStays: false);

            Assert.AreSame(group, WorldGroupOf(body), "the player aboard sorts with the boat he stands on");
            Assert.AreSame(group, WorldGroupOf(crateSr), "deck gear sorts with the boat");
            var hull = boat.transform.Find(BoatHullSkinner.VisualChildName).GetComponent<SpriteRenderer>();
            Assert.Greater(body.sortingOrder, hull.sortingOrder, "inside: the player draws over the deck");
            Assert.Greater(crateSr.sortingOrder, hull.sortingOrder, "inside: the crate draws over the deck");

            player.transform.SetParent(null, worldPositionStays: true);
            Assert.IsNull(WorldGroupOf(body), "stepped ashore, he sorts in the band again by his own Y");
        }

        // ---- the pier is an input --------------------------------------------------------------------

        [Test] public void ThePier_DecidesHerOrderNorthOfIt_AndStandsAsideSouthOfIt()
        {
            var pier = new Rect(180f, -60f, 12f, 4f);   // yMax = -56
            var root = Root("Dory", new Vector2(186f, -50f));
            var authored = root.AddComponent<SortingGroup>();   // the scene's own group, as St Peters authors it
            authored.sortingOrder = -1;
            var rule = root.AddComponent<StPetersPierBoatSorting>();
            rule.Configure(pier, behindOrder: -1);
            BoatHullSkinner.Apply(root, SpriteVisual(), boat: null, new BoatHullSkinner.Options { SkipWaveMotion = true });

            Assert.AreEqual(1, root.GetComponents<SortingGroup>().Length, "the authored group is adopted, not doubled");
            Assert.AreEqual(-1, authored.sortingOrder, "north of the pier she drops behind its deck");

            root.transform.position = new Vector3(186f, -62f, 0f);
            root.GetComponent<BoatSortingGroup>().Sort();
            Assert.AreEqual(HullSorting.OrderAt(-62f), authored.sortingOrder,
                            "south of the pier she sorts by where she lies, among its pilings");

            root.transform.position = new Vector3(170f, -50f, 0f);
            root.GetComponent<BoatSortingGroup>().Sort();
            Assert.AreEqual(HullSorting.OrderAt(-50f), authored.sortingOrder, "off the pier's end, by where she lies");
        }

        [Test] public void ThePier_NoLongerWritesTheGroup()
        {
            string code = Squash(File.ReadAllText("Assets/_Project/Code/App/StPetersPierBoatSorting.cs"));
            StringAssert.DoesNotContain(".sortingOrder=", code, "one writer per boat order (ADR 0059 decision 4)");
            StringAssert.DoesNotContain("voidLateUpdate", code);
        }

        // ---- what escapes ----------------------------------------------------------------------------

        [Test] public void AnEscape_SortsAtTheRoot_AboveAllDecor()
        {
            var boat = SkinnedBoat("Boat", new Vector2(0f, 0f));
            var rope = new GameObject("Rope"); _spawned.Add(rope);
            rope.transform.SetParent(boat.transform, false);
            var line = rope.AddComponent<LineRenderer>();
            line.sortingOrder = SortingBands.AboveDecor;
            var g = HullSorting.EscapeTheBoat(rope, SortingBands.AboveDecor);
            Assert.IsTrue(g.sortAtRoot);
            Assert.AreEqual(SortingBands.AboveDecor, g.sortingOrder);
            Assert.AreSame(g, WorldGroupOf(line), "the rope sorts at the root, not inside the boat");
            Assert.AreSame(g, HullSorting.EscapeTheBoat(rope, SortingBands.AboveDecor), "idempotent");
            Assert.AreEqual(1, rope.GetComponents<SortingGroup>().Length);
        }

        [Test] public void TheRopesTheBoatCarries_AllEscape()
        {
            foreach (string path in new[]
                     {
                         "Assets/_Project/Code/Boats/BoatMooring.cs", "Assets/_Project/Code/Boats/BoatAnchor.cs",
                         "Assets/_Project/Code/Fishing/TrapHaulController.cs",
                     })
                StringAssert.Contains("HullSorting.EscapeTheBoat(go,SortingBands.AboveDecor);",
                                      Squash(File.ReadAllText(path)), path);
        }

        // ---- one rule for every boat ----------------------------------------------------------------

        [Test] public void TheBeachedWreck_LostItsPrivateCopyOfTheRule()
        {
            string code = Squash(File.ReadAllText("Assets/_Project/Code/Boats/BeachedWreck.cs"));
            StringAssert.DoesNotContain("SortingBands.DecorBase-", code, "the wreck sorts by the boat's group now");
            StringAssert.DoesNotContain("SetSorting(", code);
        }

        [Test] public void TheAmbientFleetsPlainHulls_TakeTheGroupToo()
        {
            StringAssert.Contains("BoatSortingGroup.Install(fisher.Root);",
                                  Squash(File.ReadAllText("Assets/_Project/Code/Boats/AmbientFleetPresenter.cs")));
        }

        // ---- cost -----------------------------------------------------------------------------------

        [Test] public void Sorting_AllocatesNothing()
        {
            var boat = SkinnedBoat("Boat", new Vector2(0f, 0f));
            var writer = boat.GetComponent<BoatSortingGroup>();
            writer.Sort();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                boat.transform.position = new Vector3(0f, i * 0.01f, 0f);
                writer.Sort();
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before, "one rounding and a compare per boat per frame, no garbage");
        }

        static string Squash(string s) => Regex.Replace(s, @"\s+", "");
    }
}
