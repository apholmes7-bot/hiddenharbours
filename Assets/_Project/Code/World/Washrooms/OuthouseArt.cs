using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The outhouse's baked art, its door, and him going in</b> (owner: outhouses "with openable doors and
    /// players can sit on them"). It reads its sibling <see cref="SitPoint"/> every frame and plays the
    /// <see cref="OuthouseDoorTimeline"/>: press to sit and the door swings open, stands open a beat, he is put
    /// on the bench (<see cref="FixturePoseRequested"/>, the seated pose the water closet uses) and the door
    /// swings shut; press to get up and it swings open, he is put back outside, and it shuts.
    ///
    /// <para><b>Hidden inside.</b> While he is in the booth the whole booth draws one step in front of him, so the
    /// booth covers him from the moment he steps in until he is put back outside: the open doorway shows the
    /// bench, never a figure cut by the lintel. Otherwise the booth sorts by its own pivot like any standing
    /// piece.</para>
    ///
    /// <para><b>Walls.</b> The root's polygon collider is the footprint at the drawn facing (refitted when the
    /// facing is set). It is switched off while he is inside, so the walls never shove the seated figure, and
    /// back on once he is out in front of the door.</para>
    ///
    /// <para>Timing is the <see cref="WashroomDef"/>'s (<see cref="WashroomDef.DoorFrameSeconds"/>,
    /// <see cref="WashroomDef.DoorHoldOpenSeconds"/>); the stand-off outside the door is the wash visuals'.
    /// Presentation only: nothing here is saved, and a reload finds the door shut and him standing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OuthouseArt : MonoBehaviour, IFacingCellSource
    {
        /// <summary>How many sort orders the booth draws in front of the seated figure while he is inside.</summary>
        public const int OrdersOverFigure = 2;

        [Tooltip("The washroom this booth is (its door timing).")]
        [SerializeField] private WashroomDef _def;

        [Tooltip("The sheet cell drawn (0..7). 0 = the door facing down the screen.")]
        [SerializeField] private int _cell = OuthouseSheet.FrontCell;

        [Tooltip("The booth.")]
        [SerializeField] private SpriteRenderer _art;

        [Tooltip("The walls (the root's polygon collider), fitted to the footprint at the drawn cell.")]
        [SerializeField] private PolygonCollider2D _walls;

        private readonly OuthouseDoorTimeline _timeline = new OuthouseDoorTimeline();
        private SitPoint _seat;
        private bool _posed;
        private int _shownRow = -1;
        private int _shownSort = int.MinValue;

        public WashroomDef Def => _def;
        public int Cell => _cell;
        public int FacingCell => _cell;
        public OuthouseDoorTimeline Timeline => _timeline;
        public SpriteRenderer Art => _art;
        public PolygonCollider2D Walls => _walls;

        /// <summary>Wire the booth (the builder's prefab step, and tests).</summary>
        public void Configure(WashroomDef def, SpriteRenderer art, PolygonCollider2D walls, int cell = OuthouseSheet.FrontCell)
        {
            _def = def; _art = art; _walls = walls;
            Init();
            SetCell(cell);
        }

        /// <summary>Turn the booth to a facing cell (0..7): the art and the walls follow.</summary>
        public void SetCell(int cell)
        {
            _cell = ((cell % OuthouseSheet.Facings) + OuthouseSheet.Facings) % OuthouseSheet.Facings;
            FitWalls();
            _shownRow = -1; _shownSort = int.MinValue;
            Show();
        }

        private void Awake() => Init();

        private void Init()
        {
            _seat = GetComponent<SitPoint>();
            _shownRow = -1; _shownSort = int.MinValue;
        }

        private void Start()
        {
            FitWalls();
            Show();
        }

        private void OnDisable()
        {
            if (_posed) Publish(false);
            if (_walls != null) _walls.enabled = true;
        }

        private void Update() => Step(_seat != null && _seat.IsSeatedHere, Time.deltaTime);

        /// <summary>One tick: <paramref name="seated"/> is "the seat is his". Public so tests drive it without a clock.</summary>
        public void Step(bool seated, float dt)
        {
            float per = _def != null ? _def.DoorFrameSeconds : WashroomDef.DefaultDoorFrameSeconds;
            float hold = _def != null ? _def.DoorHoldOpenSeconds : WashroomDef.DefaultDoorHoldOpenSeconds;
            _timeline.Tick(seated, dt, per, hold);
            if (_timeline.Inside != _posed) Publish(_timeline.Inside);
            Show();
        }

        // ---- the pose ------------------------------------------------------------------------------------

        private void Publish(bool on)
        {
            _posed = on;
            Vector2 entry = (Vector2)transform.position
                          + ApplianceFacing.ModelToWorldOffset(_cell, EntryProp(WashVisualsSettings.Current.EntryStandOffMetres));
            float heading = ApplianceFacing.BearingDegrees(_cell, OuthouseSheet.SeatFacing);
            string id = _seat != null ? _seat.Id : name;
            if (_walls != null) _walls.enabled = !on;
            EventBus.Publish(new FixturePoseRequested(on, FixturePose.Sit, SeatWorld(), entry, OuthouseSheet.Seat.z, heading, id));
        }

        /// <summary>Where he is put when he steps out: in front of the door, the stand-off beyond the front wall (PROP frame).</summary>
        public static Vector3 EntryProp(float standOffMetres)
            => new Vector3(OuthouseSheet.Seat.x, -OuthouseSheet.Foot.y * 0.5f - Mathf.Max(0f, standOffMetres), 0f);

        /// <summary>The seat's world point (where the presenter sits him), for the sort and the tests.</summary>
        public Vector2 SeatWorld()
            => (Vector2)transform.position + ApplianceFacing.ModelToWorldOffset(_cell, new Vector3(OuthouseSheet.Seat.x, OuthouseSheet.Seat.y, 0f));

        // ---- drawing -------------------------------------------------------------------------------------

        private void Show()
        {
            int row = _timeline.Row;
            if (row != _shownRow)
            {
                _shownRow = row;
                Sprite s = OuthouseSheet.SpriteAt(row, _cell);
                if (_art != null && s != null) _art.sprite = s;
            }
            Sort();
        }

        /// <summary>Own pivot while he is outside; in front of him while he is inside.</summary>
        private void Sort()
        {
            int key = _posed ? 1 : 0;
            if (key == _shownSort || _art == null) return;
            _shownSort = key;
            _art.sortingOrder = _posed ? ApplianceArt.OrderFor(SeatWorld().y) + OrdersOverFigure
                                       : ApplianceArt.OrderFor(transform.position.y);
        }

        private void FitWalls()
        {
            if (_walls == null) return;
            _walls.pathCount = 1;
            _walls.SetPath(0, OuthouseSheet.FootprintOutline(_cell));
        }
    }
}
