using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE BUBBLE WEARS THE FACE ITS OWN RIG DREW</b> — and lands it on the grid the panel is drawn on.
    ///
    /// <para><b>The size is the half a font check would miss.</b> <c>HarbourType</c> is baked at
    /// <see cref="HarbourType.GlyphHeight"/> px with a monospace advance of
    /// <see cref="HarbourType.Advance"/>, and Unity draws a font at <c>fontSize / font.fontSize</c> times
    /// its baked size. So the bubble's <c>fontSize</c> has to be <c>GlyphHeight × ArtScale</c> for one
    /// glyph pixel to land on one kit pixel — and a test that only asserted <c>text.font</c> would call a
    /// bubble whose type sat at a fractional scale a pass.</para>
    ///
    /// <para><b>What is measured here</b> is that one character occupies <c>Advance × ArtScale</c> canvas
    /// units, that N of them occupy N of those, and that a line of
    /// <see cref="DialogueBubbleKit.MaxCols"/> characters therefore fills the kit's widest panel exactly.
    /// That is the monospace grid the caret owns, stated as arithmetic a headless run can check.</para>
    ///
    /// <para><b>Both surfaces, on purpose.</b> The modal bubble and the ambient pool are separate
    /// presenters; the failure guarded against is not "the font is wrong" but "the two disagree", which
    /// only shows when a villager speaks near a conversation.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠ do not relax).</b> Nothing renders or reads pixels.</para>
    /// </summary>
    public class BubbleWearsTheFacePlayTests
    {
        /// <summary>Why a missing face is a FAILURE and not a skip — see the tripwire below.</summary>
        const string MissingFace =
            "the baked face did not load through Resources.Load<Font>(\"Type/HarbourType\"). " +
            "It is COMMITTED under Assets/_Project/Art/UI/Resources/Type, so this is not \"the art has " +
            "not landed yet\" — it is the asset having moved out of a Resources root, been renamed, or " +
            "failed to import. The bubble would fall back to the built-in font and nobody would notice.";

        readonly List<Object> _spawned = new();
        DialoguePresenter _modal;
        Transform _speaker;

        static DialogueVoice Voice => new DialogueVoice
        {
            CharactersPerSecond = 4000f,
            CharactersPerTick = 1,
            PunctuationPauseSeconds = 0f,
            ReadPauseSeconds = 30f,
        };

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();
            InteractionGate.Reset();
            DialogueVoiceCatalog.Clear();
            BubbleFace.ForgetCache();

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            _speaker = Spawn("Speaker").transform;
            _modal = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();

            if (AmbientSpeechPresenter.Instance != null)
            {
                AmbientSpeechPresenter.Instance.SetCamera(cam);
                AmbientSpeechPresenter.Instance.CancelAll();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AmbientSpeechPresenter.Instance != null)
            {
                AmbientSpeechPresenter.Instance.CancelAll();
                AmbientSpeechPresenter.Instance.SetCamera(null);
            }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            DialogueVoiceCatalog.Clear();
            BubbleFace.ForgetCache();
            InteractionGate.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static Text Find(Component root, string name)
        {
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t.gameObject.name == name) return t;
            return null;
        }

        IEnumerator ShowModal(string line, string speakerName = null)
        {
            _modal.Play(new DialogueRequest(
                new[] { new DialogueLine(speakerName, line) },
                _speaker, new Vector3(0f, 2.1f, 0f), Voice, null, "dialogue.test", "npc.test"));
            yield return null;
            yield return null;
        }

        // ---- the tripwire ---------------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>A missing face FAILS here, it does not skip.</b>
        ///
        /// <para>The runtime is deliberately forgiving — <see cref="BubbleFace.Resolve"/> falls back to
        /// the built-in font so the bubble keeps working in a greybox, which is the project's standing
        /// "right art or fallback, never wrong art" discipline. <b>The suite must not be forgiving in the
        /// same direction.</b> The face is COMMITTED; if it stops loading, the game quietly ships in
        /// Unity's built-in font and this is the only thing that would say so.</para>
        ///
        /// <para><b>A different arm from <c>HarbourTypeBakeTests</c></b>, which loads the asset through
        /// <c>AssetDatabase</c> by path. This loads it the way the GAME does — <c>Resources.Load</c> by
        /// key at runtime — so it catches the asset leaving a Resources root, or the key drifting from
        /// the folder, neither of which a path check can see.</para>
        ///
        /// <para><b>No matching runtime warning, on purpose:</b> an unexpected log message fails whichever
        /// test happens to be running, so a "loud" runtime would redden unrelated fixtures instead.</para>
        /// </summary>
        [Test]
        public void TheFaceIsInTheProject_AndLoadsThroughItsResourcesKey()
        {
            BubbleFace.ForgetCache();
            Assert.IsNotNull(BubbleFace.Face, MissingFace);
        }

        // ---- the face ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheModalBubbleWearsTheBakedFace()
        {
            yield return ShowModal("Fine morning.");
            Assert.IsNotNull(BubbleFace.Face, MissingFace);

            Text body = Find(_modal, "Body");
            Assert.IsNotNull(body, "the bubble has no Body text");
            Assert.IsTrue(ReferenceEquals(body.font, BubbleFace.Face),
                "the bubble is not drawing in HarbourType — it is wearing " +
                $"'{(body.font != null ? body.font.name : "nothing")}'");
        }

        [UnityTest]
        public IEnumerator BothBubbleSurfacesWearTheSameFaceAtTheSameSize()
        {
            yield return ShowModal("Fine morning.");

            AmbientSpeechPresenter ambient = AmbientSpeechPresenter.Instance;
            Assert.IsNotNull(ambient, "the ambient presenter did not install itself");

            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), _speaker, new Vector3(0f, 2.1f, 0f), "npc.test", null,
                "Some weather", null, AmbientSpeechKind.NpcToNpc));
            yield return null;
            yield return null;

            Text modalBody = Find(_modal, "Body");
            Text ambientBody = Find(ambient, "Body");
            Assert.IsNotNull(ambientBody, "the ambient pool has no Body text");

            // ⭐ The failure this exists for: not "the font is wrong" but "the two surfaces disagree",
            // which is only visible when a villager speaks near a conversation.
            Assert.IsTrue(ReferenceEquals(modalBody.font, ambientBody.font),
                $"the conversation bubble wears '{modalBody.font?.name}' and the overheard one wears " +
                $"'{ambientBody.font?.name}' — two faces on one screen");
            Assert.That(ambientBody.fontSize, Is.EqualTo(modalBody.fontSize),
                "the two surfaces draw their type at different sizes, so one of them is off the grid");
        }

        /// <summary>
        /// ⭐ The defect this PR fixes beyond the swap itself. The body was already at 24 — which is
        /// <c>GlyphHeight × ArtScale</c>, by luck of the greybox rather than by derivation — but the name
        /// chip was at 17 and the option rows at 22, neither a multiple of the baked 8. Wearing the face
        /// at those sizes would resample every glyph in them at a fractional scale.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryTextInTheBubbleIsAtTheDerivedSize_NotJustTheBody()
        {
            var options = new[]
            {
                new DialogueOption { Id = "option.a", Label = "Ask about the dory" },
                new DialogueOption { Id = "option.b", Label = "Ask about the weather" },
            };
            _modal.Play(new DialogueRequest(
                new[] { new DialogueLine("Aunt Ginny", "Fine morning.") },
                _speaker, new Vector3(0f, 2.1f, 0f), Voice, options, "dialogue.test", "npc.test"));
            yield return null;
            _modal.Advance();      // fill the line
            _modal.Advance();      // ...and open the rows
            yield return null;
            yield return null;

            foreach (Text t in _modal.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == "ContinueHint") continue;   // deliberately smaller, and hidden
                Assert.That(t.fontSize, Is.EqualTo(BubbleFace.FontSize),
                    $"'{t.gameObject.name}' draws at {t.fontSize}, not the derived " +
                    $"{BubbleFace.FontSize}. A size that is not a whole multiple of " +
                    $"{HarbourType.GlyphHeight} resamples the baked glyphs at a fractional scale.");
            }
        }

        // ---- the grid -----------------------------------------------------------------------------

        [Test]
        public void TheDerivedSizePutsOneGlyphPixelOnOneKitPixel()
        {
            Assert.That(BubbleFace.FontSize,
                        Is.EqualTo(HarbourType.GlyphHeight * DialogueBubbleKit.ArtScale),
                        "the type and the panel must be drawn at ONE scale");
            Assert.That(BubbleFace.FontSize % HarbourType.GlyphHeight, Is.Zero,
                        "a fontSize that is not a whole multiple of the baked glyph height resamples " +
                        "every glyph — which is what the point filter and the pixel-perfect zoom tiers " +
                        "exist to avoid");
            Assert.That(BubbleFace.AdvanceUnits,
                        Is.EqualTo(HarbourType.Advance * DialogueBubbleKit.ArtScale));
        }

        /// <summary>
        /// ⭐⭐ <b>The measurement that catches a wrong scale.</b> The face is monospace, so N characters
        /// must span exactly N × <see cref="BubbleFace.AdvanceUnits"/> canvas units. At a fractional or
        /// doubled scale this misses by a factor, not by a pixel.
        /// </summary>
        [UnityTest]
        public IEnumerator NCharactersSpanExactlyNAdvances()
        {
            yield return ShowModal("m");
            Assert.IsNotNull(BubbleFace.Face, MissingFace);

            Text body = Find(_modal, "Body");
            body.horizontalOverflow = HorizontalWrapMode.Overflow;

            foreach (int n in new[] { 1, 4, 10, 30 })
            {
                body.text = new string('m', n);
                Assert.That(body.preferredWidth,
                            Is.EqualTo(n * BubbleFace.AdvanceUnits).Within(0.51f),
                            $"{n} characters measured {body.preferredWidth:0.##} canvas units; the " +
                            $"monospace cell is {BubbleFace.AdvanceUnits}, so they should measure " +
                            $"{n * BubbleFace.AdvanceUnits}");
            }
        }

        /// <summary>
        /// ⭐ <b>The kit and the face agree to the unit.</b> The kit sizes its panel with
        /// <c>PanelWidthFor(cols) = insetL + (cols × cellWidth − 1) + insetR</c>, and the face advances
        /// one cell per character. So a line of exactly <see cref="DialogueBubbleKit.MaxCols"/> characters
        /// lands on the widest panel the kit allows — <i>"past that the line wants a second bubble, not a
        /// taller one"</i>. Those two numbers were written for each other before either was wired up.
        /// </summary>
        [UnityTest]
        public IEnumerator AFullWidthLineLandsOnTheKitsWidestPanel()
        {
            Assert.IsNotNull(BubbleFace.Face, MissingFace);
            yield return ShowModal(new string('m', DialogueBubbleKit.MaxCols));

            RectTransform panel = Find(_modal, "Body").transform.parent as RectTransform;
            float widest = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MaxCols)
                           * DialogueBubbleKit.ArtScale;

            Assert.That(panel.sizeDelta.x, Is.EqualTo(widest).Within(2f),
                $"a {DialogueBubbleKit.MaxCols}-character line made a panel {panel.sizeDelta.x:0.#} " +
                $"units wide; the kit's widest is {widest:0.#}. The type and the panel are not on one " +
                "grid.");
        }

        [UnityTest]
        public IEnumerator ALongerLineStillMakesABiggerBubble()
        {
            yield return ShowModal("Aye.");
            RectTransform panel = Find(_modal, "Body").transform.parent as RectTransform;
            Vector2 small = panel.sizeDelta;

            _modal.Close();
            yield return null;

            yield return ShowModal("Aye, and there is a great deal more to say about it than that, " +
                                   "as it happens, on a morning like this one.");
            Vector2 big = (Find(_modal, "Body").transform.parent as RectTransform).sizeDelta;

            Assert.IsTrue(big.x > small.x + 0.01f || big.y > small.y + 0.01f,
                $"a long line produced the same panel as a short one ({small} vs {big})");
        }

        // ---- the fallback -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithoutTheFaceTheBubbleStillDraws_AtTheSameSize()
        {
            // The greybox arm every other bubble fixture in the project runs against.
            BubbleFace.UseFallbackForPlates(true);
            yield return ShowModal("Fine morning.");

            Text body = Find(_modal, "Body");
            Assert.IsNotNull(body.font, "the bubble has no font at all without the face");
            Assert.IsFalse(BubbleFace.IsFace(body.font));
            Assert.That(body.fontSize, Is.EqualTo(BubbleFace.FontSize),
                        "the fallback should draw at the same size, so swapping the face in and out does " +
                        "not move the layout");

            BubbleFace.ForgetCache();
        }
    }
}
