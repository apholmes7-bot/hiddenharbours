using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>The NPC trawlers on passage</b> (P3, a living working coast): each morning a trawler lets go at her
    /// berth, goes out of her harbour, crosses to her ground, works it and comes home. This is only the
    /// PRESENTER. Where she is at any second is <see cref="NpcTrawlDay"/>, a pure function of
    /// <c>(worldSeed, clock)</c>; this class draws her hull in whichever of her two regions is loaded and hides
    /// it when the day has her in the other. Nothing is saved and nothing steers (rule 5).
    ///
    /// <para><b>No scene edit.</b> The hull is built here at run time, in the scene of the region she is seen
    /// in, and goes with that scene when it unloads, like any boat authored there; she is built again the next
    /// time a region she is in loads. Nothing is allocated per frame (rule 7): which scenes are loaded is read
    /// on the slow tick, and a hull is built only when she comes into view.</para>
    ///
    /// <para><b>Why App.</b> The day is Fishing's and the hull is Boats'; neither may name the other (rule 4).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcTrawlBoats : MonoBehaviour
    {
        private const float SlowTickSeconds = 0.25f;        // which scenes are loaded; new fleet entries
        private const float HeadingEpsilonDegrees = 0.05f;

        private sealed class Boat
        {
            public NpcTrawlDayDef Def;
            public BoatHullDef Hull;
            public HullPaintSchemeDef Paint;
            public GameObject Root;             // null until she is first seen, and again when her scene unloads
            public bool RootOnTheOpenWater;     // which of her two regions' scenes Root was built in
            public NpcTrawlDay Today;
            public int Seed;
            public double SecondsPerDay;
            public bool HarbourLoaded, GroundLoaded;
            public bool Shown;
            public float LastHeading = float.NaN;
        }

        private static NpcTrawlBoats _instance;
        private readonly List<Boat> _boats = new List<Boat>(4);
        private NpcTrawlFleet _fleet;
        private readonly List<string> _refused = new List<string>(2);   // fleet entries that cannot be drawn: warned once
        private float _nextSlowTick;

        /// <summary>The presenter, or null before the first scene has loaded.</summary>
        public static NpcTrawlBoats Instance => _instance;

        /// <summary>False stops the presenter adding the committed fleet (a test binds its own).</summary>
        public bool AutoBind { get; set; } = true;

        /// <summary>How many trawlers are bound.</summary>
        public int Count => _boats.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[NpcTrawlBoats]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NpcTrawlBoats>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _fleet = Resources.Load<NpcTrawlFleet>(NpcTrawlFleet.ResourcesPath);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Binds one trawler: her day, her hull and her paint. The fleet path calls this for every entry; a test
        /// calls it with its own. False (with the reason logged) when the Def cannot make a day, has no hull to
        /// draw, or is already bound.
        /// </summary>
        public bool TryBind(NpcTrawlDayDef def, BoatHullDef hull, HullPaintSchemeDef paint)
        {
            if (def == null || Find(def.Id) != null) return false;
            string problem = NpcTrawlDay.Problem(def);
            if (problem != null)
            {
                Debug.LogWarning($"[NpcTrawlBoats] '{def.Id}' cannot work a day: {problem}. She is not drawn.");
                return false;
            }
            if (hull == null || hull.Visual == null)
            {
                Debug.LogWarning($"[NpcTrawlBoats] '{def.Id}' has no hull with a visual. She is not drawn.");
                return false;
            }

            _boats.Add(new Boat { Def = def, Hull = hull, Paint = paint });
            _nextSlowTick = 0f;
            return true;
        }

        /// <summary>Drops a binding and destroys its hull.</summary>
        public void Unbind(string defId)
        {
            for (int i = _boats.Count - 1; i >= 0; i--)
            {
                if (_boats[i].Def == null || _boats[i].Def.Id != defId) continue;
                if (_boats[i].Root != null) Destroy(_boats[i].Root);
                _boats.RemoveAt(i);
            }
        }

        /// <summary>The hull drawn for <paramref name="defId"/>, or null when it is not bound or she has not
        /// yet been seen in a loaded region.</summary>
        public Transform HullOf(string defId) => Find(defId)?.Root != null ? Find(defId).Root.transform : null;

        /// <summary>True when <paramref name="defId"/>'s hull is drawn this frame.</summary>
        public bool IsShown(string defId)
        {
            Boat boat = Find(defId);
            return boat != null && boat.Shown;
        }

        /// <summary>The day built for <paramref name="defId"/>, or null before the first tick.</summary>
        public NpcTrawlDay DayOf(string defId) => Find(defId)?.Today;

        private void Update() => Tick();

        /// <summary>One frame of every bound trawler, from the clock. Update calls it; public so a frame can
        /// be timed or stepped on its own.</summary>
        public void Tick()
        {
            if (!GameServices.Ready) return;

            bool slowTick = Time.unscaledTime >= _nextSlowTick;
            if (slowTick)
            {
                _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
                if (AutoBind) BindFromFleet();
            }
            if (_boats.Count == 0) return;

            double now = GameServices.Clock.TotalSeconds;
            double secondsPerDay = GameServices.SecondsPerDay;
            int seed = GameServices.Environment.WorldSeed;
            int dayIndex = NpcPotDay.DayIndexOf(now, secondsPerDay);

            for (int b = 0; b < _boats.Count; b++)
            {
                Boat boat = _boats[b];
                if (boat.Today == null || boat.Today.DayIndex != dayIndex || boat.Seed != seed
                    || boat.SecondsPerDay != secondsPerDay)
                {
                    boat.Today = NpcTrawlDay.Build(boat.Def, seed, dayIndex, secondsPerDay);
                    boat.Seed = seed;
                    boat.SecondsPerDay = secondsPerDay;
                    if (!boat.Today.FitsTheDay(secondsPerDay))
                    {
                        Debug.LogWarning($"[NpcTrawlBoats] '{boat.Def.Id}': day {dayIndex} does not end before " +
                                         "midnight at this day length. Shorten her hours on the ground or leave earlier.");
                    }
                }

                if (slowTick)
                {
                    boat.HarbourLoaded = Loaded(boat.Def.HarbourSceneName);
                    boat.GroundLoaded = Loaded(boat.Def.GroundSceneName);
                }

                NpcTrawlBoatPose pose = boat.Today.PoseAt(now);
                bool show = pose.OnTheOpenWater ? boat.GroundLoaded : boat.HarbourLoaded;
                if (show && (boat.Root == null || boat.RootOnTheOpenWater != pose.OnTheOpenWater))
                    show = BuildHull(boat, pose.OnTheOpenWater);
                // A hull someone else has switched off (a region being left switches off its roots) stays off.
                if (show != boat.Shown)
                {
                    boat.Shown = show;
                    if (boat.Root != null) boat.Root.SetActive(show);
                    boat.LastHeading = float.NaN;
                }
                if (!show) continue;

                Transform t = boat.Root.transform;
                t.position = new Vector3(pose.Position.x, pose.Position.y, t.position.z);
                if (float.IsNaN(boat.LastHeading)
                    || Mathf.Abs(Mathf.DeltaAngle(boat.LastHeading, pose.HeadingDegrees)) > HeadingEpsilonDegrees)
                {
                    boat.LastHeading = pose.HeadingDegrees;
                    t.rotation = Quaternion.Euler(0f, 0f, -pose.HeadingDegrees);
                }
            }
        }

        /// <summary>
        /// Builds her hull in the scene of the region she is in, replacing one built in her other region. The
        /// hull belongs to that scene, so everything fitted to it (its shadow among them) unloads with it.
        /// False when that scene has gone since the slow tick last looked.
        /// </summary>
        private static bool BuildHull(Boat boat, bool onTheOpenWater)
        {
            Scene scene = SceneManager.GetSceneByName(onTheOpenWater ? boat.Def.GroundSceneName
                                                                     : boat.Def.HarbourSceneName);
            if (!scene.IsValid() || !scene.isLoaded) return false;
            if (boat.Root != null) Destroy(boat.Root);

            var root = new GameObject("NpcTrawler_" + boat.Def.Id);
            SceneManager.MoveGameObjectToScene(root, scene);
            BoatHullSkinner.Apply(root, boat.Hull.Visual, null,
                                  new BoatHullSkinner.Options { SkipOars = true, PaintScheme = boat.Paint });
            boat.Root = root;
            boat.RootOnTheOpenWater = onTheOpenWater;
            return true;
        }

        private void BindFromFleet()
        {
            if (_fleet == null || _fleet.Trawlers == null) return;
            for (int i = 0; i < _fleet.Trawlers.Length; i++)
            {
                NpcTrawlFleet.Trawler entry = _fleet.Trawlers[i];
                if (entry.Day == null || entry.Hull == null || Find(entry.Day.Id) != null) continue;
                if (!Loaded(entry.Day.HarbourSceneName) && !Loaded(entry.Day.GroundSceneName)) continue;
                if (_refused.Contains(entry.Day.Id)) continue;
                if (!TryBind(entry.Day, entry.Hull, entry.Paint)) _refused.Add(entry.Day.Id);
            }
        }

        private static bool Loaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private Boat Find(string defId)
        {
            for (int i = 0; i < _boats.Count; i++)
                if (_boats[i].Def != null && _boats[i].Def.Id == defId) return _boats[i];
            return null;
        }
    }
}
