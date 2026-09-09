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
    /// <para><b>Where the migration bar really lives.</b> <see cref="HelmStationHeadingTests"/> already
    /// holds #789's tuned dory helm against her drawn stern and her outline at six headings. This PR does
    /// not touch that number — the dory publishes no station, so she takes the fallback branch before
    /// anything is computed. <see cref="TheDoryStillHasNoStation_SoTheShippedBarStillMeasuresTheFallback"/>
    /// is what stops that suite quietly changing meaning: the day her rig starts publishing a station, it
    /// stops being a bar on the fallback and nobody would otherwise notice.</para>
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

        /// <summary>⭐⭐ THE MIGRATION BAR, stated as the precondition of the suite that holds it. A hull
        /// with no published station gets the tuned offset she always got, unchanged — and the dory is one
        /// of those hulls, so every assertion in <see cref="HelmStationHeadingTests"/> still measures the
        /// fallback. Asserted through the un-projection that IS the shipped behaviour, at heading north
        /// where the projection and its inverse are exact.</summary>
        [Test]
        public void TheDoryStillHasNoStation_SoTheShippedBarStillMeasuresTheFallback()
        {
            BoatDeckDef dory = Load(DoryDeckPath);
            Assert.IsFalse(dory.HasHelmStation,
                "the dory's rig has begun publishing a helm station. That is good news — but it means " +
                "HelmStationHeadingTests is no longer a bar on the FALLBACK, and the migration oracle " +
                "#789 left has to be re-pointed at a hull that still has none before that suite can be " +
                "trusted to mean what its doc says.");

            Vector2 helm = Build(0f, dory).HelmDeckOffset();
            Vector2 asShipped = DeckAreaMath.DeckToWorld(helm, 0f, 0f, BakeElevationDeg);

            Assert.AreEqual(ShippedHelmOffset.x, asShipped.x, Tol, "x is untouched by the per-hull station");
            Assert.AreEqual(ShippedHelmOffset.y, asShipped.y, Tol,
                "with her bow north the un-projection and the projection are exact inverses, so the tuned " +
                "number reaches the helm unchanged — the fallback branch is taken before anything is " +
                "computed, which is why this is bit-identical by construction and not by care");
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
