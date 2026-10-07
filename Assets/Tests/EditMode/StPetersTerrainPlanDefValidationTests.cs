using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ST PETERS' TERRAIN PLAN IS SOUND DATA</b> (terrain PR 5; rule 2, content is data, one entity per file, with a
    /// stable id). The plan's folder, <c>Data/Terrain/StPetersPlan/</c>, holds the <see cref="RegionTerrainPlanDef"/> and
    /// every Def it carries. <see cref="TerrainPlanValidation.Validate"/> is the rule book, and this fixture holds the
    /// folder to it:
    /// <list type="bullet">
    /// <item>every id is <c>type.snake_case</c>, its type its class's (<c>coast.</c>, <c>pond.</c>, <c>path.</c>, …), and
    /// unique;</item>
    /// <item>each file holds one Def;</item>
    /// <item>every reference resolves to a Def the plan carries, every Def in the folder is carried, and each path's
    /// label paints the kit's slot for it;</item>
    /// <item>the ids are stable: every id the maps were derived from (the manifest's) is still in the folder, or still
    /// read by the plan from its other folders (the village's routes, the ground file and its asks). Ids are
    /// append-only; a new Def adds one, none is renamed or withdrawn.</item>
    /// </list>
    ///
    /// <para><b>The rule book is tested too.</b> A folder that passes proves nothing if the rules cannot fail, so a
    /// minimal plan made here passes clean, and each rule is broken on it once, alone: it must name that fault and
    /// only that one.</para>
    /// </summary>
    public class StPetersTerrainPlanDefValidationTests
    {
        // ============================ THE PLAN'S FOLDER ============================

        [Test]
        public void ThePlansFolder_Validates()
        {
            var problems = StPetersTerrainPlan.Validate();
            Assert.IsEmpty(problems, $"{StPetersTerrainPlan.DefsFolder}:\n  " + string.Join("\n  ", problems));
        }

        [Test]
        public void EachFile_HoldsOneDef_WithAnIdOfItsType()
        {
            var defs = StPetersTerrainPlan.LoadDefs(out var files);
            Assert.Greater(defs.Count, 1, $"{StPetersTerrainPlan.DefsFolder} holds no plan");
            for (int i = 0; i < defs.Count; i++)
            {
                Assert.IsNotNull(defs[i], $"{files[i]} holds no Def");
                Assert.AreEqual(1, files.Count(f => f == files[i]), $"{files[i]} holds more than one Def");
                string id = TerrainPlanValidation.IdOf(defs[i]);
                Assert.IsTrue(TerrainPlanValidation.IsId(id), $"{files[i]}: '{id}' is not type.snake_case");
                Assert.IsTrue(TerrainPlanValidation.IdTypes.TryGetValue(defs[i].GetType(), out var type), $"{files[i]}: {defs[i].GetType().Name} is not a plan Def");
                if (TerrainPlanValidation.IdTypesAlso.TryGetValue(defs[i].GetType(), out var also) && id.StartsWith(also + ".")) continue;  // a key scene's pool
                StringAssert.StartsWith(type + ".", id, $"{files[i]}: a {defs[i].GetType().Name}'s id");
            }
            var dup = defs.Select(TerrainPlanValidation.IdOf).GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dup, "ids used twice: " + string.Join(", ", dup));
        }

        [Test]
        public void Ids_AreStable_EveryIdTheMapsWereDerivedFromIsStillThere()
        {
            Assert.IsTrue(File.Exists(StPetersTerrainPlan.ManifestPath), $"no manifest at {StPetersTerrainPlan.ManifestPath}");
            var recorded = Regex.Matches(File.ReadAllText(StPetersTerrainPlan.ManifestPath), "^    \\[\"([^\"]+)\", \"", RegexOptions.Multiline)
                                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Greater(recorded.Count, 1, "the manifest records no Defs");
            var now = new HashSet<string>(StPetersTerrainPlan.LoadDefs(out _).Select(TerrainPlanValidation.IdOf));
            // the Defs the plan reads from other folders, as the map writer records them: the village's routes, and the
            // ground file with its asks
            var plan = StPetersTerrainPlan.LoadPlan();
            foreach (var route in plan.Routes ?? new RouteDef[0])
                if (route != null) now.Add(route.Id);
            if (plan.Ground != null)
            {
                now.Add(plan.Ground.Id);
                foreach (var ask in plan.Ground.Asks ?? new GroundAskDef[0])
                    if (ask != null) now.Add(ask.Id);
            }
            var gone = recorded.Where(id => !now.Contains(id)).ToList();
            Assert.IsEmpty(gone, "ids are append-only; the maps were derived from these, and they are gone: " + string.Join(", ", gone));
        }

        // ============================ THE RULE BOOK ITSELF ============================

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void DestroyWhatWasMade()
        {
            foreach (var o in _made)
                if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T Make<T>(string id) where T : ScriptableObject
        {
            var d = ScriptableObject.CreateInstance<T>();
            _made.Add(d);
            typeof(T).GetField("Id").SetValue(d, id);
            return d;
        }

        /// <summary>A plan that passes: a recipe, a beach and the crossing on it, and one path.</summary>
        private sealed class Minimal
        {
            public RegionTerrainPlanDef Plan;
            public CoastRecipeDef Recipe;
            public CoastSectionDef Beach, Crossing;
            public PathDef Path;
            public List<ScriptableObject> Defs;
            public List<string> Files;

            public List<string> Validate() => TerrainPlanValidation.Validate(Plan, Defs, Files);
        }

        private Minimal MakeMinimal()
        {
            var m = new Minimal
            {
                Plan = Make<RegionTerrainPlanDef>("terrain_plan.test"),
                Recipe = Make<CoastRecipeDef>("coast_recipe.test_beach"),
                Beach = Make<CoastSectionDef>("coast.test_beach"),
                Crossing = Make<CoastSectionDef>("coast.test_crossing"),
                Path = Make<PathDef>("path.test_path"),
            };
            m.Beach.Recipe = m.Recipe;
            m.Crossing.Recipe = m.Recipe;
            m.Path.Label = "path";
            m.Path.Material = "path";
            m.Path.Ruts = false;
            m.Path.Weight = 1f;
            m.Plan.Recipes = new[] { m.Recipe };
            m.Plan.Sections = new[] { m.Beach };
            m.Plan.Crossing = m.Crossing;
            m.Plan.Paths = new[] { m.Path };
            m.Defs = new List<ScriptableObject> { m.Plan, m.Recipe, m.Beach, m.Crossing, m.Path };
            m.Files = m.Defs.Select(d => TerrainPlanValidation.IdOf(d) + ".asset").ToList();
            return m;
        }

        /// <summary>Where <paramref name="d"/> sits, by reference (Unity's Equals compares instance ids).</summary>
        private static int At(List<ScriptableObject> defs, ScriptableObject d) => defs.FindIndex(x => ReferenceEquals(x, d));

        private static void AssertTheOneFault(List<string> problems, string fault)
        {
            Assert.AreEqual(1, problems.Count, $"expected only '{fault}', got:\n  " + string.Join("\n  ", problems));
            StringAssert.Contains(fault, problems[0]);
        }

        [Test]
        public void TheRules_PassAMinimalPlan()
        {
            var problems = MakeMinimal().Validate();
            Assert.IsEmpty(problems, string.Join("\n  ", problems));
        }

        [Test]
        public void TheRules_RefuseAnIdThatIsNotSnakeCase()
        {
            var m = MakeMinimal();
            m.Beach.Id = "coast.Test-Beach";
            AssertTheOneFault(m.Validate(), "id 'coast.Test-Beach' is not type.snake_case");
        }

        [Test]
        public void TheRules_RefuseAnIdOfAnotherType()
        {
            var m = MakeMinimal();
            m.Beach.Id = "pond.test_beach";
            AssertTheOneFault(m.Validate(), "id 'pond.test_beach' is not a coast id");
        }

        [Test]
        public void TheRules_RefuseAnIdUsedTwice()
        {
            var m = MakeMinimal();
            var twin = Make<PathDef>("path.test_path");
            twin.Label = "path";
            m.Plan.Paths = new[] { m.Path, twin };
            m.Defs.Add(twin);
            m.Files.Add("path.test_path_twin.asset");
            AssertTheOneFault(m.Validate(), "id 'path.test_path' is also path.test_path.asset");
        }

        [Test]
        public void TheRules_RefuseTwoDefsInOneFile()
        {
            var m = MakeMinimal();
            m.Files[At(m.Defs, m.Path)] = m.Files[At(m.Defs, m.Beach)];
            AssertTheOneFault(m.Validate(), "coast.test_beach.asset: a second Def in one file");
        }

        [Test]
        public void TheRules_RefuseAReferenceThatDoesNotResolve()
        {
            var m = MakeMinimal();
            m.Beach.Recipe = null;
            AssertTheOneFault(m.Validate(), "coast.test_beach's Recipe does not resolve");
        }

        [Test]
        public void TheRules_RefuseAPlanEntryWhoseAssetIsGone()
        {
            var m = MakeMinimal();
            int at = At(m.Defs, m.Path);
            m.Files.RemoveAt(at);
            m.Defs.RemoveAt(at);
            Object.DestroyImmediate(m.Path);
            AssertTheOneFault(m.Validate(), "the plan's Paths[0] does not resolve");
        }

        [Test]
        public void TheRules_RefuseAReferenceToADefThePlanDoesNotCarry()
        {
            var m = MakeMinimal();
            m.Beach.Recipe = Make<CoastRecipeDef>("coast_recipe.stray");
            AssertTheOneFault(m.Validate(), "coast.test_beach's Recipe (coast_recipe.stray) is not in the plan");
        }

        [Test]
        public void TheRules_RefuseADefInTheFolderThatThePlanDoesNotCarry()
        {
            var m = MakeMinimal();
            m.Defs.Add(Make<PondDef>("pond.test_pond"));
            m.Files.Add("pond.test_pond.asset");
            AssertTheOneFault(m.Validate(), "pond.test_pond is in the folder but not in the plan");
        }

        [Test]
        public void TheRules_RefuseAPathThatPaintsAnotherSlot()
        {
            var m = MakeMinimal();
            m.Path.Material = "dirt";
            AssertTheOneFault(m.Validate(), "path.test_path: a path paints path, not dirt");
        }
    }
}
