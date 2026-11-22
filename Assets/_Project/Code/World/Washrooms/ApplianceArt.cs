using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A bathroom or kitchen fixture's baked art, facing the right way and moving with the wash</b> (owner ask
    /// 2026-11-17: sinks, toilets and baths read as boxes; they should look like what they are from every wall,
    /// fill with water, let the player climb in, and froth while he washes).
    ///
    /// <para><b>Facing.</b> The piece works out which wall of its room it backs onto from where it stands (see
    /// <see cref="ApplianceFacing.PickWallOffset"/>), so moving it to another wall needs nothing else. A placement
    /// that knows better can say so through <see cref="SetWall"/> or the inspector's override.</para>
    ///
    /// <para><b>State.</b> It reads its sibling <see cref="WashStationPoint"/> (washing here?) or
    /// <see cref="SitPoint"/> (sitting here?) every frame, and the soap flag off <see cref="PlayerWashChanged"/>
    /// at the wash's edges. <see cref="WashFixtureTimeline"/> turns that into a sheet row. While the player is in
    /// the fixture it asks his figure into the pose with <see cref="FixturePoseRequested"/> and draws its NEAR
    /// layer (the bath's front rim, the shower's curb) one step in front of him.</para>
    ///
    /// <para>Presentation only. Nothing here is saved; nothing here changes the wash or the seat.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplianceArt : MonoBehaviour, IFacingCellSource
    {
        [Tooltip("Which piece this is: picks its sheet, its states and its seat.")]
        [SerializeField] private ApplianceKind _kind;

        [Tooltip("The wall it backs onto, as a turn from the room (0 = the +y wall, 2 = +x, 4 = −y, 6 = −x). " +
                 "−1 (shipped): worked out from where it stands in its room.")]
        [SerializeField] private int _wallOffset = -1;

        [Tooltip("A sheet cell to show regardless of any room (0..7), for a piece outside any building (the " +
                 "wharf shower). −1: use the room's facing and the wall.")]
        [SerializeField] private int _cellOverride = -1;

        [Tooltip("The whole piece.")]
        [SerializeField] private SpriteRenderer _far;

        [Tooltip("The piece's near layer, drawn in front of the figure while he is in it. Hidden otherwise.")]
        [SerializeField] private SpriteRenderer _near;

        [Tooltip("Optional solid box fitted to the piece's footprint at its facing once it knows which wall it " +
                 "stands on, so he walks round it and reaches it from the front. Empty: no fitting.")]
        [SerializeField] private BoxCollider2D _footprint;

        private static readonly Dictionary<string, Sprite[]> Sheets = new Dictionary<string, Sprite[]>();

        private ApplianceLayout _layout;
        private WashFixtureTimeline _timeline;
        private WashStationPoint _wash;
        private SitPoint _seat;
        private Sprite[] _sprites;
        private int _cell;
        private bool _resolved;
        private bool _posed;
        private bool _soaped;
        private bool _doorOpen;
        private int _shownRow = -1, _shownNear = -2;
        private bool _layered;

        public ApplianceKind Kind => _kind;

        /// <summary>The sheet cell the piece is showing (0..7).</summary>
        public int Cell => _cell;

        /// <summary>The cell, worked out first if the piece has not drawn yet (something placed on it asks).</summary>
        public int FacingCell
        {
            get
            {
                if (!_resolved) ResolveCell();
                return _cell;
            }
        }

        /// <summary>The timeline, for tests and the look probe.</summary>
        public WashFixtureTimeline Timeline => _timeline;

        /// <summary>Back the piece onto a wall (a turn from <see cref="ApplianceFacing.WallOffsets"/>), or −1 to
        /// work it out again from where it stands.</summary>
        public void SetWall(int wallOffset)
        {
            _wallOffset = wallOffset;
            _resolved = false;
            ResolveCell();
            FitFootprint();
            Show(_timeline != null ? _timeline.Row : 0, -1);
            if (!_layered) SortIdle();   // the front line turns with the footprint
        }

        /// <summary>Wire the piece (the baker's prefab step, and tests).</summary>
        public void Configure(ApplianceKind kind, SpriteRenderer far, SpriteRenderer near)
        {
            _kind = kind; _far = far; _near = near;
            Init();
        }

        private void Awake() => Init();

        private void Init()
        {
            _layout = ApplianceSheets.For(_kind);
            _timeline = new WashFixtureTimeline(_layout);
            _wash = GetComponent<WashStationPoint>();
            _seat = GetComponent<SitPoint>();
            _sprites = SheetFor(_layout);
            _shownRow = -1; _shownNear = -2; _resolved = false;
        }

        private void OnEnable() => EventBus.Subscribe<PlayerWashChanged>(OnWashChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerWashChanged>(OnWashChanged);
            if (_posed) Publish(false);
        }

        private void Start()
        {
            Settle();
            // A piece with nothing to animate is done: stop the per-frame dispatch entirely (rule 7).
            // A door piece wakes again when it is asked open (SetDoorOpen).
            if (!_layout.Fills && !_layout.Runs && !_layout.Flushes && !(_layout.HasDoor && _doorOpen)) enabled = false;
        }

        /// <summary>Work out the wall, fit the footprint box and sort the idle piece, as <c>Start</c> does (tests and
        /// tools call it on a piece that has not started).</summary>
        public void Settle()
        {
            if (_timeline == null) Init();
            ResolveCell();
            EnsureStop();
            FitFootprint();
            Show(_layout.Idle, -1);
            SortIdle();
        }

        private void OnWashChanged(PlayerWashChanged e) => _soaped = WashSoapSignal.IsSoaped(e);

        private void Update()
        {
            if (!_resolved) ResolveCell();
            WashVisualsSettings s = WashVisualsSettings.Current;

            bool active = _layout.Flushes
                ? _seat != null && _seat.IsSeatedHere
                : _layout.HasDoor ? _doorOpen
                : _wash != null && _wash.IsWashingHere;
            bool froth = active && WashVisualsSettings.Froths(_soaped, s);

            _timeline.Tick(active, froth, Time.deltaTime, s);

            if (_timeline.PoseOn != _posed) Publish(_timeline.PoseOn);
            Show(_timeline.Row, _timeline.NearRow);

            // A shut door has nothing more to draw: sleep until it is asked open again.
            if (_layout.HasDoor && !_doorOpen && _timeline.Current == WashFixtureTimeline.Phase.Idle) enabled = false;
        }

        /// <summary>Whether the piece's door (the fridge's) is being held open.</summary>
        public bool DoorOpen => _doorOpen;

        /// <summary>Ask the piece's door open or shut; it swings over <see cref="WashVisualsSettings.DoorSeconds"/>.
        /// A no-op on a piece with no door. Presentation only: nothing is stored or saved by opening it.</summary>
        public void SetDoorOpen(bool open)
        {
            _doorOpen = open;
            if (_layout.HasDoor) enabled = true;
        }

        /// <summary>The solid footprint box, if the piece has one.</summary>
        public BoxCollider2D Footprint => _footprint;

        /// <summary>Fit the footprint box to the footprint at the resolved facing (Start does it once).</summary>
        public void FitFootprint()
        {
            if (_footprint == null) return;
            Rect r = ApplianceFacing.FootprintBox(_layout, _cell);
            Vector2 c = StopCentre(r, WashVisualsSettings.Current.FixtureStopDropMetres);
            Vector3 s = _footprint.transform.lossyScale;
            _footprint.offset = new Vector2(c.x / Mathf.Max(1e-4f, s.x), c.y / Mathf.Max(1e-4f, s.y));
            _footprint.size = new Vector2(r.width / Mathf.Max(1e-4f, s.x), r.height / Mathf.Max(1e-4f, s.y));
        }

        // ---- facing ----------------------------------------------------------------------------------------

        private void ResolveCell()
        {
            _resolved = true;
            if (_cellOverride >= 0) { _cell = _cellOverride % ApplianceSheets.Facings; return; }

            BuildingInterior building = GetComponentInParent<BuildingInterior>(true);
            if (building == null) { _cell = 0; return; }

            InteriorFootprint fp = FootprintHolding(building, transform.position);
            int wall = _wallOffset >= 0
                ? _wallOffset
                : ApplianceFacing.PickWallOffset(fp.WorldToModel(transform.position), fp.WidthMetres,
                                                 fp.LengthMetres, _layout.Foot);
            _cell = ApplianceFacing.CellFor(fp.Facing, wall, fp.Facings);
        }

        /// <summary>The storey's footprint the point stands in (the ground floor's when none claims it).</summary>
        private static InteriorFootprint FootprintHolding(BuildingInterior building, Vector2 world)
        {
            for (int level = building.TopLevel; level > 0; level--)
            {
                InteriorFootprint upper = building.FootprintFor(level);
                if (upper.Contains(world, 0f)) return upper;
            }
            return building.FootprintFor(0);
        }

        // ---- the pose --------------------------------------------------------------------------------------

        private void Publish(bool on)
        {
            _posed = on;
            WashVisualsSettings s = WashVisualsSettings.Current;
            Vector2 pivot = transform.position;
            Vector2 seat = pivot + ApplianceFacing.ModelToWorldOffset(_cell, new Vector3(_layout.Seat.x, _layout.Seat.y, 0f));
            Vector2 entry = pivot + ApplianceFacing.ModelToWorldOffset(_cell, ApplianceFacing.EntryProp(_layout, s.EntryStandOffMetres));
            float heading = ApplianceFacing.BearingDegrees(_cell, _layout.SeatFacing);
            string id = _wash != null ? _wash.Id : _seat != null ? _seat.Id : name;

            EventBus.Publish(new FixturePoseRequested(on, _layout.Pose, seat, entry, _layout.Seat.z, heading, id));
            SortFor(on ? seat.y : (_near != null && _near.enabled ? seat.y : (float?)null));
        }

        // ---- drawing ---------------------------------------------------------------------------------------

        private void Show(int row, int nearRow)
        {
            if (row == _shownRow && nearRow == _shownNear) return;
            _shownRow = row; _shownNear = nearRow;

            if (_far != null) _far.sprite = SpriteAt(row);
            if (_near != null)
            {
                bool on = nearRow >= 0;
                _near.enabled = on;
                if (on) _near.sprite = SpriteAt(nearRow);
            }

            // The near layer stays in front of the figure through the climb out, after the pose has ended.
            bool layered = _posed || nearRow >= 0;
            if (layered != _layered) SortFor(layered ? SeatWorldY() : (float?)null);
        }

        private float SeatWorldY()
            => transform.position.y + ApplianceFacing.ModelToWorldOffset(_cell, new Vector3(_layout.Seat.x, _layout.Seat.y, 0f)).y;

        private Sprite SpriteAt(int row)
        {
            if (_sprites == null) return null;
            int i = row * ApplianceSheets.Facings + _cell;
            return i >= 0 && i < _sprites.Length ? _sprites[i] : null;
        }

        private void SortIdle() => SortFor(null);

        /// <summary>Sort the idle piece at its front floor line (<see cref="FrontLineY"/>), or (while he is in it)
        /// just behind and just in front of the figure stood at <paramref name="seatY"/>, using the same Y → order
        /// mapping every decor sprite uses.</summary>
        private void SortFor(float? seatY)
        {
            _layered = seatY.HasValue;
            if (seatY.HasValue)
            {
                int figure = OrderFor(seatY.Value);
                if (_far != null) _far.sortingOrder = figure - 1;
                if (_near != null) _near.sortingOrder = figure + 2;
            }
            else
            {
                int own = OrderFor(FrontLineY);
                if (_far != null) _far.sortingOrder = own;
                if (_near != null) _near.sortingOrder = own + 1;
            }
            // While he is in it (seated, bathing, climbing out) the pose holds him where the stop stands in his
            // foot frame; stand it down until he is out.
            if (_footprint != null) _footprint.enabled = !_layered;
        }

        // ---- the front line and the stop -------------------------------------------------------------------
        //
        // A walker is sorted by his drawn feet (his transform), but the solid circle that stops him sits BELOW
        // them (the player's foot circle: FixtureStopDropMetres down). A stop drawn on the art itself therefore
        // let his drawn feet walk that far into the piece: walking north into a toilet he stopped 0.13 to 0.16 m
        // north of its pivot, past its front, and a piece sorted at its pivot hid him. So the idle piece sorts at
        // its front floor line (the south edge of its footprint), and its solid stop is its footprint drawn in
        // his foot circle's frame (the same box, dropped by FixtureStopDropMetres): his drawn feet meet the art's
        // edge from every side, a foot radius out, never inside it.

        private static readonly List<Collider2D> SolidScratch = new List<Collider2D>();

        /// <summary>The idle piece's sort line (world Y): its front floor line, the south edge of its footprint
        /// at its facing.</summary>
        public float FrontLineY => transform.position.y + ApplianceFacing.FootprintBox(_layout, _cell).yMin;

        /// <summary>The footprint box's offset from the pivot as the stop (pure): the footprint's centre dropped
        /// by <paramref name="dropMetres"/>, so a walker whose foot circle sits that far below his drawn feet
        /// meets it with his drawn feet on the art's edge.</summary>
        public static Vector2 StopCentre(Rect footprint, float dropMetres)
            => new Vector2(footprint.center.x, footprint.center.y - Mathf.Max(0f, dropMetres));

        /// <summary>Make the footprint box the piece's one solid stop. A piece he steps into
        /// (<see cref="FixturePose.Stand"/>: the showers) keeps whatever it was built with. Any other piece built
        /// solid (a hand-placed circle, the shipped washroom prefabs) has those colliders stood down and gets the
        /// fitted box instead, so every piece stops him the same way. A piece built with nothing solid stays
        /// walk-through.</summary>
        private void EnsureStop()
        {
            if (_layout.Pose == FixturePose.Stand) return;
            bool solid = _footprint != null;
            GetComponentsInChildren(false, SolidScratch);
            for (int i = 0; i < SolidScratch.Count; i++)
            {
                Collider2D c = SolidScratch[i];
                if (c == null || c == _footprint || c.isTrigger || !c.enabled) continue;
                c.enabled = false;
                solid = true;
            }
            SolidScratch.Clear();
            if (solid && _footprint == null) _footprint = gameObject.AddComponent<BoxCollider2D>();
        }

        /// <summary>The decor band's Y → sorting order (lower on screen draws in front).</summary>
        public static int OrderFor(float worldY)
            => Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        // ---- sheets ----------------------------------------------------------------------------------------

        /// <summary>A sheet's sprites, indexed <c>row × facings + cell</c>, loaded once per stem.</summary>
        public static Sprite[] SheetFor(in ApplianceLayout layout)
        {
            if (Sheets.TryGetValue(layout.Stem, out Sprite[] cached)) return cached;

            var sprites = new Sprite[layout.Rows * ApplianceSheets.Facings];
            Sprite[] loaded = Resources.LoadAll<Sprite>(ApplianceSheets.ResourcesFolder + "/" + layout.Stem);
            for (int i = 0; i < loaded.Length; i++)
            {
                if (TryParse(loaded[i].name, layout.Stem, out int row, out int cell)
                    && row < layout.Rows && cell < ApplianceSheets.Facings)
                    sprites[row * ApplianceSheets.Facings + cell] = loaded[i];
            }
            Sheets[layout.Stem] = sprites;
            return sprites;
        }

        /// <summary>Parse <c>&lt;stem&gt;_s&lt;row&gt;_d&lt;cell&gt;</c>.</summary>
        public static bool TryParse(string spriteName, string stem, out int row, out int cell)
        {
            row = cell = -1;
            if (spriteName == null || !spriteName.StartsWith(stem + "_s")) return false;
            string rest = spriteName.Substring(stem.Length + 2);
            int d = rest.IndexOf("_d", System.StringComparison.Ordinal);
            if (d <= 0) return false;
            return int.TryParse(rest.Substring(0, d), out row) && int.TryParse(rest.Substring(d + 2), out cell)
                   && row >= 0 && cell >= 0;
        }
    }
}
