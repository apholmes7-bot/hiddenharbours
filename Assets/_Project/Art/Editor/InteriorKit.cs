#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
// Aliased rather than a whole `using HiddenHarbours.Core;`: this file already owns short type names
// (Build, Entry, Contract) that a wholesale import would put at risk of ambiguity for one enum.
using PillowSide = HiddenHarbours.Core.PillowSide;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The INTERIOR KIT — what the building-interiors pilot bakes out of <c>interiorIsoRig.js</c> and
    /// <c>interiorPropRig.js</c>, and the schema of the contract that bake writes beside the sheets.
    /// The sibling of <see cref="VillageBuildingKit"/> in every structural respect, and deliberately so:
    /// a room is the inside of a building this repo already knows how to bake, place and sort.
    ///
    /// <para><b>Serializer == parser.</b> <c>InteriorBakeMenu</c> writes <see cref="Contract"/> with
    /// <see cref="JsonUtility.ToJson(object,bool)"/> and <see cref="Load"/> reads it back, so a bake and
    /// its consumers cannot drift apart. Every number in it came from the rig via
    /// <c>InteriorBakeResult</c>.</para>
    ///
    /// <para><b>⚠️ THE ONE FACT THAT MAKES A ROOM DIFFERENT FROM A BUILDING: the door is on the other
    /// gable.</b> <c>houseIsoRig</c> puts its door on the <c>+Y</c> wall; the room rig puts its doorway
    /// on <c>−Y</c> and gives <c>+Y</c> to the hearth, because a room is entered over the near wall the
    /// open-dollhouse cutaway drops. Both rigs turn the same way — the same <c>camBasis</c>, verified per
    /// facing by <c>InteriorRigAzimuthProbe.ProjectionsAgree</c>, not by reading either header — so the
    /// room that stands under exterior facing <c>f</c> is interior facing
    /// <c>f + <see cref="ExteriorFacingOffset"/></c>. That offset is MEASURED at bake time (the one
    /// constant that lines the two door anchors up at all eight facings) and written into the contract;
    /// <see cref="InteriorFacingFor"/> is the only place anything applies it.</para>
    ///
    /// <para><b>⚠️ The pivot is the FLOOR CENTRE, and it is not bottom-centre.</b> Same trap and same
    /// arithmetic as the building kit: under the ¾ camera the whole near half of the floor projects
    /// BELOW the room's ground centre, so a bottom-centre pivot sinks the room by that much and it
    /// reads as an art bug. <see cref="NormalizedPivot"/> is the one definition, and it is the hull/
    /// building form <c>(cellH − pivotY)/cellH</c>, never the tree kit's <c>pad/cellH</c> (ADR 0026).</para>
    ///
    /// <para><b>The import cap is 2048 and the pack is solved for 2048</b> — see
    /// <see cref="ImportSizeCap"/>, which is where the interesting arithmetic is.</para>
    /// </summary>
    public static class InteriorKit
    {
        /// <summary>Where the room sheets land — the kit's own folder, so nothing here can be mistaken
        /// for the exterior kit's orphan.</summary>
        public const string RoomsRoot = "Assets/_Project/Art/Sprites/Interiors/Rooms/";

        /// <summary>Where the furniture sheets land.</summary>
        public const string PropsRoot = "Assets/_Project/Art/Sprites/Interiors/Props/";

        public const string ContractFileName = "Interiors.json";

        /// <summary>The contract sits one level up, over both folders: rooms and props are baked
        /// together, share a facing convention and a facing offset, and are meaningless apart.</summary>
        public const string ContractPath = "Assets/_Project/Art/Sprites/Interiors/" + ContractFileName;

        public const string RoomStemPrefix = "Interior_";
        public const string PropStemPrefix = "InteriorProp_";

        /// <summary>
        /// The importer texture cap, and — the point — the cap the PACK is solved against.
        /// <c>InteriorBakeRequest.MaxSheetDimension</c> carries it into
        /// <c>BuildingRigBaker.ChooseGrid</c>, so the bake adds a row rather than emitting a sheet the
        /// import would silently shrink. Over the cap Unity downscales and the sprite COUNT still comes
        /// out right, so only a cell-size or pivot assert catches it.
        ///
        /// <para><b>Why this kit does NOT lift the cap, even at 8 facings of a 1180×900 native cell.</b>
        /// The handoff expected a lift, on the arithmetic that eight 1180-px cells in a row is 9440 px.
        /// That is the UNCROPPED number. The union crop is what this baker exists for: a room draws its
        /// floor and its two far walls, so the drawn silhouette is roughly the footprint's diagonal wide
        /// — about <c>(Wd + Ln)/√2 · 32</c> px, which is ~330 px for a cottage and ~400 px for the
        /// biggest preset, inside a native cell sized to hold the largest room at the worst facing. Eight
        /// of those pack 4×2 into well under 2048 in both axes. <c>VillageBuildingKit.ImportSizeCap</c>
        /// gives the reason not to lift on principle (a 4096 texture is the first thing a later mobile
        /// port has to undo — CLAUDE.md rule 7), and this kit has no measured reason to override it.</para>
        ///
        /// <para><b>If a future room genuinely cannot pack under it, the bake THROWS</b> with the cell
        /// size and the cap in the message (<c>ChooseGrid</c>), which is the correct failure: raise this
        /// ONE number and the pack, the import lock and the verify assert all follow it. Two numbers
        /// that have to agree is the bug #352 recorded; one number cannot disagree with itself.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Facings every build bakes. RULED by the owner on 2026-08-05 with the budget question
        /// in front of him: the full eight, same as the exterior kit, because all eight fall out of one
        /// model and a room has to turn in lockstep with the shell over it.</summary>
        public const int Facings = 8;

        public static string RoomStemFor(string key) => RoomStemPrefix + key;
        public static string PropStemFor(string key) => PropStemPrefix + key;

        public static string RoomSheetPath(string key) => RoomsRoot + RoomStemFor(key) + ".png";
        public static string PropSheetPath(string key) => PropsRoot + PropStemFor(key) + ".png";

        public static string RoomSpriteNameFor(string key, int facing) => $"{RoomStemFor(key)}_d{facing}";
        public static string PropSpriteNameFor(string key, int facing) => $"{PropStemFor(key)}_d{facing}";

        // =================================================================================
        //  the option keys — the one defence against the silent-option trap
        // =================================================================================

        /// <summary>
        /// Every option key a ROOM build below is allowed to dial. <c>InteriorKitTests</c> greps each of
        /// these out of <c>interiorIsoRig.js</c> and asserts the build table uses nothing else.
        ///
        /// <para>That test is the whole defence: the rig resolves options as
        /// <c>opts[k] != null ? opts[k] : fallback</c>, so a misspelled key changes nothing and says
        /// nothing. The recorded worked example is <c>winD</c> (the rig's internal field, which does
        /// NOTHING as an option) versus <c>winDensity</c> (the option it reads) — both spellings appear
        /// in the rig source, which is why the grep has to look for the option's own <c>g('key'</c>
        /// call and not merely for the string.</para>
        /// </summary>
        public static readonly string[] RoomOptionKeys =
        {
            "size", "floor", "floorTone", "wall", "paper", "wainscot",
            "windows", "winDensity", "door", "dividers", "hearth", "stairs", "beams", "weather",
        };

        /// <summary>Every option key a PROP build is allowed to dial, grep-verified the same way against
        /// <c>interiorPropRig.js</c>.</summary>
        public static readonly string[] PropOptionKeys =
        {
            "wood", "paint", "fabric", "fabric2", "worktop", "variant", "len", "weather",
        };

        // =================================================================================
        //  WHAT THE PILOT NEEDS — the two hand-written tables in the chain
        // =================================================================================

        /// <summary>One room or prop the kit bakes: which rig options, under a stable key.</summary>
        public readonly struct Build
        {
            /// <summary>Stable key — the sheet stem and the contract key. Append-only.</summary>
            public readonly string Key;

            public readonly string Label;

            /// <summary>For a PROP: the catalogue name <c>PropIso.render(name, …)</c> takes. Null for a
            /// room.</summary>
            public readonly string PropName;

            /// <summary>The rig's own preset name, or null for a dialled build.</summary>
            public readonly string Preset;

            /// <summary>The dialled axes, or null for a preset build.</summary>
            public readonly IReadOnlyDictionary<string, object> Dialled;

            public readonly string Why;

            /// <summary>
            /// ⭐ <b>WHICH END OF THIS PROP THE PILLOW IS ON</b>, in the prop's OWN model frame — the
            /// frame the rig drew it in, before any placement turns it. <see cref="PillowSide.Undeclared"/>
            /// for everything that is not a bed, which is every other row in
            /// <see cref="InteriorKit.PropSet"/>.
            ///
            /// <para><b>Declared, because it cannot be measured.</b> The bake reports a bed as a
            /// <see cref="Entry.propFootprintWidth"/> × <see cref="Entry.propFootprintDepth"/> rectangle,
            /// and that rectangle is identical at both ends. What makes one end the head — a tall
            /// headboard, a short footboard, a pillow — is pixels, at eight facings, on a quilt of the same
            /// fabric. The rig states it for free (<c>interiorPropRig.js</c>: "pillow (head end, −Y
            /// front)"), so it is carried here rather than recovered later, exactly as a building's door
            /// gable is carried rather than found by looking at the wall.</para>
            /// </summary>
            public readonly PillowSide Pillow;

            /// <summary>
            /// How far the pillow's CENTRE sits in from the head end of the prop, in metres — the rig's own
            /// number, not a measurement of the sprite. 0 for anything with no pillow.
            ///
            /// <para>An inset rather than an absolute offset, so it survives a re-bake at a different
            /// size: the reach from the middle of the bed is
            /// <c>depth/2 − inset</c> (<c>BedPillow.PillowReachMetres</c>), and the depth is the bake's
            /// own. One number that means one thing, rather than two that have to agree.</para>
            /// </summary>
            public readonly float PillowInsetMetres;

            Build(string key, string label, string propName, string preset,
                  IReadOnlyDictionary<string, object> dialled, string why,
                  PillowSide pillow = PillowSide.Undeclared, float pillowInsetMetres = 0f)
            {
                Key = key; Label = label; PropName = propName; Preset = preset;
                Dialled = dialled; Why = why;
                Pillow = pillow; PillowInsetMetres = pillowInsetMetres;
            }

            public static Build RoomPreset(string key, string label, string preset, string why) =>
                new Build(key, label, null, preset, null, why);

            public static Build RoomDialled(string key, string label,
                                            Dictionary<string, object> dialled, string why) =>
                new Build(key, label, null, null, dialled, why);

            public static Build Prop(string key, string label, string propName,
                                     Dictionary<string, object> dialled, string why) =>
                new Build(key, label, propName, null, dialled, why);

            /// <summary>
            /// A prop somebody can be laid down on. The one factory that takes a
            /// <see cref="PillowSide"/> — so "a bed" is a shape in this table rather than a string
            /// comparison against the key <c>"bed"</c> scattered across the consumers, and a second bed
            /// build (a bunk, a cot, the camper's berth when its rig lands) is one more row here and
            /// nothing else anywhere.
            /// </summary>
            public static Build Bed(string key, string label, string propName,
                                    Dictionary<string, object> dialled, string why,
                                    PillowSide pillow, float pillowInsetMetres) =>
                new Build(key, label, propName, null, dialled, why, pillow, pillowInsetMetres);

            public bool IsPreset => Preset != null;
            public bool IsProp => PropName != null;

            /// <summary>Is this prop something with a head end? The single test — nothing compares a key
            /// against <c>"bed"</c>.</summary>
            public bool IsBed => Pillow != PillowSide.Undeclared;
        }

        /// <summary>
        /// The rooms the village walks into. The pilot baked ONE — the sage cottage — to prove the
        /// chain; these are the rest of St Peters' houses, which is the whole of "make the school
        /// enterable": a row here, a furnishing list in <c>StPetersInteriors</c>, and the builder
        /// already stands it (nothing in the placement pass names a building).
        ///
        /// <para><b>⚠️ <c>size</c> must match the exterior build's <c>size</c> exactly.</b> That is the
        /// whole 1:1 registration: both rigs compute <c>Wd = 6 + size·2.4</c>, <c>Ln = 7 + size·4.2</c>,
        /// and the bake's registration probe REFUSES if the two resolve different footprints rather
        /// than shimming the difference downstream. <see cref="ExteriorKeyFor"/> is the link, and
        /// <c>InteriorKitTests</c> asserts the sizes agree without baking anything.</para>
        ///
        /// <para><b>⚠️ A ROOM IS ONLY HALF THE PROMISE — THE SHELL HAS TO DRAW ITS DOOR WHERE THIS
        /// ROOM OPENS.</b> A doorway registers to <c>houseIsoRig.anchors().door</c>, which is
        /// DECLARED at the <c>+Y</c> gable centre for every shape and does not follow where the rig
        /// actually draws the door. Three of these four shells had to be re-dialled before they could
        /// be entered at all — see <see cref="VillageBuildingKit.DrawsDoorOnGable"/>, which is the
        /// rule and the measurement. Adding a room to a building that fails that predicate ships a
        /// house whose visible door is solid wall and whose walk-in gap is blank clapboard.</para>
        ///
        /// <para><b>⚠️ <c>dividers</c> is 0 on every row, and that is a COLLISION constraint, not a
        /// taste one.</b> The rig draws a partition as a full-width wall with its own 1.15 m doorway
        /// gap (<c>interiorIsoRig.js:382-396</c>), but <see cref="World.InteriorFootprint.WallQuads"/>
        /// builds the four outer walls and nothing else — so a room dialled with dividers draws
        /// partitions the player walks straight through. Give <c>InteriorFootprint</c> partition
        /// quads first; until then a divider is a wall that is not there.</para>
        /// </summary>
        public static readonly Build[] RoomSet =
        {
            Build.RoomDialled("sageCottage", "Sage cottage — one room", new Dictionary<string, object>
            {
                // MUST equal VillageBuildingKit.M1Set["sageCottage"].size — the registration rests on it.
                ["size"] = 0.25,
                ["floor"] = "plank",
                ["wall"] = "plaster",
                ["paper"] = "cream",
                ["wainscot"] = true,
                // The exterior dials twoOverTwo; the room shows the same sashes from the inside.
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.50,
                ["door"] = true,
                // ONE room. A divider would be a second room to furnish and a second doorway to
                // collide, and neither is what the pilot is proving.
                ["dividers"] = 0,
                // The exterior dials one chimney, so the room gets the hearth that chimney belongs to.
                ["hearth"] = true,
                ["beams"] = true,
                // The exterior is the most weathered of the three houses; the inside is lived in, not
                // derelict, so this is softer than the shell's 0.55.
                ["weather"] = 0.35,
            }, "the pilot room: the inside of the sage cottage the village already places"),

            // ---- the one-room school ------------------------------------------------------------
            // The smallest room in the set (6.36 × 7.63 m) and the only one that is a PUBLIC room, so
            // it is finished like one: wide boards underfoot and vertical shiplap on the walls rather
            // than a house's plaster. Mostly windows, at the shell's own density, because a schoolroom
            // is lit from the sides.
            Build.RoomDialled("school", "One-room school — the schoolroom", new Dictionary<string, object>
            {
                // MUST equal VillageBuildingKit.M1Set["school"].size.
                ["size"] = 0.15,
                ["floor"] = "wideBoard",
                ["wall"] = "board",
                ["paper"] = "cream",
                ["wainscot"] = true,
                // The shell's own sashes and density, so the window count reads the same from inside.
                ["windows"] = "sixOverSix",
                ["winDensity"] = 0.85,
                ["door"] = true,
                // ONE room — it is a one-room school. See the class remarks: a divider is also a wall
                // the collision model does not have.
                ["dividers"] = 0,
                // The shell dials one chimney; this is the stove it belongs to.
                ["hearth"] = true,
                ["beams"] = true,
                // A little more worn than a house: it is scrubbed, but the whole village uses it.
                ["weather"] = 0.30,
            }, "world-and-regions §6: the room where the aunt teaches the compass and hand skills"),

            // ---- the red saltbox ----------------------------------------------------------------
            // The plainest of the three houses outside; inside it takes the one blue paper in the set,
            // so the three houses do not read as one interior repeated. No wainscot — papered walls
            // straight down is the plainer of the two treatments. 6.96 × 8.68 m.
            //
            // ⚠️ `wall` MUST be 'wallpaper' for `paper` to draw anything: the rig picks the paper
            // material only on that finish (interiorIsoRig.js:249). Dialled 'plaster' first and the
            // blue silently did nothing — the room baked cream and read as the cottage twice.
            Build.RoomDialled("redSaltbox", "Red saltbox — the keeping room", new Dictionary<string, object>
            {
                // MUST equal VillageBuildingKit.M1Set["redSaltbox"].size.
                ["size"] = 0.4,
                ["floor"] = "plank",
                ["wall"] = "wallpaper",
                ["paper"] = "blue",
                ["wainscot"] = false,
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.60,
                ["door"] = true,
                ["dividers"] = 0,
                ["hearth"] = true,
                ["beams"] = true,
                ["weather"] = 0.30,
            }, "the plainest house's one room — the middle house of the three"),

            // ---- the white farmhouse ------------------------------------------------------------
            // The biggest room on the island (7.68 × 9.94 m) and the best kept: papered walls, a
            // wainscot, and the softest weathering in the set. The family house, so it should read as
            // the one somebody is house-proud of.
            Build.RoomDialled("whiteFarmhouse", "White farmhouse — the hall",
                new Dictionary<string, object>
            {
                // MUST equal VillageBuildingKit.M1Set["whiteFarmhouse"].size.
                ["size"] = 0.7,
                ["floor"] = "plank",
                ["wall"] = "wallpaper",
                ["paper"] = "sage",
                ["wainscot"] = true,
                ["windows"] = "sixOverSix",
                ["winDensity"] = 0.60,
                ["door"] = true,
                // ⚠️ The rig's `farmhouseHall` preset dials 2 dividers and this room deliberately does
                // not: partitions have no colliders yet (class remarks). This is the room that most
                // wants them back once InteriorFootprint can express one.
                ["dividers"] = 0,
                ["hearth"] = true,
                ["beams"] = true,
                ["weather"] = 0.15,
            }, "the biggest of the three — the one with a family in it"),
        };

        /// <summary>
        /// How far the kit bed's pillow centre sits in from its head end, metres —
        /// <c>interiorPropRig.js</c>'s own <c>pBed()</c>, whose pillow box runs from <c>−d/2 + 0.10</c> to
        /// <c>−d/2 + 0.54</c>. Named here so the one place it is stated is beside the build that uses it,
        /// and so a test can quote it rather than re-typing 0.32.
        /// </summary>
        public const float BedPillowInsetMetres = 0.32f;

        /// <summary>
        /// The furniture the pilot stands in that room. A handful, honestly placed — enough to prove the
        /// prop pipeline (bake → slice → place → collide → Y-sort past), not a decorating pass.
        ///
        /// <para>Each is one sheet of eight facings at the shared 460×460 native cell. They are cheap
        /// next to a room: a chair crops to a fraction of its cell.</para>
        /// </summary>
        public static readonly Build[] PropSet =
        {
            // ⭐ THE PILLOW IS DECLARED, and both numbers below are the RIG's, read out of
            // interiorPropRig.js's own pBed() and not off the baked sprite:
            //   const w=1.5, d=2.05 …
            //   box(…, -d/2+0.1, -d/2+0.54, …, 'fabric2', 0.3);   // pillow (head end, -Y front)
            //   box(…, -d/2,-d/2+0.08, 0,head, 'wood', 0.05);     // headboard (-Y front, tall)
            // So the head end is −Y and the pillow's centre is (0.10 + 0.54)/2 = 0.32 m in from it.
            // Everything downstream — the sleeping heading, the head position the content test checks —
            // comes off these two facts and the bake's own depth. See PillowSide for why this is stated
            // rather than found.
            Build.Bed("bed", "Bed", "bed", new Dictionary<string, object>
            {
                ["wood"] = "walnut", ["fabric"] = "sage", ["fabric2"] = "cream", ["weather"] = 0.2,
            }, "the one prop that makes a room a bedroom",
               PillowSide.MinusY, BedPillowInsetMetres),

            Build.Prop("table", "Table", "table", new Dictionary<string, object>
            {
                ["wood"] = "oak", ["len"] = 0.35, ["weather"] = 0.3,
            }, "the thing the owner is asked to walk BEHIND — the Y-sort proof"),

            Build.Prop("chair", "Chair", "chair", new Dictionary<string, object>
            {
                ["wood"] = "oak", ["weather"] = 0.3,
            }, "two of them at the table; the smallest footprint in the set, so the collision maths " +
               "is exercised at both ends"),

            Build.Prop("dresser", "Dresser", "dresser", new Dictionary<string, object>
            {
                ["paint"] = "blue", ["wood"] = "pine", ["weather"] = 0.25,
            }, "against a wall — proves a prop can sit flush without fighting the wall collider"),

            Build.Prop("seaChest", "Sea chest", "seaChest", new Dictionary<string, object>
            {
                ["wood"] = "walnut", ["weather"] = 0.45,
            }, "a fisherman's cottage has one; also the shortest prop, which is the sorting edge case"),
        };

        /// <summary>The room build for a key, or null.</summary>
        public static Build? FindRoom(string key)
        {
            foreach (var b in RoomSet) if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        /// <summary>The prop build for a key, or null.</summary>
        public static Build? FindProp(string key)
        {
            foreach (var b in PropSet) if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        /// <summary>
        /// The EXTERIOR build a room belongs inside. Keys are shared on purpose: a room named
        /// <c>sageCottage</c> is the inside of the village building named <c>sageCottage</c>, and a
        /// test asserts the two resolve the same <c>size</c>. Returns null for a room with no shell.
        /// </summary>
        public static string ExteriorKeyFor(string roomKey) =>
            VillageBuildingKit.FindBuild(roomKey) != null ? roomKey : null;

        // =================================================================================
        //  the contract schema
        // =================================================================================

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string writtenBy;

            /// <summary>Pixels per metre the sheets were baked at — the rigs' own <c>PX</c>.</summary>
            public int ppu;

            public int facings;

            /// <summary>
            /// ⭐ Add this to an EXTERIOR facing to get the interior facing that stands under it.
            /// MEASURED at bake time by <c>InteriorRigAzimuthProbe.MeasureRegistration</c> — the one
            /// constant that puts the room's doorway on the shell's door at ALL eight facings. A rig
            /// that turned the other way admits no such constant, so this number is the handedness
            /// proof as well as the offset.
            /// </summary>
            public int exteriorFacingOffset;

            public string conventionNote;
            public string pivotNote;
            public string registrationReport;

            public Entry[] rooms;
            public Entry[] props;
        }

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string label;

            /// <summary>Catalog rig key and the global it installs — recorded so a re-bake against a
            /// changed rig is visible in the diff.</summary>
            public string rig;
            public string rigScript;
            public string rigGlobal;

            /// <summary>For a prop: the catalogue name the rig takes. Empty for a room.</summary>
            public string propName;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            /// <summary>The EXACT options expression <c>render()</c> was handed — the audit trail.</summary>
            public string optionsJs;

            public int facings;
            public int cols;
            public int rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW;
            public int cellH;

            /// <summary>The rig's native cell, before the union crop.</summary>
            public int nativeCellW;
            public int nativeCellH;

            public int cropX;
            public int cropY;

            /// <summary>The FLOOR CENTRE, in cropped-cell px from the cell's TOP-LEFT.</summary>
            public float pivotX;
            public float pivotY;

            /// <summary>The Unity sprite pivot: normalised, BOTTOM-origin. Equals
            /// <c>(pivotX/cellW, (cellH − pivotY)/cellH)</c> — see <see cref="NormalizedPivot"/>.</summary>
            public float unityPivotX;
            public float unityPivotY;

            public int sheetW;
            public int sheetH;

            /// <summary>ROOM: the footprint in metres, as the rig reports it. The honest-scale number
            /// and what the wall colliders are built from.</summary>
            public float footprintWidthMetres;
            public float footprintLengthMetres;

            /// <summary>PROP: <c>footprint(name,opts)</c> in metres — what a placer collides by.</summary>
            public float propFootprintWidth;
            public float propFootprintDepth;

            public string convention;

            /// <summary>ROOM, per facing, in cropped-cell px, top-left origin: the doorway (the
            /// threshold the player crosses), the floor centre and the hearth (NaN where the build has
            /// none — zero is a legal coordinate, so a missing hearth written as zero would put a fire
            /// in the corner).</summary>
            public float[] doorX;
            public float[] doorY;
            public float[] floorX;
            public float[] floorY;
            public float[] hearthX;
            public float[] hearthY;

            public long pngBytes;
            public long runtimeBytesRgba32;
            public float cropSaving;
        }

        // =================================================================================
        //  reading it back
        // =================================================================================

        /// <summary>The committed contract, or null with a logged error if it is missing/unparseable.</summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[InteriorKit] No contract at '{ContractPath}'. Run Hidden Harbours ▸ Art ▸ Bake " +
                    "Interiors — the sheets and this file are written by the same bake and are only " +
                    "meaningful together.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.rooms == null || contract.rooms.Length == 0)
            {
                Debug.LogError($"[InteriorKit] '{ContractPath}' parsed but carries no rooms.");
                return null;
            }
            return contract;
        }

        public static Entry FindRoom(Contract c, string key) => FindIn(c?.rooms, key);
        public static Entry FindProp(Contract c, string key) => FindIn(c?.props, key);

        static Entry FindIn(Entry[] entries, string key)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (string.Equals(e.key, key, StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>The room entry a sheet stem belongs to, or null for a stranger (which must fail, not
        /// be guessed at).</summary>
        public static Entry RoomForStem(Contract c, string stem)
        {
            if (c?.rooms == null) return null;
            foreach (var e in c.rooms)
                if (string.Equals(stem, RoomStemFor(e.key), StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>The prop entry a sheet stem belongs to, or null for a stranger.</summary>
        public static Entry PropForStem(Contract c, string stem)
        {
            if (c?.props == null) return null;
            foreach (var e in c.props)
                if (string.Equals(stem, PropStemFor(e.key), StringComparison.Ordinal)) return e;
            return null;
        }

        // =================================================================================
        //  the arithmetic (pure — a test asserts all of it without an asset on disk)
        // =================================================================================

        /// <summary>
        /// The Unity sprite pivot for an entry: normalised, BOTTOM-origin, from the FLOOR CENTRE.
        ///
        /// <para>⚠️ The y term is <c>(cellH − pivotY)/cellH</c> — the hull/building form, <b>not</b> the
        /// tree kit's <c>pad/cellH</c>. Settled and measured (ADR 0026): a rig's <c>pivot</c> is a
        /// CONTINUOUS point whose origin is the cell's top-left corner, and a room's floor centre is a
        /// projected POINT like a hull's, not a chosen ROW like a tree's trunk foot.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(Entry e) =>
            new Vector2(e.pivotX / e.cellW, (e.cellH - e.pivotY) / e.cellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left, which is what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(Entry e) => new Vector2(e.pivotX, e.cellH - e.pivotY);

        /// <summary>How many rows of art sit BELOW the floor centre: the near half of the floor as the ¾
        /// camera projects it. Exactly the distance a bottom-centre pivot would sink the room.</summary>
        public static int BelowGroundPad(Entry e) => Mathf.Max(0, e.cellH - Mathf.RoundToInt(e.pivotY));

        /// <summary>
        /// ⭐ The interior facing that stands under an exterior facing. The ONE place the measured offset
        /// is applied.
        ///
        /// <para>The exterior rigs put the door on <c>+Y</c> and the room rig on <c>−Y</c>, so the two
        /// depict the same building 180° apart at the same cell index. Placing a room under a shell at
        /// the SAME index would put the doorway against the back wall: the player would walk in the
        /// front door and appear at the back of the room, and the art would look like the bug.</para>
        /// </summary>
        public static int InteriorFacingFor(int exteriorFacing, int offset, int facings)
        {
            if (facings <= 0) return 0;
            return ((exteriorFacing + offset) % facings + facings) % facings;
        }

        /// <summary>The same, reading the offset and the facing count off the contract.</summary>
        public static int InteriorFacingFor(Contract c, int exteriorFacing) =>
            c == null ? 0 : InteriorFacingFor(exteriorFacing, c.exteriorFacingOffset, c.facings);

        /// <summary>The full drawn width of a cell in metres.</summary>
        public static float DrawnWidthMetres(Entry e, int ppu) =>
            e == null || ppu <= 0 ? 0f : (float)e.cellW / ppu;

        /// <summary>How far below the floor centre the drawn art reaches, in metres — the exact distance
        /// a bottom-centre pivot would sink the room.</summary>
        public static float BelowGroundMetres(Entry e, int ppu) =>
            e == null || ppu <= 0 ? 0f : (float)BelowGroundPad(e) / ppu;

        /// <summary>
        /// A doorway anchor in WORLD metres, relative to the room's own transform (whose position IS the
        /// floor centre, because that is the pivot).
        ///
        /// <para>The contract stores the anchor in cropped-cell px from the cell's TOP-LEFT, and the
        /// pivot in the same space, so the offset is <c>(door − pivot)/ppu</c> with the y term negated:
        /// screen y grows DOWN and world y grows UP. Getting that flip wrong puts the threshold on the
        /// far wall, which is the same wrong place the facing offset guards against and would look
        /// identical.</para>
        /// </summary>
        public static Vector2 DoorOffsetMetres(Entry e, int facing, int ppu)
        {
            if (e?.doorX == null || facing < 0 || facing >= e.doorX.Length || ppu <= 0)
                return Vector2.zero;
            return new Vector2((e.doorX[facing] - e.pivotX) / ppu,
                               -(e.doorY[facing] - e.pivotY) / ppu);
        }

        /// <summary>Total runtime texture cost of the kit, in MiB of RGBA32 — reported, not presumed.</summary>
        public static float TotalRuntimeMib(Contract c)
        {
            long bytes = 0;
            if (c?.rooms != null) foreach (var e in c.rooms) bytes += e.runtimeBytesRgba32;
            if (c?.props != null) foreach (var e in c.props) bytes += e.runtimeBytesRgba32;
            return bytes / 1024f / 1024f;
        }

        /// <summary>Total committed PNG size of the kit, in MiB — what the repo actually carries.</summary>
        public static float TotalPngMib(Contract c)
        {
            long bytes = 0;
            if (c?.rooms != null) foreach (var e in c.rooms) bytes += e.pngBytes;
            if (c?.props != null) foreach (var e in c.props) bytes += e.pngBytes;
            return bytes / 1024f / 1024f;
        }

        /// <summary>Read the sprite mesh type an importer will use — it lives on
        /// <see cref="TextureImporterSettings"/>, not on the importer.</summary>
        public static SpriteMeshType MeshTypeOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.spriteMeshType;
        }

        /// <summary>sRGB also lives on <see cref="TextureImporterSettings"/>.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }
    }
}
#endif
