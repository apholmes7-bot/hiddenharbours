using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The St Peters net shed's doorway, beside the trolley stage. The shed stands at Dir 5, its double doors facing
    /// south-east down a short ramp; the bank in front of the ramp falls away to the wharf's north rim. A planked
    /// landing now bridges that bank from the ramp's foot to the wharf, the net reel that stood in the doorway is on
    /// the wharf, and no plant grows in the way.
    ///
    /// <para>The shed's sprite is baked on the ¾ camera, so a plan offset (dx, dy) from a piece's <c>At</c> is drawn
    /// at (dx, dy · sin e). The door and the place a figure stands to use it come from the shed's sidecar, the
    /// props' footprints from the wharf decor catalogue, the wharf's north edge from the landing deck's contract,
    /// so these pins follow the pieces if any of them moves.</para>
    ///
    /// <para>The shed has no way in yet in St Peters (its walls are one solid collider); these pins keep the way to
    /// its doors clear and stood on timber, so the door can open on to it when it does.</para>
    /// </summary>
    public class StPetersNetShedDoorwayTests
    {
        const string EastEnd = "keyscene.stp_east_end", Landing = "keyscene.stp_landing";
        const string Shed = StPetersTrolleyStageClearance.ShedId, ShedLanding = StPetersTrolleyStageClearance.LandingId;
        const string Reel = "prop.stp_shed_reel";
        const string DecorCatalogue = "docs/art/rigs/iso-rig-pack/wharf-decor-iso/wharfDecorRig.catalogue.json";

        /// <summary>The width of the way kept clear from the doors to the wharf, in metres: a figure and a barrow.</summary>
        const float WayHalfWidth = 0.4f;

        /// <summary>How far the way is sampled at a time, in metres: well under a figure's step.</summary>
        const float Step = 0.1f;

        /// <summary>How far the landing may lap on to the wharf's planks past its north edge, in drawn metres: a
        /// nosing's width, not a second deck.</summary>
        const float LapsTheWharf = 0.25f;

        /// <summary>How near a lamp a prop may stand on the wharf, in metres: a lamp's post wants a clear ring.</summary>
        const float LampClear = 1f;

        static float Depth => Mathf.Sin(SpriteLightMath.CameraElevationDeg * Mathf.Deg2Rad);

        static KeySceneDef Scene(string id) => StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == id);
        static KeyScenePiece Piece(string id, string scene) => Scene(scene).Pieces.Single(p => p.Id == id);

        static Vector2 Drawn(KeyScenePiece at, Vector2 local)
        {
            Vector2 w = SetPieceDef.ToWorld(local, at.Dir);
            return at.At + new Vector2(w.x, w.y * Depth);
        }

        static Vector2 Local(KeyScenePiece at, Vector2 drawn)
        {
            Vector2 plan = drawn - at.At;
            plan.y /= Depth;
            return SetPieceDef.ToWorld(plan, (8 - at.Dir % 8) % 8);
        }

        // ---- the shed's sidecar -----------------------------------------------------------------------------

        [Serializable] sealed class Footprint { public string id; public float x0, x1, y0, y1; public bool walk; }
        [Serializable] sealed class Door { public string id; public float[] at; public float[] stand; }
        [Serializable] sealed class ShedSidecar { public Footprint[] FOOTPRINT; public Door[] DOORS; }

        static ShedSidecar ReadShed() => JsonUtility.FromJson<ShedSidecar>(File.ReadAllText(
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, StPetersTrolleyStageClearance.ShedSidecarPath)));

        static Door TheDoor()
        {
            Door door = ReadShed().DOORS.Single(d => d.id == "door");
            Assert.That(door.at.Length, Is.GreaterThanOrEqualTo(2)); Assert.That(door.stand.Length, Is.EqualTo(2));
            return door;
        }

        static bool InShedWalls(Vector2 drawn)
        {
            KeyScenePiece shed = Piece(Shed, EastEnd);
            Vector2 l = Local(shed, drawn);
            return ReadShed().FOOTPRINT.Where(f => !f.walk).Any(f => l.x > f.x0 && l.x < f.x1 && l.y > f.y0 && l.y < f.y1);
        }

        // ---- the props' footprints ----------------------------------------------------------------------------

        [Serializable] sealed class DecorProp { public string key; public float[] footprint_m; }
        [Serializable] sealed class DecorCatalogueJson { public DecorProp[] props; }

        static Dictionary<string, Vector2> DecorFootprints()
        {
            var cat = JsonUtility.FromJson<DecorCatalogueJson>(File.ReadAllText(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, DecorCatalogue)));
            return cat.props.Where(p => p.footprint_m != null && p.footprint_m.Length == 2)
                      .ToDictionary(p => p.key, p => new Vector2(p.footprint_m[0], p.footprint_m[1]));
        }

        /// <summary>Whether a decor prop, as placed, stands over a drawn point: its catalogue footprint, centred on
        /// its ground point and turned by its facing.</summary>
        static bool Covers(KeyScenePiece prop, Vector2 size, Vector2 drawn, float pad = 0f)
        {
            Vector2 l = Local(prop, drawn);
            return Mathf.Abs(l.x) < size.x / 2f + pad && Mathf.Abs(l.y) < size.y / 2f + pad;
        }

        static Rect DrawnBounds(KeyScenePiece prop, Vector2 size)
        {
            Vector2[] c =
            {
                Drawn(prop, new Vector2(-size.x / 2f, -size.y / 2f)), Drawn(prop, new Vector2(size.x / 2f, -size.y / 2f)),
                Drawn(prop, new Vector2(size.x / 2f, size.y / 2f)), Drawn(prop, new Vector2(-size.x / 2f, size.y / 2f)),
            };
            return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
        }

        static IEnumerable<KeyScenePiece> StandingDecor() =>
            StPetersLayerRefresh.LoadStPetersKeyScenes().Where(s => s.Placed).SelectMany(s => s.Pieces)
                .Where(p => p.Kit == StPetersLayerRefresh.DecorKit && p.Variants.Contains(KeySceneDef.Today));

        // ---- the way out ----------------------------------------------------------------------------------------

        static Vector2 DoorPoint() { Door d = TheDoor(); return Drawn(Piece(Shed, EastEnd), new Vector2(d.at[0], d.at[1])); }
        static Vector2 StandPoint() { Door d = TheDoor(); return Drawn(Piece(Shed, EastEnd), new Vector2(d.stand[0], d.stand[1])); }
        static float WharfNorthEdge() { var s = StPetersLandingDeckBaker.ReadContract().surface; return s.y + s.depth; }

        static Rect LandingBounds()
        {
            KeyScenePiece landing = Piece(ShedLanding, EastEnd);
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(StPetersTrolleyStageClearance.LandingDefPath);
            Assert.IsNotNull(def, StPetersTrolleyStageClearance.LandingDefPath + " does not load: bake the set pieces");
            return StPetersTrolleyStageClearance.Drawn(landing.At, landing.Dir, def.BoundsMin.x, def.BoundsMax.x,
                                                       def.BoundsMin.y, def.BoundsMax.y, 0f);
        }

        /// <summary>The way a figure walks out: from the doors down the ramp to where it stands, then straight down
        /// the landing to the wharf's planks, sampled every <see cref="Step"/> and widened by
        /// <see cref="WayHalfWidth"/> either side. The part on the ramp is laid out in the shed's own plan frame
        /// (straight out of the doors, widened across them) and starts a step past the wall's face: the door's sill
        /// stands in the wall. The part on the landing runs straight south, widened east and west.</summary>
        static List<Vector2> TheWayOut(bool onlyTheLanding = false)
        {
            KeyScenePiece shed = Piece(Shed, EastEnd);
            Door door = TheDoor();
            Vector2 stand = StandPoint(), wharf = new Vector2(stand.x, WharfNorthEdge() - Step);
            var way = new List<Vector2>();
            if (!onlyTheLanding)
            {
                float wall = ReadShed().FOOTPRINT.Where(f => !f.walk).Max(f => f.y1), from = Mathf.Max(door.at[1], wall) + Step;
                int n = Mathf.CeilToInt((door.stand[1] - from) / Step);
                for (int i = 0; i <= n; i++)
                {
                    float y = Mathf.Lerp(from, door.stand[1], i / (float)n);
                    foreach (float dx in new[] { 0f, WayHalfWidth, -WayHalfWidth })
                        way.Add(Drawn(shed, new Vector2(door.stand[0] + dx, y)));
                }
            }
            int m = Mathf.CeilToInt(Vector2.Distance(stand, wharf) / Step);
            for (int i = 0; i <= m; i++)
            {
                Vector2 p = Vector2.Lerp(stand, wharf, i / (float)m);
                way.Add(p); way.Add(p + Vector2.right * WayHalfWidth); way.Add(p - Vector2.right * WayHalfWidth);
            }
            return way;
        }

        [Test]
        public void TheNetReel_StandsOnTheWharf_ClearOfTheShedsDoorway_AndOfTheLamps()
        {
            KeyScenePiece reel = Piece(Reel, EastEnd);
            Vector2 size = DecorFootprints()["netReel"];
            Assert.AreEqual(KeySceneDef.StandsOnDeck, reel.StandsOn, "the reel stands on the wharf's planks");
            var s = StPetersLandingDeckBaker.ReadContract().surface;
            Rect deck = Rect.MinMaxRect(s.x, s.y, s.x + s.width, s.y + s.depth), bounds = DrawnBounds(reel, size);
            Assert.IsTrue(deck.Contains(bounds.min) && deck.Contains(bounds.max), $"the reel's footprint {bounds} runs off the wharf {deck}");

            foreach (Vector2 p in TheWayOut())
                Assert.IsFalse(Covers(reel, size, p), $"the reel stands in the way out of the shed at {p}");
            foreach (Rect d in StPetersTrolleyStageClearance.ReadDoorway())
                Assert.IsFalse(d.Overlaps(bounds), $"the reel's footprint {bounds} lies in the shed's doorway {d}");
            Assert.IsFalse(StPetersTrolleyStageClearance.Read().Stage.Overlaps(bounds), "the reel stands on the trolley stage's clearing");

            foreach (KeyScenePiece lamp in Scene(Landing).Pieces.Where(p => p.Piece == "lanternPost"))
                Assert.IsFalse(Covers(reel, size, lamp.At, LampClear), $"the reel crowds {lamp.Id} at {lamp.At}");
            foreach (KeyScenePiece other in StandingDecor().Where(p => p.Id != Reel && p.StandsOn == KeySceneDef.StandsOnDeck))
                if (DecorFootprints().TryGetValue(other.Piece, out Vector2 o))
                    Assert.IsFalse(DrawnBounds(other, o).Overlaps(bounds), $"the reel stands in {other.Id}");
        }

        [Test]
        public void TheLanding_TakesTheFigureFromTheDoors_OnToTheWharf()
        {
            KeyScenePiece landing = Piece(ShedLanding, EastEnd);
            Assert.AreEqual(KeySceneDef.StandsOnGround, landing.StandsOn);
            Assert.AreEqual("floor", landing.Layer, "the landing is walked on, drawn under figures");
            Assert.AreEqual("on", landing.Walk);

            Rect boards = LandingBounds();
            Vector2 stand = StandPoint();
            Assert.IsTrue(boards.Contains(stand), $"the place a figure stands at the doors {stand} is off the landing {boards}");
            float edge = WharfNorthEdge();
            Assert.LessOrEqual(boards.yMin, edge, $"the landing stops at {boards.yMin}, short of the wharf's edge at {edge}");
            Assert.Greater(boards.yMin, edge - LapsTheWharf, $"the landing runs on over the wharf to {boards.yMin}");
            foreach (Vector2 p in TheWayOut(onlyTheLanding: true).Where(p => p.y >= edge))
                Assert.IsTrue(boards.Contains(p), $"the way down to the wharf leaves the boards at {p}");
        }

        [Test]
        public void TheWayOut_RunsStraight_ThroughNoWallAndNoProp()
        {
            var sizes = DecorFootprints();
            var props = StandingDecor().Where(p => sizes.ContainsKey(p.Piece)).ToList();
            List<Vector2> way = TheWayOut();
            Assert.Greater(way.Count, 30, "the way out was not sampled");
            foreach (Vector2 p in way)
            {
                Assert.IsFalse(InShedWalls(p), $"the way out runs into the shed's walls at {p}");
                foreach (KeyScenePiece prop in props)
                    Assert.IsFalse(Covers(prop, sizes[prop.Piece], p), $"{prop.Id} stands in the way out of the shed at {p}");
            }
        }

        [Test]
        public void NoPlantGrows_InTheShedsDoorway()
        {
            Rect[] doorway = StPetersTrolleyStageClearance.ReadDoorway();
            Assert.AreEqual(2, doorway.Length, "the doorway is the ramp and its landing");
            StPetersTrolleyStageClearance.Clearing clearing = StPetersTrolleyStageClearance.Read();
            Assert.IsTrue(clearing.Contains(DoorPoint()) && clearing.Contains(StandPoint()), "the clearing does not cover the doors");
            foreach (Vector2 p in TheWayOut()) Assert.IsTrue(clearing.Contains(p), $"the clearing does not cover the way out at {p}");

            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersBuildingClearance.DefinitionPath);
            foreach (var site in definition.Authored)
                foreach (Rect d in doorway)
                    Assert.IsFalse(d.Contains(site.At), $"{site.Id} at {site.At} grows in the shed's doorway");

            var ground = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(StPetersBuildingClearance.GroundPath);
            byte[] cells = ground.Decode();
            int seen = 0;
            for (int y = 0; y < ground.Height; y++)
            for (int x = 0; x < ground.Width; x++)
            {
                var at = ground.Origin + new Vector2(x + .5f, y + .5f) * ground.CellSize;
                if (!doorway.Any(d => d.Contains(at))) continue;
                Assert.AreEqual(255, cells[y * ground.Width + x], $"the meadow may seed at {at}, in the shed's doorway");
                seen++;
            }
            Assert.Greater(seen, 0, "no ground cell lies in the doorway");
        }
    }
}
