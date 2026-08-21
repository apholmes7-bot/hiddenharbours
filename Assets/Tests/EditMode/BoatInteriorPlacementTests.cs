using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PLACEMENT PASS, HELD TO ITS DATA</b> — every measured hull wired, no refused hull wired,
    /// every wiring internally consistent with the sheets it points at, and the entry gate honest in
    /// both directions.
    ///
    /// <para>Content-validation in character: these walk COMMITTED assets, so they fail when the wiring
    /// builder has not been re-run after a re-bake — which is the whole point. A hull that quietly lost
    /// her cells still draws a boat, and nothing else in the project would notice.</para>
    /// </summary>
    public class BoatInteriorPlacementTests
    {
        private const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        private const string VisualFolder = "Assets/_Project/Data/Boats/Visuals";
        private const string CellsFolder =
            "Assets/_Project/Resources/" + BoatInteriorCellsDef.ResourcesFolder;

        /// <summary>The three hulls the S0 intake ledger REFUSES. Named here as the test's own
        /// statement of the law — a def must never exist for one, and wiring must never reach one.</summary>
        private static readonly string[] RefusedStems =
        {
            "SportFisherConvertibleIso", "SportFisherSkybridgeIso", "CapeIslanderIso",
        };

        private readonly List<UnityEngine.Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        private static IEnumerable<(string stem, BoatInteriorDef def)> Interiors()
        {
            string[] guids = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorFolder });
            Array.Sort(guids);
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(path);
                if (def != null) yield return (Path.GetFileNameWithoutExtension(path), def);
            }
        }

        private static BoatVisualDef Visual(string stem)
            => AssetDatabase.LoadAssetAtPath<BoatVisualDef>($"{VisualFolder}/{stem}.asset");

        private static BoatInteriorCellsDef Cells(BoatInteriorDef def)
            => def == null ? null : AssetDatabase.LoadAssetAtPath<BoatInteriorCellsDef>(
                $"{CellsFolder}/{BoatInteriorCellsDef.FileNameFor(def.Id)}.asset");

        // =====================================================================================
        //  COVERAGE — every cleared hull, and only cleared hulls
        // =====================================================================================

        [Test]
        public void EveryMeasuredHullIsWiredToHerOwnCabin()
        {
            var missing = new List<string>();
            int seen = 0;

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                seen++;
                BoatVisualDef visual = Visual(stem);
                if (visual == null) { missing.Add($"{stem}: no BoatVisualDef"); continue; }
                if (visual.Interior == null) { missing.Add($"{stem}: Interior is null"); continue; }
                if (visual.Interior != def)
                    missing.Add($"{stem}: wired to '{visual.Interior.Id}', not her own '{def.Id}'");
            }

            Assert.Greater(seen, 0, "no interior defs found at all — the fixture is looking in the wrong place");
            Assert.IsEmpty(missing,
                "these hulls have a measured interior that nothing points at, so their cabins can never " +
                "be built. Re-run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Wire Boat Interiors To Visuals:\n  " +
                string.Join("\n  ", missing));
        }

        [Test]
        public void NoHullTheLedgerRefusedIsWired()
        {
            // The law, at the last gate before a refused hull becomes enterable content. A def must not
            // exist for one either — that is the intake's own guard — so this checks both halves.
            var wrong = new List<string>();
            foreach (string stem in RefusedStems)
            {
                if (AssetDatabase.LoadAssetAtPath<BoatInteriorDef>($"{InteriorFolder}/{stem}.asset") != null)
                    wrong.Add($"{stem}: a REFUSED hull has an interior def");

                BoatVisualDef visual = Visual(stem);
                if (visual != null && visual.Interior != null)
                    wrong.Add($"{stem}: a REFUSED hull is wired to '{visual.Interior.Id}'");
            }

            Assert.IsEmpty(wrong,
                "the S0 ledger refuses these hulls (two sport fishers on an unstamped renderer pin, the " +
                "cape on a forked rig). Fix the intake upstream, never this test:\n  " +
                string.Join("\n  ", wrong));
        }

        // =====================================================================================
        //  REGISTRATION — the cells the wiring points at are the hull's OWN
        // =====================================================================================

        [Test]
        public void EveryWiredHullsCellAndPivotAreHerExteriors()
        {
            // The kit's whole registration contract: the interior bakes into the FULL hull cell at the
            // HULL pivot, so it composites 1:1. If the wiring ever pointed a hull at another hull's
            // sheets, this is the number that catches it — every one of them draws a plausible cabin.
            var wrong = new List<string>();

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatVisualDef visual = Visual(stem);
                if (visual == null || visual.HullMesh == null) continue;   // sprite-only hull: nothing to compare

                HullMeshDef mesh = visual.HullMesh;
                if (def.CellPixels.x != mesh.CellW || def.CellPixels.y != mesh.CellH)
                    wrong.Add($"{stem}: interior cell {def.CellPixels.x}x{def.CellPixels.y} " +
                              $"but the hull's is {mesh.CellW}x{mesh.CellH}");

                if (def.PixelsPerMetre != mesh.PxPerMetre)
                    wrong.Add($"{stem}: interior {def.PixelsPerMetre} px/m but the hull bakes at " +
                              $"{mesh.PxPerMetre}");
            }

            Assert.IsEmpty(wrong, "interior sheets that do not register on the hull they are wired to:\n  " +
                                  string.Join("\n  ", wrong));
        }

        [Test]
        public void EveryWiredHullCarriesAWholeLevelMajorCellArray()
        {
            var wrong = new List<string>();

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatVisualDef visual = Visual(stem);
                if (visual == null || visual.Interior == null) continue;

                BoatInteriorCellsDef cells = Cells(def);
                if (cells == null)
                {
                    wrong.Add($"{stem}: no cells asset at {CellsFolder}/" +
                              $"{BoatInteriorCellsDef.FileNameFor(def.Id)}.asset");
                    continue;
                }

                // ⚠ The SHEET's rows, not the DEF's levels. They are different lists — see
                // BoatInteriorCellsDef.CellRowForLevel, and the failure that taught us so.
                int rows = cells.CellLevels != null ? cells.CellLevels.Length : 0;
                int want = rows * cells.Facings;
                int have = cells.Cells != null ? cells.Cells.Length : 0;

                if (have != want)
                    wrong.Add($"{stem}: {have} cells where {rows} sheet rows x {cells.Facings} " +
                              $"facings needs {want}");
                else if (!cells.IsUsableFor(def))
                    wrong.Add($"{stem}: a hole in the cell array, or a level map that does not cover " +
                              "the def — a partly wired sheet draws a plausible picture of the wrong room");
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void ThePixelGridIsStatedPerHull_AndTheTankerIsNotTheFleet()
        {
            // Two pixel grids live in this kit and conflating them is the trap the def's own doc names.
            // A zero would divide the composite by nothing; the fleet default would draw the tanker's
            // cabin at twice size, and it would still look like a cabin.
            var wrong = new List<string>();
            bool sawSixteen = false;

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                if (def.PixelsPerMetre <= 0) wrong.Add($"{stem}: PixelsPerMetre {def.PixelsPerMetre}");
                if (def.PixelsPerMetre == 16) sawSixteen = true;
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
            Assert.IsTrue(sawSixteen,
                "no hull reports 16 px/m. The tanker does, and a suite that never sees the second grid " +
                "is not guarding the trap it was written for.");
        }

        [Test]
        public void TheLevelMapNamesTheRightRoom_NotMerelyARoom()
        {
            // ⭐ THE REGRESSION THIS SUITE WAS WORTH WRITING FOR. A BoatInteriorDef declares every level
            // a route reaches, INCLUDING exterior working decks the sheets never bake; and the sheets run
            // bridge/house/below where the defs run main_deck/house_sole/bridge_sole/below_sole. Indexing
            // cells by the def's level index draws the BRIDGE while the player stands on the HOUSE sole —
            // on all five ships, and it looks like a perfectly good cabin.
            var wrong = new List<string>();
            int shipsSeen = 0;

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatVisualDef visual = Visual(stem);
                if (visual == null || visual.Interior == null) continue;
                BoatInteriorCellsDef cells = Cells(def);
                if (cells == null || cells.CellRowForLevel == null || def.Levels == null) continue;

                for (int i = 0; i < def.Levels.Length; i++)
                {
                    BoatInteriorLevel level = def.Levels[i];
                    if (level == null) continue;
                    int row = cells.CellRowForLevel[i];
                    if (row < 0) continue;   // an outdoor deck draws nothing, which is an answer

                    string key = cells.CellLevels[row];
                    bool same = string.Equals(level.Id, key, StringComparison.Ordinal) ||
                                string.Equals(level.Id, key + "_sole", StringComparison.Ordinal);
                    if (!same)
                        wrong.Add($"{stem}: level '{level.Id}' is drawn by the row baked for '{key}'");
                }

                // An outdoor deck must map to nothing at all.
                for (int i = 0; i < def.Levels.Length; i++)
                {
                    string id = def.Levels[i] != null ? def.Levels[i].Id : null;
                    if (id == null || !id.EndsWith("_deck", StringComparison.Ordinal)) continue;
                    shipsSeen++;
                    if (cells.CellRowForLevel[i] >= 0)
                        wrong.Add($"{stem}: the exterior deck '{id}' is mapped to a sheet row, but the " +
                                  "interior sheets do not bake a working deck");
                }
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
            Assert.Greater(shipsSeen, 0,
                "no hull declared an exterior working deck. The five ships do (main_deck, and the " +
                "tanker's poop_deck), so a suite that sees none is not looking at the fleet that " +
                "produced this bug.");
        }

        // =====================================================================================
        //  THE BUDGET — what a hull costs before anybody opens her door (rule 7)
        // =====================================================================================

        [Test]
        public void NoVisualDefReferencesAnInteriorSprite_SoAHullDragsNoCabinInOnLoad()
        {
            // ⭐ THE RESIDENCY PROPERTY, and it is STRUCTURAL rather than measured: Unity cannot pull
            // an asset nothing references, so if no BoatVisualDef holds a sprite from the interiors
            // folder then loading a hull loads none of her cabin. That is what makes
            // resident-while-shut exactly zero instead of merely small — 12.3 MB per lobster-class
            // hull, 281 for the tanker, 340 for the packet, for a feature the gate keeps unusable.
            //
            // A profiler measurement would prove one scene on one machine. This proves the shape.
            var offenders = new List<string>();

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatVisualDef visual = Visual(stem);
                if (visual == null) continue;

                foreach (string dep in AssetDatabase.GetDependencies(
                             AssetDatabase.GetAssetPath(visual), recursive: true))
                {
                    if (dep.StartsWith("Assets/_Project/Art/Boats/Interiors/", StringComparison.Ordinal))
                        offenders.Add($"{stem} -> {dep}");
                }
            }

            Assert.IsEmpty(offenders,
                "these visual defs reach an interior sheet, so every hull of that class drags her " +
                "cabin's pixels into memory the moment she loads — whether or not anyone can go " +
                "below. The cells belong in a BoatInteriorCellsDef under Resources, loaded at the " +
                "door's cue start:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void EveryWiredHullHasCellsUnderResources_KeyedByHerDefId()
        {
            var missing = new List<string>();
            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatInteriorCellsDef cells = Cells(def);
                if (cells == null) { missing.Add($"{stem}: no cells asset"); continue; }
                if (!string.Equals(cells.InteriorDefId, def.Id, StringComparison.Ordinal))
                    missing.Add($"{stem}: cells claim '{cells.InteriorDefId}', def is '{def.Id}'");
                if (cells.ResidentMegabytes <= 0f)
                    missing.Add($"{stem}: cells state no budget, so nothing can assert one");
            }
            Assert.IsEmpty(missing, string.Join("\n  ", missing));
        }

        [Test]
        public void TheResourcesKeyRoundTripsFromTheDefId()
        {
            // Dots become underscores because Resources.Load takes an extensionless path and a dotted
            // name invites it to strip the wrong tail. One documented transform, pinned here so it
            // cannot drift between the builder that writes the file and the runtime that asks for it.
            Assert.AreEqual("interior_tanker_iso", BoatInteriorCellsDef.FileNameFor("interior.tanker_iso"));
            Assert.AreEqual(BoatInteriorCellsDef.ResourcesFolder + "/interior_tanker_iso",
                            BoatInteriorCellsDef.PathFor("interior.tanker_iso"));
            Assert.IsNull(BoatInteriorCellsDef.PathFor(null));
            Assert.IsNull(BoatInteriorCellsDef.PathFor(""));
        }

        [Test]
        public void TheBudgetIsStatedPerHull_AndTheWorstCaseIsNamed()
        {
            // What entering ONE hull costs, so the number is in the suite rather than in a memory.
            float worst = 0f;
            string worstStem = null;
            float lobsterClass = 0f;

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                BoatInteriorCellsDef cells = Cells(def);
                if (cells == null) continue;
                if (cells.ResidentMegabytes > worst) { worst = cells.ResidentMegabytes; worstStem = stem; }
                if (stem.StartsWith("Lobster", StringComparison.Ordinal))
                    lobsterClass = Mathf.Max(lobsterClass, cells.ResidentMegabytes);
            }

            Assert.Greater(worst, 0f, "no hull states a budget");
            Assert.Less(lobsterClass, 20f,
                $"a lobster-class hull's cabin should be ~12 MB; it is {lobsterClass:F1}");
            Assert.Less(worst, 400f,
                $"the worst single hull ({worstStem}) is {worst:F1} MB. Above ~400 the per-LEVEL " +
                "loading follow-up stops being optional.");
        }

        // =====================================================================================
        //  THE THRESHOLD — the way in is somewhere you could actually stand
        // =====================================================================================

        [Test]
        public void EveryDoorSillStandsOnOneOfHerOwnLevels()
        {
            // The sill's z names the level you walk in ONTO (the sport fishers' sits at mezzanine height
            // so you walk in level). A sill that matched no sole would put the player through a deck.
            var wrong = new List<string>();

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                if (def.Door == null) { wrong.Add($"{stem}: no threshold at all"); continue; }

                float z = def.Door.ThresholdPoint.z;
                bool onALevel = def.Levels != null && def.Levels.Any(
                    l => l != null && l.IsUsable() && Mathf.Abs(l.SoleZMeters - z) < 0.51f);

                if (!onALevel)
                    wrong.Add($"{stem}: sill at z {z:0.###} matches no usable sole (" +
                              string.Join(", ", def.Levels.Where(l => l != null)
                                                          .Select(l => $"{l.Id}@{l.SoleZMeters:0.###}")) + ")");
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void EveryDoorSillLiesWithinHerOwnFootprint()
        {
            // In PLAN. A threshold outside the house envelope is a door into the sea — and it is exactly
            // the failure a hand-transcribed constant produces, which is why the sidecars exist.
            var wrong = new List<string>();

            foreach ((string stem, BoatInteriorDef def) in Interiors())
            {
                if (def.Door == null || def.FootprintOutline == null || def.FootprintOutline.Length < 3)
                    continue;

                var sill = new Vector2(def.Door.ThresholdPoint.x, def.Door.ThresholdPoint.y);
                if (!WithinOrNear(def.FootprintOutline, sill, 1.0f))
                    wrong.Add($"{stem}: sill ({sill.x:0.###}, {sill.y:0.###}) is more than a metre " +
                              "outside her own house footprint");
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        // =====================================================================================
        //  THE GATE (the S0 ruling, rider 1) — both directions
        // =====================================================================================

        [Test]
        public void ADoorOntoAnIncompletableSwapOffersNothing()
        {
            Rig rig = NewRig(wireExterior: false);

            Assert.IsFalse(rig.Interior.SwapIsCompletable,
                           "with no exterior half there is nothing for the interior to replace");
            Assert.IsFalse(rig.Door.IsAvailable,
                           "…so the door is not a thing to act on, and the popup never names it");
            Assert.IsFalse(rig.Door.TryUse(), "…and a press does nothing");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        [Test]
        public void ADoorOntoACompletableSwapOffersNormally()
        {
            // The other direction, and the one that stops the test above passing for the wrong reason:
            // if the gate were simply "always false" this would fail.
            Rig rig = NewRig(wireExterior: true);

            Assert.IsTrue(rig.Interior.SwapIsCompletable);
            Assert.IsTrue(rig.Door.IsAvailable);
            Assert.AreEqual("Go below", rig.Door.VerbLabel);
            Assert.IsTrue(rig.Door.TryUse());
        }

        [Test]
        public void CompletableIsAboutTheWiring_NotAboutWhichLayerIsOn()
        {
            // The distinction the predicate exists for: ExactlyOneLayerOn changes as you walk in and out;
            // SwapIsCompletable does not, because the mechanism did not change.
            Rig rig = NewRig(wireExterior: true);

            Assert.IsTrue(rig.Interior.SwapIsCompletable);
            Assert.IsTrue(rig.Interior.TryEnter(0));
            Assert.IsTrue(rig.Interior.SwapIsCompletable, "still whole while the occupant is inside");
            Assert.IsTrue(rig.Interior.ExactlyOneLayerOn);
            rig.Interior.TryExit();
            Assert.IsTrue(rig.Interior.SwapIsCompletable);
        }

        [Test]
        public void HandingInTheExteriorHalfLater_LightsThatCabinWithNoOtherChange()
        {
            // The property that lets the per-level face tags land hull-by-hull: no second switch, no
            // migration, and no window in which a half-built cabin is enterable.
            Rig rig = NewRig(wireExterior: false);
            Assert.IsFalse(rig.Door.IsAvailable);

            rig.Interior.Configure(rig.Def, rig.Exterior, rig.Room, null, rig.Room.transform,
                                   rig.Root.transform, Array.Empty<Sprite>(), 8, true, 0f, 5f, 1.6f, 0.02f);

            Assert.IsTrue(rig.Interior.SwapIsCompletable);
            Assert.IsTrue(rig.Door.IsAvailable, "the same door, now offering, with nothing else touched");
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        private struct Rig
        {
            public GameObject Root;
            public BoatInteriorDef Def;
            public BoatInterior Interior;
            public BoatCabinDoor Door;
            public SpriteRenderer Exterior;
            public SpriteRenderer Room;
        }

        private Rig NewRig(bool wireExterior)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = "interior.gate_test";
            def.PixelsPerMetre = 32;
            def.Levels = new[]
            {
                new BoatInteriorLevel
                {
                    Id = "house_sole",
                    SoleZMeters = 0.5f,
                    Outline = new[]
                    {
                        new Vector2(-1f, -2f), new Vector2(1f, -2f),
                        new Vector2(1f, 2f), new Vector2(-1f, 2f),
                    },
                },
            };
            def.Door = new BoatInteriorDoor
            {
                Id = "entry",
                ThresholdPoint = new Vector3(0f, -2f, 0.5f),
                CueFrames = 8,
                CueMillisecondsPerFrame = 70f,
                CueReversedOnExit = true,
            };
            _spawned.Add(def);

            var root = new GameObject("GateTestHull");
            _spawned.Add(root);

            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);
            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var interior = root.AddComponent<BoatInterior>();
            interior.Configure(def, wireExterior ? exterior : null, room, null, room.transform,
                               root.transform, Array.Empty<Sprite>(), 8, true, 0f, 5f, 1.6f, 0.02f);

            var doorGo = new GameObject("Door");
            doorGo.transform.SetParent(root.transform, false);
            var door = doorGo.AddComponent<BoatCabinDoor>();
            door.Configure(interior, "fixture.boat.gate_test.cabin_door", -1, 1.2f, "Go below", "Come out");

            return new Rig
            {
                Root = root, Def = def, Interior = interior, Door = door,
                Exterior = exterior, Room = room,
            };
        }

        /// <summary>Point in polygon (ray casting), with a slack radius for a sill authored ON the wall
        /// line — the threshold is IN the envelope's edge by construction, so an exact test would be a
        /// coin flip on floating point.</summary>
        private static bool WithinOrNear(Vector2[] poly, Vector2 p, float slackMetres)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            if (inside) return true;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (DistanceToSegment(p, poly[j], poly[i]) <= slackMetres) return true;
            return false;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            if (len <= 1e-9f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
