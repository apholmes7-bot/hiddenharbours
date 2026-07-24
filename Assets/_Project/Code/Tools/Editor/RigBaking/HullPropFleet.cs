using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One articulated fitting the baker knows how to bake, and which hull visuals wear it.
    /// </summary>
    public readonly struct FleetProp
    {
        public readonly string Key;
        public readonly string ScriptPath;
        public readonly string GlobalName;
        public readonly string AssetPath;
        public readonly string PropId;

        /// <summary>How to reach this fitting's geometry inside its rig — the builder call at its
        /// canonical pose, the pivot, the cell, and the private symbols that must be shimmed.</summary>
        public readonly RigPropExtraction Extraction;

        /// <summary>The <c>BoatVisualDef</c>s that wear this fitting, and the slot name each wears it
        /// in. One fitting, many boats: the same outboard bolts to the console skiff and both sport
        /// skiffs, which is exactly why fittings are their own assets.</summary>
        public readonly (string visualAssetPath, string slot)[] WornBy;

        public readonly string Label;

        public FleetProp(string key, string scriptPath, string globalName, string assetPath,
                         string propId, RigPropExtraction extraction,
                         (string, string)[] wornBy, string label)
        {
            Key = key;
            ScriptPath = scriptPath;
            GlobalName = globalName;
            AssetPath = assetPath;
            PropId = propId;
            Extraction = extraction;
            WornBy = wornBy;
            Label = label;
        }
    }

    /// <summary>
    /// <b>The fittings that let the small boats go mesh (ADR 0022 phase 7).</b>
    ///
    /// <para>Phase 6 baked all eleven hulls and could present only seven. The gate was never the
    /// hulls — it was that the dory's oars and the skiffs' outboards are baked one sprite cell per
    /// facing, and a mesh hull turns continuously, so there is no cell to look up. Flipping those
    /// boats without their fittings is how you ship a rowboat with no oars, and the PlayMode suite
    /// says so in four red tests. This table is the fittings becoming meshes on the same terms the
    /// hulls did.</para>
    ///
    /// <para><b>What the mesh path deletes.</b> The sprite fittings carry two whole mechanisms that
    /// exist only because sprites cannot interleave in depth: the <c>upper</c>/<c>lower</c> part split
    /// (so the engine's leg can be drawn UNDER the hull on stern-away headings, the rigs'
    /// <c>MOTOR.behind=[3,4,5]</c>), and the twin outboard's draw-the-far-engine-first ordering. A
    /// fitting parented to a mesh hull joins the same renderer list and writes the same depth buffer,
    /// so both are simply true. Neither is transcribed below, on purpose.</para>
    /// </summary>
    public static class HullPropFleet
    {
        const string Rigs = "docs/art/rigs";
        const string PropFolder = "Assets/_Project/Data/Boats/HullProps";
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>
        /// The dory's oars, one asset per side.
        ///
        /// <para><b>Why per side rather than one mirrored asset.</b> Port and starboard ARE mirror
        /// images in the rig (<c>oarlockPt</c> and <c>oarDir</c> both carry a <c>side</c> factor on
        /// x), so one mesh drawn with a negative scale would look right — and shade wrong, because
        /// mirroring reverses triangle winding and inverts every normal against a key light that is
        /// FIXED in screen space. Two honest bakes cost 30 KB and no thought.</para>
        ///
        /// <para><b>The canonical pose is (0, 0), and it is not reachable through the rig's public
        /// pose API.</b> <c>oarPose('row', t)</c> traces an ellipse — <c>sweep=30·sin</c>,
        /// <c>dip=6+22·cos</c> — that never passes through sweep 0 with dip 0. Baking anywhere on
        /// that ellipse would make the runtime rotation a rotation away from an arbitrary point,
        /// which is a needless invitation to sign errors. So the bake calls <c>buildOar</c> directly
        /// with the pose it wants, and takes the shim cost of one more private symbol.</para>
        /// </summary>
        static FleetProp Oar(int side, string sideName, string slot) => new FleetProp(
            key: $"doryOar{sideName}",
            scriptPath: $"{Rigs}/doryIsoRig.js",
            globalName: "DoryIso",
            assetPath: $"{PropFolder}/DoryOar{sideName}PropMesh.asset",
            propId: $"hullprop.dory_oar_{sideName.ToLowerInvariant()}",
            extraction: new RigPropExtraction
            {
                FaceBuilderCall = $"buildOar({side},{{sweep:0,dip:0}})",
                PivotCall = $"oarlockPt({side})",
                // Her oars are drawn through the same camera and pivot as her hull — the rig says so
                // ("separate overlay layer, same camera + pivot as the hull") — so no cell override.
                CellPath = "",
                ExtraSymbols = new[] { "buildOar", "oarlockPt" },
            },
            wornBy: new[] { ("DoryIso", slot) }
                .Select(w => ($"{Visuals}/{w.Item1}.asset", w.Item2)).ToArray(),
            label: $"dory oar, {sideName.ToLowerInvariant()} ({(side < 0 ? "port" : "starboard")})");

        /// <summary>
        /// <b>An outboard, one asset per paint build.</b> Two rigs, four builds, five boats: the punt
        /// carries her own tiller engine (<c>basic</c> / <c>upgraded</c> — a real gameplay upgrade, so
        /// the choice is DATA on the visual, never a constant here), and <c>skiffMotorRig.js</c>'s one
        /// remote-steer four-stroke (<c>work</c> / <c>sport</c>) fits BOTH skiff hulls.
        ///
        /// <para><b>The canonical pose is dead ahead and untilted, and it IS reachable.</b> Unlike the
        /// dory's oar, whose stroke ellipse never passes through (0,0), <c>motorFaces</c> takes steer
        /// and tilt directly and both rigs' <c>mxform</c> collapses to the identity placement
        /// <c>I(p) = [mx+x, YA+y, z]</c> at (0, 0) — which is exactly the transform the untilted,
        /// unsteered faces are already built through. The builder is still called directly rather than
        /// through <c>renderMotor</c>, because <c>renderMotor</c> returns PIXELS.</para>
        ///
        /// <para><b>What the pose is a rotation ABOUT.</b> Both rigs tilt about the line
        /// <c>{y = 0, z = ZT}</c> and steer about the vertical through <c>{x = 0, y = 0}</c>, then
        /// place the result at <c>(mx, YA, ·)</c>. So every reachable pose is
        /// <c>P + m + R·(v − P)</c> about <b>P = (0, YA, ZT)</b> — read off the rig by the
        /// <c>swivelPt</c> reconstruction, never written as a constant at a call site.</para>
        ///
        /// <para><b>Two things deliberately NOT transcribed.</b> The rigs' <c>MOTOR.parts</c>
        /// upper/lower split (and <c>MOTOR.behind = [3,4,5]</c>) and the twin's draw-the-far-engine-
        /// first rule exist ONLY because two sprites cannot interleave in depth. A fitting parented to
        /// a mesh hull writes the same depth buffer, so both are simply true, and the bake takes
        /// <c>part:'all'</c> — one mesh for the whole engine.</para>
        /// </summary>
        static FleetProp Motor(string key, string rig, string global, string assetName, string propId,
                               string faceCall, string probeCall, string[] extraSymbols,
                               string lateralMountsCall, bool backfaceRescueNeedsOptIn, string label,
                               params string[] wornByVisuals) => new FleetProp(
            key: key,
            scriptPath: $"{Rigs}/{rig}",
            globalName: global,
            assetPath: $"{PropFolder}/{assetName}.asset",
            propId: propId,
            extraction: new RigPropExtraction
            {
                FaceBuilderCall = faceCall,
                // ⚠️ AN OUTBOARD IS NOT ONE RIGID BODY. Both rigs build the clamp bracket (and the
                // skiff's tilt-tube cap) through the identity placement I rather than the posed X —
                // it is bolted to the transom and the engine swivels ON it. This second build, at a
                // pose with BOTH articulation axes off zero, is how the bake finds that out by
                // measurement instead of by reading the rig and declaring it.
                PoseProbeFaceBuilderCall = probeCall,
                PivotCall = "swivelPt()",
                // ⚠️ The engine ships its OWN, WIDER cell — 212×168 against the punt's 184×168 hull,
                // 272×216 against the skiffs' 244×216 — because it swings outboard of the transom.
                // Reading the hull's cell here would crop the cowl on hard-over headings.
                CellPath = "MOTOR",
                ExtraSymbols = extraSymbols,
                MaxSteerCall = "MOTOR.maxSteer",
                MaxTiltCall = "MOTOR.tiltMax",
                LateralMountsCall = lateralMountsCall,
                BackfaceRescueNeedsOptIn = backfaceRescueNeedsOptIn,
                DepthEdgeDarkening = false,      // both renderMotor entry points pass doEdge = false
            },
            wornBy: wornByVisuals.Select(v => ($"{Visuals}/{v}.asset", "Motor")).ToArray(),
            label: label);

        /// <summary>The punt's tiller outboard. Her rig owns it (own cell 212×168, own ±32° authority,
        /// own <c>basic</c>/<c>upgraded</c> builds) — it is not the skiffs' engine at another size.</summary>
        static FleetProp PuntMotor(string variant, string name, string wornBy) => Motor(
            key: $"puntMotor{name}",
            rig: "puntIsoRig.js", global: "PuntIso",
            assetName: $"PuntMotor{name}PropMesh",
            propId: $"hullprop.punt_motor_{variant}",
            faceCall: $"motorFaces({{variant:'{variant}',steer:0,tilt:0,part:'all'}})",
            probeCall: $"motorFaces({{variant:'{variant}',steerDeg:11,tilt:7,part:'all'}})",
            extraSymbols: new[] { "motorFaces", "swivelPt" },
            lateralMountsCall: null,          // single engine, on the centreline — by the art
            backfaceRescueNeedsOptIn: true,   // puntIsoRig.js:152 gates the rescue on b <= -1
            label: $"punt outboard, {variant} (tiller, ±32°)",
            wornBy);

        /// <summary>The skiffs' remote-steer four-stroke — ONE rig fitting both 7 m hulls, which is
        /// precisely why fittings are their own assets rather than fields on a hull.</summary>
        static FleetProp SkiffMotor(string variant, string name, params string[] wornBy) => Motor(
            key: $"skiffMotor{name}",
            rig: "skiffMotorRig.js", global: "SkiffMotor",
            assetName: $"SkiffMotor{name}PropMesh",
            propId: $"hullprop.skiff_motor_{variant}",
            faceCall: $"motorFaces({{variant:'{variant}',steer:0,tilt:0,mx:0,part:'all'}})",
            probeCall: $"motorFaces({{variant:'{variant}',steerDeg:11,tilt:7,mx:0,part:'all'}})",
            // PX and defaultElev are not on this rig's export at all — it is a LAYER, not a hull. Both
            // are reconstructed from its own S / DEFAULT_ELEV (RigMeshSymbols.Reconstructions).
            extraSymbols: new[] { "motorFaces", "swivelPt", "PX", "defaultElev" },
            lateralMountsCall: "MOTOR.mounts.dual",
            backfaceRescueNeedsOptIn: false,  // skiffMotorRig.js:169 rescues EVERY back-facing face
            label: $"skiff outboard, {variant} (remote steer, ±30°)",
            wornBy);

        public static readonly IReadOnlyList<FleetProp> All = new[]
        {
            Oar(-1, "Port", "OarPort"),
            Oar(+1, "Star", "OarStar"),

            PuntMotor("basic", "Basic", "PuntIsoBasic"),
            PuntMotor("upgraded", "Upgraded", "PuntIsoUpgraded"),

            SkiffMotor("work", "Work", "ConsoleSkiff"),
            // ONE bake, TWO boats — and the twin is not a third: it is this same def instantiated
            // once per entry of its LateralMountsMeters (±0.34 m), which the rig states and the bake
            // reads. No second mesh, and no ordering rule between them.
            SkiffMotor("sport", "Sport", "SportSkiffSingle", "SportSkiffTwin"),
        };

        public static FleetProp Get(string key)
        {
            foreach (var p in All) if (p.Key == key) return p;
            throw new ArgumentException(
                $"No fitting '{key}' in the prop catalog. Known: {string.Join(", ", All.Select(p => p.Key))}.");
        }
    }
}
