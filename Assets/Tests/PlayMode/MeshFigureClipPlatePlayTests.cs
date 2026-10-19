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
        Vector3 _movePlateCameraPosition;
        sealed class MovePlateSea : IEnvironmentService
        {
            public IEnvironmentService Source;
            public float Water;
            public int WorldSeed => Source.WorldSeed;
            public TideProfile ActiveTideProfile { get => Source.ActiveTideProfile; set => Source.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var s = Source.Sample();
                return new EnvironmentSample(s.WindVector, s.CurrentVector, Water,
                    s.SeaState, s.Visibility, s.SeaState01);
            }
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
            boat.transform.rotation = Quaternion.Euler(0f, 0f, -StPetersBuilder.DoryMooredHeadingDegrees);
            boat.GetComponent<Rigidbody2D>().position = boatAt;
            boat.GetComponent<Rigidbody2D>().rotation = -StPetersBuilder.DoryMooredHeadingDegrees;
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
            Vector2 beachBoat;
            Vector2 beach = FindMoveBeach(head, boat.Hull.DraughtMeters, out beachBoat);
            foreach (bool low in new[] { false, true })
            {
                // This high wharf is a ladder berth even at ordinary high water. The vault is shot
                // on an actual dry beach at mean water, not by flooding the island up to its planks.
                sea.Water = low ? StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude : StPetersBuilder.TideMean;
                Vector2 from = low ? shore : beach;
                Vector2 mooring = low ? boatAt : beachBoat;
                boat.transform.position = mooring; boat.GetComponent<Rigidbody2D>().position = mooring;
                landing.transform.position = from;
                controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
                PutHerAt(from, withBody: true);
                yield return _stage.FrameOn((from + mooring) * 0.5f + Vector2.up * (low ? -1f : 0.8f));
                FitTheFrame(low ? 20f : 10f, low ? "the full low-water climb" : "the beach vault");
                _plateOrtho = _stage.Camera.orthographicSize;
                _movePlateCameraPosition = _stage.Camera.transform.position;
                foreach (string direction in new[] { "Boarding", "Disembarking" })
                {
                    Assert.IsTrue((bool)begin.Invoke(controls, new[] { Enum.Parse(kind, direction) }));
                    Assert.AreEqual(low, controls.IsLadderBoarding, "the tide must choose the route pictured");
                    int shot = 0; float elapsed = 0f;
                    while (controls.IsBoardingMove && elapsed < 40f)
                    {
                        controls.TickBoardingMove(0.075f); player.Advance(0.075f); elapsed += 0.075f;
                        yield return null; // publish this position's Y-sort and drawn water before the shutters
                        if (player.IsPlaying)
                        {
                            _rider.SetMode(ControlMode.OnFoot, null);
                            ShootMove(player, $"m1-{(low ? "ladder" : "vault")}-{direction.ToLowerInvariant()}-" +
                                $"{(low ? "low-water-wharf" : "mean-water-beach")}-f{shot++:000}");
                        }
                    }
                    Assert.IsFalse(controls.IsBoardingMove, "the move finished within bounded seconds");
                    Assert.Greater(shot, 0, "a sequence of the move was captured");
                    Debug.Log($"[mesh-move-plate] {direction}, {(low ? "low water wharf" : "mean water beach")}, " +
                        $"water {sea.Water:0.000} m, shore {from}, boat {mooring}, {shot} frames; " +
                        "actual ControlSwitcher transition entry and tick");
                }
            }
            // D8 compares the old sprite cells with the mesh chop; the tool key alone proves no knife.
            sea.Water = StPetersBuilder.TideMean;
            landing.transform.position = beach;
            controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
            PutHerAt(beach, true);
            yield return _stage.FrameOn(beach + Vector2.up * 0.8f);
            FitTheFrame(8f, "the Chop beat and its sprite-cell control");
            _plateOrtho = _stage.Camera.orthographicSize;
            _movePlateCameraPosition = _stage.Camera.transform.position;
            Assert.IsTrue(player.PlayDriven(CharacterClip.Chop, 180f));
            for (int f = 0; f < _iso.Visual.ClipFrameCount(CharacterClip.Chop); f++)
            {
                player.SeekFrame(f); _rider.SetMode(ControlMode.OnFoot, null);
                ShootMove(player, $"m1-chop-mean-water-beach-f{f:00}");
                var figure = _presenter.AshoreFigure;
                figure.Visible = false;
                _sprite.forceRenderingOff = false;
                _stage.SavePlate($"m1-chop-sprite-control-f{f:00}.png", _stage.Capture());
                _sprite.forceRenderingOff = true;
                figure.Visible = true;
            }
            player.Stop();
            GameServices.Environment = sea.Source;
            yield return null;
        }

        void ShootMove(CharacterClipPlayer clip, string name)
        {
            _stage.Camera.transform.position = _movePlateCameraPosition;
            _stage.Camera.orthographicSize = _plateOrtho;
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

        static Vector2 FindMoveBeach(Vector2 near, float draught, out Vector2 boatAt)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(terrain, "the vault must stand on the scene's real exposed terrain");
            float water = StPetersBuilder.TideMean;
            float best = float.PositiveInfinity;
            Vector2 shore = default, mooring = default;
            foreach (Vector2 direction in new[] { Vector2.down, Vector2.right, Vector2.left, Vector2.up })
                for (int y = -60; y <= 60; y++)
                    for (int x = -60; x <= 60; x++)
                    {
                        Vector2 p = near + new Vector2(x, y);
                        float elevation = terrain.ElevationAt(p);
                        if (elevation < water || elevation > water + 0.6f) continue;
                        if (BoardingLadders.TryFindNearestNow(p, 8f, out _)) continue;
                        Vector2 b = p + direction * 2f;
                        if (terrain.ElevationAt(b) > water - draught) continue;
                        float distance = (p - near).sqrMagnitude;
                        if (distance >= best) continue;
                        best = distance; shore = p; mooring = b;
                    }
            Assert.IsFalse(float.IsPositiveInfinity(best), "no nearby dry beach with water deep enough for this hull");
            boatAt = mooring;
            return shore;
        }
    }
}
