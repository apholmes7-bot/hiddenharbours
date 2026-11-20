using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fish being cooked, on the pan or the board</b> (fish art pass 2, plan 4b): while a station has
    /// a job on it, the fish the job started from is drawn at the station's food anchor
    /// (<see cref="CookingStationDef.FoodAnchorMeters"/>), as its own deck-lay sprite. On a browning
    /// station (<see cref="CookingStationDef.BrownsAsItCooks"/>) it is tinted along the one cooked colour
    /// ramp (<see cref="GameConfig.CookedFishRamp"/>) as the job runs, so a cod on the stove goes from raw
    /// to golden without a sheet per species.
    ///
    /// <para><b>Which fish.</b> A job names only its recipe. A recipe that takes a species names it; a
    /// recipe that takes an intermediate (a fillet) is followed up the recipe book to the recipe that made
    /// it, and so to the species (<see cref="SpeciesOf"/>). A recipe that takes a whole category draws the
    /// library's fallback kind. The job carries no weight, so the middle rung is drawn.</para>
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

        private CookingStationFixture _fixture;
        private SpriteRenderer _food;
        private readonly List<SpriteRenderer> _stationArt = new List<SpriteRenderer>();
        private RecipeDef _shownRecipe;
        private double _shownReadyAt = double.NaN;
        private float _cookSeconds;
        private static RecipeBook s_book;

        /// <summary>The renderer the food is drawn with, once made. For tests and plates.</summary>
        public SpriteRenderer Food => _food;

        /// <summary>True while a fish is drawn on the station.</summary>
        public bool Showing => _food != null && _food.enabled;

        /// <summary>The kind drawn (a catch library kind), or null.</summary>
        public string ShownKind { get; private set; }

        private void Awake()
        {
            _fixture = GetComponent<CookingStationFixture>();
            GetComponentsInChildren(true, _stationArt);
        }

        private void OnDisable() => Hide();

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
            ShownKind = art != null ? art.KindFor(species) : null;
            Sprite sprite = art != null ? art.SpriteFor(ShownKind, 0) : null;
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
            _food.transform.localPosition = new Vector3(def.FoodAnchorMeters.x, def.FoodAnchorMeters.y, 0f);
            _food.sprite = sprite;
            _food.color = Color.white;
            _food.enabled = true;
        }

        private void Hide()
        {
            _shownRecipe = null;
            _shownReadyAt = double.NaN;
            ShownKind = null;
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
