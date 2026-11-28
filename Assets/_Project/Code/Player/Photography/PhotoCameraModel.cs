using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>What a press of the shutter came to.</summary>
    public enum PhotoShotOutcome
    {
        /// <summary>Too soon after the last picture: nothing happened.</summary>
        Cooling = 0,

        /// <summary>The frame held nothing the album can name: nothing kept.</summary>
        NothingInFrame = 1,

        /// <summary>The album is full: nothing kept.</summary>
        AlbumFull = 2,

        /// <summary>The picture is in the album.</summary>
        Kept = 3,
    }

    /// <summary>
    /// <b>The camera's own logic, with no scene in it</b>: up or down, where the frame is aimed, and what a
    /// press of the shutter keeps. The <see cref="PlayerPhotoCamera"/> behaviour feeds it the player's place,
    /// the intents and the clock; an EditMode test feeds it the same by hand.
    ///
    /// <para><b>Presentation until the shutter.</b> Raising, aiming and lowering change nothing but this
    /// object. Only <see cref="Shoot"/> touches the save, and only to add the one album row.</para>
    ///
    /// <para><b>No allocation per frame</b> (rule 7): the read buffers are made once, here; the shutter
    /// allocates the one row it keeps and the one line it shows.</para>
    /// </summary>
    public sealed class PhotoCameraModel
    {
        private readonly PhotoCameraDef _def;
        private readonly WildlifeSighting[] _scratch;
        private readonly WildlifeSighting[] _inFrame;
        private readonly PhotoSubjectResult[] _results;
        private Vector2 _lastFacing = Vector2.right;
        private float _readyAt;

        public PhotoCameraModel(PhotoCameraDef def)
        {
            _def = def;
            int read = def != null ? Mathf.Max(1, def.MaxSightingsRead) : 1;
            _scratch = new WildlifeSighting[read];
            _inFrame = new WildlifeSighting[read];
            _results = new PhotoSubjectResult[def != null ? Mathf.Max(1, def.MaxSubjectsPerPhoto) : 1];
        }

        public PhotoCameraDef Def => _def;

        /// <summary>True while the camera is up.</summary>
        public bool IsUp { get; private set; }

        /// <summary>The frame's centre less where he stands (world metres).</summary>
        public Vector2 Aim { get; private set; }

        /// <summary>The kinds of animal the last kept (or judged) picture held, best first.</summary>
        public PhotoSubjectResult[] LastResults => _results;

        /// <summary>How many of <see cref="LastResults"/> are meaningful.</summary>
        public int LastCount { get; private set; }

        /// <summary>The row the last press kept, or null.</summary>
        public PhotoRecordDto LastKept { get; private set; }

        /// <summary>Remember which way he faces (a zero facing, as on deck, keeps the last one).</summary>
        public void NoteFacing(Vector2 facing)
        {
            if (facing.sqrMagnitude > 1e-6f) _lastFacing = facing.normalized;
        }

        /// <summary>Lift the camera: the frame starts <see cref="PhotoCameraDef.StartReachMetres"/> ahead of
        /// the way he last faced.</summary>
        public void Raise()
        {
            if (_def == null) return;
            IsUp = true;
            Aim = PhotoScoring.ClampAim(_def, _lastFacing * _def.StartReachMetres);
        }

        public void Lower() => IsUp = false;

        /// <summary>Slide the frame by <paramref name="axis"/> (the aim keys, -1..1 a side) for
        /// <paramref name="dt"/> seconds, held inside the camera's reach.</summary>
        public void Steer(Vector2 axis, float dt)
        {
            if (!IsUp || _def == null || dt <= 0f) return;
            if (axis.sqrMagnitude > 1f) axis = axis.normalized;
            Aim = PhotoScoring.ClampAim(_def, Aim + axis * (_def.AimSpeedMetresPerSecond * dt));
        }

        /// <summary>The patch of world the picture takes in, for a camera at <paramref name="standingAt"/>.</summary>
        public Rect FrameFor(Vector2 standingAt) => PhotoScoring.FrameAt(_def, standingAt + Aim);

        /// <summary>
        /// Press the shutter, judging what the wildlife seam reports (<see cref="WildlifePresences"/>).
        /// </summary>
        public PhotoShotOutcome Shoot(SaveData save, string playerKey, Vector2 standingAt, double gameSeconds,
                                      string regionId, float now)
        {
            Rect frame = FrameFor(standingAt);
            int n = WildlifePresences.CopyInFrame(frame, _scratch, _inFrame);
            return Shoot(save, playerKey, standingAt, gameSeconds, regionId, now, _inFrame, n);
        }

        /// <summary>
        /// Press the shutter over a list of sightings the caller holds (what a test drives). Kept only when
        /// the camera is up, the cooldown has run, the frame holds a kind the album names and the album has
        /// room. <paramref name="now"/> is unscaled seconds, for the cooldown only: nothing about the
        /// picture depends on it, so it is the same picture on every machine (rule 5).
        /// </summary>
        public PhotoShotOutcome Shoot(SaveData save, string playerKey, Vector2 standingAt, double gameSeconds,
                                      string regionId, float now, WildlifeSighting[] sightings, int count)
        {
            LastKept = null;
            if (!IsUp || _def == null || now < _readyAt) return PhotoShotOutcome.Cooling;
            _readyAt = now + _def.ShutterCooldownSeconds;

            Rect frame = FrameFor(standingAt);
            LastCount = PhotoScoring.Evaluate(_def, standingAt, frame, sightings, count, _results);
            if (LastCount == 0) return PhotoShotOutcome.NothingInFrame;

            LastKept = PhotoAlbumRows.Add(save, playerKey, gameSeconds, regionId, _results, LastCount,
                                          _def.AlbumCapacity);
            return LastKept != null ? PhotoShotOutcome.Kept : PhotoShotOutcome.AlbumFull;
        }

        /// <summary>The words for what a press came to ("3 grey seals: a fine picture.").</summary>
        public string LineFor(PhotoShotOutcome outcome)
        {
            if (_def == null) return "";
            switch (outcome)
            {
                case PhotoShotOutcome.NothingInFrame: return _def.NothingInFrameLine;
                case PhotoShotOutcome.AlbumFull: return _def.AlbumFullLine;
                case PhotoShotOutcome.Kept:
                    if (LastKept == null || LastKept.Subjects.Count == 0) return "";
                    PhotoSubjectDto best = LastKept.Subjects[0];
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture, _def.TakenFormat,
                                         Capitalised(_def.CountedName(best.SubjectId, best.Count)),
                                         _def.QualityWord(LastKept.Score));
                default: return "";
            }
        }

        private static string Capitalised(string s) =>
            string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
