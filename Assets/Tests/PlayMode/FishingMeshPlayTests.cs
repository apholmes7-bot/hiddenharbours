using System;
using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class FishingMeshPlayTests
    {
        DeckRiderMeshPresenterPlayTests _rig;
        [SetUp] public void SetUp() { _rig = new DeckRiderMeshPresenterPlayTests(); _rig.SetUp(); }
        [TearDown] public void TearDown() => _rig.TearDown();
        [UnityTest] public IEnumerator FullRodSequenceKeepsOneMeshBodyAshoreAndAboard() => _rig.RunFishingMeshSequence();
        [UnityTest] public IEnumerator MissingClipsAndNestedClaimsKeepTheSprite() => _rig.RunFishingMeshFallbacks();
        [UnityTest] public IEnumerator OneDigKeepsTheMeshAndReturnsToGait() => _rig.RunDigMeshSequence();
    }

    public partial class DeckRiderMeshPresenterPlayTests
    {
        static readonly string[] FishingKeys = { "hold", "castBack", "castRelease", "bite", "strike", "reel", "land", "dig" };
        const int FishingFrames = 4;

        Rig NewFishingRig(out PlayerFishingAnimator animator)
        {
            Rig rig = NewAshoreRig();
            var clips = new List<CharacterSkinDef.SkinClip>(rig.Skin.Clips);
            var states = new List<string>(rig.Skin.MeshStates);
            foreach (string key in FishingKeys) { clips.Add(Clip(key, FishingFrames)); states.Add(key); }
            rig.Skin.Clips = clips.ToArray(); rig.Skin.MeshStates = states.ToArray();
            var sheet = new Sprite[PlayerFishingAnimator.Directions * FishingFrames];
            for (int i = 0; i < sheet.Length; i++) sheet[i] = rig.BoardCell;
            animator = rig.Body.gameObject.AddComponent<PlayerFishingAnimator>();
            animator.Configure(sheet, sheet, sheet, sheet, ClipFps, ClipFps, ClipFps, ClipFps);
            animator.ConfigureCastAndHold(sheet, sheet, sheet, ClipFps, ClipFps);
            return rig;
        }

        static FishingState FishingSnapshot(FishingPhase phase, float charge = 0.5f) =>
            new FishingState(phase, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f,
                depth01: 0f, slackWindowOpen: false, rodBend01: 0f, fishOffsetX: 1f, fishOffsetY: 0f,
                castCharge01: charge, castAimX: 1f, castAimY: 0f, rigDepthM: 0f);

        static void AssertFishingMesh(Rig rig, PlayerFishingAnimator animator, string expected, bool aboard)
        {
            AssertDrawnBy(rig, aboard ? AboardMesh : AshoreMesh, expected);
            var figure = aboard ? rig.Presenter.Figure : rig.Presenter.AshoreFigure;
            Assert.AreEqual(expected, figure.DrawnStateKey);
            Assert.IsFalse(figure.LastFrameWasFenced);
            Assert.AreEqual(animator.Frame, figure.DrawnFrame, "mesh and sprite use the same action clock");
            Assert.IsFalse(aboard ? rig.RiderSr.enabled : !rig.Body.forceRenderingOff, "the sprite body is off");
        }

        internal IEnumerator RunFishingMeshSequence()
        {
            Rig rig = NewFishingRig(out var animator);
            foreach (bool aboard in new[] { false, true })
            {
                foreach (var beat in new[]
                {
                    (FishingPhase.WindBack, "castBack"), (FishingPhase.Cast, "castRelease"),
                    (FishingPhase.Sinking, "hold"), (FishingPhase.Waiting, "hold"),
                    (FishingPhase.BiteNibble, "bite"), (FishingPhase.Bite, "bite"),
                    (FishingPhase.FightDeep, "strike"), (FishingPhase.FightSurface, "reel"),
                    (FishingPhase.Landed, "land"),
                    (FishingPhase.Fighting, "strike"), (FishingPhase.Fighting, "reel"),
                    (FishingPhase.Landed, "land"),
                })
                {
                    EventBus.Publish(new FishingStateChanged(FishingSnapshot(beat.Item1)));
                    if (beat.Item2 == "reel")
                    {
                        // Let the real Update clock leave strike, bounded so a broken transition fails.
                        for (int i = 0; i < 120 && animator.Pose != FishingPose.Reel; i++)
                        {
                            PoseMove(rig, aboard);
                            AssertFishingMesh(rig, animator, "strike", aboard);
                            yield return new WaitForSecondsRealtime(1f / ClipFps);
                        }
                        Assert.AreEqual(FishingPose.Reel, animator.Pose);
                    }
                    PoseMove(rig, aboard);
                    AssertFishingMesh(rig, animator, beat.Item2, aboard);
                    yield return null;
                    PoseMove(rig, aboard);
                    AssertFishingMesh(rig, animator, beat.Item2, aboard);

                    // Off is precisely the old performance, down to its current sheet cell and claim.
                    Sprite cell = rig.Body.sprite; int frame = animator.Frame;
                    rig.Config.MeshFishingAndDig = false; PoseMove(rig, aboard);
                    AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "switch off " + beat.Item1);
                    Assert.AreSame(cell, rig.Body.sprite);
                    Assert.AreEqual(frame, animator.Frame);
                    Assert.IsTrue(animator.OwnsRenderer); Assert.IsTrue(rig.Character.IsSuspended);
                    rig.Config.MeshFishingAndDig = true; PoseMove(rig, aboard);
                    AssertFishingMesh(rig, animator, beat.Item2, aboard);
                }
                EventBus.Publish(new FishingStateChanged(FishingSnapshot(FishingPhase.Idle)));
                PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? AboardMesh : AshoreMesh, "normal gait after landing");
                Assert.IsFalse(rig.Character.IsSuspended);
            }
        }

        internal IEnumerator RunFishingMeshFallbacks()
        {
            Rig rig = NewFishingRig(out var animator);
            EventBus.Publish(new FishingStateChanged(FishingSnapshot(FishingPhase.Bite)));
            foreach (bool aboard in new[] { false, true })
            {
                PoseMove(rig, aboard); AssertFishingMesh(rig, animator, "bite", aboard);
                rig.Character.Suspend(); PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "nested claimant");
                rig.Character.Release(); PoseMove(rig, aboard); AssertFishingMesh(rig, animator, "bite", aboard);
                string[] states = rig.Skin.MeshStates;
                rig.Skin.MeshStates = new[] { "idle", "walk" }; PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "clip not adopted");
                rig.Skin.MeshStates = states;
                var clips = rig.Skin.Clips;
                rig.Skin.Clips = Array.FindAll(clips, c => c.StateKey != "bite"); PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "missing clip");
                rig.Skin.Clips = clips;
                Mesh bind = rig.Skin.BindMesh; rig.Skin.BindMesh = null; PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "unusable skin");
                rig.Skin.BindMesh = bind; PoseMove(rig, aboard); AssertFishingMesh(rig, animator, "bite", aboard);
                rig.Character.Visual.Skin = null; PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "missing skin");
                rig.Character.Visual.Skin = rig.Skin;
                if (!aboard)
                {
                    rig.Config.MeshCharacterAshore = false; PoseMove(rig, false);
                    AssertDrawnBy(rig, BodySprite, "ashore switch still applies");
                    rig.Config.MeshCharacterAshore = true;
                }
                rig.Config.MeshCharacter = false; PoseMove(rig, aboard);
                AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "global switch still applies");
                rig.Config.MeshCharacter = true;
                yield return new WaitForSecondsRealtime(1f / ClipFps);
            }
        }

        internal IEnumerator RunDigMeshSequence()
        {
            Rig rig = NewFishingRig(out var animator);
            PoseMove(rig, false);
            rig.Config.MeshFishingAndDig = false;
            EventBus.Publish(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 1f));
            PoseMove(rig, false);
            Assert.IsFalse(animator.HasBodyPerformance, "off keeps today's unanimated dig");
            AssertDrawnBy(rig, AshoreMesh, "off keeps the normal idle mesh");
            Assert.AreEqual("idle", rig.Presenter.AshoreFigure.DrawnStateKey);
            rig.Config.MeshFishingAndDig = true;
            EventBus.Publish(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 1f));
            Assert.IsTrue(animator.HasBodyPerformance);
            PoseMove(rig, false);
            AssertDrawnBy(rig, AshoreMesh, "dig starts");
            Assert.AreEqual("dig", rig.Presenter.AshoreFigure.DrawnStateKey);
            string[] states = rig.Skin.MeshStates;
            rig.Skin.MeshStates = new[] { "idle", "walk" }; PoseMove(rig, false);
            AssertDrawnBy(rig, BodySprite, "missing dig keeps a visible body");
            rig.Skin.MeshStates = states;
            rig.Character.Suspend(); PoseMove(rig, false);
            AssertDrawnBy(rig, BodySprite, "dig yields to another owner"); rig.Character.Release();
            int drawnFrames = 0;
            while (animator.HasBodyPerformance && drawnFrames < 240)
            {
                PoseMove(rig, false);
                AssertDrawnBy(rig, AshoreMesh, "through the entire dig");
                Assert.AreEqual("dig", rig.Presenter.AshoreFigure.DrawnStateKey);
                Assert.IsFalse(rig.Presenter.AshoreFigure.LastFrameWasFenced);
                drawnFrames++;
                yield return new WaitForSecondsRealtime(1f / ClipFps);
            }
            Assert.IsFalse(animator.HasBodyPerformance, "dig releases at the authored timing's end");
            Assert.Greater(drawnFrames, 0);
            PoseMove(rig, false);
            AssertDrawnBy(rig, AshoreMesh, "dig finished");
            Assert.AreEqual("idle", rig.Presenter.AshoreFigure.DrawnStateKey);
        }
    }
}
