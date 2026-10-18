using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Audio;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// <b>The sound follows the birds.</b> The call player's decisions (<see cref="GullChorus"/> and
    /// <see cref="GullCallLogic"/>) with no engine in them: no gull near, no call; a call is panned to
    /// its bird, fades with distance past the view's edge and stops at the hearing margin; a crowd calls
    /// more, up to the voice cap; and with the gull slot empty, the calls are the procedural loop's own
    /// phrases, cut on its silences.
    /// </summary>
    public class GullCallLogicTests
    {
        private const string Gull = "bird.herring_gull";
        private const string OtherBird = "bird.other_species";
        private const double Frame = 1.0 / 60.0;
        private const int ListenEveryFrames = 15;   // the environment tick's 4 Hz
        private const int Seed = 11;

        private static readonly Rect View = new Rect(-14f, -8f, 28f, 16f);

        /// <summary>A presence that holds what a test puts in it.</summary>
        private sealed class StubPresence : IBirdPresence
        {
            public readonly List<BirdAudible> Birds = new List<BirdAudible>();
            public Rect ViewWorld => View;
            public int NearCount => Birds.Count;

            public int CopyNear(BirdAudible[] into)
            {
                if (into == null) return 0;
                int n = Math.Min(into.Length, Birds.Count);
                for (int i = 0; i < n; i++) into[i] = Birds[i];
                return n;
            }
        }

        private static StubPresence Crowd(int n, BirdActivity activity, float x = 0f, float y = 0f, string species = Gull)
        {
            var presence = new StubPresence();
            for (int i = 0; i < n; i++) presence.Birds.Add(new BirdAudible(new Vector2(x, y), activity, i + 1, species));
            return presence;
        }

        private sealed class Heard
        {
            public readonly List<GullCallCue> Cues = new List<GullCallCue>();
            public int MostVoices;
            public float MostRate;
            public int Voices;
        }

        /// <summary>The player's loop: listen at the tick, advance every frame.</summary>
        private static Heard Run(IBirdPresence presence, GullCallTuning tuning, double seconds, GullCallPhrase[] phrases,
                                 float clipSeconds)
        {
            var chorus = new GullChorus(GullCallLogic.VoiceCount(tuning), Seed);
            var into = new GullCallCue[8];
            var heard = new Heard { Voices = chorus.Voices };
            int frames = (int)Math.Round(seconds / Frame);
            for (int f = 0; f < frames; f++)
            {
                if (f % ListenEveryFrames == 0)
                {
                    chorus.Listen(presence, tuning);
                    heard.MostRate = Math.Max(heard.MostRate, chorus.Rate);
                }
                int n = chorus.Advance(Frame, tuning, phrases, clipSeconds, into);
                for (int i = 0; i < n; i++) heard.Cues.Add(into[i]);
                heard.MostVoices = Math.Max(heard.MostVoices, chorus.BusyVoices);
            }
            return heard;
        }

        private static GullCallPhrase[] LoopPhrases => ProceduralAudio.GullCallPhrases;
        private static float LoopSeconds => ProceduralAudio.GullLoopSeconds;

        [Test]
        public void NoBirdsNear_MeansSilence()
        {
            GullCallTuning tuning = GullCallTuning.Default;
            float beyond = View.xMax + tuning.HearingMarginMetres + 1f;
            var skies = new (string name, IBirdPresence presence)[]
            {
                ("no presence", null),
                ("the empty sky", EmptyBirdPresence.Instance),
                ("no birds", Crowd(0, BirdActivity.Feeding)),
                ("gulls past the hearing margin", Crowd(20, BirdActivity.Feeding, beyond, 0f)),
                ("another bird over the view", Crowd(20, BirdActivity.Feeding, 0f, 0f, OtherBird)),
            };

            var failures = new List<string>();
            foreach ((string name, IBirdPresence presence) in skies)
            {
                foreach ((string path, GullCallPhrase[] phrases, float clip) in new[] { ("the loop", LoopPhrases, LoopSeconds),
                                                                                         ("a recording", (GullCallPhrase[])null, 30f) })
                {
                    Heard heard = Run(presence, tuning, 10.0, phrases, clip);
                    if (heard.MostRate != 0f) failures.Add(name + ", " + path + ": the chorus rate is " + heard.MostRate);
                    if (heard.Cues.Count != 0) failures.Add(name + ", " + path + ": " + heard.Cues.Count + " calls in 10 s");
                }
            }
            GullAssert.None(failures, "a call with no gull to make it");
        }

        [Test]
        public void ACallPansToItsBird()
        {
            float width = GullCallTuning.Default.PanWidth;
            Assert.That(width, Is.EqualTo(0.8f), "the pan width the cases below are written for");

            Assert.That(GullCallLogic.Pan(new Vector2(0f, 0f), View, width), Is.EqualTo(0f), "the centre");
            Assert.That(GullCallLogic.Pan(new Vector2(0f, 7f), View, width), Is.EqualTo(0f), "above the centre");
            Assert.That(GullCallLogic.Pan(new Vector2(-7f, 0f), View, width), Is.EqualTo(-0.4f).Within(1e-6f), "half left");
            Assert.That(GullCallLogic.Pan(new Vector2(7f, -3f), View, width), Is.EqualTo(0.4f).Within(1e-6f), "half right");
            Assert.That(GullCallLogic.Pan(new Vector2(-14f, 0f), View, width), Is.EqualTo(-0.8f).Within(1e-6f), "the left edge");
            Assert.That(GullCallLogic.Pan(new Vector2(14f, 0f), View, width), Is.EqualTo(0.8f).Within(1e-6f), "the right edge");
            Assert.That(GullCallLogic.Pan(new Vector2(-25f, 0f), View, width), Is.EqualTo(-0.8f).Within(1e-6f), "past the left edge");
            Assert.That(GullCallLogic.Pan(new Vector2(25f, 0f), View, width), Is.EqualTo(0.8f).Within(1e-6f), "past the right edge");

            var failures = new List<string>();
            foreach ((string side, float x) in new[] { ("left", -7f), ("right", 7f) })
            {
                Heard heard = Run(Crowd(10, BirdActivity.Feeding, x, 2f), GullCallTuning.Default, 20.0, LoopPhrases, LoopSeconds);
                if (heard.Cues.Count == 0) failures.Add("the " + side + " crowd never called");
                foreach (GullCallCue cue in heard.Cues)
                    if (Math.Sign(cue.Pan) != Math.Sign(x) || Math.Abs(cue.Pan - x / 14f * width) > 1e-6f)
                        failures.Add("a call of the " + side + " crowd panned to " + cue.Pan);
            }
            GullAssert.None(failures, "a call not panned to its bird");
        }

        [Test]
        public void FartherIsQuieter_BeyondTheMarginSilent()
        {
            float margin = GullCallTuning.Default.HearingMarginMetres;
            Assert.That(margin, Is.EqualTo(15f), "the margin the cases below are written for");

            foreach (Vector2 inside in new[] { new Vector2(0f, 0f), new Vector2(-14f, 0f), new Vector2(14f, 8f),
                                               new Vector2(14f, -8f), new Vector2(-14f, 8f), new Vector2(3f, -5f) })
                Assert.That(GullCallLogic.Audibility(inside, View, margin), Is.EqualTo(1f), "in the view at " + inside);

            var failures = new List<string>();
            float was = 1f;
            for (float beyond = 0.5f; beyond <= 14.5f; beyond += 0.5f)
            {
                float a = GullCallLogic.Audibility(new Vector2(View.xMax + beyond, 0f), View, margin);
                if (!(a > 0f) || !(a < was)) failures.Add(beyond + " m past the edge: " + a + " after " + was);
                was = a;
            }
            foreach (float beyond in new[] { 15f, 20f })
            {
                float a = GullCallLogic.Audibility(new Vector2(View.xMax + beyond, 0f), View, margin);
                if (a != 0f) failures.Add(beyond + " m past the edge is still heard: " + a);
            }

            // Past a corner the distance is to the corner, not along one axis.
            float farCorner = GullCallLogic.Audibility(new Vector2(25f, 19f), View, margin);   // 15.6 m off
            float nearCorner = GullCallLogic.Audibility(new Vector2(24f, 18f), View, margin);  // 14.1 m off
            float side = GullCallLogic.Audibility(new Vector2(24f, 0f), View, margin);         // 10 m off
            if (farCorner != 0f) failures.Add("15.6 m past the corner is still heard: " + farCorner);
            if (!(nearCorner > 0f) || !(nearCorner < side))
                failures.Add("14.1 m past the corner: " + nearCorner + ", against " + side + " 10 m past the side");

            if (GullCallLogic.Audibility(new Vector2(View.xMax + 0.1f, 0f), View, 0f) != 0f)
                failures.Add("with no margin a bird past the edge is still heard");
            GullAssert.None(failures, "the hearing does not fall off with distance");
        }

        [Test]
        public void MoreBirdsMoreCalls_UpToTheVoiceCap()
        {
            GullCallTuning tuning = GullCallTuning.Default;
            Assert.That(tuning.MaxVoices, Is.EqualTo(6), "the voice count the cases below are written for");
            Assert.That(tuning.MaxCallsPerSecond, Is.EqualTo(3f), "the cap the cases below are written for");

            var failures = new List<string>();
            float was = 0f;
            for (int n = 1; n <= GullCallLogic.MaxHeardBirds; n++)
            {
                var chorus = new GullChorus(GullCallLogic.VoiceCount(tuning), Seed);
                chorus.Listen(Crowd(n, BirdActivity.Feeding), tuning);
                if (chorus.Audible != n) failures.Add(n + " birds, " + chorus.Audible + " heard");
                if (!(chorus.Rate > was)) failures.Add(n + " birds call no more than " + (n - 1) + ": " + chorus.Rate);
                if (!(chorus.Rate < tuning.MaxCallsPerSecond)) failures.Add(n + " birds call through the cap: " + chorus.Rate);
                was = chorus.Rate;
            }

            // A crowd on a long recording: every voice in use, and never one more.
            Heard recording = Run(Crowd(GullCallLogic.MaxHeardBirds, BirdActivity.Feeding), tuning, 60.0, null, 30f);
            if (recording.Voices != 6) failures.Add("the chorus has " + recording.Voices + " voices");
            if (recording.MostVoices > 6) failures.Add("a recording called on " + recording.MostVoices + " voices at once");
            if (recording.MostVoices < 6) failures.Add("a crowd on a recording never filled the voices: " + recording.MostVoices);

            Heard loop = Run(Crowd(GullCallLogic.MaxHeardBirds, BirdActivity.Feeding), tuning, 60.0, LoopPhrases, LoopSeconds);
            if (loop.MostVoices > 6) failures.Add("the loop called on " + loop.MostVoices + " voices at once");
            if (loop.Cues.Count == 0) failures.Add("a crowd on the loop never called");
            foreach (GullCallCue cue in loop.Cues)
                if (cue.Voice < 0 || cue.Voice >= 6) failures.Add("a call on voice " + cue.Voice);
            GullAssert.None(failures, "the chorus does not grow with the crowd, or grows past its voices");
        }

        [Test]
        public void FeedingBirdsCallMoreThanRestingOnes()
        {
            GullCallTuning tuning = GullCallTuning.Default;
            float feeding = GullCallLogic.CallsPerSecond(tuning, BirdActivity.Feeding);
            float aloft = GullCallLogic.CallsPerSecond(tuning, BirdActivity.Aloft);
            float resting = GullCallLogic.CallsPerSecond(tuning, BirdActivity.Resting);
            Assert.That(feeding, Is.GreaterThan(aloft), "a feeding bird calls more than one in the air");
            Assert.That(aloft, Is.GreaterThan(resting), "a bird in the air calls more than a resting one");
            Assert.That(resting, Is.GreaterThan(0f), "a resting bird still calls now and then");
            Assert.That(GullCallLogic.CallsPerSecond(null, BirdActivity.Feeding), Is.EqualTo(0f), "no tuning, no calls");

            float Rate(BirdActivity activity)
            {
                var chorus = new GullChorus(GullCallLogic.VoiceCount(tuning), Seed);
                chorus.Listen(Crowd(10, activity), tuning);
                return chorus.Rate;
            }
            Assert.That(Rate(BirdActivity.Feeding), Is.GreaterThan(Rate(BirdActivity.Aloft)), "ten feeding against ten aloft");
            Assert.That(Rate(BirdActivity.Aloft), Is.GreaterThan(Rate(BirdActivity.Resting)), "ten aloft against ten resting");

            int fed = Run(Crowd(10, BirdActivity.Feeding), tuning, 60.0, LoopPhrases, LoopSeconds).Cues.Count;
            int rested = Run(Crowd(10, BirdActivity.Resting), tuning, 60.0, LoopPhrases, LoopSeconds).Cues.Count;
            Assert.That(fed, Is.GreaterThan(rested), "a minute of ten feeding birds against ten resting");
        }

        [Test]
        public void TheFallbackPlaysThePhrasesOfTheProceduralLoop()
        {
            (float at, int kind, float gain)[] notes = ProceduralAudio.GullCallNotes;
            GullCallPhrase[] phrases = LoopPhrases;
            float fade = GullCallTuning.Default.FadeSeconds;
            Assert.That(fade, Is.EqualTo(0.06f), "the fade the windows below are written for");

            var failures = new List<string>();
            int next = 0;
            var kinds = new HashSet<GullPhraseKind>();
            foreach (GullCallPhrase phrase in phrases)
            {
                if (phrase.FirstNote != next) failures.Add(phrase.Kind + " starts at note " + phrase.FirstNote + ", not " + next);
                next = phrase.FirstNote + phrase.NoteCount;
                if (!kinds.Add(phrase.Kind)) failures.Add(phrase.Kind + " twice");

                float end = 0f;
                for (int i = phrase.FirstNote; i < phrase.FirstNote + phrase.NoteCount; i++)
                    end = Math.Max(end, notes[i].at + ProceduralAudio.GullNoteSeconds(notes[i].kind));
                if (Math.Abs(phrase.StartSeconds - notes[phrase.FirstNote].at) > 1e-5f)
                    failures.Add(phrase.Kind + " starts at " + phrase.StartSeconds + ", its first note at " + notes[phrase.FirstNote].at);
                if (Math.Abs(phrase.EndSeconds - end) > 1e-5f)
                    failures.Add(phrase.Kind + " ends at " + phrase.EndSeconds + ", its last note at " + end);

                GullCallLogic.PhraseWindow(phrase, fade, LoopSeconds, out float start, out float length);
                float stop = start + length;
                if (start < 0f || stop > LoopSeconds) failures.Add(phrase.Kind + " plays " + start + ".." + stop + ", outside the loop");
                for (int i = 0; i < notes.Length; i++)
                {
                    float a = notes[i].at, b = a + ProceduralAudio.GullNoteSeconds(notes[i].kind);
                    bool own = i >= phrase.FirstNote && i < phrase.FirstNote + phrase.NoteCount;
                    if (own && (a < start || b > stop)) failures.Add(phrase.Kind + " cuts its own note " + i);
                    if (!own && a < stop && b > start) failures.Add(phrase.Kind + " plays part of note " + i);
                }
            }
            if (next != notes.Length) failures.Add("the phrases cover " + next + " of the loop's " + notes.Length + " notes");
            foreach (GullPhraseKind kind in (GullPhraseKind[])Enum.GetValues(typeof(GullPhraseKind)))
                if (GullCallLogic.PhraseOf(phrases, kind) < 0) failures.Add("no " + kind);

            // The windows, as the loop is written today.
            var windows = new (GullPhraseKind kind, float start, float stop)[]
            {
                (GullPhraseKind.LongCall, 0.56f, 1.88f),
                (GullPhraseKind.Wail, 3.88f, 4.52f),
                (GullPhraseKind.WailAndBarks, 5.22f, 6.36f),
                (GullPhraseKind.Bark, 8.04f, 8.27f),
            };
            foreach ((GullPhraseKind kind, float start, float stop) in windows)
            {
                GullCallLogic.PhraseWindow(phrases[GullCallLogic.PhraseOf(phrases, kind)], fade, LoopSeconds,
                                           out float s, out float length);
                if (Math.Abs(s - start) > 1e-4f || Math.Abs(s + length - stop) > 1e-4f)
                    failures.Add(kind + " plays " + s + ".." + (s + length) + ", not " + start + ".." + stop);
            }

            // Every call the chorus makes on the loop is one of those windows, at a pitch inside the jitter.
            float jitter = GullCallTuning.Default.PitchJitter;
            foreach (BirdActivity activity in new[] { BirdActivity.Feeding, BirdActivity.Aloft, BirdActivity.Resting })
            {
                Heard heard = Run(Crowd(20, activity), GullCallTuning.Default, 60.0, phrases, LoopSeconds);
                if (heard.Cues.Count == 0) failures.Add(activity + " birds never called");
                foreach (GullCallCue cue in heard.Cues)
                {
                    bool known = false;
                    foreach (var (_, start, stop) in windows)
                        known |= Math.Abs(cue.ClipStartSeconds - start) <= 1e-4f
                                 && Math.Abs(cue.ClipStartSeconds + cue.LengthSeconds - stop) <= 1e-4f;
                    if (!known) failures.Add(activity + " call plays " + cue.ClipStartSeconds + " +" + cue.LengthSeconds + " s");
                    if (Math.Abs(cue.Pitch - 1f) > jitter + 1e-6f) failures.Add(activity + " call at pitch " + cue.Pitch);
                    if (Math.Abs(cue.DurationSeconds - cue.LengthSeconds / cue.Pitch) > 1e-5f)
                        failures.Add(activity + " call lasts " + cue.DurationSeconds + " for " + cue.LengthSeconds + " s at " + cue.Pitch);
                }
                if (activity == BirdActivity.Resting)
                    foreach (GullCallCue cue in heard.Cues)
                        if (Math.Abs(cue.ClipStartSeconds - 8.04f) > 1e-4f)
                            failures.Add("a resting bird said more than a bark: " + cue.ClipStartSeconds);
            }
            GullAssert.None(failures, "the fallback does not play the loop's phrases");
        }
    }

    /// <summary>Collects a test's failures and fails once, listing them (Unity's NUnit has no
    /// Assert.Multiple).</summary>
    internal static class GullAssert
    {
        public static void None(List<string> failures, string what)
        {
            if (failures.Count == 0) return;
            int shown = Math.Min(failures.Count, 40);
            Assert.Fail(what + " (" + failures.Count + "):\n  " + string.Join("\n  ", failures.GetRange(0, shown))
                        + (failures.Count > shown ? "\n  ..." : ""));
        }
    }
}
