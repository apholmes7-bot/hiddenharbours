using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What a wash fixture shows, frame by frame</b> (pure; the art component feeds it and reads it back).
    ///
    /// <list type="bullet">
    /// <item>A <b>bath</b> fills when the wash begins (the tap running), then asks the figure to climb in. The
    /// suds cycle while a soaped wash runs. When the wash ends he climbs out; the tub stays full until he is
    /// clear, then drains.</item>
    /// <item>A <b>basin</b> fills, froths and drains the same way, with no pose.</item>
    /// <item>A <b>shower</b> runs (the stream frames loop) while the wash runs, with the figure stood under it.</item>
    /// <item>A <b>water closet</b> holds the seat while he sits and flushes once when he gets up.</item>
    /// <item>The kitchen pieces never leave their idle row.</item>
    /// </list>
    ///
    /// <para>Presentation only, in real seconds: nothing here touches the wash, the seat or the save.</para>
    /// </summary>
    public sealed class WashFixtureTimeline
    {
        public enum Phase { Idle, Filling, Full, Holding, Draining, Running, Seated, Flushing }

        private readonly ApplianceLayout _layout;
        private readonly int[] _fill;
        private readonly int[] _drain;

        private Phase _phase;
        private float _phaseSeconds;
        private float _loopSeconds;
        private bool _active;

        public WashFixtureTimeline(in ApplianceLayout layout)
        {
            _layout = layout;
            _fill = layout.FillFrames();
            _drain = layout.DrainFrames();
            Row = layout.Idle;
            NearRow = -1;
        }

        public Phase Current => _phase;

        /// <summary>The sheet row to show for the whole piece.</summary>
        public int Row { get; private set; }

        /// <summary>The sheet row whose NEAR layer draws over the figure, or −1 for none.</summary>
        public int NearRow { get; private set; }

        /// <summary>Whether the figure should be in the fixture's pose now.</summary>
        public bool PoseOn { get; private set; }

        /// <summary>
        /// Advance by <paramref name="dt"/> real seconds. <paramref name="active"/> is "the player is washing
        /// here" (or, for a seat, "sitting here"); <paramref name="froth"/> is whether this wash shows suds.
        /// </summary>
        public void Tick(bool active, bool froth, float dt, in WashVisualsSettings s)
        {
            if (!s.Enabled)
            {
                _phase = Phase.Idle; _active = active; PoseOn = false;
                Row = _layout.Idle; NearRow = -1;
                return;
            }

            dt = Mathf.Max(0f, dt);
            bool rose = active && !_active;
            bool fell = !active && _active;
            _active = active;
            _phaseSeconds += dt;
            _loopSeconds += dt;

            if (_layout.Fills) StepFill(rose, fell);
            else if (_layout.Runs) StepRun(rose, fell);
            else if (_layout.Flushes) StepSeat(rose, fell);

            Resolve(froth, s);
        }

        private void Enter(Phase phase) { _phase = phase; _phaseSeconds = 0f; }

        private void StepFill(bool rose, bool fell)
        {
            if (rose && (_phase == Phase.Idle || _phase == Phase.Draining)) Enter(Phase.Filling);
            if (fell)
            {
                if (_phase == Phase.Full && _layout.Pose != FixturePose.None) Enter(Phase.Holding);
                else if (_phase == Phase.Filling || _phase == Phase.Full) Enter(Phase.Draining);
            }
        }

        private void StepRun(bool rose, bool fell)
        {
            if (rose) Enter(Phase.Running);
            if (fell) Enter(Phase.Idle);
        }

        private void StepSeat(bool rose, bool fell)
        {
            if (rose) Enter(Phase.Seated);
            if (fell) Enter(Phase.Flushing);
        }

        private void Resolve(bool froth, in WashVisualsSettings s)
        {
            // Phase timeouts first, so a fill that ends this tick shows its full row this tick.
            if (_phase == Phase.Filling && _phaseSeconds >= s.FillSeconds) Enter(Phase.Full);
            if (_phase == Phase.Holding && _phaseSeconds >= s.ClimbSeconds) Enter(Phase.Draining);
            if (_phase == Phase.Draining && _phaseSeconds >= s.DrainSeconds) Enter(Phase.Idle);
            if (_phase == Phase.Flushing && _phaseSeconds >= s.FlushSeconds) Enter(Phase.Idle);

            int frothFrame = LoopFrame(s.FrothFramesPerSecond);
            NearRow = -1;
            PoseOn = false;

            switch (_phase)
            {
                case Phase.Filling:
                    Row = Pick(_fill, Progress(s.FillSeconds), lastAtEnd: true);
                    break;
                case Phase.Full:
                    Row = froth && _layout.Froths ? _layout.FrothFirst + frothFrame : _layout.Full;
                    PoseOn = _layout.Pose != FixturePose.None;
                    if (PoseOn && _layout.NearFull >= 0)
                        NearRow = froth && _layout.NearFrothFirst >= 0 ? _layout.NearFrothFirst + frothFrame : _layout.NearFull;
                    break;
                case Phase.Holding:
                    // He is climbing out over the rim: the tub is still full and still in front of him.
                    Row = _layout.Full;
                    NearRow = _layout.NearFull;
                    break;
                case Phase.Draining:
                    Row = Pick(_drain, Progress(s.DrainSeconds), lastAtEnd: true);
                    break;
                case Phase.Running:
                    int runFrame = LoopFrame(s.RunFramesPerSecond);
                    Row = _layout.RunFirst + runFrame;
                    PoseOn = _layout.Pose != FixturePose.None;
                    if (PoseOn && _layout.NearRunFirst >= 0) NearRow = _layout.NearRunFirst + runFrame;
                    break;
                case Phase.Seated:
                    Row = _layout.Idle;
                    PoseOn = _layout.Pose != FixturePose.None;
                    break;
                case Phase.Flushing:
                    Row = _layout.FlushFirst + Mathf.Min(ApplianceLayout.LoopFrames - 1,
                        Mathf.FloorToInt(Progress(s.FlushSeconds) * ApplianceLayout.LoopFrames));
                    break;
                default:
                    Row = _layout.Idle;
                    break;
            }
        }

        private float Progress(float seconds) => seconds <= 0f ? 1f : Mathf.Clamp01(_phaseSeconds / seconds);

        private int LoopFrame(float framesPerSecond)
            => framesPerSecond <= 0f ? 0 : Mathf.FloorToInt(_loopSeconds * framesPerSecond) % ApplianceLayout.LoopFrames;

        /// <summary>One frame of a sequence by progress. The last frame is the resting one: it shows only once
        /// the progress is complete, and the others share the time before it evenly.</summary>
        public static int Pick(int[] frames, float progress, bool lastAtEnd)
        {
            if (frames == null || frames.Length == 0) return 0;
            int n = frames.Length;
            if (progress >= 1f) return frames[n - 1];
            int span = lastAtEnd ? Mathf.Max(1, n - 1) : n;
            return frames[Mathf.Clamp(Mathf.FloorToInt(progress * span), 0, n - 1)];
        }
    }
}
