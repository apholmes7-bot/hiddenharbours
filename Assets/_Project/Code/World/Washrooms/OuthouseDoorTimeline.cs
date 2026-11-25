using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What the outhouse door does, frame by frame</b> (pure; <see cref="OuthouseArt"/> feeds it and reads it
    /// back). He presses to sit: the door swings open (shut, ajar, open), stands open a beat while he steps in,
    /// and only then is he inside (<see cref="Inside"/>); the door swings shut behind him. He presses to get up:
    /// the door swings open with him still inside, stands open a beat, he steps out, and it swings shut. Getting
    /// up before he is in just lets the door fall shut again.
    ///
    /// <para>Presentation only, in real seconds: nothing here touches the seat or the save.</para>
    /// </summary>
    public sealed class OuthouseDoorTimeline
    {
        private int _frame;          // index into OuthouseSheet.DoorFrames: 0 shut .. last open
        private float _clock;
        private float _held;         // seconds the door has stood wide open

        /// <summary>The door frame shown: 0 shut, 1 ajar, 2 open (an index into <see cref="OuthouseSheet.DoorFrames"/>).</summary>
        public int Frame => _frame;

        /// <summary>The sheet row of the door frame shown.</summary>
        public int Row => OuthouseSheet.DoorFrames[_frame];

        /// <summary>True once the door is wide open.</summary>
        public bool IsOpen => _frame == OuthouseSheet.DoorFrames.Length - 1;

        /// <summary>True while he is inside the booth (the seated pose is on).</summary>
        public bool Inside { get; private set; }

        /// <summary>Advance by <paramref name="dt"/> real seconds. <paramref name="seated"/> is "the seat is his".</summary>
        public void Tick(bool seated, float dt, float frameSeconds, float holdSeconds)
        {
            dt = Mathf.Max(0f, dt);
            float per = Mathf.Max(0.01f, frameSeconds), hold = Mathf.Max(0f, holdSeconds);
            int last = OuthouseSheet.DoorFrames.Length - 1;

            if (IsOpen) _held += dt;

            bool wantOpen;
            if (seated != Inside)
            {
                // Stepping in or out: the door must be open, and stay open a beat, before he passes.
                wantOpen = true;
                if (IsOpen && _held >= hold)
                {
                    Inside = seated;
                    _held = 0f;
                    // Stepped out: the door stays open its beat before it swings shut (below).
                    wantOpen = !seated;
                }
            }
            else wantOpen = !seated && IsOpen && _held < hold;   // he has just stepped out: hold, then shut

            int target = wantOpen ? last : 0;
            if (_frame == target) { _clock = 0f; return; }
            _clock += dt;
            while (_clock >= per && _frame != target)
            {
                _clock -= per;
                _frame += wantOpen ? 1 : -1;
                _held = 0f;
            }
        }
    }
}
