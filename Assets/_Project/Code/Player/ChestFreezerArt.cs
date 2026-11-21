using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A chest freezer's baked art, and its lid</b> (owner 2026-11-17: detailed appliances with animations).
    /// The chest stands closed; walk up to it (<see cref="GameConfig.FreezerFishShowRangeMeters"/>) and the lid
    /// swings up through its frames to the open chest, whose frozen bed shows how full it is
    /// (<see cref="FreezerLayout.Rung"/>); walk away and it swings shut. While it is open the chest's front wall
    /// and rim are drawn as a NEAR layer over whatever <see cref="FreezerContentsPresenter"/> lays inside, so the
    /// fish lie in the chest and never over its front.
    ///
    /// <para><b>Everything about the look is the Def's</b> (<see cref="FreezerDef.SheetStem"/>, the lid's
    /// timing, the full share): a bigger freezer is a new Def and a new sheet. The facing cell is the
    /// placement's (<see cref="ChestFreezerSheet.FrontCell"/> for a chest backed onto a wall at the top of the
    /// screen, the one the contents opening is measured for).</para>
    ///
    /// <para>Layers sort by the decor band's Y rule: the chest one order behind its own, the fish at its own,
    /// the near layer one in front, so a figure standing in front of the chest still draws over all three.</para>
    ///
    /// <para>Presentation only: reads the freezer's count, writes nothing, no save. A frame costs a distance
    /// check and, while the lid moves, a sprite swap.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChestFreezerArt : MonoBehaviour
    {
        [Tooltip("The freezer's Def. Empty: the one on the GinnyFreezer or CamperFreezer beside it.")]
        [SerializeField] private FreezerDef _def;

        [Tooltip("The sheet cell drawn (0..7). 0 = front-on, backed onto a wall at the top of the screen; the " +
                 "contents opening is measured for that cell.")]
        [SerializeField] private int _cell = ChestFreezerSheet.FrontCell;

        [Tooltip("The chest itself.")]
        [SerializeField] private SpriteRenderer _far;

        [Tooltip("The open chest's front wall and rim, drawn over the fish inside. Hidden while it is shut.")]
        [SerializeField] private SpriteRenderer _near;

        private IHold _hold;
        private int _frame;        // 0 closed .. LidFrames.Length (open)
        private float _clock;
        private int _shownRow = -1;
        private bool _nearShown;
        private bool _sorted;

        /// <summary>The Def drawn from.</summary>
        public FreezerDef Def => _def;

        /// <summary>The facing cell drawn.</summary>
        public int Cell => _cell;

        /// <summary>True once the lid is all the way up (the fish show then).</summary>
        public bool IsOpen => _frame >= ChestFreezerSheet.LidFrames.Length;

        /// <summary>The sheet row on show now.</summary>
        public int ShownRow => _shownRow;

        /// <summary>The bed rung on show while open (Empty while shut).</summary>
        public FreezerFillRung Rung { get; private set; }

        /// <summary>The sorting order the fish inside are drawn at (between the chest and its near layer).</summary>
        public int ContentsOrder => _far != null ? _far.sortingOrder + 1 : 0;

        /// <summary>The sorting layer the fish share with the chest.</summary>
        public int SortingLayerId => _far != null ? _far.sortingLayerID : 0;

        /// <summary>Wire the piece (the prefab step, and tests).</summary>
        public void Configure(FreezerDef def, SpriteRenderer far, SpriteRenderer near, int cell = ChestFreezerSheet.FrontCell)
        {
            _def = def; _far = far; _near = near; _cell = cell;
            Init();
        }

        private void Awake() => Init();

        private void Init()
        {
            _hold = GetComponent<IHold>();
            if (_def == null)
            {
                if (TryGetComponent(out GinnyFreezer ginny)) _def = ginny.Def;
                else if (TryGetComponent(out CamperFreezer camper)) _def = camper.Def;
            }
            if (_far == null && Application.isPlaying) BuildRenderers();
            _shownRow = -1;
            _sorted = false;
        }

        // A freezer dressed at runtime (one standing in a saved scene from before the art) makes its own far
        // and near renderers, on the old square's material and layer, and hides the square. The children undo
        // the root's scale (the greybox marker was stretched), so the chest is drawn at its baked size.
        private void BuildRenderers()
        {
            var square = GetComponent<SpriteRenderer>();
            _far = NewChild("Art", square);
            _near = NewChild("ArtNear", square);
            _near.enabled = false;
            if (square != null) square.enabled = false;
        }

        private SpriteRenderer NewChild(string name, SpriteRenderer like)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            Vector3 s = transform.lossyScale;
            go.transform.localScale = new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            if (like != null) { sr.sharedMaterial = like.sharedMaterial; sr.sortingLayerID = like.sortingLayerID; }
            return sr;
        }

        private void OnEnable()
        {
            // A chest toggled back on (the room switched on again) starts shut and swings open if he is there.
            _frame = 0; _clock = 0f; _shownRow = -1;
            Show();
        }

        private void LateUpdate()
        {
            if (!_sorted) Sort();
            GameConfig config = GameServices.Config;
            Transform player = GameServices.PlayerTransform;
            bool want = config != null && player != null &&
                        FreezerContentsPresenter.Near(player.position, transform.position, config.FreezerFishShowRangeMeters);
            Step(want, Time.deltaTime);
        }

        /// <summary>Move the lid one tick toward open (<paramref name="open"/>) or shut, and show the frame.
        /// Public so tests drive it without a clock.</summary>
        public void Step(bool open, float dt)
        {
            int last = ChestFreezerSheet.LidFrames.Length;
            float per = _def != null ? Mathf.Max(0.01f, _def.LidFrameSeconds) : 0.08f;
            int target = open ? last : 0;
            if (_frame == target) _clock = 0f;
            else
            {
                _clock += Mathf.Max(0f, dt);
                while (_clock >= per && _frame != target)
                {
                    _clock -= per;
                    _frame += open ? 1 : -1;
                }
            }
            Show();
        }

        /// <summary>The row for a lid frame and a rung: the lid frames on the way, the rung's open row at the top.</summary>
        public static int RowFor(int frame, FreezerFillRung rung)
        {
            int last = ChestFreezerSheet.LidFrames.Length;
            if (frame >= last) return ChestFreezerSheet.OpenRow(rung);
            return ChestFreezerSheet.LidFrames[Mathf.Clamp(frame, 0, last - 1)];
        }

        private void Show()
        {
            int used = _hold != null ? _hold.UsedUnits : 0;
            int capacity = _hold != null ? _hold.CapacityUnits : 0;
            Rung = IsOpen ? FreezerLayout.Rung(used, capacity, _def != null ? _def.FullFromFraction : 1f) : FreezerFillRung.Empty;
            int row = RowFor(_frame, Rung);
            string stem = _def != null ? _def.SheetStem : null;
            if (row != _shownRow)
            {
                _shownRow = row;
                Sprite s = ChestFreezerSheet.SpriteAt(stem, row, _cell);
                if (_far != null && s != null) _far.sprite = s;
            }
            bool near = IsOpen;
            if (near != _nearShown || (_near != null && near && _near.sprite == null))
            {
                _nearShown = near;
                if (_near != null)
                {
                    if (near) _near.sprite = ChestFreezerSheet.SpriteAt(stem, ChestFreezerSheet.NearOpen, _cell);
                    _near.enabled = near && _near.sprite != null;
                }
            }
        }

        /// <summary>The decor band's Y rule (as every standing sprite uses): lower on screen draws in front.</summary>
        public static int OrderFor(float worldY)
            => Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        private void Sort()
        {
            _sorted = true;
            int own = OrderFor(transform.position.y);
            if (_far != null) _far.sortingOrder = own - 1;
            if (_near != null) { _near.sortingOrder = own + 1; if (_far != null) _near.sortingLayerID = _far.sortingLayerID; }
        }
    }
}
