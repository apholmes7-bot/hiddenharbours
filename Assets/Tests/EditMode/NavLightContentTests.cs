using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Boats;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE LIT MARKS, AS SHIPPED — do the ten defs say something a lamp can actually show, and
    /// do the marks standing in the two harbours light a readable picture?</b>
    ///
    /// <para>Where <see cref="NavLightCharacterTests"/> pins the arithmetic, this file points it at
    /// the real content: the ten <see cref="NavBuoyDef"/> assets in the project, and the twenty-three
    /// marks the two region planners actually place. Nothing here loads a scene — the plans are pure
    /// functions of the same terrain the builders configure, which is what lets a headless test know
    /// where every buoy in the game is.</para>
    /// </summary>
    public class NavLightContentTests
    {
        private GameObject _nmcGo;
        private GameObject _spGo;
        private NavMarkPlanResult _nmcPlan;
        private NavMarkPlanResult _spPlan;
        private Dictionary<string, NavBuoyDef> _defs;

        [SetUp]
        public void SetUp()
        {
            _nmcGo = new GameObject("NineMileCreekMainland_NavLightTest");
            var nmc = _nmcGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(nmc);
            _nmcPlan = NineMileCreekNavMarks.Plan(nmc);

            _spGo = new GameObject("StPeters_NavLightTest");
            var sp = _spGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(sp);
            _spPlan = StPetersNavMarks.Plan(sp);

            _defs = NavMarkPlacer.LoadDefsByMarkType(NineMileCreekNavMarks.DefFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (_nmcGo != null) Object.DestroyImmediate(_nmcGo);
            if (_spGo != null) Object.DestroyImmediate(_spGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE TEN DEFS
        // =============================================================================================

        /// <summary>
        /// Every shipped mark either shows a character a lamp can render, or is honestly unlit. The
        /// failure this exists to catch is the middle case: a def carrying a light string nothing can
        /// parse, which reaches the water as a mark whose lamp appears to have failed.
        /// </summary>
        [Test]
        public void EveryShippedMarkEitherFlashesOrIsHonestlyUnlit()
        {
            Assert.That(_defs.Count, Is.GreaterThan(0),
                        $"no NavBuoyDef assets under {NineMileCreekNavMarks.DefFolder} — run " +
                        "'Hidden Harbours ▸ Art ▸ Build Nav Buoy Defs'.");

            var faults = new List<string>();
            int lit = 0, unlit = 0;

            foreach (KeyValuePair<string, NavBuoyDef> kv in _defs)
            {
                NavBuoyDef def = kv.Value;
                bool idEmpty = string.IsNullOrWhiteSpace(def.LightCharacter);
                bool textEmpty = string.IsNullOrWhiteSpace(def.LightText);

                if (idEmpty && textEmpty) { unlit++; continue; }

                if (idEmpty != textEmpty)
                {
                    faults.Add($"{kv.Key}: half-lit — LightCharacter '{def.LightCharacter}' vs " +
                               $"LightText '{def.LightText}'. A mark is lit or she is not.");
                    continue;
                }

                if (!NavLightCharacter.TryParse(def.LightText, out NavLightCharacter c, out string error))
                {
                    faults.Add($"{kv.Key}: '{def.LightText}' does not parse — {error}");
                    continue;
                }

                if (!c.IsLit) { faults.Add($"{kv.Key}: '{def.LightText}' parsed to nothing"); continue; }
                lit++;
            }

            Assert.That(faults, Is.Empty, string.Join("\n", faults));
            Assert.That(lit, Is.GreaterThan(0), "not one shipped mark is lit");
            Debug.Log($"[NavLight] {lit} lit mark types, {unlit} unlit, of {_defs.Count}.");
        }

        /// <summary>
        /// ⭐ <b>The id and the text must name the same light.</b> The runtime reads
        /// <c>LightText</c> (the only one of the two that is complete — see
        /// <see cref="NavLightCharacter"/>), so a def whose <c>LightCharacter</c> id said one thing
        /// and whose text said another would show the text and be catalogued as the id, silently,
        /// forever. This is not a second parser: it checks that the id opens with the same rhythm
        /// token and carries the same colour letter, which is all an id claims to record.
        /// </summary>
        [Test]
        public void TheIdAndTheTextNameTheSameLight()
        {
            var faults = new List<string>();

            foreach (KeyValuePair<string, NavBuoyDef> kv in _defs)
            {
                NavBuoyDef def = kv.Value;
                if (string.IsNullOrWhiteSpace(def.LightText)) continue;
                if (!NavLightCharacter.TryParse(def.LightText, out NavLightCharacter c, out _)) continue;

                string id = def.LightCharacter ?? "";
                string rhythm = RhythmToken(c.Rhythm);
                if (!id.StartsWith(rhythm, System.StringComparison.Ordinal))
                    faults.Add($"{kv.Key}: id '{id}' does not open with '{rhythm}', but the text " +
                               $"'{def.LightText}' is a {c.Rhythm}.");

                if (c.ColourStated)
                {
                    char letter = ColourLetter(c.Colour);
                    if (id.IndexOf(letter) < 0)
                        faults.Add($"{kv.Key}: the text '{def.LightText}' shows {c.Colour} but the " +
                                   $"id '{id}' carries no '{letter}'.");
                }

                if (c.GroupCount > 1 && id.IndexOf(c.GroupCount.ToString()) < 0)
                    faults.Add($"{kv.Key}: the text '{def.LightText}' is a group of {c.GroupCount} " +
                               $"but the id '{id}' does not say so.");
            }

            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        private static string RhythmToken(NavLightRhythm r)
        {
            switch (r)
            {
                case NavLightRhythm.Quick:     return "Q";
                case NavLightRhythm.VeryQuick: return "VQ";
                case NavLightRhythm.LongFlash: return "LFl";
                case NavLightRhythm.Fixed:     return "F";
                default:                       return "Fl";
            }
        }

        private static char ColourLetter(NavLightColour c)
        {
            switch (c)
            {
                case NavLightColour.Green:  return 'G';
                case NavLightColour.Red:    return 'R';
                case NavLightColour.Yellow: return 'Y';
                default:                    return 'W';
            }
        }

        /// <summary>
        /// The lateral marks show the colour of the hand they stand on — Region B, green to port
        /// going upstream. Getting this backwards is the one defect a lateral system exists to make
        /// impossible, and the light has to agree with the paint.
        /// </summary>
        [Test]
        public void ALateralsLightShowsTheColourOfHerHand()
        {
            AssertMarkColour("PortCan",  NavLightColour.Green);
            AssertMarkColour("PortLit",  NavLightColour.Green);
            AssertMarkColour("StbdNun",  NavLightColour.Red);
            AssertMarkColour("StbdLit",  NavLightColour.Red);

            // The cardinals and the isolated danger are white by being what they are.
            AssertMarkColour("CardinalN", NavLightColour.White);
            AssertMarkColour("CardinalE", NavLightColour.White);
            AssertMarkColour("CardinalS", NavLightColour.White);
            AssertMarkColour("CardinalW", NavLightColour.White);
            AssertMarkColour("Isolated",  NavLightColour.White);
        }

        private void AssertMarkColour(string markType, NavLightColour want)
        {
            Assert.That(_defs.ContainsKey(markType), Is.True, $"no def for '{markType}'");
            NavBuoyDef def = _defs[markType];
            Assert.That(NavLightCharacter.TryParse(def.LightText, out NavLightCharacter c, out string e),
                        Is.True, $"{markType}: '{def.LightText}' did not parse — {e}");
            Assert.That(c.Colour, Is.EqualTo(want),
                        $"{markType} shows {c.Colour}, and IALA Region B says {want}.");
        }

        /// <summary>
        /// ⭐ <b>The one prefab every mark in the game wears carries a lamp.</b>
        ///
        /// <para>This is the wiring that makes the feature reach the water at all: twenty-five marks
        /// are already standing in the two scenes as instances of this prefab, and a component added
        /// to the asset reaches every one of them without a rebuild. It is asserted because the
        /// component block was added to the prefab's YAML by hand — a clean import proves the file
        /// parses, not that the lamp is on it — and because a future re-run of
        /// <c>NavBuoyDefBuilder.BuildPrefab</c> that dropped the line would leave every mark in both
        /// harbours dark with nothing else going red.</para>
        /// </summary>
        [Test]
        public void TheNavBuoyPrefabCarriesALamp()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                NineMileCreekNavMarks.PrefabPath);

            Assert.That(prefab, Is.Not.Null,
                        $"no nav-buoy prefab at {NineMileCreekNavMarks.PrefabPath} — run " +
                        "'Hidden Harbours ▸ Art ▸ Build Nav Buoy Prefab'.");
            Assert.That(prefab.GetComponent<NavBuoyVisual>(), Is.Not.Null,
                        "the prefab carries no NavBuoyVisual");
            Assert.That(prefab.GetComponent<NavLight>(), Is.Not.Null,
                        "the prefab carries no NavLight — every mark placed from it would be dark, " +
                        "and nothing else in the suite would notice.");

            // She must also be able to ANSWER the lamp: the seam is what joins the two.
            Assert.That(prefab.GetComponent<INavLightSource>(), Is.Not.Null,
                        "nothing on the prefab answers INavLightSource, so the lamp has no character " +
                        "to show");
        }

        /// <summary>A mooring buoy carries no light, and must therefore cost no lamp at all.</summary>
        [Test]
        public void TheMooringBuoyIsUnlitAndCostsNothing()
        {
            Assert.That(_defs.ContainsKey("Mooring"), Is.True, "no Mooring def");
            NavBuoyDef mooring = _defs["Mooring"];
            Assert.That(mooring.IsLit, Is.False, "the mooring buoy has acquired a light character");
            Assert.That(NavLightCharacter.Parse(mooring.LightText).IsLit, Is.False);
        }

        // =============================================================================================
        //  2. THE MARKS AS PLACED — twenty-three of them, in two harbours
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>No two marks of one character wink together — WITHIN A REGION.</b> Real marks are
        /// unsynchronised, and two port-hand cans flashing as one reads as a single light in the
        /// wrong place. The phase comes from each mark's own chart id, so it is a property of the
        /// CHART and identical on every machine and every run — the other half of what is asserted.
        ///
        /// <para><b>⚠️ The comparison is PER REGION, and that is a correction the measurement forced.</b>
        /// Compared across the whole game, the two closest-phased marks of one character came out
        /// <b>0.019 s apart on a one-second period</b> — which is unison to any eye. They are the north
        /// cardinals of Nine Mile Creek and St Peters: two harbours the player is never in at once, so
        /// the two lamps are never in one frame and the coincidence cannot be seen. Asserting on the
        /// global minimum would therefore have failed on a picture nobody can look at, and — worse —
        /// a global bar that PASSED would have said nothing about the thing that matters. What can be
        /// seen together is what stands in one region, so that is what is held to a bar. The
        /// cross-region figure is still reported, because it is the number that explains why this
        /// test is shaped the way it is.</para>
        /// </summary>
        [Test]
        public void NoTwoMarksOfOneCharacterFlashInUnisonInOneRegion()
        {
            var perRegion = new Dictionary<string, List<(string Id, float Phase, float Period)>>();
            int lit = 0;

            // ⭐ Through the SAME call the placer makes, per region — a test that re-derived the
            // phases its own way would be checking its own arithmetic rather than the game's.
            void TakeRegion(string region, NavMarkPlanResult plan)
            {
                Dictionary<string, float> fractions =
                    NavLightPhasePlan.Spread(CharactersOf(plan));

                foreach (PlannedNavMark m in plan.Marks)
                {
                    if (!_defs.TryGetValue(m.MarkType, out NavBuoyDef def) || def == null) continue;
                    if (!NavLightCharacter.TryParse(def.LightText, out NavLightCharacter c, out _)) continue;

                    lit++;
                    Assert.That(fractions.ContainsKey(m.Id), Is.True,
                                $"the phase plan gave '{m.Id}' no slot, though she is lit");
                    float phase = c.PeriodSeconds * fractions[m.Id];

                    string key = region + "|" + def.LightText;
                    if (!perRegion.TryGetValue(key, out var a))
                        perRegion[key] = a = new List<(string, float, float)>();
                    a.Add((m.Id, phase, c.PeriodSeconds));
                }
            }

            TakeRegion("NineMileCreek", _nmcPlan);
            TakeRegion("StPeters", _spPlan);

            Assert.That(lit, Is.GreaterThan(0), "not one placed mark is lit");

            float inRegion = Closest(perRegion, out string inPair, out int biggestGroup);

            Debug.Log($"[NavLight] {lit} lit marks placed. Closest two of one character IN ONE REGION: " +
                      $"{inRegion:0.###}s — {inPair}. Largest group of one character in a region: " +
                      $"{biggestGroup}.");

            // The bar: two marks of one character must be staggered by at least half a quick flash —
            // the shortest flash in the kit — so the pair reads as two lights rather than one.
            const float MinStaggerSeconds = 0.25f;
            Assert.That(inRegion, Is.GreaterThan(MinStaggerSeconds),
                        $"two marks of one character in one region are only {inRegion:0.###}s apart " +
                        $"({inPair}) — they overlap for most of the time either is lit and read as one " +
                        "light in the wrong place.");
        }

        /// <summary>
        /// The spread's own guarantee, checked against the marks actually placed: the worst gap it
        /// can produce is <c>(1 - 2·jitter)/k</c> of the period, and the measurement above must not
        /// beat it — if it did, the bound would be wrong rather than the picture good.
        /// </summary>
        [Test]
        public void TheSpreadDeliversTheGapItPromises()
        {
            foreach (NavMarkPlanResult plan in new[] { _nmcPlan, _spPlan })
            {
                Dictionary<string, float> fractions = NavLightPhasePlan.Spread(CharactersOf(plan));
                var byCharacter = new Dictionary<string, List<float>>();

                foreach (PlannedNavMark m in plan.Marks)
                {
                    if (!_defs.TryGetValue(m.MarkType, out NavBuoyDef def) || def == null) continue;
                    if (!fractions.TryGetValue(m.Id, out float f)) continue;
                    if (!byCharacter.TryGetValue(def.LightText, out var list))
                        byCharacter[def.LightText] = list = new List<float>();
                    list.Add(f);
                }

                foreach (KeyValuePair<string, List<float>> kv in byCharacter)
                {
                    List<float> f = kv.Value;
                    if (f.Count < 2) continue;
                    f.Sort();

                    float worst = 1f;
                    for (int i = 0; i < f.Count; i++)
                    {
                        float next = i + 1 < f.Count ? f[i + 1] : f[0] + 1f;
                        worst = Mathf.Min(worst, next - f[i]);
                    }

                    float promised = NavLightPhasePlan.GuaranteedGapFraction(f.Count);
                    Assert.That(worst, Is.GreaterThanOrEqualTo(promised - 1e-5f),
                                $"'{kv.Key}' x{f.Count}: the closest pair is {worst:0.####} of the " +
                                $"period but the plan guarantees {promised:0.####}.");
                }
            }
        }

        private IEnumerable<(string Id, string Character)> CharactersOf(NavMarkPlanResult plan)
        {
            foreach (PlannedNavMark m in plan.Marks)
            {
                _defs.TryGetValue(m.MarkType, out NavBuoyDef def);
                yield return (m.Id, def != null ? def.LightText : null);
            }
        }

        private static float Closest(Dictionary<string, List<(string Id, float Phase, float Period)>> groups,
                                     out string pair, out int biggestGroup)
        {
            float worst = float.MaxValue;
            pair = "(no character is worn twice)";
            biggestGroup = 0;

            foreach (KeyValuePair<string, List<(string Id, float Phase, float Period)>> kv in groups)
            {
                List<(string Id, float Phase, float Period)> marks = kv.Value;
                if (marks.Count > biggestGroup) biggestGroup = marks.Count;
                for (int i = 0; i < marks.Count; i++)
                    for (int j = i + 1; j < marks.Count; j++)
                    {
                        float d = Mathf.Abs(marks[i].Phase - marks[j].Phase);
                        float gap = Mathf.Min(d, marks[i].Period - d);   // circular — the period wraps
                        if (gap >= worst) continue;
                        worst = gap;
                        pair = $"{marks[i].Id} / {marks[j].Id} ('{kv.Key}')";
                    }
            }
            return worst;
        }

        /// <summary>
        /// The same chart produces the same picture every run. Phases are recomputed from the ids
        /// three times over and must come back bit-identical: nothing here may reach for a sibling
        /// index, an instance id or a clock.
        /// </summary>
        [Test]
        public void ThePictureIsIdenticalRunToRun()
        {
            List<float> First()
            {
                var phases = new List<float>();
                foreach (PlannedNavMark m in AllMarks())
                {
                    if (!_defs.TryGetValue(m.MarkType, out NavBuoyDef def) || def == null) continue;
                    NavLightCharacter c = NavLightCharacter.Parse(def.LightText);
                    phases.Add(c.IsLit ? c.PhaseFromSeed(NavLightCharacter.SeedFromId(m.Id)) : -1f);
                }
                return phases;
            }

            List<float> a = First(), b = First(), c2 = First();
            Assert.That(b, Is.EqualTo(a), "the second pass phased the marks differently");
            Assert.That(c2, Is.EqualTo(a), "the third pass phased the marks differently");
        }

        /// <summary>
        /// ⭐ <b>A lantern must not reach her neighbour.</b> The sidelights have this problem in
        /// miniature — where red and green overlap additively the answer is yellow — and a channel
        /// whose two hands merged into one colour would mark nothing. So the preset's reach is held
        /// below HALF the closest gap between any two placed marks in either harbour, measured off
        /// the real plans rather than assumed.
        /// </summary>
        [Test]
        public void ALanternCannotReachHerNeighbour()
        {
            float closest = float.MaxValue;
            string pair = "";
            var marks = new List<PlannedNavMark>(AllMarks());

            for (int i = 0; i < marks.Count; i++)
                for (int j = i + 1; j < marks.Count; j++)
                {
                    float d = Vector2.Distance(marks[i].At, marks[j].At);
                    if (d < closest) { closest = d; pair = $"{marks[i].Id} / {marks[j].Id}"; }
                }

            Assert.That(marks.Count, Is.GreaterThan(1), "fewer than two marks placed");
            Assert.That(NavLightPresets.LanternRangeMetres * 2f, Is.LessThan(closest),
                        $"two lanterns at {NavLightPresets.LanternRangeMetres} m would overlap: the " +
                        $"closest two marks in the game are {closest:0.##} m apart ({pair}). Shrink " +
                        "the preset — do not move the marks, which are placed by the water.");
            Debug.Log($"[NavLight] closest two marks {closest:0.##} m ({pair}); " +
                      $"lantern reach {NavLightPresets.LanternRangeMetres} m, so a pair clears by " +
                      $"{closest - NavLightPresets.LanternRangeMetres * 2f:0.##} m.");
        }

        private IEnumerable<PlannedNavMark> AllMarks()
        {
            foreach (PlannedNavMark m in _nmcPlan.Marks) yield return m;
            foreach (PlannedNavMark m in _spPlan.Marks) yield return m;
        }
    }
}
