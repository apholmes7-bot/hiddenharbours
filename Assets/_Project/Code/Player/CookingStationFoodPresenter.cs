using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fish being cooked, on the pan or the board</b> (fish art pass 2, plan 4b): while a station has
    /// a job on it, the fish the job started from is drawn at the station's food anchor
    /// (<see cref="CookingStationDef.FoodAnchorPropMeters"/>), turned with the wall the station backs onto.
    /// On a browning station (<see cref="CookingStationDef.BrownsAsItCooks"/>) it is tinted along the one
    /// cooked colour ramp (<see cref="GameConfig.CookedFishRamp"/>) as the job runs, so a cod on the stove
    /// goes from raw to golden without a sheet per species.
    ///
    /// <para><b>Which fish.</b> A job names only its recipe. A recipe that takes a species names it; a
    /// recipe that takes an intermediate (a fillet) is followed up the recipe book to the recipe that made
    /// it, and so to the species (<see cref="SpeciesOf"/>). A recipe that takes a whole category draws the
    /// library's fallback kind.</para>
    ///
    /// <para><b>What shape.</b> A whole fish (a recipe that takes the species) is its own deck-lay sprite. A
    /// fillet (a recipe that takes an intermediate) is never drawn as the whole fish: it is a plain fillet
    /// shape, <see cref="GameConfig.FilletLengthOfFish"/> of that fish's sprite, in the fillet colours.</para>
    ///
    /// <para><b>What size.</b> The weight of the catch taken from his hands comes with
    /// <see cref="FoodCookStarted.InputWeightKg"/>, and picks the size rung as it does in his hands. A job
    /// started from the locker, or one loaded from a save, carries no weight: the middle rung is drawn.</para>
    ///
    /// <para><b>Where.</b> The anchor is a point in the station's own frame. The station's art says which of
    /// the eight facings it shows (<see cref="IFacingCellSource"/>, through Core), and the point is turned and
    /// projected with it, so a pan on a stove against the north wall and one against the east wall each sit
    /// on the cooktop.</para>
    ///
    /// <para>Presentation only: reads the runner through Core, writes nothing (no save change). The
    /// species is resolved when the job changes; a frame while cooking costs one lookup and, on a browning
    /// station, one colour write when the colour moves. Nothing allocates per frame.</para>
    /// </summary>
    [RequireComponent(typeof(CookingStationFixture))]
    public sealed class CookingStationFoodPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("The catch art table the fish is drawn from. Empty = the shared one.")]
        private CatchItemLibrary _art;

        [SerializeField, Tooltip("Sorting orders above the station's own art the food draws at.")]
        private int _sortingAbove = 1;

        /// <summary>How far up the recipe book a fillet is followed to find its fish.</summary>
        public const int MaxChainDepth = 8;

        /// <summary>The facings a station's sheet has.</summary>
        public const int Facings = 8;

        private CookingStationFixture _fixture;
        private SpriteRenderer _food;
        private IFacingCellSource _facing;
        private readonly List<SpriteRenderer> _stationArt = new List<SpriteRenderer>();
        private RecipeDef _shownRecipe;
        private double _shownReadyAt = double.NaN;
        private int _shownCell = -1;
        private float _cookSeconds;
        private float _startedKg;
        private double _startedReadyAt = double.NaN;
        private static RecipeBook s_book;
        private static readonly Dictionary<long, Sprite> s_fillets = new Dictionary<long, Sprite>();

        /// <summary>The renderer the food is drawn with, once made. For tests and plates.</summary>
        public SpriteRenderer Food => _food;

        /// <summary>True while a fish is drawn on the station.</summary>
        public bool Showing => _food != null && _food.enabled;

        /// <summary>The kind drawn (a catch library kind, with its size rung), or null.</summary>
        public string ShownKind { get; private set; }

        /// <summary>True while the food drawn is a fillet shape, not a whole fish.</summary>
        public bool ShowingFillet { get; private set; }

        private void Awake()
        {
            _fixture = GetComponent<CookingStationFixture>();
            GetComponentsInChildren(true, _stationArt);
            _facing = GetComponentInChildren<IFacingCellSource>(true);
        }

        private void OnEnable() => EventBus.Subscribe<FoodCookStarted>(OnCookStarted);

        private void OnDisable()
        {
            EventBus.Unsubscribe<FoodCookStarted>(OnCookStarted);
            Hide();
        }

        private void OnCookStarted(FoodCookStarted e)
        {
            if (_fixture == null || e.StationKey != _fixture.Id) return;
            _startedKg = e.InputWeightKg;
            _startedReadyAt = e.ReadyAtGameSeconds;
        }

        private void LateUpdate()
        {
            CookingStationDef def = _fixture != null ? _fixture.Def : null;
            if (def == null || !def.ShowsFood || !GameServices.Cooking.IsCooking(_fixture.Id, out CookJob job)
                || job.Recipe == null)
            {
                Hide();
                return;
            }
            if (!ReferenceEquals(job.Recipe, _shownRecipe) || job.ReadyAt != _shownReadyAt)
                Show(def, job);
            if (_food == null || !_food.enabled) return;
            int cell = _facing != null ? _facing.FacingCell : 0;
            if (cell != _shownCell) Place(def, cell);
            Sort();
            if (def.BrownsAsItCooks && GameServices.Clock != null)
            {
                float t = Progress(GameServices.Clock.TotalSeconds, job.ReadyAt, _cookSeconds);
                Color c = SampleRamp(GameServices.Config != null ? GameServices.Config.CookedFishRamp : null, t);
                if (_food.color != c) _food.color = c;
            }
        }

        private void Show(CookingStationDef def, CookJob job)
        {
            _shownRecipe = job.Recipe;
            _shownReadyAt = job.ReadyAt;
            _cookSeconds = RecipeRunner.CookSeconds(job.Recipe, def);
            CatchItemLibrary art = FishArtLibrary.ResolveCatchArt(_art, FishArtLibrary.Shared);
            s_book ??= RecipeBook.FromResources();
            string species = SpeciesOf(job.Recipe, s_book);
            float kg = job.ReadyAt == _startedReadyAt ? _startedKg : 0f;
            ShownKind = art == null ? null : kg > 0f && species != null ? art.SizedKindFor(species, kg) : art.KindFor(species);
            Sprite fish = art != null ? art.SpriteFor(ShownKind, 0) : null;
            ShowingFillet = job.Recipe.InputKind == RecipeInputKind.Intermediate;
            Sprite sprite = ShowingFillet ? FilletFor(fish, art.HeldLengthPxFor(ShownKind), GameServices.Config) : fish;
            if (sprite == null)
            {
                Hide();
                return;
            }
            if (_food == null)
            {
                var go = new GameObject("StationFood") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                _food = go.AddComponent<SpriteRenderer>();
            }
            _food.sprite = sprite;
            _food.color = Color.white;
            _food.enabled = true;
            Place(def, _facing != null ? _facing.FacingCell : 0);
        }

        private void Place(CookingStationDef def, int cell)
        {
            _shownCell = cell;
            Vector2 at = AnchorOffset(cell, def.FoodAnchorPropMeters);
            _food.transform.localPosition = new Vector3(at.x, at.y, 0f);
        }

        private void Hide()
        {
            _shownRecipe = null;
            _shownReadyAt = double.NaN;
            _shownCell = -1;
            ShownKind = null;
            ShowingFillet = false;
            if (_food != null) _food.enabled = false;
        }

        // Above the highest station sprite that is drawing (the station's art picks a near or far sprite by
        // the wall it stands on, so the order is read, not assumed).
        private void Sort()
        {
            int layer = 0, order = int.MinValue;
            for (int i = 0; i < _stationArt.Count; i++)
            {
                SpriteRenderer sr = _stationArt[i];
                if (sr == null || sr == _food || !sr.enabled) continue;
                if (sr.sortingOrder > order) { order = sr.sortingOrder; layer = sr.sortingLayerID; }
            }
            if (order == int.MinValue) return;
            int want = order + _sortingAbove;
            if (_food.sortingLayerID != layer) _food.sortingLayerID = layer;
            if (_food.sortingOrder != want) _food.sortingOrder = want;
        }

        /// <summary>
        /// A point in a station's own frame (metres from its pivot: x along its front, y toward its wall, z up)
        /// as a world offset from the pivot when its sheet shows <paramref name="cell"/>: turned
        /// <c>-45 deg x cell</c> on the ground, the ground squashed by <see cref="IsoGround.GroundDepthScale"/>
        /// and the height lifted by <see cref="IsoGround.HeightScale"/>, as the station's own art is.
        /// </summary>
        public static Vector2 AnchorOffset(int cell, Vector3 prop)
        {
            float rad = -360f * cell / Facings * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            float gx = prop.x * c - prop.y * s;
            float gy = prop.x * s + prop.y * c;
            return new Vector2(gx, gy * IsoGround.GroundDepthScale + prop.z * IsoGround.HeightScale);
        }

        /// <summary>
        /// A plain fillet shape for <paramref name="fish"/>: <see cref="GameConfig.FilletLengthOfFish"/> of the
        /// fish as drawn (<paramref name="drawnLengthPx"/>, the rung's measured <see cref="CatchItemLibrary.HeldLengthPxFor"/>; the
        /// sheet cell's width when unmeasured, though the cell pads the fish), <see cref="GameConfig.FilletHeightOfLength"/> of that tall, thick at the head end
        /// and tapering to the tail, in <see cref="GameConfig.FilletFleshColor"/> with a 1 px
        /// <see cref="GameConfig.FilletEdgeColor"/> outline, at the fish sprite's pixels per unit. Made once
        /// per size and colour and shared. Null without a fish sprite.
        /// </summary>
        public static Sprite FilletFor(Sprite fish, float drawnLengthPx, GameConfig config)
        {
            if (fish == null) return null;
            float lengthOf = config != null ? config.FilletLengthOfFish : 0.6f;
            float heightOf = config != null ? config.FilletHeightOfLength : 0.4f;
            Color flesh = config != null ? config.FilletFleshColor : Color.white;
            Color edge = config != null ? config.FilletEdgeColor : Color.grey;
            float drawn = drawnLengthPx > 0f ? Mathf.Min(drawnLengthPx, fish.rect.width) : fish.rect.width;
            int length = Mathf.Max(4, Mathf.RoundToInt(drawn * Mathf.Clamp01(lengthOf)));
            int height = Mathf.Max(3, Mathf.RoundToInt(length * Mathf.Clamp01(heightOf)));
            long key = ((long)length << 48) ^ ((long)height << 36) ^ ((long)flesh.GetHashCode() << 12) ^ edge.GetHashCode()
                       ^ (long)(fish.pixelsPerUnit * 16f);
            if (s_fillets.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            bool[] fill = FilletMask(length, height);
            var px = new Color32[length * height];
            Color32 f = flesh, e = edge, clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < length; x++)
                {
                    int i = y * length + x;
                    if (!fill[i]) { px[i] = clear; continue; }
                    bool rim = x == 0 || y == 0 || x == length - 1 || y == height - 1
                               || !fill[i - 1] || !fill[i + 1] || !fill[i - length] || !fill[i + length];
                    px[i] = rim ? e : f;
                }
            var tex = new Texture2D(length, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
                name = "StationFillet",
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, length, height), new Vector2(0.5f, 0.5f),
                                          fish.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = "StationFillet";
            s_fillets[key] = sprite;
            return sprite;
        }

        /// <summary>The fillet's pixels: a leaf thickest a third of the way from the head (x = 0), tapering to a
        /// point at the tail. Row-major, y up.</summary>
        public static bool[] FilletMask(int length, int height)
        {
            var mask = new bool[Mathf.Max(0, length) * Mathf.Max(0, height)];
            if (length <= 0 || height <= 0) return mask;
            float mid = (height - 1) * 0.5f;
            for (int x = 0; x < length; x++)
            {
                float t = length == 1 ? 0.5f : x / (float)(length - 1);
                // Rounded at the head, thickest at a third, a point at the tail.
                float profile = t < 0.33f ? Mathf.Sqrt(Mathf.Clamp01(0.35f + t / 0.33f * 0.65f)) : 1f - (t - 0.33f) / 0.67f * 0.85f;
                float half = Mathf.Max(0.5f, profile * height * 0.5f);
                for (int y = 0; y < height; y++)
                    if (Mathf.Abs(y - mid) <= half - 0.25f || (Mathf.Abs(y - mid) < 0.75f)) mask[y * length + x] = true;
            }
            return mask;
        }

        /// <summary>
        /// The species a recipe's food started as: its own species input, or, for an intermediate, the
        /// species of the recipe that made it (followed up to <see cref="MaxChainDepth"/> steps). Null for
        /// a category, a stock item, or a chain that leads nowhere.
        /// </summary>
        public static string SpeciesOf(RecipeDef recipe, RecipeBook book)
        {
            for (int depth = 0; recipe != null && depth < MaxChainDepth; depth++)
            {
                switch (recipe.InputKind)
                {
                    case RecipeInputKind.Species:
                        return string.IsNullOrEmpty(recipe.InputId) ? null : recipe.InputId;
                    case RecipeInputKind.Intermediate:
                        if (book == null) return null;
                        List<RecipeDef> feeders = book.FeedersOf(recipe);
                        recipe = feeders.Count > 0 ? feeders[0] : null;
                        break;
                    default:
                        return null;
                }
            }
            return null;
        }

        /// <summary>How far through its job a station is, 0..1, from the job's ready time and length.</summary>
        public static float Progress(double now, double readyAt, float cookSeconds)
        {
            if (cookSeconds <= 0f) return 1f;
            return Mathf.Clamp01(1f - (float)((readyAt - now) / cookSeconds));
        }

        /// <summary>The ramp's colour at <paramref name="t"/> (0..1), even steps between the stops. An
        /// empty ramp is white (no tint).</summary>
        public static Color SampleRamp(Color[] ramp, float t)
        {
            if (ramp == null || ramp.Length == 0) return Color.white;
            if (ramp.Length == 1) return ramp[0];
            float x = Mathf.Clamp01(t) * (ramp.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(x), ramp.Length - 2);
            return Color.Lerp(ramp[i], ramp[i + 1], x - i);
        }
    }
}
