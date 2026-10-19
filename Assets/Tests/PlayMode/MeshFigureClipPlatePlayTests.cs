using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class MeshFigureClipPlatePlayTests
    {
        AshoreFigurePlatePlayTests _plate;

        [UnityTest]
        public IEnumerator VaultAndLowWaterLadder_BothWays()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — the move plate needs a local graphics device; " +
                              "CI verifies its drawer/frame contracts in MeshFigureClipPlayTests.");
            Assert.AreNotEqual(SaveStore.DefaultPath, SaveStore.ActivePath, "the test run owns its save");
            _plate = new AshoreFigurePlatePlayTests();
            _plate.WriteTheFloor();
            yield return _plate.SetUpRegion();
            yield return _plate.RunMeshMovePlate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_plate != null) yield return _plate.TearDownRegion();
        }
    }

    public partial class AshoreFigurePlatePlayTests
    {
        sealed class MovePlateSea : IEnvironmentService
        {
            public IEnvironmentService Source;
            public float Water;
            public int WorldSeed => Source.WorldSeed;
            public TideProfile ActiveTideProfile { get => Source.ActiveTideProfile; set => Source.ActiveTideProfile = value; }
            public EnvironmentSample Sample() => Source.Sample();
            public float TideHeightAt(double t) => Water;
            public float WaterLevelAt(double t) => Water;
        }

        internal IEnumerator RunMeshMovePlate()
        {
            yield return Arrive(StPetersScene, Noon);
            Vector2 head = StPetersWharf.LadderPosition();
            Vector2 shore = new Vector2(head.x, StPetersWharf.MooringFaceY + 0.6f);
            yield return StandHer(shore, head + Vector2.up, 10f,
                "the actual St Peters ladder and the hull alongside", plateFigure: false);
            yield return FindHerPresenter();
            var controls = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.IsNotNull(controls);
            var boat = (BoatController)typeof(ControlSwitcher)
                .GetField("_boatController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controls);
            Assert.IsNotNull(boat); Assert.IsNotNull(boat.Hull);
            boat.gameObject.SetActive(true);
            float halfBeam = boat.Hull.Visual != null && boat.Hull.Visual.HullMesh != null
                ? boat.Hull.Visual.HullMesh.WatertightHalfBeamMeters : FallbackHalfBeamMetres;
            Vector2 boatAt = new Vector2(head.x,
                StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideFenderGapMetres - halfBeam);
            boat.transform.position = boatAt;
            boat.GetComponent<Rigidbody2D>().position = boatAt;
            boat.GetComponent<Rigidbody2D>().simulated = false;
            var landing = _stage.Track(new GameObject("MeshMoveLanding"));
            landing.transform.position = shore;
            controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
            controls.ConfigureBoardingMove(true, 3f, 0.8f, 0.8f, 0.35f);
            controls.enabled = false; // step the actual transition in seconds while the camera is frozen
            var player = _player.GetComponent<CharacterClipPlayer>();
            Assert.IsNotNull(player);
            Assert.AreEqual(1, _player.GetComponents<CharacterClipPlayer>().Length);
            var sea = new MovePlateSea { Source = GameServices.Environment };
            GameServices.Environment = sea;
            _switchOn.MeshCharacter = true; _switchOn.MeshCharacterAshore = true;
            GameServices.Config = _switchOn;
            var begin = typeof(ControlSwitcher).GetMethod("BeginBoardingMove", BindingFlags.Instance | BindingFlags.NonPublic);
            Type kind = begin.GetParameters()[0].ParameterType;
            Assert.IsTrue(BoardingLadders.TryFindNearestNow(head, 3f, out IBoardingLadder ladder));
            float deckHeight = _player.GetComponent<DeckWalkController>().DeckHeightMeters;
            float levelAtRail = ladder.TopElevationMeters - deckHeight + boat.Hull.DraughtMeters;
            foreach (bool low in new[] { false, true })
            {
                sea.Water = low ? levelAtRail - 2f : levelAtRail;
                controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
                PutHerAt(shore, withBody: true);
                foreach (string direction in new[] { "Boarding", "Disembarking" })
                {
                    Assert.IsTrue((bool)begin.Invoke(controls, new[] { Enum.Parse(kind, direction) }));
                    Assert.AreEqual(low, controls.IsLadderBoarding, "the tide must choose the route pictured");
                    int shot = 0; float elapsed = 0f;
                    while (controls.IsBoardingMove && elapsed < 40f)
                    {
                        controls.TickBoardingMove(0.075f); player.Advance(0.075f); elapsed += 0.075f;
                        if (player.IsPlaying)
                        {
                            _rider.SetMode(ControlMode.OnFoot, null);
                            ShootMove(player, $"m1-{(low ? "ladder" : "vault")}-{direction.ToLowerInvariant()}-" +
                                $"{(low ? "low-water" : "rail-level-water")}-f{shot++:000}");
                        }
                    }
                    Assert.IsFalse(controls.IsBoardingMove, "the move finished within bounded seconds");
                    Assert.Greater(shot, 0, "a sequence of the move was captured");
                    Debug.Log($"[mesh-move-plate] {direction}, {(low ? "low water" : "rail-level water")}, " +
                        $"water {sea.Water:0.000} m, {shot} frames; actual ControlSwitcher transition entry and tick");
                }
            }
            // D8 compares the old sprite cells with the mesh chop; the tool key alone proves no knife.
            controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
            PutHerAt(shore, true);
            Assert.IsTrue(player.PlayDriven(CharacterClip.Chop, 180f));
            for (int f = 0; f < _iso.Visual.ClipFrameCount(CharacterClip.Chop); f++)
            {
                player.SeekFrame(f); _rider.SetMode(ControlMode.OnFoot, null);
                ShootMove(player, $"m1-chop-low-water-f{f:00}");
                _presenter.enabled = false;
                _rider.SetMode(ControlMode.OnFoot, null); _sprite.forceRenderingOff = false;
                _stage.SavePlate($"m1-chop-sprite-control-f{f:00}.png", _stage.Capture());
                _presenter.enabled = true;
            }
            player.Stop();
            GameServices.Environment = sea.Source;
            yield return null;
        }

        void ShootMove(CharacterClipPlayer clip, string name)
        {
            AssertTheSubjectIsInFrame(_player.transform, name);
            foreach (bool pixels in new[] { false, true })
            {
                _switchOn.FigurePixelTarget = pixels;
                _rider.SetMode(ControlMode.OnFoot, null);
                Assert.IsTrue(_presenter.DrawsAshore, _presenter.NotDrawingReason);
                var figure = _presenter.AshoreFigure;
                Assert.AreEqual(clip.Frame, figure.RequestedFrame);
                Assert.AreEqual(clip.Frame, figure.DrawnFrame);
                byte[] subject = _stage.Capture();
                figure.Visible = false;
                byte[] absent = _stage.Capture();
                Assert.Greater(WharfNightStage.Footprint(subject, absent), 5,
                    "a visible/in-frame actor can still be painted over; the removed control must differ");
                figure.Visible = true;
                _stage.SavePlate(name + (pixels ? "-on.png" : "-off.png"), subject);
            }
        }
    }
}
