using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The rider must read the hull she is standing on, not the one she boarded.</b>
    ///
    /// <para>The dev hull picker re-skins a boat <i>in place</i>: the physics root never changes, so
    /// <see cref="DeckRiderVisual"/>'s once-per-binding resolve never re-armed and it went on holding the
    /// presenter of a hull that had been torn off the boat. Nothing threw — both presenters are
    /// deliberately null-tolerant, and a dead one answers heading <b>0</b> — so the only symptom was a
    /// fisher who quietly stopped turning with her boat and faced north for the rest of the voyage.</para>
    ///
    /// <para><b>What makes the swap observable here.</b> The boat is held at a true heading of 40° and
    /// wears, in turn, two hulls that genuinely disagree about the picture on screen: a 4-facing SPRITE
    /// compass draws her snapped to 0° (north is the nearest cell), a MESH hull draws her continuously at
    /// 40°. So "which presenter is being read" is a number, not an inference — and 0° is BOTH the old
    /// hull's honest answer and the dead-presenter default, which is exactly the coincidence that let the
    /// defect hide.</para>
    ///
    /// <para>Headless by construction: the mesh renderer behind the Core seam is a test double, so nothing
    /// here needs a GPU (the same discipline as <c>MeshHullPresenterTests</c>, whose doubles these mirror).</para>
    /// </summary>
    public class DeckRiderHullSwapTests
    {
        private const float TrueHeadingDegrees = 40f;   // snapped to 0 by 4-facing art; drawn 40 by a mesh
        private const float Tol = 1e-3f;

        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;

        [SetUp]
        public void SetUp() => _previousService = HullMeshPresentation.Service;

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- doubles (the Core presentation seam; no URP, no GPU) ---------------------------------

        private sealed class FakeRenderer : IHullMeshRenderer, IDeckOccupantSlots
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int layerId, int order) { }

            // The deck-occupant channel: recorded, so the test can pin WHICH hull was told a fisher is
            // standing on her — the second half of the same stale-presenter bug.
            public Vector3 OccupantRigMeters { get; private set; }
            public bool OccupantActive { get; private set; }
            public float DeckOccluderIdValue { get; set; } = 7f / 255f;
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active)
            { OccupantRigMeters = rigLocalMeters; OccupantActive = active; }
            public float DeckOccluderId => DeckOccluderIdValue;

            // The SLOT seam, which is what the rider actually publishes through. This double is
            // itself the slot array — one slot is all it needs, because the question it exists to
            // answer is WHICH hull was told somebody is aboard, not how many can stand on her.
            public IDeckOccupantSlots DeckOccupants => this;

            public int Capacity => 1;
            public int ActiveCount => OccupantActive ? 1 : 0;
            public int Claim(object owner) => 0;
            public void Release(int slot, object owner) => OccupantActive = false;
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active)
                => SetDeckOccupant(rigLocalMeters, active);
            public float OccluderId(int slot) => OccupantActive ? DeckOccluderIdValue : 0f;
            public float OccluderIdTop => DeckOccluderIdValue;
        }

        private sealed class FakeService : IHullMeshPresentationService
        {
            public readonly FakeRenderer Renderer = new FakeRenderer();
            public IHullMeshRenderer Install(GameObject host, HullMeshDef def) => Renderer;
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        // ---- the rig ------------------------------------------------------------------------------

        private Sprite NewCell()
        {
            var tex = new Texture2D(4, 4); _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); _spawned.Add(spr);
            return spr;
        }

        /// <summary>A 4-facing sprite compass: coarse ENOUGH that 40° snaps to north, which is what makes
        /// the sprite hull's honest answer differ from the mesh hull's.</summary>
        private BoatVisualDef MakeSpriteVisual()
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(v);
            v.Id = "visual.rider_swap_sprite";
            v.Variant = BoatHullVariant.Sprite;
            var facings = new Sprite[4];
            for (int i = 0; i < facings.Length; i++) facings[i] = NewCell();
            v.Facings = facings;
            v.FacingsAreCounterClockwise = false;
            v.ArtBakeElevationDegrees = 40f;
            return v;
        }

        private BoatVisualDef MakeMeshVisual()
        {
            var mesh = new Mesh(); _spawned.Add(mesh);
            var def = ScriptableObject.CreateInstance<HullMeshDef>(); _spawned.Add(def);
            def.Id = "hullmesh.rider_swap";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = 40f;
            def.AzimuthCounterClockwise = true;

            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(v);
            v.Id = "visual.rider_swap_mesh";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = def;
            v.ArtBakeElevationDegrees = 40f;
            return v;
        }

        /// <summary>A boat root held at <see cref="TrueHeadingDegrees"/>. Compass degrees are CW from
        /// north and <c>transform.up</c> is the bow, so the z rotation is the negated heading.</summary>
        private GameObject MakeBoat()
        {
            var root = new GameObject("Boat");
            _spawned.Add(root);
            root.transform.rotation = Quaternion.Euler(0f, 0f, -TrueHeadingDegrees);
            return root;
        }

        /// <summary>The player rig the rider needs: a body renderer to mirror, a child renderer to draw
        /// into, and the character whose facing is the rider's output.</summary>
        private DeckRiderVisual MakeRider(out IsoCharacterSprite character)
        {
            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            var body = playerGo.AddComponent<SpriteRenderer>();
            character = playerGo.AddComponent<IsoCharacterSprite>();

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            var riderSr = riderGo.AddComponent<SpriteRenderer>();
            riderSr.enabled = false;

            var rider = playerGo.AddComponent<DeckRiderVisual>();
            rider.Configure(riderSr, body, character);
            return rider;
        }

        private static BoatHullSkinner.Options SkinOptions =>
            // No wave motion and no oars: this test is about which presenter is READ, and the rock and the
            // overlays would only add rig to tear down.
            new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true };

        // ---- the swap -----------------------------------------------------------------------------

        [Test]
        public void TheTwoHulls_DisagreeAboutTheDrawnHeading_SoTheSwapIsObservable()
        {
            // The premise the rest of the fixture rests on. If a future art change made the sprite compass
            // fine enough to draw 40° itself, every assertion below would pass on a stale read too — so
            // pin the disagreement first, and fail HERE rather than silently going green.
            HullMeshPresentation.Service = new FakeService();   // else the mesh branch falls back to sprite
            var root = MakeBoat();

            BoatHullSkinner.Apply(root, MakeSpriteVisual(), boat: null, SkinOptions);
            var sprite = BoatHullPresenterHost.Resolve(root);
            Assert.AreEqual(0f, sprite.DrawnHeadingDegrees(), Tol,
                "a 4-facing compass draws a boat pointing 40° as her NORTH cell");

            BoatHullSkinner.Apply(root, MakeMeshVisual(), boat: null, SkinOptions);
            var mesh = BoatHullPresenterHost.Resolve(root);
            Assert.AreEqual(TrueHeadingDegrees, mesh.DrawnHeadingDegrees(), Tol,
                "a mesh hull has no facing grid — she is drawn exactly where the bow points");
        }

        [Test]
        public void HullSwappedUnderTheRider_TheFacingFollowsTheHullNowWorn()
        {
            // THE DEFECT, in one test. Same boat root throughout — that is the whole trigger: the picker
            // never rebinds the rider, so a presenter cached at bind time is never re-armed.
            HullMeshPresentation.Service = new FakeService();
            var root = MakeBoat();
            BoatHullSkinner.Apply(root, MakeSpriteVisual(), boat: null, SkinOptions);

            var rider = MakeRider(out IsoCharacterSprite character);
            rider.SetMode(ControlMode.Aboard, root.transform);
            Assert.AreEqual(0f, rider.HullDrawnHeadingDegrees, Tol,
                "aboard the sprite hull the fisher reads the snapped picture she is standing on");

            // The picker, minus the keyboard: re-skin the SAME root with a hull that draws differently.
            BoatHullSkinner.Apply(root, MakeMeshVisual(), boat: null, SkinOptions);
            rider.SetMode(ControlMode.Aboard, root.transform);

            Assert.AreEqual(TrueHeadingDegrees, rider.HullDrawnHeadingDegrees, Tol,
                "the rider must re-resolve the presenter: a cached one now answers for a hull that was " +
                "torn off the boat, and answers 0 (north) doing it");

            // …and it reaches the figure. ReleaseHeading publishes whatever was last held, which is the
            // only public read of it — the fisher keeps looking where the boat was pointed.
            Assert.IsTrue(character.IsHeadingHeld, "the rider drives the character's facing while aboard");
            character.ReleaseHeading();
            Assert.AreEqual(TrueHeadingDegrees, character.HeadingDegrees, Tol,
                "the composed facing the character is drawn along follows the new hull too");
        }

        [Test]
        public void HullSwappedUnderTheRider_TheNewHullIsToldSomeoneIsStandingOnHer()
        {
            // The same stale reference had a second cost: the occupant mark (what lets a mesh hull draw her
            // own wheelhouse OVER her crew) was being written to the hull the player had just stepped off,
            // so the hull actually on screen never knew anyone was aboard and drew straight through them.
            var service = new FakeService();
            HullMeshPresentation.Service = service;
            var root = MakeBoat();
            BoatHullSkinner.Apply(root, MakeSpriteVisual(), boat: null, SkinOptions);

            var rider = MakeRider(out _);
            rider.SetMode(ControlMode.Aboard, root.transform);
            Assert.IsFalse(service.Renderer.OccupantActive,
                "nobody has stood on the MESH hull yet — she is not even worn");

            BoatHullSkinner.Apply(root, MakeMeshVisual(), boat: null, SkinOptions);
            rider.SetMode(ControlMode.Aboard, root.transform);

            Assert.IsTrue(service.Renderer.OccupantActive,
                "the hull now worn is the one told her deck is occupied");
        }
    }
}
