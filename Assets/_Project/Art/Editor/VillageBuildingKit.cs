#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The VILLAGE BUILDING KIT — what M1 needs built out of the two parametric building rigs, and the
    /// schema of the contract the bake writes beside the sheets.
    ///
    /// <para>The building rigs and <c>BuildingRigBaker</c> have existed since 2026-07-23 and bake
    /// correctly, but <b>nothing consumed their output</b>: no contract, no slicer, no placement path.
    /// This file is the first half of that consumer chain — the kit's identity — and it is the sibling
    /// of <see cref="TreeKitCatalog"/> in every structural respect. <see cref="VillageBuildingCatalog"/>
    /// is the placement half.</para>
    ///
    /// <para><b>Serializer == parser.</b> <c>VillageBuildingBakeMenu</c> writes <see cref="Contract"/>
    /// with <see cref="JsonUtility.ToJson(object,bool)"/> and <see cref="Load"/> reads it back with
    /// <see cref="JsonUtility.FromJson{T}"/>, so a bake and its consumers cannot drift apart. Every
    /// number in it comes from <c>BuildingBakeResult</c>, which read it from the rig — cell, crop,
    /// pivot, footprint metres and the measured facing convention are never restated here.</para>
    ///
    /// <para><b>⚠️ THE PIVOT IS DATA, NOT A CONSTANT — uniquely among this repo's kits.</b> Every other
    /// sheet pins its pivot with a named const (<c>DoryWaterline</c>, <c>PuntOrigin</c>) because the kit
    /// fixes the cell. Here <c>BuildingRigBaker</c> tight-crops to the union of the eight silhouettes,
    /// so the cell — and with it the pivot — depends on the build: a cannery crops differently from a
    /// shack. Baking a build and hard-coding its pivot in C# would be wrong the first time it was
    /// re-baked at a different size.</para>
    ///
    /// <para><b>⚠️ A building does NOT pivot bottom-centre, and the gap is METRES, not pixels.</b> The
    /// rig's pivot is the ground CENTRE (<c>{x:cx, y:groundY}</c> in both rigs) — the centre of the
    /// footprint, not its near edge. Under the ¾ camera the whole NEAR HALF of the footprint projects
    /// <i>below</i> that centre (a ground-plane point at <c>−y</c> lands at
    /// <c>groundY + y·sin40°·32</c>), and any porch, eave overhang and foundation that reach past the
    /// wall box add to it. Measured across the five M1 builds: <b>104–209 px, i.e. 3.3–6.5 m</b>.</para>
    ///
    /// <para><see cref="ArtImportPipeline"/> defaults anything under <c>/buildings/</c> to
    /// <see cref="SpriteAlignment.BottomCenter"/> — correct for the loose hand-drawn cottage sprites
    /// beside this kit, and here it would stand every building several metres into the dirt. Nothing
    /// throws; it reads as an art bug. <see cref="VillageBuildingSheetSlicer"/> overrides it per sheet
    /// from this contract, and <see cref="BelowGroundPad"/> is the number.</para>
    ///
    /// <para>The tree kit hits the same class of trap with its root flare, but for a different reason
    /// and at a hundredth of the scale (8–13 px): a tree's pivot is a chosen ROW just under the trunk
    /// foot, whereas a building's is a projected POINT in the middle of its footprint. That is also why
    /// the two kits normalise their pivots differently — see <see cref="NormalizedPivot"/>.</para>
    /// </summary>
    public static class VillageBuildingKit
    {
        /// <summary>
        /// The kit's own folder — a SUBFOLDER of the greybox building art rather than a sibling inside
        /// it, so this tool and the pre-existing loose <c>Buildings/*.png</c> (Cottage, LighthouseIso,
        /// the Nine Mile Creek houses) never see each other's sheets. The same separation
        /// <see cref="TreeKitCatalog.TreesRoot"/> keeps from the loose foliage drop.
        ///
        /// <para>It is also outside the reach of <c>BuildingBakeMenu</c>, whose 12-preset batch writes
        /// <c>HouseIso_*</c>/<c>WharfBuildingIso_*</c> into the parent folder. That batch is a
        /// worst-case bake exercise (it deliberately includes the cannery); this kit is what M1 ships.
        /// Keeping them in different folders means neither can be mistaken for the other's orphan.</para>
        /// </summary>
        public const string BuildingsRoot = "Assets/_Project/Art/Sprites/Buildings/Village/";

        public const string ContractFileName = "Buildings.json";

        public static string ContractPath => BuildingsRoot + ContractFileName;

        /// <summary>Sheet stem prefix. Every sheet this kit owns starts with it, so a stray file in the
        /// folder is recognisable as a stranger rather than guessed at.</summary>
        public const string StemPrefix = "Village_";

        /// <summary>
        /// The DEFAULT importer texture cap. Over it Unity imports SILENTLY DOWNSCALED and the sprite
        /// COUNT still comes out right, so only a cell-size or pivot assert catches it.
        ///
        /// <para>This kit deliberately does NOT lift the cap the way <c>SpriteSheetSlicer</c> does for
        /// the 3648 px hull sheets, because a 4096 texture is the first thing a later mobile port would
        /// have to undo (CLAUDE.md rule 7). Instead the BAKE is solved for this number: the request
        /// carries it as <c>BuildingBakeRequest.MaxSheetDimension</c> so
        /// <c>BuildingRigBaker.ChooseGrid</c> adds a row rather than emitting a sheet the import would
        /// shrink.</para>
        ///
        /// <para>⚠️ <b>That is not a precaution — it is a measured fix.</b> The first real bake of this
        /// set, solved against the baker's own 4096 limit, produced sheets 2800–3876 px wide: entirely
        /// legal for the baker, and every one of them would have imported downscaled while still
        /// reporting the right number of sprites. The cap a kit imports at and the cap its pack is
        /// solved for have to be the same number.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Facings every build bakes. 8 at 45° is the ADR-0006 recipe and what the rigs are
        /// drawn for; the union crop is what makes eight of them affordable.</summary>
        public const int Facings = 8;

        /// <summary>
        /// The cap a build packs and imports at — its own <see cref="Build.ImportCap"/> when it declares
        /// one, else the kit's <see cref="ImportSizeCap"/>. One function so the pack and the import can
        /// never be solved against different numbers.
        /// </summary>
        public static int ImportCapFor(Build build) =>
            build.ImportCap > 0 ? build.ImportCap : ImportSizeCap;

        /// <summary>The cap a CONTRACT ENTRY was baked at — the same resolution, read back off the bake
        /// rather than re-derived from the table, so the slicer honours what was actually packed.</summary>
        public static int ImportCapFor(Entry entry) =>
            entry != null && entry.importCap > 0 ? entry.importCap : ImportSizeCap;

        public static string SheetPath(string buildKey) => BuildingsRoot + StemFor(buildKey) + ".png";

        /// <summary>Sheet stem for a build key, e.g. <c>Village_school</c>.</summary>
        public static string StemFor(string buildKey) => StemPrefix + buildKey;

        /// <summary>The sprite name for one facing of one build, e.g. <c>Village_school_d4</c>.</summary>
        public static string SpriteNameFor(string buildKey, int facing) =>
            $"{StemFor(buildKey)}_d{facing}";

        // =================================================================================
        // WHAT M1 NEEDS — the one hand-written table in the chain
        // =================================================================================

        /// <summary>
        /// One build the kit bakes: which rig, and the options to hand <c>render()</c>.
        ///
        /// <para>A build is either one of the rig's own PRESETS (<see cref="Preset"/> set) or a DIALLED
        /// set of axes (<see cref="Dialled"/> set) — never both. The distinction matters to the baker:
        /// a preset gets the "did the preset actually apply?" tripwire against the rig's PRESETS table,
        /// a dialled build gets the same byte-comparison against the rig default.</para>
        /// </summary>
        public readonly struct Build
        {
            /// <summary>Stable key — the sheet stem, the prefab name and the contract key. Append-only.</summary>
            public readonly string Key;

            /// <summary>What the owner sees in a dropdown or the scene hierarchy.</summary>
            public readonly string Label;

            /// <summary>Catalog rig key — <c>"house"</c> or <c>"wharfBuilding"</c>.</summary>
            public readonly string RigKey;

            /// <summary>The rig's own preset name, or null for a dialled build.</summary>
            public readonly string Preset;

            /// <summary>The dialled axes, or null for a preset build. Keys are <c>BuildingAxes</c>
            /// keys — the rig's real option spelling, grep-verified against the rig source by
            /// <c>BuildingAxesTests</c>, which is what keeps a silently-ignored key out of here.</summary>
            public readonly IReadOnlyDictionary<string, object> Dialled;

            /// <summary>Why this build is in the set, in one line — read by nobody and worth
            /// keeping anyway, since "which of these is the school" is the question a future reader
            /// arrives with.</summary>
            public readonly string Why;

            /// <summary>
            /// Construction phase and dereliction, LAYERED ON TOP of the preset or the dialled axes —
            /// see <see cref="LifecycleSet"/>. Empty/false for every ordinary building.
            ///
            /// <para><b>⭐ Why this is its own field rather than three more dialled keys.</b> A
            /// derelict shed is <i>the same building</i> as the sound one, in a different state — so
            /// the state has to be able to ride ON a preset without dissolving it into a hand-copied
            /// set of axes. Transcribing <c>PRESETS['redShed']</c> into C# to bolt a <c>decay</c> key
            /// onto it would fork the rig's own table, and the fork would go stale in silence the
            /// first time the art director touched a preset. Keeping it separate lets the options
            /// expression stay <c>Object.assign({}, Rig.PRESETS['redShed'], {decay:'neglected'})</c>
            /// — the rig's table, plus the layer.</para>
            /// </summary>
            public readonly string Phase, Decay;

            public readonly bool Burnt;

            /// <summary>
            /// This build's own import cap, or 0 for the kit's <see cref="ImportSizeCap"/>.
            ///
            /// <para><b>⚠️ A per-build exception, and it exists for exactly one building.</b> The kit's
            /// 2048 is a discipline, not a limitation, and lifting it kit-wide would silently allow
            /// every future build to double — see <see cref="ImportSizeCap"/> for why that number is
            /// what it is. But the cannery is 9.5 × 15.4 m, and eight facings of it do not pack under
            /// 2048 <b>in any state, including sound</b>: its cropped cell measures 840 × 673, the best
            /// grid is 2 × 4, and that is 2692 px tall. Measured, not estimated.</para>
            ///
            /// <para>The cap a build IMPORTS at and the cap its pack is SOLVED for must be the same
            /// number or the two agree only by luck, so this one value drives both.</para>
            /// </summary>
            public readonly int ImportCap;

            Build(string key, string label, string rigKey, string preset,
                  IReadOnlyDictionary<string, object> dialled, string why,
                  string phase = null, string decay = null, bool burnt = false, int importCap = 0)
            {
                Key = key; Label = label; RigKey = rigKey; Preset = preset;
                Dialled = dialled; Why = why;
                Phase = phase; Decay = decay; Burnt = burnt; ImportCap = importCap;
            }

            public static Build FromPreset(string key, string label, string rigKey, string preset,
                                           string why)
                => new Build(key, label, rigKey, preset, null, why);

            public static Build FromDialled(string key, string label, string rigKey,
                                            Dictionary<string, object> dialled, string why)
                => new Build(key, label, rigKey, null, dialled, why);

            /// <summary>
            /// One of the rig's own presets, at a point in its life. The state ids are checked here,
            /// at construction, so a typo is a static-init failure with a list of the legal values in
            /// it rather than a sheet of the wrong building (BuildingLifecycleStates.AssertKnown has
            /// the measurement behind that).
            /// </summary>
            public static Build FromPresetInState(string key, string label, string rigKey,
                                                  string preset, string phase, string decay,
                                                  bool burnt, string why, int importCap = 0)
            {
                BuildingLifecycleStates.AssertKnown(phase, decay);
                if (!BuildingLifecycleStates.IsActive(phase, decay, burnt))
                    throw new ArgumentException(
                        $"'{key}' declares no lifecycle state, so it is an ordinary preset build — " +
                        "use Build.FromPreset. A build in the lifecycle set that resolves to " +
                        "finished/sound would bake a pristine building under a derelict's name.",
                        nameof(key));

                return new Build(key, label, rigKey, preset, null, why, phase, decay, burnt, importCap);
            }

            public bool IsPreset => Preset != null;

            /// <summary>Does this build ask the lifecycle pass to do anything?</summary>
            public bool HasLifecycle => BuildingLifecycleStates.IsActive(Phase, Decay, Burnt);

            /// <summary>The state as a short tag — <c>"neglected"</c>, <c>"frame+abandoned"</c> — or
            /// <c>"finished"</c>. What a log line and a contract entry carry.</summary>
            public string StateTag => BuildingLifecycleStates.Tag(Phase, Decay, Burnt);
        }

        /// <summary>
        /// The M1 building set: <b>three clapboard houses, a one-room school and a general store</b> —
        /// exactly the village <c>docs/design/world-and-regions.md</c> §6 gives St Peters, and the five
        /// entries <c>docs/art/asset-manifest.md</c> lists as the P1 "slice gap". Nothing else: the
        /// wharf rig's sheds and the cannery are M2 work and are baked (uncommitted) by
        /// <c>BuildingBakeMenu</c>'s worst-case batch instead.
        ///
        /// <para><b>Why two of the houses are presets and three builds are dialled.</b> The house rig
        /// ships five presets, but only <c>whiteFarmhouse</c> and <c>redSaltbox</c> are clapboard —
        /// <c>shingleCottage</c> and <c>dormerCape</c> are shingle and <c>gothicRevival</c> is a
        /// two-tone show house. The canon asks for clapboard, so the third house is dialled rather
        /// than substituted, and the school and the store have no preset at all. The rig source is not
        /// edited to add them (ADR 0021 §5: his file is what runs) — <c>BuildingBakeRequest.FromOptions</c>
        /// exists for exactly this.</para>
        ///
        /// <para><b>⚠️ These option values are the one hand-written thing in the chain, and unknown
        /// keys fail SILENTLY.</b> Both rigs resolve options as <c>opts[k] != null ? opts[k] :
        /// fallback</c>, so a misspelled key renders something else with no error — the recorded
        /// worked example is <c>winD</c> (the rig's internal field) versus <c>winDensity</c> (the
        /// option). Every key below is therefore a <c>BuildingAxes</c> key, and <c>BuildingAxesTests</c>
        /// already greps every one of those out of the rig source. <c>VillageBuildingSetTests</c> then
        /// asserts this table uses nothing else.</para>
        /// </summary>
        public static readonly Build[] M1Set =
        {
            // ---- the one-room school: the opening's teaching anchor -----------------------------
            // Small enough to read as ONE room (size 0.15 → 6.4 × 7.6 m), white clapboard, and mostly
            // windows — a schoolroom is lit from the sides. One chimney for the stove, no dormers.
            // The rig has no belfry axis, so it does not pretend to one.
            //
            // ⚠️ THE PORCH IS LOAD-BEARING HERE, NOT DECORATION — see <see cref="GableDoorAxes"/>.
            // porch 'none' routes this rig's door to the +X EAVE wall while anchors() goes on
            // claiming the +Y gable centre, and a room registers its doorway to that anchor. With no
            // porch the walk-in gap lands on blank gable clapboard and the drawn door is solid wall.
            Build.FromDialled("school", "One-room school", "house", new Dictionary<string, object>
            {
                ["era"] = "colonial",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "white",
                ["roof"] = "asphaltGrey",
                ["size"] = 0.15,
                ["windows"] = "sixOverSix",
                ["winDensity"] = 0.85,
                ["attic"] = "gable",
                // ⭐ was 'none'. A schoolhouse step out of the weather — and the axis that puts the
                // door on the gable its room opens onto.
                ["porch"] = "front",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = false,
                ["weather"] = 0.30,
            }, "world-and-regions §6: the school where the aunt teaches the compass and hand skills"),

            // ---- the general store: basic gear, the clam licence, gas in a can -------------------
            // Bigger than a house room (size 0.45 → 7.1 × 8.9 m), a FRONT porch to shelter the step,
            // and a bay — which is the rig's shop window. Metal roof and cream paint so it reads as
            // the one commercial building in a row of houses.
            Build.FromDialled("generalStore", "General store", "house", new Dictionary<string, object>
            {
                ["era"] = "colonial",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "cream",
                ["roof"] = "metal",
                ["size"] = 0.45,
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.70,
                ["attic"] = "gable",
                ["porch"] = "front",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = true,
                ["weather"] = 0.35,
            }, "world-and-regions §6: the general store for basic gear; fuel-and-refuelling §3 sells " +
               "gas in a can over its counter"),

            // ---- the three clapboard houses -----------------------------------------------------
            //
            // ⚠️ THE FARMHOUSE AND THE SALTBOX WERE PRESET BUILDS AND ARE NOW DIALLED. Neither preset
            // can be entered: `redSaltbox` carries porch 'none' (→ eave door) and `whiteFarmhouse`
            // carries shape 'ell' (→ a door on the forward wing, 1.21 m across and 4.17 m beyond the
            // footprint the room occupies). Both are dialled from the preset's own values — proved
            // byte-identical to the preset spread at all eight facings before a single axis moved —
            // so the ONLY differences from the shipped art are the ones called out below.

            // The biggest of the three. It KEEPS its wrap porch, metal roof, dormer and size (7.7 ×
            // 9.9 m of footprint, so its site and clearances are untouched); it loses the ell wing
            // and the bay window.
            //
            // Why both: an `ell` can never take a gable door (`gableDoor = hasPorch && shape!=='ell'`),
            // and a bay silently KILLS the porch (`bayFrontOn = !!bayKind && shape!=='ell'`, and
            // `hasPorch` requires `!bayFrontOn`) — so with the ell dropped, leaving `bay:true` would
            // have put the door straight back on the eave. Measured, not reasoned.
            Build.FromDialled("whiteFarmhouse", "White farmhouse", "house", new Dictionary<string, object>
            {
                ["era"] = "colonial",
                ["shape"] = "gable",          // ⭐ was 'ell' — an ell cannot take a gable door
                ["siding"] = "clapboard",
                ["body"] = "white",
                ["roof"] = "metal",
                ["size"] = 0.7,               // unchanged: the footprint must not move
                ["windows"] = "sixOverSix",
                ["winDensity"] = 0.60,
                ["attic"] = "gable",
                ["porch"] = "wrap",
                ["dormers"] = 1,
                ["chimneys"] = 1,
                ["bay"] = false,              // ⭐ was true — a bay cancels the porch, and the door with it
                ["weather"] = 0.10,
            }, "the biggest of the three — a wrap porch and a dormer, the one with a family in it"),

            // The plainest. Red clapboard and the saltbox's long north roof are both kept; it gains
            // the front step it needs to be a house you can walk into.
            Build.FromDialled("redSaltbox", "Red saltbox", "house", new Dictionary<string, object>
            {
                ["era"] = "modern",
                ["shape"] = "saltbox",
                ["siding"] = "clapboard",
                ["body"] = "red",
                ["roof"] = "asphaltGrey",
                ["size"] = 0.4,               // unchanged: the footprint must not move
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.60,
                ["attic"] = "none",
                ["porch"] = "front",          // ⭐ was 'none' — which put the door on the eave wall
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = false,
                ["weather"] = 0.20,
            }, "the plainest — red clapboard and the long north roof of a saltbox"),

            // The third house has no clapboard preset to take, so it is dialled: the smallest and the
            // most weathered of the three, so the row reads as houses of different ages.
            Build.FromDialled("sageCottage", "Sage cottage", "house", new Dictionary<string, object>
            {
                ["era"] = "plain",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "sage",
                ["roof"] = "asphaltBrown",
                ["size"] = 0.25,
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.50,
                ["attic"] = "gable",
                ["porch"] = "front",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = false,
                ["weather"] = 0.55,
            }, "the third clapboard house the canon asks for; no rig preset is clapboard AND small"),
        };

        // =================================================================================
        //  THE LIFECYCLE SET — the same rigs, at a different point in their lives
        // =================================================================================

        /// <summary>
        /// Buildings that are NOT in repair: the three derelict outbuildings on Aunt Ginny's woods plot,
        /// and the St Peters cannery that has been shut since the business went to the mainland.
        ///
        /// <para><b>⭐ WHY THIS IS A SECOND TABLE AND NOT FOUR MORE ROWS OF <see cref="M1Set"/>.</b>
        /// The M1 set has an identity that a dozen tests read it for — five clapboard-and-white village
        /// buildings, every one of them a house-rig build a room can be baked inside and a door drawn on
        /// the gable of. None of that is true here: these are wharf-rig sheds and a processing plant,
        /// none of them enterable, and their whole point is that they are broken. Folding them in would
        /// have meant loosening every one of those assertions for the four rows that are meant to be
        /// exceptions. Kept apart, the M1 invariants stay exactly as strict as they were and these get
        /// their own, in <c>BuildingLifecycleSetTests</c>.</para>
        ///
        /// <para><b>They ride the rig's OWN presets.</b> Each is <c>PRESETS['x']</c> plus a decay key —
        /// see <see cref="Build.Phase"/> for why the state is a field rather than three more dialled
        /// axes. Nothing about the shed is retyped into C#, so an art-director change to <c>redShed</c>
        /// reaches the derelict one too.</para>
        ///
        /// <para><b>⚠️ THE STATE IS ART, NOT GAMEPLAY, AND THAT IS DELIBERATE (CLAUDE.md rule 8).</b>
        /// A phase ladder that the player walks a building UP (buy a lot, build on it) and a repair loop
        /// that walks one back from <c>ruin</c> are the owner's stated goals, and they are M3 — logged
        /// in <c>backlog/</c>, not built here. This table gives the world its history; it grants no
        /// mechanic for changing it.</para>
        ///
        /// <para><b>The decay each one is in was NOT chosen by eye</b> — it is the state
        /// <c>StPetersGinnyPlot</c>'s own shed table already says in prose. The woodshed is "the
        /// one still standing squarest, because a woodshed is the one you keep the roof on"; the net
        /// store is "the furthest gone"; the lean-to has "its back broken". Those three sentences are
        /// <c>neglected</c>, <c>ruin</c> and <c>collapsing</c>. The art now says what the world already
        /// said.</para>
        /// </summary>
        public static readonly Build[] LifecycleSet =
        {
            // ---- Aunt Ginny's three outbuildings (St Peters, the woods plot) --------------------
            //
            // ⚠️ These REPLACE greybox markers, and the footprint therefore GROWS. The old rows were
            // hand-sized at 2.6-3.4 m across; the wharf rig cannot draw a building smaller than
            // 3.60 x 4.50 m (measured across its whole size axis, from 0.0 to 1.0), so the shed table's
            // sizes now come from the contract instead of from those numbers. StPetersVillageTests
            // re-derives the clearing from the contract and fails with the figure to use, which is what
            // makes that safe rather than hopeful.

            Build.FromPresetInState(
                "ginnyWoodshed", "Ginny's woodshed (neglected)", "wharfBuilding", "redShed",
                phase: null, decay: "neglected", burnt: false,
                why: "behind her cottage on the north side - the one still standing squarest, because " +
                     "a woodshed is the one you keep the roof on. Red board-and-batten so the three " +
                     "read as three different buildings and not one shed copied about."),

            Build.FromPresetInState(
                "ginnyNetStore", "Ginny's net store (ruin)", "wharfBuilding", "netShed",
                phase: null, decay: "ruin", burnt: false,
                why: "east, the furthest out - a net store from when this land was worked, and the " +
                     "furthest gone. The netShed preset by name as well as by shape."),

            Build.FromPresetInState(
                "ginnyLeanTo", "Ginny's lean-to (collapsing)", "wharfBuilding", "tealShack",
                phase: null, decay: "collapsing", burnt: false,
                why: "north-west, first thing you pass walking up - a lean-to with its back broken, " +
                     "which is what `collapsing` draws: a roof slope stove in, rafters showing."),

            // ---- the cannery (St Peters, the west shore) ----------------------------------------
            //
            // The biggest build the wharf rig has (9.5 x 15.4 m) and the biggest employer St Peters
            // ever had. `collapsing` rather than `ruin` on purpose: a ruin is a heap you read as
            // scenery, and this building is meant to read as a thing that could be brought back. At
            // collapsing it still has walls, a name and a roofline - and one slope stove in.
            Build.FromPresetInState(
                "stPetersCannery", "St Peters cannery (collapsing)", "wharfBuilding", "cannery",
                phase: null, decay: "collapsing", burnt: false,
                why: "the fish cannery that shut when the business went to the mainland - the long-arc " +
                     "goal the owner named on 2026-08-19: get it running again and employ the village. " +
                     "Scenery in this PR; the restart is an economy-sim arc in backlog/ (M3).",

                // ⚠️ THE ONE 4096 SHEET IN THIS KIT, AND THE COST IS STATED RATHER THAN HIDDEN.
                // Measured in the standalone V8 harness across all five decay states plus fishPlant:
                //
                //   state        cropped cell   @2048           @4096
                //   sound        688 x 664      2x4 REFUSED     5x2 = 3440x1328   17 MB
                //   neglected    790 x 694      2x4 REFUSED     5x2 = 3950x1388   20 MB
                //   abandoned    832 x 707      2x4 REFUSED     4x2 = 3328x1414   17 MB
                //   collapsing   840 x 673      2x4 REFUSED     4x2 = 3360x1346   17 MB   <- this build
                //   ruin         864 x 612      2x4 REFUSED     4x2 = 3456x1224   16 MB
                //
                // ⭐ NOTE THE FIRST ROW. This is NOT the dereliction blowing the cap - the cannery has
                // never fitted a 2048-capped 8-facing sheet, in any state. That is why M1Set never
                // carried it and why BuildingBakeMenu's worst-case batch bakes it uncommitted at 4096.
                // The lifecycle pass only made someone finally need it committed.
                //
                // The owner's lever, if 17 MB is too much for one building: this kit's facing count is
                // documented as halvable (a re-bake, not a code change - see StPetersVillage.FacingToward,
                // "with four facings the doors land on the nearest quarter-turn"). Four facings of this
                // cell packs 2x2 = 1680x1346 and fits under 2048 at 9 MB. It is not taken here because
                // the cannery's door is aimed at the pier and a quarter-turn is up to 45 deg of error on
                // the island's biggest silhouette. If a mobile port ever becomes real, this is the FIRST
                // sheet to re-solve.
                importCap: 4096),
        };

        /// <summary>
        /// Every build this kit bakes — <see cref="M1Set"/> then <see cref="LifecycleSet"/>, in that
        /// order, which is the order the contract is written in.
        ///
        /// <para>The bake, the contract and the import test walk THIS; the per-build village invariants
        /// walk <see cref="M1Set"/> alone. Two different questions: "did everything the kit declares get
        /// baked?" and "is the M1 village still five clapboard buildings you can walk into?"</para>
        /// </summary>
        public static Build[] AllBuilds
        {
            get
            {
                var all = new Build[M1Set.Length + LifecycleSet.Length];
                M1Set.CopyTo(all, 0);
                LifecycleSet.CopyTo(all, M1Set.Length);
                return all;
            }
        }

        // =================================================================================
        //  ⚠️ CAN THIS BUILDING BE ENTERED AT ALL? — the axes that decide where the door DRAWS
        // =================================================================================

        /// <summary>
        /// <b>🔴 <c>houseIsoRig.anchors()</c> DOES NOT REPORT WHERE THE DOOR IS DRAWN.</b> It returns
        /// <c>door: pj(0, Ln/2, fH+1)</c> — the <c>+Y</c> gable centre — for <i>every</i> shape and
        /// every porch, unconditionally (<c>houseIsoRig.js:898</c>). Where the door actually goes is
        /// decided three hundred lines earlier by axes the anchor never consults:
        ///
        /// <code>
        ///   :747  bayFrontOn = !!bayKind &amp;&amp; shape !== 'ell'
        ///   :748  hasPorch   = (porch === 'front' || porch === 'wrap') &amp;&amp; !bayFrontOn &amp;&amp; !isCape
        ///   :750  eaveDoor   = isCape || (!hasPorch &amp;&amp; shape !== 'ell')   → door on the +X EAVE wall
        ///   :772  gableDoor  = hasPorch &amp;&amp; shape !== 'ell'               → door on the +Y GABLE centre
        ///   :739  shape 'ell'                                            → door on the forward WING
        /// </code>
        ///
        /// <para><b>Why this matters more than it looks.</b> A room's doorway registers to the ANCHOR
        /// (<c>InteriorKit.InteriorFacingFor</c> lines the two door anchors up, and
        /// <c>InteriorFootprint</c> cuts the gap in the wall there). So when the anchor and the drawn
        /// door disagree, the gap you can walk through is somewhere other than the door you can see —
        /// and BOTH draw perfectly. Measured on this set 2026-08-12: the school's and the saltbox's
        /// doors were 90° away on the eave wall, and the farmhouse's was on its ell wing, 1.21 m
        /// across and 4.17 m beyond the footprint its room occupies.</para>
        ///
        /// <para><b>It also silently defeated the facing pass.</b> <c>BuildingFacing</c> reasons that
        /// "a wrong sign would have to be a wrong sign in the baked pixels" — true of the cell
        /// ORDER, which it measures, but not of WHICH WALL, which it takes from the same declared
        /// anchor. So <c>StPetersVillage.FacingToward</c> has been turning the school's and the
        /// saltbox's blank gable toward the green while their real doors face 90° away. Left alone
        /// deliberately (re-facing is a visible change to banked buildings and wants its own drop);
        /// recorded here so the next reader does not re-derive it.</para>
        ///
        /// <para><b>The rule this leaves.</b> Any build the interior kit bakes a room for must draw
        /// its door on the gable — porch <c>front</c>/<c>wrap</c>, shape neither <c>ell</c> nor
        /// <c>cape</c>, and no bay (a bay cancels the porch via <c>bayFrontOn</c>, taking the door
        /// with it). <c>VillageBuildingSetTests</c> enforces it and greps the rig's own routing lines
        /// so the predicate below cannot drift away from them in silence.</para>
        /// </summary>
        public static bool DrawsDoorOnGable(Build build, out string why)
        {
            if (build.IsPreset)
            {
                why = $"'{build.Key}' is a PRESET build, so its porch/shape/bay live in the rig's " +
                      "own table and cannot be read here. Dial it instead — a build that a room " +
                      "stands inside has to be checkable from the kit.";
                return false;
            }

            string porch = Value(build, "porch") as string ?? "none";
            string shape = Value(build, "shape") as string ?? "gable";
            object bay = Value(build, "bay");

            // A bay resolves to a kind for `true` as well as for the two named kinds, and any kind
            // sets bayFrontOn (given shape != 'ell'), which cancels hasPorch — and the door with it.
            bool hasBay = bay is bool b ? b : bay is string s && s.Length > 0 && s != "none";

            if (shape == "ell")
            {
                why = $"'{build.Key}' is shape 'ell': the rig draws its door on the forward WING " +
                      "(houseIsoRig.js:739), which stands outside the footprint the room occupies.";
                return false;
            }
            if (shape == "cape")
            {
                why = $"'{build.Key}' is shape 'cape': the rig always routes a cape's entry to the " +
                      "long +X eave face (houseIsoRig.js:750, `isCape ||`), whatever the porch says.";
                return false;
            }
            if (porch != "front" && porch != "wrap")
            {
                why = $"'{build.Key}' has porch '{porch}': with no porch the rig routes the door to " +
                      "the +X EAVE wall (houseIsoRig.js:750), 90° from the gable its room opens onto.";
                return false;
            }
            if (hasBay)
            {
                why = $"'{build.Key}' has a bay: bayFrontOn cancels hasPorch (houseIsoRig.js:747-748), " +
                      "so the porch stops carrying the door and it falls back to the eave wall.";
                return false;
            }

            why = $"'{build.Key}': porch '{porch}', shape '{shape}', no bay → gableDoor, so the drawn " +
                  "door sits on the +Y gable centre where anchors() claims it and where a room's " +
                  "doorway is cut.";
            return true;
        }

        static object Value(Build build, string key) =>
            build.Dialled != null && build.Dialled.TryGetValue(key, out object v) ? v : null;

        /// <summary>The build with this key, or null. Searches the WHOLE kit — M1 and lifecycle.</summary>
        public static Build? FindBuild(string key)
        {
            foreach (var b in AllBuilds)
                if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        // =================================================================================
        // the contract schema
        // =================================================================================

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string writtenBy;

            /// <summary>Pixels per metre the sheets were baked at — the rigs' own <c>PX</c>, not the
            /// import default, so the two cannot disagree in silence.</summary>
            public int ppu;

            public int facings;
            public string conventionNote;
            public string pivotNote;

            public Entry[] buildings;
        }

        [Serializable]
        public sealed class Entry
        {
            /// <summary>The <see cref="Build.Key"/> this entry was baked from.</summary>
            public string key;

            public string label;

            /// <summary>Catalog rig key, its source file and the global it installs — recorded so a
            /// re-bake against a changed rig is visible in the diff.</summary>
            public string rig;
            public string rigScript;
            public string rigGlobal;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            /// <summary>The rig preset this came from, or empty for a dialled build.</summary>
            public string preset;

            public bool fromPreset;

            /// <summary>The EXACT options expression <c>render()</c> was handed. The audit trail: if a
            /// build ever looks wrong, this says what it was actually asked to draw.</summary>
            public string optionsJs;

            /// <summary>
            /// The lifecycle state this sheet was baked in — empty/false for a building in repair.
            /// <c>state</c> is the short tag (<c>"neglected"</c>, <c>"frame+abandoned"</c>,
            /// <c>"finished"</c>).
            ///
            /// <para>Recorded because it is the ONE fact about a lifecycle sheet that the pixels
            /// cannot be asked for after the fact, and the one a placement wants: "is this thing
            /// derelict, and how far gone?" is a question the world asks (a ruin has no lit windows
            /// and no smoke), and reading it back off the contract beats re-deriving it from the
            /// build key by string surgery.</para>
            /// </summary>
            public string phase;
            public string decay;
            public bool burnt;
            public string state;

            /// <summary>The texture cap this sheet was PACKED against, and must be IMPORTED at. 0 (or a
            /// contract written before this field existed) means the kit default — see
            /// <see cref="ImportCapFor(Entry)"/>.</summary>
            public int importCap;

            public int facings;
            public int cols;
            public int rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW;
            public int cellH;

            /// <summary>The rig's native cell, before the union crop.</summary>
            public int nativeCellW;
            public int nativeCellH;

            /// <summary>Crop origin in the native cell (top-left space) — what the pivot shifted by.</summary>
            public int cropX;
            public int cropY;

            /// <summary>The GROUND CENTRE, in cropped-cell px from the cell's TOP-LEFT.</summary>
            public float pivotX;
            public float pivotY;

            /// <summary>The Unity sprite pivot: normalised, BOTTOM-origin. Equals
            /// <c>(pivotX / cellW, (cellH − pivotY) / cellH)</c> — see
            /// <see cref="NormalizedPivot"/>.</summary>
            public float unityPivotX;
            public float unityPivotY;

            public int sheetW;
            public int sheetH;

            /// <summary>The building's true footprint in metres, as the RIG reports it (and as the
            /// azimuth probe cross-checked against the rendered silhouette). The honest-scale number:
            /// at PPU 32 the drawn thing has to be this wide.</summary>
            public float footprintWidthMetres;
            public float footprintLengthMetres;

            /// <summary>The MEASURED facing convention. The correction is already applied to the
            /// sheet, so cell <c>i</c> genuinely depicts +45°·i.</summary>
            public string convention;

            /// <summary>Per-facing door anchor in cropped-cell px, top-left origin — <c>doorX[i]</c>
            /// pairs with <c>doorY[i]</c>. Two parallel arrays rather than a struct array because
            /// <see cref="JsonUtility"/> will not round-trip a nested array of objects.
            ///
            /// <para>Used for one thing today: <see cref="FrontFacing"/> derives which cell shows the
            /// door instead of anybody declaring it.</para></summary>
            public float[] doorX;
            public float[] doorY;

            public long pngBytes;
            public long runtimeBytesRgba32;

            /// <summary>Fraction of the native cell area the union crop removed.</summary>
            public float cropSaving;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>The committed contract, or null with a logged error if it is missing/unparseable.</summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[VillageBuildingKit] No contract at '{ContractPath}'. Run " +
                    "Hidden Harbours ▸ Art ▸ Bake Village Buildings — the sheets and this file are " +
                    "written by the same bake and are only meaningful together.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.buildings == null || contract.buildings.Length == 0)
            {
                Debug.LogError($"[VillageBuildingKit] '{ContractPath}' parsed but carries no buildings.");
                return null;
            }
            return contract;
        }

        /// <summary>One build's entry, or null if the bake did not cover it.</summary>
        public static Entry Find(Contract contract, string key)
        {
            if (contract?.buildings == null) return null;
            foreach (var e in contract.buildings)
                if (string.Equals(e.key, key, StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>The entry a sheet stem belongs to, or null for a stranger (which must fail, not
        /// be guessed at).</summary>
        public static Entry EntryForStem(Contract contract, string stem)
        {
            if (contract?.buildings == null) return null;
            foreach (var e in contract.buildings)
                if (string.Equals(stem, StemFor(e.key), StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>Every sheet path the contract claims, in bake order.</summary>
        public static string[] AllSheetPaths(Contract contract)
        {
            if (contract?.buildings == null) return Array.Empty<string>();
            var paths = new string[contract.buildings.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = SheetPath(contract.buildings[i].key);
            return paths;
        }

        /// <summary>
        /// The Unity sprite pivot for an entry: normalised, BOTTOM-origin, from the GROUND CENTRE.
        ///
        /// <para>⚠️ The y term is <c>(cellH − pivotY)/cellH</c> — the hull/prop convention
        /// (<c>RigGeometry.UnityNormalisedPivot</c>), <b>not</b> the tree kit's <c>pad/cellH</c>. That
        /// difference is settled and MEASURED (ADR 0026 + <c>RigPivotConventionProbe</c>): a rig's
        /// <c>pivot</c> is a CONTINUOUS point whose origin is the cell's top-left corner, and a
        /// building's ground centre is a projected POINT like a hull's, not a chosen ROW like a tree's
        /// trunk foot. Do not unify them.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(Entry e) =>
            new Vector2(e.pivotX / e.cellW, (e.cellH - e.pivotY) / e.cellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left, which is what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(Entry e) =>
            new Vector2(e.pivotX, e.cellH - e.pivotY);

        /// <summary>
        /// How many rows of art sit BELOW the ground centre: the near half of the footprint as the ¾
        /// camera projects it, plus whatever porch, eave and foundation reach past the wall box.
        ///
        /// <para><b>This is exactly the distance a bottom-centre pivot would sink this building into the
        /// ground</b> — 104–209 px (3.3–6.5 m) across the five M1 builds, which is why the slicer
        /// overrides <see cref="ArtImportPipeline"/>'s <c>/buildings/</c> default rather than trusting
        /// it.</para>
        /// </summary>
        public static int BelowGroundPad(Entry e) => Mathf.Max(0, e.cellH - Mathf.RoundToInt(e.pivotY));

        /// <summary>
        /// Which facing shows the DOOR — measured, not declared: the cell whose door anchor sits
        /// LOWEST on screen (largest y in the rigs' top-left space) is the one facing the camera.
        ///
        /// <para>Both rigs put the main door on the <c>+Y</c> gable and <c>+Y</c> projects away from
        /// the camera, so cell 0 is the building's BACK. Deriving the front from the baked anchors
        /// rather than reasoning it out is the difference between a measurement and the kind of
        /// declaration that has been wrong five times in this repo.</para>
        ///
        /// <para>Falls back to 0 when the contract predates the door anchors, so a stale contract
        /// gives a back view rather than an exception.</para>
        /// </summary>
        public static int FrontFacing(Entry e)
        {
            if (e?.doorY == null || e.doorY.Length == 0) return 0;
            int best = 0;
            for (int i = 1; i < e.doorY.Length; i++)
                if (e.doorY[i] > e.doorY[best]) best = i;
            return best;
        }

        /// <summary>Read the sprite mesh type an importer will use — it lives on
        /// <see cref="TextureImporterSettings"/>, not on the importer itself, which is an easy thing
        /// to look for in the wrong place.</summary>
        public static SpriteMeshType MeshTypeOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.spriteMeshType;
        }

        /// <summary>sRGB also lives on <see cref="TextureImporterSettings"/> — same indirection.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        /// <summary>Total runtime texture cost of the kit, in MiB of RGBA32 — reported rather than
        /// presumed, so the atlas question is answered with a number.</summary>
        public static float TotalRuntimeMib(Contract contract)
        {
            if (contract?.buildings == null) return 0f;
            long bytes = 0;
            foreach (var e in contract.buildings) bytes += e.runtimeBytesRgba32;
            return bytes / 1024f / 1024f;
        }

        /// <summary>Total committed PNG size of the kit, in MiB — what the repo actually carries.</summary>
        public static float TotalPngMib(Contract contract)
        {
            if (contract?.buildings == null) return 0f;
            long bytes = 0;
            foreach (var e in contract.buildings) bytes += e.pngBytes;
            return bytes / 1024f / 1024f;
        }
    }
}
#endif
