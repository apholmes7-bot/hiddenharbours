using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Light onboarding — the St Peters BUY-AND-REPAIR opening (canon §5.8). A single gentle hint line
    /// points the player through the new earned-dory arc, one beat at a time, then bows out:
    /// <list type="number">
    ///   <item>meet Aunt Ginny (she teaches the loop),</item>
    ///   <item>dig CLAMS on the bared low-water flats,</item>
    ///   <item>walk the sandbar to Greywick + buy a COD LICENCE,</item>
    ///   <item>buy a ROD,</item>
    ///   <item>save and BUY the damaged dory at the shipwright,</item>
    ///   <item>pay to REPAIR her,</item>
    ///   <item>sail her home to Coddle Cove.</item>
    /// </list>
    /// The dory is <b>earned, never inherited</b> (P4). It reads the opening flags (met Ginny) and
    /// listens on existing Core signals — <see cref="FishCaught"/> (the clam dig), <see cref="LicensePurchased"/>,
    /// <see cref="GearPurchased"/>, <see cref="BoatPurchased"/>, <see cref="BoatRepaired"/> — to know which
    /// step you're on; it never references another module's types (cross-module talk via Core/EventBus).
    ///
    /// Persistence: the loop closes when the dory is REPAIRED (the climactic earned-and-seaworthy beat),
    /// which sets <c>onboarded</c> via <see cref="OnboardingFlags"/> (save-backed); the closing line then
    /// nudges the player to sail home. On a later run the flag is already set, so the hint never shows
    /// again — the opening doesn't nag returning players. Deliberately minimal: no quests, no routines
    /// (that's M2), just one self-dismissing nudge.
    /// </summary>
    public sealed class OnboardingDirector : MonoBehaviour
    {
        [Tooltip("How long the closing 'sail her home / fair winds' line lingers after the dory is repaired (real seconds).")]
        [SerializeField] private float _doneSeconds = 6f;

        // Stable ids the nudge keys off (matching the economy data assets). Not gameplay magic numbers —
        // these are content ids (CLAUDE.md rule 2's id seam), surfaced so the owner can see what each beat
        // waits on. The director only compares incoming signal ids to these.
        private const string RodGearId = "gear.rod";
        private const string DoryBoatId = "boat.dory";

        private OnboardingFlags _flags;
        private Text _hint;

        private bool _clamsDug;
        private bool _licenced;
        private bool _gotRod;
        private bool _boughtDory;
        private float _doneTimer;
        private bool _subscribed;

        private void Awake()
        {
            _flags = new OnboardingFlags(new SaveFlagStore());   // VS-08: persisted via the save file, not PlayerPrefs
            BuildHint();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<LicensePurchased>(OnLicence);
            EventBus.Subscribe<GearPurchased>(OnGear);
            EventBus.Subscribe<BoatPurchased>(OnBoatBought);
            EventBus.Subscribe<BoatRepaired>(OnBoatRepaired);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<LicensePurchased>(OnLicence);
            EventBus.Unsubscribe<GearPurchased>(OnGear);
            EventBus.Unsubscribe<BoatPurchased>(OnBoatBought);
            EventBus.Unsubscribe<BoatRepaired>(OnBoatRepaired);
            _subscribed = false;
        }

        private void Update()
        {
            if (_hint == null) return;

            // Once the loop's done (dory repaired), nudge "sail her home" briefly, then go quiet for good.
            if (_flags.Onboarded)
            {
                if (_doneTimer > 0f)
                {
                    _doneTimer -= Time.unscaledDeltaTime;
                    // First the warm close, then the practical "sail home" nudge as it lingers.
                    SetHint(_doneTimer > _doneSeconds * 0.5f ? WorldStrings.OnboardDone : WorldStrings.OnboardSailHome);
                }
                else SetHint(null);
                return;
            }

            SetHint(NextStep());
        }

        private string NextStep()
        {
            if (!_flags.MetGinny) return WorldStrings.OnboardTalkGinny;   // meet your aunt
            if (!_clamsDug)       return WorldStrings.OnboardDigClams;    // first catch, by hand
            if (!_licenced)       return WorldStrings.OnboardBuyLicence;  // cross the bar, buy a cod licence
            if (!_gotRod)         return WorldStrings.OnboardBuyRod;      // a rod to hand-line cod
            if (!_boughtDory)     return WorldStrings.OnboardBuyDory;     // buy the damaged dory
            return WorldStrings.OnboardRepairDory;                        // pay to repair her
        }

        // ---- signals ------------------------------------------------------------------------

        // Any landed catch on the island is the clam dig (the first by-hand catch, before any rod).
        private void OnCaught(FishCaught e) => _clamsDug = true;

        private void OnLicence(LicensePurchased e) => _licenced = true;

        private void OnGear(GearPurchased e)
        {
            if (e.GearId == RodGearId) _gotRod = true;
        }

        private void OnBoatBought(BoatPurchased e)
        {
            if (e.BoatId == DoryBoatId) _boughtDory = true;
        }

        private void OnBoatRepaired(BoatRepaired e)
        {
            if (_flags.Onboarded) return;
            if (e.BoatId != DoryBoatId) return;
            _flags.Onboarded = true;     // the dory is earned + seaworthy — persist so the opening never re-triggers
            _doneTimer = _doneSeconds;
        }

        // ---- hint label ---------------------------------------------------------------------

        private void SetHint(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_hint.enabled != show) _hint.enabled = show;
            if (show && _hint.text != text) _hint.text = text;
        }

        private void BuildHint()
        {
            var canvasGo = new GameObject("Onboarding_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 94;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 70f); // a low banner, clear of the board/dock hint
            rt.sizeDelta = new Vector2(900f, 44f);

            _hint = go.GetComponent<Text>();
            _hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hint.fontSize = 26;
            _hint.alignment = TextAnchor.LowerCenter;
            _hint.color = new Color(1f, 0.96f, 0.85f); // warm parchment
            _hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hint.verticalOverflow = VerticalWrapMode.Overflow;
            _hint.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            _hint.enabled = false;
        }
    }
}
