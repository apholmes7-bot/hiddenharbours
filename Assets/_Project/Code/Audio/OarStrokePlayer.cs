using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// <b>Plays the oars on the stroke.</b> Hears every <see cref="OarBladeWater"/> the oar splash
    /// publishes (the frame a rowing blade goes in or comes out), lets <see cref="OarStrokeVoicing"/>
    /// decide, and starts the catch or the release sound on a free one of a few pooled 2D voices (made
    /// once by the <see cref="AudioDirector"/>, loop off). Replaces the old looping oar bed, which ran on a
    /// clock of its own and kept playing while the boat drifted.
    ///
    /// <para>The two sounds are CUT from the oar-stroke clip (<see cref="Cut"/>): the catch is the clip's
    /// bite, creak and pull, the release its drips. Built once at boot; nothing allocates per stroke.</para>
    /// </summary>
    public sealed class OarStrokePlayer
    {
        /// <summary>Every voice's GameObject name starts so: <c>OarStroke 0</c>, <c>OarStroke 1</c>...</summary>
        public const string VoicePrefix = "OarStroke ";

        private readonly OarStrokeAudioTuning _tuning;
        private readonly AudioSource[] _voices;
        private readonly AudioClip _catch;
        private readonly AudioClip _release;
        private readonly OarStrokeVoicing _voicing = new OarStrokeVoicing();
        private int _next;

        /// <summary>A voice's GameObject name. Boot only: it makes a string.</summary>
        public static string VoiceName(int index) => VoicePrefix + index;

        /// <summary>The ambience bus (not ducked, as the old oar bed was not), set by the director's mix.</summary>
        public float BusGain { get; set; }

        /// <summary>Stroke sounds started so far (catches and releases).</summary>
        public int Started { get; private set; }

        public AudioClip CatchClip => _catch;
        public AudioClip ReleaseClip => _release;

        public OarStrokePlayer(OarStrokeAudioTuning tuning, AudioSource[] voices, AudioClip strokeClip)
        {
            _tuning = tuning ?? new OarStrokeAudioTuning();
            _voices = voices ?? Array.Empty<AudioSource>();
            _catch = Cut(strokeClip, _tuning.CatchStartSeconds, _tuning.CatchLengthSeconds, _tuning.CutFadeSeconds,
                         "oar_catch");
            _release = Cut(strokeClip, _tuning.ReleaseStartSeconds, _tuning.ReleaseLengthSeconds,
                           _tuning.CutFadeSeconds, "oar_release");
        }

        /// <summary>One blade-water moment, heard at <paramref name="now"/> with the listener at
        /// <paramref name="listener"/>. True when a sound was started.</summary>
        public bool Hear(in OarBladeWater e, double now, Vector2 listener)
        {
            if (!_voicing.TryVoice(in e, now, listener, _tuning, out OarStrokeVoice v)) return false;
            AudioClip clip = v.Catch ? _catch : _release;
            if (clip == null || _voices.Length == 0) return false;

            AudioSource voice = FreeVoice();
            if (voice == null) return false;
            voice.Stop();
            voice.clip = clip;
            voice.loop = false;
            voice.pitch = v.Pitch;
            voice.panStereo = v.Pan;
            voice.volume = BusGain * v.Gain;
            voice.Play();
            Started++;
            return true;
        }

        private AudioSource FreeVoice()
        {
            for (int k = 0; k < _voices.Length; k++)
            {
                int i = (_next + k) % _voices.Length;
                if (_voices[i] != null && !_voices[i].isPlaying) { _next = (i + 1) % _voices.Length; return _voices[i]; }
            }
            // All busy: take the next in turn (the oldest-started, round robin).
            AudioSource taken = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            return taken;
        }

        /// <summary>
        /// A one-shot cut from <paramref name="source"/>: <paramref name="lengthSeconds"/> from
        /// <paramref name="startSeconds"/> of its first channel, with a short linear fade at each end so it
        /// never clicks. Null when the source is null or its samples cannot be read (a compressed recording
        /// must be imported Decompress On Load to be cut). Boot only: it allocates the new clip.
        /// </summary>
        public static AudioClip Cut(AudioClip source, float startSeconds, float lengthSeconds, float fadeSeconds,
                                    string name)
        {
            if (source == null || source.samples <= 0 || source.frequency <= 0) return null;
            int hz = source.frequency;
            int ch = Mathf.Max(1, source.channels);
            int start = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, startSeconds) * hz), 0, source.samples - 1);
            int length = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, lengthSeconds) * hz), 1, source.samples - start);

            var interleaved = new float[length * ch];
            if (!source.GetData(interleaved, start)) return null;
            var mono = new float[length];
            for (int i = 0; i < length; i++) mono[i] = interleaved[i * ch];
            ApplyEdgeFades(mono, Mathf.RoundToInt(Mathf.Max(0f, fadeSeconds) * hz));

            var clip = AudioClip.Create(name, length, 1, hz, false);
            clip.SetData(mono, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        /// <summary>A linear fade of <paramref name="fadeSamples"/> in at the start and out at the end.</summary>
        public static void ApplyEdgeFades(float[] data, int fadeSamples)
        {
            if (data == null || data.Length == 0) return;
            int n = Mathf.Min(fadeSamples, data.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }
    }
}
