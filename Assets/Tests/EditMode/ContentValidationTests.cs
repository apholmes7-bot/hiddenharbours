using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-30 — content validation. The single source of truth for "is the Data/ content well-formed":
    /// every <see cref="FishSpeciesDef"/>, <see cref="BoatHullDef"/>, <see cref="RegionDef"/>,
    /// <see cref="TrapDef"/>, and <see cref="BaitDef"/> must have a non-empty, unique id and references
    /// that actually resolve (a fish must be reachable by some region/gear/season AND its region ids must
    /// name a real region; a boat must have a name and a hold; a region must have a name + a scene to load;
    /// a trap's required bait must name a real BaitDef and its allowed catches must name real fish). It
    /// runs over the ACTUAL assets in Data/, so it catches data errors as content grows — a new Punt
    /// BoatHullDef, a copy-pasted id, a fish gated to a region that doesn't exist, an inverted weight range,
    /// a trap baited with a bait that doesn't exist. If tools-editor later adds an in-editor content
    /// validator, it should call THESE rules rather than re-deriving its own.
    /// </summary>
    public class ContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        // ---- reusable rules (the single source of truth) ------------------------------------

        /// <summary>Load every asset of type T under Data/.</summary>
        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        /// <summary>Add an id to the seen-set, failing if it is empty/blank or a duplicate.</summary>
        private static void RegisterUniqueId(Dictionary<string, string> seen, string id, string path, string kind)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{kind} '{path}' has an empty id");
            Assert.IsFalse(seen.ContainsKey(id),
                $"duplicate {kind} id '{id}' in '{path}' and '{(seen.TryGetValue(id, out var other) ? other : "?")}'");
            seen[id] = path;
        }

        // ---- fish ---------------------------------------------------------------------------

        [Test]
        public void FishSpecies_Exist_AndHaveNonEmptyUniqueIds()
        {
            var fish = LoadAll<FishSpeciesDef>();
            Assert.IsNotEmpty(fish, "the slice must ship at least one FishSpeciesDef in Data/");

            var seen = new Dictionary<string, string>();
            foreach (var f in fish)
                RegisterUniqueId(seen, f.Id, AssetDatabase.GetAssetPath(f), nameof(FishSpeciesDef));
        }

        [Test]
        public void FishSpecies_AreReachable_AndPricedSanely()
        {
            foreach (var f in LoadAll<FishSpeciesDef>())
            {
                string path = AssetDatabase.GetAssetPath(f);

                // Region references must resolve to at least one non-blank region — a fish gated to no
                // region (or a blank one) can never be caught.
                Assert.IsNotNull(f.RegionIds, $"{path}: RegionIds is null");
                Assert.IsNotEmpty(f.RegionIds, $"{path}: a fish with no region can never be caught");
                foreach (var r in f.RegionIds)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(r), $"{path}: has a blank region id");

                // Reachable by some gear and some season (an empty mask = catchable by nothing).
                Assert.AreNotEqual(0, (int)f.AllowedGear, $"{path}: AllowedGear is empty — no gear can land it");
                Assert.AreNotEqual(0, (int)f.Seasons, $"{path}: Seasons is empty — it bites in no season");

                // Sane catch + price.
                Assert.LessOrEqual(f.MinWeightKg, f.MaxWeightKg, $"{path}: MinWeightKg exceeds MaxWeightKg");
                Assert.Greater(f.MaxWeightKg, 0f, $"{path}: MaxWeightKg must be positive");
                Assert.GreaterOrEqual(f.BaseValue, 0, $"{path}: BaseValue must be non-negative");
            }
        }

        // ---- bait (trap arc Build 2) --------------------------------------------------------

        [Test]
        public void Bait_HaveNonEmptyUniqueIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (var b in LoadAll<BaitDef>())
                RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), nameof(BaitDef));
        }

        [Test]
        public void BaitFavorsSpeciesIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var b in LoadAll<BaitDef>())
            {
                string path = AssetDatabase.GetAssetPath(b);
                if (b.FavorsSpeciesIds == null) continue;
                foreach (var id in b.FavorsSpeciesIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank favored-species id");
                    Assert.IsTrue(fishIds.Contains(id),
                        $"{path}: favored-species id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- traps (trap arc Build 2) -------------------------------------------------------

        [Test]
        public void Traps_HaveNonEmptyUniqueIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (var t in LoadAll<TrapDef>())
                RegisterUniqueId(seen, t.Id, AssetDatabase.GetAssetPath(t), nameof(TrapDef));
        }

        [Test]
        public void TrapRequiredBaitIds_ResolveToRealBait()
        {
            var baitIds = new HashSet<string>();
            foreach (var b in LoadAll<BaitDef>())
                if (!string.IsNullOrWhiteSpace(b.Id)) baitIds.Add(b.Id);

            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                // A trap must be baited with a real bait — an unresolvable RequiredBaitId means the trap
                // can never be set (Build 3's resolver keys the catch weighting off the loaded bait).
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.RequiredBaitId),
                    $"{path}: RequiredBaitId is empty — the trap names no bait");
                Assert.IsTrue(baitIds.Contains(t.RequiredBaitId),
                    $"{path}: RequiredBaitId '{t.RequiredBaitId}' resolves to no BaitDef");
            }
        }

        [Test]
        public void TrapAllowedCatchFishIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                // A trap with no catchable species can never yield anything.
                Assert.IsNotNull(t.AllowedCatchFishIds, $"{path}: AllowedCatchFishIds is null");
                Assert.IsNotEmpty(t.AllowedCatchFishIds, $"{path}: a trap that can catch nothing is invalid");
                foreach (var id in t.AllowedCatchFishIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank allowed-catch fish id");
                    Assert.IsTrue(fishIds.Contains(id),
                        $"{path}: allowed-catch fish id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        [Test]
        public void TrapFillWindows_AreAuthored_NotStale()
        {
            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                // The soak-to-fill window (multi-catch): a pot is ready with 1 animal at SoakHours and
                // full at HoursToFullPot. An asset serialized BEFORE the field existed deserializes it
                // as 0 — which silently means "full the moment she's ready" (max yield). Require every
                // shipped trap to author the window explicitly: equal-to-SoakHours is the legitimate
                // instant-full choice; below it is a stale asset. (Re-save the asset to fix.)
                Assert.GreaterOrEqual(t.HoursToFullPot, t.SoakHours,
                    $"{path}: HoursToFullPot ({t.HoursToFullPot}) < SoakHours ({t.SoakHours}) — stale asset? " +
                    "Author the fill window (set it ≥ SoakHours; equal = full at ready, deliberate).");
            }
        }

        // ---- boats --------------------------------------------------------------------------

        [Test]
        public void BoatHulls_Exist_AndHaveNonEmptyUniqueIds()
        {
            var boats = LoadAll<BoatHullDef>();
            Assert.IsNotEmpty(boats, "the slice must ship at least one BoatHullDef in Data/ (the Dory)");

            var seen = new Dictionary<string, string>();
            foreach (var b in boats)
                RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), nameof(BoatHullDef));
        }

        [Test]
        public void BoatHulls_HaveNameAndHold()
        {
            foreach (var b in LoadAll<BoatHullDef>())
            {
                string path = AssetDatabase.GetAssetPath(b);
                Assert.IsFalse(string.IsNullOrWhiteSpace(b.DisplayName), $"{path}: empty DisplayName");
                // Every hull on the Dory→Dynasty ladder hauls catch; a hold of zero breaks the loop.
                Assert.GreaterOrEqual(b.HoldUnits, 1, $"{path}: HoldUnits must be at least 1");
            }
        }

        /// <summary>
        /// THE TANK IS COHERENT OR IT IS ABSENT — fuel-and-refuelling.md §9.2/§9.12.
        ///
        /// <para>A hull's fuel is three fields that only mean anything together, and every incoherent
        /// combination is silently harmless at load and wrong in play: a capacity with no grade can never
        /// be filled by any pump (the grade check refuses first, in words, forever); a capacity with no
        /// burn rate is a boat with INFINITE RANGE; a grade outside the fixed art contract matches no can
        /// ever baked. None of those throw. This is what notices.</para>
        ///
        /// <para><b>An untouched hull is not an error.</b> All three at their defaults means "no tank",
        /// which is the correct reading for the rowed dory and the state every asset written before the
        /// fields existed deserializes to. The rule only bites once a hull has been enrolled.</para>
        /// </summary>
        [Test]
        public void BoatHulls_FuelIsAuthoredCoherently_OrNotAtAll()
        {
            foreach (var b in LoadAll<BoatHullDef>())
            {
                string path = AssetDatabase.GetAssetPath(b);
                bool hasTank = b.FuelCapacityLitres > 0f;

                if (!string.IsNullOrEmpty(b.FuelGrade))
                    Assert.IsTrue(FuelGrades.IsKnown(b.FuelGrade),
                        $"{path}: FuelGrade '{b.FuelGrade}' is not one of FuelGrades.All " +
                        "(gas · diesel · mixed · oil · stove_oil). The grade set is a fixed contract " +
                        "shared with the art lane — an unknown grade is a typo, not a new fuel, and it " +
                        "would match no can and no pump row in the game.");

                if (hasTank)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(b.FuelGrade),
                        $"{path}: FuelCapacityLitres is {b.FuelCapacityLitres} but FuelGrade is empty. " +
                        "A tank with no grade can never be filled — every pump refuses on the grade " +
                        "check before it ever looks at the level. Author the grade, or zero the tank.");

                    Assert.Greater(b.FullThrottleLitresPerHour, 0f,
                        $"{path}: FuelCapacityLitres is {b.FuelCapacityLitres} but " +
                        "FullThrottleLitresPerHour is 0 — she carries fuel and burns none, which is " +
                        "infinite range. Author the reference burn (§9.6.2), or zero the tank.");
                }
                else
                {
                    // The mirror: a burn rate or a grade with no tank is half-authored, and the half that
                    // is missing is the one that decides whether the feature exists for this hull at all.
                    Assert.AreEqual(0f, b.FullThrottleLitresPerHour,
                        $"{path}: FullThrottleLitresPerHour is authored but FuelCapacityLitres is 0, so " +
                        "she has no tank and nothing burns. Author the capacity, or zero the rate.");
                }
            }
        }

        /// <summary>
        /// ⭐ THE ROWED DORY CARRIES NO TANK — named, because she is the one hull whose zero is a
        /// DESIGN FACT rather than an un-authored default. fuel-and-refuelling.md §9.6.2 lists her as
        /// "(none — she rows)": the opening boat has no engine, no fuel errand and no way to run dry,
        /// and canon's first beat is Ginny handing over a motor that turns her into boat.dory_outboard.
        /// Giving her a tank would quietly delete that beat, so the rule is spelled out rather than left
        /// to the general coherence sweep above (which she passes either way).
        /// </summary>
        [Test]
        public void TheRowedDory_HasNoTank()
        {
            foreach (var b in LoadAll<BoatHullDef>())
            {
                if (b.Id != "boat.dory") continue;
                string path = AssetDatabase.GetAssetPath(b);
                Assert.AreEqual(0f, b.FuelCapacityLitres,
                    $"{path}: boat.dory ROWS. She is the pre-motor boat the opening hands you, and a tank " +
                    "on her erases the beat where Ginny gives you the outboard. The motorised version is " +
                    "a separate hull, boat.dory_outboard.");
                Assert.IsEmpty(b.FuelGrade ?? "",
                    $"{path}: boat.dory has no engine, so she drinks nothing.");
            }
        }

        // ---- regions (VS-22+) ---------------------------------------------------------------

        [Test]
        public void Regions_Exist_AndHaveNonEmptyUniqueIds()
        {
            var regions = LoadAll<RegionDef>();
            Assert.IsNotEmpty(regions, "the slice must ship at least one RegionDef in Data/Regions (the cove + Nine Mile Creek)");

            var seen = new Dictionary<string, string>();
            foreach (var r in regions)
                RegisterUniqueId(seen, r.Id, AssetDatabase.GetAssetPath(r), nameof(RegionDef));
        }

        [Test]
        public void Regions_HaveNameAndAScene()
        {
            foreach (var r in LoadAll<RegionDef>())
            {
                string path = AssetDatabase.GetAssetPath(r);
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.DisplayName), $"{path}: empty DisplayName");
                // "Scene per region, loaded additively" (CLAUDE.md §3) — a region with no scene can't load.
                Assert.IsTrue(r.HasScene, $"{path}: no SceneName — the region can never be loaded");
                // Tide envelope is physical: amplitude can't be negative.
                Assert.GreaterOrEqual(r.TideAmplitude, 0f, $"{path}: negative TideAmplitude");
            }
        }

        [Test]
        public void RegionSpawnFishIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var r in LoadAll<RegionDef>())
            {
                string path = AssetDatabase.GetAssetPath(r);
                if (r.SpawnFishIds == null) continue;
                foreach (var id in r.SpawnFishIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank spawn-fish id");
                    Assert.IsTrue(fishIds.Contains(id), $"{path}: spawn-fish id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- cross-type ---------------------------------------------------------------------

        [Test]
        public void FishRegionIds_ResolveToRealRegions()
        {
            // Now that regions are authored as data, a fish's region ids must name an ACTUAL RegionDef —
            // not just be non-blank strings (which FishSpecies_AreReachable already checks). A fish gated
            // to a region that doesn't exist can never be caught.
            var regionIds = new HashSet<string>();
            foreach (var r in LoadAll<RegionDef>())
                if (!string.IsNullOrWhiteSpace(r.Id)) regionIds.Add(r.Id);
            Assert.IsNotEmpty(regionIds, "there must be at least one RegionDef for fish to reference");

            foreach (var f in LoadAll<FishSpeciesDef>())
            {
                string path = AssetDatabase.GetAssetPath(f);
                if (f.RegionIds == null) continue;
                foreach (var rid in f.RegionIds)
                    if (!string.IsNullOrWhiteSpace(rid))
                        Assert.IsTrue(regionIds.Contains(rid),
                            $"{path}: region id '{rid}' resolves to no RegionDef — the fish is gated to a region that doesn't exist");
            }
        }

        [Test]
        public void DefIds_AreGloballyUnique_AcrossAllDefTypes()
        {
            // Ids are append-only & stable and namespaced by type (fish.* / boat.* / region.* / trap.* /
            // bait.*), so they must not collide across the whole content set.
            var seen = new Dictionary<string, string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id))
                    RegisterUniqueId(seen, f.Id, AssetDatabase.GetAssetPath(f), "Def");
            foreach (var b in LoadAll<BoatHullDef>())
                if (!string.IsNullOrWhiteSpace(b.Id))
                    RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), "Def");
            foreach (var r in LoadAll<RegionDef>())
                if (!string.IsNullOrWhiteSpace(r.Id))
                    RegisterUniqueId(seen, r.Id, AssetDatabase.GetAssetPath(r), "Def");
            foreach (var t in LoadAll<TrapDef>())
                if (!string.IsNullOrWhiteSpace(t.Id))
                    RegisterUniqueId(seen, t.Id, AssetDatabase.GetAssetPath(t), "Def");
            foreach (var bait in LoadAll<BaitDef>())
                if (!string.IsNullOrWhiteSpace(bait.Id))
                    RegisterUniqueId(seen, bait.Id, AssetDatabase.GetAssetPath(bait), "Def");
        }

        // ---- the ASSET FILES themselves, not the objects they load into ----------------------

        /// <summary>
        /// ⭐ THE APOSTROPHE GUARD. Every rule above asks the loaded object a question, which means none of
        /// them can see a defect in the TEXT the object was loaded from. <c>GinnyOpening.asset</c> shipped
        /// with <c>- 'He's gone to the deep now…'</c>: inside a single-quoted YAML scalar an apostrophe has
        /// to be DOUBLED (<c>He''s</c>), so that scalar ends after "He" and the rest of Ginny's line is no
        /// longer part of the string. A strict parser rejects the whole document; a lenient one keeps some
        /// fraction of the line. Either way an authored line is silently not what was written, and every
        /// content rule in this file passed while it was true, because a truncated line is still a
        /// non-blank, uniquely-identified line.
        ///
        /// <para>So this one reads the raw <c>.asset</c> text. It walks each single-quoted scalar the way
        /// YAML does — <c>''</c> is an escaped apostrophe, a lone <c>'</c> closes — and fails when a scalar
        /// closes with content still left on its line, which is exactly what an unescaped apostrophe looks
        /// like and is not something well-formed Unity YAML ever produces. It covers ALL of <c>Data/</c>
        /// rather than only dialogue: any authored prose field (a licence's Flavor, a supply's
        /// DisplayName) can carry the same defect, and the whole of Data/ is what this file guards.</para>
        /// </summary>
        [Test]
        public void AuthoredAssetText_EscapesEveryApostrophe_SoNoLineIsSilentlyTruncated()
        {
            var offenders = new List<string>();
            string[] files = Directory.GetFiles(DataRoot, "*.asset", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "there must be authored .asset content under Data/ to validate");

            foreach (string file in files)
                foreach (string bad in UnescapedQuoteLines(File.ReadAllLines(file)))
                    offenders.Add($"{file.Replace('\\', '/')}: {bad}");

            Assert.IsEmpty(offenders,
                "a single-quoted YAML scalar ended early, which means an apostrophe inside it was not " +
                "doubled — the rest of that line is not in the string the game loads. Double it ('' not ') " +
                "in:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The scan behind <see cref="AuthoredAssetText_EscapesEveryApostrophe_SoNoLineIsSilentlyTruncated"/>:
        /// returns a description of every line where a single-quoted scalar closes with content still to
        /// come on that line. A scalar STARTS only at a value position (just after <c>: </c> or <c>- </c>),
        /// so an apostrophe inside an unquoted scalar (<c>DisplayName: Ginny's Freezer</c> — legal YAML)
        /// is not mistaken for one; a scalar may run over several lines, which is how Unity wraps long
        /// prose, so the walk carries across lines until it closes.
        /// </summary>
        private static IEnumerable<string> UnescapedQuoteLines(string[] lines)
        {
            bool inScalar = false;
            int cursor = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int at = inScalar ? 0 : -1;

                while (true)
                {
                    if (!inScalar)
                    {
                        at = ScalarStart(line, cursor);
                        if (at < 0) break;
                        inScalar = true;
                    }

                    int close = CloseQuote(line, at);
                    if (close < 0) break;                      // runs on to the next line — legal, keep going

                    inScalar = false;
                    string rest = line.Substring(close + 1).Trim();
                    if (rest.Length > 0)
                    {
                        yield return $"line {i + 1}: {line.Trim()}";
                        break;                                 // one report per line is enough to fix it
                    }
                    cursor = close + 1;
                }

                cursor = 0;   // a new line always starts scanning from its beginning
            }
        }

        /// <summary>Index just inside the next single-quoted scalar at or after <paramref name="from"/>,
        /// or -1. A scalar opens only at a value position: right after ": " or "- ".</summary>
        private static int ScalarStart(string line, int from)
        {
            for (int i = Mathf.Max(from, 2); i < line.Length; i++)
                if (line[i] == '\'' && line[i - 1] == ' ' && (line[i - 2] == ':' || line[i - 2] == '-'))
                    return i + 1;
            return -1;
        }

        /// <summary>Index of the quote that CLOSES a scalar whose content starts at
        /// <paramref name="from"/>, or -1 if it runs past the end of the line. "''" is an escaped
        /// apostrophe and never closes.</summary>
        private static int CloseQuote(string line, int from)
        {
            for (int i = from; i < line.Length; i++)
            {
                if (line[i] != '\'') continue;
                if (i + 1 < line.Length && line[i + 1] == '\'') { i++; continue; }
                return i;
            }
            return -1;
        }
    }
}
