using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Does the register's paint survive the WAKE?</b> — the half of the fleet-paint contract that
    /// EditMode cannot judge.
    ///
    /// <para><b>Why this is not in the EditMode fixture beside its siblings.</b>
    /// <see cref="MooredBoat"/> draws herself in <c>OnEnable</c> and nowhere else; her owner is a
    /// <c>[SerializeField]</c> and the builder's contract is "wire her before she wakes", so the draw
    /// that matters is the one at SCENE LOAD. That lifecycle does not run in an EditMode fixture —
    /// measured, not assumed: built inactive-then-activated and built-then-enable-toggled, and in
    /// both the component logged nothing and installed nothing. An EditMode assertion here would
    /// therefore have been a test that passes because nothing happened, which is worse than no test.
    /// The four DATA claims stay in <c>NineMileCreekFleetPaintTests</c>; only the two that need a
    /// running lifecycle live here.</para>
    ///
    /// <para>Headless: the renderer behind the Core seam is a double, so no GPU is involved.</para>
    /// </summary>
    public class NineMileCreekFleetPaintPlayTests
    {
        const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";

        readonly List<Object> _spawned = new List<Object>();
        IHullMeshPresentationService _previousService;
        RecordingService _service;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            HullMeshPresentation.Service = _service = new RecordingService();
        }

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        static BoatOwnerDef[] Owners()
        {
#if UNITY_EDITOR
            return AssetDatabase
                .FindAssets($"t:{nameof(BoatOwnerDef)}", new[] { OwnersFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BoatOwnerDef>)
                .Where(o => o != null)
                .OrderBy(o => o.Id)
                .ToArray();
#else
            return System.Array.Empty<BoatOwnerDef>();
#endif
        }

        /// <summary>Wire her, then wake her — the builder's own order, which is the whole point.</summary>
        MooredBoat Wake(BoatOwnerDef owner, string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.SetActive(false);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, headingDegrees: 40f);
            go.SetActive(true);
            return moored;
        }

        /// <summary>
        /// ⚠️ THE ONE THAT MATTERS. Everything else about fleet paint is data sitting in an asset;
        /// this is the only assertion that shows the data reaching the thing that draws. Break
        /// <see cref="MooredBoat"/>'s <c>PaintScheme</c> wiring and this goes red.
        /// </summary>
        [UnityTest]
        public IEnumerator MooredBoatHandsTheOwnersPaintToThePresentationSeam()
        {
            var painted = Owners().FirstOrDefault(o => o.HullPaint != null && o.IsPresentable());
            if (painted == null) Assert.Ignore("No painted owner in the register (editor-only fixture).");

            Wake(painted, "MooredPaintProbe");
            yield return null;

            Assert.AreEqual(1, _service.Installs,
                "MooredBoat installed no mesh hull, so the wiring cannot be judged — this assertion " +
                "must never be allowed to pass by nothing happening.");
            Assert.AreSame(painted.HullPaint, _service.LastScheme,
                $"'{painted.Id}' wears '{painted.HullPaint.Id}' in her def, but the presentation seam " +
                "was handed " + (_service.LastScheme == null ? "NOTHING" : $"'{_service.LastScheme.Id}'") +
                ". The register's colour never reaches the water: the wharf would re-build with the " +
                "whole fleet in one white gelcoat.");
        }

        /// <summary>The other direction — and it asserts the install happened FIRST, so it cannot pass
        /// by drawing nothing at all.</summary>
        [UnityTest]
        public IEnumerator AnUnpaintedOwnerStillDrawsButHandsNoScheme()
        {
            var plain = Owners().FirstOrDefault(
                o => o.HullPaint == null && o.IsPresentable() && o.Boat.Visual.HasHullMesh());
            if (plain == null) Assert.Ignore("Every mesh-hulled owner in the register is painted.");

            Wake(plain, "MooredPlainProbe");
            yield return null;

            Assert.AreEqual(1, _service.Installs,
                "The unpainted owner drew nothing, so 'no scheme was handed over' proves nothing.");
            Assert.IsNull(_service.LastScheme,
                $"'{plain.Id}' wears no paint but the seam was handed '{_service.LastScheme?.Id}'.");
        }

        /// <summary>
        /// Two owners, two schemes, one shared mesh — the property the wharf actually needs, asserted
        /// directly rather than inferred from a screenshot.
        ///
        /// <para><b>⚠️ The pair is now SELECTED for a shared mesh instead of assumed to have one.</b>
        /// This test used to take the first two painted owners by id and close by asserting they
        /// shared a hull — guaranteed only while every painted boat in the register was a lobster
        /// boat, which stopped being true when the punt gained an owner.
        ///
        /// <para>It is NOT red today, and only by luck of the alphabet: the two lowest painted ids
        /// (<c>arsenault_leo</c>, <c>campbell_hughie</c>) are both lobster boats. It DID go red
        /// mid-branch, when a second small-craft owner was briefly in the register, which is how the
        /// latent assumption surfaced. Selecting the pair states the premise instead of relying on
        /// it, and the cross-hull case this can no longer cover gets its own test below.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TwoOwnersWakeInTwoDifferentPaints()
        {
            var pair = Owners()
                .Where(o => o.HullPaint != null && o.IsPresentable() && o.Boat.Visual.HullMesh != null)
                .GroupBy(o => o.Boat.Visual.HullMesh)
                .FirstOrDefault(grp => grp.Count() >= 2)
                ?.Take(2).ToArray();
            if (pair == null)
                Assert.Ignore("No two painted owners in the register share a hull mesh.");

            Wake(pair[0], "MooredA");
            yield return null;
            var first = _service.LastScheme;

            Wake(pair[1], "MooredB");
            yield return null;
            var second = _service.LastScheme;

            Assert.AreEqual(2, _service.Installs, "Both boats must have drawn.");
            Assert.AreSame(pair[0].HullPaint, first, $"'{pair[0].Id}' got the wrong table.");
            Assert.AreSame(pair[1].HullPaint, second, $"'{pair[1].Id}' got the wrong table.");
            Assert.AreNotSame(first, second,
                "Both owners were handed the same ramp table — they would lie at the wharf identical.");
            Assert.AreSame(pair[0].Boat.Visual.HullMesh, pair[1].Boat.Visual.HullMesh,
                "The pair was selected for a shared mesh, so this cannot fail without the selection " +
                "above being wrong.");
        }

        /// <summary>
        /// The other half, and the one the small-craft drop makes possible: two owners on DIFFERENT
        /// meshes each get their own table, and neither table would be accepted by the other's hull.
        ///
        /// <para>Worth its own test because the failure mode is specific and silent. A scheme is a ramp
        /// table matched to a hull BY INDEX, and the punt and the lobster boat both have exactly ELEVEN
        /// materials — so a mix-up between those two passes every arity check the renderer makes and
        /// simply draws the wrong colours on the wrong panels. With one painted hull in the register
        /// that could not happen; with three it can, and only the hull id stands in the way.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OwnersOnDifferentHullsEachGetTheirOwnTable()
        {
            var byMesh = Owners()
                .Where(o => o.HullPaint != null && o.IsPresentable() && o.Boat.Visual.HullMesh != null)
                .GroupBy(o => o.Boat.Visual.HullMesh)
                .Select(grp => grp.First())
                .Take(2).ToArray();
            if (byMesh.Length < 2)
                Assert.Ignore("The register's painted boats all come off one hull mesh.");

            Wake(byMesh[0], "MooredHullA");
            yield return null;
            var first = _service.LastScheme;

            Wake(byMesh[1], "MooredHullB");
            yield return null;
            var second = _service.LastScheme;

            Assert.AreEqual(2, _service.Installs, "Both boats must have drawn.");
            Assert.AreNotSame(byMesh[0].Boat.Visual.HullMesh, byMesh[1].Boat.Visual.HullMesh,
                "These two were selected off different meshes.");
            Assert.AreSame(byMesh[0].HullPaint, first,
                $"'{byMesh[0].Id}' ({byMesh[0].Boat.Visual.HullMesh.Id}) got the wrong table.");
            Assert.AreSame(byMesh[1].HullPaint, second,
                $"'{byMesh[1].Id}' ({byMesh[1].Boat.Visual.HullMesh.Id}) got the wrong table.");

            Assert.IsTrue(first.IsUsableFor(byMesh[0].Boat.Visual.HullMesh),
                $"'{first.Id}' cannot repaint its own hull: " +
                first.ExplainUnusableFor(byMesh[0].Boat.Visual.HullMesh));
            Assert.IsFalse(first.IsUsableFor(byMesh[1].Boat.Visual.HullMesh),
                $"'{first.Id}' was baked for '{byMesh[0].Boat.Visual.HullMesh.Id}' but is accepted by " +
                $"'{byMesh[1].Boat.Visual.HullMesh.Id}'. Ramps match by INDEX and these two hulls can " +
                "have equal material counts, so this is the check that stops a silent miscolour.");
        }

        // ---- doubles ------------------------------------------------------------------------------

        sealed class RecordingService : IHullMeshPresentationService
        {
            public int Installs;
            public HullPaintSchemeDef LastScheme;
            readonly FakeRenderer _renderer = new FakeRenderer();

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def,
                                             HullPaintSchemeDef scheme = null)
            {
                Installs++;
                LastScheme = scheme;
                return _renderer;
            }

            /// <summary>
            /// ⚠️ Returns a renderer, not null — null is the REFUSAL contract, and this double is not
            /// refusing anything.
            ///
            /// <para>It used to return null and no test noticed, because the only painted hull in the
            /// register was the lobster boat and she carries no articulated fitting. Dan Peters's punt
            /// does: <c>BoatHullSkinner</c> asks for her outboard, reads the null as "the presentation
            /// service refused it", and logs <c>[Error] Visual 'visual.punt_iso_basic' is a MESH hull
            /// whose outboard could not be attached … Removing the engine</c> — which fails the test on
            /// an unhandled error message. The defect was in the double, not the boat: the real service
            /// attaches fittings, so the stand-in must too. Silencing the log instead would have
            /// suppressed a genuine error channel these fixtures rely on.</para>
            /// </summary>
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
            {
                PropsAttached++;
                return _prop;
            }

            public int PropsAttached;
            readonly FakePropRenderer _prop = new FakePropRenderer();

            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        sealed class FakePropRenderer : IHullPropRenderer
        {
            public Quaternion LocalRotation { get; set; } = Quaternion.identity;
            public float LateralOffsetMeters { get; set; }
            public Vector3 FitmentOffsetMeters { get; set; }
            public bool Visible { get; set; } = true;
            public bool IsConfigured => true;
        }

        sealed class FakeRenderer : IHullMeshRenderer, IDeckOccupantSlots
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int layerId, int order) { }

            public Vector3 OccupantRigMeters { get; private set; }
            public bool OccupantActive { get; private set; }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active)
            { OccupantRigMeters = rigLocalMeters; OccupantActive = active; }
            public float DeckOccluderId => 7f / 255f;

            public IDeckOccupantSlots DeckOccupants => this;
            public int Capacity => 1;
            public int ActiveCount => OccupantActive ? 1 : 0;
            public int Claim(object owner) => 0;
            public void Release(int slot, object owner) => OccupantActive = false;
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active)
                => SetDeckOccupant(rigLocalMeters, active);
            public float OccluderId(int slot) => OccupantActive ? DeckOccluderId : 0f;
            public float OccluderIdTop => DeckOccluderId;
        }
    }
}
