using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE TWO DOORS THAT ARE NOT POSES (PR 3c)</b> — the rollup and the liftgate, which PR 3a
    /// declared and deferred.
    ///
    /// <para><b>Its subject is WHY each is a state bake rather than a pose</b>, because that is the
    /// decision a future change could quietly undo. Both fail to be poseable, and they fail
    /// differently: the rollup changes her face COUNT, and the liftgate keeps every face and is
    /// simply not rigid. Each of those is measured here against the rig, not restated from the
    /// catalog — a test that repeated the declaration would agree with it by construction.</para>
    ///
    /// <para>⚠️ <b>Rigidity is measured by DISTANCE PRESERVATION, not by a common offset.</b> A rigid
    /// motion preserves every inter-point distance; a translation test asks something stricter and a
    /// rigid ROTATION fails it by construction. That distinction is not academic here — measured
    /// with a translation test the liftgate looks decomposable into a rotating arm set and a
    /// translating platform, and it is not. It is the same error class
    /// <see cref="RoadFleetDoorTests"/> already had to correct once, when an angle spread stood in
    /// for a positional residual.</para>
    /// </summary>
    public class RoadFleetTwoStateDoorTests
    {
        /// <summary>
        /// How far apart two vertices' separation may drift before the motion between two poses is
        /// not rigid. The same 0.1 mm <see cref="RoadFleetDoorTests"/> uses for a hinge residual, and
        /// for the same reason: above float32's resolution at these coordinates and 300× below one
        /// 31 mm pixel.
        ///
        /// <para><b>Nothing measured here is anywhere near it in either direction</b>, which is the
        /// property that makes it a threshold rather than a tuning knob. The one rigid thing in
        /// either door — the liftgate's flip half through <c>unfold</c> — measures EXACTLY 0, and
        /// everything that is not rigid misses by 60 mm to 1.7 m.</para>
        /// </summary>
        const double RigidEpsilon = 1e-4;

        public sealed class BoxTruck
        {
            public string Key, Label;
            /// <summary>Faces the whole body builds shut, and open — the count CHANGES, which is the
            /// rollup's whole problem.</summary>
            public int FacesShut, FacesOpen;
            /// <summary>What the rollup claims in each of its own builds.</summary>
            public int RollupShut, RollupOpen;
            /// <summary>What is left when the door is taken out — the same in BOTH states, which is
            /// what lets the body be baked once.</summary>
            public int BodyWithoutDoor;
            public override string ToString() => Key;
        }

        static readonly BoxTruck[] Trucks =
        {
            new BoxTruck { Key = "caboverBox", Label = "Cabover Box Truck",
                           FacesShut = 1090, FacesOpen = 1084,
                           RollupShut = 18, RollupOpen = 12, BodyWithoutDoor = 1072 },
            new BoxTruck { Key = "convBox", Label = "Conventional Box Truck",
                           FacesShut = 1211, FacesOpen = 1205,
                           RollupShut = 21, RollupOpen = 15, BodyWithoutDoor = 1190 },
        };

        public static IEnumerable<BoxTruck> Both() => Trucks;

        // ---- apparatus ---------------------------------------------------------------------------

        static string Full(string p) => Path.Combine(RigCatalog.RepoRoot, p);
        static string N(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A host with the truck's rig widened and her rest pose installed, plus the
        /// measuring apparatus — all of it computed BY THE RIG.</summary>
        static IRigScriptHost Host(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(v.ScriptPath)), v.GlobalName,
                new[] { "build", "makeMats" }, v.ScriptPath));

            host.Execute(
                "var __R = " + v.GlobalName + ";\n" +
                "function __base(){ return " + v.RestPose + "; }\n" +
                "function __pose(o){ var s = __base(); for (var k in o) s[k] = o[k]; return s; }\n" +
                "function __faces(o){ return __R.build(__R.resolve(__pose(o))); }\n" +
                "function __count(o){ return __faces(o).length; }\n" +
                // faces differing between two poses; null when the two builds are different shapes
                "function __between(a, b){\n" +
                "  var A = __faces(a), B = __faces(b), out = [];\n" +
                "  if (A.length !== B.length) return null;\n" +
                "  for (var i = 0; i < A.length; i++) {\n" +
                "    var p = A[i].v, q = B[i].v, d = false;\n" +
                "    for (var k = 0; k < p.length && !d; k++)\n" +
                "      for (var c = 0; c < 3; c++) if (p[k][c] !== q[k][c]) { d = true; break; }\n" +
                "    if (d) out.push(i);\n" +
                "  }\n" +
                "  return out;\n" +
                "}\n" +
                "function __nMoved(a, b){ var s = __between(a,b); return s === null ? -1 : s.length; }\n" +
                // ⭐ rigidity, fitting nothing: the worst change in any pairwise distance
                "function __rigidDev(a, b, mat){\n" +
                "  var A = __faces(a), B = __faces(b), s = __between(a, b);\n" +
                "  if (s === null) return -1;\n" +
                "  var P = [], Q = [];\n" +
                "  for (var g = 0; g < s.length; g++) {\n" +
                "    if (mat && A[s[g]].mat !== mat) continue;\n" +
                "    var p = A[s[g]].v, q = B[s[g]].v;\n" +
                "    for (var k = 0; k < p.length; k++) { P.push(p[k]); Q.push(q[k]); }\n" +
                "  }\n" +
                "  if (!P.length) return -1;\n" +
                "  var worst = 0;\n" +
                "  for (var i = 0; i < P.length; i++)\n" +
                "    for (var j = i+1; j < P.length; j++) {\n" +
                "      var dp = Math.sqrt(Math.pow(P[i][0]-P[j][0],2)+Math.pow(P[i][1]-P[j][1],2)+Math.pow(P[i][2]-P[j][2],2));\n" +
                "      var dq = Math.sqrt(Math.pow(Q[i][0]-Q[j][0],2)+Math.pow(Q[i][1]-Q[j][1],2)+Math.pow(Q[i][2]-Q[j][2],2));\n" +
                "      worst = Math.max(worst, Math.abs(dp-dq));\n" +
                "    }\n" +
                "  return worst;\n" +
                "}\n" +
                // ⭐ body-minus-door: remove each state's own door and compare what is left
                "function __bodyMinusDoor(a, doorA, b, doorB){\n" +
                "  var A = __faces(a), B = __faces(b), ka = {}, kb = {};\n" +
                "  for (var i = 0; i < doorA.length; i++) ka[doorA[i]] = 1;\n" +
                "  for (var i = 0; i < doorB.length; i++) kb[doorB[i]] = 1;\n" +
                "  var RA = [], RB = [];\n" +
                "  for (var i = 0; i < A.length; i++) if (!ka[i]) RA.push(A[i]);\n" +
                "  for (var i = 0; i < B.length; i++) if (!kb[i]) RB.push(B[i]);\n" +
                "  if (RA.length !== RB.length) return -2;\n" +
                "  var worst = 0;\n" +
                "  for (var i = 0; i < RA.length; i++) {\n" +
                "    if (RA[i].mat !== RB[i].mat) return -3;\n" +
                "    var p = RA[i].v, q = RB[i].v;\n" +
                "    if (p.length !== q.length) return -4;\n" +
                "    for (var k = 0; k < p.length; k++)\n" +
                "      for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(p[k][c]-q[k][c]));\n" +
                "  }\n" +
                "  return worst;\n" +
                "}\n" +
                "function __bodyLeft(a, doorA){ return __faces(a).length - doorA.length; }\n");
            return host;
        }

        /// <summary>Where the rollup's face count changes, found by bisection rather than stated.</summary>
        static double TopologyThreshold(IRigScriptHost h, int shut)
        {
            double lo = 0, hi = 1;
            for (int i = 0; i < 40; i++)
            {
                double mid = 0.5 * (lo + hi);
                if ((int)h.EvaluateNumber("__count({rollup:" + N(mid) + "})") == shut) lo = mid;
                else hi = mid;
            }
            return hi;
        }

        static VehicleMeshDef Mesh(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(v.MeshAssetPath);
            Assert.That(def, Is.Not.Null, $"{v.MeshAssetPath} did not load — re-run the vehicle bake.");
            return def;
        }

        // =============================================================================================
        //  1. THE ROLLUP — why she cannot be a pose, and why she can still be a fitting
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Her face COUNT changes, and that is the whole reason she is not a pose.</b> Six faces
        /// stop existing as the curtain rolls into its stack, so a face list claimed shut names
        /// different geometry open — which is exactly what <c>StateProbes</c> exists for.
        /// </summary>
        [Test]
        public void TheRollupChangesHerFaceCount([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);

            Assert.That((int)h.EvaluateNumber("__count({rollup:0})"), Is.EqualTo(t.FacesShut),
                $"{t.Key} no longer builds {t.FacesShut} faces shut.");
            Assert.That((int)h.EvaluateNumber("__count({rollup:1})"), Is.EqualTo(t.FacesOpen),
                $"{t.Key} no longer builds {t.FacesOpen} faces open.");

            Assert.That(t.FacesOpen, Is.Not.EqualTo(t.FacesShut),
                "the rollup's counts agree, so she is a pose after all and should be a plain " +
                "DiscreteStates fitting with one probe — not a per-state claim.");

            Assert.That((int)h.EvaluateNumber("__nMoved({rollup:0},{rollup:1})"), Is.EqualTo(-1),
                "shut and open are now index-comparable. If that is real the rollup no longer needs " +
                "a probe per state.");
        }

        /// <summary>
        /// ⚠️ <b>The declared probes must sit on their own state's side of the topology change</b> —
        /// the property the baker asserts, checked here against the threshold measured off the rig so
        /// a re-stamp that moved it is caught in the fixture too rather than only at bake time.
        /// </summary>
        [Test]
        public void EachRollupProbeIsOnItsOwnStatesSideOfTheChange([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);
            double threshold = TopologyThreshold(h, t.FacesShut);

            VehicleRigFleet.Axis rollup = VehicleRigFleet.Get(t.Key).Axes.Single(a => a.Slot == "Rollup");
            Assert.That(rollup.StateProbes, Is.Not.Null.And.Length.EqualTo(2),
                "the rollup lost her per-state probes; without them she is claimed once and the open " +
                "state takes the shut state's indices out of a shorter list.");

            // Each probe must build the same face count as the state it claims for.
            for (int i = 0; i < 2; i++)
            {
                int stateCount = (int)h.EvaluateNumber("__count(" + rollup.StatePoses[i] + ")");
                int probeCount = (int)h.EvaluateNumber("__count(" + rollup.StateProbes[i] + ")");
                Assert.That(probeCount, Is.EqualTo(stateCount),
                    $"{t.Key} state '{rollup.StateNames[i]}' builds {stateCount} faces but its probe " +
                    $"{rollup.StateProbes[i]} builds {probeCount} — the probe is on the wrong side of " +
                    $"the change at {threshold:0.#####}, so it cannot say which faces are hers.");
            }
        }

        /// <summary>She is not rigid on the shut side either — so even within one topology there is
        /// no pose to interpolate, and two stills is not a shortcut but the only honest option.</summary>
        [Test]
        public void TheRollupIsNotRigidEvenWithinOneTopology([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);
            double threshold = TopologyThreshold(h, t.FacesShut);
            double justBelow = threshold * 0.9;

            double dev = h.EvaluateNumber("__rigidDev({rollup:0},{rollup:" + N(justBelow) + "},null)");
            Assert.That(dev, Is.GreaterThan(0.05),
                $"{t.Key}'s curtain now moves rigidly below her topology change (worst pairwise " +
                $"distance change {dev:0.######} m). If that is real she is a pose and should be " +
                "hinged or slid, not baked at both ends.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE PROOF THAT LETS THE BODY BE BAKED ONCE.</b> Take the door out of each state's
        /// own build and what is left must be the same truck, to the last bit.
        ///
        /// <para>This is the assertion the whole per-state claim rests on. Without it, "the parameter
        /// only moves the door" is an assumption — and a rig change that made the rollup nudge her
        /// own door frame would ship a body frozen in whichever state was extracted first, invisible
        /// in play and permanent once baked. The baker re-proves it at every bake; this proves it
        /// again from the rig, so the two cannot drift.</para>
        /// </summary>
        [Test]
        public void TheBodyIsTheSameBodyInBothRollupStates([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);
            double threshold = TopologyThreshold(h, t.FacesShut);
            string below = "{rollup:" + N(threshold * 0.9) + "}";
            string above = "{rollup:" + N(threshold + (1 - threshold) * 0.5) + "}";

            string shutDoor = "__between({rollup:0}," + below + ")";
            string openDoor = "__between(" + above + ",{rollup:1})";

            Assert.That((int)h.EvaluateNumber(shutDoor + ".length"), Is.EqualTo(t.RollupShut),
                $"{t.Key}'s shut curtain no longer claims {t.RollupShut} faces.");
            Assert.That((int)h.EvaluateNumber(openDoor + ".length"), Is.EqualTo(t.RollupOpen),
                $"{t.Key}'s open curtain no longer claims {t.RollupOpen} faces.");

            // the arithmetic closes: both states leave the same body
            Assert.That((int)h.EvaluateNumber("__bodyLeft({rollup:0}," + shutDoor + ")"),
                Is.EqualTo(t.BodyWithoutDoor));
            Assert.That((int)h.EvaluateNumber("__bodyLeft({rollup:1}," + openDoor + ")"),
                Is.EqualTo(t.BodyWithoutDoor));

            double worst = h.EvaluateNumber(
                "__bodyMinusDoor({rollup:0}," + shutDoor + ",{rollup:1}," + openDoor + ")");

            Assert.That(worst, Is.GreaterThanOrEqualTo(0d),
                worst == -2 ? "the two states leave different numbers of body faces."
              : worst == -3 ? "a body face changed MATERIAL between the states."
              : worst == -4 ? "a body face changed its vertex count between the states."
                            : "the body comparison could not be made.");

            Assert.That(worst, Is.EqualTo(0d),
                $"{t.Key}: working the rollup moved a BODY vertex by {worst:0.#########} m. Opening a " +
                "door is supposed to move the door; here it reshapes the truck, so the body can no " +
                "longer be baked once and shared — and whichever state was extracted first would be " +
                "frozen into it.");
        }

        // =============================================================================================
        //  2. THE LIFTGATE — every face kept, and still not poseable
        // =============================================================================================

        /// <summary>Her face count never moves, which is why she needs NO per-state probe — and is
        /// the one structural difference between the two doors in this file.</summary>
        [Test]
        public void TheLiftgateKeepsEveryFaceAtEveryValue([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);
            foreach (string g in new[] { "0", "0.2", "0.45", "0.6", "0.7", "0.9", "1" })
                Assert.That((int)h.EvaluateNumber("__count({gate:" + g + "})"), Is.EqualTo(t.FacesShut),
                    $"{t.Key}'s gate changed her face count at {g}. If that is real she needs a probe " +
                    "per state exactly as the rollup does.");

            VehicleRigFleet.Axis gate = VehicleRigFleet.Get(t.Key).Axes.Single(a => a.Slot == "Liftgate");
            Assert.That(gate.StateProbes, Is.Null,
                "the liftgate was given per-state probes she does not need — her face list never " +
                "changes, so one probe claims her once.");
        }

        /// <summary>
        /// ⭐ <b>She is a linkage, not a part</b> — and the measurement says so twice over: the whole
        /// gate deforms by more than a metre across her travel, and splitting her by material does
        /// not rescue either half. Her own sidecar calls the mechanism "parallel arms".
        /// </summary>
        [Test]
        public void TheLiftgateIsNotRigid_AndNotSeparableByMaterial([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);

            double whole = h.EvaluateNumber("__rigidDev({gate:0},{gate:1},null)");
            Assert.That(whole, Is.GreaterThan(1.0),
                $"{t.Key}'s gate now moves near-rigidly across her whole travel " +
                $"({whole:0.######} m). If that is real she is a pose and should be hinged.");

            // Neither material is a rigid body on its own through the swing.
            foreach (string mat in new[] { "galv", "iron" })
            {
                double dev = h.EvaluateNumber("__rigidDev({gate:0},{gate:0.45},'" + mat + "')");
                Assert.That(dev, Is.GreaterThan(0.05),
                    $"{t.Key}: her '{mat}' faces now swing as one rigid body ({dev:0.######} m). If " +
                    "that holds through every phase the gate is separable after all and should be " +
                    "modelled as a linkage rather than baked at four stills.");
            }
        }

        /// <summary>
        /// ⚠️ <b>The one rigid thing in either door, pinned so it stays understood.</b> Through
        /// <c>unfold</c> the flip half moves alone — 6 <c>galv</c> faces, distance change EXACTLY 0.
        ///
        /// <para>It is deliberately NOT split out. A part that is rigid in one phase of three and
        /// carried by the linkage in the other two is still the linkage's, and posing it alone would
        /// leave <c>swing</c> and <c>lower</c> unexplained. This test exists so that stays a decision
        /// rather than an oversight — and because the exact zero is what proves the rigidity metric
        /// here can detect rigidity at all, rather than merely failing everything.</para>
        /// </summary>
        [Test]
        public void TheFlipHalfIsExactlyRigidThroughUnfold([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);

            Assert.That((int)h.EvaluateNumber("__nMoved({gate:0.45},{gate:0.7})"), Is.EqualTo(6),
                $"{t.Key}: unfold no longer moves exactly the 6 faces of the flip half.");

            double dev = h.EvaluateNumber("__rigidDev({gate:0.45},{gate:0.7},null)");
            Assert.That(dev, Is.LessThanOrEqualTo(RigidEpsilon),
                $"{t.Key}: the flip half is no longer rigid through unfold ({dev:0.#########} m). " +
                "That zero is also this file's evidence that the metric can see rigidity — without " +
                "it, every 'not rigid' verdict here would be unfalsifiable.");
        }

        // =============================================================================================
        //  3. WHAT SHIPPED — the states, their names, and the handles
        // =============================================================================================

        /// <summary>The baked def carries both doors, with the state counts the measurement asked
        /// for: two for the rollup, four for the gate.</summary>
        [Test]
        public void TheBakedDefCarriesBothDoorsAtTheirMeasuredStateCounts(
            [ValueSource(nameof(Both))] BoxTruck t)
        {
            VehicleMeshDef def = Mesh(t.Key);

            VehicleFitment rollup = def.Wheels.Single(f => f.Slot == "Rollup");
            Assert.That(rollup.Motion, Is.EqualTo(VehicleFitmentMotion.DiscreteStates));
            Assert.That(rollup.StateNames, Is.EqualTo(new[] { "shut", "open" }));
            Assert.That(rollup.StateProps.Length, Is.EqualTo(2));

            VehicleFitment gate = def.Wheels.Single(f => f.Slot == "Liftgate");
            Assert.That(gate.Motion, Is.EqualTo(VehicleFitmentMotion.DiscreteStates));
            Assert.That(gate.StateNames,
                Is.EqualTo(new[] { "stowed", "docked", "unfolded", "grounded" }),
                "the gate's states are the sidecar's own phase names — stowed, then the two ends of " +
                "unfold, then grounded. Renaming them here would decouple them from the art.");
            Assert.That(gate.StateProps.Length, Is.EqualTo(4));

            foreach (HullPropMeshDef p in rollup.StateProps.Concat(gate.StateProps))
                Assert.That(p != null && p.IsUsable(), Is.True,
                    $"{t.Key}: a state prop baked unusable — see its fields.");
        }

        /// <summary>
        /// ⭐ <b>The four gate states ARE the sidecar's phase boundaries</b>, read out of the sidecar
        /// rather than transcribed. If the art re-phased the gate, this is what notices.
        /// </summary>
        [Test]
        public void TheGatesStatePosesAreTheSidecarsOwnPhaseBoundaries(
            [ValueSource(nameof(Both))] BoxTruck t)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(t.Key);
            string json = File.ReadAllText(Full(v.SidecarPath));
            object root = DeckSidecarJson.Parse(json);
            object phases = DeckSidecarJson.Member(DeckSidecarJson.Member(root, "LIFTGATE"), "phases");
            Assert.That(phases, Is.Not.Null, $"{t.Key}'s sidecar publishes no LIFTGATE.phases.");

            // every boundary the sidecar names, in order, deduped
            var bounds = new SortedSet<double>();
            foreach (string phase in new[] { "swing", "unfold", "lower" })
            {
                object t01 = DeckSidecarJson.Member(DeckSidecarJson.Member(phases, phase), "t");
                var list = t01 as System.Collections.IList;
                Assert.That(list, Is.Not.Null.And.Count.EqualTo(2),
                    $"{t.Key}: LIFTGATE.phases.{phase}.t is not a pair.");
                foreach (object o in list) bounds.Add(System.Convert.ToDouble(o));
            }

            VehicleRigFleet.Axis gate = v.Axes.Single(a => a.Slot == "Liftgate");
            Assert.That(gate.StatePoses.Length, Is.EqualTo(bounds.Count),
                $"{t.Key}: the gate is baked at {gate.StatePoses.Length} states but her sidecar names " +
                $"{bounds.Count} distinct phase boundaries ({string.Join(", ", bounds)}). The states " +
                "are supposed to BE the boundaries.");

            double[] want = bounds.ToArray();
            for (int i = 0; i < want.Length; i++)
                Assert.That(gate.StatePoses[i], Is.EqualTo("{gate:" + Trim(want[i]) + "}"),
                    $"{t.Key}: state {i} is posed at {gate.StatePoses[i]}, not at the sidecar's " +
                    $"boundary {want[i]}.");
        }

        static string Trim(double d) =>
            d.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Both handles stand where the art drew them — the rollup's on the centreline aft,
        /// the gate's pendant at the curb-side tail corner. Read off each truck's own sidecar,
        /// because they differ: a 9.6 m truck's tail is not a 6.7 m truck's.</summary>
        [Test]
        public void BothHandlesAreAtTheSidecarsOwnReachPoints([ValueSource(nameof(Both))] BoxTruck t)
        {
            VehicleMeshDef def = Mesh(t.Key);
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(t.Key);
            string json = File.ReadAllText(Full(v.SidecarPath));
            object root = DeckSidecarJson.Parse(json);
            var interacts = DeckSidecarJson.Member(root, "INTERACT") as System.Collections.IList;
            Assert.That(interacts, Is.Not.Null, $"{t.Key}'s sidecar publishes no INTERACT list.");

            foreach ((string id, string slot) in new[] { ("rollup", "Rollup"), ("gate", "Liftgate") })
            {
                object entry = null;
                foreach (object o in interacts)
                    if ((DeckSidecarJson.Member(o, "id") as string) == id) { entry = o; break; }
                Assert.That(entry, Is.Not.Null, $"{t.Key}'s sidecar has no '{id}' interact point.");

                var at = DeckSidecarJson.Member(entry, "reach_point") as System.Collections.IList;
                Assert.That(at, Is.Not.Null.And.Count.GreaterThanOrEqualTo(2),
                    $"{t.Key}: '{id}' publishes no usable reach_point.");

                Assert.That(def.TryGetDoorGroup(id, out VehicleDoorGroup group), Is.True,
                    $"{t.Key}: no door group '{id}' was baked, so the player has nothing to reach for.");
                Assert.That(group.Slots, Contains.Item(slot),
                    $"{t.Key}: group '{id}' does not work the '{slot}' fitting.");
                Assert.That(group.HasReachPoint, Is.True,
                    $"{t.Key}: group '{id}' baked without the reach point its sidecar publishes.");

                Assert.That(group.ReachPointLocal.x,
                    Is.EqualTo((float)System.Convert.ToDouble(at[0])).Within(1e-4f),
                    $"{t.Key}: '{id}' handle is not where her sidecar puts it (x).");
                Assert.That(group.ReachPointLocal.y,
                    Is.EqualTo((float)System.Convert.ToDouble(at[1])).Within(1e-4f),
                    $"{t.Key}: '{id}' handle is not where her sidecar puts it (y).");
            }
        }

        /// <summary>
        /// ⚠️ <b><c>liftgate:false</c> is a PART, not a door.</b> Her sidecar is explicit — "a PART
        /// toggle, not a variant: false removes the tuck-under mechanism entirely" — and it removes
        /// 46 faces on both trucks. It is a spawn-time decision and is deliberately NOT animated;
        /// this pins that it stays one, so nobody later wires it to the gate handle and gives the
        /// player a lever that makes a mechanism vanish.
        /// </summary>
        [Test]
        public void TheLiftgatePartToggleIsNotADoor([ValueSource(nameof(Both))] BoxTruck t)
        {
            using IRigScriptHost h = Host(t.Key);
            int fitted = (int)h.EvaluateNumber("__count({})");
            int unfitted = (int)h.EvaluateNumber("__count({liftgate:false})");
            Assert.That(fitted - unfitted, Is.EqualTo(46),
                $"{t.Key}: dropping the liftgate part no longer removes 46 faces.");

            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(t.Key);
            Assert.That(v.Axes.Any(a => a.StatePoses != null &&
                                        a.StatePoses.Any(p => p.Contains("liftgate"))), Is.False,
                $"{t.Key}: a fitting is posed on 'liftgate'. That is the PART toggle — animating it " +
                "makes the whole mechanism appear and disappear rather than opening anything.");
        }
    }
}
