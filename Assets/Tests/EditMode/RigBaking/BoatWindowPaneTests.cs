using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The windows the fleet burns still are the windows her rigs draw</b> (owner's ruling,
    /// 2026-09-03: a glow is confined to its space, and an interior's reaches the outside only
    /// through the windows).
    ///
    /// <para><b>What this exists to catch.</b> <see cref="HullMeshDef.Panes"/> is 236 rectangles over
    /// twenty-seven hulls, derived from each rig's published HOUSE glazing and written into the defs
    /// by <see cref="BoatWindowProbe"/>. Nothing in the mesh bake writes it and nothing at run time
    /// checks it — so a rig revision that moves a windscreen, or a def edited by hand, would leave
    /// every hull lighting a rectangle of empty air, at every heading, with no error anywhere. The
    /// join below re-derives every pane from the rig and compares.</para>
    ///
    /// <para><b>And it is a JOIN, not a snapshot.</b> A test that pinned the numbers would go red for
    /// the right reason and then be fixed by copying the new numbers in, which proves nothing. This
    /// one names the rig as the authority: when it goes red the answer is to re-run the probe's
    /// writer, and the only way to make it green with a wrong def is to break the probe as well.</para>
    /// </summary>
    public class BoatWindowPaneTests
    {
        static List<BoatWindowProbe.HullWindows> _measured;

        /// <summary>Measured ONCE for the class: every rig file is loaded into a V8 host, and paying
        /// that per test would turn a two-second class into a minute.</summary>
        static List<BoatWindowProbe.HullWindows> Measured => _measured ??= BoatWindowProbe.Measure();

        /// <summary>The hulls with no room to light. Not a gap — five open boats, and a dory with a lit
        /// window would be the defect.</summary>
        static readonly string[] OpenBoats =
            { "dory", "punt", "consoleSkiff", "sportSkiff", "sportSkiffMk2" };

        // -------------------------------------------------------------------------------------------
        //  the join
        // -------------------------------------------------------------------------------------------

        [Test]
        public void EveryDefsPanesAreExactlyWhatHerRigPublishes()
        {
            var wrong = new List<string>();

            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(w.MeshAssetPath);
                if (def == null)
                {
                    wrong.Add($"{w.Key}: no def at {w.MeshAssetPath}");
                    continue;
                }

                HullPane[] have = def.Panes ?? System.Array.Empty<HullPane>();
                if (have.Length != w.Panes.Count)
                {
                    wrong.Add($"{w.Key}: def carries {have.Length} panes, her rig publishes " +
                              $"{w.Panes.Count}");
                    continue;
                }

                for (int i = 0; i < have.Length; i++)
                {
                    HullPane a = have[i], b = w.Panes[i];
                    if (a.Wall != b.Wall)
                        wrong.Add($"{w.Key}[{i}]: def says {a.Wall}, rig says {b.Wall}");
                    else if (Vector3.Distance(a.CentreMetres, b.CentreMetres) > 1e-4f)
                        wrong.Add($"{w.Key}[{i}] {a.Wall}: centre {a.CentreMetres} vs rig {b.CentreMetres}");
                    else if (Vector3.Distance(a.HalfAcrossMetres, b.HalfAcrossMetres) > 1e-4f ||
                             Vector3.Distance(a.HalfUpMetres, b.HalfUpMetres) > 1e-4f)
                        wrong.Add($"{w.Key}[{i}] {a.Wall}: size/attitude has drifted from her rig");
                }
            }

            Assert.IsEmpty(wrong,
                "these hulls' windows no longer match the rig they were read from. Re-run " +
                "'Hidden Harbours/Rig Baking/Write: boat windows into the hull defs' — do NOT hand-edit " +
                "the def, and do not relax this test:\n  " + string.Join("\n  ", wrong));
        }

        // -------------------------------------------------------------------------------------------
        //  what has to be true of any pane, however it was derived
        // -------------------------------------------------------------------------------------------

        [Test]
        public void EveryPaneFacesOUTOfItsOwnRoom()
        {
            // ⭐ THE ONE THAT MATTERS MOST, AND THE ONE THAT WOULD FAIL SILENTLY. HullPane derives its
            // outward direction as up x across, so the HANDEDNESS of those two vectors is what decides
            // which way a window throws its light. Get one wall's pair the wrong way round and that
            // wall's spill points INTO the cabin: nothing is drawn, nothing errors, and the boat just
            // looks a bit dark on one side. The room's own centre is the referee — every wall of a box
            // faces away from the middle of it.
            var wrong = new List<string>();

            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                if (w.Panes.Count == 0) continue;

                Vector3 middle = Vector3.zero;
                foreach (HullPane p in w.Panes) middle += p.CentreMetres;
                middle /= w.Panes.Count;

                foreach (HullPane p in w.Panes)
                {
                    float outwardness = Vector3.Dot(p.Outward, p.CentreMetres - middle);
                    if (outwardness <= 0f)
                        wrong.Add($"{w.Key} {p.Wall} at {p.CentreMetres}: outward {p.Outward} points " +
                                  $"back toward the room centre {middle} (dot {outwardness:F3})");
                }
            }

            Assert.IsEmpty(wrong,
                "these windows throw their light INTO the cabin. The cause is a flipped across/up " +
                "pair in BoatWindowProbe — outward is up x across, and a wall seen from OUTSIDE runs " +
                "the other way along its own axis:\n  " + string.Join("\n  ", wrong));
        }

        [Test]
        public void SideWindowsComeInMirrorTwins()
        {
            // A deckhouse is symmetric about the centreline and every rig publishes ONE side run for
            // BOTH sides. A port pane without its starboard twin means the loop that mirrors them has
            // lost a case; twins that are not mirrored means the handedness above is wrong on one side
            // only, which the outward test can miss when the room centre happens to sit off-centre.
            var wrong = new List<string>();

            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                var star = new List<HullPane>();
                var port = new List<HullPane>();
                foreach (HullPane p in w.Panes)
                {
                    if (p.Wall == HullWall.Starboard) star.Add(p);
                    if (p.Wall == HullWall.Port) port.Add(p);
                }

                if (star.Count != port.Count)
                {
                    wrong.Add($"{w.Key}: {star.Count} starboard windows against {port.Count} to port");
                    continue;
                }

                for (int i = 0; i < star.Count; i++)
                {
                    HullPane s = star[i], p = port[i];
                    if (Mathf.Abs(s.CentreMetres.x + p.CentreMetres.x) > 1e-4f ||
                        Mathf.Abs(s.CentreMetres.y - p.CentreMetres.y) > 1e-4f ||
                        Mathf.Abs(s.CentreMetres.z - p.CentreMetres.z) > 1e-4f)
                        wrong.Add($"{w.Key}[{i}]: {s.CentreMetres} is not the mirror of {p.CentreMetres}");
                    else if (Vector3.Distance(s.Outward, -p.Outward) > 1e-4f)
                        wrong.Add($"{w.Key}[{i}]: outward {s.Outward} and {p.Outward} are not opposite");
                }
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void NoPaneIsDegenerateAndNoneIsAWall()
        {
            // A window is a window. Anything under 15 cm is a fitting somebody mistook for glazing;
            // anything over 2 m is a wall, and lighting a wall is the blob this lane retired.
            var wrong = new List<string>();

            foreach (BoatWindowProbe.HullWindows w in Measured)
                foreach (HullPane p in w.Panes)
                {
                    if (!p.IsUsable)
                        wrong.Add($"{w.Key} {p.Wall} at {p.CentreMetres} is degenerate — it would be " +
                                  "silently skipped everywhere, so the room would simply be darker");
                    else if (p.WidthMetres < 0.15f || p.HeightMetres < 0.15f)
                        wrong.Add($"{w.Key} {p.Wall}: {p.WidthMetres:F2} x {p.HeightMetres:F2} m is too " +
                                  "small to be a window");
                    else if (p.WidthMetres > 2f || p.HeightMetres > 2f)
                        wrong.Add($"{w.Key} {p.Wall}: {p.WidthMetres:F2} x {p.HeightMetres:F2} m is a " +
                                  "wall, not a window");
                }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void AnOpenBoatHasNoWindowsAndSaysSo()
        {
            foreach (string key in OpenBoats)
            {
                BoatWindowProbe.HullWindows w = Measured.Find(h => h.Key == key);
                Assert.IsNotNull(w, $"{key} is not in the fleet catalog any more");
                Assert.IsEmpty(w.Panes, $"{key} is an open boat — she has no room to light");
                Assert.IsNotNull(w.Refusal,
                    $"{key} has no windows and no REASON. Absence must be data with a sentence " +
                    "attached, never a silent empty list that reads the same as a bug.");
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the counts, pinned — so a rig revision is NOTICED
        // -------------------------------------------------------------------------------------------

        [Test]
        public void TheFleetsWindowCountIsWhatWasMeasured()
        {
            // Measured 2026-09-03 against the rigs at main 3e22460b, and reproduced independently by a
            // standalone V8 harness before any of this shipped. It is pinned NOT because the number
            // matters but because a change to it means an art drop moved some glazing — which is a
            // thing somebody should look at, not something that should quietly re-light itself.
            int panes = 0, hulls = 0, bridge = 0;
            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                panes += w.Panes.Count;
                bridge += w.UnlitPanes.Count;
                if (w.Panes.Count > 0) hulls++;
            }

            Assert.AreEqual(25, hulls,
                "hulls with a lit room — 27 have a glazed room and TWO of them (the sport fishers) " +
                "are refused because their record does not describe the side their glass is on");
            Assert.AreEqual(218, panes,
                "lit panes across the fleet. If an art drop legitimately moved some glazing, re-run " +
                "the probe's writer and update this number in the same commit.");
            Assert.AreEqual(72, bridge,
                "bridge panes measured but deliberately left dark — this lane confines an existing " +
                "glow and lights no new room. Lighting the bridges is a data change and an owner call.");
        }

        [Test]
        public void TheTwoHullsWhoseRecordOutrunsTheirGeometryAreRefusedOutLoud()
        {
            // ⭐ THE PROBE'S OWN VALIDATION, PINNED — because a refusal that quietly stopped firing
            // would ship a lit window hanging half a metre off the boat, and nothing else here would
            // see it. The sport fishers' accommodation publishes a FLAT hx for a side that curves in
            // plan (her portholes are drawn on a profile the record does not carry), so panes placed
            // at ±hx float 0.32–0.51 m outboard of her actual side.
            //
            // ⚠️ And a refusal is not darkness: BoatLamps falls these two back to the disc they wear
            // today (BoatLamps.HasWindows). Two hulls keeping yesterday's look is the cost, it is
            // named, and the fix is upstream.
            foreach (string key in new[] { "sportFisherConvertible", "sportFisherSkybridge" })
            {
                BoatWindowProbe.HullWindows w = Measured.Find(h => h.Key == key);
                Assert.IsNotNull(w, $"{key} is not in the fleet catalog any more");
                Assert.IsEmpty(w.Panes, $"{key}'s windows cannot be placed on her, so none are written");
                Assert.IsNotNull(w.Refusal, $"{key} was dropped with no reason given");
                StringAssert.Contains("UPSTREAM ASK", w.Refusal,
                    "a refusal caused by a gap in a rig's published record has to name the ask, or " +
                    "nobody upstream ever hears about it");
            }
        }

        [Test]
        public void TheOnHullToleranceHasRoomOnBothSides()
        {
            // The bar is set from a measurement, so the measurement is what is pinned — not the bar.
            // If a rig revision moved either edge toward it, this goes red before a hull silently
            // starts (or stops) being refused.
            float worstKept = 0f, bestRefused = float.MaxValue;
            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                if (float.IsNaN(w.WorstCornerMetres)) continue;
                if (w.Panes.Count > 0) worstKept = Mathf.Max(worstKept, w.WorstCornerMetres);
                else if (w.Refusal != null && w.Refusal.Contains("UPSTREAM ASK"))
                    bestRefused = Mathf.Min(bestRefused, w.WorstCornerMetres);
            }

            Assert.Less(worstKept, BoatWindowProbe.OnHullToleranceMetres - 0.03f,
                        $"the worst honestly-placed corner is {worstKept:F3} m and the tolerance is " +
                        $"{BoatWindowProbe.OnHullToleranceMetres:F2} m — too close to call. Re-read the " +
                        "probe's printed margin before moving either.");
            Assert.Greater(bestRefused, BoatWindowProbe.OnHullToleranceMetres + 0.01f,
                           $"the best refused corner is {bestRefused:F3} m — a hull is being refused " +
                           "by a hair, which is not a measurement, it is a coin toss.");
        }

        [Test]
        public void TheCapeIsTheEightPanesHerRigDraws()
        {
            // The hull the owner reviews at, spelled out — three in the raked screen, two lights a
            // side, one small light in the aft wall. If the schema decode ever loses a family the
            // fleet total could still land by accident; this one cannot.
            BoatWindowProbe.HullWindows cape = Measured.Find(h => h.Key == "capeIslander");
            Assert.IsNotNull(cape);

            int front = 0, side = 0, aft = 0;
            foreach (HullPane p in cape.Panes)
            {
                if (p.Wall == HullWall.Front) front++;
                else if (p.Wall == HullWall.Aft) aft++;
                else side++;
            }
            Assert.AreEqual(3, front, "her three-pane raked windscreen");
            Assert.AreEqual(4, side, "two lights a side");
            Assert.AreEqual(1, aft, "the small light in the aft wall, outboard of the sliding door");

            // ⭐ AND HER SCREEN RAKES FORWARD AND DOWN, which is the whole reason the pane carries an
            // UP vector instead of a height. Her rig computes the same normal from (eaveZ - zBot,
            // -(yTop - yBot)) normalised = (0, 0.9707, -0.2405); this is that number, arrived at from
            // the published HOUSE record rather than from her private draw code.
            foreach (HullPane p in cape.Panes)
            {
                if (p.Wall != HullWall.Front) continue;
                Assert.AreEqual(0.9707f, p.Outward.y, 1e-3f, "her brow leans FORWARD");
                Assert.AreEqual(-0.2405f, p.Outward.z, 1e-3f, "and DOWN — a Cape Islander's raked brow");
            }
        }

        [Test]
        public void ARecliningScreenLeansTheOtherWay()
        {
            // The negative control on the test above. If the rake maths were hard-coded to the cape's
            // sign, every hull would agree with her and nothing would show it. The lobster boat's
            // screen RECLINES (her rig's front.yTop is aft of yBot), so her windscreen must face
            // forward and UP — the opposite z from the cape's, out of the same three published fields.
            BoatWindowProbe.HullWindows lob = Measured.Find(h => h.Key == "lobsterBoat");
            Assert.IsNotNull(lob);

            bool sawFront = false;
            foreach (HullPane p in lob.Panes)
            {
                if (p.Wall != HullWall.Front) continue;
                sawFront = true;
                Assert.Greater(p.Outward.y, 0.5f, "her screen still faces forward");
                Assert.Greater(p.Outward.z, 0.1f,
                    "and UP — she reclines where the cape rakes forward, and the derivation has to " +
                    "get both from the same three numbers");
            }
            Assert.IsTrue(sawFront, "the lobster boat publishes a front screen");
        }

        // -------------------------------------------------------------------------------------------
        //  the independent oracle: a window has to be ON the hull
        // -------------------------------------------------------------------------------------------

        [Test]
        public void EveryWindowSitsOnHerOwnHull()
        {
            // ⭐ THE SECOND, INDEPENDENT COMPUTATION, RUN AGAINST WHAT ACTUALLY SHIPS. The probe now
            // applies this same rule when it writes (see BoatWindowProbe.OnHullToleranceMetres — it is
            // what refuses the sport fishers), so this is not re-deriving the probe's own answer: it
            // is checking the DEFS, which a hand edit or a stale write could put out of step with it.
            //
            // Her BAKED MESH is the referee: the glass is real geometry there, emitted by the rig's own
            // rasteriser through a different path entirely. So every pane corner in a shipped def must
            // land near a real vertex of the hull it belongs to.
            //
            // It is also what catches the trap this probe was written around: `hxAt` takes Y on the
            // wheelhouse rigs and Z on the ship rigs, and handing it the wrong one does not throw — it
            // returns a plausible half-width for the wrong reason and floats every side window off her
            // wall. Nothing in the join test above would notice, because the probe would be wrong on
            // both sides of it.
            const float Tolerance = BoatWindowProbe.OnHullToleranceMetres;

            var wrong = new List<string>();
            var checkedHulls = 0;

            foreach (BoatWindowProbe.HullWindows w in Measured)
            {
                if (w.Panes.Count == 0) continue;
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(w.MeshAssetPath);
                if (def == null || def.Mesh == null) continue;

                Vector3[] verts = def.Mesh.vertices;
                if (verts.Length == 0) continue;
                checkedHulls++;

                // Sanity gate FIRST: if mesh vertices were not in her rig's own metres, every distance
                // below would be meaningless and the failure would read as "the probe is wrong".
                Bounds b = def.Mesh.bounds;
                Assert.Less(Mathf.Abs(b.center.x), 1f,
                            $"{w.Key}: her mesh is not centred on her centreline — vertices are not in " +
                            "rig metres and this test is measuring the wrong space");

                foreach (HullPane p in w.Panes)
                {
                    float worst = 0f;
                    for (int ax = -1; ax <= 1; ax += 2)
                        for (int up = -1; up <= 1; up += 2)
                            worst = Mathf.Max(worst, NearestVertex(verts, p.Corner(ax, up)));

                    if (worst > Tolerance)
                        wrong.Add($"{w.Key} {p.Wall} at {p.CentreMetres}: its furthest corner is " +
                                  $"{worst:F3} m from any vertex of her hull");
                }
            }

            Assert.Greater(checkedHulls, 20,
                           "the baked meshes could not be loaded, so this test proved nothing");
            Assert.IsEmpty(wrong,
                "these windows are not on the boat. Suspect the hxAt argument (Y on a wheelhouse, Z on " +
                "a ship) before suspecting the rig:\n  " + string.Join("\n  ", wrong));
        }

        static float NearestVertex(Vector3[] verts, Vector3 at)
        {
            float best = float.MaxValue;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = (verts[i] - at).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}
