using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The pure half of the fixture pose: where the figure is along a climb into (or out of) a fixture.
    /// </summary>
    public static class FixturePoseMath
    {
        /// <summary>
        /// The figure's pivot at <paramref name="t"/> (0..1) along a climb from <paramref name="from"/> to
        /// <paramref name="to"/>: a straight line on the ground with an arc of <paramref name="arcMetres"/> up the
        /// screen at its middle (he steps up over the rim and down into the water).
        /// </summary>
        public static Vector2 Climb(Vector2 from, Vector2 to, float t, float arcMetres)
        {
            t = Mathf.Clamp01(t);
            Vector2 p = Vector2.Lerp(from, to, t);
            p.y += Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, arcMetres);
            return p;
        }

        /// <summary>Where the seated pose's pivot goes: the seat's ground point, raised or lowered by the
        /// difference between the fixture's seat and the height the pose was baked sitting on.</summary>
        public static Vector2 SeatPivot(Vector2 seatGround, float seatHeightMetres, float bakedSeatMetres)
            => new Vector2(seatGround.x, seatGround.y + (seatHeightMetres - bakedSeatMetres) * IsoGround.HeightScale);
    }

    /// <summary>
    /// <b>Puts the player's figure into a bath, onto a seat, or under a shower</b> when a fixture asks
    /// (<see cref="FixturePoseRequested"/>, owner ask 2026-11-17: "a player getting inside animation").
    ///
    /// <list type="bullet">
    /// <item><b>Bathe</b>: he steps over the rim (the boarding step, stretched to the climb time) on an arc from
    /// where he stands to the seat, then sits low in the water in the seated pose; leaving reverses it.</item>
    /// <item><b>Sit</b>: he sits on the seat in the seated pose; getting up puts him back in front of it.</item>
    /// <item><b>Stand</b>: he steps onto the spot under the rose and stands there.</item>
    /// </list>
    ///
    /// <para>While posed the walk controller is held still, exactly as the sleep beat holds it (and only handed
    /// back on if this switched it off). The interact press still reaches the fixture, so pressing again ends
    /// the wash or gets him up, which is what sends the Off. The seated pose is the drive clip held on its first
    /// frame, lifted against the height it was baked sitting on (the off-deck mount contract's
    /// <c>DriveSeatZ</c>, else <see cref="WashVisualsSettings.FallbackBakedSeatMetres"/>). With no clip art the
    /// figure is still moved and held, just in its standing picture.</para>
    ///
    /// <para>Presentation only: nothing here is saved, and a reload stands him up.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFixturePosePresenter : MonoBehaviour
    {
        public enum State { Idle, ClimbIn, Posed, ClimbOut }

        private CharacterClipPlayer _clip;
        private PlayerWalkController _walk;
        private PlayerDrivePresenter _drive;
        private bool _walkWasEnabled;

        private State _state;
        private FixturePoseRequested _request;
        private Vector2 _from, _to;
        private float _t;
        private float _z;

        public State Current => _state;
        public bool IsPosed => _state != State.Idle;
        public FixturePose Pose => _state == State.Idle ? FixturePose.None : _request.Pose;

        private void Awake()
        {
            _clip = GetComponent<CharacterClipPlayer>();
            _walk = GetComponent<PlayerWalkController>();
            _drive = GetComponent<PlayerDrivePresenter>();
        }

        private void OnEnable() => EventBus.Subscribe<FixturePoseRequested>(OnRequested);

        private void OnDisable()
        {
            EventBus.Unsubscribe<FixturePoseRequested>(OnRequested);
            if (_state != State.Idle) Finish(_request.EntryWorld);
        }

        private void OnRequested(FixturePoseRequested e)
        {
            if (e.Pose == FixturePose.None) return;
            if (e.On) Begin(e);
            else if (_state != State.Idle && e.FixtureId == _request.FixtureId) Leave();
        }

        private void Begin(in FixturePoseRequested e)
        {
            if (_state != State.Idle) Finish(_request.EntryWorld);   // a new fixture: drop the old one clean
            _request = e;
            _z = transform.position.z;
            _walkWasEnabled = _walk != null && _walk.enabled;
            if (_walkWasEnabled) _walk.enabled = false;

            Vector2 seat = SeatPivot(e);
            if (e.Pose == FixturePose.Bathe)
            {
                // From where he stands (he pressed from somewhere in reach) over the rim to the seat.
                _from = transform.position; _to = seat; _t = 0f;
                _state = State.ClimbIn;
                PlayClimb(_from, seat);
                Place(_from);
            }
            else
            {
                _state = State.Posed;
                Place(seat);
                PlaySeated(e);
            }
        }

        private void Leave()
        {
            if (_request.Pose == FixturePose.Bathe)
            {
                // Out from wherever he is, even part way in.
                _from = transform.position; _to = _request.EntryWorld;
                _t = 0f;
                _state = State.ClimbOut;
                PlayClimb(_from, _to);
                return;
            }
            Finish(_request.Pose == FixturePose.Stand ? (Vector2)transform.position : _request.EntryWorld);
        }

        private void LateUpdate()
        {
            if (_state == State.Idle) return;
            WashVisualsSettings s = WashVisualsSettings.Current;

            switch (_state)
            {
                case State.ClimbIn:
                case State.ClimbOut:
                    _t += Time.deltaTime / Mathf.Max(0.05f, s.ClimbSeconds);
                    Place(FixturePoseMath.Climb(_from, _to, _t, s.ClimbArcMetres));
                    if (_t >= 1f)
                    {
                        if (_state == State.ClimbIn) { _state = State.Posed; PlaySeated(_request); }
                        else Finish(_to);
                    }
                    break;
                case State.Posed:
                    // Held: anything that nudged him this frame is put back.
                    Place(_request.Pose == FixturePose.Stand ? _request.SeatWorld : SeatPivot(_request));
                    break;
            }
        }

        private void Finish(Vector2 at)
        {
            _state = State.Idle;
            Place(at);
            if (_clip != null && (_clip.Clip == CharacterClip.Drive || _clip.Clip == CharacterClip.Board)) _clip.Stop();
            if (_walkWasEnabled && _walk != null) _walk.enabled = true;
            _walkWasEnabled = false;
        }

        private Vector2 SeatPivot(in FixturePoseRequested e)
        {
            if (e.Pose == FixturePose.Stand) return e.SeatWorld;
            float baked = _drive != null && _drive.Mounts != null
                ? _drive.Mounts.DriveSeatZ
                : WashVisualsSettings.Current.FallbackBakedSeatMetres;
            return FixturePoseMath.SeatPivot(e.SeatWorld, e.SeatHeightMetres, baked);
        }

        private void PlayClimb(Vector2 from, Vector2 to)
        {
            if (_clip == null || !_clip.CanPlay(CharacterClip.Board)) return;
            _clip.Play(CharacterClip.Board, IsoGround.BearingDegrees(from, to),
                       WashVisualsSettings.Current.ClimbSeconds, holdOnFinish: true);
        }

        private void PlaySeated(in FixturePoseRequested e)
        {
            if (e.Pose == FixturePose.Stand)
            {
                if (_clip != null && _clip.Clip == CharacterClip.Board) _clip.Stop();
                return;
            }
            if (_clip == null || !_clip.CanPlay(CharacterClip.Drive)) return;
            _clip.PlayDriven(CharacterClip.Drive, e.HeadingDegrees, 0f);   // held on its first frame
        }

        // Keep z: the iso sort band lives there and a 2D position assignment would flatten it.
        private void Place(Vector2 p) => transform.position = new Vector3(p.x, p.y, _z);
    }
}
