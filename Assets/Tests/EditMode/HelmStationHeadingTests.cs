using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>The helm is where the tiller is DRAWN — at every heading she can lie on.</b>
    /// (Owner playtest, St Peters, 2026-09-07: <i>"when i mount the dory and try to push e at the helm,
    /// i immediately jump ashore with a rope"</i>.)
    ///
    /// <para><b>The defect, in one sentence.</b> <see cref="ControlSwitcher.HelmWorldPosition"/> was the
    /// boat's origin plus a WORLD-axis offset, written when the boat picture had one facing. The starter
    /// dory lies N–S at her berth, so the helm spot sat abeam of her — past her half-beam, in the water on
    /// the pier side — while her tiller was drawn at her stern. The press then fell through the deck ladder
    /// to step-ashore, and the painter went into the player's hand.</para>
    ///
    /// <para><b>Two bars, and neither is the code's own arithmetic read back</b> (a guard that asks the
    /// code for its own bar is a mirror):
    /// <list type="bullet">
    ///   <item>the helm must be within <c>_helmReach</c> of <b>where her STERN is drawn</b> — computed from
    ///   her authored <see cref="BoatHullDef.LengthMeters"/>, not from the helm offset;</item>
    ///   <item>the helm must lie <b>inside her outline</b> — <see cref="HullFootprint"/>, built from her
    ///   authored length and the half-beam her hull mesh carries.</item>
    /// </list>
    /// Both are the starter dory's real numbers: 4.5 m LOA, 0.85 m half-beam, artwork baked at 40°.</para>
    ///
    /// <para><b>Headless by construction</b>: the hull wears a MESH visual behind the Core presentation
    /// seam (a test double, the same one <c>DeckRiderHullSwapTests</c> uses), because a mesh hull is drawn
    /// exactly where her bow points — no facing grid to snap 45° back to 0 and quietly turn six headings
    /// into two.</para>
    /// </summary>
    public class HelmStationHeadingTests
    {
        // ---- the starter dory's authored numbers (Dory.asset, DoryIsoHullMesh.asset, DoryIso.asset) ----
        private const float LoaMeters = 4.5f;             // BoatHullDef.LengthMeters
        private const float HalfBeamMeters = 0.85f;       // HullMeshDef.WatertightHalfBeamMeters
        private const float BakeElevationDeg = 40f;       // BoatVisualDef.ArtBakeElevationDegrees
        private const float WalkHalfLengthMeters = 1.8f;  // BoatDeckDef.WalkHalfExtents.y — her floor

        /// <summary>The helm station as it shipped, and as both scenes still carry it: 1.3 m down-screen
        /// of her origin with her bow north.</summary>
        private static readonly Vector2 ShippedHelmOffset = new Vector2(0f, -1.3f);
        private const float HelmReach = 0.9f;

        /// <summary>Every heading a dory can be found lying on, including the two that matter at St Peters
        /// (she is banked at rotZ 90°, which is a drawn heading of 270°).</summary>
        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 270f };

        private const float Tol = 1e-4f;

        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            HullMeshPresentation.Service = new FakeService();
            GameServices.Reset();
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
        /// The migration bar: <b>a boat pointing north is bit-identical to the behaviour that shipped</b>.
        /// North is the only heading the number was ever tuned at, so it is the one that must not move —
        /// and it must not move on artwork with a ¾ bake either, which is why this asserts the projected
        /// answer rather than the stored field.
        /// </summary>
        [Test]
        public void HeadingNorth_IsBitIdenticalToTheHelmThatShipped()
        {
            var rig = Build(headingDegrees: 0f);
            Vector3 shipped = rig.Boat.position + (Vector3)ShippedHelmOffset;

            Assert.AreEqual(shipped.x, rig.Switcher.HelmWorldPosition.x, Tol, "x is untouched by the fix");
            Assert.AreEqual(shipped.y, rig.Switcher.HelmWorldPosition.y, Tol,
                "with her bow north the un-projection and the projection are exact inverses, so the tuned " +
                "number lands exactly where it always did");
        }

        /// <summary>
        /// The fix: <b>the helm is within reach of her drawn stern whichever way she lies</b> — and the
        /// SAME assertion against the shipped world-axis formula fails at the headings the owner found her
        /// on, so this cannot pass on the old code.
        /// </summary>
        [Test]
        public void AtEveryHeading_TheHelmIsWithinReachOfHerDrawnStern()
        {
            var worstFixed = 0f;
            var worstOld = 0f;

            foreach (float heading in Headings)
            {
                var rig = Build(heading);
                Vector3 stern = DrawnSternOf(rig.Boat, heading);

                float fixedError = Vector2.Distance(rig.Switcher.HelmWorldPosition, stern);
                float oldError = Vector2.Distance(rig.Boat.position + (Vector3)ShippedHelmOffset, stern);
                worstFixed = Mathf.Max(worstFixed, fixedError);
                worstOld = Mathf.Max(worstOld, oldError);

                Assert.LessOrEqual(fixedError, HelmReach,
                    $"at {heading}° the helm must be within reach of the tiller she is DRAWN with " +
                    $"(it is {fixedError:0.000} m away)");

                Destroy(rig);
            }

            Assert.Less(worstFixed, HelmReach, "the worst heading still leaves the tiller reachable");
            Assert.Greater(worstOld, HelmReach,
                $"the shipped world-axis helm was out of reach of her own stern on some heading " +
                $"(worst {worstOld:0.000} m against a {HelmReach} m reach) — without this the case above " +
                "would pass on the very code it was written to condemn");
        }

        /// <summary>
        /// The second bar: <b>the helm spot is a place ON the boat</b>. It was 0.45 m outboard of her port
        /// side at her banked heading, which is the whole reason a player standing at the drawn tiller was
        /// judged to be nowhere near the helm.
        /// </summary>
        [Test]
        public void AtEveryHeading_TheHelmLandsInsideHerOutline()
        {
            bool theOldOneEverEscaped = false;

            foreach (float heading in Headings)
            {
                var rig = Build(heading);
                var outline = HullFootprint.FromHeading(rig.Boat.position, heading,
                                                        LoaMeters, HalfBeamMeters);

                Assert.IsTrue(outline.Contains(rig.Switcher.HelmWorldPosition),
                    $"at {heading}° the helm must be on the hull, not beside her " +
                    $"({outline.DistanceTo((Vector2)rig.Switcher.HelmWorldPosition):0.000} m off her outline)");

                if (!outline.Contains((Vector2)(rig.Boat.position + (Vector3)ShippedHelmOffset)))
                    theOldOneEverEscaped = true;

                Destroy(rig);
            }

            Assert.IsTrue(theOldOneEverEscaped,
                "the shipped helm left the hull entirely on at least one heading — the negative control " +
                "for the case above");
        }

        /// <summary>
        /// The player's half of it: <b>standing as far aft as her floor allows, the helm is in reach</b>.
        /// The two points come from two different authored numbers — her walkable half-length (1.8 m of
        /// floor) and the tuned helm offset — so agreeing is a fact about the boat, not about one formula
        /// read back through itself.
        /// </summary>
        [Test]
        public void AtEveryHeading_AFisherOnHerAfterFloorCanReachTheHelm()
        {
            foreach (float heading in Headings)
            {
                var rig = Build(heading);

                // The aftmost spot on her floor, drawn: deck frame (0, −1.8) through the same ¾ projection
                // the deck-walk clamps the player into.
                Vector3 afterFloor = rig.Boat.position + (Vector3)DeckAreaMath.DeckToWorld(
                    new Vector2(0f, -WalkHalfLengthMeters), 0f, heading, BakeElevationDeg);

                rig.PlayerGo.transform.position = afterFloor;
                Assert.IsTrue(rig.Switcher.WithinHelmReach(),
                    $"at {heading}° a fisher standing on the after end of her floor must be able to take " +
                    $"the helm (she is {Vector2.Distance(afterFloor, rig.Switcher.HelmWorldPosition):0.000} m off it)");

                Destroy(rig);
            }
        }

        /// <summary>
        /// The tuned number still names a place on the boat you can point at: 1.3 drawn metres aft with her
        /// bow north is <b>2.02 metres of real hull</b>, because the artwork is baked at 40° and the along-
        /// view axis is squashed by sin(40°). Pinned because it is the sentence the tooltip now makes, and
        /// because a future re-bake at another elevation must move this number, not silently keep it.
        /// </summary>
        [Test]
        public void TheTunedOffsetNamesTwoMetresOfRealHull()
        {
            var rig = Build(headingDegrees: 0f);
            Vector2 deck = rig.Switcher.HelmDeckOffset();

            Assert.AreEqual(0f, deck.x, Tol, "she steers from the centreline");
            Assert.AreEqual(ShippedHelmOffset.y / Mathf.Sin(BakeElevationDeg * Mathf.Deg2Rad), deck.y, 1e-3f,
                "the drawn offset un-projects to hull metres through the artwork's own bake elevation");
            Assert.Less(Mathf.Abs(deck.y), LoaMeters * 0.5f,
                "…and it is still inside her: a tiller aft of the transom would be a fitting on the water");
            Assert.Greater(Mathf.Abs(deck.y), WalkHalfLengthMeters,
                "…and abaft her floor, which is where a dory's tiller is — you reach back to it");
        }

        // ---- the rig --------------------------------------------------------------------------------

        private struct Rig
        {
            public ControlSwitcher Switcher;
            public Transform Boat;
            public GameObject PlayerGo;
            public GameObject BoatGo;
            public GameObject SwitcherGo;
        }

        /// <summary>Where her stern is DRAWN — her authored half-length aft along the keel, through the
        /// artwork's own projection. The bar, and deliberately not built from the helm offset.</summary>
        private static Vector3 DrawnSternOf(Transform boat, float headingDegrees)
            => boat.position + (Vector3)DeckAreaMath.DeckToWorld(
                   new Vector2(0f, -LoaMeters * 0.5f), 0f, headingDegrees, BakeElevationDeg);

        /// <summary>A dory lying on <paramref name="headingDegrees"/>, wearing a mesh hull so the picture
        /// is drawn exactly where her bow points, with a switcher on deck beside her.</summary>
        private Rig Build(float headingDegrees)
        {
            var boatGo = new GameObject("Boat");
            _spawned.Add(boatGo);
            boatGo.transform.position = new Vector3(213.5f, 4.25f, 0f);   // her St Peters berth, as banked
            // Compass degrees are CW from north and transform.up is the bow, so z is the negated heading.
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            var boat = boatGo.AddComponent<BoatController>();             // auto-adds Rigidbody2D
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.dory";
            hull.LengthMeters = LoaMeters;
            boat.SetHull(hull);
            boat.enabled = false;

            BoatHullSkinner.Apply(boatGo, MeshVisual(), boat: null,
                                  new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });
            Assert.AreEqual(headingDegrees, BoatHullPresenterHost.Resolve(boatGo).DrawnHeadingDegrees(), 1e-2f,
                "harness: a mesh hull draws where her bow points — if this ever snaps, every heading " +
                "below is secretly heading 0 and the fixture proves nothing");

            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            var walk = playerGo.AddComponent<PlayerWalkController>();     // auto-adds Rigidbody2D + renderer
            playerGo.AddComponent<DeckWalkController>().enabled = false;

            var swGo = new GameObject("Switcher");
            _spawned.Add(swGo);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, null, null, 0f, null);
            sw.ConfigureHelm(ShippedHelmOffset, HelmReach);

            return new Rig
            {
                Switcher = sw, Boat = boatGo.transform,
                PlayerGo = playerGo, BoatGo = boatGo, SwitcherGo = swGo,
            };
        }

        /// <summary>⚠ Torn down per heading rather than at the end. Two switchers alive at once both
        /// publish who holds the helm, and a fixture that builds six stages poisons whatever runs next.</summary>
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
            def.Id = "hullmesh.helm_heading";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = BakeElevationDeg;
            def.AzimuthCounterClockwise = true;
            def.WatertightHalfBeamMeters = HalfBeamMeters;

            var v = ScriptableObject.CreateInstance<BoatVisualDef>(); _spawned.Add(v);
            v.Id = "visual.helm_heading";
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
