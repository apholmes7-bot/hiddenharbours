using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>The four phrases of the procedural gull loop, by what they sound like.</summary>
    public enum GullPhraseKind : byte
    {
        /// <summary>The long call: a wail, then the barking "laugh" after it.</summary>
        LongCall = 0,

        /// <summary>A lone wail.</summary>
        Wail = 1,

        /// <summary>A wail and two barks.</summary>
        WailAndBarks = 2,

        /// <summary>A lone bark.</summary>
        Bark = 3,
    }

    /// <summary>
    /// One phrase of the procedural gull loop: where it starts in the clip, how long it lasts, what it
    /// is, and which run of the loop's notes it is made of (<see cref="ProceduralAudio.GullCallNotes"/>).
    /// </summary>
    public readonly struct GullCallPhrase
    {
        public readonly float StartSeconds;
        public readonly float LengthSeconds;
        public readonly GullPhraseKind Kind;
        public readonly int FirstNote;
        public readonly int NoteCount;

        public GullCallPhrase(float startSeconds, float lengthSeconds, GullPhraseKind kind, int firstNote, int noteCount)
        {
            StartSeconds = startSeconds;
            LengthSeconds = lengthSeconds;
            Kind = kind;
            FirstNote = firstNote;
            NoteCount = noteCount;
        }

        public float EndSeconds => StartSeconds + LengthSeconds;
    }

    /// <summary>
    /// One call to start now: on which voice, for which bird, from where in the clip, for how long, how
    /// well its bird is heard (before the bus and the call gain), where in the stereo field, and at what
    /// pitch. <see cref="DurationSeconds"/> is the real time it plays: a call pitched up is over sooner.
    /// </summary>
    public struct GullCallCue
    {
        public int Voice;
        public int BirdKey;
        public float ClipStartSeconds;
        public float LengthSeconds;
        public float DurationSeconds;
        public float Audibility;
        public float Pan;
        public float Pitch;
    }

    /// <summary>
    /// The arithmetic of the gull calls — pure, so it is tested without the engine: how well a bird is
    /// heard from where it is, where it sits in the stereo field, how often a chorus of birds calls, and
    /// which bird and which phrase make the next call.
    /// </summary>
    public static class GullCallLogic
    {
        /// <summary>The most voices a director may make (the tuning's range): calls are short, and a
        /// crowd past this is a wash, not more gulls.</summary>
        public const int MaxVoiceCap = 16;

        /// <summary>The most near birds the chorus listens to at once, nearest first. The birds behind
        /// them are counted by the flock, but never picked out by ear.</summary>
        public const int MaxHeardBirds = 64;

        // What each kind of bird says. Every phrase on a list is as likely as the next.
        private static readonly GullPhraseKind[] FeedingPhrases =
            { GullPhraseKind.LongCall, GullPhraseKind.WailAndBarks, GullPhraseKind.Bark };
        private static readonly GullPhraseKind[] AloftPhrases =
            { GullPhraseKind.Wail, GullPhraseKind.LongCall };
        private static readonly GullPhraseKind[] RestingPhrases =
            { GullPhraseKind.Bark };

        /// <summary>
        /// How well a bird is heard, 0..1: fully anywhere in the view, falling smoothly to nothing
        /// <paramref name="marginMetres"/> beyond its edge.
        /// </summary>
        public static float Audibility(Vector2 at, Rect view, float marginMetres)
        {
            float dx = Mathf.Max(0f, Mathf.Max(view.xMin - at.x, at.x - view.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(view.yMin - at.y, at.y - view.yMax));
            if (dx <= 0f && dy <= 0f) return 1f;
            if (!(marginMetres > 0f)) return 0f;
            float beyond = Mathf.Sqrt(dx * dx + dy * dy);
            if (beyond >= marginMetres) return 0f;
            float k = 1f - beyond / marginMetres;
            return k * k * (3f - 2f * k);
        }

        /// <summary>
        /// Where a bird sits in the stereo field: its side of the view's centre as a share of the half
        /// width, clamped, times <paramref name="panWidth"/> — so no bird is ever hard left or right.
        /// </summary>
        public static float Pan(Vector2 at, Rect view, float panWidth)
        {
            float half = view.width * 0.5f;
            if (!(half > 0f)) return 0f;
            float side = Mathf.Clamp((at.x - view.center.x) / half, -1f, 1f);
            return side * Mathf.Clamp01(panWidth);
        }

        /// <summary>How often one bird calls, by what it is doing.</summary>
        public static float CallsPerSecond(GullCallTuning t, BirdActivity activity)
        {
            if (t == null) return 0f;
            float rate = activity switch
            {
                BirdActivity.Feeding => t.FeedingCallsPerSecond,
                BirdActivity.Aloft => t.AloftCallsPerSecond,
                _ => t.RestingCallsPerSecond,
            };
            return Mathf.Max(0f, rate);
        }

        /// <summary>
        /// The chorus's calls per second: the birds' own rates added up (<paramref name="rawCallsPerSecond"/>),
        /// levelling off at the cap — <c>max x (1 - e^(-raw / max))</c>. A few birds call at their own
        /// rate; a crowd presses up against the cap and never through it.
        /// </summary>
        public static float ChorusRate(float rawCallsPerSecond, float maxCallsPerSecond)
        {
            if (!(rawCallsPerSecond > 0f) || !(maxCallsPerSecond > 0f)) return 0f;
            return maxCallsPerSecond * (1f - Mathf.Exp(-rawCallsPerSecond / maxCallsPerSecond));
        }

        /// <summary>
        /// The wait to the next call of a chorus calling at <paramref name="callsPerSecond"/>, from a
        /// uniform draw in [0, 1): the calls come as birds call, at random and on average at the rate.
        /// Never, for a silent chorus.
        /// </summary>
        public static double GapSeconds(float callsPerSecond, double u)
        {
            if (!(callsPerSecond > 0f)) return double.PositiveInfinity;
            double v = u < 0.0 ? 0.0 : u >= 1.0 ? 1.0 - 1e-12 : u;
            return -Math.Log(1.0 - v) / callsPerSecond;
        }

        /// <summary>
        /// Which bird calls: drawn by weight (how well it is heard x how often it calls), from a uniform
        /// draw in [0, 1). −1 when none has any weight.
        /// </summary>
        public static int PickBird(float[] weights, int count, double u)
        {
            if (weights == null) return -1;
            count = Math.Min(count, weights.Length);
            double total = 0.0;
            for (int i = 0; i < count; i++)
                if (weights[i] > 0f) total += weights[i];
            if (!(total > 0.0)) return -1;

            double target = (u < 0.0 ? 0.0 : u) * total;
            double run = 0.0;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                last = i;
                run += weights[i];
                if (target < run) return i;
            }
            return last;
        }

        /// <summary>
        /// What a bird says, from a uniform draw in [0, 1): a feeding bird the laugh and the barks, a
        /// bird in the air the wails, a resting one a bark (and seldom, by its rate).
        /// </summary>
        public static GullPhraseKind PickPhrase(BirdActivity activity, double u)
        {
            GullPhraseKind[] list = activity switch
            {
                BirdActivity.Feeding => FeedingPhrases,
                BirdActivity.Aloft => AloftPhrases,
                _ => RestingPhrases,
            };
            int i = (int)((u < 0.0 ? 0.0 : u) * list.Length);
            return list[Math.Min(i, list.Length - 1)];
        }

        /// <summary>The index of the phrase of a kind in a table, or −1.</summary>
        public static int PhraseOf(GullCallPhrase[] phrases, GullPhraseKind kind)
        {
            if (phrases == null) return -1;
            for (int i = 0; i < phrases.Length; i++)
                if (phrases[i].Kind == kind) return i;
            return -1;
        }

        /// <summary>
        /// What of the clip a phrase plays: the phrase, with a fade's width of the loop's silence on each
        /// side (the loop is true silence between its notes), so the fades fall on silence and every note
        /// keeps its own soft attack and tail. Never outside the clip.
        /// </summary>
        public static void PhraseWindow(in GullCallPhrase phrase, float fadeSeconds, float clipSeconds,
                                        out float startSeconds, out float lengthSeconds)
        {
            float pad = Mathf.Max(0f, fadeSeconds);
            startSeconds = Mathf.Max(0f, phrase.StartSeconds - pad);
            float end = phrase.EndSeconds + pad;
            if (clipSeconds > 0f) end = Mathf.Min(end, clipSeconds);
            lengthSeconds = Mathf.Max(0f, end - startSeconds);
        }

        /// <summary>Where an excerpt of a recording starts, from a uniform draw in [0, 1): anywhere it
        /// still fits.</summary>
        public static float ExcerptStartSeconds(float clipSeconds, float excerptSeconds, double u)
        {
            float room = Mathf.Max(0f, clipSeconds - excerptSeconds);
            return room * (float)(u < 0.0 ? 0.0 : u > 1.0 ? 1.0 : u);
        }

        /// <summary>A call's pitch, from a uniform draw in [0, 1): 1 +/- the jitter.</summary>
        public static float Pitch(float jitter, double u)
        {
            float j = Mathf.Clamp(jitter, 0f, 0.5f);
            return 1f + (float)(2.0 * u - 1.0) * j;
        }

        /// <summary>How many voices a tuning asks for, within the cap.</summary>
        public static int VoiceCount(GullCallTuning t) => t == null ? 1 : Mathf.Clamp(t.MaxVoices, 1, MaxVoiceCap);
    }

    /// <summary>
    /// THE CHORUS — the call player's decisions, with no engine in them. At the environment tick it
    /// listens: it copies the birds near the view out of the presence (Core), keeps the ones of its own
    /// bird that can be heard, and works out how often they call. On every frame it says which calls
    /// start: which voice, which bird, which phrase (or which excerpt of a recording), panned to its
    /// bird. A call with no free voice is skipped — that is the cap. No bird heard, no call.
    ///
    /// <para>Its draws are its own seeded mulberry32, so a run here and a run in the editor draw the same
    /// calls. It allocates nothing after it is made.</para>
    /// </summary>
    public sealed class GullChorus
    {
        private readonly BirdAudible[] _near = new BirdAudible[GullCallLogic.MaxHeardBirds];
        private readonly float[] _audibility = new float[GullCallLogic.MaxHeardBirds];
        private readonly float[] _weights = new float[GullCallLogic.MaxHeardBirds];
        private readonly double[] _busyUntil;
        private int _audible;
        private float _rate;
        private Rect _view;
        private double _clock;
        private double _nextCallAt = double.PositiveInfinity;
        private uint _state;

        public GullChorus(int voices, int seed)
        {
            _busyUntil = new double[Math.Max(1, Math.Min(voices, GullCallLogic.MaxVoiceCap))];
            _state = unchecked((uint)seed);
        }

        /// <summary>How many voices it calls on.</summary>
        public int Voices => _busyUntil.Length;

        /// <summary>The chorus's calls per second at the last listen.</summary>
        public float Rate => _rate;

        /// <summary>The birds it could hear at the last listen.</summary>
        public int Audible => _audible;

        /// <summary>Its own real-time seconds, advanced by every frame.</summary>
        public double Clock => _clock;

        /// <summary>How many voices are calling now.</summary>
        public int BusyVoices
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _busyUntil.Length; i++)
                    if (_busyUntil[i] > _clock) n++;
                return n;
            }
        }

        /// <summary>
        /// Takes in the birds near the view: the ones of the tuning's bird that can be heard, each
        /// weighted by how well and how often, and the chorus rate they make.
        /// </summary>
        public void Listen(IBirdPresence presence, GullCallTuning tuning)
        {
            _audible = 0;
            float raw = 0f;
            if (presence != null && tuning != null)
            {
                _view = presence.ViewWorld;
                int n = presence.NearCount > 0 ? presence.CopyNear(_near) : 0;
                n = Math.Min(n, _near.Length);
                for (int i = 0; i < n; i++)
                {
                    BirdAudible bird = _near[i];
                    if (!string.Equals(bird.SpeciesId, tuning.SpeciesId, StringComparison.Ordinal)) continue;
                    float heard = GullCallLogic.Audibility(bird.WorldPosition, _view, tuning.HearingMarginMetres);
                    float weight = heard * GullCallLogic.CallsPerSecond(tuning, bird.Activity);
                    if (!(weight > 0f)) continue;
                    _near[_audible] = bird;
                    _audibility[_audible] = heard;
                    _weights[_audible] = weight;
                    _audible++;
                    raw += weight;
                }
            }
            _rate = GullCallLogic.ChorusRate(raw, tuning != null ? tuning.MaxCallsPerSecond : 0f);

            // The calls come as birds call, which forgets: drawing the next one afresh from now, at the
            // new rate, is exactly right for a rate that changes in steps.
            _nextCallAt = _rate > 0f ? _clock + GullCallLogic.GapSeconds(_rate, NextUnit()) : double.PositiveInfinity;
        }

        /// <summary>
        /// Advances the chorus by a frame's real seconds and fills <paramref name="into"/> with the calls
        /// that start now; returns how many. <paramref name="phrases"/> is the procedural loop's phrase
        /// table, or null for a recording, which plays excerpts of <paramref name="clipSeconds"/>.
        /// </summary>
        public int Advance(double deltaSeconds, GullCallTuning tuning, GullCallPhrase[] phrases, float clipSeconds,
                           GullCallCue[] into)
        {
            if (deltaSeconds > 0.0) _clock += deltaSeconds;
            if (into == null || tuning == null) return 0;

            int n = 0;
            while (_clock >= _nextCallAt && n < into.Length)
            {
                _nextCallAt += GullCallLogic.GapSeconds(_rate, NextUnit());
                if (TryCue(tuning, phrases, clipSeconds, out GullCallCue cue)) into[n++] = cue;
            }
            return n;
        }

        private bool TryCue(GullCallTuning t, GullCallPhrase[] phrases, float clipSeconds, out GullCallCue cue)
        {
            cue = default;
            if (_audible <= 0) return false;
            int voice = FreeVoice();
            if (voice < 0) return false;   // every voice is calling: that is the cap

            int bird = GullCallLogic.PickBird(_weights, _audible, NextUnit());
            if (bird < 0) return false;
            BirdAudible who = _near[bird];

            float start, length;
            if (phrases != null && phrases.Length > 0)
            {
                int p = GullCallLogic.PhraseOf(phrases, GullCallLogic.PickPhrase(who.Activity, NextUnit()));
                if (p < 0) return false;
                GullCallLogic.PhraseWindow(in phrases[p], t.FadeSeconds, clipSeconds, out start, out length);
            }
            else
            {
                if (!(clipSeconds > 0f)) return false;
                float lo = Mathf.Max(0.1f, Mathf.Min(t.ExcerptSecondsMin, t.ExcerptSecondsMax));
                float hi = Mathf.Max(lo, Mathf.Max(t.ExcerptSecondsMin, t.ExcerptSecondsMax));
                length = Mathf.Min(clipSeconds, lo + (hi - lo) * (float)NextUnit());
                start = GullCallLogic.ExcerptStartSeconds(clipSeconds, length, NextUnit());
            }

            float pitch = GullCallLogic.Pitch(t.PitchJitter, NextUnit());
            float duration = length / pitch;
            _busyUntil[voice] = _clock + duration;
            cue = new GullCallCue
            {
                Voice = voice,
                BirdKey = who.Key,
                ClipStartSeconds = start,
                LengthSeconds = length,
                DurationSeconds = duration,
                Audibility = _audibility[bird],
                Pan = GullCallLogic.Pan(who.WorldPosition, _view, t.PanWidth),
                Pitch = pitch,
            };
            return true;
        }

        private int FreeVoice()
        {
            for (int i = 0; i < _busyUntil.Length; i++)
                if (_busyUntil[i] <= _clock) return i;
            return -1;
        }

        /// <summary>mulberry32: a uniform draw in [0, 1).</summary>
        private double NextUnit()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint z = _state;
                z = (z ^ (z >> 15)) * (z | 1u);
                z ^= z + (z ^ (z >> 7)) * (z | 61u);
                z ^= z >> 14;
                return z / 4294967296.0;
            }
        }
    }
}
