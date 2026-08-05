using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Ticks <see cref="NavTrackRecorder"/> once a frame, unconditionally.
    ///
    /// <para>That word is the whole point. This host does not ask whether a chartplotter is fitted,
    /// owned, or on screen — the track is a record of the voyage, not an output of the instrument
    /// (ADR 0025 S6, the honesty invariant). Gating it on the glass would make "where I have been"
    /// something you only have if you bought the thing that draws it.</para>
    ///
    /// <para><b>Self-installing</b> (the SaveService/HelmOverlayHost pattern): one persistent host per
    /// play session, so no scene needs a builder re-run. Headless-safe — with no save, no region or no
    /// boat the tick is a few null checks and returns.</para>
    ///
    /// <para><b>⚠ FLAG lead-architect: lane, not design.</b> The recording RULE and every input it reads
    /// are pure Core (<see cref="NavTrackRecorder"/>, <see cref="NavLocker.RecordTrack"/>); this file is
    /// nothing but the frame that calls it, and it sits in the UI lane only because that was this
    /// slice's fence. If the sim lane should own the driver, moving it is deleting this file and adding
    /// one line elsewhere — nothing in Core changes, and the tests never touch this class.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavTrackRecorderHost : MonoBehaviour
    {
        private static NavTrackRecorderHost _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("NavTrackRecorderHost");
            _instance = go.AddComponent<NavTrackRecorderHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            // A host that has just come up has no business trusting a cached crumb from a previous
            // session's boat (the recorder's cache is static and outlives a scene reload).
            NavTrackRecorder.Reset();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update() => NavTrackRecorder.Tick();
    }
}
