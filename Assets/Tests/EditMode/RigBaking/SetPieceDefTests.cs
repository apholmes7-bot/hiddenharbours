using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.Tests.RigBaking.StPetersSetPiecesIntakeTests;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The St Peters set pieces as the game holds them: one <see cref="SetPieceDef"/> per kit id, every
    /// derived field equal to what the kit's own files say, and sheets that are the kit's own pixels.
    ///
    /// <para><b>The bar is the sidecar FILE, never the builder.</b> Each Def is compared with the delivered
    /// sidecar for what no placement moves (identity, footprints, facings, the light rule, the vane), and
    /// with the today block for what the key scenes moved (bounds, cells, anchors, options: the tide board's
    /// foot and the trolley line's path). <see cref="StPetersSetPiecesIntakeTests"/> proves those two differ
    /// nowhere else.</para>
    ///
    /// <para><b>No Ignore.</b> The Defs and sheets come from one editor run (Hidden Harbours ▸ Art ▸ Bake St
    /// Peters Set Pieces (today)). A checkout without them is a checkout without the pieces, and reds.
    /// The last two tests need no bake.</para>
    /// </summary>
    public class SetPieceDefTests
    {
        const string DefFolder = "Assets/_Project/Data/SetPieces";
        const string SheetFolder = "Assets/_Project/Art/Sprites/StPeters/SetPieces";
        const string ContractPath = SheetFolder + "/stPetersSetPieces.contract.json";
        const string BoilerId = "structure.stp_cannery_boiler_stack";
        const string VaneId = "prop.stp_harbour_vane";
        const string Bake = "Phase B bakes and builds them: Hidden Harbours/Art/Bake St Peters Set Pieces (today).";

        static SetPieceDef[] AllDefs() =>
            AssetDatabase.FindAssets($"t:{nameof(SetPieceDef)}", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SetPieceDef>)
                .Where(d => d != null)
                .ToArray();

        static SetPieceDef DefFor(string id)
        {
            var hits = AllDefs().Where(d => d.Id == id).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1), $"{id}: {hits.Length} SetPieceDefs. {Bake}");
            return hits[0];
        }

        static string SheetPath(string key) => $"{SheetFolder}/SetPiece_{key}.png";

        static SetPieceSheetSlicer.Contract Contract()
        {
            FileAssert.Exists(Abs(ContractPath), $"no bake contract at {ContractPath}. {Bake}");
            var contract = JsonUtility.FromJson<SetPieceSheetSlicer.Contract>(File.ReadAllText(Abs(ContractPath)));
            Assert.That(contract?.schema, Is.EqualTo("hidden-harbours/st-peters-set-pieces-contract@1"));
            return contract;
        }

        static SetPieceSheetSlicer.Piece Sheet(SetPieceSheetSlicer.Contract contract, string key)
        {
            var hits = contract.pieces.Where(p => p.key == key).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1), $"{key}: {hits.Length} sheets in the contract.");
            return hits[0];
        }

        static Vector2 V2(object v)
        {
            var a = Items(v);
            Assert.That(a.Count, Is.EqualTo(2), "expected [x, y]");
            return new Vector2((float)Num(a[0]), (float)Num(a[1]));
        }

        static Vector3 V3(object v)
        {
            var a = Items(v);
            Assert.That(a.Count, Is.EqualTo(3), "expected [x, y, z]");
            return new Vector3((float)Num(a[0]), (float)Num(a[1]), (float)Num(a[2]));
        }

        static void AssertNear(Vector2 got, Vector2 want, string what)
        {
            Assert.That(got.x, Is.EqualTo(want.x).Within(1e-5f), what + " x");
            Assert.That(got.y, Is.EqualTo(want.y).Within(1e-5f), what + " y");
        }

        static void AssertNear(Vector3 got, Vector3 want, string what)
        {
            Assert.That(got.x, Is.EqualTo(want.x).Within(1e-5f), what + " x");
            Assert.That(got.y, Is.EqualTo(want.y).Within(1e-5f), what + " y");
            Assert.That(got.z, Is.EqualTo(want.z).Within(1e-5f), what + " z");
        }

        /// <summary>The kit writes anchor pixels to one decimal: ±0.05, and a hair for float32.</summary>
        static void AssertPx(Vector2 got, object want, string what)
        {
            var w = Items(want);
            Assert.That(got.x, Is.EqualTo(Num(w[0])).Within(0.051), what + " x");
            Assert.That(got.y, Is.EqualTo(Num(w[1])).Within(0.051), what + " y");
        }

        static List<object> AnchorList(Dictionary<string, object> anchors, string name) =>
            anchors.TryGetValue(name, out object v) ? Items(v) : new List<object>();

        // ---- the Defs ---------------------------------------------------------------------------------------

        [Test]
        public void EveryPieceHasExactlyOneSetPieceDef()
        {
            var defs = AllDefs();
            foreach (var (key, id) in Pieces)
            {
                var hits = defs.Where(d => d.Id == id).ToArray();
                Assert.That(hits.Length, Is.EqualTo(1), $"{id}: {hits.Length} SetPieceDefs. {Bake}");
                Assert.That(AssetDatabase.GetAssetPath(hits[0]), Is.EqualTo($"{DefFolder}/SetPiece_{key}.asset"), id);
            }
            Assert.That(defs.GroupBy(d => d.Id).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty,
                "Two SetPieceDefs share an id.");
            Assert.That(defs.Where(d => d.Id.StartsWith("fx.", StringComparison.Ordinal)).Select(d => d.Id), Is.Empty,
                "The cannery's smoke plume is a runtime overlay for the restoration, not a set piece.");
            Assert.That(defs.Where(d => d.Id.EndsWith("_restored", StringComparison.Ordinal)).Select(d => d.Id), Is.Empty,
                "Only the today options are baked.");
        }

        [Test]
        public void EachDefCarriesWhatItsSidecarFileSays()
        {
            foreach (var (key, id) in Pieces)
            {
                var def = DefFor(id);
                object file = ReadJson(SidecarFile(id));
                object today = Member(TodayPiece(key), "gameplay");

                // What no placement moves, from the delivered sidecar FILE.
                Assert.That(def.Id, Is.EqualTo(Str(file, "id")));
                Assert.That(def.DisplayName, Is.EqualTo(Str(file, "name")), id);
                Assert.That(def.RigKey, Is.EqualTo(Str(file, "key")), id);
                Assert.That(def.SceneId, Is.EqualTo(Str(file, "scene")), id);
                Assert.That(def.Layer, Is.EqualTo(Str(file, "layer")), id);
                Assert.That(def.Walk, Is.EqualTo(Str(file, "walk")), id);
                Assert.That(def.Mount, Is.EqualTo(Str(file, "mount")), id);

                var colliders = Items(Member(file, "colliders"));
                Assert.That(def.Colliders.Length, Is.EqualTo(colliders.Count), $"{id} colliders");
                for (int c = 0; c < colliders.Count; c++)
                {
                    Assert.That(def.Colliders[c].Type, Is.EqualTo(Str(colliders[c], "type")), id);
                    Assert.That(def.Colliders[c].Blocks, Is.EqualTo(Str(colliders[c], "blocks")), id);
                    var pts = Items(Member(colliders[c], "pts"));
                    Assert.That(def.Colliders[c].Points.Length, Is.EqualTo(pts.Count), $"{id} collider {c}");
                    for (int p = 0; p < pts.Count; p++)
                        AssertNear(def.Colliders[c].Points[p], V2(pts[p]), $"{id} collider {c} point {p}");
                }

                var facings = Items(Member(file, "facings"));
                Assert.That(def.Frames.Length, Is.EqualTo(8), id);
                for (int k = 0; k < 8; k++)
                {
                    var frame = def.Frames[k];
                    Assert.That(frame.RigDir, Is.EqualTo((8 - k) % 8), $"{id} frame {k}");
                    object facing = facings[frame.RigDir];
                    Assert.That(frame.ShowFaces, Is.EqualTo(Str(facing, "showFaces")), $"{id} frame {k}");
                    Assert.That(frame.BearingDeg, Is.EqualTo((float)Num(Member(facing, "bearingDeg"))), $"{id} frame {k}");
                    Assert.That(frame.CameraSees, Is.EqualTo(Str(facing, "cameraSees")), $"{id} frame {k}");
                }

                object light = Member(file, "light");
                Assert.That(def.HasLight, Is.EqualTo(light != null), $"{id} light");
                if (light != null)
                {
                    Assert.That(def.Light.Emitters, Is.EqualTo(Items(Member(light, "emitters")).Select(DeckSidecarJson.String).ToArray()), id);
                    Assert.That(def.Light.When, Is.EqualTo(Str(light, "when")), id);
                }

                object animated = Member(file, "animated");
                Assert.That(def.IsAnimated, Is.EqualTo(animated != null), $"{id} animated");
                if (animated != null)
                {
                    Assert.That(def.Animated.Part, Is.EqualTo(Str(animated, "part")), id);
                    Assert.That(def.Animated.Input, Is.EqualTo(Str(animated, "input")), id);
                    Assert.That(def.Animated.Steps, Is.EqualTo((int)Num(Member(animated, "steps"))), id);
                    Assert.That(def.Animated.BakedAtDeg,
                        Is.EqualTo((float)Num(Member(Member(today, "options"), def.Animated.Input))), id);
                }

                // What the key scenes moved, from the today block (the FILE's own for six of the eight).
                object bounds = Member(today, "bounds");
                Vector2 bx = V2(Member(bounds, "x")), by = V2(Member(bounds, "y")), bz = V2(Member(bounds, "z"));
                AssertNear(def.BoundsMin, new Vector3(bx.x, by.x, bz.x), $"{id} bounds min");
                AssertNear(def.BoundsMax, new Vector3(bx.y, by.y, bz.y), $"{id} bounds max");

                var cells = Items(Member(today, "cells"));
                for (int k = 0; k < 8; k++)
                {
                    var frame = def.Frames[k];
                    object cell = cells[frame.RigDir];
                    Assert.That(frame.NativeSize, Is.EqualTo(new Vector2Int((int)Num(Member(cell, "w")), (int)Num(Member(cell, "h")))), $"{id} frame {k}");
                    Assert.That(frame.NativePivot, Is.EqualTo(Vector2Int.RoundToInt(V2(Member(cell, "pivot")))), $"{id} frame {k}");
                }

                var anchors = DeckSidecarJson.AsObject(Member(today, "anchors"));
                string[] pointNames = anchors.Keys
                    .Where(n => Items(anchors[n]).Count == 3 && Items(anchors[n]).All(x => x is double))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(def.Anchors.Select(a => a.Name), Is.EqualTo(pointNames), $"{id}: its named model points");
                foreach (string name in pointNames)
                {
                    Assert.That(def.TryGetAnchor(name, out Vector3 local), Is.True, $"{id} {name}");
                    AssertNear(local, V3(anchors[name]), $"{id} {name}");
                }

                var path = AnchorList(anchors, "path");
                Assert.That(def.Path.Length, Is.EqualTo(path.Count), $"{id} path");
                for (int i = 0; i < path.Count; i++) AssertNear(def.Path[i], V2(path[i]), $"{id} path {i}");

                var marks = AnchorList(anchors, "marks");
                Assert.That(def.TideMarks.Length, Is.EqualTo(marks.Count), $"{id} marks");
                for (int i = 0; i < marks.Count; i++)
                {
                    var mark = def.TideMarks[i];
                    Assert.That(mark.Z, Is.EqualTo((float)Num(Member(marks[i], "z"))), $"{id} mark {i}");
                    Assert.That(mark.OverDatum, Is.EqualTo((float)Num(Member(marks[i], "overDatum"))), $"{id} mark {i}");
                    AssertNear(mark.At, V3(Member(marks[i], "at")), $"{id} mark {i}");
                    Assert.That(mark.Label, Is.EqualTo(Str(marks[i], "label") ?? ""), $"{id} mark {i}");
                    Assert.That(mark.Kind, Is.EqualTo(Str(marks[i], "kind")), $"{id} mark {i}");
                }

                var words = AnchorList(anchors, "words");
                Assert.That(def.Words.Length, Is.EqualTo(words.Count), $"{id} words");
                for (int i = 0; i < words.Count; i++)
                {
                    var w = def.Words[i];
                    Assert.That(w.Id, Is.EqualTo(Str(words[i], "id")), $"{id} words {i}");
                    Assert.That(w.BodyId, Is.EqualTo(Str(words[i], "body") ?? ""), $"{id} words {i}");
                    var corners = Items(Member(words[i], "corners"));
                    Assert.That(w.Corners.Length, Is.EqualTo(corners.Count), $"{id} words {i}");
                    for (int c = 0; c < corners.Count; c++) AssertNear(w.Corners[c], V3(corners[c]), $"{id} words {i} corner {c}");
                    object at = Member(words[i], "at");
                    Assert.That(w.HasPoint, Is.EqualTo(at != null), $"{id} words {i}");
                    if (at != null) AssertNear(w.Point, V3(at), $"{id} words {i}");
                    object size = Member(words[i], "size");
                    Assert.That(w.Size, Is.EqualTo(size == null ? 0f : (float)Num(size)), $"{id} words {i}");
                }

                Assert.That(JsonEquals(DeckSidecarJson.Parse(def.BakedOptionsJson), Member(today, "options")), Is.True,
                    $"{id}: the options it records are not the ones the today block was written at.");
            }
        }

        /// <summary>The boards bake blank; the Def carries where the words go and which string they are.</summary>
        [Test]
        public void TheLetteredBoardsCarryTheirStringIds()
        {
            var sign = DefFor("prop.stp_cannery_fallen_sign").Words;
            Assert.That(sign.Select(w => (w.Id, w.BodyId, w.Corners.Length, w.HasPoint)),
                Is.EqualTo(new[] { ("words.cannery_sign", "", 4, false) }));

            var notice = DefFor("prop.stp_cannery_door_notice").Words;
            Assert.That(notice.Select(w => (w.Id, w.BodyId, w.Corners.Length, w.HasPoint)),
                Is.EqualTo(new[] { ("words.cannery_notice", "notice.cannery_for_sale", 4, false) }));

            var gauge = DefFor("prop.stp_slip_tide_board").Words;
            Assert.That(gauge.Select(w => w.Id), Is.EqualTo(new[] { "words.gauge_1", "words.gauge_2", "words.gauge_3", "words.gauge_4" }));
            Assert.That(gauge.All(w => w.HasPoint && w.Corners.Length == 0 && Mathf.Approximately(w.Size, 0.12f)), Is.True);
        }

        [Test]
        public void FramesSitOnTheirSheetsAsTheBakeContractSays()
        {
            var contract = Contract();
            Assert.That(contract.pieces.Select(p => p.key), Is.EqualTo(Pieces.Select(p => p.Key)));
            foreach (var (key, id) in Pieces)
            {
                var def = DefFor(id);
                var sheet = Sheet(contract, key);
                Assert.That(def.CellSize, Is.EqualTo(new Vector2Int(sheet.cellW, sheet.cellH)), id);
                Assert.That(def.PixelsPerMetre, Is.EqualTo(32f), id);
                Assert.That(def.ElevationDeg, Is.EqualTo(40f).Within(1e-4f), id);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath(key)).OfType<Sprite>()
                                           .ToDictionary(s => s.name, StringComparer.Ordinal);
                Assert.That(sprites.Count, Is.EqualTo(sheet.cells), $"{id}: sprites on its sheet. {Bake}");

                var frames = def.Frames.Concat(def.IsAnimated ? def.Animated.Headings : new SetPieceFrame[0]).ToArray();
                Assert.That(frames.Length, Is.EqualTo(sheet.cells), id);
                for (int c = 0; c < frames.Length; c++)
                {
                    var f = frames[c];
                    var cf = sheet.frames[c];
                    string name = $"SetPiece_{key}_{c}";
                    Assert.That(f.Sprite, Is.Not.Null, $"{id} frame {c}");
                    Assert.That(f.Sprite, Is.SameAs(sprites[name]), $"{id} frame {c}");
                    Assert.That(f.RigDir, Is.EqualTo(cf.rigDir), name);
                    Assert.That(f.Crop, Is.EqualTo(new Vector2Int(cf.cropX, cf.cropY)), name);
                    Assert.That(f.Pivot, Is.EqualTo(new Vector2Int(cf.pivotX, cf.pivotY)), name);
                    Assert.That(f.NativeSize, Is.EqualTo(new Vector2Int(cf.nativeW, cf.nativeH)), name);
                    Assert.That(f.NativePivot, Is.EqualTo(new Vector2Int(cf.nativePivotX, cf.nativePivotY)), name);
                    Assert.That(f.NativePivot - f.Crop, Is.EqualTo(f.Pivot), name);
                    Assert.That(f.Pivot.x >= 0 && f.Pivot.x < sheet.cellW && f.Pivot.y >= 0 && f.Pivot.y < sheet.cellH, Is.True,
                        $"{name}: its ground point is outside its cell.");

                    int col = c % sheet.cols, rowFromTop = c / sheet.cols;
                    Assert.That(f.Sprite.rect, Is.EqualTo(new Rect(col * sheet.cellW, sheet.sheetH - (rowFromTop + 1) * sheet.cellH,
                                                                   sheet.cellW, sheet.cellH)), name);
                    Assert.That(f.Sprite.pivot.x, Is.EqualTo(f.Pivot.x).Within(1e-3f), $"{name} pivot x");
                    Assert.That(f.Sprite.pivot.y, Is.EqualTo(sheet.cellH - f.Pivot.y).Within(1e-3f), $"{name} pivot y (from the bottom)");
                    Assert.That(f.Sprite.pixelsPerUnit, Is.EqualTo(32f), name);
                }
            }
        }

        /// <summary>
        /// Every pixel the kit wrote for an anchor at dir 4 (the tide board's marks and numerals, the sign's and
        /// notice's corners, the flue, the door, the conveyor's ends, the notice's lock) lands where the Def
        /// projects the same model point: thirty in all.
        /// </summary>
        [Test]
        public void AnchorsLandOnTheKitsOwnPixelsAtDirFour()
        {
            int projected = 0;
            foreach (var (key, id) in Pieces)
            {
                var def = DefFor(id);
                var anchors = DeckSidecarJson.AsObject(Member(Member(TodayPiece(key), "gameplay"), "anchors"));
                var frame = def.Frame(4);
                Assert.That(frame.RigDir, Is.EqualTo(4), id);
                Vector2 crop = frame.Crop;
                Vector2 Native(Vector3 model) => def.AnchorPixel(model, 4) + crop;

                foreach (var kv in DeckSidecarJson.AsObject(anchors["px"]))
                {
                    Assert.That(def.TryGetAnchor(kv.Key, out Vector3 local), Is.True, $"{id}: px names {kv.Key}");
                    AssertPx(Native(local), kv.Value, $"{id} {kv.Key}");
                    projected++;
                }

                foreach (object w in AnchorList(anchors, "words"))
                {
                    var word = def.Words.Single(x => x.Id == Str(w, "id"));
                    if (Member(w, "atPx") != null)
                    {
                        AssertPx(Native(word.Point), Member(w, "atPx"), $"{id} {word.Id}");
                        projected++;
                    }
                    var cornersPx = Items(Member(w, "cornersPx"));
                    for (int c = 0; c < cornersPx.Count; c++)
                    {
                        AssertPx(Native(word.Corners[c]), cornersPx[c], $"{id} {word.Id} corner {c}");
                        projected++;
                    }
                }

                var marks = AnchorList(anchors, "marks");
                for (int i = 0; i < marks.Count; i++)
                {
                    AssertPx(Native(def.TideMarks[i].At), Member(marks[i], "px"), $"{id} mark {i}");
                    projected++;
                }
            }
            Assert.That(projected, Is.EqualTo(30));
        }

        [Test]
        public void NothingGlowsInTheBakeAndOnlyTheBoilerCarriesALightRule()
        {
            foreach (var (_, id) in Pieces)
            {
                var def = DefFor(id);
                Assert.That(def.BakedLit, Is.EqualTo(0f), $"{id} was baked with a lamp lit.");
                Assert.That(def.HasLight, Is.EqualTo(id == BoilerId), id);
            }
            var boiler = DefFor(BoilerId);
            Assert.That(boiler.Light.Emitters, Is.EqualTo(new[] { "window" }));
            Assert.That(boiler.Light.When, Does.StartWith("restored only"),
                "The boiler house's windows glow only once it is restored, as a runtime overlay.");
        }

        [Test]
        public void TheVaneTurnsThroughSixteenHeadingsAtDirZero()
        {
            var vane = DefFor(VaneId);
            Assert.That(vane.IsAnimated, Is.True);
            var part = vane.Animated;
            Assert.That(part.Part, Is.EqualTo("vane.cod"));
            Assert.That(part.Input, Is.EqualTo("windFromDeg"));
            Assert.That(part.Steps, Is.EqualTo(16));
            Assert.That(part.BakedAtDeg, Is.EqualTo(270f));
            Assert.That(part.RigDir, Is.EqualTo(0));
            Assert.That(part.Headings.Length, Is.EqualTo(16));
            for (int s = 0; s < 16; s++)
            {
                Assert.That(part.Headings[s].RigDir, Is.EqualTo(0), $"heading {s}");
                Assert.That(part.Headings[s].Sprite?.name, Is.EqualTo($"SetPiece_harbourVane_{8 + s}"), $"heading {s}");
            }
            Assert.That(part.HeadingFor(part.BakedAtDeg), Is.EqualTo(12));

            foreach (var (_, id) in Pieces)
                if (id != VaneId) Assert.That(DefFor(id).IsAnimated, Is.False, id);
        }

        [Test]
        public void SheetsAreOnTheImportLockAndUnderTheCap()
        {
            var contract = Contract();
            foreach (var (key, _) in Pieces)
            {
                string path = SheetPath(key);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, $"{path}: no texture. {Bake}");
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(ArtImportPipeline.PixelsPerUnit), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);

                var sheet = Sheet(contract, key);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture.width, Is.EqualTo(sheet.sheetW), $"{path} imported at another size");
                Assert.That(texture.height, Is.EqualTo(sheet.sheetH), $"{path} imported at another size");
                Assert.That(Math.Max(texture.width, texture.height), Is.LessThanOrEqualTo(2048), path);
            }
        }

        /// <summary>
        /// Each cell on each sheet is the kit's own render at the today options under the today sky, cropped
        /// to the cell: the same RGBA wherever the kit drew, clear wherever it did not.
        /// </summary>
        [Test]
        public void SheetCellsAreTheKitsOwnRenderPixelForPixel()
        {
            var contract = Contract();
            using var host = KitHost();
            host.Execute("globalThis.__t = " + File.ReadAllText(Abs(TodayPath)) + ";");
            host.Execute("globalThis.__sky = WeatherSky.at(__t.bake.sky);");

            var wrong = new List<string>();
            for (int i = 0; i < Pieces.Length; i++)
            {
                string key = Pieces[i].Key;
                var sheet = Sheet(contract, key);
                string png = Abs(SheetPath(key));
                FileAssert.Exists(png, Bake);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(png)), Is.True, png);
                    Assert.That(texture.width, Is.EqualTo(sheet.sheetW), png);
                    Assert.That(texture.height, Is.EqualTo(sheet.sheetH), png);
                    Color32[] pixels = texture.GetPixels32();

                    foreach (var f in sheet.frames)
                    {
                        string heading = f.heading >= 0
                            ? $"e[P.gameplay.animated.input] = {f.heading} * 360 / P.gameplay.animated.steps;"
                            : "";
                        host.Execute(
                            $"globalThis.__r = (function () {{ var P = __t.pieces[{i}], e = {{}}; {heading} " +
                            "return StPetersSetPieces.render(P.key, " + f.rigDir + ", Object.assign(" +
                            "{ sky: __sky, lit: __t.bake.lit, outline: __t.bake.outline }, P.options, e)); })();");
                        int w = (int)host.EvaluateNumber("__r.w"), h = (int)host.EvaluateNumber("__r.h");
                        byte[] rgba = host.EvaluateBytes("__r.rgba");
                        host.Execute("globalThis.__r = null;");
                        Assert.That((w, h), Is.EqualTo((f.nativeW, f.nativeH)), $"{key} cell {f.cell}");

                        int col = f.cell % sheet.cols, rowFromTop = f.cell / sheet.cols, off = 0, opaque = 0;
                        for (int y = 0; y < sheet.cellH; y++)
                        {
                            for (int x = 0; x < sheet.cellW; x++)
                            {
                                int sx = f.cropX + x, sy = f.cropY + y, s = (sy * w + sx) * 4;
                                bool inside = sx >= 0 && sx < w && sy >= 0 && sy < h;
                                byte a = inside ? rgba[s + 3] : (byte)0;
                                Color32 got = pixels[(sheet.sheetH - 1 - (rowFromTop * sheet.cellH + y)) * sheet.sheetW + col * sheet.cellW + x];
                                if (a == 255) opaque++;
                                bool same = a == 0
                                    ? got.a == 0
                                    : got.a == a && got.r == rgba[s] && got.g == rgba[s + 1] && got.b == rgba[s + 2];
                                if (!same) off++;
                            }
                        }
                        if (off > 0) wrong.Add($"{key} cell {f.cell}: {off} pixels are not the kit's.");
                        if (opaque != f.opaque) wrong.Add($"{key} cell {f.cell}: {opaque} opaque pixels; the contract says {f.opaque}.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        // ---- the Def's own geometry (no bake needed) ------------------------------------------------------

        [Test]
        public void TheDefProjectsAndTurnsAsTheKitDoes()
        {
            var def = ScriptableObject.CreateInstance<SetPieceDef>();
            try
            {
                using var host = KitHost();
                Assert.That(def.PixelsPerMetre, Is.EqualTo((float)host.EvaluateNumber("StPetersSetPieces.PPU")));
                Assert.That(def.ElevationDeg, Is.EqualTo(host.EvaluateNumber(
                    "(function (B) { return Math.atan2(B.se, B.ce) * 180 / Math.PI; })(StPetersSetPieces.basis(0))")).Within(1e-4));

                var points = new[] { new Vector3(1.25f, -0.5f, 2f), new Vector3(-3f, 2.5f, 0.75f), new Vector3(0.21f, 0.204f, 4.98f) };
                for (int dir = 0; dir < 8; dir++)
                {
                    string basis = $"StPetersSetPieces.basis({dir})";
                    foreach (var p in points)
                    {
                        string pt = $"[{F(p.x)}, {F(p.y)}, {F(p.z)}]";
                        string q = $"StPetersSetPieces.projRaw({pt}, {basis})";
                        Vector2 off = def.ScreenOffsetPx(p, dir);
                        Assert.That(off.x, Is.EqualTo(host.EvaluateNumber(q + "[0]")).Within(1e-3), $"dir {dir} {pt} x");
                        Assert.That(off.y, Is.EqualTo(-host.EvaluateNumber(q + "[1]")).Within(1e-3), $"dir {dir} {pt} y (up)");

                        Vector2 world = SetPieceDef.ToWorld(new Vector2(p.x, p.y), dir);
                        Assert.That(world.x, Is.EqualTo(host.EvaluateNumber(
                            $"(function (B) {{ return {F(p.x)} * B.ct - {F(p.y)} * B.st; }})({basis})")).Within(1e-5), $"dir {dir} {pt} east");
                        Assert.That(world.y, Is.EqualTo(host.EvaluateNumber(
                            $"(function (B) {{ return {F(p.x)} * B.st + {F(p.y)} * B.ct; }})({basis})")).Within(1e-5), $"dir {dir} {pt} north");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void AVaneHeadingIsTheNearestStepAroundTheCompass()
        {
            var part = new SetPieceAnimatedPart { Steps = 16 };
            Assert.That(part.StepDeg, Is.EqualTo(22.5f));
            Assert.That(part.HeadingFor(0f), Is.EqualTo(0));
            Assert.That(part.HeadingFor(11f), Is.EqualTo(0));
            Assert.That(part.HeadingFor(12f), Is.EqualTo(1));
            Assert.That(part.HeadingFor(270f), Is.EqualTo(12));
            Assert.That(part.HeadingFor(359f), Is.EqualTo(0));
            Assert.That(part.HeadingFor(-22.5f), Is.EqualTo(15));
            Assert.That(part.HeadingFor(765f), Is.EqualTo(2));
            Assert.That(new SetPieceAnimatedPart().HeadingFor(90f), Is.EqualTo(0), "a part with no steps has one heading");
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
