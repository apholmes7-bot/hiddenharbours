using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// Plays the gulls' calls: the <see cref="GullChorus"/> decides, this voices it. It owns a handful of
    /// one-shot 2D voices (made once by the <see cref="AudioDirector"/> in <c>BuildSources</c>, loop off)
    /// and starts each call on a free one: from its place in the clip, at its pitch, panned to its bird,
    /// faded in and out, and as loud as its bird is heard. It hears the birds through Core
    /// (<see cref="IBirdPresence"/>) and never names the flock.
    ///
    /// <para>The gull slot empty, the clip is the procedural loop and the calls are its four phrases
    /// (<see cref="ProceduralAudio.GullCallPhrases"/>); a recording in the slot plays as short excerpts.
    /// Allocation-free per frame.</para>
    /// </summary>
    public sealed class GullCallPlayer
    {
        /// <summary>Every voice's GameObject name starts so: <c>GullCall 0</c>, <c>GullCall 1</c>…</summary>
        public const string VoicePrefix = "GullCall ";

        private readonly GullCallTuning _tuning;
        private readonly AudioSource[] _voices;
        private readonly AudioClip _clip;
        private readonly GullCallPhrase[] _phrases;
        private readonly GullChorus _chorus;
        private readonly GullCallCue[] _cues;
        private readonly double[] _startAt;
        private readonly double[] _endAt;
        private readonly float[] _audibility;
        private readonly bool[] _playing;
        private readonly float _clipSeconds;
        private readonly int _clipFrequency;
        private readonly int _clipSamples;

        /// <summary>A voice's GameObject name. Boot only: it makes a string.</summary>
        public static string VoiceName(int index) => VoicePrefix + index;

        /// <summary>The ambience bus with its duck, set by the director's mix every frame.</summary>
        public float BusGain { get; set; }

        /// <summary>The decisions behind the calls (read-only use: tests and debugging).</summary>
        public GullChorus Chorus => _chorus;

        /// <param name="tuning">The director's gull tuning.</param>
        /// <param name="voices">The voices, one call each at a time; their count caps the calls at once.</param>
        /// <param name="clip">What the voices play.</param>
        /// <param name="phrases">The procedural loop's phrase table, or null when the clip is a recording.</param>
        public GullCallPlayer(GullCallTuning tuning, AudioSource[] voices, AudioClip clip, GullCallPhrase[] phrases)
        {
            _tuning = tuning ?? new GullCallTuning();
            _voices = voices ?? Array.Empty<AudioSource>();
            _clip = clip;
            _phrases = phrases;
            _chorus = new GullChorus(Math.Max(1, _voices.Length), _tuning.Seed);
            _cues = new GullCallCue[_chorus.Voices];
            _startAt = new double[_voices.Length];
            _endAt = new double[_voices.Length];
            _audibility = new float[_voices.Length];
            _playing = new bool[_voices.Length];
            if (clip != null)
            {
                _clipSeconds = clip.length;
                _clipFrequency = clip.frequency;
                _clipSamples = clip.samples;
            }
        }

        /// <summary>Hears the birds near the view (the director's 4 Hz environment tick).</summary>
        public void Listen(IBirdPresence presence) => _chorus.Listen(presence, _tuning);

        /// <summary>Starts the calls that are due and runs every voice's fades (every frame, real time).</summary>
        public void Tick(float deltaSeconds)
        {
            int started = _chorus.Advance(deltaSeconds, _tuning, _phrases, _clipSeconds, _cues);
            for (int i = 0; i < started; i++) Begin(in _cues[i]);

            double now = _chorus.Clock;
            float fade = Mathf.Max(0f, _tuning.FadeSeconds);
            float gain = BusGain * _tuning.CallGain;
            for (int v = 0; v < _voices.Length; v++)
            {
                if (!_playing[v]) continue;
                AudioSource voice = _voices[v];
                if (voice == null) { _playing[v] = false; continue; }
                if (now >= _endAt[v])
                {
                    voice.Stop();
                    voice.volume = 0f;
                    _playing[v] = false;
                    continue;
                }
                float shape = 1f;
                if (fade > 0f)
                    shape = Mathf.Clamp01((float)Math.Min(now - _startAt[v], _endAt[v] - now) / fade);
                voice.volume = gain * _audibility[v] * shape;
            }
        }

        private void Begin(in GullCallCue cue)
        {
            if (cue.Voice < 0 || cue.Voice >= _voices.Length) return;
            AudioSource voice = _voices[cue.Voice];
            if (voice == null || _clip == null) return;

            voice.Stop();
            voice.clip = _clip;
            voice.loop = false;
            voice.volume = 0f;   // the fade brings it up from the next frame
            voice.pitch = cue.Pitch;
            voice.panStereo = cue.Pan;
            voice.Play();
            if (_clipFrequency > 0)
                voice.timeSamples = Mathf.Clamp(Mathf.RoundToInt(cue.ClipStartSeconds * _clipFrequency), 0,
                                                Math.Max(0, _clipSamples - 1));

            _startAt[cue.Voice] = _chorus.Clock;
            _endAt[cue.Voice] = _chorus.Clock + cue.DurationSeconds;
            _audibility[cue.Voice] = cue.Audibility;
            _playing[cue.Voice] = true;
        }
    }
}
