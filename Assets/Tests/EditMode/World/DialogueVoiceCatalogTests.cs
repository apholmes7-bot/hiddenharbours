using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Voice id → cadence</b>, the index that exists only because an ambient request crosses Core and
    /// Core may not name a World asset (rule 4).
    ///
    /// <para>The property worth holding is the forgiving one: an id nobody registered must resolve to a
    /// cadence that can finish a line, never to zero. A registry whose miss case is "0 characters per
    /// second" would hang a bubble on its first character forever, and it would do it only for the
    /// content somebody forgot to wire — i.e. in the field, not in the fixture.</para>
    /// </summary>
    public class DialogueVoiceCatalogTests
    {
        [SetUp]
        public void ClearBefore() => DialogueVoiceCatalog.Clear();

        [TearDown]
        public void ClearAfter() => DialogueVoiceCatalog.Clear();

        [Test]
        public void AnUnknownId_ResolvesToTheDefaultCadence_NotToZero()
        {
            DialogueVoice v = DialogueVoiceCatalog.VoiceOf("voice.nobody_registered_this");

            Assert.That(v.CharactersPerSecond,
                        Is.EqualTo(DialogueVoice.DefaultCharactersPerSecond).Within(1e-4f));
            Assert.That(v.ReadPauseSeconds,
                        Is.EqualTo(DialogueVoice.DefaultReadPauseSeconds).Within(1e-4f));
        }

        [Test]
        public void ANullOrEmptyId_ResolvesToTheDefaultCadence()
        {
            Assert.That(DialogueVoiceCatalog.VoiceOf(null).CharactersPerSecond,
                        Is.EqualTo(DialogueVoice.DefaultCharactersPerSecond).Within(1e-4f));
            Assert.That(DialogueVoiceCatalog.VoiceOf("").CharactersPerSecond,
                        Is.EqualTo(DialogueVoice.DefaultCharactersPerSecond).Within(1e-4f));
        }

        [Test]
        public void ARegisteredDef_IsFoundByItsId()
        {
            var def = ScriptableObject.CreateInstance<DialogueVoiceDef>();
            def.Id = "voice.test_skipper";
            def.Voice = new DialogueVoice
            {
                CharactersPerSecond = 12f,
                CharactersPerTick = 3,
                PunctuationPauseSeconds = 0.4f,
                ReadPauseSeconds = 2.5f,
            };

            DialogueVoiceCatalog.Register(def);

            Assert.IsTrue(DialogueVoiceCatalog.Knows("voice.test_skipper"));
            DialogueVoice v = DialogueVoiceCatalog.VoiceOf("voice.test_skipper");
            Assert.That(v.CharactersPerSecond, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(v.ReadPauseSeconds, Is.EqualTo(2.5f).Within(1e-4f));
        }

        /// <summary>
        /// What is registered is the LAUNDERED cadence, not the raw fields — so a def authored before the
        /// read-pause field existed cannot smuggle a zero into the index and close its bubbles instantly.
        /// </summary>
        [Test]
        public void ARegisteredDef_IsStoredSanitised()
        {
            var def = ScriptableObject.CreateInstance<DialogueVoiceDef>();
            def.Id = "voice.test_stale";
            def.Voice = default;                     // every field zero

            DialogueVoiceCatalog.Register(def);

            DialogueVoice v = DialogueVoiceCatalog.VoiceOf("voice.test_stale");
            Assert.That(v.CharactersPerSecond, Is.GreaterThan(0f));
            Assert.That(v.CharactersPerTick, Is.GreaterThan(0));
            Assert.That(v.ReadPauseSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void ANullDef_OrOneWithNoId_IsIgnoredRatherThanThrowing()
        {
            DialogueVoiceCatalog.Register((DialogueVoiceDef)null);

            var blank = ScriptableObject.CreateInstance<DialogueVoiceDef>();
            blank.Id = "   ";
            DialogueVoiceCatalog.Register(blank);

            // A library with a hole in it costs a default voice, not a broken wake-up.
            Assert.That(DialogueVoiceCatalog.Count, Is.Zero);
        }

        [Test]
        public void RegisteringTheSameIdTwice_KeepsTheLatest()
        {
            DialogueVoiceCatalog.Register("voice.test", new DialogueVoice { CharactersPerSecond = 10f });
            DialogueVoiceCatalog.Register("voice.test", new DialogueVoice { CharactersPerSecond = 20f });

            Assert.That(DialogueVoiceCatalog.Count, Is.EqualTo(1));
            Assert.That(DialogueVoiceCatalog.VoiceOf("voice.test").CharactersPerSecond,
                        Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void Clear_LeavesNothingBehindForTheNextFixture()
        {
            DialogueVoiceCatalog.Register("voice.test", DialogueVoice.Default);
            DialogueVoiceCatalog.Clear();

            Assert.That(DialogueVoiceCatalog.Count, Is.Zero);
            Assert.IsFalse(DialogueVoiceCatalog.Knows("voice.test"));
        }
    }
}
