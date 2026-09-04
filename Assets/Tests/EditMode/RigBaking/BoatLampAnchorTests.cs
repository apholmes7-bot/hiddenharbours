using System;
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
    /// <b>The shipped lamp positions still are the ones the rigs draw</b> (ADR 0016) — for the whole
    /// fleet now, not one hull.
    ///
    /// <para><b>What this exists to catch.</b> <see cref="HullMeshDef.Lamps"/> is hand-authored data:
    /// boat-local triples measured out of each hull's own rig. Nothing in the mesh bake writes it and
    /// nothing at run time checks it, so a rig revision that moves a sidelight — or a typo in a def —
    /// would leave the lamps burning confidently in the wrong place, at every heading, with no error
    /// anywhere. This is the join: it takes the numbers the game actually ships, pushes them through
    /// the RUNTIME's own projection, and demands they land on the pixels each RIG's own
    /// <c>navMounts(dir)</c> reports, at all eight facings.</para>
    ///
    /// <para><b>Why that is a real oracle and not two transcriptions agreeing.</b> The two sides share
    /// no code and no constants. A rig computes its answer in JavaScript from its own stations and its
    /// own <c>camBasis</c>/<c>projVert</c>; the game computes its answer in C# from
    /// <see cref="IsoFacetMath.RigToWorld"/>, the def's pivot and the def's pixels-per-metre. Three
    /// separate things therefore go red here: a def drifting, a rig moving its lamps, and the
    /// runtime's handedness or elevation convention changing under the data.</para>
    ///
    /// <para><b>The Cape Islander is the CONTROL.</b> Her six shipped rows are #686's own measurement,
    /// made by a different lane by a different method, and PR 2 does not touch them — they are pinned
    /// here value by value. PR 2's fleet-wide derivation reproduced every one of them to 1.8e-15 m, so
    /// if the two ever disagree it is the derivation that is wrong.</para>
    /// </summary>
    public class BoatLampAnchorTests
    {
        const string CapeDefPath =
            "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";

        // The rig's own name for each nav lamp, beside the kind the game files it under. The cabin
        // glow, the anchor light and the searchlight are deliberately absent: no rig publishes a
        // projected anchor for any of them (they are placed against the published HOUSE box, or
        // hoisted at the masthead), so there is nothing here for them to agree with — the
        // probe-agreement test below is what holds those.
        static readonly (HullLampKind Kind, string RigName)[] NavPairs =
        {
            (HullLampKind.PortSidelight,      "port"),
            (HullLampKind.StarboardSidelight, "star"),
            (HullLampKind.SternLight,         "stern"),
            (HullLampKind.Masthead,           "mast"),
            (HullLampKind.RangeLight,         "range"),
        };

        // Sub-pixel. The two paths are different languages doing the same double-precision arithmetic,
        // so they agree to floating noise; a tenth of a pixel is far tighter than any real drift and
        // far looser than the noise.
        const double TolerancePx = 0.1;

        // Ten microns. The probe snaps its inversion's dust to the axis before the numbers are
        // authored, so a committed def and a fresh derivation agree to far better than this.
        const float ProbeToleranceMetres = 1e-5f;

        static HullMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
            Assert.IsNotNull(def, $"the hull-mesh def must load from {path}");
            return def;
        }

        static HullMeshDef Cape() => Load(CapeDefPath);

        static bool TryLamp(HullMeshDef def, HullLampKind kind, out HullLamp found)
        {
            foreach (HullLamp l in def.Lamps)
                if (l.Kind == kind) { found = l; return true; }
            found = default;
            return false;
        }

        static HullLamp LampOf(HullMeshDef def, HullLampKind kind)
        {
            if (TryLamp(def, kind, out HullLamp l)) return l;
            Assert.Fail($"'{def.Id}' declares no {kind} lamp");
            return default;
        }

        /// <summary>Every hull in the mesh fleet that declares lamps, with her def — grouped by rig
        /// FILE, so the eighteen lobster variants share one V8 load.</summary>
        static Dictionary<string, List<(FleetHull Hull, HullMeshDef Def)>> LampedHullsByRig()
        {
            var byRig = new Dictionary<string, List<(FleetHull, HullMeshDef)>>();
            foreach (FleetHull hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null || def.Lamps == null || def.Lamps.Length == 0) continue;
                if (!byRig.TryGetValue(hull.ScriptPath, out var list))
                    byRig[hull.ScriptPath] = list = new List<(FleetHull, HullMeshDef)>();
                list.Add((hull, def));
            }
            return byRig;
        }

        // ⚠️ ScopeOr, never string concatenation — HullScope carries no leading dot, and the rule
        // "the global unless this extraction names an object" already has one home.
        static string ScopedGlobal(FleetHull hull) =>
            hull.Extraction != null ? hull.Extraction.ScopeOr(hull.GlobalName) : hull.GlobalName;

        static string OptsArg(FleetHull hull) =>
            hull.Extraction != null && !string.IsNullOrEmpty(hull.Extraction.ViewOptions)
                ? ", " + hull.Extraction.ViewOptions : "";

        // ---- the fleet declares her lamps --------------------------------------------------------------

        [Test]
        public void EveryHullWhoseRigPublishesNavMountsDeclaresHerLamps()
        {
            var missing = new List<string>();
            int counted = 0;

            foreach (FleetHull hull in HullMeshFleet.Hulls)
            {
                string full = Path.Combine(RigCatalog.RepoRoot, hull.ScriptPath);
                if (!File.Exists(full)) continue;
                // Reading the file is enough to ask the question and far cheaper than a V8 host per
                // hull: a rig either has the function or it does not.
                if (!File.ReadAllText(full).Contains("navMounts")) continue;

                counted++;
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null) { missing.Add(hull.Key + " (def missing)"); continue; }
                if (def.Lamps == null || def.Lamps.Length == 0) missing.Add(hull.Key);
            }

            Assert.AreEqual(27, counted,
                "nine rigs publish navMounts and they dress twenty-seven hulls between them (the " +
                "lobster generator alone makes eighteen). A different number means a rig gained or " +
                "lost nav mounts, and the fleet's lamp table needs re-deriving — run the probe.");
            CollectionAssert.IsEmpty(missing,
                "every hull whose rig says where her lamps are must declare them. Missing: " +
                string.Join(", ", missing) + ". Run Hidden Harbours / Rig Baking / Probe: boat lamp " +
                "anchors and copy the rows in. (Absence IS data for the open boats — the dory, punt, " +
                "console, skiffs and zodiacs publish no mounts and carry no lamps — but those rigs " +
                "are not counted here at all.)");
        }

        // ---- THE PROOF: the def's numbers land where the rig draws them ---------------------------------

        [Test]
        public void EveryDeclaredNavLampLandsWhereHerRigDrawsIt_AtEveryFacing()
        {
            double worst = 0;
            string worstWhere = "";
            int hulls = 0, checks = 0;

            foreach (var pair in LampedHullsByRig())
            {
                string full = Path.Combine(RigCatalog.RepoRoot, pair.Key);
                Assert.IsTrue(File.Exists(full), $"the rig must be on disk at {pair.Key}");

                using var host = new V8RigScriptHost();
                host.Execute(File.ReadAllText(full));

                foreach ((FleetHull hull, HullMeshDef def) in pair.Value)
                {
                    string g = ScopedGlobal(hull);
                    string arg = OptsArg(hull);
                    hulls++;

                    Assert.IsTrue(host.EvaluateBool($"typeof {g}.navMounts === 'function'"),
                        $"{g} must still publish navMounts(dir) — it is the only statement {hull.Key}'s " +
                        "rig makes about where her lamps are, and this whole test is the join to it. If " +
                        "a revision drops it, the def's numbers are unmoored and somebody has to " +
                        "re-measure them rather than delete this test.");

                    // The rig and the game must also still agree about the CELL these pixels are in.
                    // Read from the def, because the def is what the runtime actually draws with; a
                    // pivot that had drifted would move every lamp together and could otherwise hide
                    // inside the comparison below.
                    Assert.AreEqual(host.EvaluateNumber($"{g}.PX"), def.PxPerMetre, 1e-9,
                                    $"{hull.Key}: the def's pixels-per-metre is the rig's own");
                    Assert.AreEqual(host.EvaluateNumber($"{g}.pivot.x"), def.PivotPx.x, 1e-6,
                                    $"{hull.Key}: the def's pivot x is the rig's own");
                    Assert.AreEqual(host.EvaluateNumber($"{g}.pivot.y"), def.PivotPx.y, 1e-6,
                                    $"{hull.Key}: the def's pivot y is the rig's own");

                    int facings = (int)host.EvaluateNumber($"{g}.DIRS");
                    Assert.AreEqual(8, facings, $"{hull.Key} is an eight-facing rig");

                    for (int d = 0; d < facings; d++)
                    {
                        // The runtime's rig-to-world map for this facing. dirUnits is the rig's own dir
                        // argument (1 unit = 45 degrees), which is exactly what navMounts is handed
                        // below, so the two sides are asked about the same heading rather than about
                        // two conventions that happen to line up at north.
                        Matrix4x4 m = IsoFacetMath.RigToWorld(d, def.ElevationDeg);

                        foreach ((HullLampKind kind, string rigName) in NavPairs)
                        {
                            if (!TryLamp(def, kind, out HullLamp lamp)) continue;
                            if (!host.EvaluateBool($"{g}.navMounts({d}{arg}).{rigName} != null")) continue;

                            // Rig metres -> world (screen x/y up, z depth) -> the rig's own cell pixels,
                            // whose origin is the cell's TOP-LEFT and whose y runs DOWN. That flip is
                            // the only convention this test asserts on its own behalf, and getting it
                            // wrong would show up as a mirrored error at every facing rather than as
                            // agreement.
                            Vector3 w = m.MultiplyPoint3x4(lamp.RigLocalMetres);
                            double px = def.PivotPx.x + w.x * def.PxPerMetre;
                            double py = def.PivotPx.y - w.y * def.PxPerMetre;

                            double rx = host.EvaluateNumber($"{g}.navMounts({d}{arg}).{rigName}.x");
                            double ry = host.EvaluateNumber($"{g}.navMounts({d}{arg}).{rigName}.y");
                            checks++;

                            double err = Math.Max(Math.Abs(px - rx), Math.Abs(py - ry));
                            if (err > worst) { worst = err; worstWhere = $"{hull.Key} {kind} at facing {d}"; }

                            Assert.AreEqual(rx, px, TolerancePx,
                                $"{hull.Key} {kind} at facing {d}: the def puts her at cell x {px:F4}, " +
                                $"her rig draws her at {rx:F4}. Either the def's boat-local triple has " +
                                "drifted or the rig has moved the lamp — re-run the probe and copy the " +
                                "row in, do not nudge the def until the numbers meet.");
                            Assert.AreEqual(ry, py, TolerancePx,
                                $"{hull.Key} {kind} at facing {d}: the def puts her at cell y {py:F4}, " +
                                $"her rig draws her at {ry:F4}. See the x message.");
                        }
                    }
                }
            }

            Assert.Greater(hulls, 20, "the fleet's lamped hulls must actually have been swept");
            Debug.Log($"[boat-lamps] {hulls} hulls, {checks} lamp/facing joins agree with their rigs; " +
                      $"worst disagreement {worst:E3} px ({worstWhere}).");
        }

        // ---- the committed rows are what a fresh derivation says ---------------------------------------

        [Test]
        public void EveryCommittedLampRowIsWhatTheProbeDerivesFromTheRig()
        {
            var complaints = new List<string>();
            int compared = 0;

            foreach (BoatLampAnchorProbe.HullLamps measured in BoatLampAnchorProbe.Measure())
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(measured.MeshAssetPath);
                if (def == null || def.Lamps == null || def.Lamps.Length == 0) continue;

                foreach (BoatLampAnchorProbe.Station s in measured.Stations)
                {
                    Assert.LessOrEqual(s.ResidualPx, BoatLampAnchorProbe.ResidualLimitPx,
                        $"{measured.Key} {s.Kind}: the inversion left a residual of {s.ResidualPx:E3} px. " +
                        "That is not a measurement — the projection is no longer affine in the lamp's " +
                        "position (a rig that heaves its nav mounts, or a changed camera), and the " +
                        "triple it yields is a best fit rather than the answer.");

                    if (!TryLamp(def, s.Kind, out HullLamp shipped))
                    {
                        complaints.Add($"{measured.Key}: the probe derives a {s.Kind} the def does not declare");
                        continue;
                    }
                    compared++;
                    float err = Vector3.Distance(shipped.RigLocalMetres, s.RigLocalMetres);
                    if (err > ProbeToleranceMetres)
                        complaints.Add($"{measured.Key} {s.Kind}: def has {shipped.RigLocalMetres:F6}, " +
                                       $"the rig now says {s.RigLocalMetres:F6} ({err:F6} m apart)");
                }

                // And nothing declared that the probe does not derive — a row somebody typed by hand,
                // or one left behind by a rig that stopped publishing the station.
                foreach (HullLamp l in def.Lamps)
                {
                    bool derived = false;
                    foreach (BoatLampAnchorProbe.Station s in measured.Stations)
                        if (s.Kind == l.Kind) { derived = true; break; }
                    if (!derived)
                        complaints.Add($"{measured.Key}: the def declares a {l.Kind} the probe does not derive");
                }
            }

            Assert.Greater(compared, 150,
                "the sweep must actually have compared the fleet's rows (27 hulls x 6-8 lamps each)");
            CollectionAssert.IsEmpty(complaints,
                "the committed lamp rows have drifted from what the rigs say. Re-run Hidden Harbours / " +
                "Rig Baking / Probe: boat lamp anchors and copy the rows in.\n  " +
                string.Join("\n  ", complaints));
        }

        // ---- the CONTROL: the cape's shipped rows, value by value ---------------------------------------

        [Test]
        public void TheCapesSixShippedLampsAreExactlyWhatPr1Measured()
        {
            HullMeshDef def = Cape();

            // #686's own numbers, transcribed from the def as it shipped on 2026-08-29. PR 2 measured
            // the whole fleet by a different method and reproduced every one of these to 1.8e-15 m;
            // they are pinned here verbatim so a change to the derivation cannot quietly redefine the
            // hull the intro's arrival is judged on.
            AssertLampIs(def, HullLampKind.PortSidelight,      new Vector3(-0.3024f, 5.752f, 3.224f));
            AssertLampIs(def, HullLampKind.StarboardSidelight, new Vector3(0.3024f, 5.752f, 3.224f));
            AssertLampIs(def, HullLampKind.SternLight,         new Vector3(0f, -6.35f, 1.49f));
            AssertLampIs(def, HullLampKind.Masthead,           new Vector3(0f, 2.36f, 4.46f));
            AssertLampIs(def, HullLampKind.CabinGlow,          new Vector3(0f, 1.52f, 2.21f));
            AssertLampIs(def, HullLampKind.Spotlight,          new Vector3(0f, 2.4f, 3.1f));
        }

        static void AssertLampIs(HullMeshDef def, HullLampKind kind, Vector3 expected)
        {
            HullLamp l = LampOf(def, kind);
            Assert.AreEqual(expected.x, l.RigLocalMetres.x, 1e-6f, $"the cape's {kind} x");
            Assert.AreEqual(expected.y, l.RigLocalMetres.y, 1e-6f, $"the cape's {kind} y");
            Assert.AreEqual(expected.z, l.RigLocalMetres.z, 1e-6f, $"the cape's {kind} z");
        }

        [Test]
        public void TheCapeDeclaresHerFourNavLampsHerCabinHerSearchlightAndAnAnchorLight()
        {
            HullMeshDef def = Cape();
            var kinds = new List<HullLampKind>();
            foreach (HullLamp l in def.Lamps) kinds.Add(l.Kind);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    HullLampKind.PortSidelight, HullLampKind.StarboardSidelight,
                    HullLampKind.SternLight, HullLampKind.Masthead,
                    HullLampKind.CabinGlow, HullLampKind.Spotlight,
                    HullLampKind.AnchorLight,
                },
                kinds,
                "the cape is the hull the intro's arrival is run on, and the owner's ruling names all " +
                "three of cabin light, navigation lights and spotlight — so those six declarations have " +
                "to be on her def or the demo is short one of the things it promises. The seventh is " +
                "PR 2's anchor light, which she needs because one of the seven boats moored at Nine " +
                "Mile Creek is a Cape Islander and she would otherwise be the one dark hull on the wall.");

            // A duplicate would build two lights at one point and read as one brighter lamp — quiet,
            // and exactly the kind of thing a table edited by hand grows.
            CollectionAssert.AllItemsAreUnique(kinds, "one lamp of each kind, not two");
        }

        [Test]
        public void HerAnchorLightWasAppendedLastSoNothingElseMoved()
        {
            // ⚠️ NOT cosmetic ordering. BoatLamps makes one child light per lamp IN ARRAY ORDER, and
            // SceneLight's deterministic flicker is seeded from the child's SIBLING INDEX — the trap
            // that cost #702 five false reds. Inserting the anchor light anywhere before the cabin glow
            // would give the glow a new seed, a different 1-LSB flicker offset, and a Cape Islander
            // whose shipped pixels no longer match. Appended last, every earlier lamp keeps the index
            // it had — and the anchor light is disabled while she is under way anyway.
            HullMeshDef def = Cape();
            Assert.AreEqual(HullLampKind.AnchorLight, def.Lamps[def.Lamps.Length - 1].Kind,
                            "the anchor light is the LAST row on the cape's def");
            for (int i = 0; i < def.Lamps.Length - 1; i++)
                Assert.AreNotEqual(HullLampKind.AnchorLight, def.Lamps[i].Kind);

            // And the cabin glow is still where it was: fifth row, and therefore the fifth light built
            // (the searchlight builds none), which is the index its flicker seed is derived from.
            Assert.AreEqual(HullLampKind.CabinGlow, def.Lamps[4].Kind,
                            "the cabin glow is still the fifth row on her def");
        }

        // ---- the lamps no rig publishes a projected anchor for ------------------------------------------

        [Test]
        public void EverySidelightPairIsOnOppositeSidesAtOneStation()
        {
            foreach (var pair in LampedHullsByRig())
                foreach ((FleetHull hull, HullMeshDef def) in pair.Value)
                {
                    HullLamp port = LampOf(def, HullLampKind.PortSidelight);
                    HullLamp star = LampOf(def, HullLampKind.StarboardSidelight);

                    // +x is starboard in this frame, so the signs are the whole claim: get them the
                    // wrong way round and the boat shows red to starboard, which is the one mistake in
                    // this feature that could actually mislead somebody about which way she is heading.
                    Assert.Less(port.RigLocalMetres.x, 0f,
                                $"{hull.Key}: the PORT sidelight sits to port (negative x)");
                    Assert.Greater(star.RigLocalMetres.x, 0f,
                                   $"{hull.Key}: the STARBOARD sidelight sits to starboard");
                    Assert.AreEqual(port.RigLocalMetres.y, star.RigLocalMetres.y, 1e-4f,
                                    $"{hull.Key}: the pair sits at one station — one fitting, two sides");
                    Assert.AreEqual(port.RigLocalMetres.z, star.RigLocalMetres.z, 1e-4f,
                                    $"{hull.Key}: and at one height");
                }
        }

        [Test]
        public void EveryAnchorLightIsHoistedAtHerMasthead()
        {
            // Not a derivation anybody can eyeball later: an all-round white wants the highest point
            // she has, and the masthead is the only high point every one of these rigs names.
            foreach (var pair in LampedHullsByRig())
                foreach ((FleetHull hull, HullMeshDef def) in pair.Value)
                {
                    HullLamp anchor = LampOf(def, HullLampKind.AnchorLight);
                    HullLamp mast = LampOf(def, HullLampKind.Masthead);
                    Assert.AreEqual(0f, Vector3.Distance(anchor.RigLocalMetres, mast.RigLocalMetres), 1e-5f,
                                    $"{hull.Key}: her anchor light hangs at her masthead");
                }
        }

        [Test]
        public void EveryCabinGlowSitsInsideTheRoomItIsSupposedToBeLighting()
        {
            var complaints = new List<string>();

            foreach (var pair in LampedHullsByRig())
            {
                using var host = new V8RigScriptHost();
                host.Execute(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, pair.Key)));

                foreach ((FleetHull hull, HullMeshDef def) in pair.Value)
                {
                    if (!TryLamp(def, HullLampKind.CabinGlow, out HullLamp cabin)) continue;
                    string g = ScopedGlobal(hull);
                    string opts = hull.Extraction != null ? hull.Extraction.ViewOptions : null;

                    // Each rig publishes the room as a box, which is the only thing a cabin glow has to
                    // respect: a lamp outside it is a lamp glowing through a deck, and no measurement of
                    // a room's centre can be checked any other way.
                    string hs = host.EvaluateBool($"typeof {g}.HOUSE === 'object' && {g}.HOUSE !== null")
                              ? g + ".HOUSE"
                              : (host.EvaluateBool($"typeof {g}.houseOf === 'function'")
                                 ? $"{g}.houseOf({opts ?? ""})" : null);
                    if (hs == null) { complaints.Add($"{hull.Key}: no published HOUSE to check against"); continue; }

                    bool ship = host.EvaluateBool($"{hs}.decks != null");
                    string room = ship ? hs + ".decks.house" : hs;
                    double y0 = host.EvaluateNumber(room + (ship ? ".y0" : ".yAft"));
                    double y1 = host.EvaluateNumber(room + (ship ? ".y1" : ".yFwd"));
                    double sole = host.EvaluateNumber(room + ".soleZ");
                    double ceil = host.EvaluateBool($"{room}.ceilZ != null")
                                ? host.EvaluateNumber(room + ".ceilZ")
                                : host.EvaluateNumber(hs + ".eaveZ");

                    Vector3 p = cabin.RigLocalMetres;
                    if (p.y < y0 - 1e-3 || p.y > y1 + 1e-3)
                        complaints.Add($"{hull.Key}: cabin glow at y {p.y:F3}, outside her room's {y0:F3}..{y1:F3}");
                    if (p.z < sole - 1e-3 || p.z > ceil + 1e-3)
                        complaints.Add($"{hull.Key}: cabin glow at z {p.z:F3}, outside her room's {sole:F3}..{ceil:F3}");
                    if (Mathf.Abs(p.x) > 1e-4f)
                        complaints.Add($"{hull.Key}: cabin glow off the centreline at x {p.x:F5}");
                }
            }

            CollectionAssert.IsEmpty(complaints,
                "a cabin glow outside its own room glows through a deck:\n  " + string.Join("\n  ", complaints));
        }

        [Test]
        public void ASearchlightIsDeclaredExactlyWhereTheShippedBeamClearsHerOwnStem()
        {
            var complaints = new List<string>();
            int withBeam = 0, without = 0;

            foreach (BoatLampAnchorProbe.HullLamps measured in BoatLampAnchorProbe.Measure())
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(measured.MeshAssetPath);
                if (def == null || def.Lamps == null || def.Lamps.Length == 0) continue;

                bool declared = TryLamp(def, HullLampKind.Spotlight, out HullLamp beam);
                bool shouldHave = BoatLampAnchorProbe.ClearsHerOwnStem(measured.BeamClearanceMetres);

                if (declared != shouldHave)
                    complaints.Add($"{measured.Key}: declares a searchlight = {declared}, but her beam " +
                                   $"clearance is {measured.BeamClearanceMetres:F3} m");
                if (!declared) { without++; continue; }
                withBeam++;

                if (Mathf.Abs(beam.RigLocalMetres.x) > 1e-4f)
                    complaints.Add($"{measured.Key}: searchlight off the centreline at x {beam.RigLocalMetres.x:F5}");
            }

            // The line has real air on both sides — the fleet is not balanced on it. Every hull that
            // clears does so by 2.3 m or more; every hull that does not falls short by 0.9 m or more.
            Assert.AreEqual(21, withBeam,
                "twenty-one hulls are conned far enough forward for the shipped 9 m beam to reach sea " +
                "past their own bow: the cape, the lobster boat, her eighteen variants and the sport " +
                "fisher convertible.");
            Assert.AreEqual(6, without,
                "and six are not — the side dragger, both stern trawlers, the coastal packet, the " +
                "tanker and the sport fisher skybridge all con from abaft the point where a 9 m beam " +
                "would clear their own stem, so a mount on them would rake their own deck rather than " +
                "the sea. Unblocking them is a per-hull throw: a preset change, not a measurement.");
            CollectionAssert.IsEmpty(complaints, string.Join("\n  ", complaints));
        }
    }
}
