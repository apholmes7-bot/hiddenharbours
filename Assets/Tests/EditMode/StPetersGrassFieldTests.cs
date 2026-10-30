using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE MIGRATION, MEASURED.</b> St Peters' grass pass stopped emitting a GameObject per tuft and
    /// started baking a FIELD — a byte per candidate site — that <see cref="GrassField"/> grows the meadow
    /// from at load. These tests pin the three things that makes true or false:
    ///
    /// <list type="number">
    /// <item><b>The field describes the same meadow the scatter always did.</b> Same sites, same habitats,
    /// same places. The migration was allowed to change what is STORED, not what is SEEN.</item>
    /// <item><b>It is idempotent.</b> Running the pass twice writes the same bytes — the trap that doubled
    /// the owner's scene, closed by construction rather than by a clear-first convention (ADR 0019).</item>
    /// <item><b>It actually costs a fraction.</b> The numbers are computed and LOGGED, not asserted into
    /// vagueness, so a reviewer can read what the change bought.</item>
    /// </list>
    ///
    /// <para><b>⚠ WHAT THESE TESTS CANNOT TELL YOU.</b> Not the frame time, and not whether it looks right.
    /// CI has no graphics device and the owner's 4060 is the only instrument that answers either. What is
    /// measurable headless is what frame time is SPENT on — how many draws a screen of meadow costs — and
    /// that is measured here against the old path on the same deciders the build uses. Same honesty
    /// <see cref="StPetersGroundCoverBudgetTests"/> already states.</para>
    /// </summary>
    // Legacy codec/scatter assertions stay on their analytic-terrain fixture.
    public class StPetersLegacyGrassFieldFixtureTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_GrassField");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// Bytes of scene YAML ONE tuft used to cost, as a saved GameObject.
        ///
        /// <para><b>Measured, not guessed.</b> Main's <c>StPeters.unity</c> at 7b9b300e holds 4,766
        /// GameObjects / 4,766 Transforms / 4,722 SpriteRenderers / 5,908 MonoBehaviours in 15,818,001
        /// bytes: ~441 B for the GameObject, ~495 B for the Transform, ~1,618 B for the SpriteRenderer and
        /// ~551 B for the <c>YSortSprite</c> — <b>~3,105 B</b> for the four objects a tuft carried. Rounded
        /// down to 3,100 so the comparison below understates rather than flatters the change.</para>
        /// </summary>
        public const int BytesPerSavedTuft = 3100;

        // =====================================================================================
        // 1. THE FIELD IS THE MEADOW
        // =====================================================================================

        [Test]
        public void TheRuntimeHash_IsTheSameHashTheIslandWasAlwaysScatteredWith()
        {
            // GrassFieldScatter restates StPetersShoreMap.Hash01 because a runtime assembly may not
            // reference an editor one (rule 4). A restatement that drifts would re-roll the whole island
            // silently — same tufts, different places — so it is pinned rather than trusted.
            for (int x = -40; x <= 40; x += 7)
            for (int y = -40; y <= 40; y += 11)
            for (int salt = 0; salt < 400; salt += 53)
                Assert.AreEqual(StPetersShoreMap.Hash01(x, y, salt),
                                GrassFieldScatter.Hash01(x, y, salt), 0f,
                    $"The runtime hash has drifted from the island's at ({x},{y},{salt}). Every tuft on " +
                    "St Peters would move.");
        }

        [Test]
        public void TheBakedField_HoldsExactlyTheSitesTheScatterPlants()
        {
            var sites = StPetersGrass.Scatter(_terrain);
            var baked = StPetersGrassField.BakeField(_terrain);

            Assert.Greater(sites.Count, 0, "sanity: the island scattered no grass at all");
            Assert.AreEqual(sites.Count, baked.Tufts,
                "The field describes a different number of tufts than the scatter plants. The bake only " +
                "ENCODES what StPetersGrass decided — a mismatch means one of them is walking a grid the " +
                "other does not know about.");

            var layout = baked.Layout;
            foreach (var site in sites)
            {
                int at = (site.CellY * layout.CellsX + site.CellX) * layout.Slots + site.Slot;
                byte b = baked.Sites[at];

                Assert.IsFalse(GrassFieldScatter.IsEmpty(b),
                    $"A scattered tuft at cell ({site.CellX},{site.CellY}) slot {site.Slot} has no byte in " +
                    "the field — it would simply not grow.");
                Assert.AreEqual(StPetersGrassField.HabitatIdOf(site.Habitat),
                                GrassFieldScatter.HabitatOf(b),
                    "A site's habitat did not survive the bake — it would wear another ground's art.");
                Assert.AreEqual(site.Broad, GrassFieldScatter.BroadOf(b),
                    "A site's broad-clump request did not survive the bake — the cores lose the wide " +
                    "clumps that pay for the grain.");
            }
        }

        [Test]
        public void EveryHabitatTheIslandResolves_HasAnIdInTheField()
        {
            // A habitat the field cannot name grows NOTHING (HabitatIdOf returns 0 and the site is
            // dropped). If StPetersGrass gains a habitat and HabitatIds is not extended, the island goes
            // bald in patches and nothing else complains.
            var seen = new HashSet<string>();
            foreach (var site in StPetersGrass.Scatter(_terrain)) seen.Add(site.Habitat);

            Assert.Greater(seen.Count, 0, "sanity: the scatter resolved no habitats");
            foreach (string habitat in seen)
                Assert.Greater(StPetersGrassField.HabitatIdOf(habitat), 0,
                    $"The scatter resolves habitat '{habitat}' but the field has no id for it, so every " +
                    "tuft on that ground would be dropped at bake. Append it to " +
                    "StPetersGrassField.HabitatIds — APPEND, never re-order, or every baked field repaints.");
        }

        [Test]
        public void TheDerivedMeadow_StandsExactlyWhereTheScatterPutIt()
        {
            // The round trip that matters: scatter -> bytes -> Base64 -> bytes -> tufts. If the codec or
            // the derive moved anything, this is where it shows.
            var sites = StPetersGrass.Scatter(_terrain);
            var baked = StPetersGrassField.BakeField(_terrain);

            var field = MakeFieldComponent(baked, out var spawned);
            try
            {
                var blades = field.DeriveBlades();
                Assert.AreEqual(sites.Count, blades.Count,
                    "The field grew a different number of tufts than the scatter planted.");

                // Both walk the grid in the same order, so they line up index for index.
                for (int i = 0; i < sites.Count; i++)
                {
                    Assert.AreEqual(sites[i].Position.x, blades[i].Position.x, 1e-4f,
                        $"tuft {i} moved in X between the scatter and the field");
                    Assert.AreEqual(sites[i].Position.y, blades[i].Position.y, 1e-4f,
                        $"tuft {i} moved in Y between the scatter and the field");
                    Assert.AreEqual(sites[i].Scale, blades[i].Scale, 1e-4f,
                        $"tuft {i} changed size between the scatter and the field");
                    Assert.AreEqual(sites[i].Mirror, blades[i].Mirror,
                        $"tuft {i} flipped between the scatter and the field");
                }
            }
            finally { foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o); }
        }

        // =====================================================================================
        // 2. IDEMPOTENCE — the trap that doubled the owner's scene
        // =====================================================================================

        [Test]
        public void BakingTwice_WritesTheIdenticalField()
        {
            // ⭐ THE WHOLE POINT OF A FIELD. The old brush APPENDED objects and rejected on min-spacing, so
            // dragging over the same ground twice laid MORE grass — which is how a scene reaches 114 MB. A
            // field cannot append: there is one byte per site, and writing it again writes the same value.
            var a = StPetersGrassField.BakeField(_terrain);
            var b = StPetersGrassField.BakeField(_terrain);

            Assert.AreEqual(a.Tufts, b.Tufts, "a second bake produced a different number of tufts");
            CollectionAssert.AreEqual(a.Sites, b.Sites,
                "A second bake wrote a different field. Re-running the pass must be a no-op (ADR 0019) — " +
                "this is the non-idempotent-stroke trap that doubled StPeters.unity.");
            CollectionAssert.AreEqual(a.Straw, b.Straw, "a second bake wrote a different straw plane");
        }

        [Test]
        public void TheFieldSurvivesTheCodec_ByteForByte()
        {
            var baked = StPetersGrassField.BakeField(_terrain);
            CollectionAssert.AreEqual(baked.Sites,
                GrassFieldCodec.Decode(GrassFieldCodec.Encode(baked.Sites)),
                "The island's field did not survive its own encoding.");
        }

        // =====================================================================================
        // 3. WHAT IT COST, AND WHAT IT COSTS NOW
        // =====================================================================================

        [Test]
        public void TheFieldCostsAFractionOfWhatTheSavedTuftsDid()
        {
            var baked = StPetersGrassField.BakeField(_terrain);
            Assert.Greater(baked.Tufts, 0, "sanity: the island baked no grass");

            int fieldChars = GrassFieldCodec.Encode(baked.Sites).Length
                           + GrassFieldCodec.Encode(baked.Straw).Length;
            long asObjects = (long)baked.Tufts * BytesPerSavedTuft;

            Debug.Log(
                $"[StPetersGrassField] {baked.Tufts:N0} tufts ({baked.HabitatSummary()}).\n" +
                $"  as saved GameObjects : {asObjects / 1024f / 1024f:F1} MiB " +
                $"({baked.Tufts * 4:N0} serialized objects)\n" +
                $"  as a baked field     : {fieldChars / 1024f:F1} KiB (1 component, 2 strings)\n" +
                $"  ratio                : {(float)asObjects / Mathf.Max(fieldChars, 1):F0}x smaller\n" +
                $"  grid                 : {baked.Layout.CellsX}x{baked.Layout.CellsY} at " +
                $"{baked.Layout.CellSize} m, {baked.Layout.Slots} sites/cell, seed {baked.Layout.Seed}");

            // A hundredfold is a floor, not a target: the measured ratio is far larger. It is set where a
            // REGRESSION shows — somebody storing per-tuft data in the field again — rather than where the
            // current numbers sit.
            Assert.Less(fieldChars * 100L, asObjects,
                $"The field ({fieldChars:N0} chars) is no longer two orders of magnitude smaller than the " +
                $"{baked.Tufts:N0} tufts it replaced ({asObjects:N0} bytes). Something per-tuft has crept " +
                "into the field.");

            // And it must stay far under the scene guard, which is the real backstop.
            Assert.Less(fieldChars, 4 * 1024 * 1024,
                "The field alone is over 4 MiB — it has stopped being a compact layer.");
        }

        [Test]
        public void AScreenOfMeadow_CostsFewerDrawsBatchedThanItDidAsSprites()
        {
            // ⭐ THE DRAW-CALL COMPARISON, on the same deciders the build uses.
            //
            // OLD: 2D sprites batch by material AND texture, and only within a run of equal sorting order.
            //      YSortSprite gives every metre of world Y four distinct orders, so a screen of grass is
            //      chopped into ~36 bands at on-foot framing and each band re-batches per texture it holds.
            // NEW: a chunk is (column x row x texture), and a row is RowOrderSteps order steps tall — so
            //      the band multiplier drops by exactly that factor. That is where the win comes from, and
            //      it is why the knob is expressed in order steps.
            var library = GrassLibraryCatalog.Load();
            if (library == null || library.Entries.Count == 0)
                Assert.Ignore("The grass library manifest is not on disk in this checkout.");

            var imported = GrassLibraryCatalog.Imported(library);
            if (imported.Count == 0)
                Assert.Ignore("No grass sprite in the library is imported in this checkout (LFS).");

            var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var sites = StPetersGrass.Scatter(_terrain);
            Assert.Greater(sites.Count, 0, "sanity: the meadow scattered nothing");

            const float ViewHeightM = HiddenHarbours.App.CameraFollow.OnFootWorldHeightMeters;
            const float ViewWidthM = ViewHeightM * 16f / 9f;
            const float ChunkWidthM = 16f;                       // GrassField's shipped default
            const int RowOrderSteps = 4;                         // GrassField's shipped default
            float rowHeight = RowOrderSteps / HiddenHarbours.Core.SortingBands.OrdersPerMetre;

            // texture per site, resolved once per (habitat, broad, roll) the way the build does
            var textureOf = new Dictionary<int, string>();
            string TextureFor(StPetersGrass.GrassTuftSite s)
            {
                int key = (StPetersGrassField.HabitatIdOf(s.Habitat) << 12)
                        | ((s.Broad ? 1 : 0) << 11) | (s.Roll & 0x7FF);
                if (textureOf.TryGetValue(key, out string t)) return t;
                var entry = chooser.Choose(s);
                t = entry?.Path ?? "(none)";
                textureOf[key] = t;
                return t;
            }

            var buckets = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < sites.Count; i++)
            {
                var k = (Mathf.FloorToInt(sites[i].Position.x / ViewWidthM),
                         Mathf.FloorToInt(sites[i].Position.y / ViewHeightM));
                if (!buckets.TryGetValue(k, out var list)) buckets[k] = list = new List<int>();
                list.Add(i);
            }

            int worstOld = 0, worstNew = 0, worstTufts = 0;
            var oldBatches = new HashSet<(string, int)>();
            var newBatches = new HashSet<(int, int, string)>();
            var candidates = new List<int>();

            foreach (var kv in buckets)
            {
                var centre = new Vector2((kv.Key.Item1 + 0.5f) * ViewWidthM,
                                         (kv.Key.Item2 + 0.5f) * ViewHeightM);
                candidates.Clear();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (buckets.TryGetValue((kv.Key.Item1 + dx, kv.Key.Item2 + dy), out var near))
                        candidates.AddRange(near);

                oldBatches.Clear();
                newBatches.Clear();
                int tufts = 0;

                foreach (int i in candidates)
                {
                    var p = sites[i].Position;
                    if (Mathf.Abs(p.x - centre.x) > ViewWidthM * 0.5f) continue;
                    if (Mathf.Abs(p.y - centre.y) > ViewHeightM * 0.5f) continue;
                    tufts++;

                    string tex = TextureFor(sites[i]);
                    int order = YSortSprite.OrderFor(
                        p.y, HiddenHarbours.Core.SortingBands.DecorBase,
                        HiddenHarbours.Core.SortingBands.OrdersPerMetre,
                        HiddenHarbours.Core.SortingBands.DecorFloor,
                        HiddenHarbours.Core.SortingBands.DecorCeiling);
                    oldBatches.Add((tex, order));
                    newBatches.Add((Mathf.FloorToInt(p.x / ChunkWidthM),
                                    Mathf.FloorToInt(p.y / rowHeight), tex));
                }

                if (oldBatches.Count > worstOld) { worstOld = oldBatches.Count; worstTufts = tufts; }
                if (newBatches.Count > worstNew) worstNew = newBatches.Count;
            }

            Debug.Log(
                $"[StPetersGrassField] worst screen at on-foot framing ({ViewWidthM:F1}x{ViewHeightM:F1} m): " +
                $"{worstTufts:N0} tufts.\n" +
                $"  as sprites : {worstTufts:N0} renderers in {worstOld} batches " +
                "(texture x sorting order, 4 orders per metre)\n" +
                $"  as chunks  : {worstNew} draws " +
                $"({ChunkWidthM:F0} m columns x {rowHeight:F2} m rows x texture)\n" +
                $"  renderers  : {worstTufts:N0} -> {worstNew} " +
                $"({(float)worstTufts / Mathf.Max(worstNew, 1):F0}x fewer)");

            Assert.Greater(worstTufts, 0, "no screen anywhere on the island contained grass");
            Assert.Less(worstNew, worstOld,
                $"The batched path draws the worst screen in {worstNew} calls against the sprite path's " +
                $"{worstOld} — batching has stopped paying for itself. The lever is RowOrderSteps (fewer, " +
                "taller rows), never a sprite atlas: the shader bends by uv.y and an atlas remaps it.");
            Assert.Less(worstNew, worstTufts,
                "A screen of meadow costs as many draws as it has tufts — the chunks are not merging.");
        }

        // =====================================================================================
        // helper
        // =====================================================================================

        /// <summary>
        /// A <see cref="GrassField"/> carrying the baked field and a synthetic one-sprite-per-habitat
        /// palette. Synthetic on purpose: these tests are about the FIELD, and a checkout whose grass PNGs
        /// are still LFS pointers must still be able to prove the geometry round-trips.
        /// </summary>
        GrassField MakeFieldComponent(StPetersGrassField.Baked baked, out List<Object> spawned)
        {
            spawned = new List<Object>();
            var go = new GameObject("IslandGrass_test");
            spawned.Add(go);
            var field = go.AddComponent<GrassField>();

            var tex = new Texture2D(32, 32);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 32f);
            spawned.Add(sprite);
            spawned.Add(tex);

            var pools = new GrassField.HabitatPool[StPetersGrassField.HabitatIds.Length];
            for (int i = 0; i < pools.Length; i++)
                pools[i] = new GrassField.HabitatPool
                {
                    Name = StPetersGrassField.HabitatIds[i],
                    Pool = new[] { 0 },
                    BroadPool = new[] { 0 },
                };

            field.SetField(
                new Vector2(baked.Layout.OriginX, baked.Layout.OriginY),
                baked.Layout.CellSize, baked.Layout.JitterMetres, baked.Layout.SpreadMetres,
                baked.Layout.CellsX, baked.Layout.CellsY, baked.Layout.Slots, baked.Layout.Seed,
                baked.Sites,
                baked.StrawCellSize, baked.StrawCellsX, baked.StrawCellsY, baked.Straw,
                StPetersGrass.StrawTint,
                new[] { sprite }, pools, material: null);

            return field;
        }
    }
}

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersGrassFieldTests
    {
        public const string DefinitionPath = "Assets/_Project/Data/Plants5/StPetersPlantField.asset";
        public const string GroundPath = "Assets/_Project/Data/Plants5/StPetersPlantGround.asset";
        public static HiddenHarbours.Art.PlantFieldDefinition Definition() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Art.PlantFieldDefinition>(DefinitionPath);

        [Test]
        public void TheSceneHasOnePlantField_AndKeepsTheWetShorePlants()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(System.IO.File.ReadAllText(StPetersLayerRefresh.ScenePath));
            Assert.IsNotNull(scene.RootNamed(StPetersPlantFieldPatch.FieldRoot));
            foreach (string name in new[] { "IslandGrass", "IslandShrubs", "IslandFlowers" })
                Assert.Throws<StPetersLayerRefresh.Refusal>(() => scene.RootNamed(name));
            Assert.AreEqual(1, System.Linq.Enumerable.Count(scene.Docs, d =>
                d.Field("m_EditorClassIdentifier") == "HiddenHarbours.Art::HiddenHarbours.Art.PlantField"));
            Assert.AreEqual(0, System.Linq.Enumerable.Count(scene.Docs, d =>
                d.Field("m_EditorClassIdentifier") == "HiddenHarbours.Art::HiddenHarbours.Art.GrassField"));
            var ground = UnityEditor.AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(GroundPath);
            var definition = Definition();
            Vector2Int Cell(Vector2 at) => new Vector2Int(
                Mathf.FloorToInt((at.x - definition.Origin.x) / definition.CellSize),
                Mathf.FloorToInt((at.y - definition.Origin.y) / definition.CellSize));
            var region = UnityEditor.AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            float high = region.TideMeanLevel + Mathf.Abs(region.TideAmplitude);
            var shore = scene.RootNamed(StPetersPlantFieldPatch.KeptShoreRoot);
            var children = scene.TransformOf(shore).FieldRefs("m_Children");
            Assert.AreEqual(30, children.Count, "PR 5p retains the wet members of the placed shore");
            var species = new Dictionary<string, int>();
            var cells = new HashSet<Vector2Int>();
            foreach (long id in children)
            {
                var tr = scene.Require(id, "kept transform");
                var go = scene.Require(tr.FieldRef("m_GameObject"), "kept plant");
                string name = go.Field("m_Name");
                species[name] = species.TryGetValue(name, out int count) ? count + 1 : 1;
                Vector2 at = StPetersLayerRefresh.ParseVec3(tr.Field("m_LocalPosition"));
                Assert.LessOrEqual(ground.HeightMap.Field.ElevationAt(at), high, name);
                cells.Add(Cell(at));
            }
            CollectionAssert.AreEquivalent(new[] { "Eelgrass:4", "SugarKelp:8", "IrishMoss:4", "Cordgrass:4",
                "Glasswort:3", "Threesquare:2", "SaltmeadowHay:2", "BlackRush:1", "KnottedWrack:1", "SeaLettuce:1" },
                System.Linq.Enumerable.Select(species, p => p.Key + ":" + p.Value));
            Assert.AreEqual(30, StPetersPlantFieldPatch.KeptStands(scene, ground.HeightMap.Field, high).Count);
            byte[] map = ground.Decode();
            foreach (var cell in cells) Assert.AreEqual(255, map[cell.y * ground.Width + cell.x], "no scatter over a kept plant");
            foreach (var row in definition.Authored)
                Assert.IsFalse(cells.Contains(Cell(row.At)), row.Id);
        }

        [Test]
        public void AllAuthoredPlantsResolveTheirSpecies_AndKeepTheSceneRowsExactly()
        {
            var definition = Definition(); Assert.IsNotNull(definition);
            Assert.AreEqual(9139, definition.Authored.Length);
            var ids = new HashSet<string>();
            foreach (var site in definition.Authored)
            {
                Assert.IsTrue(ids.Add(site.Id), site.Id);
                Assert.IsNotNull(System.Linq.Enumerable.SingleOrDefault(definition.Species, d => d.Code == site.Species), site.Id);
                Assert.That(site.Stage, Is.InRange(0, 2)); Assert.That(site.Variant, Is.InRange(0, 3));
            }
            int scenePlants = 0;
            foreach (var scene in StPetersLayerRefresh.LoadStPetersKeyScenes())
            foreach (var piece in scene.Pieces)
            {
                if (piece.Kind != "plant") continue;
                var actual = System.Linq.Enumerable.Single(definition.Authored, s => s.Id == piece.Id);
                Assert.AreEqual(piece.At, actual.At, piece.Id);
                Assert.AreEqual(piece.Piece, System.Linq.Enumerable.Single(definition.Species, d => d.Code == actual.Species).SpeciesKey);
                scenePlants++;
            }
            Assert.AreEqual(26, scenePlants);
        }

        [Test]
        public void TheNamedPatchIsIdempotent_AndRejectsADanglingForeignReference()
        {
            string before = "%YAML 1.1\n"; var transforms = new List<int>();
            for (int i = 0; i < StPetersPlantFieldPatch.RetiredRoots.Length; i++)
            {
                int go = 100 + 2 * i, tr = go + 1; transforms.Add(tr);
                before += Pair(go, tr, StPetersPlantFieldPatch.RetiredRoots[i]);
            }
            transforms.Add(201);
            before += Pair(200, 201, StPetersPlantFieldPatch.KeptShoreRoot)
                .Replace("  m_Children: []", "  m_Children:" + "\n  - {fileID: 205}\n  - {fileID: 207}");
            before += Pair(204, 205, "WetShore").Replace("  m_Father: {fileID: 0}", "  m_Father: {fileID: 201}");
            before += Pair(206, 207, "DryShore").Replace("  m_Father: {fileID: 0}", "  m_Father: {fileID: 201}");
            before += "--- !u!1660057539 &900\nSceneRoots:\n  m_Roots:\n";
            foreach (int tr in transforms) before += "  - {fileID: " + tr + "}\n";
            string added = Pair(1000, 1001, StPetersPlantFieldPatch.FieldRoot);
            var kept = new HashSet<long> { 204 };
            var patch = StPetersPlantFieldPatch.Apply(before, added, kept);
            CollectionAssert.AreEquivalent(new[] { "IslandGrass", "IslandShrubs", "IslandFlowers", "ShorePlants/DryShore" },
                patch.DeletedObjects);
            CollectionAssert.AreEqual(new[] { StPetersPlantFieldPatch.FieldRoot }, patch.AddedObjects);
            Assert.AreEqual(8, patch.DeletedDocuments.Length);
            Assert.AreEqual(patch.Text, StPetersPlantFieldPatch.Apply(patch.Text, added, kept).Text);
            var parsed = StPetersLayerRefresh.SceneYaml.Parse(patch.Text);
            CollectionAssert.AreEqual(new[] { 205L }, parsed.TransformOf(parsed.RootNamed("ShorePlants")).FieldRefs("m_Children"));
            Assert.AreEqual(StPetersLayerRefresh.SceneYaml.Parse(before).Get(204).Text, parsed.Get(204).Text);
            string bad = before + "--- !u!114 &950\nMonoBehaviour:\n  m_GameObject: {fileID: 0}\n  target: {fileID: 100}\n";
            Assert.Throws<System.InvalidOperationException>(() => StPetersPlantFieldPatch.Apply(bad, added, kept));
        }

        static string Pair(int go, int tr, string name) =>
            "--- !u!1 &" + go + "\nGameObject:\n  m_Name: " + name + "\n  m_Component:\n  - component: {fileID: " + tr + "}\n" +
            "--- !u!4 &" + tr + "\nTransform:\n  m_GameObject: {fileID: " + go + "}\n  m_Father: {fileID: 0}\n  m_Children: []\n";
    }
}
