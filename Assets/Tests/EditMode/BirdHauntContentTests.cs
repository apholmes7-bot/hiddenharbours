using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE BIRD PLACES, AS SHIPPED</b> — every <see cref="BirdHauntDef"/> under Data: a stable id
    /// in the <c>birdhaunt.</c>/<c>birdroost.</c> form, a roost in its own region, a box inside its
    /// region, numbers the schedule can use, perch hints that name real gull pieces, and a library that
    /// lists every place once. A place the flock cannot use fails here, not as an empty sky in a scene.
    /// </summary>
    public class BirdHauntContentTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string LibraryPath = "Assets/_Project/Resources/BirdHauntLibrary.asset";

        private static readonly Regex IdForm = new Regex(@"^(birdhaunt|birdroost)\.[a-z][a-z0-9]*_[a-z0-9_]+$");
        private static readonly Regex SpeciesForm = new Regex(@"^bird\.[a-z0-9_]+$");

        private static List<T> Load<T>() where T : ScriptableObject
        {
            var found = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) found.Add(asset);
            }
            return found;
        }

        private static List<BirdHauntDef> Places()
        {
            List<BirdHauntDef> places = Load<BirdHauntDef>();
            Assert.That(places.Count, Is.GreaterThan(0), "no " + nameof(BirdHauntDef) + " under " + DataRoot);
            return places;
        }

        private static Dictionary<string, RegionDef> Regions()
        {
            var regions = new Dictionary<string, RegionDef>(StringComparer.Ordinal);
            foreach (RegionDef region in Load<RegionDef>())
                if (!string.IsNullOrEmpty(region.Id)) regions[region.Id] = region;
            return regions;
        }

        private static string Where(BirdHauntDef def) => def.Id + " (" + AssetDatabase.GetAssetPath(def) + ")";

        private static bool InBox(BirdHauntDef def, Vector2 at)
            => Mathf.Abs(at.x - def.Centre.x) <= def.HalfSize.x && Mathf.Abs(at.y - def.Centre.y) <= def.HalfSize.y;

        [Test]
        public void EveryPlaceHasAStableUniqueId()
        {
            Dictionary<string, RegionDef> regions = Regions();
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var failures = new List<string>();
            foreach (BirdHauntDef def in Places())
            {
                string path = AssetDatabase.GetAssetPath(def);
                string id = def.Id ?? "";
                if (!IdForm.IsMatch(id)) { failures.Add(path + ": id '" + id + "' is not birdhaunt.<region>_<place> or birdroost.<region>_<place>"); continue; }

                string prefix = def.IsRoost ? BirdHauntDef.RoostPrefix : BirdHauntDef.HauntPrefix;
                if (!id.StartsWith(prefix, StringComparison.Ordinal)) failures.Add(Where(def) + ": a " + def.Kind + " takes the " + prefix + " prefix");

                if (seen.TryGetValue(id, out string other)) failures.Add(id + " twice: " + other + " and " + path);
                else seen[id] = path;

                string file = (def.IsRoost ? "BirdRoost_" : "BirdHaunt_") + id.Substring(id.IndexOf('.') + 1);
                if (Path.GetFileNameWithoutExtension(path) != file) failures.Add(Where(def) + ": the file is not named " + file);

                if (!regions.ContainsKey(def.RegionId ?? "")) failures.Add(Where(def) + ": no region '" + def.RegionId + "'");
            }
            GullContentAssert.None(failures, "a bird place's id");
        }

        [Test]
        public void EveryHauntsRoostIsARoostInItsRegion()
        {
            List<BirdHauntDef> places = Places();
            var byId = new Dictionary<string, BirdHauntDef>(StringComparer.Ordinal);
            foreach (BirdHauntDef def in places) byId[def.Id ?? ""] = def;

            var used = new HashSet<string>(StringComparer.Ordinal);
            var failures = new List<string>();
            foreach (BirdHauntDef def in places)
            {
                if (def.IsRoost)
                {
                    if (!string.IsNullOrEmpty(def.RoostId)) failures.Add(Where(def) + ": a roost names a roost (" + def.RoostId + ")");
                    continue;
                }
                if (string.IsNullOrEmpty(def.RoostId)) continue;   // its birds leave the map when they are not here
                if (!byId.TryGetValue(def.RoostId, out BirdHauntDef roost)) { failures.Add(Where(def) + ": no place '" + def.RoostId + "'"); continue; }
                if (!roost.IsRoost) failures.Add(Where(def) + ": " + def.RoostId + " is a " + roost.Kind + ", not a roost");
                if (roost.RegionId != def.RegionId) failures.Add(Where(def) + ": its roost is in " + roost.RegionId);
                used.Add(roost.Id);
            }
            foreach (BirdHauntDef def in places)
                if (def.IsRoost && !used.Contains(def.Id)) failures.Add(Where(def) + ": no haunt sleeps here, so it is always empty");
            GullContentAssert.None(failures, "a haunt's roost");
        }

        [Test]
        public void EveryBoxIsInsideItsRegion()
        {
            Dictionary<string, RegionDef> regions = Regions();
            var failures = new List<string>();
            foreach (BirdHauntDef def in Places())
            {
                if (!(def.HalfSize.x > 0f) || !(def.HalfSize.y > 0f)) failures.Add(Where(def) + ": an empty box " + def.HalfSize);
                if (!regions.TryGetValue(def.RegionId ?? "", out RegionDef region)) continue;   // the id test says so
                Vector2 half = region.WorldSizeMeters * 0.5f;
                Vector2 c = region.WorldCenter;
                if (def.Centre.x - def.HalfSize.x < c.x - half.x || def.Centre.x + def.HalfSize.x > c.x + half.x
                    || def.Centre.y - def.HalfSize.y < c.y - half.y || def.Centre.y + def.HalfSize.y > c.y + half.y)
                    failures.Add(Where(def) + ": the box " + def.Centre + " ± " + def.HalfSize + " leaves " + region.Id);
            }
            GullContentAssert.None(failures, "a bird place's box");
        }

        [Test]
        public void EveryNumberIsInItsRange()
        {
            var library = AssetDatabase.LoadAssetAtPath<BirdHauntLibrary>(LibraryPath);
            Assert.IsNotNull(library, LibraryPath);
            float headroom = library.Tuning.PopulationHeadroom;
            string gull = GullConfig.Default.SpeciesId;

            var failures = new List<string>();
            var perRegion = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BirdHauntDef def in Places())
            {
                string at = Where(def);
                perRegion.TryGetValue(def.RegionId ?? "", out int n);
                perRegion[def.RegionId ?? ""] = n + 1;

                // The birds: one entry per bird, by its bird. id, and the herring gull's exactly once.
                var species = new HashSet<string>(StringComparer.Ordinal);
                int gulls = 0;
                foreach (BirdHauntEntry entry in def.Birds ?? new List<BirdHauntEntry>())
                {
                    if (entry == null) { failures.Add(at + ": an empty entry"); continue; }
                    if (!SpeciesForm.IsMatch(entry.SpeciesId ?? "")) failures.Add(at + ": '" + entry.SpeciesId + "' is not a bird. id");
                    if (!species.Add(entry.SpeciesId ?? "")) failures.Add(at + ": " + entry.SpeciesId + " twice");
                    if (entry.SpeciesId == gull) gulls++;

                    if (def.IsRoost)
                    {
                        if (entry.Room <= 0 || entry.Room > GullRoster.MaxSlotsPerPlace)
                            failures.Add(at + ": room for " + entry.Room + " " + entry.SpeciesId);
                        if (entry.Residents == null || entry.Residents.Length != 4) failures.Add(at + ": not four seasons of residents");
                        else foreach (float r in entry.Residents)
                                if (r < 0f || r > 1f) failures.Add(at + ": a season's residents " + r);
                    }
                    else
                    {
                        if (!(entry.Typical > 0f) || entry.Typical > entry.Capacity || entry.Capacity > GullRoster.MaxSlotsPerPlace)
                            failures.Add(at + ": typical " + entry.Typical + ", capacity " + entry.Capacity + " " + entry.SpeciesId);
                        if (entry.SeasonWeights == null || entry.SeasonWeights.Length != 4) failures.Add(at + ": not four season weights");
                        else foreach (float w in entry.SeasonWeights)
                                if (w < 0f || w > 1.5f) failures.Add(at + ": a season weight " + w);
                    }
                }
                if (gulls != 1) failures.Add(at + ": " + gulls + " herring gull entries");

                if (def.IsRoost)
                {
                    if (def.AwayHeading == Vector2.zero) failures.Add(at + ": a roost with no way out");
                    continue;
                }

                // The place: its hours, its boosts, its tide and its day.
                if (def.BusyBoost < 1f || def.BusyBoost > headroom)
                    failures.Add(at + ": busy boost " + def.BusyBoost + " outside 1.." + headroom + " (the headroom)");
                if (def.BusyFromHour < 0f || def.BusyToHour > 24f || def.BusyFromHour > def.BusyToHour)
                    failures.Add(at + ": working hours " + def.BusyFromHour + ".." + def.BusyToHour);
                if (def.MarketDayBoost < 1f) failures.Add(at + ": market day boost " + def.MarketDayBoost);
                if (def.HasTide && !(def.TideRampMetres > 0f)) failures.Add(at + ": a tidal place with no ramp");
                if (def.HighWaterKeep < 0f || def.HighWaterKeep > 1f) failures.Add(at + ": high water keep " + def.HighWaterKeep);
                if (def.MiddayLevel < 0f || def.MiddayLevel > 1f) failures.Add(at + ": midday level " + def.MiddayLevel);
                if (def.AfternoonLevel < 0f || def.AfternoonLevel > 1f) failures.Add(at + ": afternoon level " + def.AfternoonLevel);
            }
            foreach (KeyValuePair<string, int> region in perRegion)
                if (region.Value > GullFlock.MaxAuthoredPlaces)
                    failures.Add(region.Key + ": " + region.Value + " places, past the flock's " + GullFlock.MaxAuthoredPlaces);
            GullContentAssert.None(failures, "a bird place's numbers");
        }

        [Test]
        public void EveryPerchHintExists()
        {
            var pieces = new Dictionary<string, (KeyScenePiece piece, string region)>(StringComparer.Ordinal);
            foreach (KeySceneDef scene in Load<KeySceneDef>())
                foreach (KeyScenePiece piece in scene.Pieces ?? Array.Empty<KeyScenePiece>())
                    if (piece != null && !string.IsNullOrEmpty(piece.Id)) pieces[piece.Id] = (piece, scene.RegionId);

            var failures = new List<string>();
            int hints = 0;
            foreach (BirdHauntDef def in Places())
            {
                foreach (string id in def.PerchIds ?? Array.Empty<string>())
                {
                    hints++;
                    if (!pieces.TryGetValue(id ?? "", out (KeyScenePiece piece, string region) found))
                    {
                        failures.Add(Where(def) + ": no key-scene piece '" + id + "'");
                        continue;
                    }
                    if (found.piece.Kind != KeySceneDef.KindGull) failures.Add(Where(def) + ": " + id + " is a " + found.piece.Kind + ", not a gull piece");
                    if (found.region != def.RegionId) failures.Add(Where(def) + ": " + id + " stands in " + found.region);
                    if (!InBox(def, found.piece.At)) failures.Add(Where(def) + ": " + id + " at " + found.piece.At + " is outside the box");
                }
            }
            Assert.That(hints, Is.GreaterThan(0), "no place names a perch: the test lost its cases");
            GullContentAssert.None(failures, "a perch hint");
        }

        [Test]
        public void TheLibraryListsEveryPlaceOnce()
        {
            var library = AssetDatabase.LoadAssetAtPath<BirdHauntLibrary>(LibraryPath);
            Assert.IsNotNull(library, LibraryPath);
            Assert.AreSame(library, Resources.Load<BirdHauntLibrary>(BirdHauntLibrary.ResourcesPath),
                           "the flock loads the library from Resources/" + BirdHauntLibrary.ResourcesPath);

            var listed = new HashSet<BirdHauntDef>();
            var failures = new List<string>();
            foreach (BirdHauntDef def in library.Haunts ?? Array.Empty<BirdHauntDef>())
            {
                if (def == null) { failures.Add("an empty slot in the library"); continue; }
                if (!listed.Add(def)) failures.Add(Where(def) + " is listed twice");
            }
            foreach (BirdHauntDef def in Places())
                if (!listed.Contains(def)) failures.Add(Where(def) + " is not in the library: the flock never sees it");
            GullContentAssert.None(failures, "the library");
        }
    }

    /// <summary>Collects a test's failures and fails once, listing them (Unity's NUnit has no
    /// Assert.Multiple).</summary>
    internal static class GullContentAssert
    {
        public static void None(List<string> failures, string what)
        {
            if (failures.Count == 0) return;
            int shown = Math.Min(failures.Count, 40);
            Assert.Fail(what + " (" + failures.Count + "):\n  " + string.Join("\n  ", failures.GetRange(0, shown))
                        + (failures.Count > shown ? "\n  ..." : ""));
        }
    }
}
