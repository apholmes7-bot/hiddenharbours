using System;
using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class PlantClumpGround : MonoBehaviour, IPlantFieldGround
    {
        public bool Excluded;
        public int PlantSeason => 1;
        public PlantGroundSample Sample(float x, float y) => new PlantGroundSample(0, 0, 1, Excluded);
        public float WaterLevelAt(float x, float y) => 0;
    }

    /// <summary>
    /// The clump law (PlantClumpLaw + PlantClumpDef): it is a pure function of position and seed, off is
    /// the scatter that shipped, and on it really gathers the plants.
    /// </summary>
    public class PlantClumpLawTests
    {
        const int Seed = 9109;            // the St Peters field's seed, as PlantFieldScatterTests uses
        const double CellArea = 0.25;     // a 0.5 m cell
        const float CellMetres = 0.5f;
        const float Density = 0.6f;       // the meadow's order of density
        const float Young = 0.3f;
        const int Side = 400, Box = 16;   // 200 m of ground in 8 m boxes
        const string FieldPath = "Assets/_Project/Data/Plants5/StPetersPlantField.asset";

        readonly List<Object> _owned = new List<Object>();
        [TearDown] public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }
        T Own<T>(T item) where T : Object { _owned.Add(item); return item; }

        // Every number stated here, so a retune of the shipped asset or of the class defaults moves no bar.
        PlantClumpDef Law(float strength, float clumpGain = 1.45f)
        {
            var law = Own(ScriptableObject.CreateInstance<PlantClumpDef>());
            law.Strength = strength; law.PatchMetres = 6; law.DetailMetres = 2.2f; law.DetailWeight = 0.5f;
            law.BareBelow = 0.38f; law.FullAbove = 0.56f; law.ClumpGain = clumpGain;
            law.RimStepChance = 0.8f; law.SecondStepShare = 0.35f; law.CoreGrowChance = 0.5f;
            law.TonePatchMetres = 11; law.ToneFollowsClump = 0.5f; law.ToneStrayChance = 0.15f;
            law.ToneSteps = new[] { new Color(1.07f, 1.04f, 0.9f), new Color(1.03f, 1.02f, 0.96f), Color.white,
                new Color(0.95f, 0.98f, 0.96f), new Color(0.89f, 0.94f, 0.93f) };
            return law;
        }

        static PlantMaterialRecipe Row(float density = Density) => new PlantMaterialRecipe
        { Material = 0, Density = density, Choices = new[] { new PlantChoice { Species = 1, Weight = 1 } } };

        static double X(int cell) => (cell + 0.5) * CellMetres;

        // One synthetic meadow, cell by cell, as PlantField.Rebuild grows it: code 0 = no plant.
        static void Grow(PlantClumpDef law, int seed, byte[] codes, int[] tones)
        {
            var row = Row();
            bool on = PlantClumpLaw.IsOn(law);
            for (int y = 0; y < Side; y++) for (int x = 0; x < Side; x++)
            {
                double field = on ? PlantClumpLaw.Field(X(x), X(y), seed, law) : 0;
                byte code = PlantFieldScatter.Choose(row, 0, CellArea, x, y, seed, Young,
                    on ? PlantClumpLaw.DensityGain(field, law) : 1.0);
                if (code != 0 && on)
                    code = PlantFieldScatter.Pack(PlantFieldScatter.Species(code),
                        PlantClumpLaw.StageFor(PlantFieldScatter.Stage(code), field, x, y, seed, law));
                codes[y * Side + x] = code;
                if (tones != null) tones[y * Side + x] = code != 0 && on ? PlantClumpLaw.ToneFor(field, X(x), X(y), x, y, seed, law) : -1;
            }
        }

        // The spread of local density: standard deviation over mean of the plant count per box.
        static double Spread(byte[] codes, int box, out double emptyShare)
        {
            int n = Side / box; double sum = 0, squares = 0; int empty = 0;
            for (int by = 0; by < n; by++) for (int bx = 0; bx < n; bx++)
            {
                int count = 0;
                for (int y = 0; y < box; y++) for (int x = 0; x < box; x++)
                    if (codes[(by * box + y) * Side + bx * box + x] != 0) count++;
                sum += count; squares += (double)count * count; if (count == 0) empty++;
            }
            double mean = sum / (n * n);
            emptyShare = (double)empty / (n * n);
            return Math.Sqrt(Math.Max(0, squares / (n * n) - mean * mean)) / mean;
        }

        static int Count(byte[] codes) { int n = 0; foreach (byte c in codes) if (c != 0) n++; return n; }

        [Test]
        public void TheSameSeedGrowsTheSameClumps_AndAnotherSeedGrowsOthers()
        {
            var law = Law(1);
            var a = new byte[Side * Side]; var b = new byte[Side * Side]; var other = new byte[Side * Side];
            var toneA = new int[Side * Side]; var toneB = new int[Side * Side];
            Grow(law, Seed, a, toneA); Grow(law, Seed, b, toneB); Grow(law, Seed + 1, other, null);
            CollectionAssert.AreEqual(a, b, "the law read something besides position and seed");
            CollectionAssert.AreEqual(toneA, toneB);
            int differ = 0;
            for (int i = 0; i < a.Length; i++) if ((a[i] != 0) != (other[i] != 0)) differ++;
            Assert.Greater(differ, a.Length / 20, "another world seed must move the clumps");
        }

        [Test]
        public void StrengthZeroAndNoAsset_AreOff_AndTheArithmeticAgrees()
        {
            Assert.IsFalse(PlantClumpLaw.IsOn(null));
            var off = Law(0);
            Assert.IsFalse(PlantClumpLaw.IsOn(off));
            Assert.IsTrue(PlantClumpLaw.IsOn(Law(0.01f)));
            // The field never calls these when the law is off; they must still say "no change" on their
            // own, so the switch and the arithmetic cannot come to disagree.
            for (int i = 0; i <= 100; i++)
            {
                double field = i / 100.0;
                Assert.AreEqual(1.0, PlantClumpLaw.DensityGain(field, off), "gain at field " + field);
                for (int stage = 0; stage <= 2; stage++)
                    Assert.AreEqual(stage, PlantClumpLaw.StageFor(stage, field, i, 7 * i, Seed, off));
            }
            for (int step = 0; step < off.ToneSteps.Length; step++)
                Assert.AreEqual(Color.white, PlantClumpLaw.ToneColor(step, off));
        }

        // The shipped scatter, restated from PlantFieldScatter.Choose as it stood before the law: a cell
        // grows when its hash (salt 3) is under density x area, and stands young when its hash (salt 6)
        // is under the recipe's young chance. The counts are the offline model's, not this code's.
        [Test]
        public void AFieldWithNoLaw_IsTheShippedScatterCellForCell_AndStrengthZeroIsThatField()
        {
            const int side = 128, shippedSites = 2375, shippedYoung = 706;
            var none = Field(side, null); var zero = Field(side, Law(0));
            string plane = none.EncodeCurrentField();
            Assert.AreEqual(plane, zero.EncodeCurrentField(), "strength 0 moved a plant");
            Assert.AreEqual(none.PlantCount, zero.PlantCount);
            var rootsNone = new List<Vector2>(); var rootsZero = new List<Vector2>();
            none.CopyGrassRootsTo(rootsNone); zero.CopyGrassRootsTo(rootsZero);
            CollectionAssert.AreEqual(rootsNone, rootsZero);

            byte[] cells = PlantFieldCodec.Decode(plane, side * side);
            int sites = 0, young = 0;
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                bool grows = PlantFieldScatter.Hash(x, y, Seed, 3) < Math.Min(1, Density * CellArea);
                byte shipped = grows ? PlantFieldScatter.Pack(1, PlantFieldScatter.Hash(x, y, Seed, 6) < Young ? 1 : 0) : (byte)0;
                Assert.AreEqual(shipped, cells[y * side + x], $"cell {x},{y}");
                if (grows) { sites++; if (PlantFieldScatter.Stage(shipped) == 1) young++; }
            }
            Assert.AreEqual(shippedSites, sites); Assert.AreEqual(shippedYoung, young);
            Assert.AreEqual(shippedSites, none.PlantCount);
            Assert.AreNotEqual(plane, Field(side, Law(0.8f)).EncodeCurrentField(), "the law at strength changed nothing");
        }

        [Test]
        public void TheLawGathersThePlants_FarPastTheUniformScatter()
        {
            var uniform = new byte[Side * Side]; var clumped = new byte[Side * Side];
            Grow(null, Seed, uniform, null); Grow(Law(1), Seed, clumped, null);
            double before = Spread(uniform, Box, out double emptyBefore), after = Spread(clumped, Box, out _);
            Spread(uniform, Box / 2, out double bareBefore); Spread(clumped, Box / 2, out double bareAfter);
            TestContext.WriteLine($"spread of 8 m density: uniform {before:0.000}, law {after:0.000}; " +
                $"empty 4 m boxes: uniform {bareBefore:0.000}, law {bareAfter:0.000}; " +
                $"plants: uniform {Count(uniform)}, law {Count(clumped)}");
            // The model: uniform 0.147, the law 0.498. A binomial scatter cannot leave these bars by luck.
            Assert.Less(before, 0.2, "the uniform scatter is not uniform; the comparison is void");
            Assert.Greater(after, 0.4);
            Assert.Greater(after, before * 2.5, "local density must vary far more than a uniform scatter's");
            Assert.AreEqual(0, emptyBefore, 1e-9, "the uniform scatter leaves no 8 m of ground bare");
            Assert.AreEqual(0, bareBefore, 0.002, "the uniform scatter leaves next to no 4 m of ground bare");
            Assert.Greater(bareAfter, 0.05, "the law must leave bare ground");
            // It rearranges the meadow; it does not strip it or flood it.
            Assert.That(Count(clumped), Is.InRange(Count(uniform) * 6 / 10, Count(uniform) * 11 / 10));
        }

        [Test]
        public void BareGroundGrowsNothing_AndAGainOfOneOnlyEverThins()
        {
            var full = Law(1);
            var thin = Law(0.7f, clumpGain: 1);
            var uniform = new byte[Side * Side]; var clumped = new byte[Side * Side]; var thinned = new byte[Side * Side];
            Grow(null, Seed, uniform, null); Grow(full, Seed, clumped, null); Grow(thin, Seed, thinned, null);
            int bare = 0, removed = 0;
            for (int y = 0; y < Side; y++) for (int x = 0; x < Side; x++)
            {
                int i = y * Side + x;
                double field = PlantClumpLaw.Field(X(x), X(y), Seed, full);
                Assert.That(field, Is.InRange(0.0, 1.0));
                Assert.That(PlantClumpLaw.DensityGain(field, full), Is.InRange(0.0, full.ClumpGain + 1e-9));
                if (PlantClumpLaw.Core(field, full) <= 0) { bare++; Assert.AreEqual(0, clumped[i], $"a plant on bare ground at {x},{y}"); }
                if (thinned[i] != 0) Assert.AreNotEqual(0, uniform[i], $"a gain of 1 added a plant at {x},{y}");
                else if (uniform[i] != 0) removed++;
            }
            Assert.Greater(bare, Side * Side / 10); Assert.Greater(removed, 0);
        }

        [Test]
        public void TheSizeStep_IsTheAtlasStage_SmallAtTheRimAndNeverPastASeedling()
        {
            var law = Law(1);
            var uniform = new byte[Side * Side]; var clumped = new byte[Side * Side];
            Grow(null, Seed, uniform, null); Grow(law, Seed, clumped, null);
            var stages = new int[3]; long rimStage = 0, coreStage = 0; int rim = 0, core = 0;
            for (int y = 0; y < Side; y++) for (int x = 0; x < Side; x++)
            {
                byte code = clumped[y * Side + x]; if (code == 0) continue;
                int stage = PlantFieldScatter.Stage(code); Assert.That(stage, Is.InRange(0, 2));
                Assert.AreEqual(1, PlantFieldScatter.Species(code), "the law never picks a species");
                stages[stage]++;
                double c = PlantClumpLaw.Core(PlantClumpLaw.Field(X(x), X(y), Seed, law), law);
                if (c >= 1) { core++; coreStage += stage; } else if (c < 0.5) { rim++; rimStage += stage; }
            }
            foreach (byte code in uniform) if (code != 0) Assert.Less(PlantFieldScatter.Stage(code), 2, "the recipe rolls no seedling");
            Assert.Greater(stages[2], 0, "the rim must show the smallest stage");
            Assert.Greater(stages[0], stages[1]); Assert.Greater(stages[1], stages[2]);
            Assert.Greater(rim, 100); Assert.Greater(core, 100);
            Assert.Greater((double)rimStage / rim, (double)coreStage / core + 0.4, "rims must stand smaller than cores");
            // A step, at its two ends: the rim's chance at 1 always steps down, a core with no growth keeps the roll.
            law.RimStepChance = 1; law.CoreGrowChance = 0;
            for (int i = 0; i < 500; i++)
            {
                Assert.GreaterOrEqual(PlantClumpLaw.StageFor(0, 0, i, 3 * i, Seed, law), 1);
                Assert.AreEqual(2, PlantClumpLaw.StageFor(2, 0, i, 3 * i, Seed, law));
                Assert.AreEqual(1, PlantClumpLaw.StageFor(1, 1, i, 3 * i, Seed, law));
            }
        }

        [Test]
        public void TheTone_IsOneStepPerPlant_SharedByNeighbours_NeverAGradient()
        {
            var law = Law(1); law.ToneStrayChance = 0;
            int steps = law.ToneSteps.Length, same = 0, pairs = 0; var seen = new bool[steps];
            for (int y = 0; y < Side; y++) for (int x = 0; x < Side - 1; x++)
            {
                int here = PlantClumpLaw.ToneFor(PlantClumpLaw.Field(X(x), X(y), Seed, law), X(x), X(y), x, y, Seed, law);
                int next = PlantClumpLaw.ToneFor(PlantClumpLaw.Field(X(x + 1), X(y), Seed, law), X(x + 1), X(y), x + 1, y, Seed, law);
                Assert.That(here, Is.InRange(0, steps - 1)); seen[here] = true;
                Assert.LessOrEqual(Math.Abs(here - next), 1, $"the tone jumped two steps between neighbours at {x},{y}");
                pairs++; if (here == next) same++;
            }
            Assert.Greater((double)same / pairs, 0.8, "neighbours must share a step: patches, not noise");
            int distinct = 0; foreach (bool s in seen) if (s) distinct++;
            Assert.GreaterOrEqual(distinct, 3, "the meadow must show more than one tone");
            // The tint of a step is one colour at any strength; a strength between only moves the steps nearer white.
            var half = Law(0.5f);
            for (int step = 0; step < steps; step++)
            {
                Color fullTint = PlantClumpLaw.ToneColor(step, law), halfTint = PlantClumpLaw.ToneColor(step, half);
                Assert.AreEqual(law.ToneSteps[step].r, fullTint.r, 1e-6f); Assert.AreEqual(1f, fullTint.a);
                Assert.AreEqual((1 + law.ToneSteps[step].g) / 2, halfTint.g, 1e-6f);
            }
            law.ToneSteps = Array.Empty<Color>();
            Assert.AreEqual(-1, PlantClumpLaw.ToneFor(0.5, 1, 1, 2, 2, Seed, law), "no steps named = no tone");
        }

        [Test]
        public void TheToneRidesTheRootChannel_AndAFieldWithNoLawCarriesNone()
        {
            var law = Law(1);
            var plain = Field(48, null, meshes: true); var toned = Field(48, law, meshes: true);
            Assert.AreEqual(0, plain.MissingCells); Assert.AreEqual(0, toned.MissingCells);
            var block = new MaterialPropertyBlock(); var roots = new List<Vector4>();
            int plants = 0;
            foreach (var filter in plain.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name != "PlantChunk") continue;
                filter.sharedMesh.GetUVs(1, roots);
                foreach (var root in roots) Assert.AreEqual(1f, root.w, "a grass plant's foot flag, and nothing else");
                filter.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetVectorArray("_PlantTone"), Is.Null.Or.Empty, "no law, no tone table");
                plants += roots.Count / 18;
            }
            Assert.AreEqual(plain.PlantCount, plants);
            var seen = new HashSet<int>(); plants = 0;
            foreach (var filter in toned.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name != "PlantChunk") continue;
                filter.sharedMesh.GetUVs(1, roots);
                for (int i = 0; i < roots.Count; i++)
                {
                    float w = roots[i].w; int tone = Mathf.FloorToInt(w * 0.5f);
                    Assert.AreEqual(1f, w - tone * 2, "the foot flag survives the tone");
                    Assert.That(tone, Is.InRange(1, law.ToneSteps.Length), "a plant under the law wears one of its steps");
                    Assert.AreEqual(roots[i - i % 18].w, w, "one plant, one tone: every vertex of it");
                    seen.Add(tone);
                }
                filter.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Vector4[] table = block.GetVectorArray("_PlantTone");
                Assert.IsNotNull(table); Assert.AreEqual(PlantClumpDef.MaxToneSteps + 1, table.Length);
                Assert.AreEqual(Vector4.one, table[0]);
                for (int step = 0; step < law.ToneSteps.Length; step++)
                    Assert.AreEqual((Vector4)PlantClumpLaw.ToneColor(step, law), table[step + 1]);
                plants += roots.Count / 18;
            }
            Assert.AreEqual(toned.PlantCount, plants); Assert.GreaterOrEqual(seen.Count, 2);
        }

        [Test]
        public void AuthoredSitesNeverReadTheLaw()
        {
            // Excluded ground scatters nothing, so the only plant is the authored one, on what would be bare ground or not.
            var rows = new List<PlantFieldSite>();
            for (int i = 0; i < 40; i++)
                rows.Add(new PlantFieldSite { Id = "clump-fixture-" + i, At = new Vector2(1.3f * i, 0.7f * i), Species = 1, Stage = i % 2, Variant = i % 4 });
            var none = Field(64, null, excluded: true, authored: rows.ToArray());
            var full = Field(64, Law(1), excluded: true, authored: rows.ToArray());
            Assert.AreEqual(40, none.PlantCount); Assert.AreEqual(40, full.PlantCount);
            var a = new List<Vector2>(); var b = new List<Vector2>(); none.CopyGrassRootsTo(a); full.CopyGrassRootsTo(b);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(none.EncodeCurrentField(), full.EncodeCurrentField());
        }

        [Test]
        public void StPetersShipsTheLawOn_WithinItsBounds()
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(FieldPath);
            Assert.IsNotNull(definition); var law = definition.Clump;
            Assert.IsNotNull(law, "the St Peters field binds no clump law");
            Assert.AreEqual("plant_clump.st_peters", law.Id);
            Assert.IsTrue(PlantClumpLaw.IsOn(law), "the law ships ON");
            Assert.That(law.Strength, Is.InRange(0.3f, 1f));
            Assert.Greater(law.FullAbove, law.BareBelow); Assert.GreaterOrEqual(law.ClumpGain, 1f);
            Assert.That(law.PatchMetres, Is.InRange(3f, 20f), "a patch far under 3 m is noise again; far over 20 m is a bald island");
            Assert.That(law.ToneSteps.Length, Is.InRange(2, PlantClumpDef.MaxToneSteps));
            foreach (Color step in law.ToneSteps)
                for (int c = 0; c < 3; c++)
                    Assert.That(step[c], Is.InRange(0.8f, 1.15f), "a tone step is a small step, not a repaint");
        }

        PlantField Field(int side, PlantClumpDef law, bool meshes = false, bool excluded = false, PlantFieldSite[] authored = null)
        {
            var species = Own(ScriptableObject.CreateInstance<PlantSpeciesDef>());
            species.Code = 1; species.Family = "grass";
            var definition = Own(ScriptableObject.CreateInstance<PlantFieldDefinition>());
            if (meshes)
            {
                var texture = Own(new Texture2D(64, 128));
                species.Pages = new[] { new PlantSheetPage { Albedo = texture, Normal = texture, Light = texture, Detail = texture } };
                var cells = new List<PlantCell>();
                for (int stage = 0; stage <= 2; stage++) for (int variant = 0; variant < 4; variant++)
                    cells.Add(new PlantCell { Stage = stage, Season = 1, Variant = variant, Tide = 0,
                        Rect = new Rect(0, 0, 32, 64), Pivot = new Vector2(16, 0), HeightM = 2 });
                species.Cells = cells.ToArray();
                var real = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(FieldPath);
                Assert.That(real, Is.Not.Null); Assert.That(real.Material, Is.Not.Null);
                definition.Material = Own(new Material(real.Material));
            }
            var recipe = Own(ScriptableObject.CreateInstance<BiomeRecipeDef>());
            recipe.YoungChance = Young; recipe.Materials = new[] { Row() };
            definition.Width = definition.Height = side; definition.Seed = Seed; definition.CellSize = CellMetres;
            definition.GroundMetresPerY = 1; definition.ChunkWidth = 8; definition.SlowTickSeconds = 0.5f;
            definition.WindMargin = 0.75f; definition.SunShadowStrength = 0;
            definition.Recipes = new[] { recipe }; definition.Species = new[] { species };
            if (authored != null) definition.Authored = authored;
            definition.Clump = law;
            var host = Own(new GameObject("PlantClumpFixture"));
            var ground = host.AddComponent<PlantClumpGround>(); ground.Excluded = excluded;
            var field = host.AddComponent<PlantField>(); field.Definition = definition; field.GroundSource = ground;
            field.Rebuild(meshes); return field;
        }
    }
}
