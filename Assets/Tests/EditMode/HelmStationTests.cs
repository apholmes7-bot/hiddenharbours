using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>EVERY HULL STEERS FROM HER OWN WHEEL.</b> The runtime half of the guard for the owner's
    /// 2026-09-09 playtest: <i>"when piloting boats the sprite does not stay in the accurate helm
    /// position."</i> (The import half — sidecar to asset — is
    /// <c>Tests/EditMode/RigBaking/HelmStationImportTests</c>, which lives there because it needs the
    /// sidecar reader.)
    ///
    /// <para><b>What was wrong.</b> <c>ControlSwitcher._helmLocalOffset</c> — one serialized
    /// <see cref="Vector2"/> on the PLAYER, <c>(0, −1.3)</c>, tooltipped in its own words as "the tiller
    /// at the DORY'S stern" — answered "where is the helm?" for every hull in the fleet. A cape islander
    /// steers from a wheelhouse 1.35 m FORWARD of amidships and a dory from a tiller 2 m AFT of it; one
    /// number cannot be both, and with a 0.9 m reach you could stand at the drawn wheel and press E into
    /// nothing.</para>
    ///
    /// <para><b>Where the migration bar really lives.</b> <see cref="HelmStationHeadingTests"/> holds
    /// #789's tuned offset against a drawn stern and an outline at six headings. ⚠ It is a bar on the
    /// FALLBACK, and since 2026-09-09 it is no longer the dory's bar: she publishes a station now
    /// (<see cref="TheDorySteersFromHerOwnAfterThwart_TheSeatTheOwnerRuled"/>), and that suite keeps
    /// measuring the fallback only because it wires no deck data at all and calls <c>ConfigureHelm</c>
    /// with the tuned number itself. <see cref="AShippedHullWithNoStation_StillTakesTheTunedFallback"/>
    /// is the re-pointed oracle that stops it quietly changing meaning again: it holds the fallback
    /// against a hull that really does publish nothing, and fails BY NAME the day the last one is
    /// measured — which is the notice that the fallback and that suite can both be retired.</para>

    ///
    /// <para>Headless by construction: the hull wears a MESH visual behind the Core presentation seam (a
    /// test double), because a mesh hull is drawn exactly where her bow points — a sprite compass would
    /// snap 45° back to 0 and quietly turn six headings into two.</para>
    /// </summary>
    public class HelmStationTests
    {
        private const string DeckFolder = "Assets/_Project/Data/Boats/Decks";
        private const string DoryDeckPath = DeckFolder + "/DoryIso.asset";

        private const float BakeElevationDeg = 40f;
        private const float LoaMeters = 4.5f;
        private const float Tol = 1e-4f;

        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 270f };

        /// <summary>The offset as both scenes still carry it, and as #789 un-projected it.</summary>
        private static readonly Vector2 ShippedHelmOffset = new Vector2(0f, -1.3f);

        /// <summary>The starter dory's AFTER THWART in hull metres, exactly as
        /// <c>docs/art/rigs/gameplay/doryIsoRig.gameplay.json</c> publishes it (<c>STATIONS[id=helm]</c>,
        /// evaluated from her rig rather than transcribed): y = −L/2 + 0.34·L on a 4.5 m boat, and
        /// z = the keel line at that station plus the rig's own SEAT height. Restated here because a bar
        /// the asset supplies to itself is a mirror.</summary>
        private static readonly Vector3 DoryThwart = new Vector3(0f, -0.72f, 0.3056f);

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

        // ---- the cases -------------------------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>THE OWNER'S SEAT.</b> The starter dory steers from her OWN after thwart, published
        /// as <c>STATIONS[id=helm]</c> in <c>doryIsoRig.gameplay.json</c> and imported onto her deck def.
        /// (Owner, 2026-09-09: <i>"she can walk while standing in the dory in its narrow deck, sits at
        /// the helm with e and rows only from that position."</i>)
        ///
        /// <para><b>This case replaced the migration bar that stood here.</b> It used to assert the
        /// OPPOSITE — that the dory published no station — as the precondition of
        /// <see cref="HelmStationHeadingTests"/>, and its own failure message specified the migration:
        /// <i>"that is good news — but the migration oracle #789 left has to be re-pointed at a hull
        /// that still has none."</i> It has been. The re-pointed oracle is the case below this one, and
        /// the heading suite it protects wires no deck at all, so it measures the fallback by
        /// CONSTRUCTION rather than by the dory's data.</para>
        ///
        /// <para><b>The bar is her sidecar's number, not the switcher's.</b> The station is asserted
        /// against the hull metres the rig evaluates to — a seat 0.72 m abaft amidships standing
        /// 0.3056 m off the keel — and only THEN read back through <see cref="ControlSwitcher"/>. A
        /// case that asked the switcher for both halves would agree with itself on any number at all.</para>
        /// </summary>
        [Test]
        public void TheDorySteersFromHerOwnAfterThwart_TheSeatTheOwnerRuled()
        {
            BoatDeckDef dory = Load(DoryDeckPath);

            Assert.IsTrue(dory.HasHelmStation,
                "the starter dory's helm station has gone away. She is the boat the owner ruled on: she " +
                "sits at her after thwart on E and rows only from it, so a dory with no station is a dory " +
                "steered from a tuned screen offset 0.58 m abaft her seat, over bare bottom boards.");
            Assert.AreEqual("STATIONS[id=helm]", dory.HelmStationSource,
                "her station must come from the sidecar's STATIONS array by id — the provenance is the " +
                "difference between an imported measurement and a number somebody typed into the asset");

            Assert.AreEqual(DoryThwart.x, dory.HelmStationLocalMeters.x, Tol,
                "she rows from the centreline");
            Assert.AreEqual(DoryThwart.y, dory.HelmStationLocalMeters.y, Tol,
                "the AFTER thwart — the first seat of the rig's thwart loop, station(0.34) of her LOA");
            Assert.AreEqual(DoryThwart.z, dory.HelmStationLocalMeters.z, Tol,
                "…and the seat TOP, 0.24 m above her bottom boards — a thwart you sit on, not a spot " +
                "on the sole");

            // …and the switcher actually seats her there, in her own metres.
            Vector2 seat = Build(0f, dory).HelmDeckOffset();
            Assert.AreEqual(DoryThwart.x, seat.x, Tol, "the switcher moved her station abeam");
            Assert.AreEqual(DoryThwart.y, seat.y, Tol, "the switcher moved her station along the keel");

            // The negative control: this is HER seat, not the shared tiller she used to be steered from.
            Vector2 fallback = DeckAreaMath.WorldToDeck(ShippedHelmOffset, 0f, 0f, BakeElevationDeg);
            Assert.Greater(Mathf.Abs(seat.y - fallback.y), 0.5f,
                $"her seat {seat} is still the tuned fallback {fallback} — her own station is not reaching " +
                "the switcher, and the import did nothing");

            // …and it is somewhere she can WALK to. The fallback was not: 2.02 m abaft amidships is
            // 0.22 m astern of the after end of her floor, which is why E at her seat used to be a reach
            // back over the transom rather than a sit-down.
            Assert.Less(Mathf.Abs(seat.y), dory.WalkHalfExtents.y,
                $"her helm must lie INSIDE the floor she walks ({dory.WalkHalfExtents.y:0.00} m aft of " +
                "amidships) — you cannot sit at a seat you cannot stand at");
            Assert.Greater(Mathf.Abs(fallback.y), dory.WalkHalfExtents.y,
                "harness: the fallback is supposed to be OUTSIDE her floor — if it is not, the line above " +
                "is not distinguishing the seat from the tiller");
        }

        /// <summary>⭐ <b>THE RE-POINTED MIGRATION ORACLE.</b> A shipped hull whose rig publishes NO
        /// station still gets the tuned offset she always got, unchanged — so the fallback branch is
        /// exercised by real fleet data rather than surviving untested the day the last hull is measured.
        ///
        /// <para>This is what <see cref="HelmStationHeadingTests"/> rests on. That suite builds a bare
        /// hull with no deck data at all and calls <c>ConfigureHelm</c> with the shipped offset, so it
        /// measures the fallback by construction; this case is the promise that the fallback is still a
        /// thing the game does. Asserted through the un-projection that IS the shipped behaviour, at
        /// heading north where the projection and its inverse are exact.</para>
        ///
        /// <para>Chosen from the DATA rather than named. When the last unstationed hull is finally
        /// measured this fails by NAME — and the answer then is to retire the fallback and this pair of
        /// cases together, not to pin a hull back to none.</para>
        /// </summary>
        [Test]
        public void AShippedHullWithNoStation_StillTakesTheTunedFallback()
        {
            (string stem, BoatDeckDef def) hull = AnUnstationedHull();

            Vector2 helm = Build(0f, hull.def).HelmDeckOffset();
            Vector2 asShipped = DeckAreaMath.DeckToWorld(helm, 0f, 0f, BakeElevationDeg);

            Assert.AreEqual(ShippedHelmOffset.x, asShipped.x, Tol,
                $"{hull.stem}: x is untouched by the per-hull station");
            Assert.AreEqual(ShippedHelmOffset.y, asShipped.y, Tol,
                $"{hull.stem}: with her bow north the un-projection and the projection are exact inverses, " +
                "so the tuned number reaches the helm unchanged — the fallback branch is taken before " +
                "anything is computed, which is why this is bit-identical by construction and not by care");
        }

        /// <summary>⭐ A hull WITH a station is seated at it, in her own metres, at every heading — the
        /// property one screen-tuned number could never have. A station is heading-independent by
        /// construction (it is the frame the deck polygons live in), so the same hull metres come back
        /// however she is lying, and the failure prints what the old shared number would have said.</summary>
        [Test]
        public void AStationedHull_SeatsHerPilotAtHerOwnWheel_AtEveryHeading()
        {
            (string stem, BoatDeckDef def) hull = AStationedHull();
            var expected = new Vector2(hull.def.HelmStationLocalMeters.x, hull.def.HelmStationLocalMeters.y);
            Vector2 wouldHaveBeen = DeckAreaMath.WorldToDeck(ShippedHelmOffset, 0f, 0f, BakeElevationDeg);

            foreach (float heading in Headings)
            {
                Vector2 seat = Build(heading, hull.def).HelmDeckOffset();
                Assert.AreEqual(expected.x, seat.x, Tol,
                    $"{hull.stem} at {heading:F0}°: her station moved abeam — a hull-frame station cannot. " +
                    $"(The shared fallback would have said {wouldHaveBeen}.)");
                Assert.AreEqual(expected.y, seat.y, Tol,
                    $"{hull.stem} at {heading:F0}°: her station moved along the keel — a hull-frame station " +
                    $"cannot. (The shared fallback would have said {wouldHaveBeen}.)");
            }
        }

        /// <summary>⭐ …and it is HER station, not the dory's. The negative control: without it, a bug that
        /// returned the fallback everywhere would pass the heading test above, because the fallback is
        /// heading-independent in the hull frame too.</summary>
        [Test]
        public void AStationedHull_IsNotSteeredFromTheDorysTiller()
        {
            (string stem, BoatDeckDef def) hull = AStationedHull();
            Vector2 fallback = DeckAreaMath.WorldToDeck(ShippedHelmOffset, 0f, 0f, BakeElevationDeg);
            Vector2 seat = Build(0f, hull.def).HelmDeckOffset();

            Assert.Greater((seat - fallback).magnitude, 0.05f,
                $"{hull.stem} is seated at {seat}, which is the shared fallback {fallback} — her own " +
                $"station {hull.def.HelmStationLocalMeters} is not reaching the switcher");
        }

        /// <summary>⚠ The station's HEIGHT reaches the world position, or the reach test compares two
        /// frames. <c>DeckWalkController</c> lifts a player standing on the deck by
        /// <c>height × cos(elev)</c>, so a helm spot pinned at height 0 sits that far below her feet —
        /// 0.57 m on a 0.74 m station at a 40° bake, against a 0.9 m reach. Most of the reach, spent on a
        /// frame error.</summary>
        [Test]
        public void TheStationsHeight_LiftsTheHelmSpotTheWayItLiftsThePlayer()
        {
            (string stem, BoatDeckDef def) hull = AStationedHull(needsHeight: true);
            Vector3 station = hull.def.HelmStationLocalMeters;

            Vector2 lifted = DeckAreaMath.DeckToWorld(new Vector2(station.x, station.y), station.z,
                                                      0f, BakeElevationDeg);
            Vector2 flat = DeckAreaMath.DeckToWorld(new Vector2(station.x, station.y), 0f, 0f,
                                                    BakeElevationDeg);
            Assert.AreNotEqual(flat.y, lifted.y,
                $"{hull.stem}: this station stands {station.z:F2} m off the keel, so the lift is supposed " +
                "to be visible — a fixture that cannot see it cannot fail on it");

            (ControlSwitcher sw, Transform boatRoot) = BuildWithBoat(0f, hull.def);
            Vector3 relative = sw.HelmWorldPosition - boatRoot.position;
            Assert.AreEqual(lifted.y, relative.y, 1e-3f,
                $"{hull.stem}: the helm spot is {lifted.y - relative.y:F3} m from where a player standing " +
                "at that station is drawn — the station's height is being dropped on the way through");
        }

        // ---- harness ---------------------------------------------------------------------------------

        private static BoatDeckDef Load(string path)
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
            Assert.IsNotNull(def, $"missing deck asset {path}");
            return def;
        }

        /// <summary>Any shipped hull whose rig publishes a station — chosen from the DATA rather than
        /// named, so a re-cut fleet moves these guards with it. Fails loudly rather than skipping when
        /// none carries one: "no hull has a station" is exactly the state this PR exists to end, and a
        /// silent skip would report it as a pass.</summary>
        private static (string stem, BoatDeckDef def) AStationedHull(bool needsHeight = false)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:BoatDeckDef", new[] { DeckFolder })
                                                             .OrderBy(g => g))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
                if (def == null || !def.HasHelmStation) continue;
                if (needsHeight && def.HelmStationLocalMeters.z <= 0.05f) continue;
                return (Path.GetFileNameWithoutExtension(path), def);
            }

            Assert.Fail(needsHeight
                ? "no shipped deck carries a helm station standing clear of the keel"
                : "no shipped deck carries a helm station at all — run Hidden Harbours ▸ Dev ▸ Boats ▸ " +
                  "Import deck sidecars and commit the assets, or every hull in the game is still being " +
                  "steered from the dory's tiller");
            return default;
        }

        /// <summary>…and the mirror of it: any shipped hull whose rig publishes NO station, so the
        /// FALLBACK branch is measured against real fleet data. Chosen from the data for the same reason
        /// — the day the fleet is fully measured this fails by name, which is the notice that the
        /// fallback (and the suite resting on it) can be retired.</summary>
        private static (string stem, BoatDeckDef def) AnUnstationedHull()
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:BoatDeckDef", new[] { DeckFolder })
                                                             .OrderBy(g => g))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
                if (def == null || def.HasHelmStation) continue;
                return (Path.GetFileNameWithoutExtension(path), def);
            }

            Assert.Fail("every shipped deck now carries a helm station. That is the end state this whole " +
                        "migration was for — but it means ControlSwitcher's tuned fallback is no longer " +
                        "reachable from any boat in the game, and HelmStationHeadingTests is measuring a " +
                        "branch nothing takes. Retire the fallback and both suites, do not pin a hull back.");
            return default;
        }

        /// <summary>A boat wearing <paramref name="deck"/> at <paramref name="headingDegrees"/>, with a
        /// switcher aboard her. ⚠ The deck is wired AFTER the skinner — <c>BoatHullSkinner.Apply</c>
        /// writes <c>BoatDeckAreas</c> from the VISUAL, so a deck configured before it is wiped and every
        /// case here would silently measure the greybox rectangle instead.</summary>
        private ControlSwitcher Build(float headingDegrees, BoatDeckDef deck)
            => BuildWithBoat(headingDegrees, deck).switcher;

        /// <summary>…and her root, for the one case that needs a boat-RELATIVE read: the switcher does not
        /// publish its boat, and asking it to would widen a seam for a fixture.</summary>
        private (ControlSwitcher switcher, Transform boat) BuildWithBoat(float headingDegrees, BoatDeckDef deck)
        {
            var boatGo = new GameObject("Boat");
            _spawned.Add(boatGo);
            // Compass degrees are CW from north and transform.up is the bow, so z is the negated heading.
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            // ⚠ BoatController carries [RequireComponent(typeof(BoatMooring))] — never AddComponent a
            // second one; production resolves the FIRST.
            var boat = boatGo.AddComponent<BoatController>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.fixture";
            hull.LengthMeters = LoaMeters;
            boat.SetHull(hull);
            boat.enabled = false;

            BoatHullSkinner.Apply(boatGo, MeshVisual(), boat: null,
                                  new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });

            boatGo.AddComponent<BoatDeckAreas>().Configure(deck);
            Assert.IsNotNull(BoatDeckAreas.Resolve(boatGo),
                "harness: her deck must survive the skinner — if this is null the switcher sees no hull " +
                "station and every case below is secretly measuring the fallback");
            // ⚠ Resolved and NAMED before it is dereferenced. A null host here is a fixture that
            // built a hull the skinner would not present, and an NRE says so in the least useful
            // way there is — four cases dead before a single station is compared.
            IBoatHullPresenter presenter = BoatHullPresenterHost.Resolve(boatGo);
            Assert.IsNotNull(presenter,
                "harness: the skinner installed no presenter, so this hull is not being DRAWN at " +
                "all — check MeshVisual() still says Variant = Mesh and carries a mesh, ramps and " +
                "a cell size");
            Assert.AreEqual(headingDegrees, presenter.DrawnHeadingDegrees(), 1e-2f,
                "harness: a mesh hull draws where her bow points — if this ever snaps, every heading " +
                "below is secretly heading 0 and the fixture proves nothing");

            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            playerGo.AddComponent<DeckWalkController>().enabled = false;

            var swGo = new GameObject("Switcher");
            _spawned.Add(swGo);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, null, null, 0f, null);
            return (sw, boatGo.transform);
        }

        /// <summary>
        /// A MESH hull the skinner can actually present — <c>DeckRiderHullSwapTests</c> /
        /// <c>BoardingSeatHeadingTests</c>' double, field for field.
        ///
        /// <para>⚠ <b>Every field here is load-bearing, and leaving one out fails in the FIXTURE
        /// rather than the assertion.</b> The first cut of this set a <c>HullMesh</c> and an
        /// elevation and stopped — no <see cref="BoatHullVariant.Mesh"/> variant, no mesh, no
        /// ramps, no cell size. <c>BoatHullSkinner.Apply</c> then installed no mesh presenter,
        /// <c>BoatHullPresenterHost.Resolve</c> came back null, and all four cases died on a
        /// NullReferenceException in <see cref="BuildWithBoat"/> before a single station was
        /// compared (CI run 34309924065). A hull is not a mesh hull because a field is named
        /// HullMesh; she is one because her VARIANT says so.</para>
        /// </summary>
        private BoatVisualDef MeshVisual()
        {
            var mesh = new Mesh();
            _spawned.Add(mesh);

            var meshDef = ScriptableObject.CreateInstance<HullMeshDef>();
            _spawned.Add(meshDef);
            meshDef.Id = "hullmesh.helm_station";
            meshDef.Mesh = mesh;
            meshDef.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 },
            };
            meshDef.Bayer16 = new float[16];
            meshDef.PxPerMetre = 32;
            meshDef.CellW = 456;
            meshDef.CellH = 420;
            meshDef.ElevationDeg = BakeElevationDeg;
            meshDef.AzimuthCounterClockwise = true;
            meshDef.WatertightHalfBeamMeters = 0.85f;

            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(visual);
            visual.Id = "visual.helm_station";
            visual.Variant = BoatHullVariant.Mesh;
            visual.HullMesh = meshDef;
            visual.ArtBakeElevationDegrees = BakeElevationDeg;
            return visual;
        }

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
