using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE FISH YOU CAN SEE (owner's ruling 2026-09-05: <i>"I want fish to actually be visible swimming
    /// with all their animations incorporated"</i>) — the third reader of the one school model.
    ///
    /// <para><b>It invents no fish.</b> The fish finder reads <c>IFishSchools.MarksAt</c> to draw its
    /// marks; the fishing path reads <c>IFishSchools.SchoolsAt</c> to raise the bite and weight the
    /// species roll; this reads <see cref="IFishSchoolView.SchoolsInView"/> — the same schools, from the
    /// same model, built by the same <c>(cell, slot)</c> gather. There is no spawner, no list of live
    /// fish, no timer and nothing in the save. <b>Every fish drawn here is a fish the resolver would
    /// bite, of a species it would roll</b>, which is the owner's honesty invariant extended from the
    /// glass to the water.</para>
    ///
    /// <para><b>Pose = f(clock, seed)</b> (rule 5). Positions come from <see cref="ShoalMath"/> and events
    /// from <see cref="ShoalEventMath"/>, both closed-form in <c>(worldSeed, schoolKey, t)</c>. Nothing
    /// integrates and nothing accumulates, so a save/load or a region stream-in lands the same fish in the
    /// same place mid-stroke — and two players on one seed watch the same herring jump.</para>
    ///
    /// <para><b>Depth decides what you see, not whether it is there</b> — <see cref="SwimmerVisibility"/>.
    /// A Deep school draws nothing at all while still showing on the finder and still biting: the
    /// instrument earns its keep exactly where your eyes cannot.</para>
    ///
    /// <para><b>Budget</b> (rule 7): renderers are pooled and never allocated in steady state, the school
    /// query and the swimmer solve run on a slow tick, and the whole thing early-outs to nothing when the
    /// model, the library or the camera is missing. The visible count is bounded by
    /// <c>FishSchoolModel.ViewCells</c> schools x <see cref="MaxSwimmersPerSchool"/>.</para>
    /// </summary>
    [AddComponentMenu("Hidden Harbours/Fishing/Fish School Presenter")]
    public sealed class FishSchoolPresenter : MonoBehaviour
    {
        [Header("Art")]
        [SerializeField, Tooltip("The baked fish sheets. Left empty, it is loaded from Resources at boot.")]
        private FishSwimSpriteLibrary _library;

        [Header("Sorting (a fish is never above a boat)")]
        [SerializeField, Tooltip("Sorting layer for swimmers. Empty = the default layer.")]
        private string _sortingLayer = "";

        [SerializeField, Tooltip("Sorting order for swimmers. Must stay BELOW the hull order " +
                                 "(BoatVisualDef.SortingOrder, 1) and below the wake (-1): a fish swims " +
                                 "under the boat and under its wash.")]
        private int _sortingOrder = -3;

        [Header("Pacing")]
        [SerializeField, Range(1, 30), Tooltip("School queries per second. The school set changes on the " +
                                               "scale of hours, so this is deliberately slow; swimmer " +
                                               "poses are solved on the same tick and are cheap.")]
        private int _ticksPerSecond = 10;

        [SerializeField, Min(1f), Tooltip("Seconds between events in one school — how often SOME fish in " +
                                          "it jumps, rolls or thrashes. One fish per period, never two.")]
        private float _eventPeriodSeconds = 20f;

        // RETIRED 2026-09-07 (owner's ruling of 2026-09-06, per-species spread): the shoal's loop used
        // to be _loopReach01 (0.8) x the SCHOOL's radius. Those are two different quantities and the
        // multiplication produced a loop 17-44 m across, because a school is 22-55 m wide for a reason
        // that has nothing to do with fish: it is how far a BOAT may be and still be on the mark. A
        // handful of fish wandering a 40 m circle is why nobody saw them from a camera 25 m wide. The
        // spread is now a LENGTH the species states (FishSpeciesDef.ShoalSpreadMetres), falling back to
        // GameConfig.FishSchools.ShoalSpreadMetres, which ships at the rig's own 1.2 m shoal.

        [Header("Budget")]
        [SerializeField, Range(1, 48), Tooltip("PERFORMANCE ceiling only — the most sprites one school " +
                                               "may cost. Density is the SPECIES' business now " +
                                               "(FishSpeciesDef.Min/MaxSchoolMarks, owner ruling " +
                                               "2026-09-06) and MarkCount always wins when it is smaller; " +
                                               "this exists so a mis-authored range cannot blow the frame " +
                                               "budget. Keep it above the largest authored school " +
                                               "(herring, 30) or shoals will be silently clipped.")]
        private int _maxSwimmersPerSchool = 32;

        /// <summary>The frame-budget ceiling on one school's sprites — <b>never</b> a statement about how
        /// many fish are there. Density is per-species data on the Def
        /// (<c>FishSpeciesDef.MinSchoolMarks</c>/<c>MaxSchoolMarks</c>), resolved once in the model, and
        /// <c>FishSchool.MarkCount</c> always wins when it is smaller.</summary>
        public int MaxSwimmersPerSchool => Mathf.Max(1, _maxSwimmersPerSchool);

        /// <summary>Sorting order the swimmers draw at — read by the guard test that pins them under the
        /// fleet.</summary>
        public int SortingOrder => _sortingOrder;

        /// <summary>How many swimmers were drawn on the last tick. Diagnostics and the PlayMode
        /// acceptance; never a gameplay input.</summary>
        public int VisibleSwimmers { get; private set; }

        /// <summary>How many schools the last view query returned.</summary>
        public int VisibleSchools { get; private set; }

        // ---- pooled renderers + scratch (rule 7: nothing allocates in steady state) ------------------
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(64);
        private readonly List<FishSchool> _schools = new List<FishSchool>(FishSchoolModel.ViewCells);
        private ShoalMath.Swimmer[] _swimmers = new ShoalMath.Swimmer[12];

        private Camera _camera;
        private float _tickAccum;

        /// <summary>
        /// WHAT THE FISH HEARD — recent <see cref="GullSplashed"/> signals, subscribed for exactly as
        /// long as this component is enabled.
        ///
        /// <para>The lifetime is the whole point (fixture law: a signal is heard only by a listener that
        /// already exists). The subscription is taken in <c>OnEnable</c> and dropped in <c>OnDisable</c>,
        /// so a presenter that has been switched off is not still moving fish, and one switched back on
        /// is not hearing every splash twice.</para>
        ///
        /// <para>It is the fishing side of the Core signal and it names no gull type: the bird module
        /// publishes <see cref="GullSplashed"/>, this listens, and neither one can see the other's
        /// classes (rule 4).</para>
        /// </summary>
        private readonly GullSplashLog _splashes = new GullSplashLog();

        /// <summary>
        /// SELF-INSTALLING (the <c>BoatWakeEmitter</c> / <c>WakeSpriteLibrary</c> pattern): one hidden
        /// <c>DontDestroyOnLoad</c> host before the first scene loads, so the sea shows its fish with
        /// <b>no builder change, no builder re-run and no scene edit</b>.
        ///
        /// <para>That is not just convenience here — it is the safe road. Both region builders are FULL
        /// rebuilds that wipe the hand-authored layer, and saving a region scene from an editor writes
        /// hundreds of serialization catch-up hunks belonging to other lanes. A presenter that needs
        /// neither keeps this arc out of both hazards.</para>
        ///
        /// <para>It is also correct by construction: the presenter is region-agnostic. It reads whichever
        /// model is installed at <c>GameServices.FishSchools</c>, so one persistent host draws the fish of
        /// whatever region the player is standing in, and draws nothing at all where no model is installed
        /// — the honest empty sea.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("FishSchoolPresenter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<FishSchoolPresenter>();
        }

        private static bool _installed;

        /// <summary>
        /// The self-installed host, or null before it exists.
        ///
        /// <para>⚠ Why this is not just <c>FindFirstObjectByType</c>. The host is created with
        /// <see cref="HideFlags.HideAndDontSave"/>, and Unity's object find deliberately skips hidden
        /// objects — so a fixture that looks for the presenter that way reports it MISSING while it is
        /// running perfectly well, which is exactly the false negative this arc's first plate run
        /// produced. A component that installs itself has to be able to say so.</para>
        /// </summary>
        public static FishSchoolPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (_library == null) _library = Resources.Load<FishSwimSpriteLibrary>(
                FishSwimSpriteLibrary.ResourcesPath);
            if (_swimmers.Length < MaxSwimmersPerSchool)
                _swimmers = new ShoalMath.Swimmer[MaxSwimmersPerSchool];
        }

        private void OnEnable() => _splashes.Subscribe();

        private void OnDisable()
        {
            _splashes.Unsubscribe();

            // Leave no fish frozen on the water behind us.
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].enabled = false;
            VisibleSwimmers = 0;
            VisibleSchools = 0;
        }

        private void LateUpdate()
        {
            float period = 1f / Mathf.Max(1, _ticksPerSecond);
            _tickAccum += Time.deltaTime;
            if (_tickAccum < period) return;
            _tickAccum = 0f;

            Rebuild();
        }

        /// <summary>
        /// One repaint: read the schools in view, solve their swimmers, and place the pooled renderers.
        /// Public so a plate or a PlayMode test can step it without waiting on the tick.
        /// </summary>
        public void Rebuild()
        {
            VisibleSwimmers = 0;
            VisibleSchools = 0;

            int used = 0;
            if (TryGather(out double now, out DepthDropSettings depth, out int worldSeed,
                          out FishSchoolSettings schools))
                used = Paint(now, in depth, worldSeed, in schools);

            // Everything the paint did not claim goes dark. Never destroyed — the pool is the budget.
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].enabled = false;

            VisibleSwimmers = used;
        }

        /// <summary>Read the world and the schools in view. False = draw nothing at all (no model, no
        /// camera, no library, or an empty sea).</summary>
        private bool TryGather(out double now, out DepthDropSettings depth, out int worldSeed,
                               out FishSchoolSettings schools)
        {
            now = 0.0;
            depth = default;
            worldSeed = 0;
            schools = FishSchoolMath.Sanitized(FishSchoolSettings.Default);

            if (_library == null) return false;

            // The view seam is OPTIONAL by design: a producer that cannot answer an area query leaves the
            // water empty rather than letting the presenter invent a second sim (see IFishSchoolView).
            if (!(GameServices.FishSchools is IFishSchoolView view)) return false;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            IEnvironmentService env = GameServices.Environment;
            worldSeed = env != null ? env.WorldSeed : 0;
            depth = GameServices.Config != null ? GameServices.Config.DepthDrop : DepthDropSettings.Default;
            if (GameServices.Config != null)
                schools = FishSchoolMath.Sanitized(GameServices.Config.FishSchools);

            VisibleSchools = view.SchoolsInView(ViewRect(_camera), now, _schools);
            return VisibleSchools > 0;
        }

        /// <summary>The camera's footprint in world metres — what "in view" means for the query.</summary>
        private static Rect ViewRect(Camera cam)
        {
            float h = cam.orthographic
                ? cam.orthographicSize * 2f
                : Mathf.Abs(cam.transform.position.z) * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        /// <summary>Place every visible school's swimmers; returns how many renderers were used.</summary>
        private int Paint(double now, in DepthDropSettings depth, int worldSeed,
                          in FishSchoolSettings schools)
        {
            int used = 0;

            for (int s = 0; s < _schools.Count; s++)
            {
                FishSchool school = _schools[s];

                SwimmerDraw draw = SwimmerVisibility.For(school.DepthMetres, in depth);
                if (draw == SwimmerDraw.None) continue;      // deep: the finder's job, not the water's

                // A SHELLFISH DOES NOT SWIM. A region's pool holds clams, lobster and crab beside the
                // finfish, and the art table's catch-item fallback would happily draw a clam school as a
                // shoal of cod. An unmapped species is drawn as nothing at all; only a school that states
                // NO species falls back to a generic swimmer, because that school is real and its fish
                // are simply unstated.
                string primary = FirstSpecies(school);
                string kind;
                if (string.IsNullOrEmpty(primary)) kind = _library.KindFor(null);
                else if (!_library.TrySwimKindFor(primary, out kind)) continue;

                float lengthM = _library.LengthMetresFor(kind);

                // The school's OWN density is the truth; the cap only trims a very dense one.
                int n = Mathf.Clamp(school.MarkCount, 0, MaxSwimmersPerSchool);
                if (n <= 0) continue;

                uint key = SchoolKey(school);
                float spread = SpreadMetresFor(primary);
                int solved = ShoalMath.Fill(lengthM, n, now, ShoalMath.SeedFor(key), spread,
                                            ShoalMath.DefaultSpeedMetresPerSecond, 1f,
                                            ShoalMath.DefaultZMetres, _swimmers);

                // THE SCATTER — the owner's "fish react to it landing", seen from the water. It is the
                // one event that is not on the slotted schedule and the only one the WHOLE school does at
                // once: a gull came down on them, so every fish bolts. It outranks whatever the schedule
                // had planned for this instant, because a herring mid-roll with a bird landing on top of
                // it is darting, not rolling.
                //
                // It moves the PICTURE and nothing else. No bite rate, no catch weight and no species
                // roll is read or written here (owner's ruling 2026-09-09: fish bite rates are LEAVE).
                bool scattering = _splashes.TryScatter(school, now,
                                                       schools.GullSplashScatterRadiusMetres,
                                                       schools.GullSplashScatterStrength01,
                                                       out float scatter01, out double scatterStart,
                                                       out Vector2 scatterFrom);

                ShoalEventKind ev;
                int actor;
                double evStart;
                if (scattering)
                {
                    ev = ShoalEventKind.Dart;
                    actor = -1;                  // every fish in it, not the one the schedule picked
                    evStart = scatterStart;
                }
                else
                {
                    ev = ShoalEventMath.At(worldSeed, key, now, n, KindJumps(school),
                                           _eventPeriodSeconds, out actor, out evStart);
                }

                for (int i = 0; i < solved; i++)
                {
                    bool acting = scattering || (ev != ShoalEventKind.None && i == actor);
                    if (!Place(used, school, _swimmers[i], kind, draw,
                               acting ? ev : ShoalEventKind.None, evStart, now,
                               scattering ? scatter01 : 0f, scatterFrom, spread)) continue;
                    used++;
                }
            }

            return used;
        }

        /// <summary>Point one pooled renderer at one fish. False when there is no art for it, in which
        /// case the slot is left for the next fish rather than drawn blank.</summary>
        private bool Place(int slot, in FishSchool school, in ShoalMath.Swimmer sw, string kind,
                           SwimmerDraw draw, ShoalEventKind ev, double evStart, double now,
                           float scatter01, Vector2 scatterFrom, float spreadMetres)
        {
            // A shadow school shows the SHAPE and nothing else — no fins, no events.
            string anim = draw == SwimmerDraw.Shadow
                ? FishSwimSpriteLibrary.AnimShadow
                : AnimFor(ev);

            int frame = draw == SwimmerDraw.Shadow || ev == ShoalEventKind.None
                ? sw.Frame
                : ShoalEventMath.FrameAt(ev, evStart, now);

            Sprite sprite = _library.Cell(kind, anim, sw.Dir, frame);
            if (sprite == null && anim != FishSwimSpriteLibrary.AnimSwim)
                sprite = _library.Cell(kind, FishSwimSpriteLibrary.AnimSwim, sw.Dir, sw.Frame);
            if (sprite == null) return false;

            SpriteRenderer sr = Renderer(slot);
            sr.sprite = sprite;
            sr.enabled = true;

            // A jump adds the world-space lift the rig drew the fish LEAVING the water with, so it
            // actually crosses the surface instead of sliding along it.
            float lift = ev == ShoalEventKind.Jump
                ? ShoalEventMath.JumpArc01(evStart, now) * ShoalEventMath.JumpTravelMetres
                : 0f;

            // A scattered fish bolts straight away from where the bird hit and eases back into the
            // shoal as the dart ends — the same half-sine the jump lifts on, so nothing snaps at either
            // end. With no gull the strength is zero and this is an exact zero offset: the water at rest
            // is drawn by the same line it always was.
            ShoalEventMath.ScatterOffset(scatterFrom.x, scatterFrom.y,
                                         school.Centre.x + sw.X, school.Centre.y + sw.Y,
                                         scatter01, spreadMetres, evStart, now,
                                         out float boltX, out float boltY);

            sr.transform.localPosition = new Vector3(school.Centre.x + sw.X + boltX,
                                                     school.Centre.y + sw.Y + lift + boltY, 0f);
            sr.transform.localScale = Vector3.one * Mathf.Max(0.01f, sw.Scale);

            // Inshore is dimmed on top of the depth tint the rig already baked into the cells.
            float a = draw == SwimmerDraw.Dim ? 0.65f : 1f;
            sr.color = new Color(1f, 1f, 1f, a);
            return true;
        }

        private static string AnimFor(ShoalEventKind ev)
        {
            switch (ev)
            {
                case ShoalEventKind.Jump: return FishSwimSpriteLibrary.AnimJump;
                case ShoalEventKind.Roll: return FishSwimSpriteLibrary.AnimRoll;
                case ShoalEventKind.Thrash: return FishSwimSpriteLibrary.AnimThrash;
                case ShoalEventKind.Dart: return FishSwimSpriteLibrary.AnimDart;
                default: return FishSwimSpriteLibrary.AnimSwim;
            }
        }

        /// <summary>Grow-once pooled renderer for a slot.</summary>
        private SpriteRenderer Renderer(int slot)
        {
            while (_pool.Count <= slot)
            {
                var go = new GameObject("Swimmer " + _pool.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                if (!string.IsNullOrEmpty(_sortingLayer)) sr.sortingLayerName = _sortingLayer;
                sr.sortingOrder = _sortingOrder;
                sr.enabled = false;
                _pool.Add(sr);
            }
            return _pool[slot];
        }

        /// <summary>The species this school draws as — its first stated id, or null for an unstated
        /// school (which then draws as the library's fallback kind rather than not at all).</summary>
        private static string FirstSpecies(in FishSchool school)
            => school.SpeciesIds != null && school.SpeciesIds.Count > 0 ? school.SpeciesIds[0] : null;

        /// <summary>
        /// HOW WIDE THIS SHOAL SWIMS, in metres — the species' own <c>ShoalSpreadMetres</c>, or the
        /// owner's global fallback when it states none (owner's ruling 2026-09-06: a mackerel shoal
        /// ranges loose and fast, a cod school holds tight).
        ///
        /// <para><b>A length, never a fraction of the school's radius.</b> The school's radius is how far
        /// a BOAT may be and still be on the mark (22-55 m); the fish inside it hold together within a
        /// metre or two. Multiplying the two — which is what this did until 2026-09-07 — sent a handful
        /// of fish wandering a 40 m circle in front of a camera 25 m wide, and is the whole reason the
        /// schools were there and could not be seen.</para>
        /// </summary>
        /// <remarks><b>public for the duty-cycle instrument</b> (<c>FishDutyCycleAtTheLandingTests</c>):
        /// how wide the shoal swims is one of the two terms that decide whether a fish lands in the
        /// owner's frame, and a guard that transcribed it instead of calling it would be measuring its
        /// own copy. Behaviour unchanged.</remarks>
        public static float SpreadMetresFor(string speciesId)
        {
            if (!string.IsNullOrEmpty(speciesId))
            {
                FishSpeciesDef def = FishSpeciesRegistry.Get(speciesId);
                if (def != null && def.StatesShoalSpread) return def.ShoalSpreadMetres;
            }

            GameConfig config = GameServices.Config;
            return config != null
                ? Mathf.Max(0.05f, config.FishSchools.ShoalSpreadMetres)
                : FishSchoolSettings.ShoalMathReferenceSpreadMetres;
        }

        /// <summary>Does this school's species clear the water? Reads the Def's own
        /// <see cref="FishFlags.Jumps"/> through the species registry — one publisher, and a flounder
        /// school can never jump.</summary>
        private static bool KindJumps(in FishSchool school)
        {
            string id = FirstSpecies(school);
            if (string.IsNullOrEmpty(id)) return false;
            FishSpeciesDef def = FishSpeciesRegistry.Get(id);
            return def != null && (def.BehaviorFlags & FishFlags.Jumps) != 0;
        }

        /// <summary>A stable key for one school, from the two things that identify it: where its centre is
        /// and when its window opened. The model's own <c>(cell, slot)</c> key is private to it, and this
        /// is a faithful stand-in — two different schools cannot share a centre AND a start.</summary>
        /// <remarks><b>public for the duty-cycle instrument</b> — the shoal's seed, and so the pose of
        /// every fish in it, hangs off this. Behaviour unchanged.</remarks>
        public static uint SchoolKey(in FishSchool school)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)Mathf.RoundToInt(school.Centre.x * 16f)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(school.Centre.y * 16f)) * 16777619u;
                h = (h ^ (uint)(long)school.StartSeconds) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h;
            }
        }
    }
}
