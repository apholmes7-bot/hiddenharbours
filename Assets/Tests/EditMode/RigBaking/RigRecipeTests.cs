using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE BAKE RECIPE LEDGER — the half that runs without a rig host.</b>
    ///
    /// <para>Every committed <c>&lt;stem&gt;.recipe.json</c> is checked here for the things a JSON
    /// file can be wrong about on its own: its serialisation, the rig bytes it pins, the sheet it
    /// claims, its own pack arithmetic, and whether the catalog still agrees with it. What this
    /// CANNOT check is the only thing that makes a recipe true rather than plausible — that running
    /// it back through the rigs reproduces the sheet pixel for pixel. That proof lives outside
    /// Unity, deliberately, in <c>node tools/rig-recipes/verify-ledger.mjs</c>, because it needs to
    /// run on a machine with no editor: see <c>docs/art/rig-recipe-ledger.md</c>.</para>
    ///
    /// <para><b>⭐ The load-bearing test here is
    /// <see cref="EveryCommittedRecipe_ReSerialisesByteIdentically"/>.</b> Two writers produce these
    /// files — <see cref="RigRecipe"/> in the editor and <c>tools/rig-recipes/lib/recipe.mjs</c> in
    /// node — and a ledger written by two hands that disagree about key order or number spelling
    /// would churn every recipe on alternate runs and make a byte-compare meaningless. This is what
    /// holds them together.</para>
    /// </summary>
    public class RigRecipeTests
    {
        static string RepoRoot => Directory.GetParent(UnityEngine.Application.dataPath)!.FullName;

        const string ArtRoot = "Assets/_Project/Art";

        /// <summary>Every committed recipe, project-relative, in a stable order.</summary>
        static IReadOnlyList<string> RecipePaths =>
            Directory.Exists(Path.Combine(RepoRoot, ArtRoot))
                ? Directory.GetFiles(Path.Combine(RepoRoot, ArtRoot), "*.recipe.json",
                                     SearchOption.AllDirectories)
                           .Select(p => p.Substring(RepoRoot.Length + 1).Replace('\\', '/'))
                           .OrderBy(p => p, StringComparer.Ordinal)
                           .ToArray()
                : Array.Empty<string>();

        static string ReadText(string projectRelative) =>
            File.ReadAllText(Path.Combine(RepoRoot, projectRelative));

        /// <summary>Line endings are pinned to LF in <c>.gitattributes</c>, and normalising here as
        /// well means a checkout that got it wrong fails on the ATTRIBUTE rather than reporting a
        /// content difference that is not one.</summary>
        static string Lf(string s) => s.Replace("\r\n", "\n");

        // =============================================================================================
        //  ⭐⭐ the two writers must agree, byte for byte
        // =============================================================================================

        [Test]
        public void EveryCommittedRecipe_ReSerialisesByteIdentically()
        {
            Assert.IsNotEmpty(RecipePaths,
                "no recipes found — this suite is the ledger's gate, and an empty ledger passes it " +
                "vacuously. If the ledger really is empty, delete this assertion deliberately.");

            var wrong = new List<string>();
            foreach (string rel in RecipePaths)
            {
                string onDisk = Lf(ReadText(rel));
                string reWritten;
                try
                {
                    reWritten = RigRecipe.Parse(onDisk, rel).ToJson();
                }
                catch (Exception e)
                {
                    wrong.Add($"{rel}: {e.Message}");
                    continue;
                }
                if (reWritten != onDisk) wrong.Add($"{rel}: {FirstDifference(onDisk, reWritten)}");
            }

            Assert.IsEmpty(wrong,
                "these recipes are not what RigRecipe would write for the same content. The ledger " +
                "has two writers — the editor's and tools/rig-recipes/ — and they must produce the " +
                "same bytes or every re-bake churns the whole ledger:\n  " + string.Join("\n  ", wrong));
        }

        static string FirstDifference(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
                if (a[i] != b[i])
                {
                    int line = a.Take(i).Count(c => c == '\n') + 1;
                    return $"first difference on line {line}: on disk '{Excerpt(a, i)}', " +
                           $"re-written '{Excerpt(b, i)}'";
                }
            return $"lengths differ: on disk {a.Length}, re-written {b.Length}";
        }

        static string Excerpt(string s, int at) =>
            s.Substring(Math.Max(0, at - 20), Math.Min(50, s.Length - Math.Max(0, at - 20)))
             .Replace("\n", "\\n");

        // =============================================================================================
        //  the pins: rig bytes, sheet bytes, catalog
        // =============================================================================================

        [Test]
        public void EveryCommittedRecipe_PinsRigSourcesThatStillHashTheSame()
        {
            var drifted = new List<string>();
            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                foreach (var (source, sha) in new[] { (r.Rig.Source, r.Rig.Sha256) }
                             .Concat(r.Rig.Prerequisites.Select(p => (p.Source, p.Sha256))))
                {
                    if (!File.Exists(Path.Combine(RepoRoot, source)))
                    {
                        drifted.Add($"{rel}: rig source missing at {source}");
                        continue;
                    }
                    string now = RigRecipe.Sha256OfSource(source);
                    if (now != sha)
                        drifted.Add($"{rel}: {source} now hashes {now.Substring(0, 12)}…, " +
                                    $"the recipe pins {sha.Substring(0, 12)}…");
                }
            }

            Assert.IsEmpty(drifted,
                "a recipe names the rig BYTES that drew its sheet, and these no longer match. Either " +
                "the rig moved (re-bake the kit and its recipes together) or the hash is stale — but " +
                "an unpinned recipe is exactly the 'renders from the same rigs, probably' the " +
                "scene-editor review refused to accept:\n  " + string.Join("\n  ", drifted));
        }

        [Test]
        public void EveryCommittedRecipe_HasItsSheet_AndNoSheetHasTwo()
        {
            var wrong = new List<string>();
            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                string folder = rel.Substring(0, rel.LastIndexOf('/'));
                string sheet = $"{folder}/{r.Sheet}";

                // The recipe's own name has to be the sheet's: <stem>.png ↔ <stem>.recipe.json. A
                // recipe whose `sheet` field points somewhere else would be read correctly by the
                // ledger and picked up by the WRONG sprite by anything that looks by filename.
                string expected = $"{folder}/{Path.GetFileName(rel).Replace(".recipe.json", ".png")}";
                if (sheet != expected)
                    wrong.Add($"{rel}: names sheet '{r.Sheet}', but sits beside {expected}");

                if (!File.Exists(Path.Combine(RepoRoot, sheet)))
                    wrong.Add($"{rel}: no sheet at {sheet}");

                if (claimed.TryGetValue(sheet, out string other))
                    wrong.Add($"{sheet} is claimed by both {other} and {rel}");
                else claimed[sheet] = rel;
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void EveryCommittedRecipe_DeclaresTheCatalogsOwnRigAndItsWholePrerequisiteClosure()
        {
            var wrong = new List<string>();
            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                if (!RigCatalog.Has(r.Rig.Key))
                {
                    wrong.Add($"{rel}: no catalog rig '{r.Rig.Key}'");
                    continue;
                }

                var fresh = RigRecipe.RigBlockFor(r.Rig.Key);
                if (fresh.Source != r.Rig.Source)
                    wrong.Add($"{rel}: catalog now loads {r.Rig.Key} from {fresh.Source}");
                if (fresh.Global != r.Rig.Global) wrong.Add($"{rel}: catalog now installs {fresh.Global}");
                if (fresh.Convention != r.Rig.Convention)
                    wrong.Add($"{rel}: catalog now declares {fresh.Convention}, " +
                              $"the recipe says {r.Rig.Convention}");

                string want = string.Join(" → ", fresh.Prerequisites.Select(p => p.Key));
                string got = string.Join(" → ", r.Rig.Prerequisites.Select(p => p.Key));
                if (want != got)
                    wrong.Add($"{rel}: prerequisites are [{got}], the catalog's closure is [{want}]");
            }

            // ⚠️ Under-declaring a prerequisite does NOT show up as a bad render once something else
            // in the same process has installed that global — the catalog's own install is idempotent
            // for the same reason. It only bites in a fresh host, which is where the owner's editor
            // reads these. So the closure is checked against the catalog rather than trusted.
            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        // =============================================================================================
        //  ⭐ the recipe against the SLICER — two committed artefacts, one sheet
        // =============================================================================================

        /// <summary>
        /// The importer holds one sprite rect per baked cell, plus the normalised pivot. That is the
        /// SLICER's view of the sheet, arrived at independently of the recipe, and the two have to
        /// agree: same cell size, same span, same cell count, same pivot.
        ///
        /// <para>This is deliberately NOT the pixel proof — it cannot tell you the art is right, only
        /// that the two descriptions of it match. What it buys is that it needs no rig host and no
        /// render, so it runs in the ordinary EditMode pass. `tools/rig-recipes/check-slices.mjs` is
        /// the same check outside Unity, off the raw `.meta` text; it catches a ONE-PIXEL pivot
        /// error.</para>
        /// </summary>
        [Test]
        public void EveryCommittedRecipe_AgreesWithTheSlicersOwnRects()
        {
            var wrong = new List<string>();
            int checkedSheets = 0, unsliced = 0;

            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                string sheet = $"{rel.Substring(0, rel.LastIndexOf('/'))}/{r.Sheet}";

                var rects = SpriteRectsOf(sheet);
                if (rects == null || rects.Count == 0) { unsliced++; continue; }
                checkedSheets++;

                var p = r.Pack;
                int cells = r.Grid.Axes.Aggregate(1, (n, a) => n * a.Values.Count);

                if (rects.Count != cells)
                    wrong.Add($"{rel}: the slicer holds {rects.Count} rect(s), the recipe's axes " +
                              $"make {cells} cell(s)");

                var odd = rects.Where(s => (int)s.rect.width != p.CellW ||
                                           (int)s.rect.height != p.CellH).ToList();
                if (odd.Count > 0)
                    wrong.Add($"{rel}: {odd.Count} rect(s) are not {p.CellW}×{p.CellH} (e.g. " +
                              $"{odd[0].name} is {(int)odd[0].rect.width}×{(int)odd[0].rect.height})");

                int spanW = rects.Max(s => (int)(s.rect.x + s.rect.width));
                int spanH = rects.Max(s => (int)(s.rect.y + s.rect.height));
                if (spanW != p.SheetW || spanH != p.SheetH)
                    wrong.Add($"{rel}: the rects span {spanW}×{spanH}, the recipe packs " +
                              $"{p.SheetW}×{p.SheetH}");

                // ADR 0026: a rig pivot is a CONTINUOUS coordinate from the cell's top-left, so the
                // y term is (H − pivotY)/H and NOT (H − 1 − pivotY)/H. Upside-down here is silent.
                float wantX = p.PivotX / (float)p.CellW;
                float wantY = (p.CellH - p.PivotY) / (float)p.CellH;
                // Half a pixel of tolerance, never a whole one: a tolerance loose enough to hide a
                // pixel would hide the bug this catches.
                float tol = 0.5f / UnityEngine.Mathf.Max(p.CellW, p.CellH);
                var badPivot = rects.Where(s => UnityEngine.Mathf.Abs(s.pivot.x - wantX) > tol ||
                                                UnityEngine.Mathf.Abs(s.pivot.y - wantY) > tol).ToList();
                if (badPivot.Count > 0)
                    wrong.Add($"{rel}: {badPivot.Count} sprite pivot(s) disagree — {badPivot[0].name} " +
                              $"is ({badPivot[0].pivot.x}, {badPivot[0].pivot.y}), the recipe's " +
                              $"({p.PivotX},{p.PivotY}) normalises to ({wantX}, {wantY})");
            }

            // Every sheet unsliced means the textures did not import — overwhelmingly because this
            // checkout has LFS POINTERS where the PNGs should be. That is inconclusive, not a pass,
            // and saying so is the difference between a gate and a decoration.
            if (checkedSheets == 0 && unsliced > 0)
                Assert.Ignore($"none of the {unsliced} sheets carries sprite rects. The likeliest " +
                              "cause is a checkout without the Git-LFS objects: run `git lfs pull` " +
                              "and re-run. (tools/rig-recipes/check-slices.mjs makes the same check " +
                              "off the committed .meta text, which needs no LFS object at all.)");

            Assert.IsEmpty(wrong,
                $"checked {checkedSheets} sheet(s); the recipe and the slicer disagree on:\n  " +
                string.Join("\n  ", wrong));
        }

        /// <summary>
        /// The importer's persisted sprite rects. Read through <c>ISpriteEditorDataProvider</c>, never
        /// <c>TextureImporter.spritesheet</c> — obsolete-as-error on Unity 6000.5.
        /// </summary>
        static List<UnityEditor.U2D.Sprites.SpriteRect> SpriteRectsOf(string assetPath)
        {
            if (!(UnityEditor.AssetImporter.GetAtPath(assetPath) is UnityEditor.TextureImporter importer))
                return null;
            if (importer.spriteImportMode != UnityEditor.SpriteImportMode.Multiple) return null;

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) return null;
            provider.InitSpriteEditorDataProvider();
            return provider.GetSpriteRects().ToList();
        }

        // =============================================================================================
        //  the pack arithmetic — self-consistency a JSON file can be held to on its own
        // =============================================================================================

        [Test]
        public void EveryCommittedRecipe_PackArithmeticIsSelfConsistent()
        {
            var wrong = new List<string>();
            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                var p = r.Pack;

                if (p.SheetW != r.Grid.Columns * p.CellW || p.SheetH != r.Grid.Rows * p.CellH)
                    wrong.Add($"{rel}: {r.Grid.Columns}×{r.Grid.Rows} of {p.CellW}×{p.CellH} is " +
                              $"{r.Grid.Columns * p.CellW}×{r.Grid.Rows * p.CellH}, " +
                              $"not {p.SheetW}×{p.SheetH}");

                // The crop and the pivot are two views of one offset: the cropped pivot is the native
                // pivot minus the crop origin. If those disagree the sheet's sprites slice correctly
                // and register in the wrong place — which is silent.
                if (p.CropX != p.NativePivotX - p.PivotX || p.CropY != p.NativePivotY - p.PivotY)
                    wrong.Add($"{rel}: crop ({p.CropX},{p.CropY}) and pivots " +
                              $"({p.NativePivotX},{p.NativePivotY})→({p.PivotX},{p.PivotY}) disagree");

                if (p.CellW <= 0 || p.CellH <= 0 || p.NativeW <= 0 || p.NativeH <= 0)
                    wrong.Add($"{rel}: a non-positive cell or buffer");

                int cells = r.Grid.Axes.Aggregate(1, (n, a) => n * a.Values.Count);
                if (cells > r.Grid.Columns * r.Grid.Rows)
                    wrong.Add($"{rel}: {cells} cells will not fit a {r.Grid.Columns}×{r.Grid.Rows} grid");

                // A blank slot ships as a fully transparent cell no sprite rect ever names, and reads
                // later as a MISSING facing. One row of slack is the honest maximum: it is what an
                // uneven fold costs, and more than that means the grid is wrong.
                if (cells + r.Grid.Columns <= r.Grid.Columns * r.Grid.Rows)
                    wrong.Add($"{rel}: {cells} cells in a {r.Grid.Columns}×{r.Grid.Rows} grid leaves " +
                              "a whole blank row");

                if (r.Grid.Order != "rowMajor") wrong.Add($"{rel}: unknown grid order '{r.Grid.Order}'");
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void EveryCommittedRecipe_BindsEveryAxisToSomethingTheCallCanTake()
        {
            var wrong = new List<string>();
            foreach (string rel in RecipePaths)
            {
                var r = RigRecipe.Parse(Lf(ReadText(rel)), rel);
                bool wantsDir = r.Call.Args.Contains("$dir");
                bool wantsOpts = r.Call.Args.Contains("$opts");
                bool boundDir = false;

                foreach (var axis in r.Grid.Axes)
                {
                    if (axis.Values.Count == 0) wrong.Add($"{rel}: axis '{axis.Name}' has no values");

                    if (axis.Bind == "dir") { boundDir = true; continue; }
                    if (axis.Bind.StartsWith("opt:", StringComparison.Ordinal))
                    {
                        if (!wantsOpts)
                            wrong.Add($"{rel}: axis '{axis.Name}' sets an option but the call " +
                                      "takes none");
                        continue;
                    }
                    if (axis.Bind.StartsWith("arg:", StringComparison.Ordinal))
                    {
                        if (!int.TryParse(axis.Bind.Substring(4), NumberStyles.Integer,
                                          CultureInfo.InvariantCulture, out int n) ||
                            n < 0 || n >= r.Call.Args.Count)
                            wrong.Add($"{rel}: axis '{axis.Name}' binds {axis.Bind}, out of range for " +
                                      $"{r.Call.Args.Count} arg(s)");
                        continue;
                    }
                    wrong.Add($"{rel}: axis '{axis.Name}' has an unknown bind '{axis.Bind}'");
                }

                // A `$dir` with nothing bound to it is the failure that does not throw: the rigs
                // compute dir·π/4, so a placeholder reaching one as a string makes every projected
                // vertex NaN and render comes back fully transparent WITHOUT complaining.
                if (wantsDir && !boundDir)
                    wrong.Add($"{rel}: the call takes a dir and no axis supplies one");
                if (!wantsDir && boundDir) wrong.Add($"{rel}: an axis binds dir but the call takes none");
            }

            Assert.IsEmpty(wrong, string.Join("\n  ", wrong));
        }

        [Test]
        public void EveryCommittedRecipe_IsFiledUnderAKitSomeBakerActuallyWrites()
        {
            var kits = new HashSet<string>(StringComparer.Ordinal)
            {
                FuelSheetBaker.RecipeKit,
                GasStationSheetBaker.RecipeKit,
                CamperSheetBaker.RecipeKit,
                YardSheetBaker.RecipeKit,
                NavBuoySheetBaker.RecipeKit,
                IsoPropSheetBaker.RecipeKit,
            };

            var wrong = RecipePaths
                .Select(rel => (rel, r: RigRecipe.Parse(Lf(ReadText(rel)), rel)))
                .Where(x => !kits.Contains(x.r.Kit))
                .Select(x => $"{x.rel}: kit '{x.r.Kit}' is not written by any baker")
                .ToList();

            Assert.IsEmpty(wrong,
                "a recipe filed under a kit no baker writes cannot be re-derived, so nothing would " +
                "catch it going stale:\n  " + string.Join("\n  ", wrong));
        }

        // =============================================================================================
        //  ⭐ the options: one dict, and the call string derived from it
        // =============================================================================================

        /// <summary>
        /// The exact JS literals the bakers used to build by hand, before the dict became the source.
        ///
        /// <para>These are PINNED rather than derived. The point of moving the options into a dict was
        /// that a baker cannot record one thing and render another — but the refactor is only safe if
        /// the literal it now derives is the one that baked the shipped sheets. Every string here was
        /// read back off a committed recipe that reproduces its sheet byte for byte.</para>
        /// </summary>
        [Test]
        public void Js_SpellsTheOptionsExactlyAsTheBakedSheetsWereDrawn()
        {
            Assert.AreEqual(
                "{size:'s20',fuel:'gas',fill:0,wear:'working',rest:true}",
                RigRecipe.Js(FuelSheetBaker.OptsOf(
                    new FuelStorageKit.Build("jerry", "s20", "gas"), rest: true, fill: 0f)),
                "the fuel kit's carriables bake at rest, and `wear` is the option whose omission " +
                "selects a phantom state that shares a cache key with 'working'");

            Assert.AreEqual(
                "{size:'s10k',fuel:'gas',fill:0,wear:'working'}",
                RigRecipe.Js(FuelSheetBaker.OptsOf(
                    new FuelStorageKit.Build("bulk", "s10k", "gas"), rest: false, fill: 0f)),
                "a storage vessel takes no `rest`");

            Assert.AreEqual(
                "{size:'sKerb',grades:['gas','diesel','mixed'],lit:false,hoses:0,sides:0,out:-1," +
                "style:'fascia',open:0,span:1,bollards:true,bin:false,wear:'working',seed:11," +
                "keyline:false,elev:40}",
                RigRecipe.Js(GasStationSheetBaker.OptsOf("sKerb")),
                "`grades` is GEOMETRY on this rig — hose count, sign rows and apron caps come from it");

            Assert.AreEqual(
                "{variant:'clipper',paint:null,weather:0.32,swing:0,awning:false,winAwn:false," +
                "vents:true,rack:false,propane:true,jacks:true,chocks:true,hookups:true,step:true," +
                "night:false,outline:false}",
                RigRecipe.Js(CamperSheetBaker.OptsOf("clipper", awning: false, swing: 0)),
                "paint is the LITERAL null — the bare polished-aluminium skin, not the word");

            Assert.AreEqual(
                "{size:'s12',wear:'working',lit:false}",
                RigRecipe.Js(NavBuoyKit.OptsOf("s12", NavBuoyKit.BakeWear, NavBuoyKit.BakeLit)));
        }

        [Test]
        public void YardKitAndItsBaker_StillSpellTheOptionsTheSameWay()
        {
            // YardKit lives in an assembly that cannot see RigRecipe, so its options genuinely exist
            // twice. This is the check the bake itself makes, run over the whole table.
            foreach (var b in YardKit.Builds)
                Assert.DoesNotThrow(() => YardSheetBaker.AssertOptionsAgree(b), b.Key);
        }

        // =============================================================================================
        //  the serialiser's own corners
        // =============================================================================================

        [Test]
        public void Number_SpellsWhatJavaScriptWould()
        {
            // The shortest decimal that round-trips — the same rule JS's Number#toString follows.
            // "G17" would write 0.1 as 0.10000000000000001 and churn the ledger on every re-bake.
            Assert.AreEqual("0", RigRecipe.Number(0.0));
            Assert.AreEqual("0", RigRecipe.Number(-0.0), "JSON has no negative zero and JS writes 0");
            Assert.AreEqual("1", RigRecipe.Number(1.0));
            Assert.AreEqual("-1", RigRecipe.Number(-1.0));
            Assert.AreEqual("40", RigRecipe.Number(40.0));
            Assert.AreEqual("0.25", RigRecipe.Number(0.25));
            Assert.AreEqual("0.5", RigRecipe.Number(0.5));
            Assert.AreEqual("0.75", RigRecipe.Number(0.75));
            Assert.AreEqual("0.1", RigRecipe.Number(0.1));
            Assert.AreEqual("0.6", RigRecipe.Number(3.0 / 5.0), "the camper's swing ladder");
            Assert.AreEqual("0.32", RigRecipe.Number(0.32f), "a float tunable records what it prints");
            Assert.AreEqual("0.88", RigRecipe.Number(0.88f));
            Assert.AreEqual("0.4", RigRecipe.Number(0.4f));
        }

        [Test]
        public void Number_RefusesWhatTheTwoWritersWouldSpellDifferently()
        {
            // .NET writes 1E+21 and JavaScript writes 1e+21. Nothing in this repo's rig options needs
            // exponent notation, so the schema refuses it rather than letting the two halves diverge
            // over a value nobody meant to record.
            Assert.Throws<InvalidOperationException>(() => RigRecipe.Number(1e21));
            Assert.Throws<InvalidOperationException>(() => RigRecipe.Number(double.NaN));
            Assert.Throws<InvalidOperationException>(() => RigRecipe.Number(double.PositiveInfinity));
        }

        [Test]
        public void Parse_RefusesAKeyThisBuildDoesNotUnderstand()
        {
            string good = Lf(ReadText(RecipePaths.First()));
            string withExtra = good.Replace("\"kit\":", "\"tint\": \"seafoam\",\n  \"kit\":");

            var e = Assert.Throws<InvalidOperationException>(() => RigRecipe.Parse(withExtra, "(mutated)"));
            StringAssert.Contains("tint", e.Message,
                "silently dropping an unknown key is how a consumer draws the wrong variant while " +
                "believing it read the whole file");
        }

        [Test]
        public void Write_RefusesARecipeForASheetThatIsNotThere()
        {
            Assert.Throws<FileNotFoundException>(() => RigRecipe.Write(
                "Assets/_Project/Art/Sprites/NoSuchFolder/noSuchSheet.png",
                new RigRecipe
                {
                    Kit = "nowhere", Baker = "nobody",
                    Rig = RigRecipe.RigBlockFor("fuel"),
                    Call = new RigRecipe.CallBlock(),
                    Grid = new RigRecipe.GridBlock { Columns = 1, Rows = 1 },
                    Pack = new RigRecipe.PackBlock(),
                }),
                "sheetSha256 is the sheet's own bytes, so a recipe written before its sheet would " +
                "pin nothing at all");
        }

        [Test]
        public void RoundTrip_SurvivesAnEmptyOptionsDictAndAnAxislessSheet()
        {
            // The one-cell, no-facing shape: trapFauna renders render(key, opts) with no dir at all,
            // and the iso-prop families bake at rig defaults, i.e. {}. Both spell as JSON's own empty
            // forms, and both have to survive a parse.
            var recipe = new RigRecipe
            {
                Sheet = "urchin.png", Kit = IsoPropSheetBaker.RecipeKit, Baker = "IsoPropSheetBaker",
                Rig = RigRecipe.RigBlockFor("trapFauna"),
                Call = new RigRecipe.CallBlock { Args = { "urchin", "$opts" } },
                Grid = new RigRecipe.GridBlock { Columns = 1, Rows = 1 },
                Pack = new RigRecipe.PackBlock
                {
                    NativeW = 1, NativeH = 1, CellW = 1, CellH = 1, SheetW = 1, SheetH = 1,
                },
                SheetSha256 = new string('0', 64),
            };

            string json = recipe.ToJson();
            StringAssert.Contains("\"opts\": {}", json);
            StringAssert.Contains("\"axes\": []", json);
            Assert.AreEqual(json, RigRecipe.Parse(json, "(round trip)").ToJson());
        }
    }
}
