using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE THREE MACHINES OUTSIDE THE SHOP</b> — that they stand somewhere three machines can
    /// stand, that every number saying where is DERIVED from the village rather than typed, and that
    /// the committed scene agrees with the builder that can never be re-run.
    ///
    /// <para><b>⭐ Why the scene half of this matters more here than anywhere else.</b>
    /// <c>StPetersBuilder.Build()</c> is a full rebuild that wipes the hand-authored layer, and the
    /// refresh command its own guard names does not exist for this region (memory
    /// <c>st-peters-scene-cannot-be-rebuilt</c>). So the row was written into
    /// <c>StPeters.unity</c> by hand, and nothing will ever reconcile the two automatically. This
    /// fixture is that reconciliation: it reads the committed bytes and holds them against
    /// <see cref="StPetersMachines"/>'s own arithmetic, so moving the shop and forgetting the scene
    /// fails here instead of in a screenshot six weeks later
    /// (<c>scene-wired-is-not-builder-wired</c>).</para>
    ///
    /// <para><b>It reads the scene as TEXT rather than opening it</b>, and that is deliberate twice
    /// over: opening <c>StPeters.unity</c> in an editor normalises ~590 stale hunks the moment it is
    /// saved, and a PlayMode fixture that loads a committed region scene is a known red-maker on this
    /// repo right now. The mechanical facts a load would prove — anchors resolve, components point
    /// back at their GameObject, the roots are registered — are asserted below on the bytes.</para>
    /// </summary>
    public class StPetersMachinesTests
    {
        const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";

        static string SceneText => File.ReadAllText(ScenePath);

        static Vector2 Store => new Vector2(StPetersBuilder.GeneralStorePos.x,
                                            StPetersBuilder.GeneralStorePos.y);

        static List<float> Beams()
        {
            List<float> beams = StPetersMachines.Beams();
            Assert.That(beams, Has.Count.EqualTo(StPetersMachines.Row.Count),
                "one of the three defs did not load, so nothing below is measuring the shipped row.");
            return beams;
        }

        static Vector2 Stand(int i) => StPetersMachines.StandFor(i, Beams());

        // =============================================================================================
        //  1. THE ROW IS DERIVED — no typed coordinate anywhere in it
        // =============================================================================================

        /// <summary>The row is measured from the store's OWN gate — the single point
        /// <see cref="StPetersYards"/> already publishes for the lane edge of the shop's forecourt —
        /// rather than from a second statement of where the frontage is.</summary>
        [Test]
        public void TheRowIsMeasuredFromTheStoresOwnYardGate()
        {
            Vector2 gate = Vector2.positiveInfinity;
            foreach (Yard y in StPetersYards.Yards)
                if (y.Name == StPetersYards.GeneralStoreYard) gate = y.Gates[0];

            Assert.That(gate.x, Is.Not.EqualTo(float.PositiveInfinity),
                "the general store has no yard any more — the row has lost the thing it derives from.");
            Assert.That(Vector2.Distance(StPetersMachines.Gate, gate), Is.LessThan(1e-4f),
                "StPetersMachines.Gate must BE the yard's gate, not a copy of where it used to be.");
        }

        /// <summary>Every door on this island faces the green and no row carries an angle
        /// (<see cref="StPetersYards"/>'s own law). The row inherits that: its outward direction is the
        /// shop's, and its own axis is square to it.</summary>
        [Test]
        public void TheRowTurnsWithTheVillageRatherThanCarryingAnAngle()
        {
            Vector2 expected = (StPetersBuilder.VillageGreen - Store).normalized;

            Assert.That(Vector2.Distance(StPetersMachines.Forward, expected), Is.LessThan(1e-5f),
                "the shop faces the green; the machines are set out from that and nothing else.");
            Assert.That(StPetersMachines.Across.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Vector2.Dot(StPetersMachines.Across, StPetersMachines.Forward),
                Is.EqualTo(0f).Within(1e-5f),
                "the row runs ACROSS the frontage; a row with any forward component in it would walk " +
                "out into the green as it grew.");
        }

        /// <summary>
        /// ⭐ <b>Noses OUT, and it is not a taste.</b> The enduro has no reverse worth the name (1.2 m/s
        /// — her class has no reverse gear at all), so a machine parked nose-in would have to be pushed
        /// out of the row before she could be ridden. <c>transform.up</c> is the nose, the fleet's one
        /// heading convention.
        /// </summary>
        [Test]
        public void EveryMachineIsParkedNoseOut()
        {
            Vector3 nose = StPetersMachines.Heading * Vector3.up;

            Assert.That(nose.z, Is.EqualTo(0f).Within(1e-5f), "a ground vehicle's nose stays on the map");
            Assert.That(Vector2.Distance(new Vector2(nose.x, nose.y), StPetersMachines.Forward),
                Is.LessThan(1e-4f),
                "her nose points the way the shop faces — out toward the green, ready to ride away.");
        }

        // =============================================================================================
        //  2. THREE MACHINES FIT, WITH ROOM TO GET ON ONE
        // =============================================================================================

        /// <summary>⭐ The spacing is the ART's: each gap is that pair's own published beams plus
        /// <see cref="StPetersMachines.GapMetres"/> of clear ground, so a re-bake that widens one
        /// machine widens the gaps beside her instead of quietly overlapping her neighbour.</summary>
        [Test]
        public void NeighboursAreSpacedByTheirOwnPublishedBeams()
        {
            List<float> beams = Beams();

            for (int i = 1; i < beams.Count; i++)
            {
                float centres = Vector2.Distance(Stand(i - 1), Stand(i));
                float expected = beams[i - 1] * 0.5f + StPetersMachines.GapMetres + beams[i] * 0.5f;

                Assert.That(centres, Is.EqualTo(expected).Within(1e-4f),
                    $"{StPetersMachines.Row[i - 1].Name} → {StPetersMachines.Row[i].Name}");
                Assert.That(centres - beams[i - 1] * 0.5f - beams[i] * 0.5f,
                    Is.GreaterThanOrEqualTo(StPetersMachines.GapMetres - 1e-4f),
                    "clear ground between two BODIES is what a person walking the row experiences.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>Every machine's two ways on are reachable, and none of them is inside a neighbour.</b>
        ///
        /// <para>This is the failure PR 1 measured in a different place and it is the one that would
        /// hurt here: a saddle publishes its mount points 1.15–1.19 m off its own centreline, so three
        /// machines parked a shade too close leave the middle one mountable from one side only — and
        /// silently, because <c>InteractResolver</c> refuses by distance and says nothing.</para>
        /// </summary>
        [Test]
        public void NoMachinesWayOnFallsInsideHerNeighbour()
        {
            List<float> beams = Beams();
            Vector2 across = StPetersMachines.Across;

            for (int i = 0; i < beams.Count; i++)
            {
                var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(StPetersMachines.Row[i].DefPath);
                Assert.IsNotNull(def, StPetersMachines.Row[i].DefPath);

                // Her reach points lie on her own +x/−x, which after the row's heading is ±Across.
                float reach = Mathf.Abs(def.Mesh.DriveDoorLocal.x);
                Assert.That(reach, Is.GreaterThan(0f), $"{def.name} publishes no way on");

                foreach (int sign in new[] { -1, 1 })
                {
                    Vector2 point = Stand(i) + across * (reach * sign);

                    for (int j = 0; j < beams.Count; j++)
                    {
                        if (j == i) continue;
                        float along = Mathf.Abs(Vector2.Dot(point - Stand(j), across));
                        Assert.That(along, Is.GreaterThan(beams[j] * 0.5f),
                            $"{StPetersMachines.Row[i].Name}'s way on at {point} is inside " +
                            $"{StPetersMachines.Row[j].Name}'s body. A rider standing exactly where " +
                            "the art says she may mount would be standing in the next machine.");
                    }
                }
            }
        }

        /// <summary>The gate the fence data already publishes stays walkable: the row starts clear of
        /// the opening rather than parking a quad across the shop's own path.</summary>
        [Test]
        public void TheShopsGateIsNotBlocked()
        {
            List<float> beams = Beams();
            float nearEdge = Mathf.Abs(Vector2.Dot(Stand(0) - StPetersMachines.Gate,
                                                   StPetersMachines.Across)) - beams[0] * 0.5f;

            Assert.That(nearEdge, Is.GreaterThanOrEqualTo(YardPlan.GateWidthMetres * 0.5f - 1e-4f),
                "the nearest machine stands clear of the gate's half-width, so the opening the fence " +
                "data publishes is still an opening.");
        }

        /// <summary>They stand OUTSIDE the shop's yard, not on it — the store's forecourt is
        /// <c>MownStyle.Striped</c> and somebody cuts it. Still within its frontage, so they read as
        /// belonging to the shop rather than abandoned on the green.</summary>
        [Test]
        public void TheRowStandsOffTheShopsGrassAndInsideItsFrontage()
        {
            Yard store = default;
            foreach (Yard y in StPetersYards.Yards)
                if (y.Name == StPetersYards.GeneralStoreYard) store = y;

            List<float> beams = Beams();
            for (int i = 0; i < beams.Count; i++)
            {
                Vector2 p = Stand(i);
                Assert.IsFalse(store.Contains(p),
                    $"{StPetersMachines.Row[i].Name} is parked on the shop's mown forecourt.");

                float across = Mathf.Abs(Vector2.Dot(p - StPetersMachines.Gate, StPetersMachines.Across))
                               + beams[i] * 0.5f;
                Assert.That(across, Is.LessThanOrEqualTo(13.0f * 0.5f + 1e-3f),
                    $"{StPetersMachines.Row[i].Name} has walked past the end of the shop's own " +
                    "frontage and is now standing in front of the neighbour's yard.");
            }
        }

        // =============================================================================================
        //  3. THE GROUND UNDER THEM
        // =============================================================================================

        /// <summary>Nothing grows where they stand and nothing is walked over. The village clearing
        /// keeps the woods off (44 m of it, and the row is inside 10), and both painted paths run east
        /// and west from the green rather than through the shop's frontage — asked through the SAME
        /// predicates the scatter uses, so a moved path moves this answer too.</summary>
        [Test]
        public void NothingIsPlantedOrWalkedWhereTheyStand()
        {
            List<float> beams = Beams();

            for (int i = 0; i < beams.Count; i++)
            {
                Vector2 p = Stand(i);
                string who = StPetersMachines.Row[i].Name;

                Assert.That(Vector2.Distance(p, StPetersBuilder.VillageHearthPos),
                    Is.LessThan(StPetersWoods.VillageClearingRadius),
                    $"{who} stands outside the village clearing, where the woods plant — a machine " +
                    "parked in a spruce.");

                Assert.IsFalse(StPetersWoods.OnPaintedPath(p),
                    $"{who} is parked on one of the two painted paths the villagers walk.");
            }
        }

        // =============================================================================================
        //  4. THE COMMITTED SCENE AGREES WITH THE BUILDER
        // =============================================================================================

        /// <summary>The three are IN the committed scene, once each, as scene ROOTS. A document that is
        /// not in <c>m_Roots</c> simply is not in the hierarchy — it loads, it resolves, and it is
        /// nowhere.</summary>
        [Test]
        public void AllThreeAreCommittedToTheSceneAsRoots()
        {
            string text = SceneText;
            string roots = text.Substring(text.IndexOf("  m_Roots:", System.StringComparison.Ordinal));

            foreach ((string name, string _) in StPetersMachines.Row)
            {
                Assert.That(Regex.Matches(text, "m_Name: " + Regex.Escape(name) + "\r?\n").Count,
                    Is.EqualTo(1), $"{name} appears in StPeters.unity other than exactly once.");

                SceneObject o = ReadFromScene(text, name);
                Assert.That(roots, Does.Contain("- {fileID: " + o.TransformId + "}"),
                    $"{name}'s transform is not registered as a scene root, so she is not in the " +
                    "hierarchy at all.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>The reconciliation.</b> Where each machine stands in the committed scene, against
        /// where <see cref="StPetersMachines"/> says she stands. Nothing will ever do this
        /// automatically on this island, so it is done here.
        /// </summary>
        [Test]
        public void TheCommittedSceneStandsThemWhereTheBuilderDoes()
        {
            string text = SceneText;
            List<float> beams = Beams();

            for (int i = 0; i < StPetersMachines.Row.Count; i++)
            {
                (string name, string defPath) = StPetersMachines.Row[i];
                SceneObject o = ReadFromScene(text, name);

                Assert.That(Vector2.Distance(o.Position, Stand(i)), Is.LessThan(1e-3f),
                    $"{name} stands at {o.Position} in the scene and at {Stand(i)} in the builder. " +
                    "Move the shop and this is the test that says the scene did not follow.");

                Assert.That(Quaternion.Angle(o.Rotation, StPetersMachines.Heading),
                    Is.LessThan(0.05f),
                    $"{name}'s heading in the scene is {Quaternion.Angle(o.Rotation, StPetersMachines.Heading):0.###}° " +
                    "off the builder's. Compared as an ANGLE, not bit for bit: the scene's floats and " +
                    "Unity's own FromToRotation are two transcriptions of one turn.");

                Assert.That(o.VehicleGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(defPath)),
                    $"{name} carries a different def in the scene than the builder gives her.");

                Assert.IsTrue(o.Drivable,
                    $"{name} is scenery in the committed scene — she registers no door and refuses " +
                    "every press, silently, which is exactly what a machine the owner is meant to " +
                    "ride must not be.");
            }
        }

        // ---- reading one object out of the committed scene ---------------------------------------

        struct SceneObject
        {
            public string TransformId;
            public Vector2 Position;
            public Quaternion Rotation;
            public string VehicleGuid;
            public bool Drivable;
        }

        /// <summary>Pull one named root out of the scene text: its transform, where it stands, and what
        /// its <c>ParkedVehicle</c> carries. Deliberately a text read — see the fixture remarks.
        ///
        /// <para>Sliced by <c>IndexOf</c> rather than matched with a whole-file regex: this scene is
        /// 6 MB, and a tempered-dot pattern over it costs seconds per call for an answer a linear
        /// scan gives immediately.</para>
        /// </summary>
        static SceneObject ReadFromScene(string text, string name)
        {
            int at = text.IndexOf("\n  m_Name: " + name + "\n", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), $"{name} is not in {ScenePath}");

            int header = text.LastIndexOf("--- !u!1 &", at, System.StringComparison.Ordinal);
            Assert.That(header, Is.GreaterThan(0), $"{name} has no GameObject document");

            string goId = text.Substring(header + 10, text.IndexOf('\n', header) - header - 10);
            string block = Document(text, header);

            var components = new List<string>();
            foreach (Match m in Regex.Matches(block, @"- component: \{fileID: (\d+)\}"))
                components.Add(m.Groups[1].Value);
            Assert.That(components, Is.Not.Empty, $"{name} has no components");

            var o = new SceneObject();
            foreach (string id in components)
            {
                int start = text.IndexOf(" &" + id + "\n", System.StringComparison.Ordinal);
                Assert.That(start, Is.GreaterThan(0), $"{name}: component {id} does not resolve");
                start = text.LastIndexOf("--- !u!", start, System.StringComparison.Ordinal);

                string type = text.Substring(start + 7, text.IndexOf(' ', start + 7) - start - 7);
                string body = Document(text, start);

                Assert.That(body, Does.Contain("m_GameObject: {fileID: " + goId + "}"),
                    $"{name}: component {id} does not point back at her.");

                if (type == "4")                           // Transform
                {
                    o.TransformId = id;
                    o.Position = Vec2(body, "m_LocalPosition");
                    o.Rotation = Quat(body, "m_LocalRotation");
                }
                else if (body.Contains("HiddenHarbours.Vehicles.ParkedVehicle"))
                {
                    o.VehicleGuid = Regex.Match(body, @"_vehicle: \{fileID: \d+, guid: ([0-9a-f]+)")
                                         .Groups[1].Value;
                    o.Drivable = Regex.Match(body, @"_drivable: (\d)").Groups[1].Value == "1";
                }
            }

            Assert.That(o.TransformId, Is.Not.Null.And.Not.Empty, $"{name} has no Transform");
            Assert.That(o.VehicleGuid, Is.Not.Null.And.Not.Empty, $"{name} carries no ParkedVehicle");
            return o;
        }

        /// <summary>One YAML document, from its <c>--- !u!</c> header to the next one.</summary>
        static string Document(string text, int header)
        {
            int end = text.IndexOf("\n--- ", header, System.StringComparison.Ordinal);
            return end < 0 ? text.Substring(header) : text.Substring(header, end - header);
        }

        static float Field(string body, string mapping, string key)
        {
            Match m = Regex.Match(body, mapping + @": \{[^}]*\b" + key + @": (-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"{mapping}.{key} not readable");
            return float.Parse(m.Groups[1].Value,
                               System.Globalization.CultureInfo.InvariantCulture);
        }

        static Vector2 Vec2(string body, string mapping) =>
            new Vector2(Field(body, mapping, "x"), Field(body, mapping, "y"));

        static Quaternion Quat(string body, string mapping) =>
            new Quaternion(Field(body, mapping, "x"), Field(body, mapping, "y"),
                           Field(body, mapping, "z"), Field(body, mapping, "w"));
    }
}
