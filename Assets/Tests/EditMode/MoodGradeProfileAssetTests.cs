using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The owner's grade levers ship, load by the name the director uses, and say what the art bible
    /// says</b> (juice PR 1, rule 6). Sister to <c>DayNightProfileAssetTests</c>.
    ///
    /// <para>Three things, in falling order of rot: the profile asset EXISTS at the path the builder
    /// names and <c>Resources.Load</c> returns that same object (the director loads by name, so a moved
    /// or renamed asset silently falls back to the code default and nothing tells you); the shipped keys
    /// AND the code fallback both carry the bible §4.2 FEATURES — cold night shadows, warm golden gain,
    /// fog that crushes saturation and closes the vignette, grain and chromatic aberration OFF — without
    /// pinning the owner's numbers, which are his to move; and the per-region overrides name only
    /// regions that exist, once each, with Nine Mile Creek the most colourful place on the coast
    /// (§4.3).</para>
    /// </summary>
    public class MoodGradeProfileAssetTests
    {
        static readonly string ProfilePath = MoodGradeProfileBuilder.ProfilePath;
        static readonly string RegionFolder = MoodGradeProfileBuilder.RegionFolder;

        [Test]
        public void The_profile_asset_ships_and_Resources_returns_that_same_object()
        {
            var byPath = AssetDatabase.LoadAssetAtPath<MoodGradeProfile>(ProfilePath);
            Assert.That(byPath, Is.Not.Null, $"{ProfilePath} is missing — run Hidden Harbours ▸ Lighting ▸ Create the Mood Grade Profile");
            var byName = Resources.Load<MoodGradeProfile>("MoodGradeProfile");
            Assert.That(byName, Is.Not.Null, "Resources.Load by the director's name");
            Assert.That(byName, Is.SameAs(byPath));
        }

        static IEnumerable<(string name, MoodGrade g)> Keys(MoodGradeProfile p)
        {
            yield return ("Day", p.Day); yield return ("GoldenHour", p.GoldenHour); yield return ("Night", p.Night);
            yield return ("Fog", p.Fog); yield return ("Storm", p.Storm);
        }

        static void AssertBibleFeatures(MoodGradeProfile p, string which)
        {
            foreach (var (name, g) in Keys(p))
            {
                Assert.That(g.GrainIntensity, Is.EqualTo(0f), $"{which} {name}: film grain ships OFF (charter budget)");
                Assert.That(g.ChromaticAberration, Is.EqualTo(0f), $"{which} {name}: chromatic aberration ships OFF (charter)");
                Assert.That(g.VignetteSmoothness, Is.GreaterThan(0f), $"{which} {name}: a zero smoothness is an unauthored key");
                Assert.That(g.BloomThreshold, Is.GreaterThan(0f), $"{which} {name}: a zero bloom threshold blooms the whole frame");
            }
            Assert.That(p.Night.Lift.z, Is.GreaterThan(p.Night.Lift.x), $"{which}: night shadows are cold (bible §4.2)");
            Assert.That(p.Night.Lift.x, Is.LessThan(p.Day.Lift.x + 1e-6f), $"{which}: night lift is not warmer than day's");
            Assert.That(p.Night.BloomIntensity, Is.GreaterThan(p.Day.BloomIntensity), $"{which}: lamps bloom more at night");
            Assert.That(p.GoldenHour.Gain.x, Is.GreaterThan(p.GoldenHour.Gain.z), $"{which}: golden-hour highlights are warm");
            Assert.That(p.GoldenHour.Lift.x, Is.GreaterThan(p.GoldenHour.Lift.z), $"{which}: golden-hour lift is warm");
            Assert.That(p.Fog.Saturation, Is.LessThan(p.Day.Saturation), $"{which}: fog crushes saturation toward grey");
            Assert.That(p.Fog.Contrast, Is.LessThan(p.Day.Contrast), $"{which}: fog flattens contrast");
            Assert.That(p.Fog.VignetteIntensity, Is.GreaterThan(p.Day.VignetteIntensity), $"{which}: the vignette closes in fog");
            Assert.That(p.Fog.Lift.w, Is.GreaterThan(p.Day.Lift.w), $"{which}: fog lifts the black point");
            Assert.That(p.Storm.Contrast, Is.GreaterThan(p.Day.Contrast), $"{which}: storm is high contrast");
            Assert.That(p.Storm.Saturation, Is.LessThan(p.Day.Saturation), $"{which}: storm is low value, cold");
        }

        [Test]
        public void The_shipped_keys_carry_the_bible_features()
        {
            var p = AssetDatabase.LoadAssetAtPath<MoodGradeProfile>(ProfilePath);
            Assert.That(p, Is.Not.Null);
            AssertBibleFeatures(p, "asset");
        }

        [Test]
        public void The_code_fallback_carries_the_same_bible_features()
        {
            var p = MoodGradeProfile.CreateDefault();
            try { AssertBibleFeatures(p, "fallback"); }
            finally { Object.DestroyImmediate(p); }
        }

        static HashSet<string> ShippedRegionIds()
        {
            var ids = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:RegionDef"))
            {
                var r = AssetDatabase.LoadAssetAtPath<RegionDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null && !string.IsNullOrEmpty(r.Id)) ids.Add(r.Id);
            }
            return ids;
        }

        [Test]
        public void Every_region_override_names_a_shipped_region_once_and_loads_by_the_directors_folder()
        {
            var overrides = Resources.LoadAll<MoodGradeRegionOverride>("MoodGrade");
            Assert.That(overrides.Length, Is.GreaterThanOrEqualTo(MoodGradeProfileBuilder.ShippedRegions.Length),
                        $"fewer overrides under {RegionFolder} than the builder ships");
            var regions = ShippedRegionIds();
            Assert.That(regions, Is.Not.Empty, "no RegionDef assets found — the test's premise moved");
            var seen = new HashSet<string>();
            foreach (var o in overrides)
            {
                Assert.That(o.RegionId, Is.Not.Empty, $"{o.name} has no region id");
                Assert.That(regions, Does.Contain(o.RegionId), $"{o.name} names {o.RegionId}, which no RegionDef publishes");
                Assert.That(seen.Add(o.RegionId), Is.True, $"{o.RegionId} is biased by more than one file — the last one loaded would win");
            }
        }

        [Test]
        public void The_builder_ships_every_region_it_names_and_the_files_say_what_it_says()
        {
            foreach (var r in MoodGradeProfileBuilder.ShippedRegions)
            {
                var o = AssetDatabase.LoadAssetAtPath<MoodGradeRegionOverride>($"{RegionFolder}/{r.file}.asset");
                Assert.That(o, Is.Not.Null, $"{RegionFolder}/{r.file}.asset is missing");
                Assert.That(o.RegionId, Is.EqualTo(r.id), $"{r.file} id");
            }
        }

        [Test]
        public void Nine_Mile_Creek_is_the_most_colourful_place_on_the_coast_and_St_Peters_is_the_identity()
        {
            var overrides = Resources.LoadAll<MoodGradeRegionOverride>("MoodGrade");
            var nmc = overrides.FirstOrDefault(o => o.RegionId == "region.nine_mile_creek");
            var stp = overrides.FirstOrDefault(o => o.RegionId == "region.st_peters");
            Assert.That(nmc, Is.Not.Null, "Nine Mile Creek override");
            Assert.That(stp, Is.Not.Null, "St Peters override (the file the owner edits)");
            Assert.That(nmc.SaturationOffset, Is.GreaterThan(0f), "NMC is the most colourful (bible §4.3)");
            Assert.That(nmc.SaturationOffset, Is.EqualTo(overrides.Max(o => o.SaturationOffset)), "nobody out-colours NMC");
            Assert.That(nmc.ColorFilterTint.r, Is.GreaterThanOrEqualTo(nmc.ColorFilterTint.b), "NMC is warm, not cold");
            Assert.That(stp.IsIdentity, Is.True, "St Peters ships at the identity until the owner moves it");
        }
    }
}
