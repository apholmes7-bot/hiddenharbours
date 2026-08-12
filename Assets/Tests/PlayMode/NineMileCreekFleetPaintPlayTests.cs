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

        /// <summary>Two owners, two schemes, one shared mesh — the property the wharf actually needs,
        /// asserted directly rather than inferred from a screenshot.</summary>
        [UnityTest]
        public IEnumerator TwoOwnersWakeInTwoDifferentPaints()
        {
            var painted = Owners().Where(o => o.HullPaint != null && o.IsPresentable()).Take(2).ToArray();
            if (painted.Length < 2) Assert.Ignore("Fewer than two painted owners in the register.");

            Wake(painted[0], "MooredA");
            yield return null;
            var first = _service.LastScheme;

            Wake(painted[1], "MooredB");
            yield return null;
            var second = _service.LastScheme;

            Assert.AreEqual(2, _service.Installs, "Both boats must have drawn.");
            Assert.AreSame(painted[0].HullPaint, first, $"'{painted[0].Id}' got the wrong table.");
            Assert.AreSame(painted[1].HullPaint, second, $"'{painted[1].Id}' got the wrong table.");
            Assert.AreNotSame(first, second,
                "Both owners were handed the same ramp table — they would lie at the wharf identical.");
            Assert.AreSame(painted[0].Boat.Visual.HullMesh, painted[1].Boat.Visual.HullMesh,
                "This fixture is only interesting while both boats share ONE mesh; they no longer do.");
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

            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
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
