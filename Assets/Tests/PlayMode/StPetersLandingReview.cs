#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal.Filters;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string R17Folder = "Evidence~/stp-kits/k0/ruling-17";
        static readonly Rect R17Floor = new Rect(183, 2.533452f, 30.5f, 3.71875f);

        sealed class R17Listener : ITestListener
        {
            public void TestStarted(ITest test) { }
            public void TestFinished(ITestResult result) { }
            public void TestOutput(TestOutput output) { }
        }

        static IEnumerable<NUnit.Framework.Internal.TestMethod> R17Cases(ITest test)
        {
            if (test is NUnit.Framework.Internal.TestMethod method) { yield return method; yield break; }
            foreach (ITest child in test.Tests)
                foreach (var leaf in R17Cases(child)) yield return leaf;
        }

        static void R17RunScopedContracts()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-stpR17Scoped")) return;
            var groups = new Dictionary<string, string[]> {
                { "HiddenHarbours.Tests.EditMode", new[] {
                    "HiddenHarbours.Tests.EditMode.StPetersLandingCleanDeckTests",
                    "HiddenHarbours.Tests.EditMode.StPetersGrassFieldTests",
                    "HiddenHarbours.Tests.EditMode.StPetersLandingDeckTests" } },
                { "HiddenHarbours.Tests.Art.EditMode", new[] {
                    "HiddenHarbours.Tests.Art.EditMode.SunCasterRulesTests",
                    "HiddenHarbours.Tests.Art.EditMode.SunCasterInstallerTests" } } };
            var xml = new System.Xml.XmlDocument();
            var root = xml.CreateElement("test-run"); xml.AppendChild(root);
            int passed = 0, failed = 0, skipped = 0;
            var original = SceneManager.GetActiveScene();
            var scope = SceneManager.CreateScene("StPetersLandingContractScope");
            SceneManager.SetActiveScene(scope);
            try
            {
                foreach (var group in groups)
                {
                    var builder = new DefaultTestAssemblyBuilder();
                    ITest tree = builder.Build(Assembly.Load(group.Key), new Dictionary<string, object> { { "LOAD", group.Value } });
                    foreach (var test in R17Cases(tree))
                    {
                        Type type = test.Method.MethodInfo.DeclaringType;
                        if (!group.Value.Contains(type.FullName)) continue;
                        var row = xml.CreateElement("test-case"); root.AppendChild(row);
                        row.SetAttribute("fullname", test.FullName);
                        var contextProperty = typeof(NUnit.Framework.Internal.TestExecutionContext).GetProperty("CurrentContext");
                        var previous = NUnit.Framework.Internal.TestExecutionContext.CurrentContext;
                        var context = new NUnit.Framework.Internal.TestExecutionContext(previous) {
                            CurrentTest = test, CurrentResult = test.MakeTestResult(), TestObject = Activator.CreateInstance(type) };
                        contextProperty.GetSetMethod(true).Invoke(null, new object[] { context });
                        Exception error = null;
                        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        var setups = type.GetMethods(flags).Where(m => m.IsDefined(typeof(SetUpAttribute), true)).ToArray();
                        var teardowns = type.GetMethods(flags).Where(m => m.IsDefined(typeof(TearDownAttribute), true)).ToArray();
                        try
                        {
                            foreach (var setup in setups ?? Array.Empty<MethodInfo>()) setup.Invoke(context.TestObject, null);
                            var parameters = (NUnit.Framework.Internal.TestCaseParameters)typeof(NUnit.Framework.Internal.TestMethod).GetField("parms", flags).GetValue(test);
                            test.Method.MethodInfo.Invoke(context.TestObject, parameters != null ? parameters.Arguments : Array.Empty<object>());
                        }
                        catch (Exception ex) { error = ex; }
                        finally
                        {
                            try { foreach (var teardown in teardowns ?? Array.Empty<MethodInfo>()) teardown.Invoke(context.TestObject, null); }
                            catch (Exception ex) { if (error == null) error = ex; }
                            contextProperty.GetSetMethod(true).Invoke(null, new object[] { previous });
                        }
                        while (error is TargetInvocationException wrapped && wrapped.InnerException != null) error = wrapped.InnerException;
                        if (error == null || error.GetType().Name == "SuccessException") { passed++; row.SetAttribute("result", "Passed"); }
                        else if (error.GetType().Name == "IgnoreException" || error.GetType().Name == "InconclusiveException")
                        { skipped++; row.SetAttribute("result", "Skipped"); }
                        else
                        {
                            failed++; row.SetAttribute("result", "Failed");
                            var failure = xml.CreateElement("failure"); row.AppendChild(failure);
                            var message = xml.CreateElement("message"); message.InnerText = error.ToString(); failure.AppendChild(message);
                        }
                    }
                }
                root.SetAttribute("total", (passed + failed + skipped).ToString());
                root.SetAttribute("passed", passed.ToString()); root.SetAttribute("failed", failed.ToString()); root.SetAttribute("skipped", skipped.ToString());
                root.SetAttribute("result", failed == 0 ? "Passed" : "Failed");
                Directory.CreateDirectory(R17Folder); xml.Save(Path.Combine(R17Folder, "scoped-editmode.xml"));
                Assert.That(passed + failed, Is.GreaterThan(0), "the named native contracts ran on Unity's main thread");
                Assert.That(failed, Is.Zero, "scoped contract failures are recorded by name in scoped-editmode.xml");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(scope);
            }
        }

        [Serializable] sealed class R17Removed { public PlantFieldSite[] Authored; }
        [Serializable] sealed class R17Inventory
        {
            public string phase;
            public R17Root[] roots;
        }
        [Serializable] sealed class R17Root
        {
            public string name, path, source, board;
            public Vector3 root;
            public bool spriteShadow;
        }

        static string R17Path(Transform t)
        {
            string value = t.name;
            for (var p = t.parent; p != null; p = p.parent) value = p.name + "/" + value;
            return value;
        }

        static int R17InventoryNow(string phase, PlantField field, SpriteRenderer deck)
        {
            var rows = new List<R17Root>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                Vector3 at = renderer.transform.position;
                if (!HiddenHarbours.World.StandablePlatform.Contains(R17Floor, at) && renderer != deck) continue;
                string name = renderer.name;
                bool fitting = name.StartsWith("Fitting_", StringComparison.Ordinal) || name.StartsWith("lanternPost_", StringComparison.Ordinal) ||
                    name == "prop.stp_landing_bait_barrel" || name == "prop.stp_landing_rope" || name == "prop.stp_landing_strand_bench" || renderer == deck;
                rows.Add(new R17Root { name = name, path = R17Path(renderer.transform), root = at,
                    source = renderer.gameObject.scene.name == "StPeters" && (renderer.hideFlags & HideFlags.DontSave) == 0 ? "scene doc" : "runtime",
                    board = fitting ? "board fitting: keep" : "name for the seat; leave unchanged",
                    spriteShadow = renderer.GetComponent<SpriteShadow>() != null });
            }
            var sites = (IEnumerable)typeof(PlantField).GetField("_sites", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(field);
            foreach (object site in sites)
            {
                Type t = site.GetType();
                float x = (float)t.GetField("X").GetValue(site), y = (float)t.GetField("Y").GetValue(site);
                float z = (float)t.GetField("Z").GetValue(site);
                var at = new Vector2(x, y + z * field.HeightProjection);
                if (!HiddenHarbours.World.StandablePlatform.Contains(R17Floor, at)) continue;
                var authored = field.Definition.Authored.FirstOrDefault(a => (a.At - new Vector2(x, y)).sqrMagnitude < .000001f);
                var species = (PlantSpeciesDef)t.GetField("Def").GetValue(site);
                rows.Add(new R17Root { name = authored != null ? authored.Id : species.SpeciesKey,
                    path = "StPetersPlantField/" + species.SpeciesKey, root = at,
                    source = authored != null ? "plant-field authored data rendered in runtime chunks" : "plant-field scatter",
                    board = "no plant on the board deck" });
            }
            Directory.CreateDirectory(R17Folder);
            File.WriteAllText(Path.Combine(R17Folder, "roots-" + phase + ".json"), JsonUtility.ToJson(new R17Inventory { phase = phase, roots = rows.ToArray() }, true));
            return rows.Count(row => row.source.StartsWith("plant-field", StringComparison.Ordinal));
        }

        static void R17RecordPlantBeforeAndAfter(SpriteRenderer deck)
        {
            var field = Object.FindAnyObjectByType<PlantField>();
            Assert.IsNotNull(field);
            PlantFieldDefinition actual = field.Definition;
            var legacy = Object.Instantiate(actual);
            try
            {
                var removed = JsonUtility.FromJson<R17Removed>(File.ReadAllText(Path.Combine(R17Folder, "removed-authored-plants.json")));
                legacy.Authored = actual.Authored.Concat(removed.Authored).ToArray();
                field.Definition = legacy;
                field.Rebuild();
                Assert.That(R17InventoryNow("before", field, deck), Is.EqualTo(24), "the previous data exposes the 24 plant roots");
            }
            finally
            {
                field.Definition = actual;
                field.Rebuild();
                Object.DestroyImmediate(legacy);
            }
            Assert.That(R17InventoryNow("after", field, deck), Is.Zero, "no authored or scattered plant root lies inside the floor");
            Assert.That(field.Definition.Authored.Any(a => HiddenHarbours.World.StandablePlatform.Contains(R17Floor, a.At)), Is.False);
        }

        IEnumerator R17FloorShadowProof(SpriteRenderer deck)
        {
            var actual = Resources.Load<SunCasterRules>(nameof(SunCasterRules));
            var legacy = Object.Instantiate(actual);
            legacy.ExcludedNames = actual.ExcludedNames.Where(n => n != "WharfPass2Deck").ToArray();
            var control = new GameObject("LandingFloorCasterControl");
            SpriteShadow shadow = null;
            SpriteRenderer[] standing = deck.transform.parent.parent.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r != deck && r.GetComponent<SpriteShadow>() != null).ToArray();
            try
            {
                var installer = control.AddComponent<SunCasterInstaller>();
                installer.Configure(legacy);
                shadow = deck.GetComponent<SpriteShadow>();
                Assert.IsNotNull(shadow, "the old data installs the actual whole-deck renderer as a caster");
                yield return null; yield return null;
                SavePlate("floor-caster-control-before.png", Capture());
                File.WriteAllText(Path.Combine(R17Folder, "floor-caster-proof.txt"),
                    R17Path(deck.transform) + "; sprite=" + deck.sprite.name + "; previous data SpriteShadow=true\n");
            }
            finally
            {
                if (shadow != null) Object.DestroyImmediate(shadow);
                Object.DestroyImmediate(control);
                Object.DestroyImmediate(legacy);
                SunCasterInstaller.Instance.Configure(actual);
            }
            yield return null; yield return null;
            Assert.IsNull(deck.GetComponent<SpriteShadow>(), "the exact shipped data exclusion leaves this floor unshadowed");
            File.AppendAllText(Path.Combine(R17Folder, "floor-caster-proof.txt"), "current data SpriteShadow=false; standing fittings retain the installer rules\n");
            Assert.That(standing.All(r => r != null && r.GetComponent<SpriteShadow>() != null), Is.True,
                "piles and standing fittings keep their casters");
            File.AppendAllText(Path.Combine(R17Folder, "floor-caster-proof.txt"), "standing fitting casters retained=" + standing.Length + "\n");
            SavePlate("floor-caster-control-after.png", Capture());
        }
    }
}
#endif
