using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One rung of a species' size ladder: the scale the rig is rendered at, and the catch weight
    /// that scale represents through the rig's own mass law.
    /// </summary>
    public readonly struct FishRung
    {
        /// <summary>0, 1, 2 — small, middle, large.</summary>
        public readonly int Index;

        /// <summary>The <c>scale</c> option handed to <c>FishIso2.render</c>.</summary>
        public readonly double Scale;

        /// <summary>The catch weight this rung draws, kg, from <c>FishIso2.hold(species, scale)</c>.</summary>
        public readonly double Kg;

        /// <summary>Body length in metres at this rung (<c>SPECIES.len × scale</c>).</summary>
        public readonly double LengthMetres;

        /// <summary>
        /// How many hands this rung takes, from <c>FishIso2.hold(species, scale).hands</c> — 1 (one
        /// fish per hand, by the gill or the tail) or 2 (the two-arm cradle).
        ///
        /// <para><b>It belongs to the RUNG, not the species.</b> The rig's rule is a mass threshold,
        /// and a ladder that spans a def's whole weight band crosses it. Asking <c>hold()</c> once at
        /// scale 1 — what the first cut of this sidecar did — answers for a fish that is not on the
        /// ladder at all, and measurably disagrees with the sheet actually loaded: haddock is one
        /// hand at scale 1 but 2.79 kg and a cradle at its middle rung, pollock one hand but 4.19 kg
        /// and a cradle. Two of the four shipped species held the wrong pose.</para>
        /// </summary>
        public readonly int Hands;

        /// <summary>Sheet-name suffix: <c>_sm</c>, <c>""</c> (the middle rung keeps the legacy
        /// stem), <c>_lg</c>.</summary>
        public readonly string Suffix;

        /// <summary>Where the band came from: a shipped def's weight range, or the rig's own
        /// <c>SPECIES.range</c> when no def names this species yet.</summary>
        public readonly string BandSource;

        public FishRung(int index, double scale, double kg, double lengthMetres, int hands,
                        string suffix, string bandSource)
        {
            Index = index; Scale = scale; Kg = kg; LengthMetres = lengthMetres; Hands = hands;
            Suffix = suffix; BandSource = bandSource;
        }

        public override string ToString() =>
            $"r{Index} scale {Scale:F4} = {Kg:F2} kg = {LengthMetres * 100:F0} cm, {Hands} hand(s)";
    }

    /// <summary>One cell the rig draws past the edge of its own sheet cell.</summary>
    public readonly struct ClippedCell
    {
        public readonly string Species, State;
        public readonly int Rung, Dir, Frame;
        public readonly string Edges;

        public ClippedCell(string species, string state, int rung, int dir, int frame, string edges)
        { Species = species; State = state; Rung = rung; Dir = dir; Frame = frame; Edges = edges; }

        public override string ToString() =>
            $"{Species}/{State} r{Rung} dir {Dir} f{Frame} touches {Edges}";
    }

    /// <summary>
    /// Bakes catch pass 2's fish — <c>FishIso2</c>, seven species, six water anims and four rests,
    /// at a three-rung SIZE LADDER. Sibling of <see cref="FishingKitBaker"/> rather than a branch
    /// inside it: pass 1 keeps baking exactly as it does today (its rig, its four species, its one
    /// scale), and the two can be compared cell for cell while the changeover is reviewed. All the
    /// shared plumbing — <see cref="FishingKitBaker.WriteSheet"/>, <see cref="FishingKitBaker.Render"/>,
    /// the azimuth refusal — is reused, not copied.
    ///
    /// <para><b>Why a ladder at all.</b> The runtime never scales a fish sprite: <c>RodFightPresenter</c>
    /// swaps frames and nothing else, and pixel art on the 32 px/m grid cannot be scaled at draw time
    /// without breaking that grid. So a catch that weighs twice as much has to be a DIFFERENT SHEET,
    /// and the ladder is how many of those we are willing to hold.</para>
    ///
    /// <para><b>Which band the ladder spans — the owner ruled this on 2026-09-05, and the three
    /// candidates genuinely disagree.</b> The rig declares <c>SPECIES.range</c> (cod [0.6, 1.5]);
    /// <c>CatchKit2.fillItems</c> only ever rolls <c>clamp(0.85 + rng·0.3, range)</c> = 0.85..1.15;
    /// and the GAME rolls kilograms, over the shipped <c>FishSpeciesDef</c> band
    /// (<c>fish.atlantic_cod</c> is 2..12 kg), which through the rig's own law is scale 0.873..1.586.
    /// The ruling: <b>span what the game actually rolls</b>. The rungs are then spaced evenly in
    /// RENDERED LENGTH rather than in mass, because length is what the eye reads and mass goes as
    /// the cube of it.</para>
    ///
    /// <para>⚠️ <b>That deliberately steps outside the art director's declared range</b> at the top
    /// of four species. It is sanctioned, and it is recorded per rung in the anchors JSON
    /// (<c>bandSource</c>) so no later reader has to guess why a cod is baked at 1.586. A species
    /// with no def yet — bass, flounder, herring — falls back to <c>SPECIES.range</c> min/1/max,
    /// which is all the information that exists for it.</para>
    ///
    /// <para>⚠️ <b>The middle rung keeps the legacy stem.</b> <c>Fish_cod_swim.png</c> is still the
    /// file every existing consumer loads; the ladder adds <c>_sm</c> and <c>_lg</c> siblings beside
    /// it. So the ladder is purely additive on disk. It is NOT a no-op on pixels: the middle rung is
    /// the kg band's midpoint, not scale 1, and pass 2 re-lofts the four pass-1 species anyway —
    /// 33,547 pixels differ at scale 1, in every one of 280 cells. See <c>RigCatalog.CatchPass2Kit</c>.</para>
    ///
    /// <para><b>The clip ledger.</b> Some cells draw past the 64×64 cell edge and the bake RECORDS
    /// them rather than refusing, because refusing would make pass 2 unbakeable through no fault of
    /// this code: cod at its own declared 1.5 and bass at its own declared 1.6 already clip, always
    /// at the BOTTOM edge, overwhelmingly in the <c>gill</c> rows — the pivot (32,38) leaves only
    /// 25 px below it and a big fish hangs DOWN from the gill grip. The ledger is asserted in both
    /// directions by <c>CatchPass2BakeTests</c>, so when the rig's cell grows the test goes red and
    /// the ledger gets deleted rather than quietly outliving the defect.</para>
    ///
    /// Like its siblings this stops at "PNG (+ anchors JSON) on disk": slicing is
    /// <c>FishingSheetSlicer</c>'s job (pass 2 shares the <c>Fish_</c> prefix spec exactly — same
    /// 64×64 cell, same 8 rows, same (32,38) pivot — so it needs no slicer change) and import
    /// settings are <c>ArtImportPipeline</c>'s.
    /// </summary>
    public static class CatchPass2Baker
    {
        /// <summary>The ADR-0006 recipe's facing count. <c>FishIso2</c> declares no DIRS global,
        /// exactly like pass 1, so the count is the recipe — stated once, here.</summary>
        public const int Dirs = 8;

        /// <summary>Catalog key for the pass-2 fish rig.</summary>
        public const string RigKey = "fish2";

        /// <summary>Same folder as pass 1 — these sheets REPLACE those, they do not sit beside
        /// them.</summary>
        public const string DefaultOutputFolder = FishingKitBaker.DefaultOutputFolder;

        /// <summary>Sheet-stem suffix per rung. The middle rung is unsuffixed on purpose (see the
        /// class remarks): it keeps the stem every existing consumer already loads.</summary>
        public static readonly string[] RungSuffixes = { "_sm", "", "_lg" };

        /// <summary>How many rungs a species' ladder holds.</summary>
        public const int Rungs = 3;

        // =====================================================================================
        // THE LADDER
        // =====================================================================================

        /// <summary>
        /// The scale that draws <paramref name="kg"/> of this species, inverting the rig's OWN mass
        /// law rather than a transcription of it: <c>hold()</c> computes
        /// <c>len · girth² · scale³ · 390 · massK</c>, so <c>scale = (kg / massAtScale1)^(1/3)</c>,
        /// and <c>massAtScale1</c> is read from the rig by evaluating <c>hold(species, 1)</c> at
        /// full precision through its own factors — never from <c>hold()</c>'s rounded return.
        /// </summary>
        public static double ScaleForKg(IRigScriptHost host, string g, string species, double kg)
        {
            double k = MassAtUnitScale(host, g, species);
            if (k <= 0)
                throw new InvalidOperationException(
                    $"FishIso2.SPECIES['{species}'] reports a non-positive mass at scale 1 ({k}). " +
                    "The ladder cannot be derived from it.");
            return Math.Pow(Math.Max(0.0, kg) / k, 1.0 / 3.0);
        }

        /// <summary>
        /// The species' mass at scale 1, in kg, at FULL precision — the product of the rig's own
        /// factors, not <c>hold()</c>'s two-decimal rounding. Rounding here would move every rung.
        /// </summary>
        public static double MassAtUnitScale(IRigScriptHost host, string g, string species)
        {
            AssertSpecies(host, g, species);
            string sp = $"{g}.SPECIES[{FishingKitBaker.Js(species)}]";
            return host.EvaluateNumber($"{sp}.len*{sp}.girth*{sp}.girth*390*({sp}.massK||1)");
        }

        /// <summary>
        /// This species' three rungs. When <paramref name="band"/> carries a shipped def's weight
        /// range the ladder spans it, evenly spaced in rendered LENGTH; otherwise it falls back to
        /// the rig's <c>SPECIES.range</c> min/1/max and says so in every rung's
        /// <see cref="FishRung.BandSource"/>.
        /// </summary>
        public static IReadOnlyList<FishRung> LadderFor(IRigScriptHost host, string g, string species,
                                                        FishWeightBand? band)
        {
            AssertSpecies(host, g, species);
            string sp = $"{g}.SPECIES[{FishingKitBaker.Js(species)}]";
            double len = host.EvaluateNumber($"{sp}.len");

            double[] scales;
            string source;
            if (band.HasValue)
            {
                double lo = ScaleForKg(host, g, species, band.Value.MinKg);
                double hi = ScaleForKg(host, g, species, band.Value.MaxKg);
                if (hi < lo) (lo, hi) = (hi, lo);
                scales = new[] { lo, (lo + hi) * 0.5, hi };
                source = $"{band.Value.DefId} {Num(band.Value.MinKg)}..{Num(band.Value.MaxKg)} kg";
            }
            else
            {
                double lo = host.EvaluateNumber($"{sp}.range[0]");
                double hi = host.EvaluateNumber($"{sp}.range[1]");
                scales = new[] { lo, 1.0, hi };
                source = "SPECIES.range (no FishSpeciesDef names this species yet)";
            }

            var rungs = new List<FishRung>(Rungs);
            for (int i = 0; i < Rungs; i++)
            {
                double s = scales[i];
                // Mass AND hands come from one call at THIS rung's scale. The rig decides the carry
                // from the mass it computes, so asking it once per rung is the only way the two can
                // never disagree â€” and the hand count is what picks the gill sheet over the tail.
                string hold = $"{g}.hold({FishingKitBaker.Js(species)},{Num(s)})";
                double kg = host.EvaluateNumber($"{hold}.mass");
                int hands = (int)host.EvaluateNumber($"{hold}.hands");
                if (hands < 1)
                    throw new InvalidOperationException(
                        $"{g}.hold('{species}', {Num(s)}) reports {hands} hands â€” the rig's carry " +
                        "contract is 1 or 2. Do not bake a table nothing can hold.");
                rungs.Add(new FishRung(i, s, kg, len * s, hands, RungSuffixes[i], source));
            }
            return rungs;
        }

        /// <summary>A shipped species def's weight range, resolved for the ladder.</summary>
        public readonly struct FishWeightBand
        {
            public readonly string DefId;
            public readonly double MinKg, MaxKg;
            public FishWeightBand(string defId, double minKg, double maxKg)
            { DefId = defId; MinKg = minKg; MaxKg = maxKg; }
        }

        // =====================================================================================
        // THE SIDECAR, WITHOUT THE SHEETS
        // =====================================================================================

        /// <summary>
        /// Re-emit <c>FishIsoAnchors.json</c> from the rig <b>without re-rendering a single
        /// sheet</b>, and hand back the ladders it wrote.
        ///
        /// <para><b>Why this exists.</b> The sidecar carries two kinds of thing: the ladder (what
        /// each rung weighs and how many hands it takes) and the mouth tables. Both are pure
        /// functions of the rig, and neither depends on a pixel. When a CONSUMER learns something
        /// new to ask for — as the fight did when it started picking rungs by weight — the honest
        /// fix is to publish the answer, not to reconstruct it downstream from a table that was
        /// exported for a different reader. Doing that through <see cref="BakeFish"/> would cost a
        /// full 210-sheet render and produce 210 files of churn to change one JSON; this is the
        /// same code path for the part that matters, in seconds, touching one file.</para>
        ///
        /// <para>It shares <see cref="WriteAnchors"/> with the bake rather than copying it, so the
        /// two can never drift into publishing different sidecars — and it runs the same azimuth
        /// probe and the same species/state validation first, because a sidecar written against an
        /// unverified convention is worse than none.</para>
        /// </summary>
        public static CatchPass2BakeResult RewriteFishAnchors(
            IReadOnlyDictionary<string, FishWeightBand> bands = null,
            IReadOnlyList<string> species = null,
            IReadOnlyList<string> states = null,
            string outputFolder = DefaultOutputFolder)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(RigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new CatchPass2BakeResult
            {
                RigKey = RigKey, EngineName = host.EngineName, Geometry = geo,
            };

            var probe = FishingRigAzimuthProbe.MeasureFish(host, g, geo, Dirs);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;
            FishingKitBaker.RefuseOnMismatch(RigKey, entry.DeclaredConvention, probe.Convention,
                                             probe.Report);

            species ??= FishingKitBaker.ReadStringArray(host, $"{g}.ORDER");
            states ??= ReadStateOrder(host, g);
            foreach (string s in species) AssertSpecies(host, g, s);
            foreach (string st in states) StateFrames(host, g, st);

            var ladders = new Dictionary<string, IReadOnlyList<FishRung>>(StringComparer.Ordinal);
            foreach (string s in species)
            {
                FishWeightBand? band = null;
                if (bands != null && bands.TryGetValue(s, out var b)) band = b;
                ladders[s] = LadderFor(host, g, s, band);
            }
            result.Ladders = ladders;

            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));
            result.AnchorJsonPath = WriteAnchors(host, entry, geo, species, states, ladders,
                                                 probe.Convention, outputFolder);

            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // THE BAKE
        // =====================================================================================

        /// <summary>
        /// Bakes <c>Fish_&lt;species&gt;&lt;rung&gt;_&lt;state&gt;.png</c> for every species × state ×
        /// rung. Null lists mean "everything the rig declares": species from <c>ORDER</c>, states
        /// from <c>AORDER</c> (water anims, baked with the default waterZ so the depth tint is in
        /// the pixels) followed by <c>RESTS</c>.
        ///
        /// <paramref name="bands"/> maps a rig species key to the shipped def's weight range; a key
        /// it does not carry falls back to <c>SPECIES.range</c>. Pass null to bake the whole set on
        /// the rig's own ranges (what a bake with no project content would do).
        /// </summary>
        public static CatchPass2BakeResult BakeFish(IReadOnlyDictionary<string, FishWeightBand> bands = null,
                                                    IReadOnlyList<string> species = null,
                                                    IReadOnlyList<string> states = null,
                                                    string outputFolder = DefaultOutputFolder,
                                                    Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get(RigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new CatchPass2BakeResult
            {
                RigKey = RigKey, EngineName = host.EngineName, Geometry = geo,
            };

            // MEASURE the convention from pixels, then cross-check the declaration — the same
            // refusal every sibling baker makes. Measured on pass 2: head-mass offset +3.50 px at
            // the E row and −3.50 at W, mouth() agreeing, so Clockwise, as the catalog declares.
            var probe = FishingRigAzimuthProbe.MeasureFish(host, g, geo, Dirs);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;
            FishingKitBaker.RefuseOnMismatch(RigKey, entry.DeclaredConvention, probe.Convention,
                                             probe.Report);

            species ??= FishingKitBaker.ReadStringArray(host, $"{g}.ORDER");
            states ??= ReadStateOrder(host, g);

            // Validate the WHOLE recipe before writing anything — a mistyped species or state fails
            // with zero files on disk rather than a half-baked set. This is not ceremony: the rig
            // resolves an unknown species as `SPECIES[key] || SPECIES.mackerel` and an unknown rest
            // as nothing at all, so without this a typo bakes a mackerel under a cod's name.
            foreach (string s in species) AssertSpecies(host, g, s);
            foreach (string st in states) StateFrames(host, g, st);

            // Resolve every ladder up front too, for the same reason and so the plan can be logged.
            var ladders = new Dictionary<string, IReadOnlyList<FishRung>>(StringComparer.Ordinal);
            foreach (string s in species)
            {
                FishWeightBand? band = null;
                if (bands != null && bands.TryGetValue(s, out var b)) band = b;
                ladders[s] = LadderFor(host, g, s, band);
            }
            result.Ladders = ladders;

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = species.Count * states.Count * Rungs;
            foreach (string sp in species)
            foreach (var rung in ladders[sp])
            foreach (string st in states)
            {
                string name = $"Fish_{sp}{rung.Suffix}_{st}";
                progress?.Invoke(name, (float)done++ / plan);

                int frames = StateFrames(host, g, st);
                bool isRest = IsRest(host, g, st);

                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, name, Dirs, frames, geo,
                    (d, f) =>
                    {
                        double dir = RigBaker.DirForCell(d, Dirs, probe.Convention);
                        string poseKey = isRest ? "rest" : "anim";
                        string expr = $"{g}.render({Num(dir)},{{species:{FishingKitBaker.Js(sp)}," +
                                      $"{poseKey}:{FishingKitBaker.Js(st)},frame:{f}," +
                                      $"scale:{Num(rung.Scale)}}})";
                        byte[] rgba = FishingKitBaker.Render(host, expr, geo, renderClock, result);
                        RecordIfClipped(result, rgba, geo, sp, st, rung.Index, d, f);
                        return rgba;
                    }, result));
            }

            result.AnchorJsonPath = WriteAnchors(host, entry, geo, species, states, ladders,
                                                 probe.Convention, outputFolder);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Water anims (AORDER, in rig order) then dry rests (RESTS, in rig order).</summary>
        public static IReadOnlyList<string> ReadStateOrder(IRigScriptHost host, string g)
        {
            var all = new List<string>();
            all.AddRange(FishingKitBaker.ReadStringArray(host, $"{g}.AORDER"));
            all.AddRange(FishingKitBaker.ReadStringArray(host, $"{g}.RESTS"));
            return all;
        }

        /// <summary>Frame count of one state, from the rig's own tables: <c>ANIMS[s].n</c> for water
        /// anims, <c>RPOSE[s].length</c> for rests.</summary>
        public static int StateFrames(IRigScriptHost host, string g, string state)
        {
            if (host.EvaluateBool($"typeof {g}.ANIMS[{FishingKitBaker.Js(state)}] === 'object' && " +
                                  $"{g}.ANIMS[{FishingKitBaker.Js(state)}] !== null"))
                return Positive(state, (int)host.EvaluateNumber(
                    $"{g}.ANIMS[{FishingKitBaker.Js(state)}].n"));
            if (host.EvaluateBool($"Array.isArray({g}.RPOSE[{FishingKitBaker.Js(state)}])"))
                return Positive(state, (int)host.EvaluateNumber(
                    $"{g}.RPOSE[{FishingKitBaker.Js(state)}].length"));
            throw new ArgumentException(
                $"FishIso2 declares no state '{state}'. Known anims: " +
                string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.AORDER")) + "; rests: " +
                string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.RESTS")) + ". If the " +
                "state genuinely does not exist yet, that is an art-director rig change, not a " +
                "baker workaround.");
        }

        static bool IsRest(IRigScriptHost host, string g, string state) =>
            host.EvaluateBool($"Array.isArray({g}.RPOSE[{FishingKitBaker.Js(state)}])");

        /// <summary>
        /// ⚠️ The rig resolves an unknown species as <c>SPECIES[key] || SPECIES.mackerel</c> and
        /// renders it without a word of complaint, so a typo would bake a mackerel under a cod's
        /// name. Membership of <c>ORDER</c> is the only thing that rules that out.
        /// </summary>
        public static void AssertSpecies(IRigScriptHost host, string g, string species)
        {
            if (host.EvaluateBool(
                    $"typeof {g}.SPECIES[{FishingKitBaker.Js(species)}] === 'object' && " +
                    $"{g}.SPECIES[{FishingKitBaker.Js(species)}] !== null"))
                return;
            throw new ArgumentException(
                $"FishIso2 declares no species '{species}'. Known: " +
                string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.ORDER")) + ". The rig " +
                "would silently render a MACKEREL for this key rather than fail.");
        }

        // =====================================================================================
        // THE CLIP LEDGER
        // =====================================================================================

        /// <summary>Records a cell whose opaque pixels reach the edge of the cell — see the class
        /// remarks for why this is a ledger and not a refusal.</summary>
        static void RecordIfClipped(CatchPass2BakeResult result, byte[] rgba, in RigGeometry geo,
                                    string species, string state, int rung, int dir, int frame)
        {
            int w = geo.Width, h = geo.Height;
            bool left = false, right = false, top = false, bottom = false;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (rgba[(y * w + x) * 4 + 3] == 0) continue;
                if (x == 0) left = true;
                if (x == w - 1) right = true;
                if (y == 0) top = true;
                if (y == h - 1) bottom = true;
            }
            if (!(left || right || top || bottom)) return;

            var edges = new List<string>(4);
            if (left) edges.Add("left");
            if (right) edges.Add("right");
            if (top) edges.Add("top");
            if (bottom) edges.Add("bottom");
            result.Clipped.Add(new ClippedCell(species, state, rung, dir, frame,
                                               string.Join("+", edges)));
        }

        // =====================================================================================
        // THE ANCHORS
        // =====================================================================================

        static string WriteAnchors(IRigScriptHost host, in RigEntry entry, in RigGeometry geo,
                                   IReadOnlyList<string> species, IReadOnlyList<string> states,
                                   IReadOnlyDictionary<string, IReadOnlyList<FishRung>> ladders,
                                   AzimuthConvention convention, string outputFolder)
        {
            string g = entry.GlobalName;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"rig\": \"{entry.ScriptPath}\",\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {Num(geo.PivotX)}, \"y\": {Num(geo.PivotY)} }},\n");
            sb.Append($"  \"dirs\": {Dirs},\n");
            sb.Append($"  \"measuredRigConvention\": \"{convention}\",\n");
            sb.Append("  \"facingsAreCounterClockwise\": false,\n");
            sb.Append("  \"_note\": \"Baked in-engine with the rig's measured convention applied, so " +
                      "row d of every sheet depicts heading 360*d/dirs. mouth px are offsets FROM " +
                      "THE PIVOT (the water-surface point under the body centre), per direction row " +
                      "then per frame column, AT THAT RUNG'S SCALE. mouth is exported for the WATER " +
                      "anims only: the rest poses re-pivot the cell to the grip while the rig's " +
                      "mouth() stays in body space, so a rest-pose mouth would be wrong data.\",\n");
            sb.Append("  \"_ladder\": \"Each species is baked at three rungs. The middle rung keeps " +
                      "the legacy stem (Fish_<sp>_<state>.png); the outer two are _sm and _lg. Where " +
                      "a FishSpeciesDef names the species the rungs span ITS weight band, evenly " +
                      "spaced in rendered length; otherwise they are SPECIES.range min/1/max. Every " +
                      "rung records which, in bandSource.\",\n");
            sb.Append("  \"species\": {\n");

            for (int s = 0; s < species.Count; s++)
            {
                string sp = species[s];
                string hold = host.EvaluateString($"JSON.stringify({g}.hold({FishingKitBaker.Js(sp)},1))");
                sb.Append($"    \"{sp}\": {{\n");
                sb.Append($"      \"hold\": {hold},\n");

                sb.Append("      \"rungs\": [\n");
                var ladder = ladders[sp];
                for (int i = 0; i < ladder.Count; i++)
                {
                    FishRung r = ladder[i];
                    sb.Append($"        {{ \"index\": {r.Index}, \"suffix\": \"{r.Suffix}\", " +
                              $"\"scale\": {Num(Round(r.Scale, 6))}, \"kg\": {Num(Round(r.Kg, 3))}, " +
                              $"\"lengthM\": {Num(Round(r.LengthMetres, 4))}, " +
                              $"\"hands\": {r.Hands}, " +
                              $"\"bandSource\": \"{r.BandSource}\",\n");
                    sb.Append("          \"states\": {\n");
                    WriteRungStates(host, g, sp, states, r, convention, sb);
                    sb.Append("          }\n        }");
                    sb.Append(i < ladder.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("      ]\n");
                sb.Append(s < species.Count - 1 ? "    },\n" : "    }\n");
            }

            sb.Append("  }\n}\n");
            return FishingKitBaker.WriteJson(outputFolder, "FishIsoAnchors.json", sb.ToString());
        }

        /// <summary>
        /// One rung's water-anim block: frame count, frame time, and the mouth (line-attach)
        /// table AT THAT RUNG'S SCALE.
        ///
        /// <para><b>Why the mouth is per rung rather than scaled at run time.</b> The rig's
        /// <c>mouth()</c> builds <c>y = len*scale*0.78*stretch*0.5 + girth*scale*0.9</c>, projects it
        /// through the camera basis with the body's z base ADDED, and then rounds the result to
        /// whole pixels. The z term does not scale with the fish and the rounding is not
        /// recoverable, so the published offset is affine in <c>scale</c>, not proportional to it.
        /// Multiplying the middle rung's table by a scale ratio would be a guess dressed as
        /// arithmetic, and the line would leave the mouth by a pixel or two on every fish that is
        /// not mid-sized. Asking the rig once per rung costs nothing at bake time and is exact.</para>
        ///
        /// <para>Rest poses stay out, as they always have: they re-pivot the cell to the GRIP while
        /// <c>mouth()</c> stays in body space, so a rest-pose mouth would be wrong data rather than
        /// missing data.</para>
        /// </summary>
        static void WriteRungStates(IRigScriptHost host, string g, string sp,
                                    IReadOnlyList<string> states, in FishRung rung,
                                    AzimuthConvention convention, StringBuilder sb)
        {
            var waterStates = new List<string>();
            foreach (string st in states)
                if (!IsRest(host, g, st)) waterStates.Add(st);

            for (int a = 0; a < waterStates.Count; a++)
            {
                string st = waterStates[a];
                int frames = StateFrames(host, g, st);
                double ms = host.EvaluateNumber($"{g}.ANIMS[{FishingKitBaker.Js(st)}].ms");
                sb.Append($"            \"{st}\": {{ \"frames\": {frames}, \"ms\": {Num(ms)}, " +
                          "\"mouth\": [\n");
                for (int d = 0; d < Dirs; d++)
                {
                    double dir = RigBaker.DirForCell(d, Dirs, convention);
                    sb.Append("              [");
                    for (int f = 0; f < frames; f++)
                    {
                        sb.Append(host.EvaluateString(
                            $"JSON.stringify({g}.mouth({Num(dir)},{{species:{FishingKitBaker.Js(sp)}," +
                            $"anim:{FishingKitBaker.Js(st)},frame:{f},scale:{Num(rung.Scale)}}}))"));
                        if (f < frames - 1) sb.Append(", ");
                    }
                    sb.Append(d < Dirs - 1 ? "],\n" : "]\n");
                }
                sb.Append(a < waterStates.Count - 1 ? "            ] },\n" : "            ] }\n");
            }
        }

        // =====================================================================================
        // small shared bits
        // =====================================================================================

        static int Positive(string state, int frames) =>
            frames > 0 ? frames
                       : throw new InvalidOperationException($"State '{state}' declares {frames} frames.");

        static double Round(double v, int dp)
        {
            double f = Math.Pow(10, dp);
            return Math.Round(v * f) / f;
        }

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// <see cref="FishingBakeResult"/> plus what the ladder and the clip ledger add. Derives rather
    /// than duplicates so the existing bake reporting keeps working unchanged.
    /// </summary>
    public sealed class CatchPass2BakeResult : FishingBakeResult
    {
        /// <summary>The rungs baked, per species.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<FishRung>> Ladders =
            new Dictionary<string, IReadOnlyList<FishRung>>();

        /// <summary>Cells whose opaque pixels reach the cell edge — see
        /// <see cref="CatchPass2Baker"/>'s remarks.</summary>
        public readonly List<ClippedCell> Clipped = new List<ClippedCell>();
    }
}
