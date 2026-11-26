using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Where a keeper is, from the clock alone.</b> A keeper lives at a post away from the village: a handful
    /// of places a few steps apart, and a day that names them by the hour (a <see cref="RoutineDef"/>). At each
    /// block's hour they set off from the last place straight to the next and stand there. Pure arithmetic, so
    /// the same hour always gives the same place and nothing is saved.
    /// </summary>
    public static class KeeperDay
    {
        public readonly struct Pose
        {
            public readonly Vector2 At;
            public readonly float HeadingDegrees;
            public readonly bool Hidden, Walking;
            public Pose(Vector2 at, float headingDegrees, bool hidden, bool walking)
            { At = at; HeadingDegrees = headingDegrees; Hidden = hidden; Walking = walking; }
        }

        /// <summary>The block the hour falls in; before the day's first block it is still last night's.</summary>
        public static int BlockAt(RoutineEntry[] entries, float hour)
        {
            int at = entries.Length - 1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].StartHour <= hour) at = i; else break;
            return at;
        }

        static int StopOf(RoutineStationEntry[] stops, string stationId)
        {
            for (int i = 0; i < stops.Length; i++)
                if (string.Equals(stops[i].Id, stationId, System.StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>A compass heading (0 north, clockwise) for a step on the ground.</summary>
        public static float HeadingOf(Vector2 step) => Mathf.Repeat(Mathf.Atan2(step.x, step.y) * Mathf.Rad2Deg, 360f);

        /// <summary>
        /// The keeper at <paramref name="hour"/> (0..24). False when the day or the post cannot say: fewer than
        /// two blocks, or a block naming a place the post does not have.
        /// </summary>
        public static bool TryPose(RoutineEntry[] entries, RoutineStationEntry[] stops, float hour, float secondsPerHour,
            float walkSpeed, out Pose pose)
        {
            pose = default;
            if (entries == null || stops == null || entries.Length < 2) return false;
            int block = BlockAt(entries, hour);
            int from = StopOf(stops, entries[(block + entries.Length - 1) % entries.Length].StationId);
            int to = StopOf(stops, entries[block].StationId);
            if (from < 0 || to < 0) return false;

            RoutineStationEntry a = stops[from], b = stops[to];
            float into = hour - entries[block].StartHour;
            if (into < 0f) into += RoutineSchedule.HoursPerDay;
            Vector2 leg = b.Position - a.Position;
            float length = leg.magnitude;
            float walked = into * secondsPerHour * walkSpeed;
            bool indoorsAllAlong = a.OutOfSight && b.OutOfSight;
            if (from == to || indoorsAllAlong || length <= Mathf.Epsilon || walked >= length)
            {
                pose = new Pose(b.Position, b.StandHeadingDegrees, b.OutOfSight, false);
                return true;
            }
            pose = new Pose(a.Position + leg * (walked / length), HeadingOf(leg), false, true);
            return true;
        }
    }

    /// <summary>
    /// A keeper in the scene: stands them where <see cref="KeeperDay"/> says each frame, hides them and their
    /// talk while they are indoors, and faces them the way the place asks once they have arrived.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeeperRound : MonoBehaviour
    {
        [Tooltip("The keeper's day: which place, from which hour.")]
        [SerializeField] private RoutineDef _routine;

        [Tooltip("The places of this keeper's post. Written by the region's layer refresh from the scene's own rows.")]
        [SerializeField] private RoutineStationEntry[] _stops = System.Array.Empty<RoutineStationEntry>();

        private SpriteRenderer _renderer;
        private Interactable _talkable;
        private IsoCharacterSprite _iso;
        private bool _headingHeld;
        private float _heldHeading;

        public RoutineDef Routine => _routine;
        public RoutineStationEntry[] Stops => _stops ?? System.Array.Empty<RoutineStationEntry>();

        /// <summary>True while the keeper is indoors: not drawn and not to be spoken to.</summary>
        public bool IsIndoors { get; private set; }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _talkable = GetComponent<Interactable>();
            _iso = GetComponent<IsoCharacterSprite>();
        }

        void OnDisable()
        {
            if (_headingHeld && _iso != null) _iso.ReleaseHeading();
            _headingHeld = false;
            Show(true);
        }

        void Update() => Stand();

        /// <summary>Put the keeper where the clock says. Public so a test can ask without waiting a frame.</summary>
        public void Stand()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null || _routine == null) return;
            float secondsPerHour = GameServices.SecondsPerDay / RoutineSchedule.HoursPerDay;
            if (!KeeperDay.TryPose(_routine.Entries, _stops, clock.HourOfDay, secondsPerHour,
                    _routine.WalkSpeedMetresPerSecond, out KeeperDay.Pose pose)) return;

            Vector3 at = transform.position;
            if (at.x != pose.At.x || at.y != pose.At.y) transform.position = new Vector3(pose.At.x, pose.At.y, at.z);
            Show(!pose.Hidden);
            if (_iso == null) return;
            if (pose.Walking)
            {
                if (_headingHeld) { _iso.ReleaseHeading(); _headingHeld = false; }
            }
            else if (!_headingHeld || _heldHeading != pose.HeadingDegrees)
            {
                _iso.HoldHeading(pose.HeadingDegrees);
                _headingHeld = true;
                _heldHeading = pose.HeadingDegrees;
            }
        }

        void Show(bool shown)
        {
            IsIndoors = !shown;
            if (_renderer != null && _renderer.enabled != shown) _renderer.enabled = shown;
            if (_talkable != null && _talkable.enabled != shown) _talkable.enabled = shown;
        }
    }
}
