using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The intake guard for the multi-unit building rig kit, <c>docs/art/rigs/multiunit-kit/</c>: the terrace, the
    /// walk-up and the three-decker stack, with their 16 gameplay sidecars. Nothing is baked from the kit yet, so
    /// these hold the committed files to what the sidecars state: each sidecar's five stamps against the rigs it was
    /// generated from, each <c>SHA256SUMS.json</c> against the bytes, and the <c>CONTRACT.md</c> clauses the game
    /// will rely on (#5, #12, #16, #18, #22). They read with <c>System.IO</c> and <see cref="MiniJson"/>, run no rig
    /// and re-derive no geometry. Hashes fold CRLF to LF, as <c>.gitattributes</c> says. With the kit absent every
    /// test is ignored, never passed. What they cannot see is in the kit's <c>IMPORT.md</c> §10.
    /// </summary>
    public sealed class MultiunitKitIntakeTests
    {
        const string Rigs = "docs/art/rigs";
        const string Kit = Rigs + "/multiunit-kit";
        const string PropRig = "interiorPropRig.js";
        const string Schema = "hidden-harbours/building-gameplay@1";
        const double PxPerMetre = 32;
        const int SidecarCount = 16;
        const int MillRow4Slots = 12;
        // in doubles, steps x rise_m misses floor_rise_m by at most 4.4e-16 across the committed sidecars; the rounded
        // rise #12 is written against misses by far more
        const double FlightTolerance = 1e-9;
        const int Shown = 40;

        static readonly string[] Shared =
            { "_multiunitKit.js", "_buildingGameplay.js", "_interiorPlacer.js", "_sidecarExport.js" };

        sealed class Phase
        {
            public readonly string Folder, Shell, Unit;
            public readonly string[] Presets;

            public Phase(string folder, string shell, string unit, params string[] presets)
            {
                Folder = folder;
                Shell = shell;
                Unit = unit;
                Presets = presets;
            }

            public string Dir => Kit + "/" + Folder;

            public string SidecarName(string preset) =>
                $"{Shell.Substring(0, Shell.Length - 3)}.{preset}.gameplay.json";
        }

        // each phase as the art director exported it: its shell rig, its unit rig, the presets it wrote a sidecar for
        static readonly Phase[] Phases =
        {
            new Phase("rowhouse", "rowhouseIsoRig.js", "rowhouseUnitIsoRig.js",
                "millRow4", "duplexPair", "coastalRow4"),
            new Phase("walkup", "walkupIsoRig.js", "walkupUnitIsoRig.js",
                "walkup8", "walkup12", "walkup12Fam", "walkup4", "cannery12", "cannery8Loft"),
            new Phase("stack", "stackFlatsIsoRig.js", "stackUnitIsoRig.js",
                "decker6", "decker3", "decker4", "deckerMixed", "captains6", "captains2", "captains4"),
        };

        sealed class Sidecar
        {
            public Phase Phase;
            public string Preset, Name;
            public Dictionary<string, object> Json;
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        static string Abs(string rel) => Path.Combine(RepoRoot, rel);

        [Test]
        public void SidecarsParseWithTheirSchemaAndFrame()
        {
            RequireKit();
            var failures = new List<string>();
            foreach (Phase p in Phases)
            {
                string dir = Abs(p.Dir + "/gameplay");
                if (!Directory.Exists(dir))
                    continue; // Load names every sidecar that is missing
                IEnumerable<string> extra = Directory.GetFiles(dir).Select(f => Path.GetFileName(f))
                    .Except(p.Presets.Select(k => p.SidecarName(k)));
                failures.AddRange(extra.Select(f => $"{p.Dir}/gameplay/{f}: not the sidecar of any {p.Folder} preset"));
            }
            List<Sidecar> sidecars = Load(failures);
            if (sidecars.Count != SidecarCount)
                failures.Add($"{sidecars.Count} sidecars parse, not {SidecarCount}");
            foreach (Sidecar s in sidecars)
            {
                string schema = MiniJson.String(s.Json, "schema");
                if (schema != Schema)
                    failures.Add($"{s.Name}: schema is {schema ?? "absent"}, not {Schema}");
                Dictionary<string, object> frame = MiniJson.Dict(s.Json, "frame");
                string units = MiniJson.String(frame, "units");
                double? scale = Number(frame, "scale_px_per_m");
                if (units != "metres" || scale != PxPerMetre)
                    failures.Add($"{s.Name}: frame is in {units ?? "absent"} at {scale?.ToString() ?? "absent"} " +
                                 $"px per metre, not metres at {PxPerMetre}");
            }
            AssertNone(failures, "the sidecars");
        }

        [Test]
        public void StampsEqualTheCommittedRigs()
        {
            RequireKit();
            var failures = new List<string>();
            foreach (Sidecar s in Load(failures))
            {
                Phase p = s.Phase;
                foreach ((string field, string expected) in new[]
                         { ("rig", "Art/" + p.Shell), ("interiorRig", "Art/" + p.Unit), ("propRig", "Art/" + PropRig) })
                {
                    string named = MiniJson.String(s.Json, field);
                    if (named != expected)
                        failures.Add($"{s.Name}: {field} is {named ?? "absent"}, not {expected}");
                }
                foreach ((string stamp, string file) in new[]
                         {
                             ("derivedFromRigSha256", $"{p.Dir}/{p.Shell}"),
                             ("interiorDerivedFromRigSha256", $"{p.Dir}/{p.Unit}"),
                             ("propsDerivedFromRigSha256", $"{Rigs}/{PropRig}"),
                             ("placerDerivedFromRigSha256", $"{Kit}/shared/_interiorPlacer.js"),
                             ("writerDerivedFromRigSha256", $"{Kit}/shared/_buildingGameplay.js"),
                         })
                {
                    string got = MiniJson.String(s.Json, stamp), want = Sha256Lf(file);
                    if (want == null)
                        failures.Add($"{s.Name}: {stamp} is for {file}, which is missing");
                    else if (got != want)
                        failures.Add($"{s.Name}: {stamp} is {got ?? "absent"}, but {file} hashes to {want}");
                }
            }
            AssertNone(failures, "the stamps (a rig edited without regenerating its sidecars, or the reverse)");
        }

        [Test]
        public void Sha256SumsMatchTheCommittedBytes()
        {
            RequireKit();
            var failures = new List<string>();
            foreach (Phase p in Phases)
            {
                string sums = p.Dir + "/SHA256SUMS.json";
                Dictionary<string, object> json;
                try
                {
                    json = File.Exists(Abs(sums))
                        ? MiniJson.Parse(File.ReadAllText(Abs(sums))) as Dictionary<string, object>
                        : null;
                }
                catch (FormatException ex)
                {
                    failures.Add($"{sums}: does not parse: {ex.Message}");
                    continue;
                }
                Dictionary<string, object> files = MiniJson.Dict(json, "files");
                if (files == null)
                {
                    failures.Add($"{sums}: missing, or it lists no files");
                    continue;
                }
                if (MiniJson.String(json, "algorithm") != "sha256")
                    failures.Add($"{sums}: algorithm is {MiniJson.String(json, "algorithm") ?? "absent"}, not sha256");
                Dictionary<string, string> landed = WhereEachEntryLanded(p);
                failures.AddRange(landed.Keys.Except(files.Keys).Select(k => $"{sums}: no entry for {k}"));
                failures.AddRange(files.Keys.Except(landed.Keys).Select(k => $"{sums}: {k} is not a file of this kit"));
                foreach (KeyValuePair<string, string> entry in landed.Where(x => files.ContainsKey(x.Key)))
                {
                    string got = files[entry.Key] as string, want = Sha256Lf(entry.Value);
                    if (want == null)
                        failures.Add($"{sums}: {entry.Key} landed at {entry.Value}, which is missing");
                    else if (got != want)
                        failures.Add($"{sums}: {entry.Key} is {got ?? "not a string"}, " +
                                     $"but {entry.Value} hashes to {want}");
                }
            }
            AssertNone(failures, "the kits' SHA256SUMS.json");
        }

        [Test]
        public void EveryDwellingHasItsResidentSlots()
        {
            RequireKit();
            var failures = new List<string>();
            double? millRow4 = null;
            foreach (Sidecar s in Load(failures))
            {
                List<object> units = MiniJson.List(s.Json, "UNITS");
                double? declared = Number(MiniJson.Dict(s.Json, "build"), "units");
                if (units == null || units.Count == 0)
                {
                    failures.Add($"{s.Name}: UNITS is missing or empty");
                    continue;
                }
                if (declared != units.Count)
                    failures.Add($"{s.Name}: build.units is {declared?.ToString() ?? "absent"}, " +
                                 $"but UNITS has {units.Count}");
                double slots = 0;
                foreach (object unit in units)
                {
                    string id = $"{s.Name} {MiniJson.String(unit, "id") ?? "<no id>"}";
                    double? count = Number(unit, "resident_slots");
                    List<object> anchors = MiniJson.List(unit, "sleep_anchors");
                    if (count == null || anchors == null || count != anchors.Count)
                        failures.Add($"{id}: resident_slots {count?.ToString() ?? "absent"}, " +
                                     $"sleep_anchors {anchors?.Count.ToString() ?? "absent"} (#5)");
                    if (count < 1)
                        failures.Add($"{id}: no resident slot");
                    slots += count ?? 0;
                }
                if (s.Preset == "millRow4")
                    millRow4 = slots;
            }
            if (millRow4 != MillRow4Slots)
                failures.Add($"millRow4: resident slots add up to {millRow4?.ToString() ?? "nothing"}, " +
                             $"not {MillRow4Slots}");
            AssertNone(failures, "the resident slots (CONTRACT.md #5)");
        }

        [Test]
        public void EveryFlightRisesExactlyAndLandsBothEnds()
        {
            RequireKit();
            var failures = new List<string>();
            int flights = 0;
            foreach (Sidecar s in Load(failures))
            {
                List<object> stairs = MiniJson.List(s.Json, "STAIRS");
                if (stairs == null || stairs.Count == 0)
                {
                    failures.Add($"{s.Name}: STAIRS is missing or empty");
                    continue;
                }
                foreach (object stair in stairs)
                {
                    string id = $"{s.Name} {MiniJson.String(stair, "id") ?? "<no id>"}";
                    string kind = MiniJson.String(stair, "kind") ?? "interior";
                    if (kind != "interior" && kind != "entry_steps" && kind != "service_stair")
                    {
                        failures.Add($"{id}: kind {kind} is not interior, entry_steps or service_stair");
                        continue;
                    }
                    var own = new List<(string name, object flight)>();
                    if (MiniJson.Has(stair, "steps"))
                        own.Add((id, stair));
                    else if (kind != "service_stair")
                        failures.Add($"{id}: an {kind} flight with no steps");
                    List<object> nested = kind == "service_stair" ? MiniJson.List(stair, "flights") : null;
                    if (kind == "service_stair" && (nested == null || nested.Count == 0))
                        failures.Add($"{id}: a service stair with no flights");
                    for (int i = 0; i < (nested?.Count ?? 0); i++)
                        own.Add(($"{id} flights[{i}]", nested[i]));
                    foreach ((string name, object flight) in own)
                    {
                        flights++;
                        double? steps = Number(flight, "steps"), rise = Number(flight, "rise_m");
                        double? floor = Number(flight, "floor_rise_m");
                        if (steps == null || rise == null || floor == null)
                            failures.Add($"{name}: steps, rise_m and floor_rise_m are not all numbers");
                        else if (!(Math.Abs(steps.Value * rise.Value - floor.Value) <= FlightTolerance))
                            failures.Add($"{name}: steps x rise_m = {steps} x {rise} = {steps.Value * rise.Value:R}, " +
                                         $"not floor_rise_m {floor.Value:R} (#12)");
                    }
                    if (kind != "interior")
                        continue;
                    Dictionary<string, object> landings = MiniJson.Dict(stair, "landings");
                    foreach (string end in new[] { "departure", "arrival" })
                    {
                        Dictionary<string, object> landing = MiniJson.Dict(landings, end);
                        if (landing == null || landing.Count == 0)
                            failures.Add($"{id}: no {end} landing (#16)");
                    }
                }
            }
            if (flights == 0)
                failures.Add("no flight to check");
            AssertNone(failures, "the flights (CONTRACT.md #12, #16)");
        }

        [Test]
        public void EveryAnchorIsReachableAndEveryPiecePlaced()
        {
            RequireKit();
            var failures = new List<string>();
            foreach (Sidecar s in Load(failures))
            {
                List<object> anchors = MiniJson.List(s.Json, "INTERACT");
                if (anchors == null || anchors.Count == 0)
                    failures.Add($"{s.Name}: INTERACT is missing or empty");
                foreach (object anchor in anchors ?? new List<object>())
                {
                    Dictionary<string, object> reach = MiniJson.Dict(anchor, "reach");
                    if (reach == null || !reach.TryGetValue("point", out object point) || point == null)
                        failures.Add($"{s.Name} {MiniJson.String(anchor, "id") ?? "<no id>"}: " +
                                     "reach.point is null (#18)");
                }
                s.Json.TryGetValue("_unplaced", out object unplaced);
                int left = unplaced is List<object> list ? list.Count
                    : unplaced is Dictionary<string, object> dict ? dict.Count
                    : unplaced == null ? 0 : 1;
                if (left > 0)
                    failures.Add($"{s.Name}: _unplaced holds {left} (#22)");
            }
            AssertNone(failures, "the reach and placement audits (CONTRACT.md #18, #22)");
        }

        [Test]
        public void OneInteriorPropRig()
        {
            RequireKit();
            string root = Path.GetFullPath(Abs(Rigs));
            string[] copies = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => string.Equals(Path.GetFileName(f), PropRig, StringComparison.OrdinalIgnoreCase))
                .Select(f => Rigs + "/" + f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
            Assert.That(copies, Is.EqualTo(new[] { $"{Rigs}/{PropRig}" }),
                $"{Rigs}/ must hold one {PropRig}, at its top level: the one every sidecar's props stamp is for. " +
                $"It holds {copies.Length}: {string.Join(", ", copies)}");
        }

        // every sidecar the phases name, parsed; one that is missing or does not parse is a failure, named by its path
        static List<Sidecar> Load(List<string> failures)
        {
            var sidecars = new List<Sidecar>();
            foreach (Phase p in Phases)
            {
                foreach (string preset in p.Presets)
                {
                    string name = p.SidecarName(preset), rel = $"{p.Dir}/gameplay/{name}";
                    if (!File.Exists(Abs(rel)))
                    {
                        failures.Add(rel + ": missing");
                        continue;
                    }
                    try
                    {
                        if (MiniJson.Parse(File.ReadAllText(Abs(rel))) is Dictionary<string, object> json)
                            sidecars.Add(new Sidecar { Phase = p, Preset = preset, Name = name, Json = json });
                        else
                            failures.Add(rel + ": not a JSON object");
                    }
                    catch (FormatException ex)
                    {
                        failures.Add($"{rel}: does not parse: {ex.Message}");
                    }
                }
            }
            return sidecars;
        }

        // where each SHA256SUMS.json entry landed: the phase's own files in its folder, one CONTRACT.md and one set of
        // shared modules for all three phases, and the prop rig at the top of docs/art/rigs (IMPORT.md §2)
        static Dictionary<string, string> WhereEachEntryLanded(Phase p)
        {
            var landed = new Dictionary<string, string>
            {
                ["README.md"] = p.Dir + "/README.md",
                ["CONTRACT.md"] = Kit + "/CONTRACT.md",
                ["rigs/" + p.Shell] = p.Dir + "/" + p.Shell,
                ["rigs/" + p.Unit] = p.Dir + "/" + p.Unit,
                ["rigs/" + PropRig] = Rigs + "/" + PropRig,
            };
            foreach (string module in Shared)
                landed["rigs/" + module] = Kit + "/shared/" + module;
            foreach (string preset in p.Presets)
                landed["gameplay/" + p.SidecarName(preset)] = $"{p.Dir}/gameplay/{p.SidecarName(preset)}";
            return landed;
        }

        // sha256 of a committed file with CRLF folded to LF, as .gitattributes commits it; null if it is missing
        static string Sha256Lf(string rel)
        {
            string path = Abs(rel);
            if (!File.Exists(path))
                return null;
            byte[] bytes = File.ReadAllBytes(path);
            int n = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != '\r' || i + 1 == bytes.Length || bytes[i + 1] != '\n')
                    bytes[n++] = bytes[i];
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes, 0, n)).Replace("-", "").ToLowerInvariant();
        }

        static double? Number(object node, string key) =>
            node is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is double n
                ? n
                : (double?)null;

        // a missing kit is ignored with its reason, never passed
        static void RequireKit()
        {
            if (!Directory.Exists(Abs(Kit)))
                Assert.Ignore($"{Kit}/ is not in this checkout, so there is no multi-unit kit to check: " +
                              "nothing here passed.");
        }

        static void AssertNone(List<string> failures, string what)
        {
            if (failures.Count == 0)
                return;
            string more = failures.Count > Shown ? $"\n... and {failures.Count - Shown} more" : "";
            Assert.Fail($"{what}: {failures.Count} failure(s)\n{string.Join("\n", failures.Take(Shown))}{more}");
        }
    }
}
