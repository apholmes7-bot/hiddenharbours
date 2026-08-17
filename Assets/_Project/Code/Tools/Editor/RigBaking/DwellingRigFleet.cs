#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE PARKED DWELLINGS — what the repo has, and what it does with each of them.</b>
    ///
    /// <para>The third coverage table, after <see cref="HullMeshFleet"/> (boats) and
    /// <see cref="VehicleRigFleet"/> (road vehicles), and it exists for the same reason the second one
    /// did: <b>neither of the other two can see a camper.</b></para>
    ///
    /// <list type="bullet">
    ///   <item><b>The hull law cannot.</b> Its coverage test scans <c>docs/art/rigs/</c> for rigs
    ///   containing <c>rollA</c> — a hull's sea-rock amplitude. <c>camperIsoRig.js</c> contains it zero
    ///   times, correctly: a parked trailer does not rock on a swell. So a camper drop is INVISIBLE to
    ///   it and would go silently unbaked.</item>
    ///   <item><b>The vehicle law cannot either.</b> Its population is defined by the sidecar's own
    ///   top-level <c>"kind": "road_vehicle"</c>, and the camper's sidecar declares <b>no top-level
    ///   <c>kind</c> at all</b> — measured in the repo's own V8 against the rig's
    ///   <c>gameplayGeometry()</c> generator, not just read off the shipped file. A camper is also not
    ///   a road vehicle in the sense that table means: it has no cab, no engine and nothing that
    ///   drives. Its own <c>_excluded</c> block calls it a <i>"Parked dwelling"</i>.</item>
    /// </list>
    ///
    /// <para><b>⚠ THE SIGNAL HERE IS THE FOLDER, AND THAT IS A COMPROMISE — say so rather than dress it
    /// up.</b> <see cref="VehicleRigFleet"/> keys on art's own word for what a thing is, which is the
    /// better rule. This table cannot: the drop publishes no such word. So membership is this explicit
    /// list plus the sidecar folder, and <b>the upstream ask is for the camper sidecars to carry a
    /// top-level <c>kind</c></b> (<c>"dwelling"</c> would do) so this table can key on the same kind of
    /// signal its sibling does instead of on where a file was filed.</para>
    ///
    /// <para><b>Every dwelling is either BAKED or carries a REASON.</b> A new drop that is neither
    /// fails <c>DwellingRigFleetTests</c> — art arrives by PR and this is the thing that stops one
    /// arriving unnoticed. That is the whole job of the table.</para>
    /// </summary>
    public static class DwellingRigFleet
    {
        /// <summary>
        /// Where a parked dwelling's gameplay sidecar lives — a sibling of the vehicles' subfolder,
        /// repo-relative.
        ///
        /// <para><b>⚠ A SUBFOLDER, AND IT IS LOAD-BEARING — the Dually learned this the hard way.</b>
        /// Putting these sidecars straight into <c>docs/art/rigs/gameplay/</c> beside the boats' would
        /// red <c>DeckSidecarImportParityTests</c>, which enumerates <b>every</b> <c>*.gameplay.json</c>
        /// in that folder and requires each to parse as a boat deck with a committed
        /// <c>BoatDeckDef</c> behind it. That law is right and the folder simply means "boat deck
        /// sidecars".</para>
        ///
        /// <para>A camper sidecar is a different document — <c>SOLE</c>, <c>THRESHOLD</c>,
        /// <c>STEP</c>, <c>HOOKUPS</c> — and its own <c>_excluded</c> says <i>"WASHBOARD: Not a hull.
        /// No side decks."</i> <c>Directory.GetFiles</c> is not recursive, so the three populations
        /// stay separate by construction and no fixture needs to know about the others.</para>
        /// </summary>
        public const string SidecarFolder = "docs/art/rigs/gameplay/dwellings";

        /// <summary>One parked dwelling: where its rig and sidecar are, and what installs it.</summary>
        public readonly struct Dwelling
        {
            /// <summary>Stable key — the sidecar's own <c>variant</c>, so the file and the table cannot
            /// drift apart.</summary>
            public readonly string Key;
            /// <summary>Repo-relative path to the rig <c>.js</c>.</summary>
            public readonly string ScriptPath;
            /// <summary>Repo-relative path to the gameplay sidecar.</summary>
            public readonly string SidecarPath;
            /// <summary>The global the rig's IIFE installs.</summary>
            public readonly string GlobalName;

            public Dwelling(string key, string scriptPath, string sidecarPath, string globalName)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
            }
        }

        /// <summary>Repo-relative path to the camper rig — ONE rig for both lengths, which is why the
        /// two entries below share it. The README is explicit that the variants are "the same loft
        /// re-run, not two models".</summary>
        public const string CamperRigPath = "docs/art/rigs/camper-iso-kit/camperIsoRig.js";

        /// <summary>
        /// Every parked dwelling whose rig and sidecar are committed. Being here means the drop has
        /// LANDED and is hash-verified — it does <b>not</b> mean it is baked to sheets. What is baked
        /// is <see cref="Baked"/>; why anything is not is <see cref="NotBaked"/>.
        /// </summary>
        public static readonly IReadOnlyList<Dwelling> Dwellings = new[]
        {
            new Dwelling(
                "bantam",
                CamperRigPath,
                SidecarFolder + "/camperIsoRig.bantam.gameplay.json",
                "CamperIso"),
            new Dwelling(
                "clipper",
                CamperRigPath,
                SidecarFolder + "/camperIsoRig.clipper.gameplay.json",
                "CamperIso"),
        };

        /// <summary>
        /// Dwellings that currently have baked sheets. <b>Empty, on purpose</b> — see
        /// <see cref="NotBaked"/>.
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = Array.Empty<string>();

        /// <summary>
        /// ⭐ <b>Registered refusals: a dwelling that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a dwelling in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para>Both entries share one reason, and it is a plain fact from the kit rather than a
        /// judgement: <b>the drop ships no sheets and no harness to bake them from.</b></para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bantam"] = NoSheetsReason,
                ["clipper"] = NoSheetsReason,
            };

        const string NoSheetsReason =
            "NOT baked, and the kit says why in its own words: \"No harness or baked sheets in this " +
            "kit. The rig and its two sidecars are the whole export; bake sheets from the demo page.\" " +
            "Every other rig drop this repo has taken arrived with a harness.html and reference sheets " +
            "(see docs/art/rigs/dually-iso-kit/); this one did not, so there is nothing to slice and " +
            "no committed image to check a bake against.\n" +
            "\n" +
            "The rig itself is sound and that was MEASURED, not assumed — CamperIsoKitProbeTests runs " +
            "it in the repo's own V8 and reproduces all sixteen of the README's per-facing crop boxes " +
            "exactly, plus the drawbar overhang below the pivot (61 px bantam, 94 px clipper at S). So " +
            "this is a missing-artefact blocker, not a broken-rig one, and the bake is a follow-up that " +
            "needs no new mechanism.\n" +
            "\n" +
            "⚠ TWO THINGS THE BAKER WILL HIT, both measured:\n" +
            "  1. render(dir, opts) takes an INTEGER INDEX 0..7, not the compass string the README " +
            "talks in — camBasis does dir*PI/4, so render('N') makes the angle NaN and returns a fully " +
            "TRANSPARENT cell with no error. A baker that loops over order[] by name bakes 8 empty " +
            "sheets and nothing says so.\n" +
            "  2. The Clipper fits an AWNING by default, and it occludes the door's own enter cue at " +
            "exactly the facings the door is supposed to read at. Measured: the swing-0-to-1 pixel " +
            "delta at W is 253 with the awning and 1535 without. Bake the cue with the awning off, or " +
            "the enter animation is a handful of pixels.\n" +
            "\n" +
            "⚠ AND THE KIT HAS NO INTERIOR: \"No interior. This is the shell.\" The room belongs to " +
            "camperInteriorRig.js, which is NOT in this drop. A camper used as a HOME needs it before " +
            "it can have a seamless interior (the owner's 2026-07-30 ruling) — that is an upstream art " +
            "ask, not something placement can work around.";
    }
}
#endif
