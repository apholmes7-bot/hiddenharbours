using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Audit slice 1's headless census of the COMMITTED scene, never an editor-opened or rebuilt one.
    /// Every BuildingInterior is found by its serialized type. Exact ground-wall polygons prove
    /// there is one doorway and no other gap; its sheared world-space clearance fits the actual foot.
    /// Upper floors with no stairs remain the separately documented slice-2 gap in the PlayMode test.
    /// </summary>
    public class StPetersBuildingCensusTests
    {
        const int CircleClass = 58, PolygonClass = 60, SpriteClass = 212;
        const float GeometryTolerance = 0.0001f;
        static readonly Regex VectorPattern = new Regex(@"\{x: ([^,]+), y: ([^}]+)\}");
        SceneYaml _scene;

        [SetUp]
        public void ReadCommittedText() => _scene = SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));

        Doc Owner(Doc component) => _scene.Require(component.FieldRef("m_GameObject"), "component owner");

        Matrix4x4 WorldMatrix(Doc transform)
        {
            MatchCollection q = Regex.Matches(transform.Field("m_LocalRotation"), @": ([^,}]+)");
            Assert.That(q.Count, Is.EqualTo(4), "A serialized quaternion has four coordinates");
            var rotation = new Quaternion(ParseFloat(q[0].Groups[1].Value), ParseFloat(q[1].Groups[1].Value),
                ParseFloat(q[2].Groups[1].Value), ParseFloat(q[3].Groups[1].Value));
            Matrix4x4 local = Matrix4x4.TRS(ParseVec3(transform.Field("m_LocalPosition")), rotation,
                ParseVec3(transform.Field("m_LocalScale")));
            long parent = transform.FieldRef("m_Father");
            return parent == 0 ? local : WorldMatrix(_scene.Require(parent, "parent transform")) * local;
        }

        bool Active(Doc go)
        {
            if (go.Field("m_IsActive") != "1") return false;
            long parent = _scene.TransformOf(go).FieldRef("m_Father");
            return parent == 0 || Active(Owner(_scene.Require(parent, "parent transform")));
        }

        InteriorFootprint Footprint(Doc component)
        {
            Vector2 centre = WorldMatrix(_scene.TransformOf(Owner(component))).MultiplyPoint3x4(Vector3.zero);
            return new InteriorFootprint(centre, ParseFloat(component.Field("_widthMetres")),
                ParseFloat(component.Field("_lengthMetres")), ParseInt(component.Field("_facing")),
                ParseInt(component.Field("_facings")), ParseFloat(component.Field("_groundDepthScale")),
                component.Field("_doorOnPlusY") == "1" ? 1f : -1f,
                ParseFloat(component.Field("_doorAcrossMetres")));
        }

        static Vector2 Vector(string text)
        {
            Match match = VectorPattern.Match(text);
            Assert.That(match.Success, Is.True, "A serialized 2D point is required");
            return new Vector2(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value));
        }

        Vector2[] WorldPolygon(Doc collider)
        {
            string points = collider.Text.Substring(collider.Text.IndexOf("  m_Points:", StringComparison.Ordinal));
            Assert.That(Regex.Matches(points, @"(?m)^    - - ").Count, Is.EqualTo(1),
                "A wall has one solid polygon, never multiple paths that can cancel into holes");
            Matrix4x4 matrix = WorldMatrix(_scene.TransformOf(Owner(collider)));
            Vector2 offset = Vector(collider.Field("m_Offset"));
            return VectorPattern.Matches(points).Cast<Match>()
                .Select(m => (Vector2)matrix.MultiplyPoint3x4(Vector(m.Value) + offset)).ToArray();
        }

        static bool SameQuad(Vector2[] expected, Vector2[] actual) => expected.Length == actual.Length &&
            Enumerable.Range(0, actual.Length).Any(start => new[] { 1, -1 }.Any(direction =>
                Enumerable.Range(0, expected.Length).All(index => Vector2.Distance(expected[index],
                    actual[(start + direction * index + actual.Length) % actual.Length]) < GeometryTolerance)));

        [Test]
        public void EverySerializedInterior_HasARoomSheet_AndOnlyOnePlayerWideGapInItsClosedGroundWalls()
        {
            Doc[] buildings = _scene.Docs.Where(d => d.IsScript(typeof(BuildingInterior).FullName)).ToArray();
            Assert.That(buildings.Length, Is.GreaterThanOrEqualTo(10), "The enterable-building census may grow, never shrink");
            Doc walker = _scene.Docs.Single(d => d.IsScript(typeof(PlayerWalkController).FullName));
            Doc player = Owner(walker);
            Doc foot = _scene.ComponentsOf(player).Single(d => d.ClassId == CircleClass && d.Field("m_IsTrigger") == "0");
            Assert.That(foot.Field("m_Enabled"), Is.EqualTo("1"));
            Matrix4x4 playerMatrix = WorldMatrix(_scene.TransformOf(player));
            float diameter = ParseFloat(foot.Field("m_Radius")) * 2f * Mathf.Max(
                playerMatrix.MultiplyVector(Vector3.right).magnitude, playerMatrix.MultiplyVector(Vector3.up).magnitude);
            Assert.That(diameter, Is.GreaterThan(0f));

            foreach (Doc building in buildings)
            {
                Doc go = Owner(building);
                string who = go.Field("m_Name");
                Assert.That(building.Field("m_Enabled"), Is.EqualTo("1"), who);
                Assert.That(Active(go), Is.True, who);
                // A stale explicit occupant would make the real walk test observe another body.
                Assert.That(building.FieldRef("_occupant"), Is.AnyOf(0L, _scene.TransformOf(player).FileId), who);
                InteriorFootprint floor = Footprint(building);
                float thickness = ParseFloat(building.Field("_wallThicknessMetres"));
                float doorway = ParseFloat(building.Field("_doorwayWidthMetres"));
                Assert.That(floor.WidthMetres, Is.GreaterThan(thickness * 2f), who);
                Assert.That(floor.LengthMetres, Is.GreaterThan(thickness * 2f), who);
                Assert.That(floor.DepthScale, Is.GreaterThan(0f), who);

                // Across the sheared corridor, not just the nominal unsquashed 1.4 m setting.
                Vector2 across = floor.ModelToWorld(Vector2.right) - floor.Centre;
                Vector2 along = floor.ModelToWorld(Vector2.up) - floor.Centre;
                float gap0 = Mathf.Max(-floor.WidthMetres * 0.5f, floor.DoorAcrossMetres - doorway * 0.5f);
                float gap1 = Mathf.Min(floor.WidthMetres * 0.5f, floor.DoorAcrossMetres + doorway * 0.5f);
                float clearance = (gap1 - gap0) * Mathf.Abs(across.x * along.y - across.y * along.x) / along.magnitude;
                Assert.That(clearance, Is.GreaterThanOrEqualTo(diameter), who + " doorway fits the placed player's actual foot");

                Doc room = _scene.Require(building.FieldRef("_room"), who + " room renderer");
                Assert.That(room.ClassId, Is.EqualTo(SpriteClass), who);
                Assert.That(room.FieldRef("m_Sprite"), Is.Not.Zero, who + " room sheet is assigned");
                Match guid = Regex.Match(room.Field("m_Sprite"), @"guid: ([0-9a-f]{32})");
                Assert.That(guid.Success, Is.True, who);
                string sheet = UnityEditor.AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value);
                Assert.That(sheet, Is.Not.Empty, who + " room sheet resolves");
                Assert.That(File.Exists(sheet), Is.True, who + " room sheet exists");
                Vector2 roomCentre = WorldMatrix(_scene.TransformOf(Owner(room))).MultiplyPoint3x4(Vector3.zero);
                Assert.That(Vector2.Distance(roomCentre, floor.Centre), Is.LessThan(GeometryTolerance), who + " floor registration");

                Doc walls = _scene.ChildNamed(_scene.TransformOf(go), BuildingInteriorStander.WallsChildName);
                Assert.That(Active(walls), Is.True, who + " walls stay solid outside and inside");
                Doc[] colliders = _scene.ComponentsOf(walls).Where(d => d.ClassId == PolygonClass).ToArray();
                Vector2[][] expected = floor.WallQuads(thickness, doorway);
                Assert.That(colliders.Length, Is.EqualTo(expected.Length), who + " back, both sides and both door jambs");
                var unmatched = expected.ToList();
                foreach (Doc collider in colliders)
                {
                    Assert.That(collider.Field("m_Enabled"), Is.EqualTo("1"), who);
                    Assert.That(collider.Field("m_IsTrigger"), Is.EqualTo("0"), who);
                    Assert.That(collider.Field("m_CompositeOperation"), Is.EqualTo("0"), who);
                    Vector2[] actual = WorldPolygon(collider);
                    int match = unmatched.FindIndex(quad => SameQuad(quad, actual));
                    Assert.That(match, Is.GreaterThanOrEqualTo(0), who + " wall polygon must close its own side, with no extra gap");
                    unmatched.RemoveAt(match);
                }
                Assert.That(unmatched, Is.Empty, who + " the entire perimeter except the doorway is closed");
                TestContext.WriteLine($"CENSUS {who}: doorway {clearance:F3}m, foot {diameter:F3}m, {colliders.Length} solid walls, room sheet assigned");
            }
            TestContext.WriteLine($"CENSUS TOTAL {buildings.Length}");
        }
    }
}
