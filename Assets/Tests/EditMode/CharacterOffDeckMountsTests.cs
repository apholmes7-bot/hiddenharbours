using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the rig-6.5 OFF-DECK import: the mount sidecar's parse, the four clips' wiring on every
    /// character visual def, and the one thing neither of those can see — that the frame counts the
    /// code declares are the frame counts the PNGs actually carry.
    ///
    /// <para><b>Why the sidecar is asserted against the ART and the CODE both.</b> The numbers exist in
    /// three places that no compiler relates: the JSON on disk, the field initialisers on
    /// <see cref="CharacterVisualDef"/>'s clip blocks, and the PNG widths. A re-export that lengthens
    /// the swim stroke updates the first and the third and silently disagrees with the second, which
    /// plays the sheet at the wrong rate and loops it a frame short — visible, but only if you are
    /// looking. Comparing all three directly is the only thing that catches it.</para>
    ///
    /// <para><b>The parse has a negative control</b>, because a permissive reader that half-fills a def
    /// is worse than one that refuses: a def with three of ten water lines draws seven characters at
    /// the wrong depth and reports success.</para>
    /// </summary>
    public class CharacterOffDeckMountsTests
    {
        const string Iso = "Assets/_Project/Art/Characters/Iso/";

        /// <summary>The ten presets, as sheet stem + folder + the visual def id they build into.
        /// <c>Fisher</c> has no folder — the player bakes at the Iso root.</summary>
        static readonly (string stem, string folder, string visualId)[] Presets =
        {
            ("Fisher", null, "visual.fisher_iso"),
            ("Ginny", "ginny", "visual.ginny_iso"),
            ("Skipper", "skipper", "visual.skipper_iso"),
            ("Nan", "nan", "visual.nan_iso"),
            ("DeckBoss", "deckboss", "visual.deckboss_iso"),
            ("Packer", "packer", "visual.packer_iso"),
            ("Cutter", "cutter", "visual.cutter_iso"),
            ("Hand", "hand", "visual.hand_iso"),
            ("Boy", "boy", "visual.boy_iso"),
            ("Girl", "girl", "visual.girl_iso"),
        };

        /// <summary>The four off-deck clips and the frame count each sheet must carry. Restated as the
        /// contract, then checked against the sidecar, the def and the PNGs in turn.</summary>
        static readonly (CharacterClip clip, string suffix, int frames, float ms)[] OffDeck =
        {
            (CharacterClip.Swim,  "_swim",  8, 130f),
            (CharacterClip.Tread, "_tread", 6, 170f),
            (CharacterClip.Sleep, "_sleep", 6, 640f),
            (CharacterClip.Drive, "_drive", 6, 170f),
        };

        static string SheetPath(string stem, string folder, string suffix) =>
            folder == null ? $"{Iso}{stem}{suffix}.png" : $"{Iso}{folder}/{stem}{suffix}.png";

        static string SidecarText()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(
                CharacterOffDeckMountsBuilder.SidecarPath);
            Assert.IsNotNull(text,
                $"no sidecar at '{CharacterOffDeckMountsBuilder.SidecarPath}' — it ships beside the " +
                "off-deck sheets and must be committed with them");
            return text.text;
        }

        static CharacterOffDeckMountsDef ParseCommittedSidecar(
            out Dictionary<CharacterClip, CharacterOffDeckMountsBuilder.AnimTiming> anims)
        {
            var def = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();
            Assert.IsTrue(
                CharacterOffDeckMountsBuilder.TryParse(SidecarText(), def, out anims, out string error),
                $"the committed sidecar did not parse: {error}");
            return def;
        }

        // ---- the sidecar ------------------------------------------------------------------------

        [Test]
        public void Sidecar_Parses_IntoOneRowPerPreset()
        {
            var def = ParseCommittedSidecar(out _);

            CollectionAssert.AreEquivalent(
                Presets.Select(p => p.visualId).ToArray(),
                def.Rows.Select(r => r.VisualId).ToArray(),
                "the sidecar's preset keys must map onto exactly the ten character visual def ids. " +
                "⚠️ The key is lower-cased to build the id, so 'Deckboss' → visual.deckboss_iso — " +
                "which is correct, and is NOT the sheet stem (DeckBoss, capital B).");
        }

        [Test]
        public void Sidecar_CarriesTheFishersMeasuredWaterLines()
        {
            var def = ParseCommittedSidecar(out _);
            Assert.IsTrue(def.TryGetRow("visual.fisher_iso", out var fisher), "no row for the player");

            // Spot-check the one build every other number is relative to, so a wholesale unit change
            // (metres → pixels, ms → fps) cannot pass by shifting everything together.
            Assert.AreEqual(0.4488f, fisher.Swim.WaterZ, 1e-4f, "fisher swim waterZ");
            Assert.AreEqual(69, fisher.Swim.WaterRowLane, "fisher swim lane row");
            Assert.AreEqual(1.0047f, fisher.Tread.WaterZ, 1e-4f, "fisher tread waterZ");
            Assert.AreEqual(55, fisher.Tread.WaterRowLane, "fisher tread lane row");
        }

        [Test]
        public void Sidecar_TreadCutsHigherUpTheBodyThanSwim_OnEveryBuild()
        {
            // The invariant that catches a swapped pair — which is otherwise invisible, because both
            // blocks are well-formed and every value stays in range. Treading stands the character UP,
            // so the sea reaches further up the body (bigger waterZ) and therefore crosses the art
            // HIGHER, which is a SMALLER row index counting down from the cell top.
            var def = ParseCommittedSidecar(out _);

            foreach (var row in def.Rows)
            {
                Assert.Greater(row.Tread.WaterZ, row.Swim.WaterZ,
                               $"{row.VisualId}: treading must cut higher up the body than swimming");
                Assert.Less(row.Tread.WaterRowLane, row.Swim.WaterRowLane,
                            $"{row.VisualId}: the tread lane row must sit ABOVE the swim one " +
                            "(smaller row = higher in the cell)");
            }
        }

        [Test]
        public void Sidecar_EveryLaneRow_IsInsideTheOffDeckCell()
        {
            // ⚠️ 88, not 92. These lane rows are read against the OFF-DECK cell; a row of 89 would mean
            // the sidecar was exported against the locomotion cell and every waterline is wrong by two.
            const int OffDeckCellH = 88;
            var def = ParseCommittedSidecar(out _);

            foreach (var row in def.Rows)
            foreach (var (name, line) in new[] { ("swim", row.Swim), ("tread", row.Tread) })
            {
                Assert.Greater(line.WaterRowLane, 0, $"{row.VisualId} {name}: lane row must be positive");
                Assert.Less(line.WaterRowLane, OffDeckCellH,
                            $"{row.VisualId} {name}: lane row {line.WaterRowLane} falls outside the " +
                            $"{OffDeckCellH}px off-deck cell — was the sidecar exported at the 92 cell?");
                Assert.Greater(line.WaterZ, 0f, $"{row.VisualId} {name}: waterZ must be positive");
            }
        }

        [Test]
        public void Sidecar_GlobalMounts_AreTheContractsOwnNumbers()
        {
            // These four are global in the DATA, not per preset — a bed and a driving seat belong to
            // the furniture and the machine. Modelled as single fields for that reason; if a future
            // sidecar makes them per-preset, this test is the thing that must change first.
            var def = ParseCommittedSidecar(out _);

            Assert.AreEqual(0.30f, def.SleepBedZ, 1e-4f, "mattress height, world metres");
            Assert.AreEqual(0.40f, def.DriveSeatZ, 1e-4f, "seat height, world metres");
            Assert.AreEqual(0.655f, def.DriveWheelZ, 1e-4f, "wheel height, world metres");
            Assert.AreEqual(0.315f, def.DriveWheelY, 1e-4f, "wheel forward of the seat, world metres");
        }

        [Test]
        public void Sidecar_AnimTimings_AgreeWithTheDefsClipInitialisers()
        {
            // The seam with no compiler relationship: the sidecar states ms/frame, the def stores fps.
            // ⚠️ ms, NOT fps — reading 130 as a frame RATE runs the swim stroke seventeen times too
            // fast, and nothing else here would notice.
            ParseCommittedSidecar(out var anims);
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();

            foreach (var (clip, _, frames, ms) in OffDeck)
            {
                Assert.IsTrue(anims.TryGetValue(clip, out var timing),
                              $"the sidecar declares no timing for {clip}");
                Assert.AreEqual(frames, timing.Frames, $"{clip}: sidecar frame count");
                Assert.AreEqual(ms, timing.MillisecondsPerFrame, 1e-3f, $"{clip}: sidecar ms/frame");

                var sheets = def.ClipSheetsFor(clip);
                Assert.IsNotNull(sheets, $"{clip}: CharacterVisualDef has no clip block for it");
                Assert.AreEqual(timing.Frames, sheets.FrameCount,
                                $"{clip}: the def's FrameCount disagrees with the sidecar");
                Assert.AreEqual(timing.FramesPerSecond, sheets.FramesPerSecond, 1e-3f,
                                $"{clip}: the def's rate disagrees with the sidecar's {ms} ms/frame " +
                                $"(= {timing.FramesPerSecond:0.###} fps)");
                Assert.IsTrue(sheets.Loops,
                              $"{clip}: all four off-deck clips LOOP — none of them is a transition");
            }
        }

        // ---- the negative controls --------------------------------------------------------------

        static readonly (string name, string json)[] BadSidecars =
        {
            ("empty", ""),
            ("whitespace", "   \n  "),
            ("not JSON at all", "this is not json {{{"),
            ("a JSON array, not an object", "[1, 2, 3]"),
            ("an object with no presets block", "{\"rig\":\"characterIsoRig6.js rev 6.5\"}"),
            ("a presets block with nothing in it", "{\"presets\":{}}"),
            ("presets whose every row is malformed",
             "{\"presets\":{\"Fisher\":{\"swim\":{\"waterZ\":0.4}},\"Ginny\":{\"nope\":1}}}"),
        };

        [Test]
        public void Sidecar_Refuses_RatherThanHalfFilling()
        {
            // A half-filled def is the failure that matters: it reports success and then draws seven
            // of ten characters at the wrong depth. Refusing is the only safe answer.
            foreach (var (name, json) in BadSidecars)
            {
                var def = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();
                def.Rows = new[] { new CharacterOffDeckRow { VisualId = "visual.sentinel" } };

                bool ok = CharacterOffDeckMountsBuilder.TryParse(json, def, out _, out string error);

                Assert.IsFalse(ok, $"'{name}' must not parse");
                Assert.IsNotEmpty(error ?? "", $"'{name}' must say WHY it refused");
                Assert.AreEqual(1, def.Rows.Length,
                                $"'{name}': a refused parse must leave the def untouched, not clear " +
                                "or half-fill it");
                Assert.AreEqual("visual.sentinel", def.Rows[0].VisualId, $"'{name}'");
            }
        }

        [Test]
        public void Sidecar_SkipsOneBadPreset_ButKeepsTheGoodOnes()
        {
            // The other half of the house rule: degrade per element. One malformed preset must not
            // cost the other nine their water lines.
            const string json =
                "{\"presets\":{" +
                "\"Fisher\":{\"swim\":{\"waterZ\":0.4488,\"waterRowLane\":69}," +
                            "\"tread\":{\"waterZ\":1.0047,\"waterRowLane\":55}}," +
                "\"Ginny\":{\"swim\":{\"waterZ\":0.42,\"waterRowLane\":70}}" +   // no tread block
                "}}";

            var def = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();

            // The skip is LOUD by design (importer house rule), so the warning is part of the contract
            // and must be consumed here or the test runner fails the case on an unexpected log.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "preset 'Ginny' is missing its swim or tread block"));

            Assert.IsTrue(CharacterOffDeckMountsBuilder.TryParse(json, def, out _, out string error),
                          $"a sidecar with one good preset must still parse: {error}");
            Assert.AreEqual(1, def.Rows.Length, "only the well-formed preset should land");
            Assert.AreEqual("visual.fisher_iso", def.Rows[0].VisualId);
        }

        // ---- the def's own lookups ---------------------------------------------------------------

        [Test]
        public void WaterLine_IsOfferedForTheWaterAnims_AndRefusedForTheRest()
        {
            // Sleep and drive are pinned to a mattress and a seat, not to the sea. Answering them with
            // a water line would be a plausible-looking number in the wrong units.
            var def = ParseCommittedSidecar(out _);

            Assert.IsTrue(def.TryGetWaterLine("visual.fisher_iso", CharacterClip.Swim, out var swim));
            Assert.AreEqual(69, swim.WaterRowLane);
            Assert.IsTrue(def.TryGetWaterLine("visual.fisher_iso", CharacterClip.Tread, out _));

            Assert.IsFalse(def.TryGetWaterLine("visual.fisher_iso", CharacterClip.Sleep, out _),
                           "sleep has no water line");
            Assert.IsFalse(def.TryGetWaterLine("visual.fisher_iso", CharacterClip.Drive, out _),
                           "drive has no water line");
            Assert.IsFalse(def.TryGetWaterLine("visual.fisher_iso", CharacterClip.Haul, out _),
                           "an unrelated clip has no water line");
        }

        [Test]
        public void UnknownVisualId_IsRefusedRatherThanGuessed()
        {
            var def = ParseCommittedSidecar(out _);

            Assert.IsFalse(def.TryGetRow("visual.nobody_iso", out _));
            Assert.IsFalse(def.TryGetRow(null, out _));
            Assert.IsFalse(def.TryGetRow("", out _));
        }

        // ---- the art, and the all-or-nothing gate -------------------------------------------------

        [Test]
        public void EveryPreset_ShipsAllFourOffDeckSheets_AtTheDeclaredFrameCount()
        {
            // The counts, read off the PNGs themselves. A 64 px cell means width/64 IS the frame count,
            // so a re-export that lengthened an anim shows up here as a plain arithmetic mismatch.
            const int CellW = 64;

            foreach (var (stem, folder, _) in Presets)
            foreach (var (clip, suffix, frames, _) in OffDeck)
            {
                string path = SheetPath(stem, folder, suffix);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"{path}: missing — all ten presets bake all four off-deck anims");
                Assert.AreEqual(frames * CellW, tex.width,
                                $"{path}: {clip} is {frames} frames, so the sheet must be " +
                                $"{frames * CellW} px wide, not {tex.width}");
            }
        }

        [Test]
        public void EveryVisualDef_WiresAllFourOffDeckClips()
        {
            // The gate is all-or-nothing (HasClip), so this is not "the field is set" — it is "the
            // sheet sliced into exactly FacingCount × FrameCount ordered sprites". A short slice drops
            // the clip whole and silently, which is precisely why it needs asserting.
            var allDefs = AssetDatabase.FindAssets("t:CharacterVisualDef")
                                       .Select(AssetDatabase.GUIDToAssetPath)
                                       .Select(AssetDatabase.LoadAssetAtPath<CharacterVisualDef>)
                                       .Where(d => d != null)
                                       .ToArray();

            foreach (var (_, _, visualId) in Presets)
            {
                var def = allDefs.FirstOrDefault(d => d.Id == visualId);
                Assert.IsNotNull(def, $"no CharacterVisualDef with id '{visualId}'");

                foreach (var (clip, suffix, frames, _) in OffDeck)
                {
                    var sheets = def.ClipSheetsFor(clip);
                    Assert.IsNotNull(sheets, $"{visualId}: no clip block for {clip}");
                    Assert.AreEqual(frames, sheets.FrameCount, $"{visualId} {clip}: frame count");
                    Assert.IsTrue(def.HasClip(clip),
                                  $"{visualId}: {clip} is not wired. The sheet is " +
                                  $"'<preset>{suffix}.png' and it must slice into " +
                                  $"{def.FacingCount} × {frames} = {def.FacingCount * frames} ordered " +
                                  "sprites. Re-run Slice Iso Character Sheets, then Build Character " +
                                  "Visual Defs.");
                }
            }
        }

        [Test]
        public void TheBuiltMountsAsset_IsCommitted_AndMatchesTheSidecar()
        {
            var built = AssetDatabase.LoadAssetAtPath<CharacterOffDeckMountsDef>(
                CharacterOffDeckMountsBuilder.MountsPath);
            Assert.IsNotNull(built,
                $"no built asset at '{CharacterOffDeckMountsBuilder.MountsPath}' — run Hidden Harbours ▸ " +
                "Art ▸ Import (after a new drop) ▸ Build Off-Deck Mounts and commit it");

            Assert.AreEqual(CharacterOffDeckMountsBuilder.MountsId, built.Id);

            var fresh = ParseCommittedSidecar(out _);
            Assert.AreEqual(fresh.Rows.Length, built.Rows.Length,
                            "the committed asset has drifted from the sidecar — re-run the builder");
            Assert.AreEqual(fresh.SleepBedZ, built.SleepBedZ, 1e-4f);
            Assert.AreEqual(fresh.DriveSeatZ, built.DriveSeatZ, 1e-4f);

            foreach (var row in fresh.Rows)
            {
                Assert.IsTrue(built.TryGetRow(row.VisualId, out var got),
                              $"the committed asset is missing '{row.VisualId}'");
                Assert.AreEqual(row.Swim.WaterRowLane, got.Swim.WaterRowLane, $"{row.VisualId} swim lane");
                Assert.AreEqual(row.Tread.WaterZ, got.Tread.WaterZ, 1e-4f, $"{row.VisualId} tread waterZ");
            }
        }
    }
}
