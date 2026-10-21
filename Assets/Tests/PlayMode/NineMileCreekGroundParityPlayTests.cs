#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>NMC H2: the ground the game samples IS G3.</b> The real NineMileCreek scene, loaded the way the game loads
    /// it: its PaintedTidalTerrain registers itself into <see cref="GameServices.TidalTerrain"/> and decodes the map
    /// asset's IMPORTED height texture. That runtime field must equal G3 decoded straight from its committed R16 file
    /// (<see cref="NineMileCreekGround.Load"/>) within one R16 code, at the places the loop stands on and on a grid
    /// across the whole map. Two decoders, one ground: an import that rounds the height, a map pointing at another
    /// texture, or a terrain that never registers reds here.
    /// </summary>
    public class NineMileCreekGroundParityPlayTests
    {
        Scene _loaded;
        bool _oldIgnore;

        static IEnumerable<(string name, Vector2 at)> NamedPoints()
        {
            yield return ("the step ashore", NineMileCreekGroundPlan.DisembarkPos);
            yield return ("the berth", NineMileCreekGroundPlan.DockZonePos);
            yield return ("the arrival park", NineMileCreekGroundPlan.ArrivalPos);
            yield return ("the berth ladder", NineMileCreekGroundPlan.BerthLadderPos);
            yield return ("the west cleat", NineMileCreekGroundPlan.WestCleat);
            yield return ("the wharf ladder", NineMileCreekGroundPlan.WharfLadderPos);
            yield return ("the gangway's shore end", NineMileCreekGroundPlan.GangwayShoreEnd);
            yield return ("the gangway's float end", NineMileCreekGroundPlan.GangwayFloatEnd);
            yield return ("the Otter's park", NineMileCreekGroundPlan.OtterParkPos);
            yield return ("the turning pad", NineMileCreekGroundPlan.TurningPadCentre);
            yield return ("the derelict dory", NineMileCreekMainland.DerelictDoryPos);
            var deck = NineMileCreekGroundPlan.BridgeDeck;
            yield return ("the bridge deck's centre", deck.center);
            yield return ("the bridge's south bank", new Vector2(deck.center.x, deck.yMin));
            yield return ("the bridge's north bank", new Vector2(deck.center.x, deck.yMax));
            var route91 = NineMileCreekGroundPlan.Route91;
            yield return ("Route 91's first point", route91[0]);
            yield return ("Route 91's last point", route91[route91.Length - 1]);
            var wharfRoad = NineMileCreekGroundPlan.WharfRoad;
            yield return ("Wharf Road's first point", wharfRoad[0]);
            yield return ("Wharf Road's last point", wharfRoad[wharfRoad.Length - 1]);
            yield return ("the map's centre", Vector2.zero);
        }

        [UnitySetUp]
        public IEnumerator LoadCreek()
        {
            _oldIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true; // existing decor-import errors, as other NMC fixtures
            yield return SceneManager.LoadSceneAsync("NineMileCreek", LoadSceneMode.Single);
            _loaded = SceneManager.GetSceneByName("NineMileCreek");
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadCreek()
        {
            LogAssert.ignoreFailingMessages = _oldIgnore;
            var empty = SceneManager.CreateScene("GroundParityCleanup");
            SceneManager.SetActiveScene(empty);
            if (_loaded.IsValid() && _loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(_loaded);
        }

        [UnityTest]
        public IEnumerator TheGroundTheGameSamples_IsG3DecodedFromItsFile_AtTheNamedPointsAndAcrossTheMap()
        {
            var painted = GameServices.TidalTerrain as PaintedTidalTerrain;
            Assert.IsNotNull(painted, "the game samples " + (GameServices.TidalTerrain?.GetType().Name ?? "nothing") +
                                      ", not a PaintedTidalTerrain");
            Assert.That(painted.gameObject.scene == _loaded, Is.True, "the registered terrain is not the creek's own");
            Assert.That(GameServices.StillWater, Is.SameAs(painted.Map.StillWater),
                "the still water registered is not the creek map's own");
            TestContext.WriteLine($"terrain '{painted.name}', still water {(painted.Map.StillWater != null ? "registered" : "none")}");

            var g3 = NineMileCreekGround.Load();
            float code = NineMileCreekGround.CodeMetres;
            foreach (var (name, at) in NamedPoints())
            {
                float game = GameServices.TidalTerrain.ElevationAt(at), file = g3.ElevationAt(at);
                TestContext.WriteLine($"{name} ({at.x:R}, {at.y:R}): game {game:R}, G3 {file:R}, diff {game - file:R}");
                Assert.That(game, Is.EqualTo(file).Within(code), $"{name} at ({at.x}, {at.y})");
            }

            int points = 0, off = 0;
            float worst = 0f;
            var worstAt = Vector2.zero;
            for (float x = -376f; x <= 376f; x += 8f)
                for (float y = -276f; y <= 276f; y += 8f)
                {
                    var at = new Vector2(x, y);
                    float diff = Mathf.Abs(GameServices.TidalTerrain.ElevationAt(at) - g3.ElevationAt(at));
                    points++;
                    if (diff > code) off++;
                    if (diff > worst) { worst = diff; worstAt = at; }
                }
            TestContext.WriteLine($"grid: {points} points every 8 m, {off} beyond one code; worst {worst:R} m at {worstAt}");
            Assert.That(off, Is.Zero, $"{off} of {points} grid points differ by more than one R16 code ({code} m); " +
                                      $"worst {worst} m at {worstAt}");
            yield break;
        }
    }
}
#endif
