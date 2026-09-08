using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Boarding lands you amidships — on every heading she can lie on.</b>
    ///
    /// <para><b>The defect, found while measuring the helm's (2026-09-07, #789).</b>
    /// <c>ControlSwitcher._boardLocalOffset</c> (0, 0.4) was read as a WORLD-axis offset, so "a step
    /// forward" was only forward on a hull pointing north. The starter dory lies bow-WEST at her St
    /// Peters berth, which turned that step into <b>0.4 m to STARBOARD</b> — against a walkable
    /// half-beam of 0.225 m. Every boarding clamped hard onto whichever rail happened to be up-screen,
    /// on the SEAWARD side, with the wharf behind her; and it was that pinning, not the rule, that made
    /// two PlayMode fixtures' facing geometry read backwards for two CI runs.</para>
    ///
    /// <para><b>The bar is her AUTHORED deck</b> — <c>DoryIso.asset</c>'s own floor polygon, loaded from
    /// the shipped asset — and the question asked of it is the one that matters: <b>does the seat need
    /// clamping?</b> A seat that has to be dragged back onto the deck is a seat that was not on the
    /// boat. Every case carries its negative control: the same question of the world-axis offset, which
    /// fails.</para>
    ///
    /// <para>Headless by construction: the hull wears a MESH visual behind the Core presentation seam (a
    /// test double), because a mesh hull is drawn exactly where her bow points — a sprite compass would
    /// snap 45° back to 0 and quietly turn six headings into two.</para>
    /// </summary>
    public class BoardingSeatHeadingTests
    {
        private const string DeckAssetPath = "Assets/_Project/Data/Boats/Decks/DoryIso.asset";
        private const float LoaMeters = 4.5f;
        private const float BakeElevationDeg = 40f;
        private const float WalkHalfBeam = 0.225f;          // her floor is 0.45 m wide

        /// <summary>The seat as it shipped, and as both scenes still carry it.</summary>
        private static readonly Vector2 ShippedBoardOffset = new Vector2(0f, 0.4f);

        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 270f };
        private const float Tol = 1e-4f;

        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;
        private BoatDeckDef _deck;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            HullMeshPresentation.Service = new FakeService();
            GameServices.Reset();
            _deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(DeckAssetPath);
            Assert.IsNotNull(_deck, $"the starter dory's authored deck {DeckAssetPath} must exist — it " +
                                    "is the bar this whole fixture measures against");
        }

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the cases ------------------------------------------------------------------------------

        /// <summary>
        /// The migration bar: with her bow north the deck walk is handed the very offset that shipped.
        ///
        /// <para>⚠ Asserted on the OFFSET, not on where the fisher's transform ends up. Her floor is a
        /// tilted plane (<c>HeightPlane</c> 0.104 m at the keel, rising 0.020 m/m forward), and
        /// <see cref="DeckAreaMath.DeckToWorld"/> lifts a standing point up-screen by
        /// <c>height·cos(elev)</c> — 0.089 m here. A world-space read that un-projects at height 0 puts
        /// that lift back into the deck frame as 0.138 m of phantom offset, which at any heading but
        /// north rotates into a phantom 0.138 m ABEAM. That is a fixture measuring its own projection
        /// error, not the seat.</para>
        /// </summary>
        [Test]
        public void HeadingNorth_HandsTheDeckWalkTheOffsetThatShipped()
        {
            Vector2 seat = Build(0f).Switcher.BoardDeckOffset();
            Vector2 asShipped = DeckAreaMath.DeckToWorld(seat, 0f, 0f, BakeElevationDeg);

            Assert.AreEqual(ShippedBoardOffset.x, asShipped.x, Tol, "x is untouched by the fix");
            Assert.AreEqual(ShippedBoardOffset.y, asShipped.y, Tol,
                "with her bow north the un-projection and the projection are exact inverses, so the " +
                "tuned number reaches the deck walk unchanged");
        }

        /// <summary>
        /// ⭐ The fix: <b>the seat is a place on her floor, not a spot on the screen</b>. Asserted where
        /// it is meaningful — in her own frame — and paired with the negative control, so it cannot pass
        /// on the code it was written to condemn.
        /// </summary>
        [Test]
        public void AtEveryHeading_SheLandsOnHerFloor_AndIsNeverDraggedThere()
        {
            int oldOnesDragged = 0;
            float worstAbeam = 0f, leastAlong = float.MaxValue;

            foreach (float heading in Headings)
            {
                Rig rig = Build(heading);
                Assert.IsTrue(rig.Switcher.TryInteract(), $"at {heading}° she boards");

                // ⭐ Where the WALK says she is standing, in her own frame — the deck-local the snap
                // settled on, clamp and all. Read, not derived: no un-projection here to get wrong.
                Vector2 seat = rig.Walk.DeckLocalPosition;
                worstAbeam = Mathf.Max(worstAbeam, Mathf.Abs(seat.x));
                leastAlong = Mathf.Min(leastAlong, seat.y);

                Assert.Greater(seat.y, 0f,
                    $"at {heading}° she landed {seat.y:0.000} m along the keel — the seat is a step " +
                    "FORWARD of amidships, and a negative reading is her tiller end");
                Assert.Less(Mathf.Abs(seat.x), WalkHalfBeam,
                    $"at {heading}° she landed {Mathf.Abs(seat.x):0.000} m abeam against a " +
                    $"{WalkHalfBeam:0.000} m walkable half-beam — that is against the rail");
                Assert.IsFalse(WasDraggedOntoTheFloor(rig, ProjectedSeat(rig), heading),
                    $"at {heading}° the seat had to be CLAMPED onto her floor. A seat the deck has to " +
                    "drag inboard was not a place on the boat.");

                if (WasDraggedOntoTheFloor(rig, ShippedBoardOffset, heading)) oldOnesDragged++;
                Destroy(rig);
            }

            Assert.Greater(oldOnesDragged, 0,
                $"the world-axis seat was dragged onto her floor at {oldOnesDragged} of the six " +
                "headings — without this the assertions above would pass on the very code they condemn");

            Debug.Log($"[board-seat] worst abeam {worstAbeam:F4} m (half-beam {WalkHalfBeam:F3}), least " +
                      $"along {leastAlong:F4} m; the world-axis seat was dragged at {oldOnesDragged}/6.");
        }

        /// <summary>
        /// The one heading where the old read did NOT pin her against a rail is the one where it put her
        /// on the wrong END of the boat. "It was inside the deck" is not the same as "it was the seat".
        /// </summary>
        [Test]
        public void HeadingSouth_TheOldSeatWasOnHerTillerEnd()
        {
            Rig rig = Build(180f);

            Vector2 oldSeat = Settle(rig, ShippedBoardOffset, 180f);
            Assert.IsFalse(WasDraggedOntoTheFloor(rig, ShippedBoardOffset, 180f),
                "harness: at 180° the old seat was on her floor, so nothing clamped it…");
            Assert.Less(oldSeat.y, 0f, "…but ABAFT amidships, which on this hull is her tiller");

            Assert.IsTrue(rig.Switcher.TryInteract(), "she boards");
            Vector2 seat = rig.Walk.DeckLocalPosition;
            Assert.Greater(seat.y, 0f, "the fix puts her forward, where the seat is authored");
            Assert.Greater(seat.y - oldSeat.y, 1f,
                $"the two answers are {seat.y - oldSeat.y:0.000} m of keel apart — the whole length of " +
                "her walking floor and then some");
        }

        /// <summary>The seat names a place on the boat you can point at: 0.4 drawn metres with her bow
        /// north is 0.62 m of real hull, because the artwork is baked at 40°.</summary>
        [Test]
        public void TheTunedOffsetNamesTwoThirdsOfAMetreOfRealHull()
        {
            Vector2 deck = Build(0f).Switcher.BoardDeckOffset();

            Assert.AreEqual(0f, deck.x, Tol, "she boards onto the centreline");
            Assert.AreEqual(ShippedBoardOffset.y / Mathf.Sin(BakeElevationDeg * Mathf.Deg2Rad), deck.y, 1e-3f,
                "the drawn offset un-projects through the artwork's own bake elevation");
            Assert.Less(deck.y, LoaMeters * 0.5f, "…and it is aboard her");
        }

        /// <summary>The boat-relative WORLD offset production hands the deck walk after the fix — the
        /// authored seat in her own frame, projected through the heading she is drawn on.</summary>
        private static Vector2 ProjectedSeat(Rig rig)
            => DeckAreaMath.DeckToWorld(rig.Switcher.BoardDeckOffset(), 0f, rig.Heading, BakeElevationDeg);

        /// <summary>Where a boat-relative WORLD offset settles on her floor — <b>the shipped iteration</b>
        /// (<see cref="DeckWalkController.SeedDeckLocalPure"/>), which inverts the projection against the
        /// deck's own height plane rather than assuming a flat sole.</summary>
        private Vector2 Settle(Rig rig, Vector2 worldRelative, float heading)
        {
            int hint = -1;
            return DeckWalkController.SeedDeckLocalPure(worldRelative, heading, BakeElevationDeg,
                                                        _deck, false, ref hint, out float _);
        }

        /// <summary>Did the deck have to DRAG this offset inboard? Settle it, take the height that solve
        /// converged on, un-project at that height, and ask the clamp whether it had to move the answer.
        /// This is the question the whole fixture turns on: a seat that needs clamping was never on the
        /// boat, and on a hull lying athwart the screen the world-axis read needed it every time.</summary>
        private bool WasDraggedOntoTheFloor(Rig rig, Vector2 worldRelative, float heading)
        {
            int hint = -1;
            DeckWalkController.SeedDeckLocalPure(worldRelative, heading, BakeElevationDeg,
                                                 _deck, false, ref hint, out float height);
            Vector2 wanted = DeckAreaMath.WorldToDeck(worldRelative, height, heading, BakeElevationDeg);
            int clampHint = -1;
            Vector2 clamped = _deck.ClampToWalkable(wanted, ref clampHint, out float _);
            return (clamped - wanted).sqrMagnitude > 1e-8f;
        }

        // ---- the rig --------------------------------------------------------------------------------

        private struct Rig
        {
            public ControlSwitcher Switcher;
            public DeckWalkController Walk;
            public Transform Boat;
            public GameObject PlayerGo, BoatGo, SwitcherGo;
            public float Heading;
        }

        /// <summary>Is this deck-frame point on her authored floor <b>without being dragged there</b>?
        /// The clamp returning the point unchanged is the whole question.</summary>
        private bool IsOnHerFloor(Vector2 deckPoint)
        {
            int hint = -1;
            Vector2 clamped = _deck.ClampToWalkable(deckPoint, ref hint, out float _);
            return (clamped - deckPoint).sqrMagnitude <= 1e-6f;
        }

        private Rig Build(float headingDegrees)
        {
            var boatGo = new GameObject("Boat");
            _spawned.Add(boatGo);
            boatGo.transform.position = new Vector3(213.5f, 4.25f, 0f);   // her St Peters berth
            // Compass degrees are CW from north and transform.up is the bow, so z is the negated heading.
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            // ⚠ BoatController carries [RequireComponent(typeof(BoatMooring))] — her rope arrives with
            // her controller. Never AddComponent a second one: production resolves the FIRST.
            var boat = boatGo.AddComponent<BoatController>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.dory";
            hull.LengthMeters = LoaMeters;
            boat.SetHull(hull);
            boat.enabled = false;
            boatGo.AddComponent<BoatDeckAreas>().Configure(_deck);

            BoatHullSkinner.Apply(boatGo, MeshVisual(), boat: null,
                                  new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });
            Assert.AreEqual(headingDegrees, BoatHullPresenterHost.Resolve(boatGo).DrawnHeadingDegrees(), 1e-2f,
                "harness: a mesh hull draws where her bow points — if this ever snaps, every heading " +
                "below is secretly heading 0 and the fixture proves nothing");

            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            playerGo.transform.position = boatGo.transform.position;      // alongside, well inside reach
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;

            var swGo = new GameObject("Switcher");
            _spawned.Add(swGo);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, null, null, 0f, null);

            return new Rig
            {
                Switcher = sw, Walk = deckWalk, Boat = boatGo.transform, Heading = headingDegrees,
                PlayerGo = playerGo, BoatGo = boatGo, SwitcherGo = swGo,
            };
        }

        /// <summary>⚠ Torn down per heading. Two switchers alive at once both publish who holds the helm,
        /// and a fixture that builds six stages poisons whatever runs next.</summary>
        private void Destroy(Rig rig)
        {
            Object.DestroyImmediate(rig.SwitcherGo);
            Object.DestroyImmediate(rig.PlayerGo);
            Object.DestroyImmediate(rig.BoatGo);
        }

        private BoatVisualDef MeshVisual()
        {
            var mesh = new Mesh(); _spawned.Add(mesh);
            var def = ScriptableObject.CreateInstance<HullMeshDef>(); _spawned.Add(def);
            def.Id = "hullmesh.board_seat";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = BakeElevationDeg;
            def.AzimuthCounterClockwise = true;
            def.WatertightHalfBeamMeters = 0.85f;

            var v = ScriptableObject.CreateInstance<BoatVisualDef>(); _spawned.Add(v);
            v.Id = "visual.board_seat";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = def;
            v.ArtBakeElevationDegrees = BakeElevationDeg;
            return v;
        }

        // ---- the Core presentation seam, doubled (no URP, no GPU) -----------------------------------

        private sealed class FakeRenderer : IHullMeshRenderer, IDeckOccupantSlots
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int layerId, int order) { }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 7f / 255f;
            public IDeckOccupantSlots DeckOccupants => this;

            public int Capacity => 1;
            public int ActiveCount => 0;
            public int Claim(object owner) => 0;
            public void Release(int slot, object owner) { }
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active) { }
            public float OccluderId(int slot) => 0f;
            public float OccluderIdTop => DeckOccluderId;
        }

        private sealed class FakeService : IHullMeshPresentationService
        {
            private readonly FakeRenderer _renderer = new FakeRenderer();
            public IHullMeshRenderer Install(GameObject host, HullMeshDef def,
                                             HullPaintSchemeDef scheme = null) => _renderer;
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }
    }
}
