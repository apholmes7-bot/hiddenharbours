using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Reuse the existing isolated mesh/scene harness without inheriting and duplicating its tests.
    public sealed class MeshFigureClipPlayTests
    {
        DeckRiderMeshPresenterPlayTests _rig;
        [SetUp] public void SetUp() { _rig = new DeckRiderMeshPresenterPlayTests(); _rig.SetUp(); }
        [TearDown] public void TearDown() => _rig.TearDown();
        [UnityTest] public IEnumerator Vault_DrawsTheMesh_EveryFrame() =>
            _rig.RunMeshMoves(new[] { CharacterClip.Board, CharacterClip.BoardDown }, carrying: true);
        [UnityTest] public IEnumerator Ladder_MeshFrame_FollowsTheRungs_BothWays() =>
            _rig.RunRealLadder();
        [UnityTest] public IEnumerator Haul_DrawsTheMesh() =>
            _rig.RunMeshMoves(new[] { CharacterClip.Haul }, carrying: false);
        [UnityTest] public IEnumerator SternDeckWork_DrawsTheMesh_SeekedByTheLine() => _rig.RunSternMoves();
    }

    public partial class DeckRiderMeshPresenterPlayTests
    {
        sealed class MoveHands : ICarrier, ICarriable
        {
            public ICarriable Carried => this;
            public bool IsCarrying => true;
            public string DefId => "test.move_load";
            public bool IsCarriable => true;
            public Transform Transform { get; set; }
            public int BakedFacings => 0;
            public void ShowFacing(int i) { }
            public void RideSortingBand(int l, int o) { }
            public void OnLifted(ICarrier c) { }
            public void OnPlaced() { }
        }

        static readonly CharacterClip[] MappedMoves =
        {
            CharacterClip.Board, CharacterClip.BoardDown, CharacterClip.LadderDown, CharacterClip.Haul,
            CharacterClip.Hauler, CharacterClip.Bench, CharacterClip.Chop,
            CharacterClip.Lift, CharacterClip.Place, CharacterClip.Toss,
        };

        Rig NewMoveRig(bool pixels)
        {
            Rig rig = NewAshoreRig();
            rig.Config.FigurePixelTarget = pixels;
            var clips = new List<CharacterSkinDef.SkinClip>(rig.Skin.Clips);
            var states = new List<string>(rig.Skin.MeshStates);
            foreach (var move in MappedMoves)
            {
                var sheet = new CharacterClipSheets
                {
                    Sheet = new Sprite[rig.Character.Visual.FacingCount * 4],
                    FrameCount = 4, FramesPerSecond = 10f,
                    Loops = move == CharacterClip.LadderDown || move == CharacterClip.Haul ||
                            move == CharacterClip.Hauler || move == CharacterClip.Bench || move == CharacterClip.Chop,
                };
                for (int i = 0; i < sheet.Sheet.Length; i++) sheet.Sheet[i] = rig.BoardCell;
                typeof(CharacterVisualDef).GetField(move + "Clip").SetValue(rig.Character.Visual, sheet);
                foreach (string carry in move == CharacterClip.Board || move == CharacterClip.BoardDown
                    ? new[] { null, "buckets", "tray", "pot" } : new string[] { null })
                {
                    string key = CharacterClipStateMap.StateKeyFor(move, carry);
                    clips.Add(Clip(key, 4)); states.Add(key);
                }
            }
            rig.Skin.Clips = clips.ToArray();
            rig.Skin.MeshStates = states.ToArray();
            return rig;
        }

        static void PoseMove(Rig rig, bool aboard)
            => rig.Rider.SetMode(aboard ? ControlMode.OnDeck : ControlMode.OnFoot,
                                 aboard ? rig.BoatRoot.transform : null);

        static void AssertMove(Rig rig, CharacterClipPlayer player, string key, bool aboard)
        {
            AssertDrawnBy(rig, aboard ? AboardMesh : AshoreMesh, key);
            var figure = aboard ? rig.Presenter.Figure : rig.Presenter.AshoreFigure;
            Assert.AreEqual(key, figure.DrawnStateKey);
            Assert.AreEqual(player.Frame, figure.RequestedFrame);
            Assert.AreEqual(player.Frame, figure.DrawnFrame, "no hidden integrity-frame substitution");
            Assert.IsFalse(figure.LastFrameWasFenced);
            float yaw = aboard ? rig.Presenter.FigureYawDegrees : rig.Presenter.AshoreYawDegrees;
            float expected = rig.Skin.AzimuthCounterClockwise ? -player.HeadingDegrees : player.HeadingDegrees;
            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, yaw), 0.001f, "clip heading, not stale gait heading");
        }

        internal IEnumerator RunMeshMoves(CharacterClip[] moves, bool carrying, bool bothWays = false)
        {
            foreach (bool pixels in new[] { false, true })
            {
                Rig rig = NewMoveRig(pixels);
                var player = rig.Body.gameObject.AddComponent<CharacterClipPlayer>();
                var table = ScriptableObject.CreateInstance<CharacterCarryPoseDef>(); _spawned.Add(table);
                PoseMove(rig, false);
                typeof(DeckRiderMeshPresenter).GetField("_carryPoses", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rig.Presenter, table);
                typeof(DeckRiderMeshPresenter).GetField("_carryPosesLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rig.Presenter, true);
                foreach (bool aboard in new[] { false, true })
                    foreach (CharacterClip move in moves)
                        foreach (string carry in carrying ? new[] { null, "buckets", "tray", "pot" } : new string[] { null })
                        {
                            table.Rows = carry == null ? Array.Empty<CharacterCarryPoseDef.Row>() : new[]
                            { new CharacterCarryPoseDef.Row { CarriableDefId = "test.move_load", Carry = carry } };
                            GameServices.Hands = carry == null ? null : new MoveHands { Transform = rig.Body.transform };
                            string key = CharacterClipStateMap.StateKeyFor(move, carry);
                            Assert.IsTrue(player.PlayDriven(move, 213.25f, 0f));
                            for (int pass = 0; pass < (bothWays ? 2 : 1); pass++)
                                for (int f = 0; f < 4; f++)
                                {
                                    player.SetHeading(pass == 0 ? 213.25f : 33.25f);
                                    player.SeekFrame(f); // the caller's rung/quantity phase, unchanged in ascent
                                    PoseMove(rig, aboard);
                                    AssertMove(rig, player, key, aboard);
                                    player.Advance(20f);
                                    Assert.AreEqual(f, player.Frame, "a held line/rung phase must not run on the clock");
                                    yield return null;
                                    AssertMove(rig, player, key, aboard);
                                }
                            // A nested claimant must still receive the whole sprite.
                            rig.Character.Suspend(); PoseMove(rig, aboard);
                            AssertDrawnBy(rig, aboard ? RiderSprite : BodySprite, "another claimant");
                            rig.Character.Release(); PoseMove(rig, aboard);
                            AssertMove(rig, player, key, aboard);
                            player.Stop(); GameServices.Hands = null; PoseMove(rig, aboard);
                            AssertDrawnBy(rig, aboard ? AboardMesh : AshoreMesh, "the move ended");
                        }
                Object.Destroy(rig.Body.gameObject); Object.Destroy(rig.BoatRoot);
                yield return null;
            }
        }

        internal IEnumerator RunSternMoves()
        {
            foreach (bool pixels in new[] { false, true })
            {
                Rig rig = NewMoveRig(pixels);
                var player = rig.Body.gameObject.AddComponent<CharacterClipPlayer>();
                var kit = ScriptableObject.CreateInstance<DeckGearKitDef>(); _spawned.Add(kit);
                var gear = ScriptableObject.CreateInstance<BoatDeckGearDef>(); _spawned.Add(gear);
                var kinds = new[] { DeckGearKind.HaulerStation, DeckGearKind.BandingTable,
                                    DeckGearKind.ChopBoard, DeckGearKind.BaitBin };
                var entries = new List<DeckGearKitEntry>(); var mounts = new List<BoatDeckGearMount>();
                foreach (var kind in kinds)
                {
                    entries.Add(new DeckGearKitEntry { Kind = kind, StandLocalMeters = Vector3.zero,
                        WorkZMeters = 0.8f, ClearMeters = Vector2.one });
                    mounts.Add(new BoatDeckGearMount { Kind = kind, MountLocalMeters = Vector3.zero });
                }
                kit.Entries = entries.ToArray(); gear.Mounts = mounts.ToArray();
                var deck = rig.BoatRoot.AddComponent<SternDeckGearPresenter>();
                deck.Configure(gear, kit, player);
                PoseMove(rig, true);
                foreach (var stage in new[] { SternDeckStage.Hauling, SternDeckStage.Aboard, SternDeckStage.Emptying,
                                              SternDeckStage.Baiting, SternDeckStage.Stacking, SternDeckStage.Setting })
                    foreach (float quantity in new[] { 0.08f, 0.36f, 0.67f })
                    {
                        deck.OnDeckLoopChanged(new DeckLoopStateChanged(new DeckLoopState(stage, quantity, 0, false)));
                        yield return null;
                        Assert.IsTrue(player.IsPlaying, stage.ToString());
                        AssertMove(rig, player, CharacterClipStateMap.StateKeyFor(player.Clip), true);
                    }
                Object.Destroy(rig.BoatRoot); Object.Destroy(rig.Body.gameObject);
                yield return null;
            }
        }

        internal IEnumerator RunRealLadder()
        {
            foreach (bool pixels in new[] { false, true })
            {
                Rig rig = NewMoveRig(pixels);
                var player = rig.Body.gameObject.AddComponent<CharacterClipPlayer>();
                var walk = rig.Body.gameObject.AddComponent<PlayerWalkController>();
                rig.Body.gameObject.AddComponent<DeckWalkController>();
                var boat = rig.BoatRoot.AddComponent<BoatController>();
                var hull = ScriptableObject.CreateInstance<BoatHullDef>(); _spawned.Add(hull);
                hull.Id = "boat.clip_ladder_test"; hull.CameraWorldHeightMeters = 14f;
                boat.SetHull(hull);
                boat.enabled = false; boat.GetComponent<Rigidbody2D>().simulated = false;
                rig.Body.transform.position = new Vector3(2.6f, 0f, 0f);
                var ladderGo = new GameObject("ClipLadder"); _spawned.Add(ladderGo);
                ladderGo.transform.position = rig.Body.transform.position;
                var ladder = ladderGo.AddComponent<WharfLadder>();
                ladder.Configure("wharf.clip_test", 3f, faceHeadingDegrees: 180f);
                var dock = new GameObject("ClipDock"); _spawned.Add(dock);
                dock.transform.position = new Vector3(0f, 2f, 0f);
                var landing = new GameObject("ClipLanding"); _spawned.Add(landing);
                landing.transform.position = new Vector3(2.6f, 0f, 0f);
                var host = new GameObject("ClipSwitcher"); _spawned.Add(host);
                var control = host.AddComponent<ControlSwitcher>();
                control.Configure(walk, boat, null, dock.transform, 4f, landing.transform);
                control.ConfigureBoardingMove(true, 3f, 0.8f, 0.5f, 0.35f);
                control.enabled = false; // drive its public seconds seam, never a frame-count clock
                var begin = typeof(ControlSwitcher).GetMethod("BeginBoardingMove", BindingFlags.Instance | BindingFlags.NonPublic);
                Type kind = begin.GetParameters()[0].ParameterType;
                foreach (string direction in new[] { "Boarding", "Disembarking" })
                {
                    Assert.IsTrue((bool)begin.Invoke(control, new[] { Enum.Parse(kind, direction) }));
                    Assert.IsTrue(control.IsLadderBoarding, "the actual route must be the low-water ladder");
                    bool sawRungs = false;
                    float elapsed = 0f;
                    while (control.IsBoardingMove && elapsed < 30f)
                    {
                        control.TickBoardingMove(0.055f); player.Advance(0.055f); elapsed += 0.055f;
                        if (player.IsPlaying)
                        {
                            PoseMove(rig, false);
                            AssertMove(rig, player, CharacterClipStateMap.StateKeyFor(player.Clip), false);
                            sawRungs |= player.Clip == CharacterClip.LadderDown;
                        }
                        yield return null;
                    }
                    Assert.IsFalse(control.IsBoardingMove, "bounded seconds reached the real move's end");
                    Assert.IsTrue(sawRungs, direction + " visited the rungs, not just the rail ends");
                }
                Object.Destroy(host); Object.Destroy(ladderGo); Object.Destroy(dock); Object.Destroy(landing);
                Object.Destroy(rig.BoatRoot); Object.Destroy(rig.Body.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AMappedClip_KeepsTheMeshForItsLength_AndReturnsToGaitWhenItEnds()
        {
            Rig rig = NewMoveRig(true);
            var player = rig.Body.gameObject.AddComponent<CharacterClipPlayer>();
            Assert.IsTrue(player.Play(CharacterClip.Board, 47.5f, 0.4f, holdOnFinish: true));
            for (int f = 0; f < 4; f++)
            {
                PoseMove(rig, false);
                AssertMove(rig, player, "board", false);
                player.Advance(0.1f);
            }
            Assert.IsTrue(player.IsPlaying, "the held endpoint remains a move");
            player.Stop(); PoseMove(rig, false);
            AssertDrawnBy(rig, AshoreMesh, "gait takes the same figure back");
            Assert.AreNotEqual("board", rig.Presenter.AshoreFigure.DrawnStateKey);
            rig.Skin.MeshStates = new[] { "idle", "walk", "balance" };
            LogAssert.Expect(LogType.Warning,
                "[DeckRiderMeshPresenter] Skin 'character.playtest_fisher' cannot draw move 'board' " +
                "as mesh; that move keeps its sprite (ADR 0041).");
            Assert.IsTrue(player.PlayDriven(CharacterClip.Board, 47.5f));
            PoseMove(rig, false);
            AssertDrawnBy(rig, BodySprite, "a disabled skin state keeps the sprite");
            PoseMove(rig, false); // the same missing move must not log a second warning
            AssertDrawnBy(rig, BodySprite, "the next refusal still keeps one drawer");
            player.Stop();
            yield return null;
        }
    }
}
