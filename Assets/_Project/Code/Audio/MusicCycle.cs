using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// The music bus's song cycle: each song plays through once, then <see cref="GapSeconds"/> of
    /// silence, then the next, wrapping after the last. No shuffle. A set without songs makes an empty
    /// cycle, which never starts anything, so the bus stays silent.
    ///
    /// <para>It decides and <see cref="AudioDirector"/> plays. Each frame the director says whether the
    /// music source is playing and how much unscaled time passed, and starts whatever song this hands
    /// back. A song counts as ended only once it has been heard playing: a streamed song can take a few
    /// frames to begin, and those frames are not its end.</para>
    /// </summary>
    public sealed class MusicCycle
    {
        private readonly AudioClip[] _songs;
        private readonly float _gapSeconds;
        private int _place = -1;
        private bool _heard;
        private float _gapLeft = -1f;   // < 0: no gap running

        public MusicCycle(IReadOnlyList<AudioClip> songs, float gapSeconds)
        {
            var kept = new List<AudioClip>();
            if (songs != null)
                foreach (var song in songs)
                    if (song != null) kept.Add(song);
            _songs = kept.ToArray();
            _gapSeconds = Mathf.Max(0f, gapSeconds);
        }

        /// <summary>The set's songs in order, with the set's gap between them.</summary>
        public static MusicCycle For(AudioClipSetDef set) =>
            new MusicCycle(AudioClipSets.SongsOf(set), set != null ? set.SongGapSeconds : 0f);

        public int SongCount => _songs.Length;
        public float GapSeconds => _gapSeconds;

        /// <summary>The place in the cycle of the song started last, or -1 before the first.</summary>
        public int Place => _place;

        /// <summary>The song to start now, or null to leave the bus as it is.</summary>
        public AudioClip Tick(bool playing, float deltaSeconds)
        {
            if (_songs.Length == 0) return null;
            if (_place < 0) return Begin(0);             // the first song starts at once: no gap before it
            if (playing) { _heard = true; return null; }
            if (!_heard) return null;                    // started but not heard yet: a streamed song may still be loading
            if (_gapLeft < 0f) _gapLeft = _gapSeconds;   // it has just ended: the gap begins
            _gapLeft -= deltaSeconds;
            return _gapLeft > 0f ? null : Begin((_place + 1) % _songs.Length);
        }

        private AudioClip Begin(int place)
        {
            _place = place;
            _heard = false;
            _gapLeft = -1f;
            return _songs[place];
        }
    }
}
