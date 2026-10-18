using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// The music bus's song cycle (<see cref="MusicCycle"/>), on songs made here: no test reads a file of
    /// the private sound pack. Each song plays through once, then the set's gap, then the next, wrapping
    /// after the last; a set without songs never starts anything.
    /// </summary>
    public class MusicCycleTests
    {
        private const float Frame = 1f / 60f;
        private const int FramesBeforeGivingUp = 100000;

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private AudioClip Song(string name)
        {
            var clip = AudioClip.Create(name, 64, 2, 48000, false);
            _made.Add(clip);
            return clip;
        }

        private AudioClipSetDef Set(float gapSeconds, params AudioClip[] songs)
        {
            var set = ScriptableObject.CreateInstance<AudioClipSetDef>();
            set.Songs = songs;
            set.SongGapSeconds = gapSeconds;
            _made.Add(set);
            return set;
        }

        /// <summary>Plays the song just started for two frames, lets it end, and ticks the silence until
        /// the cycle starts another: returns that one and the silence it took.</summary>
        private static AudioClip NextAfter(MusicCycle cycle, out float silence)
        {
            Assert.IsNull(cycle.Tick(true, Frame), "nothing new starts while a song plays");
            Assert.IsNull(cycle.Tick(true, Frame), "nothing new starts while a song plays");
            silence = 0f;
            for (int i = 0; i < FramesBeforeGivingUp; i++)
            {
                silence += Frame;
                var next = cycle.Tick(false, Frame);
                if (next != null) return next;
            }
            Assert.Fail("the cycle never started another song");
            return null;
        }

        [Test]
        public void Cycle_PlaysEachSongOnceInOrder_AndWrapsAfterTheLast()
        {
            var a = Song("a");
            var b = Song("b");
            var c = Song("c");
            var cycle = MusicCycle.For(Set(2f, a, b, c));

            var heard = new List<AudioClip> { cycle.Tick(false, Frame) };
            for (int i = 0; i < 6; i++) heard.Add(NextAfter(cycle, out _));

            CollectionAssert.AreEqual(new[] { a, b, c, a, b, c, a }, heard,
                "the first song starts at once, each plays once in the set's order, and the last wraps to the first");
        }

        [Test]
        public void Gap_IsReadFromTheDef()
        {
            foreach (float gap in new[] { 0.5f, 7.25f })
            {
                var a = Song("a");
                var b = Song("b");
                var cycle = MusicCycle.For(Set(gap, a, b));
                Assert.AreSame(a, cycle.Tick(false, Frame));

                Assert.AreSame(b, NextAfter(cycle, out float silence));
                Assert.AreEqual(gap, silence, Frame + 1e-4f,
                    $"the silence between two songs is the Def's SongGapSeconds ({gap} s here)");
            }
        }

        [Test]
        public void SetWithoutSongs_PlaysNothing()
        {
            var sets = new[] { Set(2f), Set(2f, new AudioClip[] { null, null }), null };
            foreach (var set in sets)
            {
                var cycle = MusicCycle.For(set);
                Assert.AreEqual(0, cycle.SongCount);
                for (int i = 0; i < 600; i++)
                    Assert.IsNull(cycle.Tick(i % 2 == 0, Frame), "a set without songs never starts one");
                Assert.AreEqual(-1, cycle.Place);
            }
        }

        [Test]
        public void PublicSet_HasNoSongs_SoTheBusIsSilentWithoutThePack()
        {
            var publicSet = Resources.Load<AudioClipSetDef>("AudioClipSet");
            Assert.IsNotNull(publicSet, "The public clip set must load by name, as both players load it.");
            Assert.IsNotNull(publicSet.Songs);
            CollectionAssert.IsEmpty(publicSet.Songs,
                "Songs live in the private sound pack: the public set ships none, so a fresh clone's music bus " +
                "is silent exactly as before.");
            Assert.IsNull(MusicCycle.For(publicSet).Tick(false, Frame));
        }

        [Test]
        public void ASongNotYetHeard_HasNotEnded()
        {
            var a = Song("a");
            var b = Song("b");
            var cycle = MusicCycle.For(Set(0f, a, b));
            Assert.AreSame(a, cycle.Tick(false, Frame));

            // A streamed song can take frames to begin; those frames are not its end.
            for (int i = 0; i < 300; i++)
                Assert.IsNull(cycle.Tick(false, Frame), "a song still loading has not ended");

            Assert.IsNull(cycle.Tick(true, Frame));
            Assert.AreSame(b, cycle.Tick(false, Frame), "with no gap, the next song starts the frame the last one ends");
        }

        [Test]
        public void EmptyEntries_AreSkipped()
        {
            var a = Song("a");
            var b = Song("b");
            var cycle = MusicCycle.For(Set(1f, a, null, b));
            Assert.AreEqual(2, cycle.SongCount);

            var heard = new List<AudioClip> { cycle.Tick(false, Frame) };
            for (int i = 0; i < 3; i++) heard.Add(NextAfter(cycle, out _));
            CollectionAssert.AreEqual(new[] { a, b, a, b }, heard);
        }
    }
}
