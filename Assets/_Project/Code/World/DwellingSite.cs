using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A placed mesh dwelling's site and its mesh host</b> (ADR 0060 section 6): the one component on the
    /// dwelling prefab's root that knows WHERE this copy of the home stands. The prefab is the same at every
    /// site; the scene instance carries only its position, its ground bearing and its site key.
    ///
    /// <para><b>What it does, once, at enable.</b> (1) Turns the footprint to the site's bearing
    /// (<see cref="BuildingInterior.SetFacing"/>, one facing per degree), so the inside test, the wall line
    /// and the fixtures all turn with it. (2) Lays the wall collider along the floor outline with the door
    /// gap cut (<see cref="DwellingSiteLayout.WallRun"/>), and stands each fixture child on its anchor,
    /// both read off the <see cref="DwellingMeshDef"/> and never retyped. (3) Skins the home through Core's
    /// <see cref="DwellingMeshPresentation"/> (the World module never names the Art renderer, rule 4), turns
    /// the mesh to the same bearing and writes its sort order once (<see cref="DwellingSorting.OrderAt"/>):
    /// a dwelling never moves. Then it asks the <see cref="DwellingCutaway"/> to say its last cut again to
    /// the renderer it just installed.</para>
    ///
    /// <para><b>What it does not do.</b> It decides nothing about inside (that is
    /// <see cref="BuildingInterior"/>'s), it polls nothing (no Update), and it saves nothing: the layout is
    /// recomputed from the def and the bearing (ADR 0060 section 7). The site key is the
    /// <c>HomeStoragePlan</c> building key, read by the editor step that writes the store keys into this
    /// instance's fixtures; at runtime it is carried for anyone asking which home this is.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingInterior))]
    [RequireComponent(typeof(DwellingCutaway))]
    public sealed class DwellingSite : MonoBehaviour
    {
        /// <summary>One fixture child, joined to the def's anchor by its id.</summary>
        [Serializable]
        public struct AnchorPoint
        {
            [Tooltip("The def's anchor id ('freezer', 'bunk'): where this child stands is read off it.")]
            public string AnchorId;

            [Tooltip("The child that carries the fixture's component.")]
            public Transform Point;

            public AnchorPoint(string anchorId, Transform point)
            {
                AnchorId = anchorId;
                Point = point;
            }
        }

        [Tooltip("The site's key: the HomeStoragePlan building key ('ginny_lot_camper'). Two homes placed " +
                 "from one prefab carry two keys, so their stores are two save rows. Scene data.")]
        [SerializeField] private string _siteKey = "";

        [Tooltip("The compass heading of the dwelling's nose (rig +y), whole degrees, 0 = north, clockwise " +
                 "(ADR 0034). Scene data. The footprint, the walls, the fixtures and the mesh all turn by it.")]
        [SerializeField, Range(0, DwellingSiteLayout.FacingsPerTurn - 1)] private int _groundBearingDegrees;

        [Tooltip("The wall line along the floor outline, open at the door. Written by Layout; the builder " +
                 "makes the child.")]
        [SerializeField] private EdgeCollider2D _walls;

        [Tooltip("One row per fixture child. Written by the dwelling prefab builder.")]
        [SerializeField] private AnchorPoint[] _anchors = Array.Empty<AnchorPoint>();

        private BuildingInterior _interior;
        private DwellingCutaway _cutaway;

        /// <summary>The site's key (the HomeStoragePlan building key).</summary>
        public string SiteKey => _siteKey;

        /// <summary>The compass heading of the dwelling's nose, whole degrees in [0, 360).</summary>
        public int GroundBearingDegrees => _groundBearingDegrees;

        /// <summary>The wall collider, or null.</summary>
        public EdgeCollider2D Walls => _walls;

        /// <summary>The fixture children. Read by tests and by the editor's site step.</summary>
        public AnchorPoint[] Anchors => _anchors;

        /// <summary>The renderer the last <see cref="Skin"/> installed, or null (none installed, or no mesh
        /// path in this context, as in EditMode).</summary>
        public IHullMeshRenderer Renderer { get; private set; }

        /// <summary>The order the last <see cref="Skin"/> wrote, or <see cref="int.MinValue"/> before one.</summary>
        public int SortingOrder { get; private set; } = int.MinValue;

        /// <summary>Wire the prefab's parts (the builder, and tests).</summary>
        public void Configure(EdgeCollider2D walls, AnchorPoint[] anchors)
        {
            _walls = walls;
            _anchors = anchors ?? Array.Empty<AnchorPoint>();
        }

        /// <summary>Set the site (the editor's site step, and tests): its key and its ground bearing. The
        /// bearing is folded into [0, 360). Lays out again so the instance shows where it now stands.</summary>
        public void SetSite(string siteKey, int groundBearingDegrees)
        {
            _siteKey = siteKey ?? "";
            _groundBearingDegrees = DwellingSiteLayout.NormalizeBearing(groundBearingDegrees);
            Layout();
        }

        private void OnEnable()
        {
            // A site added before its building was made a dwelling (AddComponent order) waits for the
            // builder's explicit Layout rather than logging a refusal.
            if (Interior().DwellingMesh == null) return;
            Layout();
            // In play only: an edit-time builder or refresh step must never serialise a renderer into the
            // prefab or the scene (the presentation locator's rule).
            if (Application.isPlaying) Skin();
        }

        /// <summary>
        /// Turn the footprint, lay the wall line and stand the fixtures, from the def and the bearing. False,
        /// with a logged reason, when the building has no usable dwelling def (nothing is moved). Public so
        /// the builder and EditMode tests run the exact production path.
        /// </summary>
        public bool Layout()
        {
            BuildingInterior interior = Interior();
            DwellingMeshDef def = interior.DwellingMesh;
            if (def == null)
            {
                Debug.LogError($"[DwellingSite] '{name}' has no dwelling mesh on its BuildingInterior; " +
                               "nothing laid out.", this);
                return false;
            }

            interior.SetFacing(DwellingSiteLayout.NormalizeBearing(_groundBearingDegrees), DwellingSiteLayout.FacingsPerTurn);

            if (_walls != null)
            {
                InteriorFootprint ground = interior.FootprintFor(InteriorLevelLayout.GroundLevel);
                // The gap is the published clear width PLUS the collider's rounded ends, so the daylight
                // between them is the door the art draws.
                float gap = def.Threshold.ClearWidthMeters + 2f * interior.WallThicknessMetres;
                Vector2[] run = ground.HasFloorOutline
                    ? DwellingSiteLayout.WallRun(ground.FloorOutline, DwellingInteriorRules.DoorModel(def), gap)
                    : Array.Empty<Vector2>();
                Vector2 wallsAt = _walls.transform.position;
                var points = new Vector2[run.Length];
                for (int i = 0; i < run.Length; i++) points[i] = ground.ModelToWorld(run[i]) - wallsAt;
                if (points.Length >= 2) _walls.points = points;
                _walls.edgeRadius = interior.WallThicknessMetres;
                _walls.enabled = points.Length >= 2;
            }

            for (int i = 0; i < _anchors.Length; i++)
            {
                Transform point = _anchors[i].Point;
                if (point == null || !def.TryGetAnchor(_anchors[i].AnchorId, out DwellingFixtureAnchor anchor)) continue;
                int storey = def.TryGetLevel(anchor.LevelId, out DwellingLevel level) ? level.Storey : 0;
                Vector2 at = interior.FootprintFor(storey)
                                     .ModelToWorld(new Vector2(anchor.PositionMeters.x, anchor.PositionMeters.y));
                point.position = new Vector3(at.x, at.y, point.position.z);
            }
            return true;
        }

        /// <summary>
        /// Install the mesh through Core's seam, turn it to the site's bearing, write its order once, and
        /// have the cutaway say its last cut to it. Returns the renderer, or null when there is no service
        /// (EditMode, a tool context) or the service refused the def (logged by the service).
        /// </summary>
        public IHullMeshRenderer Skin()
        {
            BuildingInterior interior = Interior();
            DwellingMeshDef def = interior.DwellingMesh;
            IDwellingMeshPresentationService service = DwellingMeshPresentation.Service;
            if (def == null || service == null) return Renderer = null;

            IHullMeshRenderer renderer = service.Install(gameObject, def);
            Renderer = renderer;
            if (renderer == null) return null;

            renderer.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(
                DwellingSiteLayout.NormalizeBearing(_groundBearingDegrees), def.ZeroHeadingDegrees, def.AzimuthCounterClockwise);

            DwellingCutaway cutaway = Cutaway();
            SortingOrder = cutaway.DwellingOrder;
            renderer.SetSorting(SortingLayer.NameToID(DefaultSortingLayer), SortingOrder);
            cutaway.Reassert();
            return renderer;
        }

        /// <summary>The sorting layer every upright world object shares (the decor band lives on it).</summary>
        public const string DefaultSortingLayer = "Default";

        private BuildingInterior Interior()
        {
            if (_interior == null) _interior = GetComponent<BuildingInterior>();
            return _interior;
        }

        private DwellingCutaway Cutaway()
        {
            if (_cutaway == null) _cutaway = GetComponent<DwellingCutaway>();
            return _cutaway;
        }
    }
}
