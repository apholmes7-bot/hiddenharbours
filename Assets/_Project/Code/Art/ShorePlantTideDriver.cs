using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The ONE thing that ticks the shore — a single self-installing component that walks every
    /// registered <see cref="ShorePlantTideView"/> on the slow tick, so ~700 plants cost one
    /// <c>Update</c> between them instead of seven hundred.
    ///
    /// <para><b>⭐ WHY THIS EXISTS.</b> A per-plant <c>Update</c> that early-returns is not free: the
    /// engine still pays a managed dispatch per instance per frame, and St Peters places about seven
    /// hundred plants. That is the exact cost <see cref="YSortSprite"/> already disables itself to
    /// avoid ("a clearing of ~1300 tufts/trees costs literally nothing per frame instead of ~1300
    /// empty calls"), and it would be an odd discipline to keep on one decor layer and drop on the
    /// next. Rule 7: the performance budget is a feature.</para>
    ///
    /// <para><b>⚠ SPREAD ACROSS FRAMES, not done in one.</b> Refreshing all seven hundred on the same
    /// frame would be a periodic hitch — small, but a regular one, which is the kind the eye finds.
    /// The list is walked in slices (<see cref="MaxPerFrame"/>), so the whole shore is re-read every
    /// <see cref="SlowTickSeconds"/> without any single frame carrying more than a bounded chunk of
    /// it. A plant's own <c>Refresh</c> already returns immediately when its state has not changed, so
    /// the common case is a couple of service reads and a float compare.</para>
    ///
    /// <para><b>Self-installing, exactly like <c>GrassWindBridge</c>:</b> a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> host spawned BEFORE the first scene loads, hidden
    /// and <c>DontDestroyOnLoad</c>. No scene wiring and no builder step — and, importantly, no
    /// GameObject created from inside another component's <c>OnEnable</c>, which is a thing Unity
    /// tolerates unevenly during scene load. Registration is idempotent, and a destroyed view drops
    /// out on its own: the walk skips and removes null entries rather than trusting <c>OnDisable</c>
    /// to have fired (it does not, for an object destroyed by an editor Undo).</para>
    ///
    /// <para><b>Play mode only, and that costs nothing in the editor.</b> Edit mode has no clock — a
    /// view there reads its own preview slider and refreshes when that changes — so there is nothing
    /// for a driver to tick.</para>
    ///
    /// <para>Visual only: it drives sprite choice and sorting order and nothing else. No sim, no save
    /// (rule 5).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShorePlantTideDriver : MonoBehaviour
    {
        /// <summary>How often the whole shore is re-read. The water moves in metres per hour; twice a
        /// second is already far finer than the art can show, and it is the cadence
        /// <c>SeaweedPresenter</c> uses.</summary>
        public const float SlowTickSeconds = 0.5f;

        /// <summary>Upper bound on plants refreshed in one frame. 128 × 60 fps = 7,680 per second,
        /// which walks a 700-plant shore about eleven times over in the half-second window — so the
        /// slice bound never delays a state change, it only stops one frame carrying all of it.</summary>
        public const int MaxPerFrame = 128;

        static ShorePlantTideDriver s_instance;
        static readonly List<ShorePlantTideView> s_views = new List<ShorePlantTideView>();

        float _nextTick;
        int _cursor;

        /// <summary>How many plants this driver is currently walking — read by the density test and
        /// worth having in a bug report.</summary>
        public static int RegisteredCount => s_views.Count;

        public static void Register(ShorePlantTideView view)
        {
            if (view == null || s_views.Contains(view)) return;
            s_views.Add(view);
        }

        public static void Unregister(ShorePlantTideView view)
        {
            if (view == null) return;
            s_views.Remove(view);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            // Not saved and not shown: it is machinery, and a scene that stored it would get a second
            // one the next time a plant enabled in a fresh scene.
            var host = new GameObject("ShorePlantTideDriver") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<ShorePlantTideDriver>();
        }

        static bool s_installed;

        private void OnEnable()
        {
            if (s_instance == null) s_instance = this;
            _nextTick = 0f;
        }

        private void OnDisable()
        {
            if (s_instance == this) s_instance = null;
        }

        private void Update()
        {
            if (s_views.Count == 0) return;

            // Unscaled: a paused or slowed game should still settle the art it is showing.
            if (Time.unscaledTime < _nextTick && _cursor == 0) return;
            if (_cursor == 0) _nextTick = Time.unscaledTime + SlowTickSeconds;

            int end = Mathf.Min(s_views.Count, _cursor + MaxPerFrame);
            for (int i = _cursor; i < end; i++)
            {
                var v = s_views[i];
                // == null on purpose, never ?. — a destroyed UnityEngine.Object is "fake null" and the
                // null-conditional operator sails straight past it into a MissingReferenceException.
                if (v == null) continue;
                v.Refresh();
            }

            _cursor = end >= s_views.Count ? 0 : end;
            if (_cursor == 0) s_views.RemoveAll(v => v == null);
        }
    }
}
