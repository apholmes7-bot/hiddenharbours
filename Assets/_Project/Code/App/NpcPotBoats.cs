using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>The NPC pot boats at work</b> (P3, a living working coast): each morning a moored boat casts off, runs to
    /// her ground, and works her string of pots, then comes home. This is only the PRESENTER. What she is doing at
    /// any second is <see cref="NpcPotDay"/>, a pure function of <c>(worldSeed, clock)</c>; this class puts the
    /// scene's own <see cref="MooredBoat"/> where the day says and draws each pot's buoy, rope and pot. Nothing is
    /// saved and nothing steers (rule 5): load at any hour and the picture is the clock's.
    ///
    /// <para><b>Why App.</b> The day lives in Fishing and the moored hull in Boats, and neither may name the other
    /// (rule 4). App is the composition root that may bind both.</para>
    ///
    /// <para><b>No scene edit.</b> The boat is the one the region's builder already baked at her berth; the gear is
    /// built here at run time and pooled for the life of the binding (rule 7: no allocation per frame, the tide
    /// and seabed are read on the slow tick, only a pot being worked is touched every frame).</para>
    ///
    /// <para><b>The player's pots are untouched.</b> No <c>TrapPlaced</c> is published, nothing is written to the
    /// save, and the buoy is the owner's colour, never the player's yellow.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcPotBoats : MonoBehaviour
    {
        private const float SlowTickSeconds = 0.25f;     // tide, seabed, soaking pots, binding
        private const int RopePoints = 8;                 // samples along a drawn rope
        private const double TideProbeClockSeconds = 20;  // half-width of the tide-rate difference
        private const float BerthMismatchMeters = 0.75f;  // Def berth vs scene berth before it is worth a warning
        private const float HeadingEpsilonDegrees = 0.25f;
        private const float BuoyTossAt01 = 0.5f;          // share of the set after which the buoy follows the pot

        private static NpcPotBoats _instance;

        /// <summary>The one live host, or null before it installs.</summary>
        public static NpcPotBoats Instance => _instance;

        /// <summary>
        /// When false, no boat is bound from the library by itself; <see cref="TryBind"/> still works. Proofs that
        /// hold the moored fleet to its berths at an arbitrary hour switch this off rather than fight the clock.
        /// </summary>
        public static bool AutoBind { get; set; } = true;

        private readonly List<Boat> _boats = new List<Boat>();
        private readonly Vector2[] _ropeBuffer = new Vector2[RopePoints];
        private NpcPotDayLibrary _library;
        private float _nextSlowTick;
        private Material _ropeMaterial;
        private ITidalTerrain _terrain;   // read on the slow tick

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[NpcPotBoats]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NpcPotBoats>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _library = Resources.Load<NpcPotDayLibrary>(NpcPotDayLibrary.ResourcesPath);
        }

        private void OnDestroy()
        {
            for (int i = _boats.Count - 1; i >= 0; i--) Release(_boats[i]);
            _boats.Clear();
            if (_ropeMaterial != null) Destroy(_ropeMaterial);
            if (_instance == this) _instance = null;
        }

        // ---- what a test or a tool may ask ---------------------------------------------------------

        /// <summary>How many boats are working a day right now.</summary>
        public int BoundCount => _boats.Count;

        /// <summary>True when <paramref name="defId"/>'s boat is bound.</summary>
        public bool IsBound(string defId) => Find(defId) != null;

        /// <summary>The buoy of pot <paramref name="potIndex"/>, or null. Inactive while the pot is aboard.</summary>
        public Transform BuoyOf(string defId, int potIndex)
        {
            Boat boat = Find(defId);
            return boat != null && potIndex >= 0 && potIndex < boat.Pots.Length ? boat.Pots[potIndex].BuoyRoot.transform : null;
        }

        /// <summary>The rope of pot <paramref name="potIndex"/>, or null. Disabled while the pot is aboard.</summary>
        public LineRenderer RopeOf(string defId, int potIndex)
        {
            Boat boat = Find(defId);
            return boat != null && potIndex >= 0 && potIndex < boat.Pots.Length ? boat.Pots[potIndex].Rope : null;
        }

        /// <summary>The pot sprite of pot <paramref name="potIndex"/> (seen at the rail and just under it), or null.</summary>
        public SpriteRenderer PotOf(string defId, int potIndex)
        {
            Boat boat = Find(defId);
            return boat != null && potIndex >= 0 && potIndex < boat.Pots.Length ? boat.Pots[potIndex].PotRenderer : null;
        }

        /// <summary>The deck of <paramref name="defId"/>'s boat (her hand, what is in it, her crates), or null.</summary>
        public NpcPotDeck DeckOf(string defId) => Find(defId)?.Deck;

        /// <summary>What <paramref name="defId"/>'s pots hold today, or null before her first frame.</summary>
        public NpcPotDayCatch CatchOf(string defId) => Find(defId)?.Catch;

        /// <summary>The plan for landing <paramref name="defId"/>'s catch today, or null before her first frame.</summary>
        public NpcPotLanding LandingOf(string defId) => Find(defId)?.Landing;

        /// <summary>Her figure and crates ashore, or null.</summary>
        public NpcPotLandingWalk LandingWalkOf(string defId) => Find(defId)?.Walk;

        /// <summary>
        /// Binds <paramref name="hull"/> to <paramref name="def"/>'s day. The library path calls this for each Def
        /// whose region scene is loaded; a test calls it with its own hull. False (with the reason logged) when the
        /// Def cannot make a day or is already bound.
        /// </summary>
        public bool TryBind(NpcPotDayDef def, MooredBoat hull)
        {
            if (def == null || hull == null) return false;
            if (Find(def.Id) != null) return false;
            string problem = NpcPotDay.Problem(def);
            if (problem != null)
            {
                Debug.LogWarning($"[NpcPotBoats] '{def.Id}' cannot work a day: {problem}. The boat stays at her berth.");
                return false;
            }

            Vector2 lies = hull.transform.position;
            if ((lies - def.BerthPosition).magnitude > BerthMismatchMeters)
            {
                Debug.LogWarning($"[NpcPotBoats] '{def.Id}': the scene moors her at {lies} but the day's berth is " +
                                 $"{def.BerthPosition}. The day's berth is used; re-author the Def if the berth moved.");
            }

            var boat = new Boat
            {
                Def = def,
                Hull = hull,
                HullTransform = hull.transform,
                Skipper = hull.GetComponentInChildren<IsoCharacterSprite>(true),
                Z = hull.transform.position.z,
                GearRoot = new GameObject("NpcPotGear_" + def.Id),
                Pots = new Pot[def.PotCount],
                LastHeading = float.NaN,
            };
            boat.GearRoot.transform.SetParent(transform, worldPositionStays: true);
            boat.SoakGradient = FadingRope(def.RopeColour);
            boat.HaulGradient = SolidRope(def.RopeColour);
            for (int i = 0; i < boat.Pots.Length; i++) boat.Pots[i] = BuildPot(boat, i);
            boat.Deck = new NpcPotDeck(def, hull, boat.Skipper);
            boat.Walk = new NpcPotLandingWalk(def, hull.Owner != null ? hull.Owner.Skipper : null,
                                              boat.Skipper != null ? boat.Skipper.GetComponent<SpriteRenderer>() : null,
                                              hull.transform, boat.GearRoot.transform, boat.Z);
            _boats.Add(boat);
            _nextSlowTick = 0f;   // read the tide and lay the gear on the very next frame
            return true;
        }

        /// <summary>Stops working <paramref name="defId"/>'s day, puts the boat back at her berth and removes her gear.</summary>
        public void Unbind(string defId)
        {
            Boat boat = Find(defId);
            if (boat == null) return;
            Release(boat);
            _boats.Remove(boat);
        }

        // ---- the frame ------------------------------------------------------------------------------

        private void Update() => Tick();

        /// <summary>One frame of every bound boat: hull, deck, landing and gear, from the clock. Update
        /// calls it; public so a frame can be timed on its own.</summary>
        public void Tick()
        {
            if (!GameServices.Ready) return;

            bool slowTick = Time.unscaledTime >= _nextSlowTick;
            if (slowTick)
            {
                _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
                DropDeadBindings();
                if (AutoBind) BindFromLibrary();
            }
            if (_boats.Count == 0) return;

            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            double now = clock.TotalSeconds;
            double secondsPerDay = GameServices.SecondsPerDay;
            int seed = env.WorldSeed;
            int dayIndex = NpcPotDay.DayIndexOf(now, secondsPerDay);

            float level = 0f;
            if (slowTick)
            {
                level = env.WaterLevelAt(now);
                _terrain = GameServices.TidalTerrain;
            }

            for (int b = 0; b < _boats.Count; b++)
            {
                Boat boat = _boats[b];
                if (boat.Today == null || boat.Today.DayIndex != dayIndex || boat.Seed != seed
                    || boat.SecondsPerDay != secondsPerDay)
                {
                    boat.Today = NpcPotDay.Build(boat.Def, seed, dayIndex, secondsPerDay);
                    boat.Yesterday = NpcPotDay.Build(boat.Def, seed, dayIndex - 1, secondsPerDay);
                    boat.Catch = NpcPotDayCatch.Build(boat.Def, seed, boat.Today, boat.Yesterday, clock.Season,
                                                      env.WaterLevelAt, secondsPerDay);
                    boat.Landing = NpcPotLanding.Build(boat.Def, boat.Today, boat.Catch);
                    boat.Seed = seed;
                    boat.SecondsPerDay = secondsPerDay;
                    if (!boat.Today.FitsTheDay(secondsPerDay))
                    {
                        Debug.LogWarning($"[NpcPotBoats] '{boat.Def.Id}': day {dayIndex} does not end before midnight " +
                                         "at this day length. Shorten the string or the job, or leave earlier.");
                    }
                }

                if (slowTick)
                {
                    float before = env.WaterLevelAt(now - TideProbeClockSeconds);
                    float after = env.WaterLevelAt(now + TideProbeClockSeconds);
                    float rise = PotRopeMath.RiseMetersPerHour(before, after, 2.0 * TideProbeClockSeconds, secondsPerDay);
                    boat.WaterLevel = level;
                    boat.Stream = PotRopeMath.Stream(rise, boat.Def.FullStreamRiseMetersPerHour);
                }

                NpcPotBoatPose pose = boat.Today.PoseAt(now);
                PlaceHull(boat, pose);
                boat.Deck.Tick(in pose, boat.Today, boat.Catch, now, boat.Landing);
                boat.Walk.Tick(boat.Landing, now);

                for (int i = 0; i < boat.Pots.Length; i++)
                {
                    Pot pot = boat.Pots[i];
                    NpcPotState state = NpcPotDay.PotAt(boat.Today, boat.Yesterday, i, now);
                    bool changed = state.Phase != pot.Phase || state.Seabed != pot.Seabed || !pot.Laid;
                    if (slowTick || changed)
                    {
                        pot.BedElevation = _terrain != null
                            ? _terrain.ElevationAt(state.Seabed)
                            : boat.WaterLevel - boat.Def.Trap.MinSoakDepthMeters;
                    }
                    if (!slowTick && !changed && state.Phase == NpcPotPhase.Soaking) continue;   // nothing moves it
                    DrawPot(boat, pot, i, state, pose, changed);
                }
            }
        }

        private void DropDeadBindings()
        {
            for (int i = _boats.Count - 1; i >= 0; i--)
            {
                if (_boats[i].Hull != null) continue;   // her scene is still loaded
                Release(_boats[i]);
                _boats.RemoveAt(i);
            }
        }

        private void BindFromLibrary()
        {
            if (_library == null || _library.Days == null) return;
            MooredBoat[] moored = null;
            for (int d = 0; d < _library.Days.Length; d++)
            {
                NpcPotDayDef def = _library.Days[d];
                if (def == null || Find(def.Id) != null) continue;
                Scene scene = SceneManager.GetSceneByName(def.RegionSceneName);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                moored ??= FindObjectsByType<MooredBoat>();
                for (int m = 0; m < moored.Length; m++)
                {
                    MooredBoat hull = moored[m];
                    if (hull == null || hull.Owner == null || hull.Owner.Id != def.OwnerId) continue;
                    if (hull.gameObject.scene != scene) continue;
                    TryBind(def, hull);
                    break;
                }
            }
        }

        // ---- the hull -------------------------------------------------------------------------------

        private static void PlaceHull(Boat boat, in NpcPotBoatPose pose)
        {
            bool atBerth = pose.Stage == NpcPotBoatStage.AtBerth;
            if (atBerth && boat.RestingAtBerth) return;   // moored: written once, then left alone
            boat.RestingAtBerth = atBerth;

            boat.HullTransform.position = new Vector3(pose.Position.x, pose.Position.y, boat.Z);
            if (float.IsNaN(boat.LastHeading)
                || Mathf.Abs(Mathf.DeltaAngle(boat.LastHeading, pose.HeadingDegrees)) > HeadingEpsilonDegrees)
            {
                boat.LastHeading = pose.HeadingDegrees;
                boat.HullTransform.rotation = Quaternion.Euler(0f, 0f, -pose.HeadingDegrees);   // the deck faces her hand
            }

            if (pose.UnderWay != boat.UnderWay)
            {
                boat.UnderWay = pose.UnderWay;
                boat.Hull.SetWay(pose.UnderWay ? VesselWay.UnderWay : VesselWay.Moored);
            }
        }

        // ---- one pot --------------------------------------------------------------------------------

        private void DrawPot(Boat boat, Pot pot, int index, in NpcPotState state, in NpcPotBoatPose pose, bool changed)
        {
            NpcPotDayDef def = boat.Def;
            pot.Laid = true;
            pot.Seabed = state.Seabed;
            NpcPotPhase was = pot.Phase;
            pot.Phase = state.Phase;

            float depth = Mathf.Max(0.05f, boat.WaterLevel - pot.BedElevation);
            Vector2 afloat = state.Seabed + PotRopeMath.FloatOffset(def.RopeLengthMeters, depth, boat.Stream,
                                                                    def.FloodBearingDegrees, def.StreamLean01);
            Vector2 onTheBottom = PotRopeMath.DrawnAtDepth(state.Seabed, depth, def.DepthDrawFraction);
            Vector2 rail = pose.Position + NpcPotDay.Starboard(pose.HeadingDegrees) * def.HaulerLocalMeters.x
                                         + NpcPotDay.Along(pose.HeadingDegrees) * def.HaulerLocalMeters.y;

            switch (state.Phase)
            {
                case NpcPotPhase.Soaking:
                    ShowBuoy(pot, afloat);
                    ShowRope(pot, boat.SoakGradient, changed, afloat, onTheBottom, taut01: 1f, def);
                    HidePot(pot);
                    break;

                case NpcPotPhase.Gaffed:
                {
                    Vector2 buoy = Vector2.Lerp(afloat, rail, Mathf.SmoothStep(0f, 1f, state.Phase01));
                    ShowBuoy(pot, buoy);
                    ShowRope(pot, boat.SoakGradient, changed, buoy, onTheBottom, taut01: 1f, def);
                    HidePot(pot);
                    break;
                }

                case NpcPotPhase.Hauling:
                {
                    float below = depth * (1f - state.Phase01);
                    Vector2 rising = PotRopeMath.DrawnAtDepth(Vector2.Lerp(state.Seabed, rail, state.Phase01), below,
                                                              def.DepthDrawFraction);
                    HideBuoy(pot);
                    ShowRope(pot, boat.HaulGradient, changed, rail, rising, taut01: 1f, def);
                    ShowPot(pot, def.Trap.TrapSpriteWet, rising,
                            PotRopeMath.SeenThroughWater01(below, def.SeenToDepthMeters));
                    break;
                }

                case NpcPotPhase.Aboard:
                    HideBuoy(pot);
                    HideRope(pot);
                    ShowPot(pot, def.Trap.TrapSprite, rail, 1f);
                    break;

                case NpcPotPhase.Setting:
                {
                    if (was != NpcPotPhase.Setting && was != NpcPotPhase.Soaking && def.Trap.SplashBurstFrames != null
                        && def.Trap.SplashBurstFrames.Length > 0)
                    {
                        TrapSplashFx.Play(def.Trap.SplashBurstFrames, def.Trap.SplashBurstFps, rail);
                    }

                    float below = depth * state.Phase01;
                    Vector2 sinking = PotRopeMath.DrawnAtDepth(Vector2.Lerp(rail, state.Seabed, state.Phase01), below,
                                                               def.DepthDrawFraction);
                    ShowPot(pot, def.Trap.TrapSpriteWet, sinking,
                            PotRopeMath.SeenThroughWater01(below, def.SeenToDepthMeters));
                    if (state.Phase01 < BuoyTossAt01)
                    {
                        HideBuoy(pot);
                        ShowRope(pot, boat.HaulGradient, changed || pot.BuoyOut, rail, sinking,
                                 taut01: 1f - state.Phase01, def);
                        pot.BuoyOut = false;
                    }
                    else
                    {
                        float thrown = Mathf.InverseLerp(BuoyTossAt01, 1f, state.Phase01);
                        Vector2 buoy = Vector2.Lerp(rail, afloat, Mathf.SmoothStep(0f, 1f, thrown));
                        ShowBuoy(pot, buoy);
                        ShowRope(pot, boat.SoakGradient, changed || !pot.BuoyOut, buoy, sinking, taut01: 1f, def);
                        pot.BuoyOut = true;
                    }
                    break;
                }
            }
        }

        private static void ShowBuoy(Pot pot, Vector2 at)
        {
            pot.BuoyRoot.transform.position = new Vector3(at.x, at.y, 0f);
            if (pot.BuoyRoot.activeSelf) return;
            pot.BuoyRoot.SetActive(true);
            pot.Wave.enabled = true;
        }

        private static void HideBuoy(Pot pot)
        {
            if (!pot.BuoyRoot.activeSelf) return;
            pot.Wave.enabled = false;
            pot.BuoyRoot.SetActive(false);
        }

        private void ShowRope(Pot pot, Gradient gradient, bool restyle, Vector2 from, Vector2 to, float taut01,
                              NpcPotDayDef def)
        {
            LineRenderer rope = pot.Rope;
            if (!rope.enabled || restyle)
            {
                rope.colorGradient = gradient;
                rope.positionCount = RopePoints;
                rope.enabled = true;
            }
            TrapHaulMath.SampleHaulRope(from, to, taut01, def.HaulRopeSagMeters, _ropeBuffer);
            for (int i = 0; i < RopePoints; i++)
                rope.SetPosition(i, new Vector3(_ropeBuffer[i].x, _ropeBuffer[i].y, 0f));
        }

        private static void HideRope(Pot pot)
        {
            if (pot.Rope.enabled) pot.Rope.enabled = false;
        }

        private static void ShowPot(Pot pot, Sprite sprite, Vector2 at, float alpha)
        {
            SpriteRenderer r = pot.PotRenderer;
            if (r.sprite != sprite) r.sprite = sprite;
            r.transform.position = new Vector3(at.x, at.y, 0f);
            Color c = r.color;
            if (!Mathf.Approximately(c.a, alpha)) { c.a = alpha; r.color = c; }
            bool seen = alpha > 0.01f && sprite != null;
            if (r.enabled != seen) r.enabled = seen;
        }

        private static void HidePot(Pot pot)
        {
            if (pot.PotRenderer.enabled) pot.PotRenderer.enabled = false;
        }

        // ---- building and releasing ------------------------------------------------------------------

        private Pot BuildPot(Boat boat, int index)
        {
            NpcPotDayDef def = boat.Def;
            var pot = new Pot { Phase = NpcPotPhase.Soaking };

            pot.BuoyRoot = new GameObject($"NpcPotBuoy_{index}");
            pot.BuoyRoot.transform.SetParent(boat.GearRoot.transform, worldPositionStays: true);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(pot.BuoyRoot.transform, worldPositionStays: false);
            var buoyRenderer = visual.AddComponent<SpriteRenderer>();
            buoyRenderer.sprite = def.Trap.BuoySprite;
            buoyRenderer.color = def.BuoyColour;          // whose gear it is; set before the wave rider caches it
            buoyRenderer.sortingOrder = def.BuoySortingOrder;
            pot.Wave = pot.BuoyRoot.AddComponent<BuoyWaveVisual>();   // the proven wave rider, reused unchanged
            pot.Wave.Configure(buoyRenderer, visual.transform);
            pot.Wave.enabled = false;
            pot.BuoyRoot.SetActive(false);

            var ropeGo = new GameObject($"NpcPotRope_{index}");
            ropeGo.transform.SetParent(boat.GearRoot.transform, worldPositionStays: true);
            pot.Rope = ropeGo.AddComponent<LineRenderer>();
            pot.Rope.useWorldSpace = true;
            pot.Rope.numCapVertices = 2;
            pot.Rope.startWidth = def.RopeWidthMeters;
            pot.Rope.endWidth = def.RopeWidthMeters;
            pot.Rope.sortingOrder = def.RopeSortingOrder;
            Material material = RopeMaterial();
            if (material != null) pot.Rope.sharedMaterial = material;
            pot.Rope.positionCount = 0;
            pot.Rope.enabled = false;

            var potGo = new GameObject($"NpcPot_{index}");
            potGo.transform.SetParent(boat.GearRoot.transform, worldPositionStays: true);
            pot.PotRenderer = potGo.AddComponent<SpriteRenderer>();
            pot.PotRenderer.sortingOrder = def.RopeSortingOrder;
            pot.PotRenderer.enabled = false;
            return pot;
        }

        private Material RopeMaterial()
        {
            if (_ropeMaterial != null) return _ropeMaterial;
            Shader shader = Shader.Find("Sprites/Default");   // the player's haul rope uses the same
            if (shader == null) return null;                  // headless: the line still carries its points
            _ropeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _ropeMaterial;
        }

        private static Gradient FadingRope(Color colour)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(colour.a, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Gradient SolidRope(Color colour)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(colour.a, 0f), new GradientAlphaKey(colour.a, 1f) });
            return gradient;
        }

        private static void Release(Boat boat)
        {
            boat.Walk?.Dispose();
            if (boat.GearRoot != null) Destroy(boat.GearRoot);
            if (boat.Hull == null) return;   // her scene went with her
            boat.Deck?.Dispose();
            NpcPotDayDef def = boat.Def;
            boat.HullTransform.position = new Vector3(def.BerthPosition.x, def.BerthPosition.y, boat.Z);
            boat.HullTransform.rotation = Quaternion.Euler(0f, 0f, -def.BerthHeadingDegrees);
            if (boat.Skipper != null) boat.Skipper.HoldHeading(def.BerthHeadingDegrees);
            if (boat.UnderWay) boat.Hull.SetWay(VesselWay.Moored);
        }

        private Boat Find(string defId)
        {
            for (int i = 0; i < _boats.Count; i++)
                if (_boats[i].Def != null && _boats[i].Def.Id == defId) return _boats[i];
            return null;
        }

        // ---- run-time shapes (allocated at binding only) ----------------------------------------------

        private sealed class Boat
        {
            public NpcPotDayDef Def;
            public MooredBoat Hull;
            public Transform HullTransform;
            public IsoCharacterSprite Skipper;
            public float Z;
            public GameObject GearRoot;
            public Pot[] Pots;
            public Gradient SoakGradient, HaulGradient;

            public NpcPotDeck Deck;
            public NpcPotDay Today, Yesterday;
            public NpcPotDayCatch Catch;
            public NpcPotLanding Landing;
            public NpcPotLandingWalk Walk;
            public int Seed;
            public double SecondsPerDay;

            public float WaterLevel, Stream;
            public float LastHeading;
            public bool UnderWay, RestingAtBerth;
        }

        private sealed class Pot
        {
            public GameObject BuoyRoot;
            public BuoyWaveVisual Wave;
            public LineRenderer Rope;
            public SpriteRenderer PotRenderer;

            public NpcPotPhase Phase;
            public Vector2 Seabed;
            public float BedElevation;
            public bool Laid, BuoyOut;
        }
    }
}
