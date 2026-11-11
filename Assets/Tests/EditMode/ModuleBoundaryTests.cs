using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Rule 4 fences the runtime graph, including GUID-form assembly references.</summary>
    public class ModuleBoundaryTests
    {
        private const string CodeRoot = "Assets/_Project/Code";
        private const string Prefix = "HiddenHarbours.";
        private const string Core = Prefix + "Core";
        private static readonly HashSet<string> Composition = new HashSet<string>
        {
            Prefix + "App", Prefix + "Player", Prefix + "UI"
        };

        [Test]
        public void Core_HasNoProjectDependencies()
        {
            var graph = ReadGraph();
            Assert.That(graph.ContainsKey(Core), Is.True, "Core must be present in the audited graph.");
            foreach (string reference in graph[Core])
                Assert.That(reference.StartsWith(Prefix, StringComparison.Ordinal), Is.False,
                    "Core must not depend on " + reference);
        }

        [Test]
        public void Features_UseCoreExceptTheRecordedFishingEconomyEdge()
        {
            foreach (var assembly in ReadGraph())
            {
                if (IsEditor(assembly.Key) || Composition.Contains(assembly.Key)) continue;
                foreach (string reference in assembly.Value)
                {
                    if (!reference.StartsWith(Prefix, StringComparison.Ordinal) || reference == Core) continue;
                    // Project structure section 5 records this existing exception. Removing it needs
                    // a separate licence/bait contract migration; it permits no other feature edge.
                    Assert.That(assembly.Key == Prefix + "Fishing" && reference == Prefix + "Economy",
                        Is.True, assembly.Key + " must communicate with " + reference + " through Core.");
                }
            }
        }

        [Test]
        public void RuntimeAssemblies_DoNotDependOnEditorAssemblies()
        {
            foreach (var assembly in ReadGraph())
            {
                if (IsEditor(assembly.Key)) continue;
                foreach (string reference in assembly.Value)
                    Assert.That(IsEditor(reference), Is.False, assembly.Key + " depends on " + reference);
            }
        }

        [Test]
        public void ProjectAssemblyGraph_IsAcyclic()
        {
            var graph = ReadGraph();
            var visited = new HashSet<string>();
            var active = new HashSet<string>();
            foreach (string name in graph.Keys) Visit(name, graph, visited, active);
        }

        private static void Visit(string name, Dictionary<string, string[]> graph,
                                  HashSet<string> visited, HashSet<string> active)
        {
            Assert.That(active.Contains(name), Is.False, "Assembly dependency cycle through " + name);
            if (!visited.Add(name)) return;
            active.Add(name);
            foreach (string reference in graph[name])
                if (graph.ContainsKey(reference)) Visit(reference, graph, visited, active);
            active.Remove(name);
        }

        private static bool IsEditor(string name) => name.EndsWith(".Editor", StringComparison.Ordinal);

        private static Dictionary<string, string[]> ReadGraph()
        {
            var graph = new Dictionary<string, string[]>();
            var namesByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(CodeRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                string json = File.ReadAllText(path);
                Match name = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                Assert.That(name.Success, Is.True, path + " needs an assembly name.");
                string assemblyName = name.Groups[1].Value;
                Match references = Regex.Match(json, "\"references\"\\s*:\\s*\\[([^\\]]*)\\]");
                Assert.That(references.Success, Is.True, path + " needs an explicit reference list.");
                var names = new List<string>();
                foreach (Match reference in Regex.Matches(references.Groups[1].Value, "\"([^\"]+)\""))
                    names.Add(reference.Groups[1].Value);
                graph.Add(assemblyName, names.ToArray());
                Match guid = Regex.Match(File.ReadAllText(path + ".meta"), @"(?m)^guid[:]\s*([a-fA-F0-9]+)");
                Assert.That(guid.Success, Is.True, path + " needs a stable meta GUID.");
                namesByGuid.Add(guid.Groups[1].Value, assemblyName);
            }
            Assert.That(graph.ContainsKey(Core), Is.True, "The project assembly scan must find Core.");
            foreach (string[] references in graph.Values)
                for (int i = 0; i < references.Length; i++)
                    if (references[i].StartsWith("GUID:", StringComparison.Ordinal))
                    {
                        string guid = references[i].Substring("GUID:".Length);
                        Assert.That(namesByGuid.ContainsKey(guid), Is.True,
                            "Resolve the GUID reference before extending the audited assembly graph: " + guid);
                        references[i] = namesByGuid[guid];
                    }
            return graph;
        }
    }
}
