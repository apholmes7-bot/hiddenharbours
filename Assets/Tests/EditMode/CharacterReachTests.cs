using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;      // MiniJson — the shared editor JSON reader

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the rig-6.6 REACH import: the sidecar's parse, the three clips' wiring on every
    /// character visual def, and the things neither of those can see on its own — that the numbers
    /// the code declares are the numbers the sidecar states and the PNGs actually carry, and that the
    /// clip's two timing points are the right way round.
    ///
    /// <para><b>Why a set-down needs guarding at all.</b> It is the one clip in the kit where being
    /// slightly wrong is invisible in a screenshot and obvious in motion. Release a frame early and
    /// the tool falls out of the hand before it lands; release at the seam and it teleports; read the
    /// frame → <c>u</c> mapping as cyclic and the settled rest is never drawn, so the character
    /// finishes reaching for something they never put down. Every one of those is a single number,
    /// and every one of them lives in two places that no compiler relates.</para>
    ///
    /// <para><b>The pixel half of this is not here.</b> <c>tools/rig-recipes/reach-continuity.mjs</c>
    /// re-renders all thirty sheets from the rig sources and byte-compares them against the committed
    /// PNGs, which is the proof that the art IS the rig's. This file guards the import around it.</para>
    /// </summary>
    public class CharacterReachTests
    {
        const string Iso = "Assets/_Project/Art/Characters/Iso/";
        const string VisualsFolder = "Assets/_Project/Data/Characters";

        /// <summary>The rod kit's own exported contract, used here as the cross-rig oracle for one
        /// number. Data, not code: the rod rig states its ground datum and this is where it lands.</summary>
        const string RodAnchors = "Assets/_Project/Art/Fishing/Iso/RodIsoAnchors.json";

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

        /// <summary>The three clips and the sheet suffix each is found by. One rig clip, three rest
        /// heights, so one frame count and one rate across all three.</summary>
        static readonly (CharacterClip clip, string suffix)[] Reaches =
        {
            (CharacterClip.ReachGround, "_reach_ground"),
            (CharacterClip.ReachStowV, "_reach_stowV"),
            (CharacterClip.ReachStowH, "_reach_stowH"),
        };

        const int Frames = 6;
        const float MsPerFrame = 100f;

        static string SheetPath(string stem, string folder, string suffix) =>
            folder == null ? $"{Iso}{stem}{suffix}.png" : $"{Iso}{folder}/{stem}{suffix}.png";

        static string SidecarText()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterReachBuilder.SidecarPath);
            Assert.IsNotNull(text,
                $"no sidecar at '{CharacterReachBuilder.SidecarPath}' — it ships beside the reach " +
                "sheets and must be committed with them");
            return text.text;
        }

        static CharacterReachDef ParseCommittedSidecar()
        {
            var def = ScriptableObject.CreateInstance<CharacterReachDef>();
            Assert.IsTrue(CharacterReachBuilder.TryParse(SidecarText(), def, out string error),
                          $"the committed sidecar did not parse: {error}");
            return def;
        }

        // ---- the enum ---------------------------------------------------------------------------

        [Test]
        public void TheReachIds_AreAppendedAfterDrive_NotRenumbered()
        {
            // CharacterClip names ART, and art ids are stable (CLAUDE.md §5). Renumbering would
            // re-point every serialized clip reference in the project at a different animation — an
            // asset-database migration disguised as a one-character edit.
            Assert.AreEqual(14, (int)CharacterClip.Drive, "Drive was the tail before 6.6");
            Assert.AreEqual(15, (int)CharacterClip.ReachGround);
            Assert.AreEqual(16, (int)CharacterClip.ReachStowV);
            Assert.AreEqual(17, (int)CharacterClip.ReachStowH);
        }

        [Test]
        public void OnlyTheReachClipsSettle()
        {
            // ClipSettles is what tells a player to STOP on the last frame rather than play through it.
            // Answering true for anything else would freeze that clip on its final frame forever.
            foreach (CharacterClip clip in System.Enum.GetValues(typeof(CharacterClip)))
                Assert.AreEqual(Reaches.Any(r => r.clip == clip), CharacterVisualDef.ClipSettles(clip),
                                $"{clip}: ClipSettles disagrees with the reach family");
        }

        // ---- the sidecar ------------------------------------------------------------------------

        [Test]
        public void Sidecar_Parses_IntoOneRowPerPreset()
        {
            var def = ParseCommittedSidecar();

            CollectionAssert.AreEquivalent(
                Presets.Select(p => p.visualId).ToArray(),
                def.Rows.Select(r => r.VisualId).ToArray(),
                "the sidecar's preset keys must map onto exactly the ten character visual def ids");
            Assert.AreEqual(CharacterReachBuilder.ReachId, def.Id, "the def id is append-only");
            Assert.IsNotEmpty(def.Rig, "the sidecar states the rig revision it was exported from");
        }

        [Test]
        public void Sidecar_TheToolIsHomeBeforeTheHandOpens()
        {
            // THE seam rule, and the reason the drop publishes two numbers instead of one. A rest that
            // releases at its own first frame is a teleport — which is exactly the defect the rod kit's
            // continuity law was written for. Asserted as an ORDER, not as two literals, so a retimed
            // clip that keeps the order still passes and one that inverts it cannot.
            var def = ParseCommittedSidecar();

            Assert.Less(def.ArriveAt, def.ReleaseAt,
                        "the tool must be HOME before the hand lets go of it");
            Assert.Greater(def.ArriveAt, 0f, "arriving at frame zero means it was never carried");
            Assert.Less(def.ReleaseAt, 1f, "releasing at the very end means the hand never lets go");

            Assert.AreEqual(0.62f, def.ArriveAt, 1e-4f, "the drop's arrive point");
            Assert.AreEqual(0.72f, def.ReleaseAt, 1e-4f, "the drop's release point");
        }

        [Test]
        public void Sidecar_TheFrameToUMapping_SettlesOnTheLastFrame()
        {
            // ⚠️ u = f/(frames−1), not f/frames. Every OTHER clip in the kit is cyclic; this one is
            // the exception, and the whole point of the exception is that the last frame lands on u = 1
            // so it can be HELD as the settled rest. Off by one and the character stops one step short
            // of putting the thing down.
            var def = ParseCommittedSidecar();

            Assert.AreEqual(Frames, def.Frames, "the drop's frame count");
            Assert.AreEqual(0f, def.UAtFrame(0), 1e-6f, "frame 0 is the hold pose the clip cuts out of");
            Assert.AreEqual(1f, def.UAtFrame(def.Frames - 1), 1e-6f,
                            "the LAST frame is the settled rest — a cyclic f/frames would land at " +
                            $"{(def.Frames - 1) / (float)def.Frames:0.###} and never draw it");

            // Gripped on 0-3, empty on 4-5, counted off the same mapping rather than written down.
            Assert.AreEqual(4, def.GrippedFrames, "frames still holding the tool");
            for (int f = 0; f < def.Frames; f++)
                Assert.AreEqual(f < 4, def.IsGripped(f), $"frame {f}: gripped");
        }

        [Test]
        public void Sidecar_APickUpIsThisClipReversed()
        {
            // There is no pick-up sheet and there should not be: a 0.72 release mirrors to a 0.28
            // grip-close. Published as one number so no call site derives it, because a pick-up that
            // closes at the wrong moment grabs at thin air.
            var def = ParseCommittedSidecar();
            Assert.AreEqual(1f - def.ReleaseAt, def.ReleaseAtReversed, 1e-6f);
            Assert.AreEqual(0.28f, def.ReleaseAtReversed, 1e-4f);
        }

        [Test]
        public void Sidecar_TheGripRise_IsTheRodKitsOwnGroundDatum()
        {
            // The one number the two kits genuinely share. The rod kit states how far a settled rod
            // holds its GRIP above whatever it rests on; on the ground, for a reeled rod, that is
            // 0.095 m — and it is the same 0.095 m the character clip raises the hand by. Pinned
            // against the rod's own exported anchors rather than a literal, so a re-bake of either
            // kit that moves it shows up as a disagreement instead of as two numbers drifting apart.
            //
            // ⚠️ This is the ONLY cross-kit lift pin, and restLift() at the two RACKS is deliberately
            // not one: the rod's number there is still grip-above-its-own-surface (0.16 / 0.62 m),
            // while the character's is that surface's HEIGHT. Comparing them is a category error, and
            // the rod rig has no way to know how high a rack is.
            var def = ParseCommittedSidecar();

            string json = File.ReadAllText(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", RodAnchors)));
            object root = MiniJson.Parse(json);
            var ground = MiniJson.Dict(
                MiniJson.Dict(MiniJson.Dict(MiniJson.Dict(root, "tiers"), "coast"), "states"), "ground");
            Assert.IsNotNull(ground, $"{RodAnchors}: no tiers.coast.states.ground block");

            float rodLift = MiniJson.Float(ground, "liftM", -1f);
            Assert.Greater(rodLift, 0f, $"{RodAnchors}: the rod kit states no ground liftM");
            Assert.AreEqual(rodLift, def.GripRiseM, 1e-4f,
                            "the character's grip rise and the rod's ground lift are the same datum " +
                            "seen from two kits; they must not drift apart");
        }

        [Test]
        public void Sidecar_TheRestHeightsAreOrdered_AndOnlyTheRacksClamp()
        {
            // Two invariants that catch a swapped or mis-keyed pair, which is otherwise invisible
            // because every value stays in range: the ground is the floor for everyone, and a rack is
            // above it. The CLAMP is the interesting half — a build that cannot reach the rack gets
            // the reach lowered to what it CAN touch, and the flag is what stops a consumer placing
            // the tool at a height the hand never gets to.
            var def = ParseCommittedSidecar();

            foreach (var row in def.Rows)
            {
                Assert.AreEqual(0f, row.Ground.LiftM, 1e-6f, $"{row.VisualId}: the ground is the floor");
                Assert.IsFalse(row.Ground.Clamped, $"{row.VisualId}: everyone can reach the floor");

                Assert.Greater(row.StowV.LiftM, row.Ground.LiftM, $"{row.VisualId}: a rack is above the floor");
                Assert.Greater(row.StowH.LiftM, row.Ground.LiftM, $"{row.VisualId}: a rack is above the floor");

                foreach (var (name, rest) in new[] { ("stowV", row.StowV), ("stowH", row.StowH) })
                {
                    Assert.AreEqual(rest.Clamped, rest.LiftM < rest.RequestedM - 1e-6f,
                                    $"{row.VisualId} {name}: the clamp flag must mean the height moved");
                    Assert.LessOrEqual(rest.LiftM, rest.RequestedM + 1e-6f,
                                       $"{row.VisualId} {name}: a clamp lowers a reach, never raises it");
                }
            }
        }

        [Test]
        public void Sidecar_TheClampedReaches_AreTheBuildsThatCannotReach()
        {
            // Named, because "some of them clamp" is not a contract. The two children cannot reach
            // either rack and one small adult cannot reach the high one; everybody else reaches both.
            // If a re-bake changes who clamps, that is a real change to the art and it should be read,
            // not absorbed.
            var def = ParseCommittedSidecar();

            var clamped = def.Rows
                .SelectMany(r => new (string id, string rest, bool clamped)[]
                {
                    (r.VisualId, "stowV", r.StowV.Clamped),
                    (r.VisualId, "stowH", r.StowH.Clamped),
                })
                .Where(x => x.clamped)
                .Select(x => $"{x.id}/{x.rest}")
                .OrderBy(x => x, System.StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "visual.boy_iso/stowH", "visual.boy_iso/stowV",
                        "visual.cutter_iso/stowH",
                        "visual.girl_iso/stowH", "visual.girl_iso/stowV" },
                clamped,
                "the set of builds whose reach is clamped at a rack");
        }

        [Test]
        public void Sidecar_TimingAgreesWithTheDefsClipInitialisers()
        {
            // The seam with no compiler relationship: the sidecar states MILLISECONDS per frame and
            // the def stores fps. ⚠️ ms, NOT fps — reading 100 as a frame rate runs the set-down a
            // hundred times too fast, and nothing else here would notice.
            var reach = ParseCommittedSidecar();
            var visual = ScriptableObject.CreateInstance<CharacterVisualDef>();

            Assert.AreEqual(MsPerFrame, reach.MillisecondsPerFrame, 1e-3f, "the drop's ms/frame");
            Assert.AreEqual(1000f / MsPerFrame, reach.FramesPerSecond, 1e-3f);

            foreach (var (clip, _) in Reaches)
            {
                var sheets = visual.ClipSheetsFor(clip);
                Assert.IsNotNull(sheets, $"{clip}: CharacterVisualDef has no clip block for it");
                Assert.AreEqual(reach.Frames, sheets.FrameCount,
                                $"{clip}: the def's FrameCount disagrees with the sidecar");
                Assert.AreEqual(reach.FramesPerSecond, sheets.FramesPerSecond, 1e-3f,
                                $"{clip}: the def's rate disagrees with the sidecar's " +
                                $"{reach.MillisecondsPerFrame} ms/frame");
                Assert.IsFalse(sheets.Loops,
                               $"{clip}: a set-down does not loop — it settles and is held");
            }
        }

        // ---- the negative controls --------------------------------------------------------------

        static readonly (string name, string json)[] BadSidecars =
        {
            ("empty", ""),
            ("whitespace", "   \n  "),
            ("not JSON at all", "this is not json {{{"),
            ("a JSON array, not an object", "[1, 2, 3]"),
            ("an object with no clips block", "{\"presets\":[{\"key\":\"fisher\"}]}"),
            ("a reach clip but no presets list",
             "{\"clips\":{\"reach\":{\"frames\":6}},\"presets\":[]}"),
            // The trap this importer exists to name: the OFF-DECK sidecar keys its presets by NAME,
            // this one lists them. Reading the wrong shape finds nothing and would import an empty
            // def, which every consumer then degrades past in silence.
            ("presets keyed like the off-deck sidecar, not listed",
             "{\"clips\":{\"reach\":{\"frames\":6}},\"presets\":{\"fisher\":{\"rests\":{}}}}"),
            ("presets whose every row is malformed",
             "{\"clips\":{\"reach\":{\"frames\":6}},\"presets\":[{\"key\":\"fisher\"},{\"nope\":1}]}"),
        };

        [Test]
        public void Sidecar_Refuses_RatherThanHalfFilling()
        {
            // A half-filled def is the failure that matters: it reports success and then places every
            // tool at a height nobody measured. Refusing is the only safe answer.
            foreach (var (name, json) in BadSidecars)
            {
                if (name.StartsWith("presets whose every row"))
                    for (int i = 0; i < 2; i++)                    // one per malformed row
                        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                            "a preset entry has no 'key' or no 'rests' block"));

                var def = ScriptableObject.CreateInstance<CharacterReachDef>();
                def.Rows = new[] { new CharacterReachRow { VisualId = "visual.sentinel" } };

                bool ok = CharacterReachBuilder.TryParse(json, def, out string error);

                Assert.IsFalse(ok, $"'{name}' must not parse");
                Assert.IsNotEmpty(error ?? "", $"'{name}' must say WHY it refused");
                Assert.AreEqual(1, def.Rows.Length,
                                $"'{name}': a refused parse must leave the def untouched, not clear " +
                                "or half-fill it");
                Assert.AreEqual("visual.sentinel", def.Rows[0].VisualId, $"'{name}'");
            }
        }

        [Test]
        public void Sidecar_RefusesAReleaseThatBeatsTheArrival()
        {
            // A sidecar that inverts the two would import cleanly and tell every consumer to drop the
            // tool before it lands. Well-formed and wrong is exactly the case a parse must catch.
            const string json =
                "{\"clips\":{\"reach\":{\"frames\":6,\"ms_per_frame\":100," +
                "\"release_at\":0.4,\"arrive_at\":0.8}}," +
                "\"presets\":[{\"key\":\"fisher\",\"rests\":{" +
                "\"ground\":{\"lift_m\":0},\"stowV\":{\"lift_m\":0.95},\"stowH\":{\"lift_m\":1.05}}}]}";

            var def = ScriptableObject.CreateInstance<CharacterReachDef>();
            def.Rows = new[] { new CharacterReachRow { VisualId = "visual.sentinel" } };

            Assert.IsFalse(CharacterReachBuilder.TryParse(json, def, out string error),
                           "a release before the arrival must be refused");
            StringAssert.Contains("release_at", error ?? "");
            Assert.AreEqual("visual.sentinel", def.Rows[0].VisualId,
                            "a refused parse must leave the def untouched — the presets in that " +
                            "sidecar were well-formed, so the refusal must come BEFORE anything lands");
        }

        [Test]
        public void Sidecar_SkipsOneBadPreset_ButKeepsTheGoodOnes()
        {
            // The other half of the house rule: degrade per element. One malformed preset must not
            // cost the other nine their rest heights.
            const string json =
                "{\"clips\":{\"reach\":{\"frames\":6,\"ms_per_frame\":100," +
                "\"release_at\":0.72,\"arrive_at\":0.62}}," +
                "\"presets\":[" +
                "{\"key\":\"fisher\",\"rests\":{\"ground\":{\"lift_m\":0}," +
                "\"stowV\":{\"lift_m\":0.95},\"stowH\":{\"lift_m\":1.05}}}," +
                "{\"key\":\"ginny\",\"rests\":{\"ground\":{\"lift_m\":0}}}" +   // no rack blocks
                "]}";

            var def = ScriptableObject.CreateInstance<CharacterReachDef>();

            // The skip is LOUD by design (importer house rule), so the warning is part of the contract
            // and must be consumed here or the runner fails the case on an unexpected log.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "preset 'ginny' is missing one of its three rest blocks"));

            Assert.IsTrue(CharacterReachBuilder.TryParse(json, def, out string error),
                          $"a sidecar with one good preset must still parse: {error}");
            Assert.AreEqual(1, def.Rows.Length, "only the well-formed preset should land");
            Assert.AreEqual("visual.fisher_iso", def.Rows[0].VisualId);
        }

        // ---- the def's own lookups ---------------------------------------------------------------

        [Test]
        public void Rest_IsOfferedForTheReachClips_AndRefusedForTheRest()
        {
            // A haul or a swim rests on nothing. Answering them with a height would be a
            // plausible-looking number attached to the wrong animation.
            var def = ParseCommittedSidecar();

            Assert.IsTrue(def.TryGetRest("visual.fisher_iso", CharacterClip.ReachGround, out var ground));
            Assert.AreEqual(0f, ground.LiftM, 1e-6f);
            Assert.IsTrue(def.TryGetRest("visual.fisher_iso", CharacterClip.ReachStowV, out var stowV));
            Assert.AreEqual(0.95f, stowV.LiftM, 1e-4f);
            Assert.IsTrue(def.TryGetRest("visual.fisher_iso", CharacterClip.ReachStowH, out var stowH));
            Assert.AreEqual(1.05f, stowH.LiftM, 1e-4f);

            Assert.IsFalse(def.TryGetRest("visual.fisher_iso", CharacterClip.Haul, out _));
            Assert.IsFalse(def.TryGetRest("visual.fisher_iso", CharacterClip.Swim, out _));
            Assert.IsFalse(def.TryGetRest("visual.fisher_iso", CharacterClip.None, out _));
        }

        [Test]
        public void SettledGripZ_IsTheSurfacePlusTheGripRise()
        {
            // The number that places a TOOL, as opposed to the number that places the furniture. Kept
            // as a call rather than a stored field so the two cannot disagree — and computed off the
            // CLAMPED height, which is why a child's settled grip sits below an adult's at the same rack.
            var def = ParseCommittedSidecar();

            Assert.IsTrue(def.TryGetSettledGripZ("visual.fisher_iso", CharacterClip.ReachStowH, out float adult));
            Assert.AreEqual(1.05f + def.GripRiseM, adult, 1e-4f);

            Assert.IsTrue(def.TryGetSettledGripZ("visual.girl_iso", CharacterClip.ReachStowH, out float child));
            Assert.Less(child, adult,
                        "a build whose reach is clamped settles the tool LOWER than one that reaches " +
                        "the rack — placing it at the rack's own height would hang it above her hand");

            Assert.IsFalse(def.TryGetSettledGripZ("visual.nobody_iso", CharacterClip.ReachGround, out _));
        }

        [Test]
        public void UnknownVisualId_IsRefusedRatherThanGuessed()
        {
            var def = ParseCommittedSidecar();

            Assert.IsFalse(def.TryGetRow("visual.nobody_iso", out _));
            Assert.IsFalse(def.TryGetRow(null, out _));
            Assert.IsFalse(def.TryGetRow("", out _));
        }

        // ---- the art, and the all-or-nothing gate -------------------------------------------------

        [Test]
        public void EveryPreset_ShipsAllThreeReachSheets_AtTheDeclaredFrameCount()
        {
            // The counts, read off the PNGs themselves. A 64 px cell means width/64 IS the frame
            // count, so a re-export that lengthened the set-down shows up here as plain arithmetic.
            const int CellW = 64;

            foreach (var (stem, folder, _) in Presets)
            foreach (var (clip, suffix) in Reaches)
            {
                string path = SheetPath(stem, folder, suffix);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"{path}: missing — all ten presets bake all three rests");
                Assert.AreEqual(Frames * CellW, tex.width,
                                $"{path}: {clip} is {Frames} frames, so the sheet must be " +
                                $"{Frames * CellW} px wide, not {tex.width}");
            }
        }

        [Test]
        public void EveryCharacterDef_WiresAllThreeReachClips()
        {
            // The plumbed-but-unread failure, asserted as asset state: a clip can be on the enum, on
            // the def's fields and baked to a sheet, and still be wired by nobody — every code path
            // answering "false, correctly" the whole time.
            //
            // ⚠️ Red until the last mile runs. The sheets land in this PR without .meta files; the
            // import is Art ▸ Import (after a new drop) ▸ … and then Build Character Visual Defs.
            foreach (var (_, _, visualId) in Presets)
            {
                string path = $"{VisualsFolder}/{AssetNameFor(visualId)}.asset";
                var def = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(path);
                Assert.IsNotNull(def, $"{path} is missing — run Build Character Visual Defs.");

                foreach (var (clip, _) in Reaches)
                    Assert.IsTrue(def.HasClip(clip),
                        $"'{clip}' is declared and baked but NOT wired on {path} — re-slice the sheets " +
                        "and re-run Build Character Visual Defs. A clip in this state never plays and " +
                        "no runtime path reports it.");
            }
        }

        [Test]
        public void EveryCastPreset_HasARunSheet_AndARunGaitWiredToIt()
        {
            // ⚠️ THIS FIXTURE USED TO BE TheTwoPresetsWithARunSheet_HaveARunGait_AndTheRestDoNot, and
            // the rename is the finding. The 6.6 drop had completed the run for exactly two of the
            // cast — Ginny and Skipper — so the honest assertion then was "these two and no others",
            // read off the ART rather than assumed.
            //
            // Rig 6.9 (2026-09-02) closed the gap, and not by shipping seven more PNGs: it is a FACE
            // pass in which every one of 100 measured (preset × anim) cells moved, so leaving seven
            // casts without a run while the other two ran with a 6.6 face was no longer a tenable
            // asymmetry. `run` joined CharacterRigBakeMenu.CastStates and the seven missing sheets
            // were baked in-engine at the 64 × 92 locomotion lane (the drop's own run sheets are
            // windowed at the OFF-DECK 88 and are the wrong cell for a gait).
            //
            // The property that MATTERS is unchanged and is the second assert: the gait is wired
            // exactly when the sheet exists, because a run that half-wired would index a stale cell
            // mid-stride. What changed is that the answer is now "all of them" — so this asks the
            // ART, as before, and requires the ART to be complete.
            foreach (var (stem, folder, visualId) in Presets)
            {
                if (visualId == "visual.fisher_iso") continue;      // the player has always had one

                string sheet = SheetPath(stem, folder, "_run");
                bool artExists = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet) != null;
                Assert.IsTrue(artExists,
                              $"{sheet}: every cast preset has run since rig 6.9 — CastStates bakes " +
                              "it. A missing sheet means the bake was not re-run, or a preset was " +
                              "added to the cast without one.");

                string path = $"{VisualsFolder}/{AssetNameFor(visualId)}.asset";
                var def = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(path);
                Assert.IsNotNull(def, $"{path} is missing — run Build Character Visual Defs.");
                Assert.AreEqual(artExists, def.HasGait(CharacterGait.Run),
                                $"{path}: the run gait must be wired exactly when the sheet exists — " +
                                "the builder asks for all three gaits and the all-or-nothing gate " +
                                "decides, so a preset with no run art simply never shows one.");
            }
        }

        /// <summary>The def asset behind a visual id: <c>visual.ginny_iso</c> → <c>GinnyIso</c>. The
        /// stem's display capitalisation is the Presets table's, not a re-derivation of the id.</summary>
        static string AssetNameFor(string visualId) =>
            Presets.First(p => p.visualId == visualId).stem + "Iso";
    }
}
