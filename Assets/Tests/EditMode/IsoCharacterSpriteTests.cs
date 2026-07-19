using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pure gait/heading/frame maths behind the iso character presenter, and the RENDERER HAND-OFF
    /// between it and the deck haul animation — the one place two components could otherwise fight over
    /// <c>SpriteRenderer.sprite</c>.
    /// </summary>
    public class IsoCharacterSpriteTests
    {
        // ---- heading from motion --------------------------------------------------------------------

        [Test]
        public void Heading_UsesTheProjectsBearingConvention_ZeroIsNorthGrowingClockwise()
        {
            Assert.AreEqual(0f, IsoCharacterMath.HeadingFor(new Vector2(0f, 1f), 0.01f, 999f), 1e-3f, "N");
            Assert.AreEqual(90f, IsoCharacterMath.HeadingFor(new Vector2(1f, 0f), 0.01f, 999f), 1e-3f, "E");
            Assert.AreEqual(180f, Mathf.Abs(IsoCharacterMath.HeadingFor(new Vector2(0f, -1f), 0.01f, 999f)), 1e-3f, "S");
            Assert.AreEqual(-90f, IsoCharacterMath.HeadingFor(new Vector2(-1f, 0f), 0.01f, 999f), 1e-3f, "W");
            Assert.AreEqual(45f, IsoCharacterMath.HeadingFor(new Vector2(1f, 1f), 0.01f, 999f), 1e-3f, "NE");
        }

        [Test]
        public void Heading_IsHELD_WhenTheCharacterStops_NeverSnappingBackToNorth()
        {
            const float wasFacing = 217f;
            Assert.AreEqual(wasFacing, IsoCharacterMath.HeadingFor(Vector2.zero, 0.05f, wasFacing), 1e-4f);
            // ...and a sub-threshold twitch (a collider nudge) is not a turn either.
            Assert.AreEqual(wasFacing, IsoCharacterMath.HeadingFor(new Vector2(0.001f, 0f), 0.05f, wasFacing), 1e-4f);
        }

        // ---- gait from speed ------------------------------------------------------------------------

        [Test]
        public void Gait_IsPickedBySpeed_WithADeadBandAtTheBottom()
        {
            Assert.AreEqual(CharacterGait.Idle, IsoCharacterMath.GaitFor(0f, 0.35f, 4.5f));
            Assert.AreEqual(CharacterGait.Idle, IsoCharacterMath.GaitFor(0.2f, 0.35f, 4.5f), "jitter is not a step");
            Assert.AreEqual(CharacterGait.Walk, IsoCharacterMath.GaitFor(0.35f, 0.35f, 4.5f), "boundary walks");
            Assert.AreEqual(CharacterGait.Walk, IsoCharacterMath.GaitFor(3f, 0.35f, 4.5f), "the on-foot walk speed");
            Assert.AreEqual(CharacterGait.Run, IsoCharacterMath.GaitFor(4.5f, 0.35f, 4.5f), "boundary runs");
            Assert.AreEqual(CharacterGait.Run, IsoCharacterMath.GaitFor(9f, 0.35f, 4.5f));
        }

        [Test]
        public void Gait_SurvivesAnAssetAuthoredWithTheThresholdsInverted()
        {
            // run below walk would otherwise make a gait that can never be reached.
            Assert.AreEqual(CharacterGait.Idle, IsoCharacterMath.GaitFor(0.1f, 2f, 1f));
            Assert.AreEqual(CharacterGait.Run, IsoCharacterMath.GaitFor(2f, 2f, 1f));
        }

        // ---- frame from time ------------------------------------------------------------------------

        [Test]
        public void Frame_AdvancesAtTheSheetsRate_AndWraps()
        {
            Assert.AreEqual(0, IsoCharacterMath.FrameFor(0f, 10f, 8));
            Assert.AreEqual(3, IsoCharacterMath.FrameFor(0.35f, 10f, 8));
            Assert.AreEqual(0, IsoCharacterMath.FrameFor(0.8f, 10f, 8), "wraps at the end of the cycle");
            Assert.AreEqual(1, IsoCharacterMath.FrameFor(0.9f, 10f, 8));
        }

        [Test]
        public void Frame_FreezesRatherThanDividingByZero()
        {
            Assert.AreEqual(0, IsoCharacterMath.FrameFor(5f, 0f, 8), "a zero rate holds frame 0");
            Assert.AreEqual(0, IsoCharacterMath.FrameFor(5f, 10f, 0), "an empty cycle holds frame 0");
            Assert.AreEqual(0, IsoCharacterMath.FrameFor(float.NaN, 10f, 8), "NaN never propagates");
        }

        // ---- the def's gates ------------------------------------------------------------------------

        static CharacterVisualDef MakeDef(int facings, int idle, int walk, int run)
        {
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            def.FacingCount = facings;
            def.IdleFrameCount = idle; def.WalkFrameCount = walk; def.RunFrameCount = run;
            def.IdleSheet = Fill(facings * idle);
            def.WalkSheet = Fill(facings * walk);
            def.RunSheet = Fill(facings * run);
            return def;
        }

        static Sprite[] Fill(int n)
        {
            var tex = new Texture2D(2, 2);
            var set = new Sprite[n];
            for (int i = 0; i < n; i++)
                set[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f);
            return set;
        }

        [Test]
        public void AGaitIsAllOrNothing_APartialSheetIsDroppedWhole()
        {
            var def = MakeDef(8, 6, 8, 6);
            Assert.IsTrue(def.HasGait(CharacterGait.Walk));

            def.WalkSheet[17] = null;      // one hole
            Assert.IsFalse(def.HasGait(CharacterGait.Walk), "a hole would index a stale cell mid-stride");
            Assert.AreEqual(CharacterGait.Idle, def.PlayableGait(CharacterGait.Walk),
                            "no walk art → stand rather than show a stale cell");
            Assert.AreEqual(CharacterGait.Run, def.PlayableGait(CharacterGait.Run),
                            "the run sheet is still whole, so a run still runs");

            // Break the run sheet too and the ladder falls all the way down.
            def.RunSheet[5] = null;
            Assert.AreEqual(CharacterGait.Idle, def.PlayableGait(CharacterGait.Run), "run → walk → idle");
        }

        [Test]
        public void NoRunArt_TopsOutAtTheWalkCycle()
        {
            var def = MakeDef(8, 6, 8, 6);
            def.RunSheet = System.Array.Empty<Sprite>();
            Assert.IsFalse(def.HasGait(CharacterGait.Run));
            Assert.AreEqual(CharacterGait.Walk, def.PlayableGait(CharacterGait.Run));
        }

        [Test]
        public void SpriteLookup_IsRowMajor_AndWrapsRatherThanThrowing()
        {
            var def = MakeDef(8, 6, 8, 6);
            Assert.AreSame(def.WalkSheet[3 * 8 + 5], def.SpriteFor(CharacterGait.Walk, 3, 5));
            Assert.AreSame(def.SpriteFor(CharacterGait.Walk, 0, 0), def.SpriteFor(CharacterGait.Walk, 8, 8));
            Assert.AreSame(def.SpriteFor(CharacterGait.Walk, 7, 7), def.SpriteFor(CharacterGait.Walk, -1, -1));
        }

        [Test]
        public void NoArtAtAll_LeavesTheDefInert_SoTheOldSheetStillDraws()
        {
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            Assert.IsFalse(def.HasAnyArt());
            Assert.IsNull(def.SpriteFor(CharacterGait.Idle, 0, 0));
        }

        // ---- the renderer hand-off (the thing that must not become a fight) -------------------------

        [Test]
        public void HaulAnimation_SUSPENDS_TheIsoDriver_AndGivesTheRendererBackWhenTheHaulEnds()
        {
            var go = new GameObject("Player");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var iso = go.AddComponent<IsoCharacterSprite>();
                iso.Configure(MakeDef(8, 6, 8, 6));
                var haul = go.AddComponent<PlayerHaulAnimator>();
                haul.Configure(Fill(8));

                var walkSprite = Fill(1)[0];
                sr.sprite = walkSprite;
                Assert.IsFalse(iso.IsSuspended, "nothing is hauling yet");

                // A live haul claims the renderer...
                haul.OnHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.2f, false, 0f, 0f)));
                Assert.IsTrue(iso.IsSuspended,
                    "the haul owns the renderer, so the iso driver must stand down — two components " +
                    "writing SpriteRenderer.sprite is a fight, not a fallback");

                // ...and hands it back when the haul is over.
                haul.OnHaulStateChanged(new TrapHaulStateChanged(TrapHaulState.Idle));
                Assert.IsFalse(iso.IsSuspended, "the haul ended — the iso driver drives again");
                Assert.AreSame(walkSprite, sr.sprite, "the sprite is restored exactly as the haul found it");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SuspendClaimsAreCounted_SoOverlappingClaimantsCannotUnsuspendEarly()
        {
            var go = new GameObject("Character");
            try
            {
                go.AddComponent<SpriteRenderer>();
                var iso = go.AddComponent<IsoCharacterSprite>();

                iso.Suspend(); iso.Suspend();
                iso.Release();
                Assert.IsTrue(iso.IsSuspended, "one claimant is still holding it");
                iso.Release();
                Assert.IsFalse(iso.IsSuspended);
                iso.Release();
                Assert.IsFalse(iso.IsSuspended, "an extra release is harmless, never negative");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WalkController_YieldsTheSprite_WhenACompleteIsoSkinIsInstalled()
        {
            var go = new GameObject("Player");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                go.AddComponent<Rigidbody2D>();
                var iso = go.AddComponent<IsoCharacterSprite>();
                iso.Configure(MakeDef(8, 6, 8, 6));
                Assert.IsTrue(iso.HasArt);

                var isoCell = iso.Visual.SpriteFor(CharacterGait.Idle, 4, 0);
                sr.sprite = isoCell;
                sr.flipX = true;   // a stale flip left by the old mirrored 4-way sheet

                // Awake runs on AddComponent, and Awake ends in ApplyFrame — so simply adding the controller
                // is the whole test: with the guard it writes nothing, without it, it clears flipX.
                go.AddComponent<PlayerWalkController>();

                Assert.AreSame(isoCell, sr.sprite,
                    "the 4-way walk controller must not overwrite the iso cell");
                Assert.IsTrue(sr.flipX,
                    "...and must not touch flipX either — the iso driver owns clearing it, and a stale " +
                    "flip written here would invert every westward facing");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
