using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The dooryards, pinned — the ARITHMETIC, not the pixels.</b>
    ///
    /// <para><c>YardDressing.PlanOne</c> is pure: a yard row and a contract in, a deterministic list of
    /// (piece, position, facing) out, with no AssetDatabase and no GameObject. That is what lets these
    /// tests measure the real tables of both regions whether or not the kit has been baked — instead of
    /// quietly passing on an empty scene the day the sheets are missing, which is the shape of a
    /// placement test that proves nothing.</para>
    ///
    /// <para><b>⭐⭐ THE ONE THING THESE EXIST FOR.</b> The yard kit's gameplay sidecar publishes a facing
    /// list that is MIRRORED against its own art — the rig steps counter-clockwise and the list reads
    /// clockwise, so they agree only at two of eight cells. A consumer that trusts the label asks for
    /// "E" and draws a panel facing WEST: a plausible picture, no error anywhere, and only an eyeball
    /// standing in the village would ever catch it. So the facing is chosen by searching the bearings
    /// the BAKE MEASURED, and <see cref="FacingFor_ReadsTheMeasuredTable_NotAFormula"/> is the negative
    /// control that proves the search is really a search.</para>
    /// </summary>
    public class YardDressingTests
    {
        // =============================================================================================
        //  a synthetic contract — the shape the bake writes, without needing the bake
        // =============================================================================================

        /// <summary>
        /// Panel metres at the baked <c>len</c>, from the KIT'S OWN SIDECAR
        /// (<c>foot[0] × (1 + len × runGain)</c> at <see cref="YardKit.RunLen"/>).
        ///
        /// <para>⚠ These are a stand-in so the arithmetic can be tested with no art on disk. They are
        /// NOT the authority — the bake reads <c>footprint()</c> off the live rig and writes the real
        /// numbers into the contract. <see cref="TheRealContract_AgreesWithTheSidecarsOwnGeometry"/>
        /// reconciles the two whenever the kit has actually been baked.</para>
        /// </summary>
        static readonly Dictionary<string, float> SidecarWidths = new Dictionary<string, float>
        {
            ["picketPanel"] = 1.70f * (1f + YardKit.RunLen * 0.94f),
            ["postRail"] = 2.00f * (1f + YardKit.RunLen * 0.90f),
            ["splitRail"] = 2.20f * (1f + YardKit.RunLen * 0.82f),
            ["wireFence"] = 2.20f * (1f + YardKit.RunLen * 0.91f),
            ["stoneWall"] = 1.80f * (1f + YardKit.RunLen * 0.89f),
            ["hedgeRun"] = 1.60f * (1f + YardKit.RunLen * 1.19f),
            ["picketGate"] = 1.30f,
            ["fenceCorner"] = 0.70f,
            ["stonePillar"] = 0.64f,
            ["hedgeCorner"] = 0.95f,
        };

        /// <summary>A contract with the shape the bake writes: one entry per build row, and a HEALTHY
        /// measured facing table (cell k faces 45k°).</summary>
        static YardKit.Contract SyntheticContract()
        {
            var bearings = new float[YardKit.Facings];
            for (int k = 0; k < bearings.Length; k++) bearings[k] = 45f * k;
            return SyntheticContract(bearings);
        }

        static YardKit.Contract SyntheticContract(float[] cellFaceBearings)
        {
            var pieces = YardKit.Builds.Select(b => new YardKit.Entry
            {
                key = b.Key,
                piece = b.Piece,
                role = b.Part.ToString(),
                len = b.Len,
                kept = b.Kept,
                facings = YardKit.Facings,
                columns = YardKit.Facings,
                rows = 1,
                cellW = 96, cellH = 96, pivotX = 48, pivotY = 78,
                footprintWidthMetres = SidecarWidths.TryGetValue(b.Key, out float w) ? w : 1f,
                footprintDepthMetres = 0.4f,
            }).ToArray();

            return new YardKit.Contract
            {
                rigKey = YardKit.RigKey,
                globalName = YardKit.GlobalName,
                facings = YardKit.Facings,
                ppu = YardKit.PixelsPerUnit,
                convention = "CounterClockwise",
                cellFaceBearings = cellFaceBearings,
                cellRunBearings = cellFaceBearings.Select(b => Mathf.Repeat(b + 90f, 360f)).ToArray(),
                pieces = pieces,
            };
        }

        static IReadOnlyList<Yard> BothRegions()
        {
            var all = new List<Yard>();
            all.AddRange(StPetersYards.Yards);
            all.AddRange(NineMileCreekYards.Yards);
            return all;
        }

        // =============================================================================================
        //  the style table
        // =============================================================================================

        [Test]
        public void EveryFenceStyle_ResolvesToPiecesTheBuildTableActuallyBakes()
        {
            var baked = new HashSet<string>(YardKit.Builds.Select(b => b.Key));

            foreach (YardFence fence in System.Enum.GetValues(typeof(YardFence)))
            {
                var style = YardDressing.StyleFor(fence);

                if (fence == YardFence.None)
                {
                    Assert.IsFalse(style.Builds, "YardFence.None builds nothing — the mow line alone " +
                                                 "says where the property ends.");
                    continue;
                }

                Assert.IsTrue(style.Builds, $"{fence} has no panel, so a yard authored with it would " +
                                            "be silently unfenced.");
                foreach (string key in new[] { style.PanelKey, style.PostKey, style.GateKey })
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    Assert.IsTrue(baked.Contains(key),
                        $"{fence} asks for '{key}', which YardKit.Builds does not bake. A style may " +
                        "legitimately have no post or no gate — but it may not name one that has no " +
                        "sheet, because that is a warning at build time rather than a red here.");
                }
            }
        }

        [Test]
        public void EveryBakedPiece_PlaysTheRoleItsStyleUsesItFor()
        {
            var byKey = YardKit.Builds.ToDictionary(b => b.Key);

            foreach (YardFence fence in System.Enum.GetValues(typeof(YardFence)))
            {
                var style = YardDressing.StyleFor(fence);
                if (!style.Builds) continue;

                Assert.AreEqual(YardKit.Role.Panel, byKey[style.PanelKey].Part,
                    $"{fence}'s panel '{style.PanelKey}' is baked as a " +
                    $"{byKey[style.PanelKey].Part} — a post tiled along a run would draw a line of " +
                    "gateposts.");
                if (!string.IsNullOrEmpty(style.PostKey))
                    Assert.AreEqual(YardKit.Role.Post, byKey[style.PostKey].Part);
                if (!string.IsNullOrEmpty(style.GateKey))
                    Assert.AreEqual(YardKit.Role.Gate, byKey[style.GateKey].Part);
            }
        }

        [Test]
        public void TheBuildTable_BakesNothingNoStyleAsksFor()
        {
            var required = new HashSet<string>(YardDressing.RequiredPieceKeys());
            var spare = YardKit.Builds.Where(b => !required.Contains(b.Key)).Select(b => b.Key).ToArray();

            Assert.IsEmpty(spare,
                "these sheets bake and nothing places them: " + string.Join(", ", spare) + ". The kit " +
                "has 52 more pieces of dressing that deliberately do NOT bake yet; a row here that no " +
                "style names is texture memory nobody asked for (CLAUDE.md rule 7).");
        }

        [Test]
        public void EveryBuildKey_IsUnique()
        {
            var dupes = YardKit.Builds.GroupBy(b => b.Key).Where(g => g.Count() > 1)
                                      .Select(g => g.Key).ToArray();
            Assert.IsEmpty(dupes, "two rows share a sheet path, so one silently overwrites the other: " +
                                  string.Join(", ", dupes));
        }

        [Test]
        public void EveryBuild_StatesEveryOptionThatMovesAPixel()
        {
            // ⚠ The trap this pins is the fuel kit's: an OMITTED option is not "no value", it is the
            // rig's own default — and for `len` that default is 0.4 on a run piece and 0 on a fixed
            // one, which is GEOMETRY, not colour. Every key must appear in the options expression.
            foreach (var b in YardKit.Builds)
            {
                string js = YardKit.OptionsJs(b);
                foreach (string key in new[] { "paint:", "variant:", "len:", "kept:", "phase:", "snow:",
                                               "weather:", "night:", "seed:", "compose:", "elev:" })
                    StringAssert.Contains(key, js, $"{b.Key}'s options omit {key}");
            }
        }

        // =============================================================================================
        //  ⭐⭐ the facing — measured table, never a label and never a formula
        // =============================================================================================

        [Test]
        public void FacingFor_ReadsTheMeasuredTable_NotAFormula()
        {
            // THE NEGATIVE CONTROL for the whole kit. Hand the placer a contract whose measured table
            // runs the OTHER way — cell k faces −45k — and the chosen cell must follow the table. A
            // placer that had quietly hard-coded round(bearing/45) would return the same answer for
            // both contracts and this would fail.
            var healthy = SyntheticContract();
            var mirrored = SyntheticContract(Enumerable.Range(0, YardKit.Facings)
                                                       .Select(k => Mathf.Repeat(-45f * k, 360f))
                                                       .ToArray());

            Assert.AreEqual(2, YardCatalog.FacingFor(healthy, 90f), "east is cell 2 on a clockwise bake");
            Assert.AreEqual(6, YardCatalog.FacingFor(mirrored, 90f),
                "on a counter-clockwise table east is cell 6 — and if this reads 2, the facing is being " +
                "computed rather than looked up, which is precisely how this kit's mirrored sidecar " +
                "would have reached the village.");
        }

        [Test]
        public void BearingOf_IsACompass_NotAMathsAngle()
        {
            // ⚠ Atan2(y, x) and the compass agree at EXACTLY one place — east — which is where a wrong
            // one gets spot-checked and passes.
            Assert.AreEqual(0f, YardCatalog.BearingOf(Vector2.up), 1e-3f, "north");
            Assert.AreEqual(90f, YardCatalog.BearingOf(Vector2.right), 1e-3f, "east");
            Assert.AreEqual(180f, YardCatalog.BearingOf(Vector2.down), 1e-3f, "south");
            Assert.AreEqual(270f, YardCatalog.BearingOf(Vector2.left), 1e-3f, "west");
            Assert.AreEqual(45f, YardCatalog.BearingOf(new Vector2(1f, 1f)), 1e-3f, "north-east");
        }

        [Test]
        public void EveryPieceFacesOUT_OfTheYardItStandsOn()
        {
            var contract = SyntheticContract();
            int checkedPieces = 0;
            float worst = 0f;

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                foreach (var piece in plan.Pieces)
                {
                    checkedPieces++;

                    // 1. the aim itself leaves the yard
                    Vector2 outward = new Vector2(Mathf.Sin(piece.OutwardBearing * Mathf.Deg2Rad),
                                                  Mathf.Cos(piece.OutwardBearing * Mathf.Deg2Rad));
                    Assert.IsFalse(yard.Contains(piece.Position + outward * 1.0f),
                        $"{yard.Name}/{piece.Key}: its 'outward' points back into the yard, so the " +
                        "fence would present its garden side to the road.");

                    // 2. the chosen CELL is the nearest one to that aim
                    float error = YardCatalog.FacingErrorDegrees(contract, piece.OutwardBearing,
                                                                 piece.Facing);
                    worst = Mathf.Max(worst, error);
                    Assert.LessOrEqual(error, 22.5f + 1e-3f,
                        $"{yard.Name}/{piece.Key} at {piece.OutwardBearing:0.0}° took cell " +
                        $"{piece.Facing}, which is {error:0.0}° away. Eight facings means half a step " +
                        "is 22.5° and nothing may be worse.");
                }
            }

            Assert.Greater(checkedPieces, 0, "no pieces planned at all — the tables went empty.");
            TestContext.WriteLine($"{checkedPieces} pieces aimed, worst facing error {worst:0.00}°.");
        }

        // =============================================================================================
        //  tiling
        // =============================================================================================

        [Test]
        public void PanelsOverlap_AndNeverGAP()
        {
            // ⌈L/w⌉ panels, so the pitch is never longer than a panel. A gap in a fence is visible; an
            // overlap between two lengths of the same repeating art is not. This measures the pitch that
            // actually came out rather than re-deriving the rule.
            var contract = SyntheticContract();
            float worstOverlapFraction = 0f;

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                if (!plan.Fenced) continue;

                var panelKey = YardDressing.StyleFor(yard.Fence).PanelKey;
                float width = contract.Find(panelKey).footprintWidthMetres;

                foreach (var run in plan.Runs)
                    for (int s = 0; s + 1 < run.Length; s++)
                    {
                        float length = Vector2.Distance(run[s], run[s + 1]);
                        if (length < width) continue;            // an offcut; counted elsewhere

                        int count = Mathf.CeilToInt(length / width - 1e-4f);
                        float pitch = length / count;
                        Assert.LessOrEqual(pitch, width + 1e-3f,
                            $"{yard.Name}: a {length:0.00} m segment took {count} × {width:0.00} m " +
                            $"panels at {pitch:0.00} m pitch — that is a HOLE in the fence.");
                        worstOverlapFraction = Mathf.Max(worstOverlapFraction, (width - pitch) / width);
                    }
            }

            TestContext.WriteLine($"worst panel overlap {worstOverlapFraction * 100f:0.0}% of a panel.");
            Assert.Less(worstOverlapFraction, 0.5f,
                "an overlap over half a panel means a segment barely longer than one panel got two, " +
                "which reads as a doubled post rather than as a continuous run.");
        }

        [Test]
        public void NeitherRegionLeavesAnOffcutBare_Today()
        {
            // ⚠ THE MEASURED CLAIM, not a rule: a segment shorter than one panel gets nothing, and
            // today none is. The shortest segment in either table is a 13 m frontage's half-front at
            // (13 − 1.8)/2 = 5.6 m against a longest panel of about 3 m. This goes red the day a yard
            // shrinks or gains a gate near a corner — which is the moment somebody has to decide what a
            // sub-panel offcut should look like, rather than shipping a hole.
            var contract = SyntheticContract();
            var bare = new List<string>();

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                if (plan.OffcutsSkipped > 0)
                    bare.Add($"{yard.Name}: {plan.OffcutsSkipped} offcut(s), {plan.OffcutMetres:0.00} m");
            }

            Assert.IsEmpty(bare, "segments too short for one panel of their own style: " +
                                 string.Join("; ", bare));
        }

        [Test]
        public void ShortestSegment_HasRoomForItsOwnPanel()
        {
            var contract = SyntheticContract();
            float shortest = float.MaxValue;
            string where = "";
            float widest = 0f;

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                if (!plan.Fenced) continue;

                float width = contract.Find(YardDressing.StyleFor(yard.Fence).PanelKey)
                                      .footprintWidthMetres;
                widest = Mathf.Max(widest, width);

                foreach (var run in plan.Runs)
                    for (int s = 0; s + 1 < run.Length; s++)
                    {
                        float length = Vector2.Distance(run[s], run[s + 1]);
                        if (length >= shortest) continue;
                        shortest = length;
                        where = $"{yard.Name} ({yard.Fence}, panel {width:0.00} m)";
                    }
            }

            TestContext.WriteLine($"shortest segment {shortest:0.00} m at {where}; " +
                                  $"longest panel {widest:0.00} m.");
            Assert.Greater(shortest, widest,
                $"the shortest segment in either region ({shortest:0.00} m at {where}) is under the " +
                $"longest panel ({widest:0.00} m), so some boundary is now being left bare.");
        }

        // =============================================================================================
        //  determinism
        // =============================================================================================

        [Test]
        public void ThePlan_IsDeterministic_PieceForPiece()
        {
            var contract = SyntheticContract();

            StPetersYards.InvalidateCache();
            var first = YardDressing.PlanAll(BothRegions(), contract);
            StPetersYards.InvalidateCache();
            var second = YardDressing.PlanAll(BothRegions(), contract);

            Assert.AreEqual(first.Count, second.Count);
            for (int y = 0; y < first.Count; y++)
            {
                Assert.AreEqual(first[y].Pieces.Count, second[y].Pieces.Count,
                    $"{first[y].YardName} planned a different number of pieces the second time.");

                for (int i = 0; i < first[y].Pieces.Count; i++)
                {
                    var a = first[y].Pieces[i];
                    var b = second[y].Pieces[i];
                    Assert.AreEqual(a.Key, b.Key, $"{first[y].YardName} piece {i}");
                    Assert.AreEqual(a.Facing, b.Facing, $"{first[y].YardName} piece {i} facing");
                    Assert.AreEqual(a.Position.x, b.Position.x, 1e-4f, $"{first[y].YardName} piece {i} x");
                    Assert.AreEqual(a.Position.y, b.Position.y, 1e-4f, $"{first[y].YardName} piece {i} y");
                }
            }
        }

        // =============================================================================================
        //  what the pass comes to
        // =============================================================================================

        [Test]
        public void AnUnfencedYard_GetsNoFence_AndNoWALL()
        {
            // ⭐ The second half matters more than the first. Walling a lawn nobody fenced is the
            // INVISIBLE WALL — the owner's 2026-08-19 walk found three of those and they are worse than
            // no wall, because there is nothing on screen to explain why you stopped.
            var contract = SyntheticContract();
            int unfenced = 0;

            foreach (var yard in BothRegions())
            {
                if (yard.Fence != YardFence.None) continue;
                unfenced++;

                var plan = YardDressing.PlanOne(yard, contract);
                Assert.IsFalse(plan.Fenced, $"{yard.Name} is YardFence.None and planned a fence.");
                Assert.IsEmpty(plan.Pieces, $"{yard.Name} is YardFence.None and planned art.");
                Assert.AreEqual(0f, plan.BlockingMetres, 1e-4f,
                    $"{yard.Name} is YardFence.None and planned a wall you cannot see.");
            }

            Assert.Greater(unfenced, 0,
                "no unfenced yard in either table — this test then proves nothing, and the shop " +
                "forecourts that are open to the lane on purpose have quietly grown fences.");
        }

        [Test]
        public void TheWallStandsOnTheSameRunsTheFenceDoes()
        {
            // ONE authored fact, two derivations. The gate you can see and the gate you can walk
            // through are the same gap because both come out of Yard.FenceRuns().
            var contract = SyntheticContract();

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                if (!plan.Fenced) continue;

                var expected = yard.FenceRuns();
                Assert.AreEqual(expected.Count, plan.Runs.Count, $"{yard.Name} run count");

                float authored = 0f;
                foreach (var run in expected)
                    for (int s = 0; s + 1 < run.Length; s++)
                        authored += Vector2.Distance(run[s], run[s + 1]);

                Assert.AreEqual(authored, plan.BlockingMetres, 1e-2f,
                    $"{yard.Name}: the wall is {plan.BlockingMetres:0.00} m against " +
                    $"{authored:0.00} m of authored boundary.");
            }
        }

        [Test]
        public void AGateLeafStandsONTheBoundary_NotAtTheAuthoredPoint()
        {
            // ⚠ A gate is authored as "the village green" or "the road" — a point that can be tens of
            // metres away. PerimeterRuns cuts its gap at the NEAREST POINT ON THE PERIMETER; the leaf
            // has to be snapped the same way or it stands out in a field beside a hole in a fence.
            var contract = SyntheticContract();
            int gates = 0;

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                foreach (var piece in plan.Pieces)
                {
                    if (piece.Role != YardKit.Role.Gate) continue;
                    gates++;
                    Assert.LessOrEqual(yard.Polygon.DistanceToEdge(piece.Position), 0.05f,
                        $"{yard.Name}: its gate leaf is " +
                        $"{yard.Polygon.DistanceToEdge(piece.Position):0.00} m off its own boundary.");
                }
            }

            Assert.Greater(gates, 0, "no gate leaf planned anywhere — every picket yard lost its gate.");
        }

        [Test]
        public void EveryCornerOfAPostedStyle_GetsAPost()
        {
            // A picket, stone or hedge boundary turns four corners and opens at one gate, so an open run
            // round a rectangle carries its four corners plus two jambs. Counted rather than assumed:
            // a corner that quietly loses its post shows as a mitre gap only from one side.
            var contract = SyntheticContract();
            int posted = 0;

            foreach (var yard in BothRegions())
            {
                var style = YardDressing.StyleFor(yard.Fence);
                if (string.IsNullOrEmpty(style.PostKey)) continue;

                var plan = YardDressing.PlanOne(yard, contract);
                if (!plan.Fenced) continue;
                posted++;

                int vertices = 0;
                foreach (var run in plan.Runs)
                {
                    bool closed = (run[0] - run[run.Length - 1]).sqrMagnitude < 1e-6f;
                    vertices += closed ? run.Length - 1 : run.Length;
                }

                Assert.AreEqual(vertices, plan.CountOf(YardKit.Role.Post),
                    $"{yard.Name}: {vertices} vertices on its runs but " +
                    $"{plan.CountOf(YardKit.Role.Post)} posts. A closed run's first point is a corner " +
                    "like any other and must not be double-posted.");
            }

            Assert.Greater(posted, 0, "no yard in either table uses a style with posts.");
        }

        [Test]
        public void TheFenceBill_IsWhatTheTablesAskFor()
        {
            // The headline number, stated rather than trusted: a pass that quietly built nothing still
            // reports success everywhere else.
            var contract = SyntheticContract();
            int panels = 0, posts = 0, gates = 0, fenced = 0;
            float wall = 0f;

            foreach (var yard in BothRegions())
            {
                var plan = YardDressing.PlanOne(yard, contract);
                if (!plan.Fenced) continue;
                fenced++;
                panels += plan.CountOf(YardKit.Role.Panel);
                posts += plan.CountOf(YardKit.Role.Post);
                gates += plan.CountOf(YardKit.Role.Gate);
                wall += plan.BlockingMetres;
            }

            TestContext.WriteLine($"{fenced} fenced yard(s): {panels} panels + {posts} posts + " +
                                  $"{gates} gates over {wall:0.0} m of boundary.");

            Assert.Greater(fenced, 0, "not one yard in either region planned a fence.");
            Assert.Greater(panels, 0, "fences planned with no panels at all.");
            Assert.Less(panels + posts + gates, 2000,
                "the boundary pass is drawing thousands of sprites — at 32 px/m that is a draw-call " +
                "budget problem, not a dressing pass (CLAUDE.md rule 7).");
        }

        // =============================================================================================
        //  reconciling with the real bake, when there is one
        // =============================================================================================

        [Test]
        public void TheRealContract_AgreesWithTheSidecarsOwnGeometry()
        {
            var real = YardKit.Load();
            if (real == null)
                Assert.Ignore("the yard kit is not baked in this checkout — run Hidden Harbours ▸ Art " +
                              "▸ Bake Yard Kit. The arithmetic above is pinned against the sidecar's " +
                              "own geometry regardless.");

            foreach (var kv in SidecarWidths)
            {
                var entry = real.Find(kv.Key);
                Assert.IsNotNull(entry, $"the baked contract has no '{kv.Key}'.");
                Assert.AreEqual(kv.Value, entry.footprintWidthMetres, 0.01f,
                    $"'{kv.Key}' baked at {entry.footprintWidthMetres:0.000} m but the kit's sidecar " +
                    $"says {kv.Value:0.000} m at len {YardKit.RunLen}. The rig's footprint() is the " +
                    "authority — if these have drifted, the SIDECAR table in this test is what is " +
                    "stale, and the placer is fine.");
            }
        }

        [Test]
        public void TheRealContract_CarriesAClockwiseMeasuredCompass()
        {
            var real = YardKit.Load();
            if (real == null)
                Assert.Ignore("the yard kit is not baked in this checkout.");

            Assert.IsTrue(YardCatalog.HasMeasuredBearings(real),
                "the baked contract carries no measured facing table, so nothing can be aimed.");

            for (int k = 0; k < real.facings; k++)
            {
                Assert.LessOrEqual(YardCatalog.AngleGap(real.cellFaceBearings[k], 45f * k), 0.5f,
                    $"cell {k} faces {real.cellFaceBearings[k]:0.00}°, not {45f * k}°. The bake applies " +
                    "the counter-clockwise correction so the SHEET is a plain clockwise compass; if " +
                    "that stopped being true, RigCatalog's declaration and the rig have diverged.");

                Assert.LessOrEqual(
                    YardCatalog.AngleGap(real.cellRunBearings[k] - real.cellFaceBearings[k], 90f), 0.5f,
                    $"cell {k}'s run axis is not square to its face. The kit's axis contract is '+X " +
                    "along the run, +Y away from the house'; a panel is aimed by its face and drawn " +
                    "along its own length, so those two must be 90° apart or the fence stands at an " +
                    "angle to its own wall.");
            }
        }
    }
}
