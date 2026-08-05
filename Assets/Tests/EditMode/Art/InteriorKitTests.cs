using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the INTERIOR KIT's identity and arithmetic — everything that can be checked without a JS
    /// engine, a graphics device or a baked sheet, which is most of what goes silently wrong.
    ///
    /// <para>Two of these are load-bearing beyond the usual:</para>
    /// <list type="bullet">
    ///   <item><see cref="EveryDialledKeyIsAKeyTheRigActuallyReads"/> — a misspelled option key changes
    ///   nothing and says nothing, so the only defence is greping the rig source for the option's own
    ///   resolve call. The worked example is <c>winD</c> (an internal field) versus <c>winDensity</c>
    ///   (the option): both strings appear in the rig, so the grep has to look for the CALL.</item>
    ///   <item><see cref="ThePilotRoomIsTheSameSizeAsTheShellItGoesInside"/> — the 1:1 registration rests
    ///   on both rigs resolving one footprint from one <c>size</c>, and the bake refuses if they do not.
    ///   Catching it here means finding out before a five-minute bake instead of after.</item>
    /// </list>
    /// </summary>
    public class InteriorKitTests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static string RigSource(string fileName) =>
            File.ReadAllText(Path.Combine(RepoRoot, "docs", "art", "rigs", fileName));

        // =============================================================================
        //  identity
        // =============================================================================

        [Test]
        public void TheKitBakesEightFacings_TheOwnersRuling()
        {
            Assert.AreEqual(8, InteriorKit.Facings,
                            "RULED 2026-08-05 with the budget question in front of the owner: the full " +
                            "eight, same as the exterior kit, because a room has to turn in lockstep " +
                            "with the shell over it");
        }

        [Test]
        public void RoomsAndPropsLandInSeparateFoldersUnderOneContract()
        {
            Assert.AreNotEqual(InteriorKit.RoomsRoot, InteriorKit.PropsRoot);
            StringAssert.StartsWith("Assets/_Project/Art/Sprites/Interiors/", InteriorKit.RoomsRoot);
            StringAssert.StartsWith("Assets/_Project/Art/Sprites/Interiors/", InteriorKit.PropsRoot);
            StringAssert.StartsWith("Assets/_Project/Art/Sprites/Interiors/", InteriorKit.ContractPath);
        }

        [Test]
        public void EveryKeyIsUnique_BecauseTheKeyIsTheSheetStemAndTheContractKey()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in InteriorKit.RoomSet)
                Assert.IsTrue(seen.Add(b.Key), $"duplicate room key '{b.Key}'");

            seen.Clear();
            foreach (var b in InteriorKit.PropSet)
                Assert.IsTrue(seen.Add(b.Key), $"duplicate prop key '{b.Key}'");
        }

        [Test]
        public void StemsAndSpriteNamesCannotCollideBetweenRoomsAndProps()
        {
            // "Interior_bed" and "InteriorProp_bed" have to stay distinguishable: the slicer resolves a
            // sheet to a contract entry BY STEM and fails loudly on a stranger, so a collision would
            // point a prop sheet at a room's cell.
            Assert.AreNotEqual(InteriorKit.RoomStemFor("bed"), InteriorKit.PropStemFor("bed"));
            Assert.AreNotEqual(InteriorKit.RoomSpriteNameFor("bed", 3),
                               InteriorKit.PropSpriteNameFor("bed", 3));
        }

        // =============================================================================
        //  ⭐ the silent-option trap
        // =============================================================================

        [Test]
        public void EveryDialledKeyIsAKeyTheRigActuallyReads()
        {
            AssertKeysResolve(RigSource("interiorIsoRig.js"), InteriorKit.RoomOptionKeys, "interiorIsoRig");
            AssertKeysResolve(RigSource("interiorPropRig.js"), InteriorKit.PropOptionKeys, "interiorPropRig");
        }

        [Test]
        public void EveryBuildUsesOnlyDeclaredKeys()
        {
            foreach (var b in InteriorKit.RoomSet)
            {
                if (b.Dialled == null) continue;
                foreach (var kv in b.Dialled)
                    CollectionAssert.Contains(InteriorKit.RoomOptionKeys, kv.Key,
                                              $"room '{b.Key}' dials '{kv.Key}', which is not in " +
                                              "InteriorKit.RoomOptionKeys — so nothing greps it against " +
                                              "the rig and a typo would ship in silence");
            }

            foreach (var b in InteriorKit.PropSet)
            {
                if (b.Dialled == null) continue;
                foreach (var kv in b.Dialled)
                    CollectionAssert.Contains(InteriorKit.PropOptionKeys, kv.Key,
                                              $"prop '{b.Key}' dials '{kv.Key}'");
            }
        }

        [Test]
        public void TheKnownTypoIsNotAKeyTheRigReads()
        {
            // The negative control on the grep itself. `winD` is the rig's INTERNAL field name and
            // appears in the source; it does nothing as an option. If the grep below passed it, the
            // whole defence above would be decorative.
            string source = RigSource("interiorIsoRig.js");
            StringAssert.Contains("winD", source, "the internal field really is spelled this way");
            Assert.IsFalse(ResolvesKey(source, "winD"),
                           "…and it is never READ as an option — which is what the grep must see");
            Assert.IsTrue(ResolvesKey(source, "winDensity"), "whereas the real option is");
        }

        /// <summary>Does the rig resolve <paramref name="key"/> as an OPTION? These rigs read options
        /// through <c>g('key', default)</c> or <c>opts.key</c>/<c>opts['key']</c>, so the presence of
        /// the bare string proves nothing.</summary>
        static bool ResolvesKey(string source, string key) =>
            Regex.IsMatch(source, $@"g\(\s*'{Regex.Escape(key)}'") ||
            Regex.IsMatch(source, $@"opts\.{Regex.Escape(key)}\b") ||
            Regex.IsMatch(source, $@"opts\[\s*'{Regex.Escape(key)}'\s*\]");

        static void AssertKeysResolve(string source, IReadOnlyList<string> keys, string rig)
        {
            foreach (string key in keys)
                Assert.IsTrue(ResolvesKey(source, key),
                              $"'{key}' is declared as an option of {rig} but the rig never resolves it. " +
                              "A key the rig does not read changes NOTHING and says nothing — the " +
                              "recorded worked example is winD versus winDensity.");
        }

        // =============================================================================
        //  ⭐ the 1:1 registration, checked without baking
        // =============================================================================

        [Test]
        public void ThePilotRoomIsTheSameSizeAsTheShellItGoesInside()
        {
            foreach (var room in InteriorKit.RoomSet)
            {
                string exteriorKey = InteriorKit.ExteriorKeyFor(room.Key);
                if (exteriorKey == null) continue;

                var exterior = VillageBuildingKit.FindBuild(exteriorKey);
                Assert.IsNotNull(exterior, $"'{room.Key}'");

                Assert.IsTrue(TryGetSize(room.Dialled, out double roomSize),
                              $"room '{room.Key}' must dial an explicit size — the registration rests " +
                              "on it and a defaulted one is a coincidence waiting to break");
                Assert.IsTrue(TryGetSize(exterior.Value.Dialled, out double shellSize),
                              $"shell '{exteriorKey}' must dial an explicit size");

                Assert.AreEqual(shellSize, roomSize, 1e-9,
                                $"'{room.Key}' is dialled at size {roomSize} but its shell at " +
                                $"{shellSize}. Both rigs compute Wd = 6 + size*2.4 and Ln = 7 + " +
                                "size*4.2, so a mismatch is a room that does not fit its house — and " +
                                "it draws perfectly either way.");
            }
        }

        [Test]
        public void EveryRoomKeyNamesAVillageBuilding()
        {
            // The keys are shared on purpose: StPetersInteriors looks a room up by the BUILDING's key,
            // so a room named anything else is a room nothing will ever stand.
            foreach (var room in InteriorKit.RoomSet)
                Assert.IsNotNull(VillageBuildingKit.FindBuild(room.Key),
                                 $"room '{room.Key}' has no village building of the same key, so " +
                                 "nothing will ever place it. Name a room after the building it is " +
                                 "inside.");
        }

        static bool TryGetSize(IReadOnlyDictionary<string, object> dialled, out double size)
        {
            size = 0;
            if (dialled == null || !dialled.TryGetValue("size", out object v)) return false;
            size = Convert.ToDouble(v);
            return true;
        }

        // =============================================================================
        //  the pivot and the facing offset
        // =============================================================================

        static InteriorKit.Entry Entry(int cellW = 400, int cellH = 320,
                                       float pivotX = 200f, float pivotY = 210f) =>
            new InteriorKit.Entry
            {
                key = "test", facings = 8, cols = 4, rows = 2,
                cellW = cellW, cellH = cellH, pivotX = pivotX, pivotY = pivotY,
                sheetW = cellW * 4, sheetH = cellH * 2,
            };

        [Test]
        public void NormalizedPivot_IsTheHullForm_NotTheTreeKitsPadOverHeight()
        {
            var e = Entry();
            Vector2 p = InteriorKit.NormalizedPivot(e);

            Assert.AreEqual(200f / 400f, p.x, 1e-6f);
            Assert.AreEqual((320f - 210f) / 320f, p.y, 1e-6f,
                            "(cellH − pivotY)/cellH. The tree kit's pad/cellH is a DIFFERENT quantity " +
                            "(ADR 0026): a rig pivot is a continuous point from the cell's top-left, " +
                            "and a room's floor centre is a projected POINT like a hull's, not a chosen " +
                            "ROW like a trunk foot.");
        }

        [Test]
        public void BelowGroundPad_IsWhatABottomCentrePivotWouldSinkTheRoomBy()
        {
            var e = Entry();
            Assert.AreEqual(320 - 210, InteriorKit.BelowGroundPad(e));
            Assert.AreEqual((320 - 210) / 32f, InteriorKit.BelowGroundMetres(e, 32), 1e-6f);
        }

        [Test]
        public void InteriorFacingFor_AppliesTheMeasuredOffsetAndWrapsBothWays()
        {
            Assert.AreEqual(4, InteriorKit.InteriorFacingFor(0, 4, 8));
            Assert.AreEqual(0, InteriorKit.InteriorFacingFor(4, 4, 8), "wraps");
            Assert.AreEqual(1, InteriorKit.InteriorFacingFor(5, 4, 8));
            Assert.AreEqual(7, InteriorKit.InteriorFacingFor(3, 4, 8));

            // C#'s % keeps the sign of the dividend, which is how an off-by-one turns into an
            // IndexOutOfRange three files away.
            Assert.AreEqual(3, InteriorKit.InteriorFacingFor(-1, 4, 8));
        }

        [Test]
        public void DoorOffsetMetres_FlipsTheScreenYIntoWorldY()
        {
            var e = Entry();
            e.doorX = new[] { 200f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            e.doorY = new[] { 290f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };   // BELOW the pivot on screen

            Vector2 offset = InteriorKit.DoorOffsetMetres(e, 0, 32);

            Assert.AreEqual(0f, offset.x, 1e-6f, "centred across the room");
            Assert.AreEqual(-(290f - 210f) / 32f, offset.y, 1e-6f,
                            "screen y grows DOWN and world y grows UP. Without the flip the threshold " +
                            "lands on the FAR wall — the same wrong place the facing offset guards " +
                            "against, and it looks identical.");
        }

        [Test]
        public void DoorOffsetMetres_IsSafeOnAContractThatPredatesTheAnchors()
        {
            Assert.AreEqual(Vector2.zero, InteriorKit.DoorOffsetMetres(Entry(), 0, 32));
            Assert.AreEqual(Vector2.zero, InteriorKit.DoorOffsetMetres(null, 0, 32));
        }

        // =============================================================================
        //  the import cap
        // =============================================================================

        [Test]
        public void TheImportCapIsTheCapThePackIsSolvedAgainst()
        {
            // #352's lesson made structural: the cap a kit imports at and the cap its pack is solved
            // for have to be the SAME number. The bake reads InteriorKit.ImportSizeCap into
            // InteriorBakeRequest.MaxSheetDimension and the slicer asserts against it — one constant,
            // which cannot disagree with itself.
            Assert.AreEqual(2048, InteriorKit.ImportSizeCap,
                            "raising this is legitimate if a room genuinely cannot pack; raising it in " +
                            "only one of the two places is the bug");
            Assert.LessOrEqual(InteriorKit.ImportSizeCap, 4096, "Unity's hard texture cap");
        }

        // =============================================================================
        //  the slicer's grid
        // =============================================================================

        [Test]
        public void BuildRects_LaysTheGridOutRowMajorFromTheTopLeft_AndStopsAtTheFacingCount()
        {
            var e = Entry();
            e.facings = 8; e.cols = 3; e.rows = 3;          // 9 cells for 8 facings — one is empty

            var rects = InteriorSheetSlicer.BuildRects(e, InteriorKit.RoomSpriteNameFor);

            Assert.AreEqual(8, rects.Length,
                            "the 9th cell gets no sprite: a transparent rect is something a placement " +
                            "tool would happily offer");

            // Facing 0 is the TOP-LEFT cell, which in Unity's bottom-origin rects is the highest y.
            Assert.AreEqual("Interior_test_d0", rects[0].name);
            Assert.AreEqual(0, rects[0].rect.x, 1e-3);
            Assert.AreEqual((e.rows - 1) * e.cellH, rects[0].rect.y, 1e-3);

            Assert.AreEqual("Interior_test_d3", rects[3].name);
            Assert.AreEqual(0, rects[3].rect.x, 1e-3, "facing 3 starts the second row");
            Assert.AreEqual((e.rows - 2) * e.cellH, rects[3].rect.y, 1e-3);
        }

        [Test]
        public void BuildRects_GivesEveryFacingTheSameFloorCentrePivot()
        {
            var e = Entry();
            Vector2 expected = InteriorKit.NormalizedPivot(e);

            foreach (var r in InteriorSheetSlicer.BuildRects(e, InteriorKit.RoomSpriteNameFor))
            {
                Assert.AreEqual(expected, r.pivot,
                                "ONE pivot for all eight — the union crop exists for exactly this, and " +
                                "a per-facing pivot would make the room jump as the building turned");
                Assert.AreEqual(SpriteAlignment.Custom, r.alignment,
                                "any corner preset is wrong for a floor centre");
            }
        }

        [Test]
        public void BuildRects_ReusesExistingSpriteIds_SoAReSliceIsANoOpOnTheMeta()
        {
            var e = Entry();
            var first = InteriorSheetSlicer.BuildRects(e, InteriorKit.RoomSpriteNameFor);

            var existing = new Dictionary<string, GUID>(StringComparer.Ordinal);
            foreach (var r in first) existing[r.name] = r.spriteID;

            var second = InteriorSheetSlicer.BuildRects(e, InteriorKit.RoomSpriteNameFor, existing);
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i].spriteID, second[i].spriteID,
                                "fresh GUIDs every run rewrite every spriteID — pure diff noise that " +
                                "buries the owner's real changes");
        }
    }
}
