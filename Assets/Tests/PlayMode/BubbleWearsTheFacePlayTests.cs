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
    /// <b>THE BUBBLE WEARS THE FACE ITS OWN RIG DREW</b> — and, the half that is easy to miss, at the
    /// right SIZE.
    ///
    /// <para><b>Why this is not a one-line assertion about a font field.</b> <c>HarbourType</c> is a
    /// Unity legacy (non-dynamic) <c>Font</c>: uGUI passes it <c>fontSize = 0</c>, so it ignores
    /// <c>Text.fontSize</c> and draws every glyph at its baked size — advance 5, line height 10. The
    /// bubble's art is drawn at <see cref="DialogueBubbleKit.ArtScale"/>. Swapping the font alone would
    /// therefore put every line of dialogue on screen at a THIRD of its size inside a panel three times
    /// too big for it, and a test that only checked <c>text.font</c> would call that a pass.</para>
    ///
    /// <para><b>So the property measured here is geometric</b>: for every text in the bubble, <b>the
    /// rect times its own scale IS the area it was meant to fill</b>, and its corner is that area's
    /// corner. Hold those and the words land inside the panel at the size the kit drew them, whichever
    /// font is loaded — which is what makes this checkable with no graphics device and no screenshot.
    /// What it cannot tell anybody is whether it LOOKS right; that is the owner's eye.</para>
    ///
    /// <para><b>Both surfaces, on purpose.</b> The modal bubble and the ambient one are separate
    /// presenters, and the failure this guards against is not "the font is wrong" but "the two are
    /// wearing different faces on one screen", which only shows up when a villager speaks near a
    /// conversation.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠ do not relax).</b> Nothing renders or reads pixels.</para>
    /// </summary>
    public class BubbleWearsTheFacePlayTests
    {
        const float Scale = DialogueBubbleKit.ArtScale;

        static readonly float BubblePadX = DialogueBubbleKit.PanelInsetL * Scale;
        static readonly float BubblePadBottom = DialogueBubbleKit.PanelInsetB * Scale;
        static readonly float BubblePadTop =
            (DialogueBubbleKit.PanelInsetT + DialogueBubbleKit.ChipHeight) * Scale;

        readonly List<Object> _spawned = new();
        DialoguePresenter _modal;
        Transform _speaker;

        static DialogueVoice Voice => new DialogueVoice
        {
            CharactersPerSecond = 400f,
            CharactersPerTick = 1,
            PunctuationPauseSeconds = 0f,
            ReadPauseSeconds = 0.2f,
        };

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();
            InteractionGate.Reset();
            DialogueVoiceCatalog.Clear();

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

        // ---- the face itself ------------------------------------------------------------------

        /// <summary>
        /// The tripwire the charter asked for: the bubble's font IS the baked asset, by object identity
        /// rather than by name — a look-alike with the right name would still draw at the wrong size.
        ///
        /// <para>⚠ It is conditional on the face being imported, and says so loudly when it is not. The
        /// bubble is required to keep WORKING without the face (that is the greybox arm every other test
        /// in the project runs against), so an unimported face is an <c>Ignore</c> and not a failure —
        /// but a silent skip would let the whole point of this PR rot, hence the message.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheModalBubbleWearsTheBakedFace()
        {
            yield return ShowModal("Fine morning.");

            Font face = BubbleFace.Face;
            if (face == null)
                Assert.Ignore($"{HarbourType.ResourceKey} is not imported — the bubble is on its " +
                              "built-in fallback. Bake the face (Hidden Harbours ▸ Rigs ▸ Harbour Type) " +
                              "and this becomes a real assertion.");

            Text body = Find(_modal, "Body");
            Assert.IsNotNull(body, "the bubble has no Body text");
            Assert.IsTrue(ReferenceEquals(body.font, face),
                "the bubble is not drawing in HarbourType — it is wearing " +
                $"'{(body.font != null ? body.font.name : "nothing")}'");
        }

        [UnityTest]
        public IEnumerator BothBubbleSurfacesWearTheSameFace()
        {
            yield return ShowModal("Fine morning.");

            AmbientSpeechPresenter ambient = AmbientSpeechPresenter.Instance;
            Assert.IsNotNull(ambient, "the ambient presenter did not install itself");

            int id = AmbientSpeechId.Next();
            EventBus.Publish(new AmbientSpeechRequested(
                id, _speaker, new Vector3(0f, 2.1f, 0f), "npc.test", null, "Some weather", null,
                AmbientSpeechKind.NpcToNpc));
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
            Assert.That(ambientBody.rectTransform.localScale.x,
                        Is.EqualTo(modalBody.rectTransform.localScale.x).Within(1e-4f),
                        "the two surfaces scale their type differently, so one of them is the wrong size");
        }

        // ---- the size, which is the half a font check misses -------------------------------------

        /// <summary>
        /// ⭐⭐ <b>The property the whole change turns on.</b> The body text's rect, multiplied by its own
        /// localScale, must equal the panel's inner area — the panel inset by the kit's own paddings.
        /// Hold that and the words fill the panel exactly at either scale; break it and they are three
        /// times too big or three times too small, which no font assertion would notice.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBodyTextCoversExactlyThePanelsInnerArea()
        {
            yield return ShowModal("Fine morning, and a fair wind with it.");

            Text body = Find(_modal, "Body");
            RectTransform panel = body.transform.parent as RectTransform;
            Assert.IsNotNull(panel, "the body text is not parented to the panel");

            Vector2 expected = new Vector2(panel.sizeDelta.x - BubblePadX * 2f,
                                           panel.sizeDelta.y - BubblePadTop - BubblePadBottom);
            Vector2 actual = Vector2.Scale(body.rectTransform.sizeDelta,
                                           body.rectTransform.localScale);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f),
                $"the body text covers {actual.x:0.##} canvas units of width where the panel's inner " +
                $"area is {expected.x:0.##} — scale {body.rectTransform.localScale.x} applied to rect " +
                $"{body.rectTransform.sizeDelta.x:0.##}");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f),
                $"the body text covers {actual.y:0.##} canvas units of height where the panel's inner " +
                $"area is {expected.y:0.##}");
        }

        /// <summary>The other half of the same property: the text starts at the panel's INSET CORNER, so
        /// filling the right amount of space is not enough — it has to be the right space.</summary>
        [UnityTest]
        public IEnumerator TheBodyTextStartsAtThePanelsInsetCorner()
        {
            yield return ShowModal("Fine morning.");

            Text body = Find(_modal, "Body");
            RectTransform panel = body.transform.parent as RectTransform;
            RectTransform rt = body.rectTransform;

            Assert.That(rt.pivot, Is.EqualTo(new Vector2(0f, 1f)),
                        "the text must pivot at its top-left, or the scale grows it off the panel");

            var expected = new Vector2(-panel.sizeDelta.x * 0.5f + BubblePadX,
                                       panel.sizeDelta.y * 0.5f - BubblePadTop);
            Assert.That(rt.anchoredPosition.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(rt.anchoredPosition.y, Is.EqualTo(expected.y).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TheAmbientBodyTextCoversItsOwnPanelsInnerArea()
        {
            AmbientSpeechPresenter ambient = AmbientSpeechPresenter.Instance;
            Assert.IsNotNull(ambient);

            int id = AmbientSpeechId.Next();
            EventBus.Publish(new AmbientSpeechRequested(
                id, _speaker, new Vector3(0f, 2.1f, 0f), "npc.test", null,
                "Some weather we are having", null, AmbientSpeechKind.NpcToNpc));
            yield return null;
            yield return null;

            Text body = Find(ambient, "Body");
            RectTransform panel = body.transform.parent as RectTransform;

            Vector2 expected = new Vector2(panel.sizeDelta.x - BubblePadX * 2f,
                                           panel.sizeDelta.y - BubblePadTop - BubblePadBottom);
            Vector2 actual = Vector2.Scale(body.rectTransform.sizeDelta, body.rectTransform.localScale);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }

        /// <summary>
        /// The panel still GROWS with the words. This is the regression the scale arithmetic could
        /// silently cause: a measurement taken in the text's units and compared against a panel in canvas
        /// units, with the multiply missing, sizes every bubble to the minimum and clips the line.
        /// </summary>
        [UnityTest]
        public IEnumerator ALongerLineStillMakesAWiderOrTallerBubble()
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
                $"a long line produced the same panel as a short one ({small} vs {big}) — the text " +
                "measurement is not reaching the panel size");
        }

        /// <summary>
        /// ⭐ <b>The kit and the face agree, and this is where you can see it.</b> The face is monospace
        /// at <see cref="HarbourType.Advance"/> per character; the kit sizes its panel with
        /// <c>PanelWidthFor(cols) = insetL + (cols × cellWidth − 1) + insetR</c>. So a line of exactly
        /// <see cref="DialogueBubbleKit.MaxCols"/> characters should land on the widest panel the kit
        /// allows — <i>"past that the line wants a second bubble, not a taller one"</i>.
        ///
        /// <para>If the scale arithmetic were out by a factor of three in either direction, this would
        /// miss by hundreds of units. It is the cheapest end-to-end check that the type and the paper it
        /// sits on are drawn to one grid.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AFullWidthLineLandsOnTheKitsWidestPanel()
        {
            if (BubbleFace.Face == null)
                Assert.Ignore($"{HarbourType.ResourceKey} is not imported — the monospace grid only " +
                              "exists with the baked face.");

            yield return ShowModal(new string('m', DialogueBubbleKit.MaxCols));

            RectTransform panel = Find(_modal, "Body").transform.parent as RectTransform;
            float widest = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MaxCols) * Scale;

            Assert.That(panel.sizeDelta.x, Is.EqualTo(widest).Within(1f),
                $"a {DialogueBubbleKit.MaxCols}-character line made a panel {panel.sizeDelta.x:0.#} " +
                $"units wide; the kit's widest is {widest:0.#}. The type and the panel are not on one " +
                "grid — check the scale multiply in SizeBubbleFor.");
        }

        /// <summary>The monospace promise the caret depends on: N characters measure N advances, in the
        /// text's own units, whatever N is.</summary>
        [UnityTest]
        public IEnumerator TheFaceMeasuresOneAdvancePerCharacter()
        {
            if (BubbleFace.Face == null) Assert.Ignore("the face is not imported");

            yield return ShowModal("mmmm");
            Text body = Find(_modal, "Body");
            body.text = "mmmm";

            Assert.That(body.preferredWidth, Is.EqualTo(4f * HarbourType.Advance).Within(0.5f),
                "four characters did not measure four advances — the face is not monospace here, and " +
                "the caret owning a cell depends on it");
        }

        // ---- the fallback still works -------------------------------------------------------------

        /// <summary>
        /// Without the face the bubble must be EXACTLY what it was before this change: scale 1, and the
        /// rect equal to the area rather than a third of it. The greybox is what every other test in the
        /// project runs against, so breaking it would break them and not this.
        /// </summary>
        [Test]
        public void TheFallbackScaleIsOne_AndTheRectIsTheAreaUnchanged()
        {
            Assert.That(BubbleFace.FallbackScale, Is.EqualTo(1f));
            Assert.That(BubbleFace.ScaleFor(BubbleFace.Fallback()), Is.EqualTo(1f),
                        "the built-in font honours fontSize and must not be scaled as well");

            var area = new Vector2(300f, 90f);
            Assert.That(BubbleFace.RectFor(area, BubbleFace.FallbackScale), Is.EqualTo(area));
        }

        [Test]
        public void TheFaceScaleIsTheKitsArtScale_NotANumberOfItsOwn()
        {
            Assert.That(BubbleFace.FaceScale, Is.EqualTo((float)DialogueBubbleKit.ArtScale),
                        "the type and the panel must be drawn at ONE scale, or the words and the paper " +
                        "they sit on disagree");
        }

        [Test]
        public void RectForAndAreaFor_AreInverses()
        {
            // The one function whose failure mode is silent: divide instead of multiply and the words
            // are nine times the wrong size, with nothing throwing.
            var area = new Vector2(357f, 120f);
            Vector2 rect = BubbleFace.RectFor(area, BubbleFace.FaceScale);
            Assert.That(BubbleFace.AreaFor(rect, BubbleFace.FaceScale).x, Is.EqualTo(area.x).Within(1e-3f));
            Assert.That(BubbleFace.AreaFor(rect, BubbleFace.FaceScale).y, Is.EqualTo(area.y).Within(1e-3f));
            Assert.That(rect.x, Is.LessThan(area.x), "the rect must be SMALLER than the area it scales up to");
        }

        [Test]
        public void AZeroScaleDoesNotDivideByZero()
        {
            var area = new Vector2(100f, 50f);
            Assert.That(BubbleFace.RectFor(area, 0f), Is.EqualTo(area),
                        "a nonsense scale should leave the rect alone, not produce infinities");
        }
    }
}
