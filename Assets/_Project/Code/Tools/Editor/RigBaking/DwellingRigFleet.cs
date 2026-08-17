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
        /// Dwellings that currently have baked sheets.
        ///
        /// <para>Both lengths, as of the camper bake. Each gets TWO sheets — a <c>rest</c> at the
        /// variant's own default fit-out and an <c>enter</c> carrying the door cue — laid out columns
        /// = swing frames, rows = facings. <see cref="HiddenHarbours.Art.Editor.CamperKit"/> owns the
        /// build table, <c>CamperSheetBaker</c> writes them and <c>CamperBakeTests</c> checks that this
        /// list is a claim with files behind it.</para>
        ///
        /// <para><b>Both of the traps the earlier refusal recorded are now guards in the baker rather
        /// than warnings in a comment</b>, each with a sabotage test that fires it: a compass string
        /// for <c>dir</c> trips an opaque-pixel floor (<c>MinOpaquePixelsPerCell</c>), and an awninged
        /// enter build trips a cue-motion floor (<c>MinCueDeltaPx</c>).</para>
        ///
        /// <para>⚠ <b>What the bake did NOT solve, because it is upstream art:</b> the kit still has
        /// no interior — <i>"No interior. This is the shell."</i> The room belongs to
        /// <c>camperInteriorRig.js</c>, which is not in the repo. A camper used as a HOME needs it
        /// before it can have a seamless interior (the owner's 2026-07-30 ruling); no amount of
        /// placement or baking works around a rig that was never dropped.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = new[] { "bantam", "clipper" };

        /// <summary>
        /// ⭐ <b>Registered refusals: a dwelling that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a dwelling in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>Empty, and an entry is DELETED rather than reworded when its dwelling is baked</b> —
        /// a refusal that outlives its cause goes on explaining a blocker somebody already cleared.
        /// The camper's two entries lived here between its import and its bake; what they recorded that
        /// still matters has moved into <see cref="Baked"/> and into the baker's own guards.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
#endif
