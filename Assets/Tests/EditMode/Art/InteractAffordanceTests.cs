using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The interact affordance's RULES, as arithmetic — armed when something is offered and nothing is
    /// holding it down, disarmed the moment either stops being true.
    ///
    /// <para>These run without a scene, a camera or a renderer on purpose. The presenter's job is to glue
    /// two sprites to a third; the DECISION about whether anything should be lit at all is a pure
    /// function, and pinning it here is what stops "the highlight stayed up under the dialogue box" from
    /// being a thing only a human on a walk can catch.</para>
    /// </summary>
    public class InteractAffordanceStateTests
    {
        static InteractOfferChanged Offer(string id = "fixture.bed", string label = "Sleep") =>
            new InteractOfferChanged(InteractOfferSource.Fixture, id, label, Vector2.zero, 1f);

        [Test]
        public void CandidateArrives_Arms_OnThatId()
        {
            InteractAffordanceState state = InteractAffordanceState.For(Offer(), suppressed: false);

            Assert.IsTrue(state.Armed, "An offer with nothing suppressing it must light something.");
            Assert.AreEqual("fixture.bed", state.Id,
                "The affordance must arm on the id the offer named — lighting anything else would be a " +
                "second opinion about what the player is facing.");
        }

        [Test]
        public void CandidateLeaves_Disarms()
        {
            InteractAffordanceState state =
                InteractAffordanceState.For(InteractOfferChanged.None, suppressed: false);

            Assert.IsFalse(state.Armed, "No offer means nothing lit.");
            Assert.IsNull(state.Id);
        }

        [Test]
        public void ModalUp_Disarms_EvenWithAnOfferStanding()
        {
            // The offer is still there — a feeder does not go quiet under a modal, and the popup's own
            // suppression is what hides it. The affordance MUST follow the same rule or a dialogue box
            // would sit over a still-glowing bed.
            InteractAffordanceState state = InteractAffordanceState.For(Offer(), suppressed: true);

            Assert.IsFalse(state.Armed,
                "A suppressed offer must not light anything: the press it describes cannot happen, and a " +
                "highlight that outlives its own actionability teaches the player a lie.");
        }

        [Test]
        public void AnOfferWithNoLabel_IsNotAnOffer()
        {
            // Has is false without a label, and the affordance keys off the same property the popup does.
            var unlabelled = new InteractOfferChanged(
                InteractOfferSource.Fixture, "fixture.bed", "", Vector2.zero, 1f);

            Assert.IsFalse(InteractAffordanceState.For(unlabelled, suppressed: false).Armed,
                "A candidate that offers no words is machinery, not an offer — the popup stays quiet for " +
                "it and so must the highlight.");
        }

        [Test]
        public void SameAs_ComparesArmedAndId()
        {
            InteractAffordanceState bed = InteractAffordanceState.For(Offer("fixture.bed"), false);
            InteractAffordanceState stair = InteractAffordanceState.For(Offer("fixture.stair"), false);

            Assert.IsTrue(bed.SameAs(InteractAffordanceState.For(Offer("fixture.bed"), false)));
            Assert.IsFalse(bed.SameAs(stair),
                "Moving from one fixture to another is a CHANGE — if these compared equal the overlay " +
                "would stay stuck on the thing you walked away from.");
            Assert.IsFalse(bed.SameAs(InteractAffordanceState.Disarmed));
        }

        [Test]
        public void TheRuleIsTheSameOneThePopupUses()
        {
            // Not a tautology: it pins that the affordance reads InteractOfferVisibility rather than
            // keeping its own copy of the suppression list. The two surfaces are one statement to the
            // player, and this is the assertion that says so.
            var offer = Offer();
            Assert.AreEqual(InteractOfferVisibility.ShouldPresent(offer),
                            InteractAffordanceState.For(offer).Armed,
                            "The affordance must arm exactly when the popup would show. If these ever " +
                            "disagree the player sees a lit fixture under an empty corner, or the " +
                            "reverse.");
        }
    }

    /// <summary>
    /// The look's arithmetic — the cycle, the breath, and the half-strength rule the "both" variant is
    /// defined by.
    /// </summary>
    public class InteractAffordanceStyleTests
    {
        [Test]
        public void Next_VisitsEveryVariantAndWraps()
        {
            var seen = new System.Collections.Generic.HashSet<InteractAffordanceVariant>();
            var v = InteractAffordanceVariant.ShadeLift;

            for (int i = 0; i < InteractAffordanceStyle.VariantCount; i++)
            {
                seen.Add(v);
                v = InteractAffordanceStyle.Next(v);
            }

            Assert.AreEqual(InteractAffordanceStyle.VariantCount, seen.Count,
                "The dev menu's cycle must reach every variant, or the owner cannot judge the one it " +
                "skips.");
            Assert.AreEqual(InteractAffordanceVariant.ShadeLift, v, "The cycle must wrap.");
        }

        [Test]
        public void Pulse_OpensAtFull_BreathesToTheFloor_AndReturns()
        {
            float period = 1f / InteractAffordanceStyle.PulseHz;

            Assert.AreEqual(1f, InteractAffordanceStyle.Pulse(0f), 1e-4f,
                "The pulse is phased from the moment of arming and must OPEN at full — a highlight that " +
                "fades up reads as arriving late.");
            Assert.AreEqual(InteractAffordanceStyle.PulseFloor,
                            InteractAffordanceStyle.Pulse(period * 0.5f), 1e-4f);
            Assert.AreEqual(1f, InteractAffordanceStyle.Pulse(period), 1e-4f);
        }

        [Test]
        public void Pulse_NeverGoesOut()
        {
            // Sampled across two full breaths: the edge dims, but a highlight that vanished on the
            // downbeat would read as a flicker rather than as "this is the thing".
            float period = 1f / InteractAffordanceStyle.PulseHz;
            for (int i = 0; i <= 64; i++)
            {
                float t = (i / 64f) * period * 2f;
                float p = InteractAffordanceStyle.Pulse(t);
                Assert.GreaterOrEqual(p, InteractAffordanceStyle.PulseFloor - 1e-4f, $"t={t}");
                Assert.LessOrEqual(p, 1f + 1e-4f, $"t={t}");
            }
        }

        [Test]
        public void EachVariantUsesExactlyTheHalvesItsNameClaims()
        {
            Assert.IsTrue(InteractAffordanceStyle.UsesLift(InteractAffordanceVariant.ShadeLift));
            Assert.IsFalse(InteractAffordanceStyle.UsesOutline(InteractAffordanceVariant.ShadeLift));

            Assert.IsFalse(InteractAffordanceStyle.UsesLift(InteractAffordanceVariant.OutlinePulse));
            Assert.IsTrue(InteractAffordanceStyle.UsesOutline(InteractAffordanceVariant.OutlinePulse));

            Assert.IsTrue(InteractAffordanceStyle.UsesLift(InteractAffordanceVariant.Both));
            Assert.IsTrue(InteractAffordanceStyle.UsesOutline(InteractAffordanceVariant.Both));
        }

        [Test]
        public void Both_RunsEachHalfAtHalfStrength()
        {
            Assert.AreEqual(0.5f * InteractAffordanceStyle.LiftStrength(InteractAffordanceVariant.ShadeLift),
                            InteractAffordanceStyle.LiftStrength(InteractAffordanceVariant.Both), 1e-6f,
                            "'Both' is defined as each half at HALF strength — at full it would shout, " +
                            "which is the failure the owner asked to avoid in a dim room.");
            Assert.AreEqual(0.5f * InteractAffordanceStyle.OutlineStrength(InteractAffordanceVariant.OutlinePulse),
                            InteractAffordanceStyle.OutlineStrength(InteractAffordanceVariant.Both), 1e-6f);
        }
    }

    /// <summary>
    /// The outline's geometry. The edge is made by drawing the same sprite slightly LARGER behind the
    /// fixture, so the scale factor is the whole of its thickness — and it must not depend on how big the
    /// prop happens to be.
    /// </summary>
    public class InteractAffordanceOutlineGeometryTests
    {
        static Sprite MakeSprite(int wPx, int hPx, float ppu)
        {
            var tex = new Texture2D(wPx, hPx, TextureFormat.RGBA32, mipChain: false);
            return Sprite.Create(tex, new Rect(0, 0, wPx, hPx), new Vector2(0.5f, 0f), ppu);
        }

        [Test]
        public void OutlineScale_PutsTheRequestedHaloOnTheLongAxis()
        {
            const float ppu = 32f;
            Sprite sprite = MakeSprite(64, 32, ppu);

            float scale = InteractAffordancePresenter.OutlineScaleFor(sprite, pixels: 1f);

            float longest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            float grownPixels = (longest * scale - longest) * ppu;

            Assert.AreEqual(2f, grownPixels, 1e-3f,
                "One pixel of halo on EACH side of the long axis is two pixels of growth. Getting this " +
                "wrong is the difference between an edge and a glow.");
        }

        [Test]
        public void OutlineScale_IsIndependentOfPixelsPerUnit()
        {
            // A halo asked for in PIXELS must be the same number of pixels whatever the sprite's PPU —
            // this project has two live pixel grids (24 and 32) and an outline that changed thickness
            // between them would read as a bug in the art.
            float a = InteractAffordancePresenter.OutlineScaleFor(MakeSprite(64, 32, 32f), 1f);
            float b = InteractAffordancePresenter.OutlineScaleFor(MakeSprite(64, 32, 24f), 1f);

            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void OutlineScale_IsAlwaysAGrowth_AndDegradesSafely()
        {
            Assert.Greater(InteractAffordancePresenter.OutlineScaleFor(MakeSprite(16, 16, 32f), 1f), 1f,
                "A scale at or below 1 would draw the outline copy INSIDE the fixture, where it is " +
                "invisible — a silently dead variant.");

            Assert.AreEqual(1f, InteractAffordancePresenter.OutlineScaleFor(null, 1f), 1e-6f,
                "No sprite must be a no-op, not a divide by zero.");
            Assert.AreEqual(1f, InteractAffordancePresenter.OutlineScaleFor(MakeSprite(16, 16, 32f), 0f),
                1e-6f, "Zero pixels of outline means no growth.");
        }
    }

    /// <summary>
    /// The Core seam this lane resolves an offer through — <see cref="Interactables.TryGetTransform"/>.
    ///
    /// <para>Pinned from the art side because the art side is its only consumer, and because its two
    /// failure modes are both silent: an id that matches nothing simply does not light, and a registrant
    /// that is not a Component would throw on a naive cast. Neither shows up as a red test anywhere else.</para>
    /// </summary>
    public class InteractAffordanceTargetLookupTests
    {
        sealed class PocoFixture : IInteractable
        {
            public string Id => "fixture.poco";
            public string VerbLabel => "Poke";
            public Vector2 WorldPosition => Vector2.zero;
            public float ReachMeters => 1f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnFoot;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public void Interact(in InteractActor actor) { }
        }

        sealed class BehaviourFixture : MonoBehaviour, IInteractable
        {
            public string Id => "fixture.behaviour";
            public string VerbLabel => "Open";
            public Vector2 WorldPosition => transform.position;
            public float ReachMeters => 1f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnFoot;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public void Interact(in InteractActor actor) { }
        }

        [TearDown]
        public void TearDown() => Interactables.Clear();

        [Test]
        public void ARegisteredBehaviour_ResolvesToItsTransform()
        {
            var go = new GameObject("Bed");
            try
            {
                var fixture = go.AddComponent<BehaviourFixture>();
                Interactables.Register(fixture);

                Assert.IsTrue(Interactables.TryGetTransform("fixture.behaviour", out Transform t));
                Assert.AreSame(go.transform, t,
                    "The affordance draws on what this returns — the wrong transform lights the wrong " +
                    "object, which is worse than lighting nothing.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AnIdNobodyRegistered_ResolvesToNothing()
        {
            // The normal case for a transit or conversation offer: those are keyed by action and by NPC,
            // and neither is in this registry. It must be a quiet false, not a throw.
            Assert.IsFalse(Interactables.TryGetTransform("board", out Transform t));
            Assert.IsNull(t);
            Assert.IsFalse(Interactables.TryGetTransform(null, out _));
            Assert.IsFalse(Interactables.TryGetTransform("", out _));
        }

        [Test]
        public void APlainObjectRegistrant_ResolvesToNothing_RatherThanThrowing()
        {
            var poco = new PocoFixture();
            Interactables.Register(poco);

            Assert.DoesNotThrow(() => Interactables.TryGetTransform("fixture.poco", out _),
                "Plain-object registrants are legal — the resolver's own tests use them — and they " +
                "simply have nowhere to draw.");
            Assert.IsFalse(Interactables.TryGetTransform("fixture.poco", out Transform t));
            Assert.IsNull(t);
        }
    }

    /// <summary>
    /// MAGENTA GUARD for the affordance shader — the sibling of
    /// <see cref="LitSpriteShaderCompileGuardTests"/>, and it exists for that test's reason: a shader
    /// error does NOT fail a normal run. It is a magenta smear at runtime and a green CI log, on a GPU
    /// nobody in CI has.
    ///
    /// <para><b>Both modes are compiled, not just the shader.</b> The lift and the outline differ by a
    /// material float and by their blend factors, and this shader's whole job is to be two things — so
    /// compiling one variant and calling it guarded would leave half the feature unchecked.</para>
    /// </summary>
    public class InteractAffordanceShaderCompileGuardTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursInteractAffordance.shader";

        [Test]
        public void AffordanceShader_CompilesBothModes_NoShaderErrors()
        {
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(
                ShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader,
                $"Could not load the affordance shader at '{ShaderPath}'. A missing or renamed shader " +
                "leaves the magenta class UNGUARDED — treat it as a failure, not a pass.");

            var errors = new StringBuilder();
            Collect(errors, "<import>", ShaderUtil.GetShaderMessages(shader));

            foreach (float mode in new[] { 0f, 1f })
            {
                var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetFloat("_Mode", mode);

                int passes = mat.passCount > 0 ? mat.passCount : 1;
                for (int pass = 0; pass < passes; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);

                Collect(errors, mode > 0.5f ? "outline" : "lift", ShaderUtil.GetShaderMessages(mat.shader));
                Object.DestroyImmediate(mat);
            }

            Assert.IsEmpty(errors.ToString(),
                "The affordance shader has compile errors. The classic traps this project has paid for: " +
                "an operator character in a [Header(...)] label or property string is a ShaderLab PARSE " +
                "error, and a loop with a runtime bound is an HLSL variant error.\n" + errors);
        }

        [Test]
        public void TheShaderIsAlwaysIncluded_SoABuildCanStillFindIt()
        {
            // Shader.Find is how the presenter gets this at runtime, and NO material asset references it —
            // so without an Always Included entry the player build would strip it and the affordance would
            // be silently absent in a build while working perfectly in the editor.
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader);

            string settings = System.IO.File.ReadAllText("ProjectSettings/GraphicsSettings.asset");
            string guid = AssetDatabase.AssetPathToGUID(ShaderPath);

            Assert.IsTrue(settings.Contains(guid),
                $"'{ShaderPath}' (guid {guid}) is not in Always Included Shaders. Nothing else references " +
                "it, so a player build would strip it and the affordance would work in the editor and be " +
                "missing in the build — the worst shape a bug can have.");
        }

        static void Collect(StringBuilder errors, string where, ShaderMessage[] messages)
        {
            if (messages == null) return;
            foreach (ShaderMessage m in messages)
            {
                if (m.severity != ShaderCompilerMessageSeverity.Error) continue;
                errors.AppendLine($"[{where}] {m.file}({m.line}): {m.message} {m.messageDetails}");
            }
        }
    }
}
