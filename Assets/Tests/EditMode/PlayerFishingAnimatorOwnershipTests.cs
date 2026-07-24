using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The fight animator's RENDERER-OWNERSHIP contract (<see cref="PlayerFishingAnimator"/>) — the
    /// statue-still regression: the component must only ever claim the shared SpriteRenderer when it
    /// has an actual cell to draw. The old code claimed FIRST and bailed on a dead sprite, which froze
    /// the fisher on his last walk cell for the whole fight, silently. These tests pin the fixed order
    /// (drawable-then-claim), the loud one-shot diagnostics, and the new cast/hold pose surface.
    /// </summary>
    public class PlayerFishingAnimatorOwnershipTests
    {
        private readonly System.Collections.Generic.List<Object> _spawned = new();
        private GameObject _go;
        private SpriteRenderer _sr;
        private PlayerFishingAnimator _anim;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Fisher");
            _spawned.Add(_go);
            _anim = _go.AddComponent<PlayerFishingAnimator>();   // RequireComponent adds the renderer
            _sr = _go.GetComponent<SpriteRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            s.name = name;
            _spawned.Add(s);
            return s;
        }

        private Sprite[] MakeSheet(string name, int framesPerDir)
        {
            var frames = new Sprite[8 * framesPerDir];
            for (int i = 0; i < frames.Length; i++) frames[i] = MakeSprite($"{name}_{i}");
            return frames;
        }

        private static FishingState State(FishingPhase phase, float castCharge = 0f,
                                          float aimX = 0f, float aimY = 0f)
            => new FishingState(phase, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f,
                                depth01: 0f, slackWindowOpen: false, rodBend01: 0f,
                                fishOffsetX: 0f, fishOffsetY: 0f,
                                castCharge01: castCharge, castAimX: aimX, castAimY: aimY, rigDepthM: 0f);

        [Test]
        public void AWiredSheet_ClaimsTheRenderer_AndDrawsTheCell()
        {
            Sprite[] bite = MakeSheet("bite", 6);
            _anim.Configure(bite, null, null, null);

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));

            Assert.AreEqual(FishingPose.Bite, _anim.Pose);
            Assert.IsTrue(_anim.OwnsRenderer);
            Assert.AreSame(bite[_anim.Row * 6 + 0], _sr.sprite, "the row-0 first cell draws");
        }

        [Test]
        public void DeadCells_NeverClaimTheRenderer_AndWarnOnce()
        {
            // A stale scene: the array has the right SHAPE (48 = 8×6) but every ref resolved null.
            _anim.Configure(new Sprite[48], null, null, null);
            _sr.sprite = MakeSprite("walkCell");
            Sprite before = _sr.sprite;

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("resolved NULL"));
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));

            Assert.AreEqual(FishingPose.None, _anim.Pose,
                "a pose with nothing to draw must fall through, never freeze");
            Assert.IsFalse(_anim.OwnsRenderer,
                "claiming a renderer you cannot draw into IS the statue-still bug");
            Assert.AreSame(before, _sr.sprite, "the walk cell is untouched");

            // The warning is one-shot — a second bite must not spam.
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Idle)));
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AMissingSheet_FallsThrough_AndWarnsOnce()
        {
            _anim.Configure(null, null, null, null);   // nothing wired at all
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("not wired"));
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));
            Assert.AreEqual(FishingPose.None, _anim.Pose);
            Assert.IsFalse(_anim.OwnsRenderer);
        }

        [Test]
        public void TheWindBack_ScrubsTheCastBackSheet_ByThePublishedCharge()
        {
            Sprite[] castBack = MakeSheet("castBack", 6);
            _anim.ConfigureCastAndHold(null, castBack, null);

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.WindBack, castCharge: 0f)));
            Assert.AreEqual(FishingPose.CastBack, _anim.Pose);
            Assert.AreEqual(0, _anim.Frame, "unloaded = frame 0");

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.WindBack, castCharge: 1f)));
            Assert.AreEqual(5, _anim.Frame, "full charge = the fully-drawn-back last frame");
            Assert.AreSame(castBack[_anim.Row * 6 + 5], _sr.sprite);
        }

        [Test]
        public void TheWait_HoldsTheRod_AndFacesTheCastAim()
        {
            Sprite[] hold = MakeSheet("hold", 6);
            _anim.ConfigureCastAndHold(hold, null, null);

            // Aim due EAST — the fisher must face row 2 (8 rows, CW from North, as-labelled bake).
            _anim.OnFishingStateChanged(new FishingStateChanged(
                State(FishingPhase.Waiting, aimX: 5f, aimY: 0f)));

            Assert.AreEqual(FishingPose.Hold, _anim.Pose, "standing still, line out = the hold cycle");
            Assert.AreEqual(2, _anim.Row, "the cast-path far end picks the facing (no more frozen row)");
            Assert.IsTrue(_anim.OwnsRenderer);
        }

        [Test]
        public void TheResultBeat_HandsTheRendererBack_ExactlyAsItWas()
        {
            Sprite[] bite = MakeSheet("bite", 6);
            _anim.Configure(bite, null, null, null);
            _sr.sprite = MakeSprite("walkCell");
            Sprite walk = _sr.sprite;

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));
            Assert.IsTrue(_anim.OwnsRenderer);

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Snapped)));
            Assert.IsFalse(_anim.OwnsRenderer, "a snap goes straight back to walking");
            Assert.AreSame(walk, _sr.sprite, "the walk cell comes back exactly as it was");
        }
    }
}
