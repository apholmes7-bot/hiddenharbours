using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Light onboarding — the St Peters BUY-AND-REPAIR opening (canon §5.8). A single gentle hint line
    /// points the player through the new earned-dory arc, one beat at a time, then bows out:
    /// <list type="number">
    ///   <item>meet Aunt Ginny (she teaches the loop),</item>
    ///   <item>dig CLAMS on the bared low-water flats,</item>
    ///   <item>walk the sandbar to Nine Mile Creek + buy a COD LICENCE,</item>
    ///   <item>buy a ROD,</item>
    ///   <item>save and BUY the damaged dory at the shipwright,</item>
    ///   <item>pay to REPAIR her,</item>
    ///   <item>sail her home to Coddle Cove.</item>
    /// </list>
    /// The dory is <b>earned, never inherited</b> (P4). Which step you're on is DERIVED from what the
    /// save holds (<see cref="OnboardingProgress"/>: the opening flags, the cod licence by its Def id,
    /// the owned rod, the repaired dory), so a new game, a Continue and a reload show the same line for
    /// the same state. The Core signals — <see cref="FishCaught"/> (the clam dig), <see cref="LicensePurchased"/>,
    /// <see cref="GearPurchased"/>, <see cref="BoatPurchased"/>, <see cref="BoatRepaired"/> — are still
    /// heard, for the session's own sake (a rig with no save service must still walk the steps) and to
    /// record the two beats that leave nothing behind to read. It never references another module's
    /// types (cross-module talk via Core/EventBus).
    ///
    /// Persistence: the loop closes when the dory is REPAIRED (the climactic earned-and-seaworthy beat),
    /// which sets <c>onboarded</c> via <see cref="OnboardingFlags"/> (save-backed); the closing line then
    /// nudges the player to sail home. On a later run the flag is already set, so the hint never shows
    /// again — the opening doesn't nag returning players. Deliberately minimal: no quests, no routines
    /// (that's M2), just one self-dismissing nudge.
    ///
    /// <para><b>⭐ It stopped DRAWING on 2026-08-22 (the owner's diegetic ruling).</b> The step logic
    /// below is untouched — it is still the one thing that knows which beat the player is on, and the
    /// lines are still <see cref="WorldStrings"/>'s. What went is the bottom-centre banner of white
    /// outlined text this file used to build for itself: the nudge is now drawn by
    /// <see cref="QuestPanelPresenter"/> as a leaf off her notebook, pinned lower-right. One source,
    /// one voice, and the director is left holding nothing but the decision.</para>
    /// </summary>
    public sealed class OnboardingDirector : MonoBehaviour
    {
        [Tooltip("How long the closing 'sail her home / fair winds' line lingers after the dory is repaired (real seconds).")]
        [SerializeField] private float _doneSeconds = 6f;

        // The stable content ids each beat waits on live in OnboardingProgress (the one place the
        // derivation and the signal handlers below both read them from).

        private OnboardingFlags _flags;

        // VS-08: persisted via the save file, not PlayerPrefs. Lazy, because an EditMode AddComponent
        // does not run Awake and the step must still be askable there.
        private OnboardingFlags Flags => _flags ??= new OnboardingFlags(new SaveFlagStore());

        // The note the nudge is written on. Made here rather than found, so the opening works in any
        // scene that has a director — the same reasoning that had this file build its own canvas before.
        private QuestPanelPresenter _panel;

        // What THIS SESSION has heard on the bus. Never the answer by itself: each is OR-ed with the
        // fact read from the save (see Facts), so they only matter where the save cannot answer yet —
        // a rig with no save service, or the frame between a signal and the write behind it. Cleared
        // when a game is loaded, so one save's session cannot colour the next one's note.
        private bool _landedCatch;
        private bool _hasCodLicence;
        private bool _hasRod;
        private bool _boughtDory;
        private float _doneTimer;
        private bool _subscribed;

        private void Awake()
        {
            _panel = GetComponent<QuestPanelPresenter>();
            if (_panel == null) _panel = gameObject.AddComponent<QuestPanelPresenter>();
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
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
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
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            _subscribed = false;
        }

        private void Update()
        {
            if (_panel == null) return;

            // Once the loop's done (dory repaired), nudge "sail her home" briefly, then go quiet for good.
            OnboardingStep step = CurrentStep;
            if (step == OnboardingStep.Done)
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

            SetHint(OnboardingProgress.LineFor(step));
        }

        /// <summary>
        /// The beat the player is on, decided from state: what the save holds
        /// (<see cref="OnboardingProgress.Read"/>) OR what this session has heard. Public and
        /// input-free, so a test can build a director over a save and ask — which is also the proof
        /// that a Continue and a reload land on the same beat as the session that made the save.
        /// Alloc-free (a few short list scans), so asking every frame is within rule 7.
        /// </summary>
        public OnboardingStep CurrentStep
        {
            get
            {
                OnboardingFacts facts = Facts();
                return OnboardingProgress.StepFor(in facts);
            }
        }

        private OnboardingFacts Facts()
        {
            OnboardingFacts saved = OnboardingProgress.Read(GameServices.Save?.Current, Flags);
            bool boughtDory = saved.BoughtDory || _boughtDory;
            bool hasRod = saved.HasRod || _hasRod;
            bool hasCodLicence = saved.HasCodLicence || _hasCodLicence;
            return new OnboardingFacts(
                metGinny: saved.MetGinny,
                landedCatch: saved.LandedCatch || _landedCatch || hasCodLicence || hasRod || boughtDory,
                hasCodLicence: hasCodLicence,
                hasRod: hasRod,
                boughtDory: boughtDory,
                doryRepaired: saved.DoryRepaired,
                onboarded: saved.Onboarded);
        }

        // ---- signals ------------------------------------------------------------------------
        // Public so a test can drive each through the same path the bus uses (an EditMode AddComponent
        // does not run OnEnable, so nothing is subscribed there).

        /// <summary>Any landed catch on the island is the clam dig (the first by-hand catch, before any
        /// rod). Sold, it leaves nothing in the save to read — so the first one is written down.</summary>
        public void OnCaught(FishCaught e)
        {
            _landedCatch = true;
            if (!Flags.LandedCatch) Flags.LandedCatch = true;
        }

        /// <summary>Only the COD licence moves the note on. A licence is the one named by its Def id:
        /// the clam licence Ginny fronts is a different licence and must not count as this one.</summary>
        public void OnLicence(LicensePurchased e)
        {
            if (e.LicenseId == OnboardingProgress.CodLicenceId) _hasCodLicence = true;
        }

        public void OnGear(GearPurchased e)
        {
            if (e.GearId == OnboardingProgress.RodGearId) _hasRod = true;
        }

        /// <summary>The damaged dory, paid for. Written down because <c>SaveData.OwnedBoats</c> cannot
        /// say it: that list also takes every hull she has had the helm of.</summary>
        public void OnBoatBought(BoatPurchased e)
        {
            if (e.BoatId != OnboardingProgress.DoryBoatId) return;
            _boughtDory = true;
            if (!Flags.BoughtDory) Flags.BoughtDory = true;
        }

        public void OnBoatRepaired(BoatRepaired e)
        {
            if (Flags.Onboarded) return;
            if (e.BoatId != OnboardingProgress.DoryBoatId) return;
            Flags.Onboarded = true;      // the dory is earned + seaworthy — persist so the opening never re-triggers
            _doneTimer = _doneSeconds;
        }

        /// <summary>A game was loaded over this session: what the session heard belonged to the game
        /// before it. The save just restored is the whole truth from here.</summary>
        public void OnGameLoaded(GameLoaded e)
        {
            _landedCatch = false;
            _hasCodLicence = false;
            _hasRod = false;
            _boughtDory = false;
        }

        // ---- the nudge ----------------------------------------------------------------------

        /// <summary>
        /// Hand this beat's line to the note; null or empty takes the note down. Called every frame and
        /// deliberately unguarded here — <see cref="QuestPanelPresenter.Show"/> change-detects, so an
        /// unchanged nudge costs one string compare and draws nothing (rule 7).
        /// </summary>
        private void SetHint(string text) => _panel.Show(text);
    }
}
